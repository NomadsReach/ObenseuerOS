using System;
using System.IO;
using System.Reflection;

internal static class Program
{
    private static void Main(string[] args)
    {
        string[] icons = { "mail", "world", "folder-filled", "trash", "settings", "chart-line", "terminal-2", "notes", "building-bank", "news", "message-circle", "shopping-cart", "device-gamepad-2", "x", "viewports" };
        Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        foreach (string icon in icons)
        {
            using (Stream resource = assembly.GetManifestResourceStream("desktop-icons/" + icon + ".png"))
            {
                if (resource == null) throw new Exception("Missing icon: " + icon);
                foreach (byte expected in signature) if (resource.ReadByte() != expected) throw new Exception("Invalid PNG: " + icon);
            }
        }
        Console.WriteLine("All Toolkit desktop icons are embedded.");
    }
}
