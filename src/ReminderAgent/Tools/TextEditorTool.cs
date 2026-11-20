using System.Text;
using System.Text.Json;
using Anthropic.Models.Messages;

namespace ReminderAgent.Tools;

/// <summary>
/// str_replace_based_edit_tool: Anthropic's text editor tool, run on the local disk.
/// Unlike our other tools it has no schema or description of ours: the model already knows its
/// commands (view, create, str_replace, insert). We only carry them out, and only inside the notes folder.
/// </summary>
public sealed class TextEditorTool : IAgentTool
{
    // Windows paths are case-insensitive, so "Notes/../NOTES/x" must count as inside the folder there.
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string _workingDirectory;
    private readonly string _notesDirectory;

    /// <param name="notesDirectory">
    /// The only folder Claude may read or write, relative to the current directory (e.g. "notes").
    /// Claude's paths are resolved against the current directory, so they look like "notes/trip.md".
    /// </param>
    public TextEditorTool(string notesDirectory)
    {
        _workingDirectory = Directory.GetCurrentDirectory();
        _notesDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(notesDirectory, _workingDirectory));
    }

    public string Name => "str_replace_based_edit_tool";

    // The constructor sets the type and name; there is nothing else to configure.
    public ToolUnion Definition { get; } = new ToolTextEditor20250728();

    public string Execute(IReadOnlyDictionary<string, JsonElement> input)
    {
        string command = input.GetRequiredString("command");
        string path = ResolvePath(input.GetRequiredString("path"));

        return command switch
        {
            "view" => View(path, input),
            "create" => Create(path, input.GetRequiredText("file_text")),
            "str_replace" => StrReplace(path, input.GetRequiredText("old_str"), input.GetRequiredText("new_str")),
            "insert" => Insert(path, input.GetRequiredInt("insert_line"), input.GetRequiredText("insert_text")),
            _ => throw new ArgumentException(
                $"Unknown command '{command}'. Use one of: view, create, str_replace, insert."),
        };
    }

    private string View(string path, IReadOnlyDictionary<string, JsonElement> input)
    {
        if (Directory.Exists(path))
        {
            return ListDirectory(path);
        }

        if (IsNotesDirectory(path))
        {
            return $"{Display(path)}/ is empty.";
        }

        string[] lines = SplitLines(ReadExistingFile(path));
        (int start, int end) = GetViewRange(input, lines.Length);

        var output = new StringBuilder();
        for (int lineNumber = start; lineNumber <= end; lineNumber++)
        {
            output.Append(lineNumber.ToString().PadLeft(6)).Append('\t').AppendLine(lines[lineNumber - 1]);
        }

        return output.Length == 0 ? $"{Display(path)} is empty." : output.ToString();
    }

    private string Create(string path, string fileText)
    {
        // No backups or undo in this app, so create never overwrites: changes to an existing file
        // go through str_replace or insert, which touch only the lines Claude names.
        if (File.Exists(path))
        {
            throw new InvalidOperationException(
                $"{Display(path)} already exists. Use str_replace or insert to change it.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, fileText);
        return $"Created {Display(path)}.";
    }

    private string StrReplace(string path, string oldStr, string newStr)
    {
        if (oldStr.Length == 0)
        {
            throw new ArgumentException("'old_str' must not be empty.");
        }

        string text = ReadExistingFile(path);
        int first = text.IndexOf(oldStr, StringComparison.Ordinal);

        if (first < 0)
        {
            throw new InvalidOperationException(
                $"'old_str' was not found in {Display(path)}. View the file and copy the text exactly.");
        }

        if (text.IndexOf(oldStr, first + oldStr.Length, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException(
                $"'old_str' appears more than once in {Display(path)}. Include more surrounding text so it matches once.");
        }

        File.WriteAllText(path, string.Concat(text.AsSpan(0, first), newStr, text.AsSpan(first + oldStr.Length)));
        return $"Replaced 1 occurrence in {Display(path)}.";
    }

    private string Insert(string path, int insertLine, string insertText)
    {
        string text = ReadExistingFile(path);
        int lineCount = SplitLines(text).Length;

        if (insertLine < 0 || insertLine > lineCount)
        {
            throw new ArgumentException(
                $"'insert_line' must be between 0 and {lineCount} (the number of lines in {Display(path)}).");
        }

        // Character offset just after line `insertLine` (0 = start of file).
        int offset = 0;
        for (int line = 0; line < insertLine; line++)
        {
            int newline = text.IndexOf('\n', offset);
            offset = newline < 0 ? text.Length : newline + 1;
        }

        // Keep the inserted text on its own lines.
        string prefix = offset > 0 && text[offset - 1] != '\n' ? "\n" : "";
        string suffix = insertText.EndsWith('\n') ? "" : "\n";

        File.WriteAllText(path, text.Insert(offset, prefix + insertText + suffix));
        return $"Inserted text after line {insertLine} of {Display(path)}.";
    }

    /// <summary>
    /// Turns Claude's path into a full path inside the notes folder, or throws. The path is untrusted
    /// model output: "..", absolute paths and links could otherwise reach any file on the machine.
    /// </summary>
    private string ResolvePath(string path)
    {
        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path, _workingDirectory));

        bool insideNotes = IsNotesDirectory(fullPath) ||
            fullPath.StartsWith(_notesDirectory + Path.DirectorySeparatorChar, PathComparison);

        if (!insideNotes)
        {
            throw new UnauthorizedAccessException(
                $"'{path}' is outside the notes folder. Only paths under {Display(_notesDirectory)}/ are allowed.");
        }

        // A symbolic link or junction inside the notes folder could point anywhere, so refuse them.
        for (string? current = fullPath;
             current is not null && current.Length >= _notesDirectory.Length;
             current = Path.GetDirectoryName(current))
        {
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.Exists && info.LinkTarget is not null)
            {
                throw new UnauthorizedAccessException($"'{path}' goes through a link, which is not allowed.");
            }
        }

        return fullPath;
    }

    private bool IsNotesDirectory(string fullPath) => string.Equals(fullPath, _notesDirectory, PathComparison);

    private string ReadExistingFile(string path) =>
        File.Exists(path)
            ? File.ReadAllText(path)
            : throw new FileNotFoundException($"{Display(path)} does not exist.");

    private string ListDirectory(string path)
    {
        string[] entries =
        [
            .. Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).Select(d => Display(d) + "/"),
            .. Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Select(Display),
        ];

        return entries.Length == 0
            ? $"{Display(path)}/ is empty."
            : string.Join('\n', entries.Order(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Reads optional view_range [start, end] (1-based, end -1 = last line) and checks it.</summary>
    private static (int Start, int End) GetViewRange(IReadOnlyDictionary<string, JsonElement> input, int lineCount)
    {
        if (!input.TryGetValue("view_range", out JsonElement range))
        {
            return (1, lineCount);
        }

        if (range.ValueKind != JsonValueKind.Array || range.GetArrayLength() != 2 ||
            !range[0].TryGetInt32(out int start) || !range[1].TryGetInt32(out int end))
        {
            throw new ArgumentException("'view_range' must be two whole numbers: [start_line, end_line].");
        }

        if (end == -1)
        {
            end = lineCount;
        }

        if (start < 1 || start > end || end > lineCount)
        {
            throw new ArgumentException(
                $"'view_range' [{start}, {end}] is outside the file, which has {lineCount} lines.");
        }

        return (start, end);
    }

    /// <summary>Splits file text into lines; a trailing newline does not count as an extra empty line.</summary>
    private static string[] SplitLines(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        string withoutFinalNewline = text.EndsWith('\n') ? text[..^1] : text;
        return [.. withoutFinalNewline.Split('\n').Select(line => line.TrimEnd('\r'))];
    }

    /// <summary>The path as Claude should see it: relative to the current directory, with forward slashes.</summary>
    private string Display(string fullPath) =>
        Path.GetRelativePath(_workingDirectory, fullPath).Replace('\\', '/');
}
