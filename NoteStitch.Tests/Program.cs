using NoteStitch;

var operations = new List<string>();
var windowStateFolder = @"C:\Notepad\WindowState";

Win11NotepadSessionCloser.CloseAll(
    [42],
    [@"C:\Notepad\TabState\tab.bin"],
    windowStateFolder,
    pid =>
    {
        operations.Add($"stop:{pid}");
        return true;
    },
    path => operations.Add($"delete:{path}"),
    folder =>
    {
        operations.Add($"enumerate:{folder}");
        return [Path.Combine(folder, "window.bin")];
    });

string[] expected =
[
    "stop:42",
    @"delete:C:\Notepad\TabState\tab.bin",
    $@"enumerate:{windowStateFolder}",
    $@"delete:{windowStateFolder}\window.bin"
];

if (!operations.SequenceEqual(expected))
{
    throw new Exception(
        "Windows 11 Notepad state must be removed only after every process has stopped.\n" +
        $"Expected: {string.Join(", ", expected)}\n" +
        $"Actual:   {string.Join(", ", operations)}");
}

Console.WriteLine("PASS: Windows 11 session state is cleared after Notepad exits.");

operations.Clear();
Win11NotepadSessionCloser.CloseAll(
    [42],
    [@"C:\Notepad\TabState\tab.bin"],
    windowStateFolder,
    _ => false,
    path => operations.Add($"delete:{path}"),
    _ => [Path.Combine(windowStateFolder, "window.bin")]);

if (operations.Count != 0)
    throw new Exception("Session state must not be deleted while a Notepad process is still running.");

Console.WriteLine("PASS: session cleanup is aborted if Notepad does not exit.");
