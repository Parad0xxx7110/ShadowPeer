using Spectre.Console;

class Program
{
    static async Task Main()
    {
        // Set console output encoding to UTF-8 for proper icon display
        // it is recommended to use Windows Terminal or a compatible terminal with a proper UTF-8 support.
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var initScreen = new InitScreen();
        initScreen.ShowPrompt();
        AnsiConsole.Clear();



    }
}
