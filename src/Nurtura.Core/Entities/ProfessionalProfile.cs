namespace Nurtura.Core.Entities;

public class ProfessionalProfile
{
    public const int DisplayNameMaxLength = 50;
    public const int BioMaxLength = 500;

    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    // Not used yet; photo upload comes later.
    public string? PhotoUrl { get; set; }

    public ProfessionalType ProfessionalType { get; set; }

    public string Bio { get; set; } = string.Empty;

    // Starts as Pending; only an approved professional can use the Professional area.
    public ApprovalStatus ApprovalStatus { get; set; }

    // The admin can deactivate an approved professional: she cannot use her area and is not shown in booking.
    // Nothing of hers is deleted, and the admin can reactivate her.
    public bool IsActive { get; set; } = true;

    // UTC.
    public DateTime CreatedAt { get; set; }
}
