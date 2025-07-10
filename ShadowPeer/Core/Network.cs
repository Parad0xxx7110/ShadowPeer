using ShadowPeer.DataModels;
using ShadowPeer.Helpers;
using Spectre.Console;
using System.Net.Sockets;
using System.Text;

namespace ShadowPeer.Core
{
    internal static class Network
    {
        private const int BufferSize = 8192;

        public static async Task<TrackerResponse?> SendAnnounceOverTCPAsync(
            string host,
            string port,
            string payload,
            string userAgent = "ShadowPeerClient/1.0",
            int connectTimeoutMs = 10000)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                Console.WriteLine("Announce payload cannot be null or empty.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(port) || !int.TryParse(port, out int parsedPort))
            {
                Console.WriteLine("Invalid tracker host or port.");
                return null;
            }

            try
            {
                using var client = new TcpClient();
                client.NoDelay = true;

                AnsiConsole.MarkupLine($"[yellow]Connecting to tracker {host}:{parsedPort}...[/]");

                var connectTask = client.ConnectAsync(host, parsedPort);
                if (await Task.WhenAny(connectTask, Task.Delay(connectTimeoutMs)) != connectTask)
                {
                    AnsiConsole.MarkupLine($"[red]Connection to {host}:{parsedPort} timed out after {connectTimeoutMs} ms.[/]");
                    return null;
                }

                AnsiConsole.MarkupLine($"[green]Connected to tracker {host}:{parsedPort}.[/]");

                string finalRequest = $"GET {payload} HTTP/1.1\r\n" +
                                      $"Host: {host}\r\n" +
                                      "Connection: close\r\n" +
                                      $"User-Agent: {userAgent}\r\n" +
                                      "Accept: */*\r\n" +
                                      "\r\n";

                var stream = client.GetStream();
                stream.ReadTimeout = connectTimeoutMs; // reusing for read timeout as well

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
                        await ms.WriteAsync(buffer, 0, bytesRead);
                } while (bytesRead > 0);

              

                var responseBytes = ms.ToArray();

                if (DataParser.TryParseTrackerResponse(responseBytes, out var trackerResponse))
                    return trackerResponse;

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during TCP communication: {ex}");
                return null;
            }
        }
    }
}
