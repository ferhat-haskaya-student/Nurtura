using System.ComponentModel.DataAnnotations;
using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Mother.Models;

// Names and messages are keys in Resources/en.json and pl.json.
public class EditNoteViewModel
{
    public int Id { get; set; }

    // Normalized like the request form: "\r\n" counts as one character.
    [Required(ErrorMessage = "Book_ConcernRequired")]
    [StringLength(Appointment.ConcernMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Book_Concern")]
    public string? Concern
    {
        get => _concern;
        set => _concern = value?.ReplaceLineEndings("\n");
    }

    private string? _concern;

    // Filled in by the controller for display; not posted.
    public MotherAppointment? Item { get; set; }
}
