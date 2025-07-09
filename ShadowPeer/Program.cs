using ShadowPeer.Core;
using Spectre.Console;

class Program
{
    static async Task Main()
    {

        Console.OutputEncoding = System.Text.Encoding.UTF8;

        InitScreen.ShowPrompt();
        AnsiConsole.Clear();

        string torrentFilePath = "C:\\figaro.torrent";
        var torrentHandler = new TorrentHandler(torrentFilePath);

        var torrentMetas = await torrentHandler.LoadTorrentAsync();

        var signature = ClientSignatureFactory.Create(TorrentClient.Random);

        var builder = new AnnounceBuilder(torrentMetas);

        var announceEngine = new AnnounceEngine(torrentMetas, signature, builder);

        await announceEngine.FirstAnnounce(builder);

        



    }
}
