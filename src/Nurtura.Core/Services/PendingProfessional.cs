using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// One professional on the admin's approval list.
public sealed record PendingProfessional(
    int ProfileId,
    string DisplayName,
    string? Email,
    ProfessionalType ProfessionalType,
    IReadOnlyList<Topic> Topics,
    IReadOnlyList<Language> Languages,
    string Bio,
    DateTime CreatedAt);
