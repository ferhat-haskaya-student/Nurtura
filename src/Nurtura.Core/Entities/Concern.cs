namespace Nurtura.Core.Entities;

// The six main concerns from onboarding; the same topics as the appointment categories.
// Stored as text, so renaming a value needs a migration.
public enum Concern
{
    PostpartumWellbeing,
    BabySleep,
    BabyFeeding,
    Nutrition,
    RecoveryAfterBirth,
    EmotionalSupport
}
