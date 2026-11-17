using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal sealed class TerminalSession
    {
        internal const string Banner = "Viewports [Version 8]";
        private readonly NotepadStorage files;
        private readonly ExplorerStorage folders;
        private readonly Func<string> user, date, time, money;
        private readonly Func<string, bool> openApp;
        private readonly Action<string> openFile;
        private readonly Action close;
        private readonly List<string> history = new List<string>();
        private int historyIndex;
        private string folder = "Documents";
        internal bool ClearRequested { get; private set; }

        internal TerminalSession(NotepadStorage files, ExplorerStorage folders, Func<string> user, Func<string> date, Func<string> time, Func<string> money, Func<string, bool> openApp, Action<string> openFile, Action close)
        {
            this.files = files; this.folders = folders; this.user = user; this.date = date; this.time = time; this.money = money;
            this.openApp = openApp; this.openFile = openFile; this.close = close;
        }

        internal string Prompt { get { return "C:\\Users\\" + user() + (folder == null ? "" : "\\" + folder) + ">"; } }

        internal string Recall(int direction)
        {
            historyIndex = Math.Max(0, Math.Min(history.Count, historyIndex + direction));
            return historyIndex < history.Count ? history[historyIndex] : "";
        }

        internal string Run(string raw)
        {
            ClearRequested = false;
            string text = (raw ?? "").Trim();
            if (text.Length == 0) return "";
            history.Add(text);
            if (history.Count > 50) history.RemoveAt(0);
            historyIndex = history.Count;
            int split = text.IndexOfAny(new[] { ' ', '\t' });
            string command = (split < 0 ? text : text.Substring(0, split)).ToLowerInvariant();
            string arg = split < 0 ? "" : text.Substring(split).Trim();
            switch (command)
            {
                case "cls": ClearRequested = true; return "";
                case "ver": return Banner;
                case "echo": return arg.Length == 0 ? "ECHO is on." : arg;
                case "help": return "CD        Show or change the current folder.\nCLS       Clear the screen.\nDATE      Show the game date.\nDIR       List the files in the current folder.\nECHO      Show a line of text.\nEXIT      Close Command Prompt.\nHELP      Show this list.\nMONEY     Show your OC and RM.\nNOTEPAD   Open a file in Notepad.\nSTART     Open an app by name.\nTIME      Show the game time.\nTYPE      Show the contents of a file.\nVER       Show the version.\nWHOAMI    Show the player name.\nType an available app name, such as files, to open it.";
                case "whoami": return user();
                case "date": return date();
                case "time": return time();
                case "money": return money();
                case "exit": close(); return "";
                case "cd":
                    if (arg.Length == 0) return Prompt.TrimEnd('>');
                    if (arg == ".." || arg == "\\") folder = null;
                    else if (string.Equals(arg, "Documents", StringComparison.OrdinalIgnoreCase)) folder = "Documents";
                    else if (string.Equals(arg, "Downloads", StringComparison.OrdinalIgnoreCase)) folder = "Downloads";
                    else return "The system cannot find the path specified.";
                    return "";
                case "dir": return List();
                case "type": return Read(arg, false);
                case "notepad": return Read(arg, true);
                case "start":
                    if (arg.Length == 0) return "The syntax of the command is incorrect.";
                    return openApp(arg) ? "" : "Viewports cannot find '" + arg + "'. Make sure you typed the name correctly, and then try again.";
                default:
                    return openApp(command) ? "" : "'" + (split < 0 ? text : text.Substring(0, split)) + "' is not recognized as an internal or external command, operable program or batch file.";
            }
        }

        private string List()
        {
            string heading = "Directory of " + Prompt.TrimEnd('>') + "\n\n";
            if (folder == null) return heading + "<DIR>          Documents\n<DIR>          Downloads";
            if (folders == null) return "Files are available when opened through the OS.";
            ExplorerEntry[] entries = folders.ListFiles(folder == "Documents" ? "/docs" : "/downloads");
            if (entries.Length == 0) return heading + "File Not Found";
            List<string> rows = new List<string>();
            foreach (ExplorerEntry item in entries) rows.Add(item.Path.Substring(item.Path.LastIndexOf('/') + 1).PadRight(32) + item.Bytes + " bytes");
            return heading + string.Join("\n", rows.ToArray()) + "\n\n" + entries.Length + " File(s)";
        }

        private string Read(string name, bool edit)
        {
            if (name.Length == 0)
            {
                if (edit) { openFile(null); return ""; }
                return "The syntax of the command is incorrect.";
            }
            if (folder == null) return "The system cannot find the file specified.";
            if (files == null) return "Files are available when opened through the OS.";
            string path = (folder == "Documents" ? "/docs/" : "/downloads/") + name;
            bool exists = false;
            if (folders != null)
                foreach (ExplorerEntry entry in folders.ListFiles(folder == "Documents" ? "/docs" : "/downloads"))
                    if (entry.Path == path) { exists = true; break; }
            if (!exists)
            {
                if (!edit) return "The system cannot find the file specified.";
                files.Write(path, "");
            }
            if (edit) { openFile(path); return ""; }
            return files.Read(path);
        }
    }
}
