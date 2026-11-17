using System;
using System.IO;
using System.Reflection;

internal static class Program
{
    private static void Main(string[] args)
    {
        string[] icons = Directory.GetFiles("web/icons", "*.svg");
        Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        foreach (string source in icons)
        {
            string icon = Path.GetFileNameWithoutExtension(source);
            using (Stream resource = assembly.GetManifestResourceStream("desktop-icons/" + icon + ".png"))
            {
                if (resource == null) throw new Exception("Missing icon: " + icon);
                foreach (byte expected in signature) if (resource.ReadByte() != expected) throw new Exception("Invalid PNG: " + icon);
            }
        }
        using (Stream resource = assembly.GetManifestResourceStream("desktop-icons/viewports.png"))
        {
            if (resource == null) throw new Exception("Missing icon: viewports");
            foreach (byte expected in signature) if (resource.ReadByte() != expected) throw new Exception("Invalid PNG: viewports");
        }
        Console.WriteLine("All Toolkit desktop icons are embedded.");
    }
}
