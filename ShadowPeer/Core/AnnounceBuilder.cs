using ShadowPeer.DataModels;
using ShadowPeer.Helpers;
using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;

/// <summary>
/// Fluent API for building BitTorrent tracker announces.
/// Supports automatic passkey placement and advanced parameter composition.
/// </summary>
public class AnnounceBuilder
{
    private const int DefaultPort = 25341; // Default port for the client, totally arbitrary and random here.
    private const int DefaultNumWant = 10;

    private byte[]? _infoHash;
    private int? _port = DefaultPort;
    private long _uploaded, _downloaded, _left;
    private string? _trackerUrl, _event, _key, _trackerId, _ipv6, _peerId, _passkey, _ip, _extra;

    /// <summary>
    /// Initializes the builder from a TorrentMetadatas object.
    /// </summary>
    public AnnounceBuilder(TorrentMetadatas torrent)
    {
        ArgumentNullException.ThrowIfNull(torrent);

        _trackerUrl = torrent.AnnounceUrls?.Trim();
        _infoHash = torrent.InfoHashBytes;
        _peerId = GeneratePeerId();

        if (!string.IsNullOrWhiteSpace(torrent.PassKey) &&
            !torrent.PassKey.Equals("No passkey provided / found.", StringComparison.OrdinalIgnoreCase))
        {
            _passkey = torrent.PassKey.Trim();
        }
    }

    public AnnounceBuilder WithTrackerUrl(string url)
    {
        _trackerUrl = string.IsNullOrWhiteSpace(url) ? throw new ArgumentException("Announce URL cannot be null or empty.", nameof(url)) : url.Trim();
        return this;
    }

    public AnnounceBuilder WithInfoHash(ReadOnlySpan<byte> infoHash)
    {
        if (infoHash.Length != 20)
            throw new ArgumentException("Info hash must be 20 bytes.");

        _infoHash = infoHash.ToArray();
        return this;
    }

    public AnnounceBuilder WithPeerId(string peerId)
    {
        if (peerId.Length != 20)
            throw new ArgumentException("Peer ID must be 20 bytes.");

        _peerId = peerId;
        return this;
    }

    public AnnounceBuilder WithPort(int port)
    {
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

        _port = port;
        return this;
    }

    public AnnounceBuilder WithStats(long uploaded, long downloaded, long left)
    {
        _uploaded = uploaded;
        _downloaded = downloaded;
        _left = left;
        return this;
    }

    public AnnounceBuilder WithIpAddress(string ip)
    {
        if (!IPAddress.TryParse(ip, out var ipAddr))
            throw new ArgumentException("Invalid IP address.", nameof(ip));

        if (ipAddr.AddressFamily == AddressFamily.InterNetwork)
            _ip = ip;
        else if (ipAddr.AddressFamily == AddressFamily.InterNetworkV6)
            _ipv6 = ip;

        return this;
    }

    public AnnounceBuilder WithEvent(string trackerEvent)
    {
        _event = trackerEvent;
        return this;
    }

    public AnnounceBuilder WithTrackerId(string trackerId)
    {
        _trackerId = trackerId;
        return this;
    }

    public AnnounceBuilder WithKey(string key)
    {
        _key = key;
        return this;
    }

    public AnnounceBuilder WithPasskey(string passkey)
    {
        _passkey = string.IsNullOrWhiteSpace(passkey) ? null : passkey.Trim();
        return this;
    }

    public AnnounceBuilder WithExtraQuery(string extraQueryString)
    {
        _extra = string.IsNullOrWhiteSpace(extraQueryString) ? null : extraQueryString.Trim('&', '?');
        return this;
    }

