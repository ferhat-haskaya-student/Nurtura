using Nurtura.Core.Services;
using Nurtura.Web.Content;

namespace Nurtura.Web.Areas.Mother.Models;

// One guidance page, with its Markdown body already turned into safe HTML (raw HTML switched off).
public sealed class GuidePageViewModel
{
    public GuidePageViewModel(GuidancePage page)
    {
        Page = page;
        BodyHtml = GuidanceMarkdown.ToHtml(page.Content.Body);
    }

    public GuidancePage Page { get; }

    public string BodyHtml { get; }
}
