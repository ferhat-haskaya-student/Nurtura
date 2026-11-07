using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Nurtura.Web.Content;

// Turns a guidance page body (simple Markdown: headings, paragraphs, lists, links) into HTML.
// Raw HTML is switched off (DisableHtml), so any HTML or script in a body is shown as escaped text.
// Links may only be http, https, mailto or relative: a javascript: or data: link loses its address.
public static class GuidanceMarkdown
{
    private const string NoUrl = "#";

    private static readonly string[] AllowedSchemes = [Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto];

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .Build();

    public static string ToHtml(string markdown)
    {
        var document = Markdown.Parse(markdown, Pipeline);

        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!IsSafeUrl(link.Url))
            {
                link.Url = NoUrl;
            }
        }

        foreach (var link in document.Descendants<AutolinkInline>())
        {
            if (!IsSafeUrl(link.Url))
            {
                link.Url = NoUrl;
            }
        }

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    // Also used for the source links under a page.
    public static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        // No colon: a relative address or an anchor on the page.
        if (!url.Contains(':'))
        {
            return true;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
    }
}
