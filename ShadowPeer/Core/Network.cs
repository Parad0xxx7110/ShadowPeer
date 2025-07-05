using ShadowPeer.DataModels;
using ShadowPeer.Helpers;
using Spectre.Console;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace ShadowPeer.Core
{
    internal static class Network
    {
        private const int BufferSize = 8192;
        private const int ResponseTimeoutMs = 10000;

        public static async Task SendAnnounceOverTCPAsync(TorrentMetadatas torrentMeta, string announcePayload)
        {
            if (torrentMeta == null)
            {
                Console.WriteLine("torrentMeta is null.");
                return;
            }

            string host = torrentMeta.Host;
            string portStr = torrentMeta.Port;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(portStr) || !int.TryParse(portStr, out int port))
            {
                Console.WriteLine("Invalid tracker host or port.");
                return;
            }

            try
            {
                using var client = new TcpClient();
                AnsiConsole.MarkupLine($"[yellow]Connecting to tracker {host}:{port}...[/]");
                await client.ConnectAsync(host, port);
                AnsiConsole.MarkupLine($"[green]Connected to tracker {host}:{port}.[/]");

                string finalRequest = $"GET {announcePayload} HTTP/1.1\r\n" +
                                      $"Host: {host}\r\n" +
                                      "Connection: close\r\n" +
                                      "User-Agent: ShadowPeerClient/1.0\r\n" +
                                      "Accept: */*\r\n" +
                                      "\r\n";

                var stream = client.GetStream();

                
                stream.ReadTimeout = ResponseTimeoutMs;

                var requestBytes = Encoding.ASCII.GetBytes(finalRequest);
                await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
                AnsiConsole.MarkupLine("[green]Request sent successfully.[/]");

                var buffer = new byte[BufferSize];
                int bytesRead;
                using var ms = new MemoryStream();

                do
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        await ms.WriteAsync(buffer, 0, bytesRead);
                    }
                } while (bytesRead > 0);

                var responseBytes = ms.ToArray();
                AnsiConsole.MarkupLine("[green]Response received.[/]");
              
                if (DataParser.TryParseTrackerResponse(responseBytes, out var trackerResponse))
                {
                    AnsiConsole.WriteLine($"Seeders: {trackerResponse.Seeders}");
                    AnsiConsole.WriteLine($"Leechers: {trackerResponse.Leechers}");
                    AnsiConsole.WriteLine($"Interval: {trackerResponse.Interval}");
                    AnsiConsole.WriteLine($"MinInterval: {trackerResponse.MinInterval}");

                    if (trackerResponse.IsCompact && trackerResponse.PeersCompact != null)
                    {
                        Console.WriteLine("Peers (compact format):");

                        var peers = DataParser.ParseCompactPeers(trackerResponse.PeersCompact);
                        foreach (var peer in peers)
                        {
                            Console.WriteLine($" - {peer.IP}:{peer.Port}");
                        }
                    }
                    else if (trackerResponse.PeersList != null)
                    {
                        Console.WriteLine("Peers (list format):");
                        foreach (var peer in trackerResponse.PeersList)
                        {
                            Console.WriteLine($" - {peer.IP}:{peer.Port}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No peers found in response.");
                    }
                }
                else
                {
                    Console.WriteLine("Failed to parse tracker response.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during TCP communication: {ex}");
            }
        }

        public static async Task SendTCPTest()
        {
            string host = "tracker.p2p-world.net";
            int trackerPort = 8080;

            
            string infoHashEncoded = "%de%bf%9b%06%1c%e6%d3%f1CAR%e9%b2%0b%81%87%feVz%b4";
            string peerId = "-DE1200-hQj0UCmYXZ7w";
            int peerPort = 25341;

            string requestBody = $"/QIwOGEPByBzj5jr2OWcLgWP5GIULCtjA/announce?" +
                                 $"info_hash={infoHashEncoded}" +
                                 $"&peer_id={HttpUtility.UrlEncode(peerId)}" +
                                 $"&port={peerPort}" +
                                 $"&uploaded=0&downloaded=0&left=0" +
                                 $"&event=started" +
                                 $"&key=gcBz7G0t" +
                                 $"&compact=1" +
                                 $"&numwant=100" +
                                 $"&supportcrypto=1&no_peer_id=1";

            string finalRequest = $"GET {requestBody} HTTP/1.1\r\n" +
                                  $"Host: {host}\r\n" +
                                  "Connection: close\r\n" +
                                  "User-Agent: ShadowPeerClient/1.0\r\n" +
                                  "Accept: */*\r\n" +
                                  "\r\n";

            try
            {
                using var client = new TcpClient();
                AnsiConsole.MarkupLine($"[yellow]Connecting to tracker {host}:{trackerPort}...[/]");
                await client.ConnectAsync(host, trackerPort);
                AnsiConsole.MarkupLine($"[green]Connected to tracker {host}:{trackerPort}.[/]");

                var stream = client.GetStream();

                stream.ReadTimeout = ResponseTimeoutMs;

                var requestBytes = Encoding.ASCII.GetBytes(finalRequest);
                await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
                AnsiConsole.MarkupLine("[green]Request sent successfully.[/]");

                var buffer = new byte[BufferSize];
                int bytesRead;

                using var ms = new MemoryStream();

                do
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        await ms.WriteAsync(buffer, 0, bytesRead);
                    }
                } while (bytesRead > 0);

                var responseBytes = ms.ToArray();
                AnsiConsole.MarkupLine("[green]Response received.[/]");

                // Dump de la réponse brute en ASCII (tu peux aussi tenter UTF8 si besoin)
                string responseText = Encoding.ASCII.GetString(responseBytes);
                Console.WriteLine("=== RAW RESPONSE START ===");
                Console.WriteLine(responseText);
                Console.WriteLine("=== RAW RESPONSE END ===");

                if (DataParser.TryParseTrackerResponse(responseBytes, out var trackerResponse))
                {
                    Console.WriteLine($"Seeders: {trackerResponse.Seeders}");
                    Console.WriteLine($"Leechers: {trackerResponse.Leechers}");
                    Console.WriteLine($"Interval: {trackerResponse.Interval}");
                    Console.WriteLine($"MinInterval: {trackerResponse.MinInterval}");

                    if (trackerResponse.IsCompact && trackerResponse.PeersCompact != null)
                    {
                        Console.WriteLine("Peers (compact format):");
                        var peers = DataParser.ParseCompactPeers(trackerResponse.PeersCompact);
                        foreach (var peer in peers)
                        {
                            Console.WriteLine($" - {peer.IP}:{peer.Port}");
                        }
                    }
                    else if (trackerResponse.PeersList != null)
                    {
                        Console.WriteLine("Peers (list format):");
                        foreach (var peer in trackerResponse.PeersList)
                        {
                            Console.WriteLine($" - {peer.IP}:{peer.Port}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No peers found in response.");
                    }
                }
                else
                {
                    Console.WriteLine("Failed to parse tracker response.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during TCP communication: {ex}");
            }
        }
    }
}
