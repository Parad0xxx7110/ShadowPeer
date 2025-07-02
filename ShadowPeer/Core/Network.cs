using BencodeNET.Objects;
using BencodeNET.Parsing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;

namespace ShadowPeer.Core
    {
        internal static class Network
        {
            private const int BufferSize = 8192; // Should be fairly enough for most responses
            private const int ResponseTimeoutMs = 10000; // 10 seconds timeout for tracker response
            private const int PeerCompactSize = 6; // 6 bytes.  4 (IP) + 2 (port)

            public static void SendTCPTest()
            {
                string host = "tracker.p2p-world.net";
                int trackerPort = 8080;

            
                string infoHashEncoded = "%de%bf%9b%06%1c%e6%d3%f1CAR%e9%b2%0b%81%87%feVz%b4"; // Url-encoded info_hash

                string peerId = "-DE1200-hQj0UCmYXZ7w"; // 20 bytes
                int peerPort = 25341;

                // Requête originale avec votre encodage d'info_hash
                string requestPath = $"/QIwOGEPByBzj5jr2OWcLgWP5GIULCtjA/announce?" +
                                   $"info_hash={infoHashEncoded}" +
                                   $"&peer_id={HttpUtility.UrlEncode(peerId)}" +
                                   $"&port={peerPort}" +
                                   $"&uploaded=0&downloaded=0&left=0" +
                                   $"&event=started" +
                                   $"&key=gcBz7G0t" +
                                   $"&compact=1" +
                                   $"&numwant=100" +
                                   $"&supportcrypto=1&no_peer_id=1";

                try
                {
                    using var client = new TcpClient();

                    client.SendTimeout = ResponseTimeoutMs;
                    client.ReceiveTimeout = ResponseTimeoutMs;

                    var connectTask = client.ConnectAsync(host, trackerPort);
                    if (!connectTask.Wait(ResponseTimeoutMs))
                    {
                        Console.WriteLine("Connection timeout.");
                        return;
                    }

                    if (!client.Connected)
                    {
                        Console.WriteLine("Failed to connect to the tracker.");
                        return;
                    }

                    Console.WriteLine("Connected to the tracker.");

                    using NetworkStream stream = client.GetStream();

                    string request =
                        $"GET {requestPath} HTTP/1.1\r\n" +
                        $"Host: {host}\r\n" +
                        $"User-Agent: Shad0wPeer/1.0\r\n" +
                        $"Connection: close\r\n" +
                        $"\r\n";

                    byte[] requestBytes = Encoding.ASCII.GetBytes(request);
                    stream.Write(requestBytes, 0, requestBytes.Length);

                    byte[] buffer = new byte[BufferSize];
                    using var responseBuilder = new MemoryStream();

                    var readTask = Task.Run(() =>
                    {
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            responseBuilder.Write(buffer, 0, bytesRead);
                        }
                    });

                    if (!readTask.Wait(ResponseTimeoutMs))
                    {
                        Console.WriteLine("Response timeout.");
                        return;
                    }

                    ProcessResponse(responseBuilder.ToArray());
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Network error: {ex.SocketErrorCode} - {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }

            private static void ProcessResponse(byte[] responseBytes)
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
            }

            private static void PrintIfNumber(BDictionary dict, string key, string displayName)
            {
                if (dict.TryGetValue(key, out var obj) && obj is BNumber num)
                    Console.WriteLine($"{displayName}: {num.Value}");
            }


            // Parse compact list of peers (blob)
            private static void ParseCompactPeers(BString peersBStr)
            {
                byte[] peersBytes = peersBStr.Value.ToArray();
                if (peersBytes.Length % PeerCompactSize != 0)
                {
                    Console.WriteLine($"Warning: Peers data length {peersBytes.Length} is not a multiple of {PeerCompactSize}");
                }

                int peerCount = peersBytes.Length / PeerCompactSize;
                Console.WriteLine($"\nFound {peerCount} peers (compact format):");

                for (int i = 0; i < peerCount; i++)
                {
                    int offset = i * PeerCompactSize;
                    string ip = new IPAddress(peersBytes.AsSpan(offset, 4)).ToString();
                    ushort port = (ushort)(peersBytes[offset + 4] << 8 | peersBytes[offset + 5]);
                    Console.WriteLine($"  Peer {i + 1}: {ip}:{port}");
                }
            }

            // Parser for peer list in BList format(non-compact dictionnary)
            private static void ParsePeerList(BList peersList)
            {
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


            // Clean cut between header and body in HTTP response
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
        }
    }