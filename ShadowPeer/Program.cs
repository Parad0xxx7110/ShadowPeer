using ShadowPeer.Core;
using Spectre.Console;

class Program
{
    static async Task Main()
    {

        Console.OutputEncoding = System.Text.Encoding.UTF8;

        InitScreen.ShowPrompt();
        AnsiConsole.Clear();

        string torrentFilePath = "C:\\kam.torrent";
        var torrentHandler = new TorrentHandler(torrentFilePath);

        var torrentMetas = await torrentHandler.LoadTorrentAsync();

        var signature = ClientSignatureFactory.Create(TorrentClient.Random);

        var builder = new AnnounceBuilder(torrentMetas);




        long minSpeed = 5 * 1024 * 1024; // 5 MB/s
        long maxSpeed = 15 * 1024 * 1024; // 15 MB/s
       long targetUpload = 10L * 1024 * 1024 * 1024; // 10 GB

        var peerSim = new PeerTrafficSim(-1,minSpeed,maxSpeed);

        var announceEngine = new AnnounceEngine(torrentMetas, signature, builder,peerSim);

        await announceEngine.StartEngineAsync();

        



    }
}
