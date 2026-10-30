using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Everything the professional fills in on the sign-up form.
public sealed record ProfessionalRegistration(
    string DisplayName,
    string Email,
    string Password,
    ProfessionalType ProfessionalType,
    IReadOnlyCollection<Topic> Topics,
    IReadOnlyCollection<Language> Languages,
    string Bio);
