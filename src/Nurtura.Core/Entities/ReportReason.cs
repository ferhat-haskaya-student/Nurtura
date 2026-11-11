namespace Nurtura.Core.Entities;

// Why a mother reported a forum post or comment. Stored as text, so renaming a value needs a migration.
public enum ReportReason
{
    SomeoneInDanger,
    MedicalAdvice,
    PersonalInformation,
    Unkind,
    Spam,
    Other
}
