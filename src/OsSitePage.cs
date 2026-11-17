using System;

namespace ObenseuerOS
{
    public sealed class OsSitePage
    {
        public Func<bool> CanClose;
        public Action<bool> OnClosed;
        private Func<string, bool> navigate;
        private bool claimed;

        public bool Navigate(string address) { return navigate != null && navigate(address); }
        internal bool TryClaim() { if (claimed) return false; claimed = true; return true; }
        internal void Bind(Func<string, bool> action) { navigate = action; }

        internal static string Resolve(string current, string address)
        {
            if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(address) || address.Length > 300) return null;
            foreach (char ch in address) if (char.IsWhiteSpace(ch) || char.IsControl(ch) || ch == '\\') return null;
            if (address.StartsWith("//", StringComparison.Ordinal)) return null;
            int at = current.IndexOfAny(new[] { '/', '?', '#' });
            string host = at < 0 ? current : current.Substring(0, at);
            if (address[0] == '/') return host + address;
            if (address[0] == '?' || address[0] == '#')
            {
                int end = address[0] == '?' ? current.IndexOfAny(new[] { '?', '#' }) : current.IndexOf('#');
                return (end < 0 ? current : current.Substring(0, end)) + address;
            }
            if (address.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) address = address.Substring(8);
            else if (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) address = address.Substring(7);
            int endHost = address.IndexOfAny(new[] { '/', '?', '#' });
            string targetHost = endHost < 0 ? address : address.Substring(0, endHost);
            if (targetHost.Length == 0 || targetHost.IndexOf('.') < 0 || targetHost[0] == '.' || targetHost[targetHost.Length - 1] == '.') return null;
            foreach (char ch in targetHost)
                if (!((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '.' || ch == '-')) return null;
            return address.Length <= 300 ? address : null;
        }
    }
}
