using ShadowPeer.DataModels;
using ShadowPeer.Helpers;
using Spectre.Console;
using System.Diagnostics;

namespace ShadowPeer.Core;

internal class AnnounceEngine : IDisposable
{
    private readonly TorrentMetadatas _torrentMeta;
    private readonly ClientSignature _clientSignature;
    private readonly AnnounceBuilder _announceBuilder;
    private readonly PeerTrafficSim _peerTrafficSim;
    private TrackerResponse? _lastTrackerResponse;

    public long SessionBytesAnnounced { get; private set; } = 0;
    public string SessionTorrentName { get; private set; } = string.Empty;
    public double SessionCurrentUploadSpeed { get; private set; } = 0;
    public TimeSpan SessionElapsedTime { get; private set; } = TimeSpan.Zero;
    public TimeSpan NextAnnounce { get; private set; } = TimeSpan.Zero;

    private TimeSpan _trackerInterval = TimeSpan.FromMinutes(30);
    private readonly Stopwatch _trackerIntervallTimer = new();
    private AnnounceEngineState _state = AnnounceEngineState.Ready;
    private CancellationTokenSource? _cts = new();
    private bool _isDisposed = false;
    private long _lastAnnouncedBytes = 0;

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
        SessionTorrentName = _torrentMeta.Name;
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

        // Setup Live Display Table
        var table = new Table()
            .AddColumn("Property")
            .AddColumn("Value");

        table.AddRow("Torrent Name", Markup.Escape(SessionTorrentName));
        table.AddRow("Bytes Announced", SessionBytesAnnounced.ToString());
        table.AddRow("Current Upload Speed", $"{SessionCurrentUploadSpeed / 1024.0 / 1024.0:F2} MB/s");
        table.AddRow("Elapsed Time", SessionElapsedTime.ToString(@"hh\:mm\:ss"));
        table.AddRow("Next Announce", NextAnnounce.ToString(@"mm\:ss"));

        await AnsiConsole.Live(table)
            .StartAsync(async ctx =>
            {
                while (!Token.IsCancellationRequested)
                {
                    _peerTrafficSim.Tick();
                    SessionCurrentUploadSpeed = _peerTrafficSim.CurrentUploadSpeed;
                    SessionElapsedTime = _trackerIntervallTimer.Elapsed;

                    var nextAnnounce = _trackerInterval - _trackerIntervallTimer.Elapsed;
                    NextAnnounce = nextAnnounce > TimeSpan.Zero ? nextAnnounce : TimeSpan.Zero;

                    if (nextAnnounce <= TimeSpan.Zero && _lastTrackerResponse is not null && _lastTrackerResponse.Interval.HasValue)
                    {
                        await SendHeartBeatAnnounce(_announceBuilder, _lastTrackerResponse);
                        SessionBytesAnnounced += _peerTrafficSim.TotalUploadedBytes;
                        _trackerIntervallTimer.Restart();
                        SessionElapsedTime = TimeSpan.Zero;
                    }

                    double mb = SessionBytesAnnounced / 1024.0 / 1024.0; // make a func for convert bytes ffs...

                    
                    table.UpdateCell(0, 1, Markup.Escape(SessionTorrentName));
                    table.UpdateCell(1, 1, $"{mb:F2} MB");
                    table.UpdateCell(2, 1, $"{SessionCurrentUploadSpeed / 1024.0 / 1024.0:F2} MB/s");
                    table.UpdateCell(3, 1, SessionElapsedTime.ToString(@"hh\:mm\:ss"));
                    table.UpdateCell(4, 1, NextAnnounce.ToString(@"mm\:ss"));


                    ctx.Refresh();

                    await Task.Delay(1000, Token);
                }
            });
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
            _lastAnnouncedBytes = 0;

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
            long totalUploaded = _peerTrafficSim.TotalUploadedBytes;
            long uploadedDelta = totalUploaded - _lastAnnouncedBytes;

            builder
                .WithEvent("")
                .WithKey(_clientSignature.Key)
                .WithPeerId(_clientSignature.PeerId)
                .WithInfoHash(_torrentMeta.InfoHashBytes)
                .WithStats(totalUploaded, 0, 0);

            string announceUrl = builder.Build();

            if (string.IsNullOrWhiteSpace(announceUrl))
                throw new InvalidOperationException("Failed to build heartbeat announce URL.");

            var trackerResponse = await Network.SendAnnounceOverTCPAsync(_torrentMeta.Host, _torrentMeta.Port, announceUrl)
                ?? throw new InvalidOperationException("Tracker response is null.");

            _lastTrackerResponse = trackerResponse;

            SessionBytesAnnounced += uploadedDelta;
            _lastAnnouncedBytes = totalUploaded;

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

        SessionBytesAnnounced = 0;
        SessionTorrentName = string.Empty;
        _lastAnnouncedBytes = 0;

        _trackerIntervallTimer.Reset();
        _state = AnnounceEngineState.Ready;
        _lastTrackerResponse = null;
    }

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

    private void StartKeysListner()
    {
        Task.Run(async () =>
        {
            while (_state == AnnounceEngineState.Running)
            {
                var forceKey = Console.ReadKey(true);
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
