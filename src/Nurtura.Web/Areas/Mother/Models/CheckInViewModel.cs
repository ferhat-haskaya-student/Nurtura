using System.ComponentModel.DataAnnotations;
using Nurtura.Core.Entities;

namespace Nurtura.Web.Areas.Mother.Models;

// Names and messages are keys in Resources/en.json and pl.json.
// "Date is today or up to 7 days back" is checked in CheckInController.
// The date is bound from dd-mm-yyyy by ModelBinding/DateOnlyModelBinder.
public class CheckInViewModel
{
    [Required(ErrorMessage = "Validation_Required")]
    [Display(Name = "CheckIn_Date")]
    public DateOnly? Date { get; set; }

    [Required(ErrorMessage = "Validation_SelectOne")]
    [Range(DailyCheckIn.RatingMin, DailyCheckIn.RatingMax, ErrorMessage = "Validation_Range")]
    [Display(Name = "CheckIn_Mood")]
    public int? Mood { get; set; }

    [Required(ErrorMessage = "Validation_SelectOne")]
    [Range(DailyCheckIn.RatingMin, DailyCheckIn.RatingMax, ErrorMessage = "Validation_Range")]
    [Display(Name = "CheckIn_SleepQuality")]
    public int? SleepQuality { get; set; }

    [Required(ErrorMessage = "Validation_SelectOne")]
    [Range(DailyCheckIn.RatingMin, DailyCheckIn.RatingMax, ErrorMessage = "Validation_Range")]
    [Display(Name = "CheckIn_Energy")]
    public int? Energy { get; set; }

    [Required(ErrorMessage = "Validation_SelectOne")]
    [Range(DailyCheckIn.RatingMin, DailyCheckIn.RatingMax, ErrorMessage = "Validation_Range")]
    [Display(Name = "CheckIn_Stress")]
    public int? Stress { get; set; }

    [Display(Name = "CheckIn_FeelingTags")]
    public List<int> FeelingTagIds { get; set; } = [];

    // Line breaks are normalized to "\n" when bound, so a browser's "\r\n" counts as one character,
    // the same as the counter under the box.
    [StringLength(DailyCheckIn.NoteMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "CheckIn_Note")]
    public string? Note
    {
        get => _note;
        set => _note = value?.ReplaceLineEndings("\n");
    }

    private string? _note;

    public bool IsShared { get; set; }

    // Filled in by the controller for display; not posted.
    public bool IsExisting { get; set; }

    public IReadOnlyList<FeelingTag> AvailableTags { get; set; } = [];
}
