using ShadowPeer.DataModels;
using ShadowPeer.Helpers;
using Spectre.Console;
using System.Diagnostics;

namespace ShadowPeer.Core;

internal class AnnounceEngine : IDisposable
{
    private long _sessionBytesToUpload = 0;
    private long _sessionBytesUploaded = 0;
    private long _sessionBytesToDownload = 0;
    private long _sessionBytesDownloaded = 0;

    private readonly TorrentMetadatas _torrentMeta;
    private readonly ClientSignature _clientSignature;
    private readonly AnnounceBuilder _announceBuilder;
    private readonly PeerTrafficSim _peerTrafficSim;
    private TrackerResponse? _lastTrackerResponse;

    // Default tracker interval is 30 minutes, can be adjusted based on tracker response
    private TimeSpan _trackerInterval = TimeSpan.FromMinutes(30);
    private readonly Stopwatch _trackerIntervallTimer = new();
    private AnnounceEngineState _state = AnnounceEngineState.Ready;
    private CancellationTokenSource? _cts = new();
    private bool _isDisposed = false;

    private CancellationToken Token => _cts?.Token ?? throw new InvalidOperationException("CancellationTokenSource is null.");

    private enum AnnounceEngineState { Ready, Starting, Running, Stopped, Error }

    public AnnounceEngine(TorrentMetadatas metas, ClientSignature signature, AnnounceBuilder announceBuilder, PeerTrafficSim trafficSim)
    {
        ArgumentNullException.ThrowIfNull(metas, nameof(metas));
        ArgumentNullException.ThrowIfNull(signature, nameof(signature));
        ArgumentNullException.ThrowIfNull(announceBuilder, nameof(announceBuilder));
        ArgumentNullException.ThrowIfNull(trafficSim, nameof(trafficSim));

        _torrentMeta = metas;
        _clientSignature = signature;
        _announceBuilder = announceBuilder;
        _peerTrafficSim = trafficSim;
    }

    public async Task StartEngineAsync()
    {
        if (_state != AnnounceEngineState.Ready)
            throw new InvalidOperationException("Announce engine is not in a ready state.");

        _state = AnnounceEngineState.Starting;

        await FirstAnnounce(_announceBuilder);
        _trackerIntervallTimer.Restart();
        _state = AnnounceEngineState.Running;

        StartKeysListner();

        while (!Token.IsCancellationRequested)
        {
            _peerTrafficSim.Tick();

            var nextAnnounce = _trackerInterval - _trackerIntervallTimer.Elapsed;

            if (nextAnnounce <= TimeSpan.Zero && _lastTrackerResponse is not null && _lastTrackerResponse.Interval.HasValue)
            {
                await SendHeartBeatAnnounce(_announceBuilder, _lastTrackerResponse);
                _trackerIntervallTimer.Restart();
            }

            await Task.Delay(1000, Token);
        }
    }

    public async Task FirstAnnounce(AnnounceBuilder builder)
    {
        try
        {
            builder
                .WithEvent("started")
                .WithKey(_clientSignature.Key)
                .WithPeerId(_clientSignature.PeerId)
                .WithInfoHash(_torrentMeta.InfoHashBytes)
                .WithStats(0, 0, 0);

            string announceUrl = builder.Build();

            AnsiConsole.Clear();
            AnsiConsole.MarkupLine($"[yellow]Announcing to tracker: {announceUrl}[/]");
            AnsiConsole.MarkupLine("[yellow]Press any key to continue...[/]");
            Console.ReadKey();

            if (string.IsNullOrWhiteSpace(announceUrl))
                throw new InvalidOperationException("Failed to build announce URL.");

            var trackerResponse = await Network.SendAnnounceOverTCPAsync(_torrentMeta.Host, _torrentMeta.Port, announceUrl)
                ?? throw new InvalidOperationException("Tracker response is null.");

            if (trackerResponse.Interval.HasValue)
                _trackerInterval = TimeSpan.FromSeconds(trackerResponse.Interval.Value);
            else
                throw new InvalidOperationException("Tracker response interval is null.");

            _lastTrackerResponse = trackerResponse;
            PrettyPrint.PrintTrackerResponse(trackerResponse);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during initial announce: {ex.Message}");
            _state = AnnounceEngineState.Error;
            throw;
        }
    }

