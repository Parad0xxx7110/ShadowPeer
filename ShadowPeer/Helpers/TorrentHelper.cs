using System.Net;
using System.Web;
using Spectre;
using Spectre.Console;

namespace ShadowPeer.Helpers
{
    public static class PassKeyExtractor
    {
        private static readonly string[] DefaultQueryKeys = { "passkey", "key", "token", "auth" };
        private static readonly string[] DefaultPathIdentifiers = { "announce", "announce.php", "scrape" };

        public static async Task<string> ExtractPassKeyAsync(string trackerUrl)
        {
            if (string.IsNullOrWhiteSpace(trackerUrl))
                return string.Empty;

            try
            {
                var uri = new Uri(trackerUrl);


                Console.WriteLine($"Extracting passkey with method 1...");
                var pathResult = await ExtractFromPathAsync(uri);
                if (!string.IsNullOrEmpty(pathResult))
                {
                    AnsiConsole.MarkupLine("[green]Method one found the passkey ![/]");
                    return pathResult;
                }

                Console.WriteLine($"Extracting passkey with method 2...");
                var queryResult = await ExtractFromQueryAsync(uri);
                if (!string.IsNullOrEmpty(queryResult))
                    return queryResult;
                Console.WriteLine($"Extracting passkey with method 3...");
                var fragResult = await ExtractFromFragmentAsync(uri);
                if (!string.IsNullOrEmpty(fragResult))
                    return fragResult;
            }
            catch { }

            AnsiConsole.MarkupLine("[red] Warning, all methods of extraction failed. Passkey unavailable ![/]");
            return string.Empty;
        }

        private static Task<string> ExtractFromPathAsync(Uri uri)
        {
            var segments = uri.Segments
                .Select(s => s.Trim('/'))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();

            // Search for passkey in path segments
            for (int i = 0; i < segments.Length; i++)
            {
                if (DefaultPathIdentifiers.Contains(segments[i], StringComparer.OrdinalIgnoreCase))
                {
                    if (i > 0)
                        return Task.FromResult(segments[i - 1]);
                }
            }

            // Fallback
            if (segments.Length > 0 &&
                !DefaultPathIdentifiers.Contains(segments.Last(), StringComparer.OrdinalIgnoreCase))
            {
                return Task.FromResult(segments.Last());
            }

            return Task.FromResult(string.Empty);
        }

        private static Task<string> ExtractFromQueryAsync(Uri uri)
        {
            if (string.IsNullOrEmpty(uri.Query))
                return Task.FromResult(string.Empty);

            var query = HttpUtility.ParseQueryString(uri.Query);

            foreach (var key in DefaultQueryKeys)
            {
                var value = query.Get(key);
                if (!string.IsNullOrWhiteSpace(value))
                    return Task.FromResult(value);
            }

            return Task.FromResult(string.Empty);
        }

        private static Task<string> ExtractFromFragmentAsync(Uri uri)
        {
            if (string.IsNullOrEmpty(uri.Fragment))
                return Task.FromResult(string.Empty);

            var fragment = uri.Fragment.TrimStart('#');
            var fake = "?" + fragment;
            var query = HttpUtility.ParseQueryString(fake);

            foreach (var key in DefaultQueryKeys)
            {
                var value = query.Get(key);
                if (!string.IsNullOrWhiteSpace(value))
                    return Task.FromResult(value);
            }

            return Task.FromResult(string.Empty);
        }




        // Parse compact list (blob) reponse from BT tracker
        private static List<IPEndPoint> ParseCompactPeersList(ReadOnlySpan<byte> data)
        {
            var list = new List<IPEndPoint>();
            for (int i = 0; i + 6 <= data.Length; i += 6)
            {
                var ip = new IPAddress(new byte[] { data[i], data[i + 1], data[i + 2], data[i + 3] });
                var port = data[i + 4] << 8 | data[i + 5];
                list.Add(new IPEndPoint(ip, port));
            }
            return list;
        }
    }
}