using Ganss.Xss;

namespace KhoiProjectManagement.Application
{
    // Descriptions are rich text (HTML written by the editor). Whatever a client sends is scrubbed here before it is
    // stored, so a crafted request can never plant script in a page other people open.
    public static class RichText
    {
        private static readonly HtmlSanitizer Sanitizer = Build();

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

        public static string Sanitize(string? html) => string.IsNullOrWhiteSpace(html) ? string.Empty : Sanitizer.Sanitize(html).Trim();
    }
}
