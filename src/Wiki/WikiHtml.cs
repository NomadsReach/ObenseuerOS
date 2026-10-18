using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ObenseuerOS
{
    internal static class WikiHtml
    {
        private static readonly HashSet<string> Blocked = new HashSet<string>(new[] { "script", "noscript", "iframe", "noembed", "noframes", "plaintext", "xmp", "template", "frameset" }, StringComparer.Ordinal);
        private static readonly HashSet<string> Hidden = new HashSet<string>(new[] { "head", "base", "frame", "embed", "source", "track", "param", "object", "video", "audio", "picture", "applet", "portal", "animate", "set", "animatemotion", "animatetransform" }, StringComparer.Ordinal);
        private static readonly HashSet<string> Bare = new HashSet<string>(new[] { "area", "base", "br", "col", "embed", "frame", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr" }, StringComparer.Ordinal);
        private static readonly HashSet<string> UrlAttrs = new HashSet<string>(new[] { "href", "src", "srcset", "action", "formaction", "poster", "data", "background", "cite", "longdesc", "usemap", "codebase", "archive", "classid", "icon", "manifest", "ping", "dynsrc", "lowsrc", "xlink:href", "srcdoc", "target", "download", "formtarget", "imagesrcset" }, StringComparer.Ordinal);
        private static readonly Regex Doctype = new Regex(@"\A\s*(<!doctype[^>]*>)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex AttrName = new Regex(@"\A[a-z_:][a-z0-9_:.\-]{0,63}\z", RegexOptions.CultureInvariant);
        private static readonly Regex Descriptor = new Regex(@"\A[0-9.]{1,6}[xw]\z", RegexOptions.CultureInvariant);
        private static readonly Regex NamedEntity = new Regex("&(lpar|rpar|sol|colon|bsol|period|NewLine|Tab);?", RegexOptions.CultureInvariant);
        private static readonly Regex Entity = new Regex(@"&[A-Za-z][A-Za-z0-9]*;", RegexOptions.CultureInvariant);
        private static readonly Regex ViewportContent = new Regex(@"\A[a-zA-Z0-9=.,\s-]{1,100}\z", RegexOptions.CultureInvariant);
        private static readonly Dictionary<string, string> EntityChars = new Dictionary<string, string> { { "lpar", "(" }, { "rpar", ")" }, { "sol", "/" }, { "colon", ":" }, { "bsol", "\\" }, { "period", "." }, { "NewLine", "\n" }, { "Tab", "\t" } };

        internal static string Render(string html, WikiAssets assets)
        {
            return new Renderer((html ?? "").Replace("\u001B", ""), assets).Run();
        }

        internal static string ImageKey(string url)
        {
            if (url == null) return null;
            string v = url.Trim();
            if (v.StartsWith(WikiTitle.Origin, StringComparison.Ordinal)) v = v.Substring(WikiTitle.Origin.Length);
            int query = v.IndexOf('?');
            if (query >= 0) v = v.Substring(0, query);
            return WikiTitle.IsImagePath(v) ? v : null;
        }

        private sealed class Attr
        {
            internal string Name = "";
            internal string Raw;
            internal bool HasValue;
        }

        private sealed class Tag
        {
            internal string Name = "";
            internal readonly List<Attr> Attrs = new List<Attr>();
            internal bool SelfClosing;
        }

        private sealed class Renderer
        {
            private readonly string html;
            private readonly WikiAssets assets;
            private readonly StringBuilder output = new StringBuilder();

            internal Renderer(string html, WikiAssets assets)
            {
                this.html = html;
                this.assets = assets;
            }

            internal string Run()
            {
                Match doctype = Doctype.Match(html);
                int i = doctype.Success ? doctype.Length : 0;
                if (doctype.Success) output.Append(doctype.Groups[1].Value);
                output.Append("<head><meta charset=\"utf-8\">").Append(WikiSite.Csp).Append("</head>");
                while (i < html.Length)
                {
                    int lt = html.IndexOf('<', i);
                    if (lt < 0)
                    {
                        output.Append(html, i, html.Length - i);
                        break;
                    }
                    output.Append(html, i, lt - i);
                    i = Markup(lt);
                }
                if (assets.Title != null) output.Append("</textarea></title>").Append(WikiSite.Credit(assets.Title));
                return output.ToString();
            }

            private int Markup(int lt)
            {
                if (Starts(lt, "<!--")) return Skip(lt + 4, "-->");
                char c = At(lt + 1);
                if (c == '!' || c == '?')
                {
                    int e = html.IndexOf('>', lt);
                    return e < 0 ? html.Length : e + 1;
                }
                if (c == '/') return EndTag(lt);
                if (c < 128 && char.IsLetter(c)) return StartTag(lt);
                return Literal(lt);
            }

            private int StartTag(int lt)
            {
                Tag tag = new Tag();
                int after = Parse(lt, tag);
                if (after < 0) return html.Length;
                if (Blocked.Contains(tag.Name)) return Skipped(tag, after);
                switch (tag.Name)
                {
                    case "style":
                        return Style(tag, after);
                    case "a":
                        return Anchor(tag, after);
                    case "img":
                        return Image(tag, after);
                    case "link":
                        return Link(tag, after);
                    case "meta":
                        return Meta(tag, after);
                }
                if (!Hidden.Contains(tag.Name)) Write(tag, Generic(tag), tag.SelfClosing);
                return after;
            }

            private int EndTag(int lt)
            {
                int e = html.IndexOf('>', lt);
                if (e < 0) return html.Length;
                string name = Name(lt + 2);
                if (name.Length > 0 && !Blocked.Contains(name) && !Bare.Contains(name)) output.Append("</").Append(name).Append('>');
                return e + 1;
            }

            private int Skipped(Tag tag, int after)
            {
                int e = html.IndexOf("</" + tag.Name, after, StringComparison.OrdinalIgnoreCase);
                if (e < 0) return html.Length;
                int gt = html.IndexOf('>', e);
                return gt < 0 ? html.Length : gt + 1;
            }

            private int Style(Tag tag, int after)
            {
                int e = html.IndexOf("</style", after, StringComparison.OrdinalIgnoreCase);
                int gt = e < 0 ? -1 : html.IndexOf('>', e);
                string body = html.Substring(after, (e < 0 ? html.Length : e) - after);
                Write(tag, Generic(tag), false);
                output.Append(WikiCss.Render(body, assets)).Append("</style>");
                return gt < 0 ? html.Length : gt + 1;
            }

            private int Anchor(Tag tag, int after)
            {
                List<KeyValuePair<string, string>> kept = new List<KeyValuePair<string, string>>();
                string href = Value(tag, "href");
                if (href != null) kept.Add(new KeyValuePair<string, string>("href", Encode(LinkValue(href))));
                kept.AddRange(Generic(tag));
                Write(tag, kept, tag.SelfClosing);
                return after;
            }

            private int Image(Tag tag, int after)
            {
                string local = LocalImage(Value(tag, "src"));
                if (local == null) return after;
                List<KeyValuePair<string, string>> kept = new List<KeyValuePair<string, string>>();
                kept.Add(new KeyValuePair<string, string>("src", Encode(local)));
                string set = Srcset(Value(tag, "srcset"));
                if (set != null) kept.Add(new KeyValuePair<string, string>("srcset", set));
                kept.AddRange(Generic(tag));
                Write(tag, kept, tag.SelfClosing);
                return after;
            }

            private int Link(Tag tag, int after)
            {
                string href = Value(tag, "href");
                if (href == null || !HasWord(Value(tag, "rel") ?? "", "stylesheet")) return after;
                string key = href.Trim();
                if (key.StartsWith("/", StringComparison.Ordinal)) key = WikiTitle.Origin + key;
                if (!key.StartsWith(WikiTitle.Origin + "/load.php?", StringComparison.Ordinal) || !WikiTitle.Allowed(key)) return after;
                string local = assets.Local(key);
                if (local == null) return after;
                List<KeyValuePair<string, string>> kept = new List<KeyValuePair<string, string>>();
                kept.Add(new KeyValuePair<string, string>("rel", "stylesheet"));
                kept.Add(new KeyValuePair<string, string>("href", Encode(local)));
                Write(tag, kept, tag.SelfClosing);
                return after;
            }

            private int Meta(Tag tag, int after)
            {
                List<KeyValuePair<string, string>> kept = new List<KeyValuePair<string, string>>();
                if (Value(tag, "charset") == null && string.Equals(Value(tag, "name"), "viewport", StringComparison.OrdinalIgnoreCase) && Value(tag, "content") != null)
                {
                    kept.Add(new KeyValuePair<string, string>("name", "viewport"));
                    if (ViewportContent.IsMatch(Value(tag, "content"))) kept.Add(new KeyValuePair<string, string>("content", Encode(Value(tag, "content"))));
                }
                if (kept.Count > 0) Write(tag, kept, tag.SelfClosing);
                return after;
            }

            private string LocalImage(string url)
            {
                string key = ImageKey(url);
                return key == null ? null : assets.Local(key);
            }

            private string Srcset(string value)
            {
                if (value == null) return null;
                List<string> parts = new List<string>();
                foreach (string candidate in value.Split(','))
                {
                    string[] bits = candidate.Trim().Split(new[] { ' ', '\t', '\n', '\r', '\f' }, StringSplitOptions.RemoveEmptyEntries);
                    if (bits.Length == 0 || bits.Length > 2 || (bits.Length == 2 && !Descriptor.IsMatch(bits[1]))) continue;
                    string local = LocalImage(bits[0]);
                    if (local != null) parts.Add(local + (bits.Length == 2 ? " " + bits[1] : ""));
                }
                return parts.Count == 0 ? null : Encode(string.Join(", ", parts.ToArray()));
            }

            private string LinkValue(string href)
            {
                string value = href.Trim();
                if (value.StartsWith("#", StringComparison.Ordinal))
                {
                    string anchor = WikiSite.Fragment(value);
                    return anchor.Length > 0 ? anchor : "#";
                }
                string title;
                string fragment;
                if (!WikiSite.ArticleLink(value, out title, out fragment)) return "#";
                return (assets.Saved(title) ? WikiSite.PagePath(title) : WikiSite.MissingPath) + fragment;
            }

            private List<KeyValuePair<string, string>> Generic(Tag tag)
            {
                List<KeyValuePair<string, string>> kept = new List<KeyValuePair<string, string>>();
                foreach (Attr attr in tag.Attrs)
                {
                    if (!AttrName.IsMatch(attr.Name) || attr.Name.StartsWith("on", StringComparison.Ordinal) || UrlAttrs.Contains(attr.Name)) continue;
                    if (attr.Name == "style" && attr.HasValue) kept.Add(new KeyValuePair<string, string>("style", Encode(WikiCss.Render(WebUtility.HtmlDecode(attr.Raw), assets))));
                    else if (!attr.HasValue) kept.Add(new KeyValuePair<string, string>(attr.Name, null));
                    else
                    {
                        string value = WebUtility.HtmlDecode(attr.Raw);
                        if (!Unsafe(value)) kept.Add(new KeyValuePair<string, string>(attr.Name, Encode(value)));
                    }
                }
                return kept;
            }

            private static bool Unsafe(string value)
            {
                string text = NamedEntity.Replace(value, match => EntityChars[match.Groups[1].Value]);
                return WikiCss.Remote(text) || text.IndexOf("url(", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf('\\') >= 0 || text.IndexOf('\u001B') >= 0 || Entity.IsMatch(text);
            }

            private void Write(Tag tag, List<KeyValuePair<string, string>> kept, bool selfClosing)
            {
                output.Append('<').Append(tag.Name);
                foreach (KeyValuePair<string, string> pair in kept)
                {
                    output.Append(' ').Append(pair.Key);
                    if (pair.Value != null) output.Append("=\"").Append(pair.Value).Append('"');
                }
                output.Append(selfClosing ? " />" : ">");
            }

            private string Value(Tag tag, string name)
            {
                foreach (Attr attr in tag.Attrs)
                {
                    if (attr.Name == name) return attr.HasValue ? WebUtility.HtmlDecode(attr.Raw) : "";
                }
                return null;
            }

            private int Parse(int lt, Tag tag)
            {
                tag.Name = Name(lt + 1);
                int i = lt + 1 + tag.Name.Length;
                while (true)
                {
                    while (i < html.Length && IsSpace(html[i])) i++;
                    if (i >= html.Length) return -1;
                    if (html[i] == '>') return i + 1;
                    if (html[i] == '/')
                    {
                        i++;
                        if (At(i) == '>')
                        {
                            tag.SelfClosing = true;
                            return i + 1;
                        }
                        continue;
                    }
                    int start = i;
                    while (i < html.Length && !IsSpace(html[i]) && html[i] != '>' && html[i] != '=' && html[i] != '/') i++;
                    if (i == start)
                    {
                        i++;
                        continue;
                    }
                    Attr attr = new Attr { Name = html.Substring(start, i - start).ToLowerInvariant() };
                    int j = i;
                    while (j < html.Length && IsSpace(html[j])) j++;
                    if (At(j) == '=')
                    {
                        j++;
                        while (j < html.Length && IsSpace(html[j])) j++;
                        if (j >= html.Length) return -1;
                        if (html[j] == '"' || html[j] == '\'')
                        {
                            int close = html.IndexOf(html[j], j + 1);
                            if (close < 0) return -1;
                            attr.Raw = html.Substring(j + 1, close - j - 1);
                            i = close + 1;
                        }
                        else
                        {
                            int value = j;
                            while (j < html.Length && !IsSpace(html[j]) && html[j] != '>') j++;
                            attr.Raw = html.Substring(value, j - value);
                            i = j;
                        }
                        attr.HasValue = true;
                    }
                    else i = j;
                    tag.Attrs.Add(attr);
                }
            }

            private int Literal(int lt)
            {
                output.Append("&lt;");
                return lt + 1;
            }

            private string Name(int i)
            {
                int j = i;
                while (j < html.Length && html[j] < 128 && (char.IsLetterOrDigit(html[j]) || html[j] == '-' || html[j] == ':')) j++;
                return html.Substring(i, j - i).ToLowerInvariant();
            }

            private int Skip(int from, string token)
            {
                int e = html.IndexOf(token, from, StringComparison.Ordinal);
                return e < 0 ? html.Length : e + token.Length;
            }

            private bool Starts(int at, string token)
            {
                return html.Length - at >= token.Length && string.CompareOrdinal(html, at, token, 0, token.Length) == 0;
            }

            private char At(int i)
            {
                return i < html.Length ? html[i] : '\0';
            }

            private static bool IsSpace(char c)
            {
                return c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f';
            }

            private static bool HasWord(string text, string word)
            {
                foreach (string part in text.Split(new[] { ' ', '\t', '\n', '\r', '\f' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (string.Equals(part, word, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }

            private static string Encode(string text)
            {
                return WebUtility.HtmlEncode(text);
            }
        }
    }
}
