using System;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ObenseuerOS
{
    internal sealed class WikiReply
    {
        internal long Status;
        internal string Error;
        internal bool TooLarge;
        internal string RetryAfter;
        internal string ContentType;
        internal string Mitigated;
        internal byte[] Body = new byte[0];
        internal string Kind;
    }

    internal static class WikiResponse
    {
        internal const string Ok = "ok";
        internal const string NotFound = "not_found";
        internal const string RateLimited = "rate_limited";
        internal const string Maxlag = "maxlag";
        internal const string Challenge = "challenge";
        internal const string BadJson = "bad_json";
        internal const string ApiError = "api_error";
        internal const string HttpError = "http_error";
        internal const string TooLarge = "too_large";
        internal const string Network = "network";
        internal const string Timeout = "timeout";
        internal const string Blocked = "blocked";
        internal const string Dropped = "dropped";
        internal const int MaxSide = 4096;
        private const int DayBackoff = 24 * 60 * 60;
        private const int ErrorBackoff = 60;
        private const int MaxlagDefault = 60;
        private const int MaxlagMin = 5;
        private const int MaxlagMax = 3600;
        private const int SnippetChars = 160;
        private const int PrefixBytes = 512;
        private const string SpanOpen = "<span class=\"searchmatch\">";
        private const string SpanClose = "</span>";
        private static readonly Regex Tag = new Regex("<span class=\"searchmatch\">|</span>|<[^>]*>", RegexOptions.CultureInvariant);
        private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.CultureInvariant);
        private static readonly Regex ErrorPattern = new Regex(@"^\s*\{\s*""error""\s*:\s*\{\s*""code""\s*:\s*""([^""]{1,64})""", RegexOptions.CultureInvariant);

        internal static string Classify(WikiReply reply, bool json)
        {
            reply.Kind = KindOf(reply, json);
            return reply.Kind;
        }

        internal static int BackoffSeconds(string kind, string retryAfter)
        {
            if (kind == RateLimited || kind == Challenge) return DayBackoff;
            if (kind == Maxlag) return Math.Min(MaxlagMax, Math.Max(MaxlagMin, Number(retryAfter, MaxlagDefault)));
            if (kind == BadJson || kind == ApiError || kind == HttpError) return ErrorBackoff;
            return 0;
        }

        internal static bool ImageFits(byte[] data)
        {
            return WikiImage.Fits(data, MaxSide);
        }

        internal static string Snippet(string html)
        {
            string text = Spaces.Replace(WebUtility.HtmlDecode(Tag.Replace(html ?? "", Mark)), " ").Trim();
            if (text.Length > SnippetChars) text = text.Substring(0, SnippetChars);
            if (text.Length > 0 && char.IsHighSurrogate(text[text.Length - 1])) text = text.Substring(0, text.Length - 1);
            return text;
        }

        private static string KindOf(WikiReply reply, bool json)
        {
            if (reply.TooLarge) return TooLarge;
            if (reply.Status == 0) return reply.Error != null && reply.Error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 ? Timeout : Network;
            if (reply.Status == 429) return RateLimited;
            if ((reply.Status == 403 || reply.Status == 503) && (reply.Mitigated != null || Html(reply))) return Challenge;
            if (json)
            {
                string code = ErrorCodeOf(reply.Body);
                if (code == "maxlag") return Maxlag;
                if (code == "missingtitle" || code == "invalidtitle") return NotFound;
                if (code != null) return ApiError;
            }
            if (reply.Status == 404) return NotFound;
            if (reply.Status != 200) return HttpError;
            if (reply.Error != null) return Network;
            if (!json) return Ok;
            return Shaped(reply.Body) ? Ok : BadJson;
        }

        private static bool Html(WikiReply reply)
        {
            return reply.ContentType != null && reply.ContentType.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ErrorCodeOf(byte[] body)
        {
            Match match = ErrorPattern.Match(Encoding.UTF8.GetString(body, 0, Math.Min(body.Length, PrefixBytes)));
            return match.Success ? match.Groups[1].Value : null;
        }

        private static bool Shaped(byte[] body)
        {
            int first = 0;
            while (first < body.Length && Space(body[first])) first++;
            int last = body.Length - 1;
            while (last > first && Space(body[last])) last--;
            return last > first && body[first] == '{' && body[last] == '}';
        }

        private static bool Space(byte b)
        {
            return b == ' ' || b == '\t' || b == '\r' || b == '\n';
        }

        private static int Number(string text, int fallback)
        {
            int value;
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static string Mark(Match match)
        {
            return match.Value == SpanClose ? "\u0002" : match.Value == SpanOpen ? "\u0001" : "";
        }

    }
}
