using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nurtura.Core.Entities;

namespace Nurtura.Web.Areas.Mother.Models;

public enum SleepTimerState
{
    // Not started: Start, or set both times for a past sleep and Save.
    New,
    Running,
    Stopped
}

// The timer screen. Times are Warsaw time; dates bind from dd-mm-yyyy, times from HH:mm.
// Names and messages are keys in Resources/en.json and pl.json.
public class SleepTimerViewModel
{
    [Required(ErrorMessage = "Validation_Required")]
    [Display(Name = "Sleep_StartDate")]
    public DateOnly? StartDate { get; set; }

    [Required(ErrorMessage = "Validation_Required")]
    [Display(Name = "Sleep_StartTime")]
    public TimeOnly? StartTime { get; set; }

    [Display(Name = "Sleep_EndDate")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "Sleep_EndTime")]
    public TimeOnly? EndTime { get; set; }

    // The "now" the start fields were filled with. Start with them unchanged uses the exact time of the tap.
    public string? DefaultStart { get; set; }

    // Line breaks are normalized to "\n" when bound, so a browser's "\r\n" counts as one character,
    // the same as the counter under the box.
    [StringLength(SleepRecord.NoteMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Sleep_Note")]
    public string? Note
    {
        get => _note;
        set => _note = value?.ReplaceLineEndings("\n");
    }

    private string? _note;

    public bool IsShared { get; set; }

    // Shown on the screen; never bound from the form.

    [BindNever]
    public int? SleepId { get; set; }

    [BindNever]
    public int BabyId { get; set; }

    [BindNever]
    public string BabyName { get; set; } = string.Empty;

    [BindNever]
    public SleepTimerState State { get; set; }

    // UTC; set while it runs, for the ticking time.
    [BindNever]
    public DateTime? RunningSince { get; set; }

    [BindNever]
    public bool IsLongRunning { get; set; }

    // Resume is shown only on the baby's latest sleep, within an hour of its end.
    [BindNever]
    public bool CanResume { get; set; }

    // How long a stopped sleep lasted.
    [BindNever]
    public TimeSpan? Duration { get; set; }
}