    private async Task SendHeartBeatAnnounce(AnnounceBuilder builder, TrackerResponse currentTrackerParams)
    {
        try
        {
            builder
                .WithEvent("") // Empty event for heartbeat
                .WithKey(_clientSignature.Key)
                .WithPeerId(_clientSignature.PeerId)
                .WithInfoHash(_torrentMeta.InfoHashBytes)
                .WithStats(_peerTrafficSim.TotalUploadedBytes, 0, 0); //  left bytes -> seeding + simulated upload

            string announceUrl = builder.Build();

            if (string.IsNullOrWhiteSpace(announceUrl))
                throw new InvalidOperationException("Failed to build heartbeat announce URL.");

            var trackerResponse = await Network.SendAnnounceOverTCPAsync(_torrentMeta.Host, _torrentMeta.Port, announceUrl);

            if (trackerResponse != null)
                _lastTrackerResponse = trackerResponse;
            else
                throw new InvalidOperationException("Tracker response is null.");

            AnsiConsole.Clear();
            PrettyPrint.PrintTrackerResponse(trackerResponse);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during heartbeat announce: {ex.Message}");
            _state = AnnounceEngineState.Error;
            throw;
        }
    }

    private async Task SendStopAnnounceAsync()
    {
        try
        {
            _announceBuilder
                .WithEvent("stopped")
                .WithKey(_clientSignature.Key)
                .WithPeerId(_clientSignature.PeerId)
                .WithInfoHash(_torrentMeta.InfoHashBytes)
                .WithStats(_peerTrafficSim.TotalUploadedBytes, 0, 0);

            string announceUrl = _announceBuilder.Build();
            await Network.SendAnnounceOverTCPAsync(_torrentMeta.Host, _torrentMeta.Port, announceUrl);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during stop announce: {ex.Message}");
        }
        finally
        {
            // Dispose();
            // _state = AnnounceEngineState.Ready;
            Reset();
        }
    }

    public async Task ForceHeartBeatAsync()
    {
        if (_state != AnnounceEngineState.Running)
            throw new InvalidOperationException("Announce engine is not running.");

        await SendHeartBeatAnnounce(_announceBuilder, _lastTrackerResponse!);
        _trackerIntervallTimer.Restart();
    }

    public void Reset()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(AnnounceEngine));

        _cts?.Cancel();
        _cts?.Dispose();

        _cts = new CancellationTokenSource();

        _sessionBytesToUpload = 0;
        _sessionBytesUploaded = 0;
        _sessionBytesToDownload = 0;
        _sessionBytesDownloaded = 0;

        _trackerIntervallTimer.Reset();

        _state = AnnounceEngineState.Ready;

        _lastTrackerResponse = null;
    }


    // Gracefull shutdown func
    public async Task StopAsync()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(AnnounceEngine));

        if (_state != AnnounceEngineState.Running && _state != AnnounceEngineState.Starting)
            return;


        _cts?.Cancel();


        await SendStopAnnounceAsync();


        _state = AnnounceEngineState.Stopped;


        Dispose();
    }



    // Listen for keys in a separate thread, low cpu usage, non-blocking, easy peezy.
    private void StartKeysListner()
    {
        Task.Run(async () =>
        {
            while (_state == AnnounceEngineState.Running)
            {
                var forceKey = Console.ReadKey(true); // consume key without display

                // F5 to force heartbeat announce, escape to stop the announce engine
                if (forceKey.Key == ConsoleKey.F5)
                {
                    AnsiConsole.MarkupLine("[yellow]Forcing heartbeat announce...[/]");
                    await ForceHeartBeatAsync();
                }
                else if (forceKey.Key == ConsoleKey.Escape)
                {
                    AnsiConsole.MarkupLine("[red]Stopping announce engine...[/]");
                    await StopAsync();
                    break;
                }
            }


        });
    }


    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        GC.SuppressFinalize(this);
    }
}
