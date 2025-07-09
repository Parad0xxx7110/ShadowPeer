using Spectre.Console;

// Code by Cyril "Parad0x" Bouvier
// Inspired by Lutonet code -> https://github.com/Lutonet/SpectreConsoleFileBrowser

// STABLE & SEALED


namespace ShadowPeer.Core
{
    public class FileBrowser
    {
        public bool DisplayIcons { get; set; } = true;
        public static bool IsWindows => Environment.OSVersion.Platform.ToString().ToLower().StartsWith("win");
        public int PageSize { get; set; } = 15;
        public bool CanCreateFolder { get; set; } = true;

        public string ActualFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public string? SelectedFile { get; set; }

        // UI Texts
        public string LevelUpText { get; set; } = "Go to upper level";
        public string ActualFolderText { get; set; } = "Selected Folder";
        public string MoreChoicesText { get; set; } = "Use arrows Up and Down to select";
        public string CreateNewText { get; set; } = "Create new folder";
        public string SelectFileText { get; set; } = "Select File";
        public string SelectFolderText { get; set; } = "Select Folder";
        public string SelectDriveText { get; set; } = "Select Drive";
        public string SelectActualText { get; set; } = "Select Actual Folder";

        private const string DriveCommand = "__DRIVE__";
        private const string NewFolderCommand = "__NEW__";
        private const string CurrentFolderCommand = "__CURRENT__";

        public async Task<string> GetFilePath(string? folder = null) => await GetPath(folder ?? ActualFolder, true);
        public async Task<string> GetFolderPath(string? folder = null) => await GetPath(folder ?? ActualFolder, false);

        private async Task<string> GetPath(string initialFolder, bool selectFile)
        {
            return await Task.Run(() =>
            {
                string currentFolder = initialFolder;

                while (true)
                {
                    AnsiConsole.Clear();
                    DrawHeader(selectFile);
                    DrawCurrentPath(currentFolder);

                    var choices = BuildChoices(currentFolder, selectFile);
                    string selection = PromptSelection(selectFile, choices.Keys);
                    string selectedPath = choices[selection];

                    switch (selectedPath)
                    {
                        case DriveCommand:
                            currentFolder = PromptDriveSelection();
                            break;

                        case NewFolderCommand:
                            currentFolder = TryCreateNewFolder(currentFolder);
                            break;

                        case CurrentFolderCommand:
                            return currentFolder;

                        default:
                            if (Directory.Exists(selectedPath))
                                currentFolder = selectedPath;
                            else if (File.Exists(selectedPath))
                                return selectedPath;
                            break;
                    }
                }
            });
        }

        private void DrawHeader(bool isFileMode)
        {
            var header = new Rule($"[b][green]{(isFileMode ? SelectFileText : SelectFolderText)}[/][/]").Centered();
            AnsiConsole.WriteLine();
            AnsiConsole.Write(header);
            AnsiConsole.WriteLine();
        }

        private void DrawCurrentPath(string path)
        {
            AnsiConsole.Markup($"[b][yellow]{ActualFolderText}: [/][/]");
            var textPath = new TextPath(path)
            {
                RootStyle = new Style(foreground: Color.Green),
                SeparatorStyle = new Style(foreground: Color.Green),
                StemStyle = new Style(foreground: Color.Blue),
                LeafStyle = new Style(foreground: Color.Yellow)
            };
            AnsiConsole.Write(textPath);
            AnsiConsole.WriteLine();
        }

        private Dictionary<string, string> BuildChoices(string folder, bool isFileMode)
        {
            var choices = new Dictionary<string, string>();

            if (IsWindows)
                choices.Add(SafeFormatChoice(":computer_disk:", SelectDriveText), DriveCommand);

            var parent = Directory.GetParent(folder);
            if (parent != null)
                choices.Add(SafeFormatChoice(":upwards_button:", LevelUpText), parent.FullName);

            if (!isFileMode)
                choices.Add(SafeFormatChoice(":ok_button:", SelectActualText), CurrentFolderCommand);

            if (CanCreateFolder)
                choices.Add(SafeFormatChoice(":plus:", CreateNewText), NewFolderCommand);

            foreach (var dir in SafeGet(() => Directory.GetDirectories(folder)))
            {
                string name = Path.GetFileName(dir);
                choices.Add(SafeFormatChoice(":file_folder:", name), dir);
            }

            if (isFileMode)
            {
                foreach (var file in SafeGet(() => Directory.GetFiles(folder)))
                {
                    string name = Path.GetFileName(file);
                    choices.Add(SafeFormatChoice(":abacus:", name), file);
                }
            }

            return choices;
        }

        private string PromptSelection(bool isFileMode, IEnumerable<string> options)
        {
            return AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[green]{(isFileMode ? SelectFileText : SelectFolderText)}:[/]")
                    .PageSize(PageSize)
                    .MoreChoicesText($"[grey]{MoreChoicesText}[/]")
                    .AddChoices(options)
            );
        }

        private string PromptDriveSelection()
        {
            var drives = Directory.GetLogicalDrives();
            var map = drives.ToDictionary(
                d => SafeFormatChoice(":computer_disk:", d),
                d => d
            );

            AnsiConsole.Clear();
            DrawHeader(false);

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[green]{SelectDriveText}:[/]")
                    .PageSize(PageSize)
                    .MoreChoicesText($"[grey]{MoreChoicesText}[/]")
                    .AddChoices(map.Keys)
            );

            return map[selected];
        }

        private string TryCreateNewFolder(string baseFolder)
        {
            string name = AnsiConsole.Ask<string>($"[blue]{CreateNewText}: [/]", "new_folder");
            if (!string.IsNullOrWhiteSpace(name))
            {
                string newPath = Path.Combine(baseFolder, name);
                try
                {
                    Directory.CreateDirectory(newPath);
                    return newPath;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
                }
            }
            return baseFolder;
        }

        private string SafeFormatChoice(string emoji, string label)
        {
            string escapedLabel = Markup.Escape(label);
            return DisplayIcons
                ? $"{emoji} [green]{escapedLabel}[/]"
                : $"[green]{escapedLabel}[/]";
        }

        private static string[] SafeGet(Func<string[]> getter)
        {
            try { return getter(); } catch { return []; }
        }
    }
}
