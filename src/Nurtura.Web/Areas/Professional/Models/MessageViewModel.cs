using System.ComponentModel.DataAnnotations;
using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Professional.Models;

// Reject a request or cancel an appointment, with an optional message to the mother.
// Names and messages are keys in Resources/en.json and pl.json.
public class MessageViewModel
{
    public int Id { get; set; }

    // Optional. Normalized like the mother's note: "\r\n" counts as one character.
    [StringLength(Appointment.MessageMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Appointments_MessageField")]
    public string? Message
    {
        get => _message;
        set => _message = value?.ReplaceLineEndings("\n");
    }

    private string? _message;

    // Filled in by the controller for display; not posted.
    public ProfessionalAppointment? Item { get; set; }
}
