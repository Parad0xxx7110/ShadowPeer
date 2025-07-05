using Spectre.Console;
using System;

public class InitScreen
{
    private static readonly string[] AsciiArtLines = new[]
    {
        " _____ _               _ _____         ______              ",
        "/  ___| |             | |  _  |        | ___ \\             ",
        "\\ `--.| |__   __ _  __| | |/' |_      _| |_/ /__  ___ _ __ ",
        " `--. \\ '_ \\ / _` |/ _` |  /| \\ \\ /\\ / /  __/ _ \\/ _ \\ '__|",
        "/\\__/ / | | | (_| | (_| \\ |_/ /\\ V  V /| | |  __/  __/ |   ",
        "\\____/|_| |_|\\__,_|\\__,_|\\___/  \\_/\\_/ \\_|  \\___|\\___|_|   "
    };

    public bool ShowPrompt()
    {
        AnsiConsole.Clear();
        DrawAscii();

       
        int promptLine = AsciiArtLines.Length + 1;
        Console.SetCursorPosition(0, promptLine);

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Welcome to Shad0wPeer !")
                .AddChoices(new[] { "Continue", "Quit" }));

        if (choice == "Quit")
        {
            Console.WriteLine("Exiting...");
            return false;
        }
        else
        {
            return true;
        }
    }

    private void DrawAscii()
    {
        Console.CursorVisible = false;
        for (int i = 0; i < AsciiArtLines.Length; i++)
        {
            Console.SetCursorPosition(0, i);
            AnsiConsole.Markup("[green]" + AsciiArtLines[i] + "[/]");
        }
        
    }
}
