using BencodeNET.Parsing;
using BencodeNET.Torrents;
using ShadowPeer.DataModels;
using ShadowPeer.Helpers;
using System.Diagnostics;

namespace ShadowPeer.Core
{
    internal class TorrentHandler
    {
        public string TorrentFilePath { get; }
        public Torrents Torrent { get; private set; }

        public TorrentHandler(string torrentFilePath)
        {
            TorrentFilePath = torrentFilePath ?? throw new ArgumentNullException(nameof(torrentFilePath));
            Torrent = null!; 
        }

        public async Task<Torrents> LoadTorrentAsync()
        {
            try
            {
                if (!File.Exists(TorrentFilePath))
                {
                    Debug.WriteLine($"Torrent file not found: {TorrentFilePath}");
                    return null;
                }

                await using var fs = new FileStream(TorrentFilePath, FileMode.Open, FileAccess.Read);

                if (fs.Length == 0)
                {
                    Debug.WriteLine("Torrent file is empty / invalid file");
                    return null;
                }

                var parser = new BencodeParser();
                var torrent = await parser.ParseAsync<BencodeNET.Torrents.Torrent>(fs);

                var torrentMdl = Torrents.MapFromBencodeTorrent(torrent);
                torrentMdl.AnnounceUrls = GetPrimaryAnnounceUrl(torrentMdl);

                if (!string.IsNullOrEmpty(torrentMdl.AnnounceUrls))
                {
                    torrentMdl.PassKey = await PassKeyExtractor.ExtractPassKeyAsync(torrentMdl.AnnounceUrls);
                }

                Torrent = torrentMdl;
                return torrentMdl;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing torrent file: {ex.Message}");
                return null;
            }
        }

        private static string? GetPrimaryAnnounceUrl(Torrents torrent)
        {
            if (torrent.Trackers == null || torrent.Trackers.Count == 0)
            {
                Debug.WriteLine("No trackers available in torrent");
                return null;
            }

            // Take the first tracker from the list, private tracker use only one tracker most of the time 
            return torrent.Trackers.FirstOrDefault()?.FirstOrDefault() ?? string.Empty;
        }
    }
}