using Spectre.Console;

namespace ShadowPeer.Core
{
    public class InitScreen
    {
        private static readonly string[] AsciiArtLines =
        [
        " _____ _               _ _____         ______              ",
        "/  ___| |             | |  _  |        | ___ \\             ",
        "\\ `--.| |__   __ _  __| | |/' |_      _| |_/ /__  ___ _ __ ",
        " `--. \\ '_ \\ / _` |/ _` |  /| \\ \\ /\\ / /  __/ _ \\/ _ \\ '__|",
        "/\\__/ / | | | (_| | (_| \\ |_/ /\\ V  V /| | |  __/  __/ |   ",
        "\\____/|_| |_|\\__,_|\\__,_|\\___/  \\_/\\_/ \\_|  \\___|\\___|_|   "
        ];

        public static bool ShowPrompt()
        {
            AnsiConsole.Clear();
            ShowAscii();


            int lines = AsciiArtLines.Length + 1;
            Console.SetCursorPosition(0, lines);

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Welcome to Shad0wPeer !")
                    .AddChoices(["Continue", "Quit"]));

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

        private static void ShowAscii()
        {
            Console.CursorVisible = false;

            for (int i = 0; i < AsciiArtLines.Length; i++)
            {
                Console.SetCursorPosition(0, i);
                AnsiConsole.Markup("[green]" + AsciiArtLines[i] + "[/]");
            }

        }
    }
}