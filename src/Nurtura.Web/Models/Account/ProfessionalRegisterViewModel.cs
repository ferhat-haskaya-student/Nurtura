using System.ComponentModel.DataAnnotations;
using Nurtura.Core.Entities;

namespace Nurtura.Web.Models.Account;

// Names and messages are keys in Resources/en.json and pl.json.
// "At least one" for topics and languages is checked in AccountController.
public class ProfessionalRegisterViewModel
{
    [Required(ErrorMessage = "Validation_Required")]
    [StringLength(ProfessionalProfile.DisplayNameMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Account_DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required")]
    [EmailAddress(ErrorMessage = "Validation_Email")]
    [Display(Name = "Account_Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required")]
    [DataType(DataType.Password)]
    [Display(Name = "Account_Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Validation_PasswordsDoNotMatch")]
    [Display(Name = "Account_ConfirmPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_SelectOne")]
    [Display(Name = "ProfessionalRegister_ProfessionalType")]
    public ProfessionalType? ProfessionalType { get; set; }

    [Display(Name = "ProfessionalRegister_Topics")]
    public List<Topic> Topics { get; set; } = [];

    [Display(Name = "ProfessionalRegister_Languages")]
    public List<Language> Languages { get; set; } = [];

    [Required(ErrorMessage = "Validation_Required")]
    [StringLength(ProfessionalProfile.BioMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "ProfessionalRegister_Bio")]
    public string Bio { get; set; } = string.Empty;
}
