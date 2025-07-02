using BencodeNET.Objects;
using BencodeNET.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ShadowPeer.Helpers
{
    internal class DataParser
    {
        private const int PeerCompactSize = 6; // Each peers is 6 bytes.  4 IP + 2 port



        public static void ProcessResponse(byte[] responseBytes)
        {
            int headerEndIndex = IndexOfSequence(responseBytes, "\r\n\r\n"u8.ToArray());
            if (headerEndIndex == -1)
            {
                Console.WriteLine("Invalid HTTP response, no header-body separator found.");
                return;
            }

            string headers = Encoding.ASCII.GetString(responseBytes, 0, headerEndIndex);
            Console.WriteLine("Response headers:");
            Console.WriteLine(headers);

            int bodyStartIndex = headerEndIndex + 4;
            byte[] bodyBytes = new byte[responseBytes.Length - bodyStartIndex];

            Array.Copy(responseBytes, bodyStartIndex, bodyBytes, 0, bodyBytes.Length);

            try
            {
                var parser = new BencodeParser();
                var dict = parser.Parse<BDictionary>(bodyBytes);

                Console.WriteLine("\nTracker response:");
                PrintIfNumber(dict, "complete", "Seeders");
                PrintIfNumber(dict, "incomplete", "Leechers");
                PrintIfNumber(dict, "interval", "Interval");
                PrintIfNumber(dict, "min interval", "Min Interval");

                if (!dict.TryGetValue("peers", out var peersObj))
                {
                    Console.WriteLine("No peers in response.");
                    return;
                }

                switch (peersObj)
                {
                    case BString peersBStr:
                        ParseCompactPeers(peersBStr);
                        break;
                    case BList peersList:
                        ParsePeerList(peersList);
                        break;
                    default:
                        Console.WriteLine("Unknown peers format.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse response: {ex.Message}");
                Console.WriteLine($"Raw response: {Encoding.ASCII.GetString(bodyBytes)}");
            }
            return;
        }

        private static void PrintIfNumber(BDictionary dict, string key, string displayName)
        {
            if (dict.TryGetValue(key, out var obj) && obj is BNumber num)
                Console.WriteLine($"{displayName}: {num.Value}");
        }


        // Clean cut between header and body
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

        private static void ParseCompactPeers(BString peersBStr)
        {
            // Convert the BString's underlying value to a byte array
            byte[] peersBytes = peersBStr.Value.ToArray();

            // Check if the total length of the bytes is a multiple of the expected peer size
            if (peersBytes.Length % PeerCompactSize != 0)
            {
                Console.WriteLine($"Warning: Peers data length {peersBytes.Length} mismatch not a multiple of {PeerCompactSize}");
            }

            // Calculate how many peers are contained in the byte array
            int peerCount = peersBytes.Length / PeerCompactSize;

            // Warn if no peers are found in the data blob
            if (peerCount == 0)
            {
                Console.WriteLine("Warning -> No peers found in blob!.");
            }

            Console.WriteLine($"\nFound {peerCount} peers (compact format):");

            // Loop through each peer entry
            for (int i = 0; i < peerCount; i++)
            {
                // Calculate the starting index of the current peer data in the array
                int offset = i * PeerCompactSize;

                // Extract the 4 bytes corresponding to the IPv4 address, then convert to a human-readable string
                string ip = new IPAddress(peersBytes.AsSpan(offset, 4)).ToString();

                // Extract the 2 bytes corresponding to the port number
                // Shift the first byte by 8 bits to the left (high byte) and OR it with the second byte (low byte) to form the port number
                ushort port = (ushort)(peersBytes[offset + 4] << 8 | peersBytes[offset + 5]);

                // Convert the port to string for consistent formatting
                string portStr = port.ToString();

                // Output the peer info as IP:port
                Console.WriteLine($"  Peer {i + 1}: {ip}:{portStr}");
            }
        }


        // Parser for peer list in BList format(non-compact dictionnary)
        private static void ParsePeerList(BList peersList)
        {
            if (peersList.Count == 0)
            {
                Console.WriteLine("Warning -> No peers found in list format.");
                
            }

            Console.WriteLine($"\nFound {peersList.Count} peers (list format):");

            for (int i = 0; i < peersList.Count; i++)
            {
                
                if (peersList[i] is not BDictionary peerDict)
                {
                    Console.WriteLine($"  Peer {i + 1}: Invalid format");
                    continue;
                }

                string? ip = peerDict.Get<BString>("ip")?.Value.ToString();
                long? port = peerDict.Get<BNumber>("port")?.Value;

                if (ip != null && port.HasValue)
                    Console.WriteLine($"  Peer {i + 1}: {ip}:{port}");
                else
                    Console.WriteLine($"  Peer {i + 1}: Incomplete data");
            }


        }

        public static string UrlEncodeInfoHashBytes(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 3); // worst case: all bytes encoded
      
            foreach (var b in bytes)
            {
                char c = (char)b;
                if ((c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '.' || c == '-' || c == '_' || c == '~') // URI safe chars
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('%');
                    sb.Append(b.ToString("x2")); // lower case hex
                }
            }
            return sb.ToString();
        }



    }
}