    /// <summary>
    /// Builds the final announce URL with all required and optional parameters.
    /// Automatically decides whether to include the passkey in the path or as a query parameter :).
    /// </summary>
    /// <returns>A validated payload string of the request for HTTP over TCP</returns>
    /// <exception cref="InvalidOperationException">Thrown if required fields are missing or invalid</exception>
    public string Build()
    {
        ValidateRequiredFields();

        var uriBuilder = new UriBuilder(_trackerUrl!);


        bool passkeyInPath = !string.IsNullOrWhiteSpace(_passkey) &&
                             uriBuilder.Path.Contains(_passkey, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(_passkey))
        {
            if (uriBuilder.Path.Contains("announce", StringComparison.OrdinalIgnoreCase) &&
                !passkeyInPath)
            {
                uriBuilder.Path = InsertPasskeyIntoPath(uriBuilder.Path, _passkey);
            }

        }


        var query = BuildQueryParameters(excludeInfoHash: true);

        var sb = new StringBuilder();
        string encodedInfoHash = DataParser.InfoHashBytesToUrl(_infoHash!);

        sb.Append($"info_hash={encodedInfoHash}");

        foreach (string? key in query.AllKeys)
        {
            if (key == null) continue;
            sb.Append('&').Append(key).Append('=').Append(query[key]);
        }

        return $"{uriBuilder.Path}?{sb}";
    }




    private void ValidateRequiredFields()
    {
        if (string.IsNullOrWhiteSpace(_trackerUrl))
            throw new InvalidOperationException("Tracker URL is required.");
        if (_infoHash == null || _infoHash.Length != 20)
            throw new InvalidOperationException("Info hash must be 20 bytes.");
        if (string.IsNullOrWhiteSpace(_peerId) || _peerId.Length != 20)
            throw new InvalidOperationException("Peer ID must be 20 bytes.");
    }

    private static string InsertPasskeyIntoPath(string path, string passkey)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        int announceIndex = segments.FindIndex(s => s.Equals("announce", StringComparison.OrdinalIgnoreCase));
        if (announceIndex >= 0)
            segments.Insert(announceIndex, passkey);
        else
            segments.Add(passkey);

        return "/" + string.Join('/', segments);
    }

    private NameValueCollection BuildQueryParameters(bool excludeInfoHash = false)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);

        if (!excludeInfoHash)
            query["info_hash"] = DataParser.InfoHashBytesToUrl(_infoHash!);

        query["peer_id"] = _peerId;
        query["port"] = (_port ?? DefaultPort).ToString();
        query["uploaded"] = _uploaded.ToString();
        query["downloaded"] = _downloaded.ToString();
        query["left"] = _left.ToString();
        query["compact"] = "1";
        query["numwant"] = DefaultNumWant.ToString();
        query["supportcrypto"] = "1";
        query["no_peer_id"] = "1";

        if (!string.IsNullOrWhiteSpace(_event)) query["event"] = _event;
        if (!string.IsNullOrWhiteSpace(_key)) query["key"] = _key;
        if (!string.IsNullOrWhiteSpace(_trackerId)) query["trackerid"] = _trackerId;
        if (!string.IsNullOrWhiteSpace(_ip)) query["ip"] = _ip;
        if (!string.IsNullOrWhiteSpace(_ipv6)) query["ipv6"] = _ipv6;

        if (!string.IsNullOrWhiteSpace(_extra))
        {
            var parts = _extra.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=');
                if (kv.Length == 2)
                    query[kv[0]] = kv[1];
            }
        }


        var KeysOrder = new[]
        {
            "info_hash",
            "peer_id",
            "port",
            "uploaded",
            "downloaded",
            "left",
            "event",
            "key",
            "compact",
            "numwant",
            "supportcrypto",
            "no_peer_id",
            "trackerid",
            "ip",
            "ipv6"
        };

        var orderedQuery = new NameValueCollection();


        foreach (var key in KeysOrder)
        {
            var val = query[key];
            if (!string.IsNullOrWhiteSpace(val))
            {
                orderedQuery[key] = val;
            }
        }


        foreach (string key in query.AllKeys!)
        {
            if (!KeysOrder.Contains(key))
            {
                var val = query[key];
                if (!string.IsNullOrWhiteSpace(val))
                {
                    orderedQuery[key] = val;
                }
            }
        }

        return orderedQuery;
    }



    /// <summary>
    /// Generates a default peer ID if none was set. Format: -SP1337-XXXXXXXXXXXX
    /// </summary>
    private static string GeneratePeerId()
    {
        return "-SP1337-" + Guid.NewGuid().ToString("N")[..12];
    }
}
