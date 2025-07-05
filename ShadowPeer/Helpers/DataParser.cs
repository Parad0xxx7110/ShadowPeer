using BencodeNET.Objects;
using BencodeNET.Parsing;
using ShadowPeer.DataModels;
using System.Net;
using System.Text;

namespace ShadowPeer.Helpers
{
    internal class DataParser
    {
        private const int PeerCompactSize = 6; // Compact peer format: 4 bytes IP + 2 bytes port (big endian)

        /// <summary>
        /// Parses the raw HTTP response (headers + Bencode body) received from the BitTorrent tracker.
        /// </summary>
        /// <param name="responseBytes">Raw response bytes (HTTP headers + Bencode body)</param>
        /// <param name="responseModel">Output: DTO model containing extracted data</param>
        /// <returns>True if parsing succeeded, false otherwise</returns>
        public static bool TryParseTrackerResponse(byte[] responseBytes, out TrackerResponse responseModel)
        {
            responseModel = new TrackerResponse();

            // Search for the end of the HTTP header section
            int headerEndIndex = IndexOfSequence(responseBytes, "\r\n\r\n"u8.ToArray());
            if (headerEndIndex == -1)
            {
                // Invalid response format, no header-body separator found
                return false;
            }

            // Extract Bencode body from the response
            int bodyStartIndex = headerEndIndex + 4;
            byte[] bodyBytes = new byte[responseBytes.Length - bodyStartIndex];
            Array.Copy(responseBytes, bodyStartIndex, bodyBytes, 0, bodyBytes.Length);

            try
            {
                var parser = new BencodeParser();
                var dict = parser.Parse<BDictionary>(bodyBytes);

                // Extract common fields
                responseModel.Seeders = GetIntIfExists(dict, "complete");
                responseModel.Leechers = GetIntIfExists(dict, "incomplete");
                responseModel.Interval = GetIntIfExists(dict, "interval");
                responseModel.MinInterval = GetIntIfExists(dict, "min interval");

                // Extract peers list
                if (!dict.TryGetValue("peers", out var peersObj))
                    return true; // Parsing successful even if no peers present

                if (peersObj is BString peersBStr)
                {
                    // Compact peer format
                    responseModel.PeersCompact = peersBStr.Value.ToArray();
                }
                else if (peersObj is BList peersList)
                {
                    // Legacy format: list of dictionaries for each peer
                    var parsedPeers = new List<Peer>();
                    foreach (var peerItem in peersList)
                    {
                        if (peerItem is BDictionary peerDict)
                        {
                            var ip = peerDict.Get<BString>("ip")?.Value.ToString();
                            var port = peerDict.Get<BNumber>("port")?.Value;

                            if (!string.IsNullOrEmpty(ip) && port.HasValue)
                            {
                                parsedPeers.Add(new Peer { IP = ip, Port = (ushort)port.Value });
                            }
                        }
                    }
                    responseModel.PeersList = parsedPeers;
                }
                else
                {
                    // Unexpected peers format - ignore or handle if needed
                }

                return true;
            }
            catch
            {
                // Parsing failure due to invalid Bencode or other errors
                return false;
            }
        }

        /// <summary>
        /// Finds the first occurrence of a byte pattern within a byte array.
        /// </summary>
        private static int IndexOfSequence(byte[] buffer, byte[] pattern)
        {
            for (int i = 0; i <= buffer.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (buffer[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Tries to extract an integer value from a BDictionary by key.
        /// </summary>
        private static int? GetIntIfExists(BDictionary dict, string key)
        {
            if (dict.TryGetValue(key, out var val) && val is BNumber num)
            {
                return (int)num.Value;
            }
            return null;
        }

        /// <summary>
        /// Utility method to parse and decode compact peer list format.
        /// </summary>
        public static IEnumerable<Peer> ParseCompactPeers(byte[] peersCompact)
        {
            var peers = new List<Peer>();

            if (peersCompact.Length % PeerCompactSize != 0)
                throw new ArgumentException("Compact peers data length mismatch !");

            int peerCount = peersCompact.Length / PeerCompactSize;

            for (int i = 0; i < peerCount; i++)
            {
                int offset = i * PeerCompactSize;

                // IP address (4 bytes)
                string ip = new IPAddress(peersCompact.AsSpan(offset, 4)).ToString();

                // Port number (2 bytes, big endian)
                ushort port = (ushort)((peersCompact[offset + 4] << 8) | peersCompact[offset + 5]);

                peers.Add(new Peer { IP = ip, Port = port });
            }

            return peers;
        }

        /// <summary>
        /// Encodes a byte array representing an info_hash into a URL-encoded string.
        /// </summary>
        public static string UrlEncodeInfoHashBytes(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 3);

            foreach (var b in bytes)
            {
                // Only encode unreserved characters from RFC 3986
                if ((b >= 0x30 && b <= 0x39) || // 0-9
                    (b >= 0x41 && b <= 0x5A) || // A-Z
                    (b >= 0x61 && b <= 0x7A) || // a-z
                    b == 0x2D || // -
                    b == 0x2E || // .
                    b == 0x5F || // _
                    b == 0x7E)   // ~
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append('%');
                    sb.Append(b.ToString("x2")); // uppercase hexadecimal
                }
            }

            return sb.ToString();
        }

    }
}
