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
        public TorrentMetadatas Torrent { get; private set; }

        public TorrentHandler(string torrentFilePath)
        {
            TorrentFilePath = torrentFilePath ?? throw new ArgumentNullException(nameof(torrentFilePath));
            Torrent = null!;
        }

        public async Task<TorrentMetadatas> LoadTorrentAsync()
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
                var torrent = await parser.ParseAsync<Torrent>(fs);

                var torrentMetas = TorrentMetadatas.MapFromBencodeTorrent(torrent);
                torrentMetas.AnnounceUrls = TorrentHelper.GetPrimaryAnnounceUrl(torrentMetas);

                if (!string.IsNullOrEmpty(torrentMetas.AnnounceUrls))
                {
                    torrentMetas.PassKey = await TorrentHelper.ExtractPassKeyAsync(torrentMetas.AnnounceUrls);
                }

                Torrent = torrentMetas;
                return torrentMetas;
            }
            catch (Exception ex)
            {
                Debug.WriteLine ($"Error parsing torrent file: {ex.Message}");
                return null;
            }
        }

        
    }
}