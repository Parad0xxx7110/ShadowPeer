using BencodeNET.Objects;
using BencodeNET.Parsing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;
using ShadowPeer.Helpers;
using System.Threading.Tasks;

namespace ShadowPeer.Core
    {
        internal static class Network
        {
            private const int BufferSize = 8192; // Should be fairly enough for most responses
            private const int ResponseTimeoutMs = 10000; // 10 seconds timeout for tracker response
            

            public static async Task SendTCPTest()
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
                        Console.WriteLine("Connection timeout."); // Retry ???
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
                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

                    byte[] buffer = new byte[BufferSize];
                    using var responseBuilder = new MemoryStream();

                    var readTask = Task.Run(async () =>
                    {
                        int bytesRead;
                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                           await responseBuilder.WriteAsync(buffer, 0, bytesRead);
                        }
                    });

                    if (!readTask.Wait(ResponseTimeoutMs))
                    {
                        Console.WriteLine("Response timeout.");
                        return;
                    }

                    DataParser.ProcessResponse(responseBuilder.ToArray());
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
        }
    }