using System;
using System.Collections.Generic;
using System.Linq;
using ObenseuerOS;

internal sealed class Disk : NotepadStorage, ExplorerStorage
{
    internal readonly Dictionary<string, string> Data = new Dictionary<string, string>();
    public string[] List() { return Data.Keys.ToArray(); }
    public string Read(string path) { return Data[path]; }
    public void Write(string path, string value) { if (path.Contains("..")) throw new InvalidOperationException("invalid file path"); Data[path] = value; }
    public bool Valid(string path) { return true; }
    public void Opened(string path) { }
    public string[] Folders() { return Array.Empty<string>(); }
    public ExplorerEntry[] ListFiles(string folder) { return Data.Where(x => x.Key.StartsWith(folder + "/", StringComparison.Ordinal)).Select(x => new ExplorerEntry(x.Key, x.Value.Length)).ToArray(); }
    public string CreateDocument() { throw new NotSupportedException(); }
    public void DeleteFile(string path) { throw new NotSupportedException(); }
}

internal static class Program
{
    private static void Check(bool value) { if (!value) throw new Exception("Terminal check failed"); }
    private static void Main()
    {
        Disk disk = new Disk();
        disk.Data["/docs/hello.txt"] = "hello";
        string opened = null;
        bool closed = false;
        TerminalSession terminal = new TerminalSession(disk, disk, () => "Ricky", () => "date", () => "time", () => "money", app => app == "files", path => opened = path, () => closed = true);
        Check(terminal.Prompt == "C:\\Users\\Ricky\\Documents>");
        Check(terminal.Run("type hello.txt") == "hello");
        Check(terminal.Run("dir").Contains("hello.txt"));
        Check(terminal.Run("notepad new.txt") == "" && opened == "/docs/new.txt" && disk.Data.ContainsKey(opened));
        Check(terminal.Run("cd ..") == "" && terminal.Run("dir").Contains("Downloads"));
        Check(terminal.Run("cd Downloads") == "" && terminal.Prompt.EndsWith("\\Downloads>"));
        Check(terminal.Run("type missing.txt").Contains("cannot find"));
        Check(terminal.Run("start files") == "");
        Check(terminal.Run("start unknown").Contains("cannot find"));
        Check(terminal.Recall(-1) == "start unknown" && terminal.Recall(1) == "");
        terminal.Run("cls"); Check(terminal.ClearRequested);
        terminal.Run("exit"); Check(closed && !terminal.ClearRequested);
        Console.WriteLine("Terminal checks passed");
    }
}
