using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Mother.Models;

// The Nurtura Assistant search box. LabelKey is the resource key of its label; From is the page it sits on
// ("Guides" or "Book"), where a blank search goes back to. Search fills the box on the results page.
public sealed record AssistantSearchBox(string LabelKey, string From, string? Search = null);

public sealed record AssistantResultsViewModel(AssistantSearchBox Box, AssistantResult Result);
