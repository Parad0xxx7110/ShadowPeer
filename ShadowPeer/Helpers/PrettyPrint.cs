using ShadowPeer.DataModels;
using Spectre.Console;

namespace ShadowPeer.Helpers
{
    internal class PrettyPrint
    {
        public static void PrintTrackerResponse(TrackerResponse trackerResponse)
        {
            AnsiConsole.Clear();

            var table = new Table();
            table.AddColumn("IP");
            table.AddColumn("Port");

            foreach (var peer in DataParser.ParseCompactPeers(trackerResponse.PeersCompact))
            {
                table.AddRow(peer.IP, peer.Port.ToString());
            }


            var rule = new Rule("[green]Tracker Response[/]");
            rule.Justification = Justify.Left;

            AnsiConsole.Write(rule);
            AnsiConsole.MarkupLine($"[yellow]Seeders:[/] {trackerResponse.Seeders}, [yellow]Leechers:[/] {trackerResponse.Leechers}, [yellow]Interval:[/] {trackerResponse.Interval}s\n");
            AnsiConsole.Write(table);
        }
    }
}
