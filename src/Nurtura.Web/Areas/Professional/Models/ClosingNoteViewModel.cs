using System.ComponentModel.DataAnnotations;
using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Professional.Models;

// Write or edit the closing note. Names and messages are keys in Resources/en.json and pl.json.
public class ClosingNoteViewModel
{
    public int Id { get; set; }

    // Normalized like the chat: "\r\n" counts as one character. The service checks it again.
    [Required(ErrorMessage = "ClosingNote_Required")]
    [StringLength(ConsultationNote.TextMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "ClosingNote_Field")]
    public string? Text
    {
        get => _text;
        set => _text = value?.ReplaceLineEndings("\n");
    }

    private string? _text;

    // Filled in by the controller for display; not posted.
    public Consultation? Consultation { get; set; }
}
