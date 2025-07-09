using ShadowPeer.DataModels;
using System.Security.Cryptography;



// STABLE & SEALED

namespace ShadowPeer.Core
{
    public enum TorrentClient
    {
        uTorrent,
        BitTorrent,
        Vuze,
        Transmission,
        Deluge,
        ShadowPeer,
        Random
    }

    public static class ClientSignatureFactory
    {
        private static readonly ThreadLocal<Random> _rnd = new(() => new Random());

        private static readonly Dictionary<TorrentClient, string> _clientVersions = new()
        {
            [TorrentClient.uTorrent] = "3.5.5",
            [TorrentClient.BitTorrent] = "7.10",
            [TorrentClient.Vuze] = "5.7.6",
            [TorrentClient.Transmission] = "3.0",
            [TorrentClient.Deluge] = "2.0.3",
            [TorrentClient.ShadowPeer] = "1.3.3.7"
        };

        public static ClientSignature Create(TorrentClient client)
        {
            if (client == TorrentClient.Random)
            {
                var clients = Enum.GetValues(typeof(TorrentClient))
                                .Cast<TorrentClient>()
                                .Where(c => c != TorrentClient.Random)
                                .ToArray();

                client = clients[_rnd.Value.Next(clients.Length)];
            }

            if (!_clientVersions.TryGetValue(client, out var version))
            {
                version = "0.0.0";
            }

            var (peerIdPrefix, userAgentPrefix) = client switch
            {
                TorrentClient.uTorrent => ("-UT" + version.Replace(".", "") + "-", "uTorrent/"),
                TorrentClient.BitTorrent => ("-BT" + version.Replace(".", "") + "-", "BitTorrent/"),
                TorrentClient.Vuze => ("-AZ" + version.Replace(".", "") + "-", "Azureus "),
                TorrentClient.Transmission => ("-TR" + version.Replace(".", "") + "-", "Transmission "),
                TorrentClient.Deluge => ("-DE" + version.Replace(".", "") + "-", "Deluge "),
                TorrentClient.ShadowPeer => ("-SP1337-", "ShadowPeer/"),
                _ => ("-XX0000-", "Unknown")
            };

            var peerId = GeneratePeerId(peerIdPrefix);
            var userAgent = userAgentPrefix + version;

            return new ClientSignature
            {
                UserAgent = userAgent,
                Version = version,
                PeerId = peerId,
                Key = GenerateKey()
            };
        }



        private static string GeneratePeerId(string prefix)
        {
            if (prefix.Length > 20)
                throw new InvalidOperationException("Peer ID prefix is too long");

            int randomLength = 20 - prefix.Length;
            return prefix + RandomString(randomLength);
        }

        private static string GenerateKey()
        {
            // Some trackers expect an 8-digit hex key
            return RandomString(8, "0123456789ABCDEF");
        }

        private static string RandomString(int length, string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
        {
            var data = new byte[length];
            RandomNumberGenerator.Fill(data);
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[data[i] % chars.Length];
            }
            return new string(result);
        }

    }
}