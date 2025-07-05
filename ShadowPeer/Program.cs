using BencodeNET.Torrents;
using ShadowPeer.DataModels;
using ShadowPeer.Helpers;
using ShadowPeer.Core;
using Spectre.Console;

class Program
{
    static async Task Main()
    {
        // Set console output encoding to UTF-8 for proper icon display
        // it is recommended to use Windows Terminal or a compatible terminal that supports UTF-8.
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var browser = new CLIFileBrowser();

        browser.DisplayIcons = true;
        await browser.GetFilePath();
        AnsiConsole.MarkupLine("[bold green]Welcome to ShadowPeer Torrent Client![/]");


    }
}
