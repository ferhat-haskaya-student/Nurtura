using System.ComponentModel.DataAnnotations;
using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Mother.Models;

// Names and messages are keys in Resources/en.json and pl.json.
// The slot, the 12 hours to 4 weeks window and the limits are checked in BookingService.
public class BookRequestViewModel
{
    public int SlotId { get; set; }

    [Required(ErrorMessage = "Validation_Required")]
    [DefinedEnum(ErrorMessage = "Validation_SelectOne")]
    public Topic? Topic { get; set; }

    // Line breaks are normalized to "\n" when bound, so a browser's "\r\n" counts as one character,
    // the same as the counter under the box.
    [Required(ErrorMessage = "Book_ConcernRequired")]
    [StringLength(Appointment.ConcernMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Book_Concern")]
    public string? Concern
    {
        get => _concern;
        set => _concern = value?.ReplaceLineEndings("\n");
    }

    private string? _concern;

    // Both off by default.
    public bool ShareCheckIns { get; set; }

    // The baby whose sleeps and feeds she chose to mark as shared; empty for none.
    public int? ShareBabyId { get; set; }

    // Her appointment that this request replaces, when rescheduling.
    public int? RescheduleId { get; set; }

    // Filled in by the controller for display; not posted.
    public BookingRequestPage? Page { get; set; }

    // Filled in by the controller when rescheduling; not posted.
    public RescheduleInfo? Reschedule { get; set; }

    // Why the request was not sent, when it was refused on the server.
    public BookingStatus? Problem { get; set; }
}
