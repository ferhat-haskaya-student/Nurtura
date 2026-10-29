namespace Nurtura.Core.Entities;

public class MotherProfile
{
    public const int DisplayNameMaxLength = 50;

    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    // Not used yet; photo upload comes later.
    public string? PhotoUrl { get; set; }

    // Empty until she finishes onboarding.
    public bool? IsFirstBaby { get; set; }

    // Empty until she finishes onboarding.
    public HelpAtHome? HelpAtHome { get; set; }

    // UTC. Empty until she finishes onboarding.
    public DateTime? OnboardingCompletedAt { get; set; }

    // UTC.
    public DateTime CreatedAt { get; set; }
}
