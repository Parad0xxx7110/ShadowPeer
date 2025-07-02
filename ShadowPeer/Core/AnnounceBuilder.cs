using System.Net;
using System.Net.Sockets;
using System.Text;

public class AnnounceBuilder
{
    private string? _trackerUrl;
    private byte[]? _infoHash;
    private byte[]? _peerId;
    private int? _port;
    private long _uploaded = 0, _downloaded = 0, _left = 0;
    private string? _ip, _ipv6, _event, _trackerId, _key, _passkey;
    private string? _extra;

    public AnnounceBuilder WithTrackerUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Tracker URL cannot be null or empty.", nameof(url));

        _trackerUrl = url.Trim();
        return this;
    }

    public AnnounceBuilder WithInfoHash(ReadOnlySpan<byte> infoHash)
    {
        if (infoHash.Length != 20)
            throw new ArgumentException("Info hash must be 20 bytes.");

        _infoHash = infoHash.ToArray();
        return this;
    }

    public AnnounceBuilder WithPeerId(ReadOnlySpan<byte> peerId)
    {
        if (peerId.Length != 20)
            throw new ArgumentException("Peer ID must be 20 bytes.");

        _peerId = peerId.ToArray();
        return this;
    }

    public AnnounceBuilder WithPort(int port)
    {
        if (port < 1 || port > 65535)
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

    public AnnounceBuilder WithEvent(string evt)
    {
        _event = evt;
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
        if (!string.IsNullOrWhiteSpace(passkey))
            _passkey = passkey;
        return this;
    }

    public AnnounceBuilder WithExtraQuery(string queryString)
    {
        _extra = queryString;
        return this;
    }

    public string Build()
    {
        if (_trackerUrl == null || _infoHash == null || _infoHash.Length == 0 || _peerId == null || _peerId.Length == 0 || !_port.HasValue)
            throw new InvalidOperationException("Tracker URL, InfoHash, PeerId, and Port are required.");

        var uri = new Uri(_trackerUrl);
        var segments = uri.Segments.Select(s => s.Trim('/')).Where(s => s.Length > 0).ToList();

        if (!string.IsNullOrWhiteSpace(_passkey))
        {
            int idx = segments.FindIndex(s => s.Equals("announce", StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                segments.Insert(idx, _passkey!);
            else
                segments.Add(_passkey!);
        }

        var baseUrl = $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : $":{uri.Port}")}/{string.Join('/', segments)}";

        var sb = new StringBuilder(512);

        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                sb.Append('&').Append(key).Append('=').Append(WebUtility.UrlEncode(value));
        }

        sb.Append("?info_hash=").Append(PercentEncode(_infoHash));
        sb.Append("&peer_id=").Append(PercentEncode(_peerId));
        sb.Append("&port=").Append(_port.Value);
        sb.Append("&uploaded=").Append(_uploaded);
        sb.Append("&downloaded=").Append(_downloaded);
        sb.Append("&left=").Append(_left);
        sb.Append("&compact=1&numwant=80");

        Add("ip", _ip);
        Add("ipv6", _ipv6);
        Add("event", _event);
        Add("trackerid", _trackerId);
        Add("key", _key);

        if (!string.IsNullOrWhiteSpace(_extra))
            sb.Append('&').Append(_extra);

        return baseUrl + sb.ToString();
    }

    // Construire la requête HTTP GET complète au format texte pour TCP
    public string BuildTcpRequest()
    {
        if (_trackerUrl == null)
            throw new InvalidOperationException("Tracker URL must be set.");

        var fullUrl = Build();
        var uri = new Uri(fullUrl);

        var sb = new StringBuilder();

        sb.Append($"GET {uri.PathAndQuery} HTTP/1.0\r\n");
        sb.Append($"Host: {uri.Host}\r\n");
        sb.Append("User-Agent: ShadowPeer/1.0\r\n");
        sb.Append("Connection: close\r\n");
        sb.Append("\r\n");

        return sb.ToString();
    }

    private static string PercentEncode(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder(data.Length * 3);
        foreach (byte b in data)
            sb.Append('%').Append(b.ToString("X2"));
        return sb.ToString();
    }

    public const string EventStarted = "started";
    public const string EventStopped = "stopped";
    public const string EventCompleted = "completed";
}