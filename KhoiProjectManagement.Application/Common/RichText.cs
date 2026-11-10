using Ganss.Xss;

namespace KhoiProjectManagement.Application
{
    // Descriptions are rich text (HTML written by the editor). Whatever a client sends is scrubbed here before it is
    // stored, so a crafted request can never plant script in a page other people open.
    public static class RichText
    {
        // The sanitizer is configured once per thread: its settings are mutable, so one shared instance is not assumed safe.
        private static readonly ThreadLocal<HtmlSanitizer> Sanitizer = new(Build);

        // The description column holds this many characters. Sanitising can make the text slightly longer (links gain
        // target/rel), so the limit is checked on the result, not just the request.
        public const int MaxLength = 20000;

        private static HtmlSanitizer Build()
        {
            var s = new HtmlSanitizer();
            s.AllowedTags.Clear();
            foreach (var tag in new[] { "p", "br", "strong", "b", "em", "i", "u", "s", "h1", "h2", "h3", "ul", "ol", "li", "blockquote", "code", "pre", "a", "hr", "div", "span" })
                s.AllowedTags.Add(tag);
            s.AllowedAttributes.Clear();
            s.AllowedAttributes.Add("href");
            s.AllowedSchemes.Clear();
            s.AllowedSchemes.Add("http");
            s.AllowedSchemes.Add("https");
            s.AllowedSchemes.Add("mailto");
            s.AllowedCssProperties.Clear();
            // Links open in a new tab and never hand the page to the target.
            s.PostProcessNode += (_, e) =>
            {
                if (e.Node is AngleSharp.Dom.IElement a && a.LocalName == "a")
                {
                    a.SetAttribute("target", "_blank");
                    a.SetAttribute("rel", "noopener noreferrer");
                }
            };
            return s;
        }

        public static string Sanitize(string? html) => string.IsNullOrWhiteSpace(html) ? string.Empty : Sanitizer.Value!.Sanitize(html).Trim();

        // Sanitise for storage; a result that no longer fits is a 400, never a database error.
        public static string SanitizeForStorage(string? html)
        {
            var clean = Sanitize(html);
            if (clean.Length > MaxLength) throw new InvalidOperationException($"The description is too long (limit {MaxLength} characters including formatting).");
            return clean;
        }

        // Words that only ever appear in the markup of a description, never as something a person wrote. A search for one of
        // them must not match every formatted description.
        private static readonly HashSet<string> MarkupWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "strong", "blockquote", "span", "href", "noopener", "noreferrer", "target", "_blank", "rel", "https", "http", "mailto", "div",
        };
        public static bool IsMarkupWord(string term) => MarkupWords.Contains(term.Trim());
    }
}
