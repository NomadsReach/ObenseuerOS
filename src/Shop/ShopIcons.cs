using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using ObenseuerCore;
using UnityEngine;

namespace ObenseuerOS
{
    internal static class ShopIcons
    {
        internal const string Folder = "icons";
        internal const string Host = "https://icons.nomad-obenseuer-os.obx/";
        internal const int MaxSide = 256;
        private const string VersionFile = "version";
        internal const int MaxFiles = 500;
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static readonly HashSet<int> missing = new HashSet<int>();

        internal static string Dir
        {
            get
            {
                return Path.Combine(Paths.BepInExRootPath, "cache", "ObenseuerOS", Folder);
            }
        }

        internal static void Map(WebView view)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                Invalidate(Dir);
            }
            catch (Exception e)
            {
                Log.Warn("shop icon folder not ready: " + e.Message);
                return;
            }
            if (!view.MapFolder(Folder, Dir)) Log.Warn("shop icon folder not mapped: " + WebUi.LastError);
        }

        private static void Invalidate(string dir)
        {
            string build = Application.version + "|" + Application.buildGUID;
            string file = Path.Combine(dir, VersionFile);
            if (File.Exists(file) && File.ReadAllText(file) == build) return;
            foreach (string png in Directory.GetFiles(dir, "*.png")) File.Delete(png);
            File.WriteAllText(file, build);
        }

        internal static string Url(int itemId)
        {
            if (itemId <= 1) return null;
            string file = itemId + ".png";
            if (File.Exists(Path.Combine(Dir, file))) return Host + file;
            if (missing.Contains(itemId)) return null;
            if (Export(itemId, Path.Combine(Dir, file))) return Host + file;
            missing.Add(itemId);
            return null;
        }

        private static bool Export(int itemId, string path)
        {
            RenderTexture saved = RenderTexture.active;
            RenderTexture target = null;
            Texture2D copy = null;
            try
            {
                Sprite sprite = Items.Icon(itemId);
                if (sprite == null || sprite.texture == null || sprite.textureRect.width < 1f || sprite.textureRect.height < 1f)
                {
                    Log.Warn("shop icon " + itemId + " has no sprite, the letter is shown");
                    return false;
                }
                if (Directory.GetFiles(Dir, "*.png").Length >= MaxFiles)
                {
                    Log.Warn("shop icon cache holds " + MaxFiles + " files, the letter is shown");
                    return false;
                }
                Texture source = sprite.texture;
                Rect rect = sprite.textureRect;
                float scale = Mathf.Min(1f, MaxSide / Mathf.Max(rect.width, rect.height));
                int width = Mathf.Max(1, Mathf.RoundToInt(rect.width * scale));
                int height = Mathf.Max(1, Mathf.RoundToInt(rect.height * scale));
                target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(source, target, new Vector2(rect.width / source.width, rect.height / source.height), new Vector2(rect.x / source.width, rect.y / source.height));
                RenderTexture.active = target;
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                copy.Apply();
                string temp = path + ".tmp";
                File.WriteAllBytes(temp, ImageConversion.EncodeToPNG(copy));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                Log.Info("shop icon " + itemId + " exported " + width + "x" + height);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("shop icon " + itemId + " not exported: " + e.Message);
                return false;
            }
            finally
            {
                RenderTexture.active = saved;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (copy != null) UnityEngine.Object.Destroy(copy);
            }
        }
    }
}
