using System;
using System.Threading.Tasks;
using ShadowPeer;
using ShadowPeer.Core;
using Spectre.Console;

class Program
{
    static async Task Main(string[] args)
    {
        string path = "C:\\figaro.torrent";

        try
        {
            var decoder = new TorrentHandler(path);

            var torrent = await decoder.LoadTorrentAsync();
            if (torrent == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to load torrent. Aborting.[/]");
                return;
            }

        
            AnsiConsole.MarkupLine("[green]Torrent info:[/]");

            AnsiConsole.WriteLine(torrent.ToString());
            Network.SendTCPTest();

        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Unexpected error: {ex.Message}[/]");
        }
    }
}
