using System.ComponentModel.DataAnnotations;
using Nurtura.Core.Entities;

namespace Nurtura.Web.Areas.Mother.Models;

// Names and messages are keys in Resources/en.json and pl.json.
// "At least one" for concerns and support types, and "birth date not in the future", are checked in OnboardingController.
// The birth date is bound from dd-mm-yyyy by ModelBinding/DateOnlyModelBinder.
public class OnboardingViewModel
{
    [Display(Name = "Onboarding_Concerns")]
    public List<Topic> Concerns { get; set; } = [];

    [Display(Name = "Onboarding_SupportType")]
    public List<SupportType> SupportTypes { get; set; } = [];

    [Required(ErrorMessage = "Validation_SelectOne")]
    [Display(Name = "Onboarding_IsFirstBaby")]
    public bool? IsFirstBaby { get; set; }

    [Required(ErrorMessage = "Validation_SelectOne")]
    [Display(Name = "Onboarding_HelpAtHome")]
    public HelpAtHome? HelpAtHome { get; set; }

    [Required(ErrorMessage = "Validation_Required")]
    [StringLength(Baby.NameMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Onboarding_BabyName")]
    public string BabyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required")]
    [Display(Name = "Onboarding_BabyBirthDate")]
    public DateOnly? BabyBirthDate { get; set; }

    [Required(ErrorMessage = "Validation_SelectOne")]
    [Display(Name = "Onboarding_BirthType")]
    public BirthType? BirthType { get; set; }

    [Required(ErrorMessage = "Validation_Required")]
    [Range(Baby.BirthWeekMin, Baby.BirthWeekMax, ErrorMessage = "Validation_Range")]
    [Display(Name = "Onboarding_BirthWeek")]
    public int? BirthWeek { get; set; } = Baby.BirthWeekDefault;

    [Required(ErrorMessage = "Validation_SelectOne")]
    [Display(Name = "Onboarding_FeedingType")]
    public BabyFeedingType? FeedingType { get; set; }
}
