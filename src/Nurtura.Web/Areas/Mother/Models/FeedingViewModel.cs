using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nurtura.Core.Entities;

namespace Nurtura.Web.Areas.Mother.Models;

// The feeding screen. The time is Warsaw time (date dd-mm-yyyy, time HH:mm, from the time picker).
// Only the chosen type's fields are used; FeedingController drops the others before saving.
// "Side for breast" and "content for bottle" are checked in FeedingController.
// Names and messages are keys in Resources/en.json and pl.json.
public class FeedingViewModel
{
    [Required(ErrorMessage = "Validation_Required")]
    [Display(Name = "Feeding_Date")]
    public DateOnly? Date { get; set; }

    [Required(ErrorMessage = "Validation_Required")]
    [Display(Name = "Feeding_Time")]
    public TimeOnly? Time { get; set; }

    // The "now" the time field was filled with. Saved unchanged, the exact time of the tap is used.
    public string? DefaultTime { get; set; }

    [Required(ErrorMessage = "Validation_SelectOne")]
    [DefinedEnum(ErrorMessage = "Validation_SelectOne")]
    [Display(Name = "Feeding_Type")]
    public FeedingType? Type { get; set; }

    [DefinedEnum(ErrorMessage = "Validation_SelectOne")]
    [Display(Name = "Feeding_Side")]
    public BreastSide? BreastSide { get; set; }

    [Range(FeedingRecord.MinutesMin, FeedingRecord.MinutesMax, ErrorMessage = "Validation_Range")]
    [Display(Name = "Feeding_Minutes")]
    public int? Minutes { get; set; }

    [Range(FeedingRecord.AmountMlMin, FeedingRecord.AmountMlMax, ErrorMessage = "Validation_Range")]
    [Display(Name = "Feeding_AmountMl")]
    public int? AmountMl { get; set; }

    [DefinedEnum(ErrorMessage = "Validation_SelectOne")]
    [Display(Name = "Feeding_Content")]
    public BottleContent? BottleContent { get; set; }

    // Line breaks are normalized to "\n" when bound, so a browser's "\r\n" counts as one character,
    // the same as the counter under the box.
    [StringLength(FeedingRecord.NoteMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Feeding_Note")]
    public string? Note
    {
        get => _note;
        set => _note = value?.ReplaceLineEndings("\n");
    }

    private string? _note;

    public bool IsShared { get; set; }

    // Shown on the screen; never bound from the form.

    [BindNever]
    public int? FeedId { get; set; }

    [BindNever]
    public int BabyId { get; set; }

    [BindNever]
    public string BabyName { get; set; } = string.Empty;

    // Side of her last breastfeed for this baby, shown as "Last time: left". Never pre-selected.
    [BindNever]
    public BreastSide? LastBreastSide { get; set; }
}
