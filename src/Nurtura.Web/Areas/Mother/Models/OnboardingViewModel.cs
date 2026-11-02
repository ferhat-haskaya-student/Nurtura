using System.ComponentModel.DataAnnotations;
using Nurtura.Core.Entities;

namespace Nurtura.Web.Areas.Mother.Models;

// Names and messages are keys in Resources/en.json and pl.json.
// "At least one" for concerns and support types is checked in OnboardingController.
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

    // The same questions as adding a baby later.
    public BabyFormModel Baby { get; set; } = new();
}
