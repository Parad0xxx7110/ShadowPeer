using ShadowPeer.Core;
using Spectre.Console;

class Program
{
    static async Task Main()
    {
        // Set console output encoding to UTF-8 for proper icon display
        // it is recommended to use Windows Terminal or a compatible terminal with a proper UTF-8 support.
        Console.OutputEncoding = System.Text.Encoding.UTF8;


        InitScreen.ShowPrompt();
        AnsiConsole.Clear();

       var sim = new TrafficSim(5*1024*1024, 100*1024,500*1024);

        while(!sim.isDone)
        {
            sim.Tick();

            long uploaded = sim.TotalUploadedBytes;
            long speed = sim.CurrentUploadSpeed;

            AnsiConsole.Markup($"[green]Uploaded: {uploaded / 1024} KiB, Speed: {speed / 1024} KiB/s[/]");
            await Task.Delay(1000); // Simulate a 1 second delay for the next tick
            AnsiConsole.Clear();
        }

    }
}
