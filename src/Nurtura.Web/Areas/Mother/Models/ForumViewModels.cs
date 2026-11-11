using System.ComponentModel.DataAnnotations;
using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Mother.Models;

// Forum forms and pages. Names and messages are keys in Resources/en.json and pl.json. Text fields are
// normalized like the closing note ("\r\n" counts as one character), so the counter and the limit agree.
// ForumService checks everything again.

public sealed record ForumIndexViewModel(ForumList List, ForumFilter Filter);

public sealed record ForumSearchViewModel(string Search, List<ForumPostCard> Results);

public class ForumPostFormModel
{
    // 0 for a new post.
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation_SelectOne")]
    [Display(Name = "Forum_Category")]
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Validation_SelectOne")]
    [DefinedEnum(ErrorMessage = "Validation_SelectOne")]
    [Display(Name = "Forum_Language")]
    public Language? Language { get; set; }

    [Required(ErrorMessage = "Validation_Required")]
    [StringLength(ForumPost.TitleMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Forum_PostTitle")]
    public string? Title
    {
        get => _title;
        set => _title = value?.ReplaceLineEndings(" ");
    }

    [Required(ErrorMessage = "Validation_Required")]
    [StringLength(ForumPost.BodyMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Forum_PostBody")]
    public string? Body
    {
        get => _body;
        set => _body = value?.ReplaceLineEndings("\n");
    }

    private string? _title;
    private string? _body;

    // Filled in by the controller for display; not posted.
    public IReadOnlyList<ForumCategory> Categories { get; set; } = [];

    // Editing only: when the 30 minutes end (UTC).
    public DateTime? EditableUntil { get; set; }

    public ForumPostInput ToInput() => new(CategoryId ?? 0, Language ?? default, Title, Body);
}

public class ForumCommentFormModel
{
    // The comment, when editing.
    public int Id { get; set; }

    public int PostId { get; set; }

    [Required(ErrorMessage = "Validation_Required")]
    [StringLength(ForumComment.BodyMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Forum_CommentBody")]
    public string? Body
    {
        get => _body;
        set => _body = value?.ReplaceLineEndings("\n");
    }

    private string? _body;

    // Editing only: when the 30 minutes end (UTC).
    public DateTime? EditableUntil { get; set; }
}

public class ForumReportFormModel
{
    // The post, or the comment when IsComment.
    public int Id { get; set; }

    public bool IsComment { get; set; }

    [Required(ErrorMessage = "Validation_SelectOne")]
    [DefinedEnum(ErrorMessage = "Validation_SelectOne")]
    [Display(Name = "Forum_ReportReason")]
    public ReportReason? Reason { get; set; }

    [StringLength(PostReport.NoteMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Forum_ReportNote")]
    public string? Note
    {
        get => _note;
        set => _note = value?.ReplaceLineEndings("\n");
    }

    private string? _note;

    // Filled in by the controller: the post to go back to.
    public int PostId { get; set; }
}

// The post page. Comment is the comment box (with her text and errors after a refused send).
public sealed record ForumPostPageViewModel(ForumPostView Post, bool Urgent, ForumCommentFormModel Comment);
