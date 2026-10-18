using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ObenseuerCore;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ObenseuerOS
{
    internal static class OsGameArt
    {
        private const int MaxWidth = 1920;
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly List<Entry> Entries = new List<Entry>();
        private static bool scanned;

        internal sealed class Entry
        {
            internal string Id;
            internal string Name;
            internal string FilePath;
            internal object Key;
            internal int Rev = -1;
            internal AsyncOperationHandle<Sprite> Handle;
            internal bool Loading;
            internal bool Failed;
        }

        internal static List<Entry> Ready()
        {
            List<Entry> ready = new List<Entry>();
            foreach (Entry entry in Entries) if (entry.Rev >= 0) ready.Add(entry);
            return ready;
        }

        internal static void Request(string webFolder)
        {
            if (!scanned) Scan(webFolder);
            foreach (Entry entry in Entries)
            {
                if (entry.Rev >= 0 || entry.Loading || entry.Failed) continue;
                if (File.Exists(entry.FilePath))
                {
                    entry.Rev = Revision(entry.FilePath);
                    continue;
                }
                entry.Handle = Addressables.LoadAssetAsync<Sprite>(entry.Key);
                entry.Loading = true;
            }
        }

        internal static bool Step()
        {
            foreach (Entry entry in Entries)
            {
                if (!entry.Loading || !entry.Handle.IsDone) continue;
                entry.Loading = false;
                try
                {
                    Sprite sprite = entry.Handle.Result;
                    if (sprite == null) throw new InvalidOperationException("sprite did not load");
                    Write(sprite, entry.FilePath);
                    entry.Rev = Revision(entry.FilePath);
                }
                catch (Exception e)
                {
                    entry.Failed = true;
                    Log.Warn("game background " + entry.Name + " not exported: " + e);
                }
                finally
                {
                    Addressables.Release(entry.Handle);
                }
                return entry.Rev >= 0;
            }
            return false;
        }

        private static void Scan(string webFolder)
        {
            if (LoadingScreen.instance == null) return;
            scanned = true;
            FieldInfo field = typeof(LoadingScreen).GetField("hintList", BindingFlags.Instance | BindingFlags.NonPublic);
            loadingSreenHints[] hints = field == null ? null : field.GetValue(LoadingScreen.instance) as loadingSreenHints[];
            if (hints == null)
            {
                Log.Warn("loading screen backgrounds not found");
                return;
            }
            List<string> names = new List<string>();
            for (int i = 0; i < hints.Length; i++)
            {
                loadingSreenHints hint = hints[i];
                if (hint == null || hint.hintBg == null) continue;
                string id = "hint-" + i;
                string name = string.IsNullOrEmpty(hint.title) ? "Loading screen " + (i + 1) : hint.title;
                Entries.Add(new Entry { Id = id, Name = name, Key = hint.hintBg, FilePath = Path.Combine(webFolder, "wallpapers", "game", id + ".png") });
                names.Add(id + " " + name);
            }
            Log.Info("found " + names.Count + " loading screen backgrounds");
            Log.Debug("gamewalls.found", string.Join("; ", names));
        }

        private static void Write(Sprite sprite, string path)
        {
            Texture2D source = sprite.texture;
            Rect r = sprite.textureRect;
            int width = Math.Min((int)r.width, MaxWidth);
            int height = Math.Max(1, (int)Math.Round(r.height * width / r.width));
            RenderTexture saved = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D copy = null;
            try
            {
                Graphics.Blit(source, rt, new Vector2(r.width / source.width, r.height / source.height), new Vector2(r.x / source.width, r.y / source.height));
                RenderTexture.active = rt;
                copy = new Texture2D(width, height, TextureFormat.RGB24, false);
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                copy.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(copy));
            }
            finally
            {
                RenderTexture.active = saved;
                RenderTexture.ReleaseTemporary(rt);
                if (copy != null) UnityEngine.Object.Destroy(copy);
            }
        }

        private static int Revision(string path)
        {
            return (int)(File.GetLastWriteTimeUtc(path) - Epoch).TotalSeconds;
        }
    }
}
