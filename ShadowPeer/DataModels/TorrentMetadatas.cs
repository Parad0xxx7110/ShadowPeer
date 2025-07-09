using BencodeNET.Torrents;
using ShadowPeer.Helpers;
using Spectre.Console;
using System.Text.RegularExpressions;

namespace ShadowPeer.DataModels
{


    public class TorrentMetadatas
    {
        private string _name = string.Empty;
        private long _size = 0;
        private string _hash = string.Empty;
        private byte[] _hashBytes = [];
        private string _comment = string.Empty;
        private string _createdBy = string.Empty;
        private string _creationDate = string.Empty;
        private string _passKey = string.Empty;
        private string _announceUrl = string.Empty;
        private string _host = string.Empty;
        private string _port = string.Empty;

        private IList<IList<string>> _trackerList = new List<IList<string>>();

        public required string Name
        {
            get => _name;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _name = "Unnamed Torrent";
                }
                else if (value.Length > 255)
                {
                    throw new ArgumentException("Name too long (max 255 chars).");
                }
                else
                {
                    _name = value.Trim();
                }
            }
        }

        public required long Size
        {
            get => _size;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentException("Size cannot be negative.");
                }
                _size = value;
            }
        }

        public required string Comment
        {
            get => _comment;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _comment = "No comment provided.";
                }
                else if (value.Length > 1024)
                {
                    throw new ArgumentException("Comment too long (max 1024 chars).");
                }
                else
                {
                    _comment = value.Trim();
                }
            }
        }

        public required string CreatedBy
        {
            get => _createdBy;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _createdBy = "Unknown";
                }
                else if (value.Length > 255)
                {
                    throw new ArgumentException("CreatedBy too long (max 255 chars).");
                }
                else
                {
                    _createdBy = value.Trim();
                }
            }
        }

        public required string CreationDate
        {
            get => _creationDate;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _creationDate = "Unknown";
                }
                else
                {
                    // ISO Format validation (yyyy-MM-dd ou yyyy-MM-dd HH:mm:ss)
                    if (!Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}( \d{2}:\d{2}:\d{2})?$"))
                    {
                        throw new ArgumentException("CreationDate must be in 'yyyy-MM-dd' or 'yyyy-MM-dd HH:mm:ss' format.");
                    }
                    _creationDate = value;
                }
            }
        }

        public required string InfoHash
        {
            get => _hash;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _hash = "Unknown";
                }
                else if (value.Length != 40 || !Regex.IsMatch(value, @"\A\b[0-9a-fA-F]+\b\Z"))
                {
                    throw new ArgumentException("InfoHash must be a 40-character hexadecimal string.");
                }
                else
                {
                    _hash = value.ToLowerInvariant();
                }
            }
        }

        public required byte[] InfoHashBytes
        {
            get => _hashBytes;
            set
            {
                if (value == null || value.Length != 20)
                {
                    throw new ArgumentException("InfoHashBytes must be a 20-byte array.");
                }
                _hashBytes = value;
            }
        }

        public required string PassKey
        {
            get => _passKey;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _passKey = "No passkey provided / found.";
                }
                else if (value.Length > 255)
                {
                    throw new ArgumentException("PassKey too long (max 255 chars).");
                }
                else
                {
                    _passKey = value.Trim();
                }
            }
        }

        public required IList<IList<string>> Trackers
        {
            get => _trackerList;
            set
            {
                if (value == null || value.Count == 0)
                {
                    _trackerList = new List<IList<string>> { new List<string> { "No trackers available." } };
                }
                else
                {
                    _trackerList = value;
                }
            }
        }

        public required string AnnounceUrls
        {
            get => _announceUrl;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _announceUrl = "No announce URL provided.";
                    _host = "Unknown";
                    _port = "Unknown";
                }
                else
                {
                    _announceUrl = value.Trim();

                    try
                    {
                        var uri = new Uri(_announceUrl);
                        _host = uri.Host;
                        _port = uri.Port.ToString();
                    }
                    catch
                    {
                        _host = "Invalid URL";
                        _port = "Invalid URL";
                    }
                }
            }
        }

        public string Host => _host;
        public string Port => _port;

        // Manual mapping from BencodeNET Torrent obj to this model for validation and fallbacks.
        public static TorrentMetadatas MapFromBencodeTorrent(Torrent torrent)
        {
            if (torrent == null)
                throw new ArgumentNullException(nameof(torrent), "Torrent object cannot be null.");

            return new TorrentMetadatas
            {
                Name = torrent.DisplayName,
                Size = torrent.TotalSize,
                Comment = torrent.Comment,
                CreatedBy = torrent.CreatedBy,
                CreationDate = torrent.CreationDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown",
                InfoHash = torrent.GetInfoHash(),
                InfoHashBytes = torrent.GetInfoHashBytes(),
                PassKey = string.Empty,
                AnnounceUrls = string.Empty,
                Trackers = torrent.Trackers ?? new List<IList<string>> { new List<string> { "No trackers available." } },
            };
        }

        // Shouldn't be here but i'm lazy AF.
        public void ShowTorrentMeta()
        {
            string urlEncodedHash = DataParser.InfoHashBytesToUrl(InfoHashBytes);

            var table = new Table()
                .AddColumn(new TableColumn("[u]Property[/]").Centered())
                .AddColumn(new TableColumn("[u]Value[/]"))
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey);

            table.AddRow("Name", $"[bold green]{Name}[/]");
            table.AddRow("Comment", string.IsNullOrWhiteSpace(Comment) ? "[grey]No comment provided.[/]" : Comment);
            table.AddRow("Created By", CreatedBy ?? "[grey]Unknown[/]");
            table.AddRow("Creation Date", CreationDate);
            table.AddRow("InfoHash", $"[bold blue]{InfoHash}[/]");
            table.AddRow("UrlEncoded Infohash", $"[italic]{urlEncodedHash}[/]");
            table.AddRow("PassKey", string.IsNullOrWhiteSpace(PassKey) ? "[grey]None[/]" : $"[bold yellow]{PassKey}[/]");
            table.AddRow("Host", Host);
            table.AddRow("Port", Port);

            if (!string.IsNullOrWhiteSpace(AnnounceUrls) && AnnounceUrls != "No announce URL provided.")
            {
                table.AddRow("Announce URLs", AnnounceUrls);
            }

            if (Trackers?.Any() == true)
            {
                int groupIndex = 1;
                foreach (var group in Trackers)
                {
                    table.AddRow($"Tracker Group {groupIndex++}", string.Join(", ", group));
                }
            }
            else
            {
                table.AddRow("Trackers", "[grey]No trackers found[/]");
            }

            AnsiConsole.Write(new Panel(table)
                .Header("[bold underline blue]Loaded torrent info[/]", Justify.Center)
                .BorderColor(Color.CadetBlue)
                .Padding(1, 1));
        }
    }
}
