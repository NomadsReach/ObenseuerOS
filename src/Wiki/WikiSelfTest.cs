using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ObenseuerOS
{
    internal static class WikiSelfTest
    {
        private const string CssUrl = "https://stalburg.net/load.php?lang=en&modules=site.styles&only=styles&skin=vector";
        private const string Sample = @"<!DOCTYPE html><html class=""client-nojs""><head><meta charset=""UTF-8""><meta property=""og:image"" content=""https://evil.example/o.png""><title>Sample</title><link rel=""stylesheet"" href=""/load.php?lang=en&amp;modules=site.styles&amp;only=styles&amp;skin=vector""><link rel=""icon"" href=""/resources/assets/favicon.ico""><script>var s = ""</scr"" + ""ipt>"";</script><script async src=""https://evil.example/x.js""></script></head><body onload=""alert(1)""><!-- <script>alert(2)</script> --><p><a href=""/Stalburg_Bank"">bank</a> <A HREF='/Missing_Page#Top'>missing</A> <a href=""/index.php?title=X&amp;action=edit&amp;redlink=1"">red</a> <a href=""/File:X.png"">file</a> <a href=""javascript:print();"">print</a> <a href=""https://example.com/"">ext</a> <a href=""#sec"">anchor</a></p><img src=""/images/a/ab/x.png"" alt=""x"" srcset=""/images/a/ab/x.png 1.5x, https://evil.example/y.png 2x"" onerror=""alert(3)""><img src=""https://licensebuttons.net/88x31.png"" alt=""cc""><p style=""background:url(//upload.wikimedia.org/a.png) red"">styled</p><iframe src=""https://evil.example/""></iframe><base href=""https://evil.example/""></body></html>";
        private static readonly byte[] SamplePng = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        private static readonly string Html = "<p>\"quoted\" \\ " + Ch(0x6F22) + "</p>";

        internal static string[] Run()
        {
            List<string> lines = new List<string>();
            Titles(lines);
            Paths(lines);
            Urls(lines);
            Requests(lines);
            Replies(lines);
            Pictures(lines);
            Snippets(lines);
            Robots(lines);
            Cache(lines);
            return lines.ToArray();
        }

        internal static string Failure()
        {
            foreach (string line in Run())
            {
                if (line.StartsWith("FAIL", StringComparison.Ordinal)) return line;
            }
            return null;
        }

        private static void Check(List<string> lines, string name, bool ok)
        {
            lines.Add((ok ? "ok    " : "FAIL  ") + name);
        }

        private static string Ch(int code)
        {
            return ((char)code).ToString();
        }

        private static string Pair()
        {
            return Ch(0xD83D) + Ch(0xDE00);
        }

        private static bool Accepts(string raw, string want)
        {
            string title;
            return WikiTitle.TryNormalise(raw, out title) && title == want;
        }

        private static bool Valid(string raw)
        {
            string title;
            return WikiTitle.TryNormalise(raw, out title);
        }

        private static bool Rejects(string raw)
        {
            return !Valid(raw);
        }

        private static string Repeat(string part, int times)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < times; i++) sb.Append(part);
            return sb.ToString();
        }

        private static void Titles(List<string> lines)
        {
            Check(lines, "title: spaces, underscores, first letter and namespace case", Accepts("  main  page ", "Main_page") && Accepts("stalburg_bank", "Stalburg_bank") && Accepts("file:brass bar.png", "File:Brass_bar.png") && Accepts("category:Items", "Category:Items"));
            Check(lines, "title: colons in main-namespace titles are accepted", Accepts("INFRA: Moonshine", "INFRA:_Moonshine") && Accepts("Operation: Will of Steel", "Operation:_Will_of_Steel") && Accepts("Obenseuer/Modding:Making Lavender Mods", "Obenseuer/Modding:Making_Lavender_Mods") && Accepts("Stolland: x", "Stolland:_x"));
            Check(lines, "title: Special, Media, MediaWiki, User, Talk, Template, Module and Help rejected", Rejects("Special:Random") && Rejects("sPeCiAl_:Search") && Rejects("Media:x") && Rejects("MediaWiki:Common.js") && Rejects("User:Ricky") && Rejects("Talk:Main_Page") && Rejects("Template:Infobox") && Rejects("Module:X") && Rejects("Help:Contents"));
            Check(lines, "title: any '<namespace> talk' prefix rejected", Rejects("user talk:X") && Rejects("Template_talk:X") && Rejects("File talk:X"));
            Check(lines, "title: a leading colon is refused", Rejects(":Special:Random") && Rejects(" :User:X"));
            Check(lines, "title: File and Category need a name and one colon", Rejects("File:") && Rejects("Category:") && Rejects("Category:A:B") && Rejects("File:A:B"));
            Check(lines, "title: empty and blank rejected", Rejects("") && Rejects(" _ ") && Rejects(null));
            Check(lines, "title: | # < > [ ] { } rejected", Rejects("a|b") && Rejects("a#b") && Rejects("a<b") && Rejects("a>b") && Rejects("a[b") && Rejects("a]b") && Rejects("a{b") && Rejects("a}b"));
            Check(lines, "title: control characters rejected", Rejects("a\tb") && Rejects("a\nb") && Rejects("a" + Ch(7) + "b") && Rejects("a" + Ch(0x85) + "b"));
            Check(lines, "title: no-break space is a space, bidi controls rejected", Accepts("Main" + Ch(0xA0) + "Page", "Main_Page") && Rejects("a" + Ch(0x200E) + "b") && Rejects("a" + Ch(0x202E) + "b") && Rejects("a" + Ch(0x2066) + "b"));
            Check(lines, "title: Unicode spaces count as spaces, so the namespace denylist still applies", Accepts("Main" + Ch(0x2003) + "Page", "Main_Page") && Rejects("User" + Ch(0x2003) + "talk:Foo") && Rejects("Special" + Ch(0x3000) + ":X") && Rejects("User" + Ch(0x2003) + ":X"));
            Check(lines, "title: dot segments and empty path segments rejected, dots inside a name allowed", Rejects("..") && Rejects("a/../b") && Rejects("a/./b") && Rejects("/Main_Page") && Rejects("a//b") && Rejects("a/") && Accepts("Pros..Cons", "Pros..Cons"));
            Check(lines, "title: 255 UTF-8 byte limit", Valid(Repeat("a", 255)) && Rejects(Repeat("a", 256)) && Valid(Repeat(Ch(0x6F22), 85)) && Rejects(Repeat(Ch(0x6F22), 86)));
            Check(lines, "title: lone surrogate rejected", Rejects("a" + Ch(0xD83D) + "b") && Rejects("a" + Ch(0xDE00)));
        }

        private static void Paths(List<string> lines)
        {
            Check(lines, "image path: original and thumbnail forms accepted", WikiTitle.IsImagePath("/images/a/ab/Brass_Bar.png") && WikiTitle.IsImagePath("/images/thumb/a/ab/Brass_Bar.png/480px-Brass_Bar.png"));
            string[] bad = { "/images/a/ab/x.png?x=1", "/images/a/ab/x.png#f", "//images/a/ab/x.png", "https://evil.example/images/a/ab/x.png", "/images/a/ab/../x.png", "/images/a/ab/..", "/images/a/ab/.", "/images/a/ab/x.png/extra", "/images/ab/x.png", "/images/a/abc/x.png", "/images/A/AB/x.png", "/images/a/ab/", "/images/a/ab/x" + Ch(1) + ".png", "/images/a/ab/x\\y.png", "/images/a/ab/x.png\n", "/images/a/ab/x" + Ch(0xD83D) + ".png", "/Special:Search", "" };
            bool allRejected = true;
            foreach (string path in bad)
            {
                if (WikiTitle.IsImagePath(path)) allRejected = false;
            }
            Check(lines, "image path: query, fragment, host, dot segments, depth, hex case, control rejected", allRejected);
            Check(lines, "image path: %2e, %2f and %5c escapes refused, other escapes kept", !WikiTitle.IsImagePath("/images/a/ab/%2e%2e") && !WikiTitle.IsImagePath("/images/a/ab/..%2Fx.png") && !WikiTitle.IsImagePath("/images/a/ab/x%5Cy.png") && !WikiTitle.IsImagePath("/images/a/ab/x%2ey.png") && WikiTitle.IsImagePath("/images/a/ab/x%20y.png"));
            Check(lines, "image path: paths over 300 characters refused", !WikiTitle.IsImagePath("/images/a/ab/" + Repeat("x", 300) + ".png") && WikiTitle.IsImagePath("/images/a/ab/" + Repeat("x", 200) + ".png"));
            Check(lines, "image path: skin rasters under /resources/assets accepted, svg, other folders, dot segments, queries and lookalike hosts refused", WikiTitle.IsImagePath("/resources/assets/wikilogo.png") && WikiTitle.Image("/resources/assets/wikilogo.png") == "https://stalburg.net/resources/assets/wikilogo.png" && WikiTitle.Allowed("https://stalburg.net/resources/assets/wikilogo.png") && !WikiTitle.IsImagePath("/resources/assets/x.svg") && !WikiTitle.IsImagePath("/resources/x.png") && !WikiTitle.IsImagePath("/resources/assets/../x.png") && !WikiTitle.IsImagePath("/resources/assets/x.png?e923f") && !WikiTitle.IsImagePath("//resources/assets/x.png") && !WikiTitle.Allowed("https://stalburg.net.evil.example/resources/assets/wikilogo.png"));
            Check(lines, "image path: builder returns stalburg.net URL only for valid paths",WikiTitle.Image("/images/a/ab/x.png") == "https://stalburg.net/images/a/ab/x.png" && WikiTitle.Image("https://evil.example/images/a/ab/x.png") == null && WikiTitle.Image("/images/a/ab/x.png?x") == null);
            Check(lines, "image key: origin and query are stripped, other hosts and paths give null", WikiHtml.ImageKey("https://stalburg.net/images/a/ab/x.png?123") == "/images/a/ab/x.png" && WikiHtml.ImageKey("https://evil.example/images/a/ab/x.png") == null && WikiHtml.ImageKey("//upload.wikimedia.org/a.png") == null);
        }

        private static void Urls(List<string> lines)
        {
            Check(lines, "url: article URLs are escaped per segment and invalid titles give null", WikiTitle.Article("Main Page") == "https://stalburg.net/Main_Page" && WikiTitle.Article("A&B=c") == "https://stalburg.net/A%26B%3Dc" && WikiTitle.Article("Obenseuer/Modding:Making Lavender Mods") == "https://stalburg.net/Obenseuer/Modding%3AMaking_Lavender_Mods" && WikiTitle.Article("Special:Search") == null);
            Check(lines, "url: search clamps the limit and needs a query", WikiTitle.Search("bank x", 20, 50).Contains("srsearch=bank%20x&srnamespace=0&srlimit=10&sroffset=20&") && WikiTitle.Search("bank", -3, 0).Contains("srlimit=1&sroffset=0&") && WikiTitle.Search("  ", 0, 10) == null);
            Check(lines, "url: title index request is exact", WikiTitle.TitleIndex(null) == "https://stalburg.net/api.php?action=query&list=allpages&apnamespace=0&apfilterredir=nonredirects&aplimit=500&format=json&formatversion=2&maxlag=5");
            Check(lines, "url: over-long input and lone surrogates give null, never throw", WikiTitle.TitleIndex("a" + Ch(0xD83D)) == null && WikiTitle.TitleIndex(Repeat("a", 301)) == null && WikiTitle.Search(Repeat("a", 301), 0, 10) == null && WikiTitle.Article(Repeat("a", 301)) == null);
            string[] urls = { WikiTitle.Article("Main Page"), WikiTitle.Search("bank", 0, 10), WikiTitle.TitleIndex("A"), WikiTitle.Image("/images/a/ab/x.png") };
            bool hosts = true;
            foreach (string url in urls)
            {
                Uri uri = new Uri(url);
                if (uri.Host != "stalburg.net" || uri.Scheme != "https") hosts = false;
            }
            Check(lines, "url: every builder targets https stalburg.net only", hosts);
        }

        private static void Requests(List<string> lines)
        {
            Check(lines, "request: article, robots, title index, search, load.php and image URLs pass the re-check", WikiTitle.Allowed(WikiTitle.Article("Main Page")) && WikiTitle.Allowed(WikiTitle.Search("bank", 0, 10)) && WikiTitle.Allowed(WikiTitle.TitleIndex(null)) && WikiTitle.Allowed(WikiTitle.Image("/images/a/ab/x.png")) && WikiTitle.Allowed(WikiTitle.Image("/images/thumb/a/ab/x.png/480px-x.png")) && WikiTitle.Allowed("https://stalburg.net/robots.txt") && WikiTitle.Allowed(CssUrl));
            Check(lines, "request: article paths must be canonical: escaped, no query, fragment or empty segment", WikiTitle.Allowed("https://stalburg.net/Stalburg_Bank") && WikiTitle.Allowed("https://stalburg.net/Obenseuer/Modding%3AMaking_Lavender_Mods") && !WikiTitle.Allowed("https://stalburg.net/Stalburg%5FBank") && !WikiTitle.Allowed("https://stalburg.net/Main Page") && !WikiTitle.Allowed("https://stalburg.net/Main_Page?x=1") && !WikiTitle.Allowed("https://stalburg.net/Main_Page#x") && !WikiTitle.Allowed("https://stalburg.net//Main_Page") && !WikiTitle.Allowed("https://stalburg.net/Main_Page/") && !WikiTitle.Allowed("https://stalburg.net/Main%2FPage"));
            Check(lines, "request: Special, Talk and User paths are refused as articles", !WikiTitle.Allowed("https://stalburg.net/Special:Random") && !WikiTitle.Allowed("https://stalburg.net/Talk:Main_Page") && !WikiTitle.Allowed("https://stalburg.net/User:Ricky"));
            Check(lines, "request: load.php only as a plain query, index.php and other scripts refused", !WikiTitle.Allowed("https://stalburg.net/load.php?x=<script>") && !WikiTitle.Allowed("https://stalburg.net/load.php?x=a b") && !WikiTitle.Allowed("https://stalburg.net/load.php?x=1#f") && !WikiTitle.Allowed("https://stalburg.net/load.php") && !WikiTitle.Allowed("https://stalburg.net/index.php?title=Main_Page") && !WikiTitle.Allowed("https://stalburg.net/load.php/../x?y=1"));
            Check(lines, "request: other schemes, hosts, ports, user info, case and lookalike hosts are refused", !WikiTitle.Allowed("http://stalburg.net/api.php?x=1") && !WikiTitle.Allowed("https://evil.example/api.php?x=1") && !WikiTitle.Allowed("https://stalburg.net.evil.example/api.php?x=1") && !WikiTitle.Allowed("https://stalburg.net@evil.example/api.php?x=1") && !WikiTitle.Allowed("https://stalburg.net:8443/api.php?x=1") && !WikiTitle.Allowed("https://STALBURG.net/api.php?x=1") && !WikiTitle.Allowed("https://stalburg.netx/Main_Page") && !WikiTitle.Allowed("//stalburg.net/api.php?x=1") && !WikiTitle.Allowed("https:/stalburg.net/api.php?x=1"));
            Check(lines, "request: api.php needs a query, images need the exact pattern, spaces, controls, backslashes, non-ASCII, null and empty are refused", !WikiTitle.Allowed("https://stalburg.net/api.php") && !WikiTitle.Allowed("https://stalburg.net/api.php?x=1#frag") && !WikiTitle.Allowed("https://stalburg.net/images/a/ab/x.png?x=1") && !WikiTitle.Allowed("https://stalburg.net/images/a/ab/../x.png") && !WikiTitle.Allowed("https://stalburg.net/api.php?x=a b") && !WikiTitle.Allowed("https://stalburg.net/api.php?x=" + Ch(1)) && !WikiTitle.Allowed("https://stalburg.net/api.php?x=a\\b") && !WikiTitle.Allowed("https://stalburg.net/api.php?x=" + Ch(0x6F22)) && !WikiTitle.Allowed(null) && !WikiTitle.Allowed(""));
        }

        private static void Replies(List<string> lines)
        {
            Check(lines, "reply: a parse object and a PNG are ok, and page text that looks like an error is still ok", Kind(200, "application/json", "{\"query\":{\"allpages\":[]}}", true) == WikiResponse.Ok && Kind(200, "image/png", "\u0089PNG", false) == WikiResponse.Ok && Kind(200, "application/json", "{\"parse\":{\"text\":\"{\\\"error\\\":{\\\"code\\\":\\\"maxlag\\\"}}\"}}", true) == WikiResponse.Ok);
            Check(lines, "reply: maxlag, missing title and other API errors are classified by their code", WikiResponse.Classify(new WikiReply { Status = 200, RetryAfter = "12", Body = Encoding.UTF8.GetBytes("{\"error\":{\"code\":\"maxlag\",\"info\":\"Waiting\"}}") }, true) == WikiResponse.Maxlag && Kind(200, "application/json", "{\"error\":{\"code\":\"missingtitle\"}}", true) == WikiResponse.NotFound && Kind(200, "application/json", "{\"error\":{\"code\":\"readonly\"}}", true) == WikiResponse.ApiError);
            Check(lines, "reply: 429, a Cloudflare challenge page or header and a 404 map to their kinds", Kind(429, "text/html", "<html>slow down</html>", true) == WikiResponse.RateLimited && Kind(503, "text/html; charset=UTF-8", "<html>Just a moment</html>", true) == WikiResponse.Challenge && WikiResponse.Classify(new WikiReply { Status = 403, ContentType = "application/json", Mitigated = "challenge", Body = Encoding.UTF8.GetBytes("{}") }, true) == WikiResponse.Challenge && Kind(404, "image/png", "", false) == WikiResponse.NotFound && Kind(403, "application/json", "{}", true) == WikiResponse.HttpError);
            Check(lines, "reply: HTML, truncated JSON and a JSON array are bad_json, other statuses are http_error", Kind(200, "text/html", "<!DOCTYPE html><html>", true) == WikiResponse.BadJson && Kind(200, "application/json", "{\"parse\":{\"title\":\"Main", true) == WikiResponse.BadJson && Kind(200, "application/json", "[1,2]", true) == WikiResponse.BadJson && Kind(500, "text/html", "oops", true) == WikiResponse.HttpError && Kind(301, "", "", false) == WikiResponse.HttpError);
            Check(lines, "reply: no response is a timeout or a network failure, a transfer error after headers is network, an oversize body is too_large", WikiResponse.Classify(new WikiReply { Status = 0, Error = "Request timeout" }, true) == WikiResponse.Timeout && WikiResponse.Classify(new WikiReply { Status = 0, Error = "Cannot resolve destination host" }, true) == WikiResponse.Network && WikiResponse.Classify(new WikiReply { Status = 0 }, false) == WikiResponse.Network && WikiResponse.Classify(new WikiReply { Status = 200, Error = "Data processing failed" }, false) == WikiResponse.Network && WikiResponse.Classify(new WikiReply { Status = 200, TooLarge = true, Body = Encoding.UTF8.GetBytes("{\"parse\":{}}") }, true) == WikiResponse.TooLarge);
            Check(lines, "reply: backoff is 24 h for 429 and challenge, Retry-After for maxlag clamped to 5 to 3600 s, 60 s for bad JSON, API and HTTP errors, none for the rest", WikiResponse.BackoffSeconds(WikiResponse.RateLimited, null) == 86400 && WikiResponse.BackoffSeconds(WikiResponse.Challenge, null) == 86400 && WikiResponse.BackoffSeconds(WikiResponse.Maxlag, "12") == 12 && WikiResponse.BackoffSeconds(WikiResponse.Maxlag, null) == 60 && WikiResponse.BackoffSeconds(WikiResponse.Maxlag, "1") == 5 && WikiResponse.BackoffSeconds(WikiResponse.Maxlag, "99999") == 3600 && WikiResponse.BackoffSeconds(WikiResponse.Maxlag, "soon") == 60 && WikiResponse.BackoffSeconds(WikiResponse.BadJson, null) == 60 && WikiResponse.BackoffSeconds(WikiResponse.ApiError, null) == 60 && WikiResponse.BackoffSeconds(WikiResponse.HttpError, null) == 60 && WikiResponse.BackoffSeconds(WikiResponse.Ok, null) == 0 && WikiResponse.BackoffSeconds(WikiResponse.NotFound, null) == 0 && WikiResponse.BackoffSeconds(WikiResponse.Network, null) == 0 && WikiResponse.BackoffSeconds(WikiResponse.Timeout, null) == 0 && WikiResponse.BackoffSeconds(WikiResponse.TooLarge, null) == 0);
        }

        private static void Pictures(List<string> lines)
        {
            Check(lines, "picture: PNG, GIF, JPEG and WebP up to 4096 pixels a side fit, while larger, empty, truncated and unknown data do not", WikiResponse.ImageFits(Png(4000, 3000)) && WikiResponse.ImageFits(Gif(320, 200)) && WikiResponse.ImageFits(Jpeg(200, 100)) && WikiResponse.ImageFits(WebpExtended(3000, 2000)) && WikiResponse.ImageFits(WebpLossless(400, 300)) && WikiResponse.ImageFits(WebpLossy(640, 480)) && !WikiResponse.ImageFits(Png(4097, 10)) && !WikiResponse.ImageFits(Png(0, 10)) && !WikiResponse.ImageFits(Gif(5000, 200)) && !WikiResponse.ImageFits(Jpeg(5000, 100)) && !WikiResponse.ImageFits(WebpExtended(5000, 2000)) && !WikiResponse.ImageFits(Head(Png(10, 10), 20)) && !WikiResponse.ImageFits(Bytes("<svg/>")) && !WikiResponse.ImageFits(Bytes("RIFF0000WAVEfmt ")) && !WikiResponse.ImageFits(null));
        }

        private static void Snippets(List<string> lines)
        {
            Check(lines, "snippet: search marks become U+0001 and U+0002, other tags go and entities decode", WikiResponse.Snippet("<span class=\"searchmatch\">Bank</span> &amp; notes") == Ch(1) + "Bank" + Ch(2) + " & notes" && WikiResponse.Snippet("Foo <b>bar</b>\n\n  baz") == "Foo bar baz" && WikiResponse.Snippet(null) == "");
            Check(lines, "snippet: at most 160 characters and never ending in half a surrogate pair", WikiResponse.Snippet(Repeat("a", 300)).Length == 160 && WikiResponse.Snippet(Repeat("a", 159) + Pair()).Length == 159);
            Check(lines, "cost: escaped quote and backslash cost 2, control 6, BMP 2 and 3, valid pair 4, lone surrogate 6", WikiChunk.Cost("\"") == 2 && WikiChunk.Cost("\\") == 2 && WikiChunk.Cost("\n") == 6 && WikiChunk.Cost(Ch(0xE9)) == 2 && WikiChunk.Cost(Ch(0x6F22)) == 3 && WikiChunk.Cost(Pair()) == 4 && WikiChunk.Cost(Ch(0xD83D)) == 6 && WikiChunk.Cost(Ch(0x2028)) == 6);
        }

        private static void Robots(List<string> lines)
        {
            WikiRobots.Set("User-agent: *\nDisallow: /Special:\nDisallow: /Special%3A # comment\n\nUser-agent: Other\nDisallow: /Private\n", 1000);
            Check(lines, "robots: only the * group applies and # comments are ignored", WikiRobots.Blocked("/Special:Random") && WikiRobots.Blocked("/Special%3ARandom") && !WikiRobots.Blocked("/Private") && !WikiRobots.Blocked("/Main_Page"));
            WikiRobots.Set("User-agent: Other\nUser-agent: *\nDisallow: /Help\n", 1000);
            Check(lines, "robots: consecutive User-agent lines share one group", WikiRobots.Blocked("/Help") && !WikiRobots.Blocked("/Main_Page"));
            WikiRobots.Set("User-agent: *\nDisallow:\n", 1000);
            Check(lines, "robots: an empty Disallow blocks nothing", !WikiRobots.Blocked("/Special:Random"));
            WikiRobots.Set("User-agent: *\nDisallow: /*.json$\nDisallow: /*action=edit\nDisallow: /Wiki/\nAllow: /Wiki/Public\nDisallow: /a\nAllow: /a\n", 1000);
            Check(lines, "robots: * matches any run and $ anchors the end", WikiRobots.Blocked("/Main.json") && !WikiRobots.Blocked("/Main.json?x=1") && WikiRobots.Blocked("/Main_Page?action=edit&x=1"));
            Check(lines, "robots: the longest matching rule wins, and Allow wins a tie", !WikiRobots.Blocked("/Wiki/Public_page") && WikiRobots.Blocked("/Wiki/Private") && !WikiRobots.Blocked("/a"));
            WikiRobots.Set("User-agent: *\nCrawl-delay: 5\n", 1000);
            int five = WikiRobots.DelayMs;
            WikiRobots.Set("User-agent: *\nCrawl-delay: 0.2\n", 1000);
            int low = WikiRobots.DelayMs;
            WikiRobots.Set("User-agent: *\nCrawl-delay: 99999\n", 1000);
            int capped = WikiRobots.DelayMs;
            WikiRobots.Set("User-agent: Other\nCrawl-delay: 9\n", 1000);
            int other = WikiRobots.DelayMs;
            Check(lines, "robots: Crawl-delay applies to the * group, with a 1 s floor and a 1 h cap", five == 5000 && low == 1000 && capped == 3600000 && other == 1000);
            WikiRobots.Set("User-agent: *\nDisallow: /api.php\n", 1000);
            Check(lines, "robots: an api.php rule refuses index and search requests, articles are not refused", WikiRobots.Refuses(WikiTitle.TitleIndex(null)) && WikiRobots.Refuses(WikiTitle.Search("bank", 0, 10)) && !WikiRobots.Refuses(WikiTitle.Article("Main Page")));
            WikiRobots.Set("User-agent: *\nDisallow: /\n", 1000);
            Check(lines, "robots: a blanket rule still lets robots.txt itself be fetched", !WikiRobots.Refuses(WikiTitle.Origin + WikiRobots.RobotsPath) && WikiRobots.Refuses(WikiTitle.Article("Main Page")));
            WikiRobots.Set("User-agent: *\nDisallow: /Special:\n", 1000);
            Check(lines, "robots: Set keeps rules by prefix, fresh for 24 h, stale after", WikiRobots.Blocked("/Special:Random") && !WikiRobots.Blocked("/Main_Page") && WikiRobots.Fresh(1000) && WikiRobots.Fresh(1000 + WikiRobots.FreshSeconds - 1) && !WikiRobots.Fresh(1000 + WikiRobots.FreshSeconds));
            WikiRobots.Set("", 1000);
            Check(lines, "robots: an empty file allows everything and waits 1 s", !WikiRobots.Blocked("/Special:Random") && WikiRobots.DelayMs == 1000);
            WikiRobots.Set("\uFEFFUser-agent: *\nDisallow: /Special:\n", 1000);
            Check(lines, "robots: a leading byte order mark is ignored", WikiRobots.Blocked("/Special:Random"));
        }

        private static void Cache(List<string> lines)
        {
            string root = Path.Combine(Path.GetTempPath(), "wiki-selftest-" + Guid.NewGuid().ToString("N"));
            try
            {
                CacheChecks(lines, root);
                SchemaChecks(lines, root);
                TitleIndexChecks(lines, root);
                SiteChecks(lines, root);
                HardeningChecks(lines, root);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void CacheChecks(List<string> lines, string root)
        {
            string dir = Path.Combine(root, "main");
            WikiCache cache = new WikiCache(dir, 1000000);
            Check(lines, "cache: SHA-1 hash matches the known vector", WikiCache.Hash("abc") == "a9993e364706816aba3e25717850c26c9cd0d89d");
            bool saved = cache.SavePage("Main_Page", 7, 1000, Html, 1000);
            WikiPage got = cache.LoadPage("Main_Page", 1001);
            Check(lines, "cache: page round trip keeps title, revision, fetched time and the page file", saved && got != null && got.Title == "Main_Page" && got.RevId == 7 && got.Fetched == 1000 && got.File == "pages/" + WikiCache.Hash("Main_Page") + ".html");
            string page = Path.Combine(dir, "pages", WikiCache.Hash("Main_Page") + ".html");
            Check(lines, "cache: the page file is one metadata line, then the HTML verbatim", File.ReadAllText(page, Encoding.UTF8) == "<!--OBXWIKI4 title=Main_Page rev=7 fetched=1000 -->\n" + Html);
            Check(lines, "cache: the not-saved page is written at start and is not counted as a page", File.ReadAllText(Path.Combine(dir, WikiSite.MissingName), Encoding.UTF8) == WikiSite.MissingHtml && cache.PageCount() == 1);
            Check(lines, "cache: unknown title is a miss, PageFile reports saved pages only", cache.LoadPage("Other", 1001) == null && cache.PageFile("Other") == null && cache.PageFile("Main_Page") != null);
            UTF8Encoding plain = new UTF8Encoding(false);
            string[] bad = { "<!--OBXWIKI1 title=Main_Page rev=7 fetched=1000 -->\n" + Html, "<!--OBXWIKI2 title=Main_Page rev=7 fetched=1000 -->\n" + Html, "<!--OBXWIKI3 title=Main_Page rev=7 fetched=1000 -->\n" + Html, "<!--OBXWIKI4 title=Main_Page rev=x fetched=1000 -->\n" + Html, "<!--OBXWIKI4 title=Main_Page rev=7 -->\n" + Html, "<!--OBXWIKI4 title=Other rev=7 fetched=1000 -->\n" + Html, Html };
            bool misses = true;
            foreach (string text in bad)
            {
                File.WriteAllText(page, text, plain);
                if (cache.LoadPage("Main_Page", 1003) != null || File.Exists(page)) misses = false;
            }
            Check(lines, "cache: wrong-schema, malformed, wrong-title and metadata-less page files are misses and get deleted", misses);
            string temp = page + ".0123456789abcdef0123456789abcdef.tmp";
            cache.SavePage("Main_Page", 7, 1000, Html, 1000);
            File.WriteAllText(temp, "<!--OBXWIKI2 title=Main_Page rev=9 fetched=9 -->\ntorn", plain);
            WikiPage kept = cache.LoadPage("Main_Page", 1002);
            Check(lines, "cache: a leftover temp file with the real name pattern leaves the saved page intact", kept != null && kept.RevId == 7);
            WikiCache locked = new WikiCache(Path.Combine(root, "locked"), 1000000);
            locked.SavePage("Main_Page", 7, 1000, Html, 1000);
            bool failed;
            using (FileStream hold = new FileStream(Path.Combine(root, "locked", "pages", WikiCache.Hash("Main_Page") + ".html"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                failed = !locked.SavePage("Main_Page", 8, 2000, "new", 2000);
            }
            WikiPage held = locked.LoadPage("Main_Page", 2001);
            Check(lines, "cache: a locked target fails the save without throwing, keeps the old page and leaves no temp file", failed && held != null && held.RevId == 7 && Directory.GetFiles(Path.Combine(root, "locked", "pages"), "*.tmp").Length == 0);
            Check(lines, "cache: fresh within 24 h, stale at 24 h, and a fetched time in the future is stale", WikiCache.IsFresh(1000, 1000 + 86399) && !WikiCache.IsFresh(1000, 1000 + 86400) && !WikiCache.IsFresh(2000, 1000));
            WikiCache strict = new WikiCache(Path.Combine(root, "strict"), 1000000);
            Check(lines, "cache: negative rev or fetched and HTML over 9 MB are refused", !strict.SavePage("Main_Page", -1, 1000, Html, 1000) && !strict.SavePage("Main_Page", 7, -1, Html, 1000) && !strict.SavePage("Main_Page", 7, 1000, Repeat("x", 9 * 1024 * 1024), 1000) && strict.LoadPage("Main_Page", 1001) == null);
            bool clockSafe;
            try
            {
                clockSafe = strict.SavePage("Clock", 1, 1, "x", long.MaxValue);
            }
            catch (Exception)
            {
                clockSafe = false;
            }
            Check(lines, "cache: a clock value that is not a date does not throw from SavePage", clockSafe);
            WikiCache lru = new WikiCache(Path.Combine(root, "lru"), 1000);
            string body = Repeat("x", 400);
            lru.SavePage("P1", 1, 1, body, 1);
            lru.SavePage("P2", 1, 2, body, 2);
            lru.LoadPage("P1", 3);
            lru.SavePage("P3", 1, 4, body, 4);
            Check(lines, "cache: LRU evicts the least recently used page to the cap", lru.LoadPage("P2", 5) == null && lru.LoadPage("P1", 5) != null && lru.LoadPage("P3", 5) != null && lru.TotalBytes() <= 1000);
            WikiCache fits = new WikiCache(Path.Combine(root, "fits"), 1000);
            fits.SavePage("P1", 1, 1, body, 1);
            Check(lines, "cache: Fits reports the room left under the cap and never evicts", fits.Fits(100) && !fits.Fits(1000) && fits.LoadPage("P1", 2) != null);
            WikiCache tie = new WikiCache(Path.Combine(root, "tie"), 500);
            tie.SaveCss(CssUrl, Repeat("p{color:red}", 20), 5);
            tie.SavePage("P1", 1, 5, body, 5);
            Check(lines, "cache: a stylesheet saved in the same second as a page is not evicted by that page", tie.CssFile(CssUrl, 6) != null && tie.LoadPage("P1", 6) != null);
            WikiCache totals = new WikiCache(Path.Combine(root, "totals"), 1000000);
            totals.SavePage("P1", 1, 1, body, 1);
            totals.SavePage("P1", 2, 2, body, 2);
            totals.SaveCss(CssUrl, "p{color:red}", 2);
            Check(lines, "cache: the running total matches the files on disk after a replace and a stylesheet", totals.TotalBytes() > 0 && totals.TotalBytes() == DiskBytes(Path.Combine(root, "totals")));
            totals.Clear();
            Check(lines, "cache: the running total is zero after Clear", totals.TotalBytes() == 0);
            WikiCache keep = new WikiCache(Path.Combine(root, "keep"), 100);
            keep.SavePage("P1", 1, 1, body, 1);
            keep.SavePage("P2", 1, 2, body, 2);
            Check(lines, "cache: the page just written is never evicted, even over the cap", keep.LoadPage("P2", 3) != null && keep.LoadPage("P1", 3) == null);
            WikiCache guard = new WikiCache(Path.Combine(root, "guard"), 100);
            string notes = Path.Combine(root, "guard", "pages", "notes.txt");
            string named = Path.Combine(root, "guard", "pages", "abc.html");
            File.WriteAllText(notes, body, Encoding.ASCII);
            File.WriteAllText(named, body, Encoding.ASCII);
            guard.SavePage("P1", 1, 1, body, 1);
            guard.SavePage("P2", 1, 2, body, 2);
            Check(lines, "cache: eviction never deletes a file whose name is not a cache name, and still evicts cache names", File.Exists(notes) && File.Exists(named) && guard.LoadPage("P1", 3) == null);
            string pngRel = cache.SaveImage("/images/a/ab/x.png", SamplePng, 1);
            Check(lines, "cache: image saved under its hash and magic-byte extension", pngRel == "img/" + WikiCache.Hash("/images/a/ab/x.png") + ".png" && cache.ImageFile("/images/a/ab/x.png", 2) == pngRel);
            byte[] jpg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 };
            byte[] gif = Join(Bytes("GIF89a"), new byte[] { 1, 0 });
            byte[] webp = Join(Bytes("RIFF"), Zeros(4), Bytes("WEBP"), Bytes("VP8 "));
            byte[] wave = Join(Bytes("RIFF"), Zeros(4), Bytes("WAVEfmt "));
            byte[] svg = Bytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
            Check(lines, "cache: JPEG, GIF and WebP accepted, SVG, WAVE and bad image paths refused", cache.SaveImage("/images/a/ab/j.jpg", jpg, 1) != null && cache.SaveImage("/images/a/ab/g.gif", gif, 1) != null && cache.SaveImage("/images/a/ab/w.webp", webp, 1) != null && cache.SaveImage("/images/a/ab/y.svg", svg, 1) == null && cache.SaveImage("/images/a/ab/w.wav", wave, 1) == null && cache.SaveImage("/images/../x.png", SamplePng, 1) == null);
            string cssRel = cache.SaveCss(CssUrl, "p{color:red}", 1);
            Check(lines, "cache: a stylesheet is saved under its URL hash and found by CssFile only", cssRel == "css/" + WikiCache.Hash(CssUrl) + ".css" && cache.CssFile(CssUrl, 2) == cssRel && cache.CssFile("https://stalburg.net/load.php?x=2", 2) == null);
            cache.Clear();
            Check(lines, "cache: clear removes pages, stylesheets and images, and keeps the not-saved page", cache.LoadPage("Main_Page", 6) == null && cache.ImageFile("/images/a/ab/x.png", 6) == null && cache.CssFile(CssUrl, 6) == null && cache.TotalBytes() == 0 && File.Exists(Path.Combine(dir, WikiSite.MissingName)));
        }

        private static void SchemaChecks(List<string> lines, string root)
        {
            string dir = Path.Combine(root, "schema");
            UTF8Encoding plain = new UTF8Encoding(false);
            WikiCache first = new WikiCache(dir, 1000000);
            first.SavePage("Main_Page", 7, 1000, Html, 1000);
            first.SaveCss(CssUrl, "p{color:red}", 1000);
            File.WriteAllText(Path.Combine(dir, "schema"), "OBXWIKI3", plain);
            File.WriteAllText(Path.Combine(dir, "pages", WikiCache.Hash("Main_Page") + ".html"), "<!--OBXWIKI3 title=Main_Page rev=7 fetched=1000 -->\n" + Html, plain);
            WikiCache upgraded = new WikiCache(dir, 1000000);
            Check(lines, "schema: a cache from an older schema has its pages and stylesheets purged on the first start", upgraded.LoadPage("Main_Page", 1001) == null && upgraded.CssFile(CssUrl, 1001) == null && upgraded.PageCount() == 0);
            upgraded.SavePage("Main_Page", 8, 1002, Html, 1002);
            WikiCache again = new WikiCache(dir, 1000000);
            Check(lines, "schema: a page saved under the current schema survives the next start", again.LoadPage("Main_Page", 1003) != null);
        }

        private static void TitleIndexChecks(List<string> lines, string root)
        {
            WikiCache index = new WikiCache(Path.Combine(root, "index"), 1000000);
            long fetched;
            bool missing = index.LoadTitles(out fetched) == null;
            bool saved = index.SaveTitles(new List<string> { "Main_Page", "File:Brass_Bar.png" }, 1000);
            List<string> back = index.LoadTitles(out fetched);
            Check(lines, "index: titles round trip with their fetched time, and a missing index is null", missing && saved && back != null && back.Count == 2 && back[0] == "Main_Page" && back[1] == "File:Brass_Bar.png" && fetched == 1000);
            Check(lines, "index: a title that is not normalised refuses the save", !index.SaveTitles(new List<string> { "bad title" }, 1000));
            File.WriteAllText(Path.Combine(root, "index", "titles"), "OBXWIKI1\nfetched:5\nMain Page\n", new UTF8Encoding(false));
            Check(lines, "index: a file with an unnormalised title is a miss", index.LoadTitles(out fetched) == null);
            WikiCache counted = new WikiCache(Path.Combine(root, "counted"), 1000000);
            counted.SavePage("Main_Page", 1, 1, "x", 1);
            counted.SavePage("Other", 1, 1, "y", 1);
            File.WriteAllText(Path.Combine(root, "counted", "pages", "notes.html.txt"), "x", Encoding.ASCII);
            Check(lines, "index: page count counts page files only", counted.PageCount() == 2);
            index.SaveTitles(new List<string> { "Main_Page" }, 2);
            index.Clear();
            Check(lines, "index: clear removes the title index", index.LoadTitles(out fetched) == null);
        }

        private static void SiteChecks(List<string> lines, string root)
        {
            WikiCache cache = new WikiCache(Path.Combine(root, "site"), 1000000);
            cache.SaveCss(CssUrl, "p{color:red}", 1);
            string imagePath = "/images/a/ab/x.png";
            string imageLocal = "/img/" + WikiCache.Hash(imagePath) + ".png";
            cache.SaveImage(imagePath, SamplePng, 1);
            string bankLocal = "/pages/" + WikiCache.Hash("Stalburg_Bank") + ".html";
            cache.SavePage("Stalburg_Bank", 1, 1, "<p>x</p>", 1);
            string output = WikiHtml.Render(Sample, new WikiAssets(cache, 2));
            string cssLocal = "/css/" + WikiCache.Hash(CssUrl) + ".css";
            Check(lines, "html: the utf-8 charset meta opens head, then the CSP meta, byte for byte", output.StartsWith("<!DOCTYPE html><head><meta charset=\"utf-8\">" + WikiSite.Csp + "</head>", StringComparison.Ordinal) && WikiSite.Csp.Contains("default-src 'none'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'"));
            Check(lines, "html: a head that only appears in a comment still gets the CSP", WikiHtml.Render("<!-- <head> --><p>x</p>", new WikiAssets(cache, 2)).Contains("<head><meta charset=\"utf-8\">" + WikiSite.Csp));
            Check(lines, "html: scripts, inline handlers and comments are removed, text is kept", !Regex.IsMatch(output, "<script", RegexOptions.IgnoreCase) && !Regex.IsMatch(output, "\\son[a-z]+\\s*=", RegexOptions.IgnoreCase) && !output.Contains("alert(") && !output.Contains("<!--") && output.Contains(">bank</a>"));
            Check(lines, "html: article links go to the saved page, or to the not-saved page", output.Contains("<a href=\"" + bankLocal + "\">bank</a>") && output.Contains("<a href=\"/missing.html#Top\">missing</a>"));
            Check(lines, "html: red links, file links, javascript and external links become inert, anchors stay", output.Contains("<a href=\"#\">red</a>") && output.Contains("<a href=\"#\">file</a>") && output.Contains("<a href=\"#\">print</a>") && output.Contains("<a href=\"#\">ext</a>") && output.Contains("<a href=\"#sec\">anchor</a>"));
            Check(lines, "html: stylesheet links are rewritten to the saved CSS, other links, meta, base and iframes are gone", output.Contains("<link rel=\"stylesheet\" href=\"" + cssLocal + "\">") && !output.Contains("favicon") && !output.Contains("og:image") && !output.Contains("<base") && !output.Contains("<iframe"));
            Check(lines, "html: images keep only saved sources and srcset candidates, remote images are removed", output.Contains("src=\"" + imageLocal + "\"") && output.Contains("srcset=\"" + imageLocal + " 1.5x\"") && Regex.Matches(output, "<img ").Count == 1);
            Check(lines, "html: style attributes get the same url() rules", output.Contains("style=\"background:url(data:,) red\""));
            Check(lines, "html: a kept attribute is written re-encoded, so a quote inside a single-quoted value cannot end the attribute", WikiHtml.Render("<p title='a\" data-z=\"1'>x</p>", new WikiAssets(cache, 2)).Contains("<p title=\"a&quot; data-z=&quot;1\">x</p>"));
            Check(lines, "html: no remote URL and no evil host survives in the output", !output.Contains("http:") && !output.Contains("https:") && !output.Contains("//upload") && !output.Contains("evil") && !output.Contains("example.com") && !output.Contains("licensebuttons"));
            WikiAssets plan = new WikiAssets(new WikiCache(Path.Combine(root, "plan"), 1000000), 2);
            WikiHtml.Render(Sample, plan);
            Check(lines, "html: a plan lists only the allowed stylesheet and image keys", plan.Wanted.Count == 2 && plan.Wanted.Contains(CssUrl) && plan.Wanted.Contains(imagePath));
            StringBuilder many = new StringBuilder();
            for (int i = 0; i < 300; i++) many.Append("<img src=\"/images/a/ab/x" + i + ".png\">");
            WikiAssets capped = new WikiAssets(new WikiCache(Path.Combine(root, "capped"), 1000000), 2);
            WikiHtml.Render(many.ToString(), capped);
            Check(lines, "html: a plan holds at most 200 missing assets, the first ones found", capped.Wanted.Count == WikiAssets.MaxWanted && capped.Wanted.Count == 200 && capped.Wanted.Contains("/images/a/ab/x0.png") && !capped.Wanted.Contains("/images/a/ab/x200.png"));
            Check(lines, "html: a document with no head gets the CSP straight after the doctype", WikiHtml.Render("<!doctype html><p>x</p>", new WikiAssets(cache, 2)).StartsWith("<!doctype html><head><meta charset=\"utf-8\">" + WikiSite.Csp + "</head><p>x</p>", StringComparison.Ordinal));
            Check(lines, "html: a torn tag drops the rest of the input, and uppercase names and single quotes are handled", WikiHtml.Render("<p>a<b", new WikiAssets(cache, 2)).EndsWith("<p>a", StringComparison.Ordinal) && !WikiHtml.Render("<p title=\"x", new WikiAssets(cache, 2)).Contains("<p") && WikiHtml.Render("<A HREF='/Stalburg_Bank'>x</A>", new WikiAssets(cache, 2)).Contains("<a href=\"" + bankLocal + "\">x"));
            Check(lines, "html: an empty end tag is dropped, and plaintext content is not passed on", WikiHtml.Render("<p>x</ >y</p>", new WikiAssets(cache, 2)).Contains("<p>xy</p>") && !WikiHtml.Render("<plaintext><img src=\"/images/a/ab/x.png\">", new WikiAssets(cache, 2)).Contains("<img"));
            string hostile = WikiHtml.Render("<img src=\"javascript:alert(1)\"><a href=\"java&#x09;script:alert(1)\">x</a><svg><use href=\"https://evil.example/s.svg#a\"/></svg><form action=\"https://evil.example/\"><input name=q></form><object data=\"https://evil.example/o\"></object>", new WikiAssets(cache, 2));
            Check(lines, "html: hostile markup (javascript URLs, entities, svg use, form action, object data) leaves nothing loading", !hostile.Contains("evil") && !hostile.Contains("javascript") && !hostile.Contains("alert") && !hostile.Contains("action=") && !hostile.Contains("data="));
            string css = WikiCss.Render("@import url(x.css); a{background:url(/images/a/ab/x.png)} b{background:url(//evil.example/a.png)} c{background:url(\"https://evil.example/b.png\")} d{background:url(/images/a/ab/x.png?123)}", new WikiAssets(cache, 2));
            Check(lines, "css: url() to saved images is rewritten, with the query stripped, other urls become data:, and @import is removed", css.Contains("url(" + imageLocal + ")") && css.Contains("url(data:,)") && !css.Contains("@import") && !css.Contains("evil") && !css.Contains("?123"));
            string broken = WikiCss.Render("a{background:url(https://evil.example/x", new WikiAssets(cache, 2));
            Check(lines, "css: an unclosed url() is neutralised and nothing after it leaks", broken == "a{background:url(data:,)" && !broken.Contains("evil"));
            string spaced = WikiCss.Render("a{background:url(x y)} b{color:red}", new WikiAssets(cache, 2));
            Check(lines, "css: a url() with a space in it is neutralised and the next rule survives", spaced == "a{background:url(data:,)} b{color:red}");
            Check(lines, "csp: saved pages also forbid form actions and base URIs", WikiSite.Csp.Contains("form-action 'none'; base-uri 'none'"));
            Bypass(lines, cache, "meta refresh with a remote url", "<meta http-equiv=\"refresh\" content=\"0; url=https://evil.example/\">");
            string meta = WikiHtml.Render("<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><meta name=\"x\" content=\"https://evil.example/\">", new WikiAssets(cache, 2)).Replace(WikiSite.Csp, "");
            Check(lines, "meta: only charset and viewport survive, with no http-equiv", meta.Contains("<meta charset=\"utf-8\">") && meta.Contains("<meta name=\"viewport\" content=\"width=device-width\">") && !meta.Contains("evil") && !meta.Contains("http-equiv"));
            Bypass(lines, cache, "css escape u\\72l( in a style element", "<style>a{background:u\\72l(https://evil.example/a.png)}</style>");
            Bypass(lines, cache, "css escape \\75rl(// in a style element", "<style>a{background:\\75rl(//evil.example/a.png)}</style>");
            Bypass(lines, cache, "css escape @\\69mport in a style element", "<style>@\\69mport \"https://evil.example/x.css\";</style>");
            Bypass(lines, cache, "css escape u\\72l( in a style attribute", "<p style=\"background:u\\72l(//evil.example/a.png)\">x</p>");
            Bypass(lines, cache, "css @import url() is removed", "<style>@import url(https://evil.example/x.css);</style>");
            Bypass(lines, cache, "css image-set() with a string", "<style>a{background:image-set(\"https://evil.example/a.png\" 1x)}</style>");
            Bypass(lines, cache, "css -webkit-image-set() in a style attribute", "<p style=\"background:-webkit-image-set('https://evil.example/a.png' 1x)\">x</p>");
            Bypass(lines, cache, "css src(), image() and element() with strings", "<style>a{background:src(\"https://evil.example/a.png\")} b{background:image(\"https://evil.example/b.png\")} c{background:element(#x)}</style>");
            Bypass(lines, cache, "css comment inside url()", "<style>a{b:/**/u/**/rl(https://evil.example/a.png)} c{background:url(/**/https://evil.example/a.png)}</style>");
            string split = WikiHtml.Render("<style>d{content:\"url(\"} e{background:url(/images/a/ab/x.png)}</style>", new WikiAssets(cache, 2));
            Check(lines, "css: a url( inside a string does not eat the rule after it, and the saved image still resolves", split.Contains("e{background:url(" + imageLocal + ")}") && !Remote(split));
            Bypass(lines, cache, "svg set to a remote href", "<svg><set attributeName=\"href\" to=\"https://evil.example/\"/></svg>");
            Bypass(lines, cache, "svg animate with a remote value", "<svg><animate attributeName=\"href\" values=\"https://evil.example/\"/><a><rect/></a></svg>");
            Bypass(lines, cache, "svg fill url() to a remote target", "<svg><rect fill=\"url(https://evil.example/f.svg#x)\"/></svg>");
            Bypass(lines, cache, "svg filter url() to a protocol-relative target", "<svg><rect filter=\"url(//evil.example/f.svg)\"/></svg>");
            Bypass(lines, cache, "svg escaped url() in a presentation attribute", "<svg><rect fill=\"u\\72l(https://evil.example/f.svg)\"/></svg>");
            Bypass(lines, cache, "svg feImage with a remote x attribute", "<svg><filter><feImage x=\"https://evil.example/\"/></filter></svg>");
            Stopwatch clock = Stopwatch.StartNew();
            string torn = WikiHtml.Render(Repeat("<a ", 16000), new WikiAssets(cache, 2));
            clock.Stop();
            Check(lines, "html: 16000 unterminated start tags render in under 1 s", clock.ElapsedMilliseconds < 1000 && !torn.Contains("<a"));
            string credit = WikiHtml.Render("<p>x</p>", new WikiAssets(cache, 2, "Stalburg_Bank"));
            Check(lines, "html: a saved page ends with an attribution footer for its own title, with no live link", credit.Contains("CC BY-SA 4.0") && credit.ToLowerInvariant().Contains("not affiliated") && credit.Contains("Images are not licensed with the text") && credit.Contains("Source: https://stalburg.net/Stalburg_Bank</p></footer>") && !credit.Contains("<a "));
        }

        private const string Escaped = "\\68 ttp\\3a \\2f\\2f evil.example/x";

        private static void HardeningChecks(List<string> lines, string root)
        {
            WikiCache cache = new WikiCache(Path.Combine(root, "hardening"), 1000000);
            Check(lines, "css: an @import hidden by a CDO, a CDC or an earlier at-rule is removed", !Leaks(Page(cache, "<style><!--@import \"" + Escaped + "\";--></style>")) && !Leaks(Page(cache, "<style>-->@import \"" + Escaped + "\"</style>")) && !Leaks(Page(cache, "<style>@charset \"utf-8\";<!--@import \"" + Escaped + "\"</style>")) && !Leaks(Page(cache, "<style><!--@import \"x.css\";</style>")));
            string crlf = WikiCss.Render("body{content:'x\\\r\n';background:url(\"" + Escaped + "\")}", new WikiAssets(cache, 2));
            Check(lines, "css: a backslash before CRLF continues the string, so the url() after it is still rewritten", crlf.Contains("background:url(data:,)") && !Leaks(crlf));
            string svg = Page(cache, "<svg><style><img src=\"&#104;ttp&colon;&sol;&sol;evil.example/x\"></style></svg>");
            Match markup = Regex.Match(svg, "<style>(.*?)</style>", RegexOptions.Singleline);
            Check(lines, "css: a style element inside SVG cannot open markup, because < is written as a CSS escape", markup.Success && WebUtility.HtmlDecode(markup.Groups[1].Value).IndexOf('<') < 0 && !svg.Contains("<img"));
            string entities = Page(cache, "<svg><rect width=10 height=10 fill=\"url&lpar;&sol;&sol;evil.example/x.svg#f&rpar;\"/><rect mask=\"url&lpar;&sol;&sol;evil.example/m.svg#f&rpar;\"/><rect clip-path=\"url&lpar;&sol;&sol;evil.example/c.svg#f&rpar;\"/><path marker-end=\"url&lpar;&sol;&sol;evil.example/e.svg#f&rpar;\"/></svg>");
            Check(lines, "html: HTML5 entities in fill, mask, clip-path and marker-end cannot hide a remote url()", !Leaks(entities) && !entities.Contains("evil"));
            string meta = Page(cache, "<meta name=\"viewport\" http-equiv=\"refresh\" content=\"0;url=http://evil.example/x\"><meta charset=\"x;url=https://evil.example/\">").Replace(WikiSite.Csp, "").Replace("<meta charset=\"utf-8\">", "");
            Check(lines, "meta: a viewport content or charset outside its pattern is dropped, and no refresh survives", meta.Contains("<meta name=\"viewport\">") && !meta.Contains("charset") && !meta.Contains("http-equiv") && !Leaks(meta));
            string footer = WikiHtml.Render("<p>x</p>", new WikiAssets(cache, 2, "Stalburg_Bank"));
            Check(lines, "html: the footer names the licence URI, the modification and the creator as plain text", footer.Contains("https://creativecommons.org/licenses/by-sa/4.0/") && footer.Contains("Changed for offline reading: scripts removed and links made local.") && footer.Contains("By the Stalburg Wiki contributors."));
            Check(lines, "css: Unescape reads hex escapes with one trailing space or CRLF and keeps other escaped characters", WikiCss.Unescape("\\68 ttp\\3a \\2f\\2f") == "http://" && WikiCss.Unescape("\\68\r\nttp") == "http" && WikiCss.Unescape("\\:\\\\") == ":\\");
            string[] ampersands = { "<svg><style>.t{background:u&#114;l(http&colon;&sol;&sol;evil.example/x.png)}</style></svg>", "<svg><style>.t{background:u&#117;rl(http&colon;&sol;&sol;evil.example/x.png)}</style></svg>", "<svg><style>.t{background:ima&#103;e-set(\"http&colon;&sol;&sol;evil.example/a.png\" 1x)}</style></svg>", "<svg><style>&#64;import \"http&colon;&sol;&sol;evil.example/x.css\";</style></svg>", "<svg><style>@font-face{src:u&#114;l(http&colon;&sol;&sol;evil.example/f.woff)}</style></svg>" };
            bool inert = true;
            foreach (string payload in ampersands)
            {
                Match style = Regex.Match(Page(cache, payload), "<style>(.*?)</style>", RegexOptions.Singleline);
                string body = WebUtility.HtmlDecode(style.Groups[1].Value);
                if (!style.Success || body.IndexOf('&') >= 0 || body.IndexOf('<') >= 0 || body.IndexOf("url(", StringComparison.OrdinalIgnoreCase) >= 0 || body.IndexOf("@import", StringComparison.OrdinalIgnoreCase) >= 0 || body.IndexOf("image-set(", StringComparison.OrdinalIgnoreCase) >= 0) inert = false;
            }
            Check(lines, "css: a character reference in a style element cannot form url(), @import or image-set(), because & is written as a CSS escape", inert);
            bool continued = true;
            foreach (string hex in new[] { "\\41\n", "\\41\r", "\\41\r\n", "\\41\f", "\\000041\n" })
            {
                foreach (string quote in new[] { "\"", "'" })
                {
                    string local = WikiCss.Render(".t{content:" + quote + hex + quote + ";background:url(" + quote + "/images/a/ab/x.png" + quote + ")}", new WikiAssets(cache, 2));
                    string remote = WikiCss.Render(".t{content:" + quote + hex + quote + ";background:url(" + quote + "/\t/evil.example/x.png" + quote + ")}", new WikiAssets(cache, 2));
                    if (!local.Contains("url(data:,)") || !remote.Contains("url(data:,)") || remote.Contains("/\t/")) continued = false;
                }
            }
            string attribute = WikiHtml.Render("<p style=\"content:'\\41\n';background:url('/images/a/ab/x.png')\">x</p>", new WikiAssets(cache, 2));
            Check(lines, "css: a hex escape before a line break continues the string, so the url() after it is rewritten and no TAB host survives", continued && attribute.Contains("url(data:,)"));
            string charset = WikiHtml.Render("<meta charset=\"iso-2022-jp\"><p>x</p>", new WikiAssets(cache, 2));
            Check(lines, "html: an ISO-2022-JP charset meta is dropped and exactly one utf-8 charset meta is written", Regex.Matches(charset, "charset=").Count == 1 && charset.Contains("<head><meta charset=\"utf-8\">") && !charset.Contains("iso-2022"));
            string charsetCss = WikiCss.Render("@charset \"iso-2022-jp\";a{color:red}", new WikiAssets(cache, 2));
            Check(lines, "css: @charset is dropped, so a stylesheet cannot switch the decoder", !charsetCss.Contains("@charset") && charsetCss.Contains("a{color:red}"));
            string raw = WikiHtml.Render("<style>a{b:\u001B}</style><p title=\"&#27;x\" style=\"color:&#27;red\">\u001B(B</p>", new WikiAssets(cache, 2)) + WikiCss.Render("u\u001Brl(/x.png)", new WikiAssets(cache, 2));
            Check(lines, "html and css: no raw ESC character survives, so an ISO-2022 escape cannot reach the output", !raw.Contains("\u001B"));
            Check(lines, "css: Remote ignores TAB, CR and LF, so a tab inside a host or scheme still counts as remote", WikiCss.Remote("/\t/evil.example/x") && WikiCss.Remote("ht\ttp://evil.example"));
            Check(lines, "css: Unescape drops a backslash before LF, CR, FF or CRLF, so the two halves join", WikiCss.Unescape("ht\\\ntp:") == "http:" && WikiCss.Unescape("ht\\\r\ntp:") == "http:" && WikiCss.Unescape("ht\\\rtp:") == "http:" && WikiCss.Unescape("ht\\\ftp:") == "http:");
            bool footerOutside = WikiHtml.Render("<p>x</p><title>", new WikiAssets(cache, 2, "Stalburg_Bank")).Contains("</textarea></title><footer") && WikiHtml.Render("<p>x</p><textarea>", new WikiAssets(cache, 2, "Stalburg_Bank")).Contains("</textarea></title><footer") && WikiHtml.Render("<p>x</p><!--", new WikiAssets(cache, 2, "Stalburg_Bank")).Contains("</textarea></title><footer");
            Check(lines, "html: a page that ends inside an open title or textarea, or inside a comment, keeps the footer outside it", footerOutside);
        }

        private static string Page(WikiCache cache, string html)
        {
            return WikiHtml.Render(html, new WikiAssets(cache, 2));
        }

        private static bool Leaks(string html)
        {
            string text = WikiCss.Unescape(WebUtility.HtmlDecode(NamedEntities(html)));
            return text.IndexOf("//", StringComparison.Ordinal) >= 0 || text.IndexOf("http:", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("https:", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("@import", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NamedEntities(string text)
        {
            string[] names = { "&lpar;", "(", "&rpar;", ")", "&sol;", "/", "&colon;", ":", "&bsol;", "\\", "&period;", ".", "&NewLine;", "\n", "&Tab;", "\t" };
            for (int i = 0; i < names.Length; i += 2) text = text.Replace(names[i], names[i + 1]);
            return text;
        }

        private static void Bypass(List<string> lines, WikiCache cache, string name, string input)
        {
            Check(lines, "bypass: " + name + " leaves no remote reference", !Remote(WikiHtml.Render(input, new WikiAssets(cache, 2))));
        }

        private static bool Remote(string html)
        {
            string body = html.Replace(WikiSite.Csp, "");
            return body.IndexOf("http:", StringComparison.OrdinalIgnoreCase) >= 0 || body.IndexOf("https:", StringComparison.OrdinalIgnoreCase) >= 0 || body.IndexOf("//", StringComparison.Ordinal) >= 0 || body.IndexOf('\\') >= 0;
        }

        private static long DiskBytes(string dir)
        {
            long total = 0;
            foreach (string sub in new[] { "pages", "css", "img" })
            {
                string path = Path.Combine(dir, sub);
                if (!Directory.Exists(path)) continue;
                foreach (string file in Directory.GetFiles(path)) total += new FileInfo(file).Length;
            }
            return total;
        }

        private static byte[] Bytes(string ascii)
        {
            return Encoding.ASCII.GetBytes(ascii);
        }

        private static byte[] Zeros(int count)
        {
            return new byte[count];
        }

        private static byte[] Join(params byte[][] parts)
        {
            List<byte> all = new List<byte>();
            foreach (byte[] part in parts) all.AddRange(part);
            return all.ToArray();
        }

        private static byte[] BigEndian(long value, int count)
        {
            byte[] bytes = new byte[count];
            for (int i = 0; i < count; i++) bytes[count - 1 - i] = (byte)((value >> (8 * i)) & 0xFF);
            return bytes;
        }

        private static byte[] LittleEndian(long value, int count)
        {
            byte[] bytes = new byte[count];
            for (int i = 0; i < count; i++) bytes[i] = (byte)((value >> (8 * i)) & 0xFF);
            return bytes;
        }

        private static byte[] Head(byte[] data, int count)
        {
            byte[] head = new byte[count];
            Array.Copy(data, head, count);
            return head;
        }

        private static string Kind(long status, string type, string body, bool json)
        {
            return WikiResponse.Classify(new WikiReply { Status = status, ContentType = type, Body = Encoding.UTF8.GetBytes(body) }, json);
        }

        private static byte[] Png(long width, long height)
        {
            return Join(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, BigEndian(13, 4), Bytes("IHDR"), BigEndian(width, 4), BigEndian(height, 4), Zeros(5));
        }

        private static byte[] Gif(long width, long height)
        {
            return Join(Bytes("GIF89a"), LittleEndian(width, 2), LittleEndian(height, 2), Zeros(3));
        }

        private static byte[] Jpeg(long width, long height)
        {
            return Join(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 }, Zeros(14), new byte[] { 0xFF, 0xC0, 0x00, 0x11, 0x08 }, BigEndian(height, 2), BigEndian(width, 2), Zeros(4));
        }

        private static byte[] WebpExtended(long width, long height)
        {
            return Join(Bytes("RIFF"), Zeros(4), Bytes("WEBP"), Bytes("VP8X"), new byte[] { 10, 0, 0, 0 }, Zeros(4), LittleEndian(width - 1, 3), LittleEndian(height - 1, 3));
        }

        private static byte[] WebpLossless(long width, long height)
        {
            return Join(Bytes("RIFF"), Zeros(4), Bytes("WEBP"), Bytes("VP8L"), new byte[] { 5, 0, 0, 0, 0x2F }, LittleEndian((width - 1) | ((height - 1) << 14), 4), Zeros(5));
        }

        private static byte[] WebpLossy(long width, long height)
        {
            return Join(Bytes("RIFF"), Zeros(4), Bytes("WEBP"), Bytes("VP8 "), Zeros(4), new byte[] { 0x10, 0x02, 0x00, 0x9D, 0x01, 0x2A }, LittleEndian(width, 2), LittleEndian(height, 2));
        }
    }
}
