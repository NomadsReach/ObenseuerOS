using System.Security.Cryptography;
using System.Text;

namespace ObenseuerOS
{
    internal static class WikiImage
    {
        internal static bool Fits(byte[] data, int maxSide)
        {
            long width, height;
            if (data == null || !Dimensions(data, out width, out height)) return false;
            return width >= 1 && height >= 1 && width <= maxSide && height <= maxSide;
        }
        internal static string Hash(string text)
        {
            using (SHA1 sha = SHA1.Create())
            {
                StringBuilder result = new StringBuilder();
                foreach (byte b in sha.ComputeHash(new UTF8Encoding(false).GetBytes(text))) result.Append(b.ToString("x2"));
                return result.ToString();
            }
        }
        internal static bool At(byte[] data, int offset, string magic)
        {
            if (data.Length < offset + magic.Length) return false;
            for (int i = 0; i < magic.Length; i++) if (data[offset + i] != (byte)magic[i]) return false;
            return true;
        }
        private static bool Dimensions(byte[] d, out long width, out long height)
        {
            width = 0;
            height = 0;
            if (d.Length >= 24 && At(d, 0, "\u0089PNG\r\n\u001a\n") && At(d, 12, "IHDR"))
            {
                width = Be(d, 16, 4);
                height = Be(d, 20, 4);
                return true;
            }
            if (d.Length >= 10 && (At(d, 0, "GIF87a") || At(d, 0, "GIF89a")))
            {
                width = Le(d, 6, 2);
                height = Le(d, 8, 2);
                return true;
            }
            if (d.Length >= 30 && At(d, 0, "RIFF") && At(d, 8, "WEBP")) return Webp(d, out width, out height);
            return d.Length >= 4 && d[0] == 0xFF && d[1] == 0xD8 && Jpeg(d, out width, out height);
        }

        private static bool Webp(byte[] d, out long width, out long height)
        {
            width = 0;
            height = 0;
            if (At(d, 12, "VP8 "))
            {
                width = Le(d, 26, 2) & 0x3FFF;
                height = Le(d, 28, 2) & 0x3FFF;
                return true;
            }
            if (At(d, 12, "VP8L"))
            {
                long bits = Le(d, 21, 4);
                width = (bits & 0x3FFF) + 1;
                height = ((bits >> 14) & 0x3FFF) + 1;
                return true;
            }
            if (At(d, 12, "VP8X"))
            {
                width = Le(d, 24, 3) + 1;
                height = Le(d, 27, 3) + 1;
                return true;
            }
            return false;
        }

        private static bool Jpeg(byte[] d, out long width, out long height)
        {
            width = 0;
            height = 0;
            int pos = 2;
            while (pos + 4 <= d.Length)
            {
                if (d[pos] != 0xFF) return false;
                int marker = d[pos + 1];
                if (marker == 0xFF)
                {
                    pos++;
                }
                else if (Frame(marker))
                {
                    if (pos + 9 > d.Length) return false;
                    height = Be(d, pos + 5, 2);
                    width = Be(d, pos + 7, 2);
                    return true;
                }
                else if (marker == 0xD9 || marker == 0xDA)
                {
                    return false;
                }
                else if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD8))
                {
                    pos += 2;
                }
                else
                {
                    pos += 2 + (int)Be(d, pos + 2, 2);
                }
            }
            return false;
        }

        private static bool Frame(int marker)
        {
            return marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
        }

        private static long Be(byte[] d, int at, int count)
        {
            long value = 0;
            for (int i = 0; i < count; i++) value = (value << 8) | d[at + i];
            return value;
        }

        private static long Le(byte[] d, int at, int count)
        {
            long value = 0;
            for (int i = count - 1; i >= 0; i--) value = (value << 8) | d[at + i];
            return value;
        }
    }
}
