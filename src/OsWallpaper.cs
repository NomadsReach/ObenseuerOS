using System;
using System.IO;
using ObenseuerCore;
using UnityEngine;
using UnityEngine.UI;

namespace ObenseuerOS
{
    internal static class OsWallpaper
    {
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static int lastId;
        private static int revision = Environment.TickCount & int.MaxValue;

        internal static int Capture(RectTransform screen, string webFolder)
        {
            if (screen == null) return -1;
            Image image = screen.GetComponent<Image>();
            if (image == null || image.sprite == null) return -1;
            Sprite sprite = image.sprite;
            int id = sprite.GetInstanceID();
            string path = Path.Combine(webFolder, "wallpapers", "game.jpg");
            if (id == lastId && File.Exists(path)) return revision;
            RenderTexture saved = RenderTexture.active;
            RenderTexture rt = null;
            Texture2D copy = null;
            try
            {
                Texture2D source = sprite.texture;
                Rect r = sprite.textureRect;
                rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                copy = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGB24, false);
                copy.ReadPixels(r, 0, 0);
                copy.Apply();
                byte[] jpg = ImageConversion.EncodeToJPG(copy, 90);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, jpg);
                lastId = id;
                return ++revision;
            }
            catch (Exception e)
            {
                Log.Warn("computer wallpaper not captured: " + e);
                return -1;
            }
            finally
            {
                RenderTexture.active = saved;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (copy != null) UnityEngine.Object.Destroy(copy);
            }
        }
    }
}
