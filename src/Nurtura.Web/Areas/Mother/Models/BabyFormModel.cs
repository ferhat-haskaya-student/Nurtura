using System.ComponentModel.DataAnnotations;
using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Mother.Models;

// The baby questions, shared by onboarding and by adding and editing a baby (Views/Shared/_BabyFields.cshtml).
// Names and messages are keys in Resources/en.json and pl.json.
// The birth date is bound from dd-mm-yyyy by ModelBinding/DateOnlyModelBinder.
public class BabyFormModel
{
    [Required(ErrorMessage = "Validation_Required")]
    [StringLength(Baby.NameMaxLength, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Onboarding_BabyName")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required")]
    [NotInFuture(ErrorMessage = "Validation_DateNotInFuture")]
    [Display(Name = "Onboarding_BabyBirthDate")]
    public DateOnly? BirthDate { get; set; }

    [Required(ErrorMessage = "Validation_SelectOne")]
    [DefinedEnum(ErrorMessage = "Validation_SelectOne")]
    [Display(Name = "Onboarding_BirthType")]
    public BirthType? BirthType { get; set; }

    [Required(ErrorMessage = "Validation_Required")]
    [Range(Baby.BirthWeekMin, Baby.BirthWeekMax, ErrorMessage = "Validation_Range")]
    [Display(Name = "Onboarding_BirthWeek")]
    public int? BirthWeek { get; set; } = Baby.BirthWeekDefault;

    [Required(ErrorMessage = "Validation_SelectOne")]
    [DefinedEnum(ErrorMessage = "Validation_SelectOne")]
    [Display(Name = "Onboarding_FeedingType")]
    public BabyFeedingType? FeedingType { get; set; }

    // Only call after the model is valid.
    public BabyDetails ToDetails() =>
        new(Name, BirthDate!.Value, BirthType!.Value, BirthWeek!.Value, FeedingType!.Value);

    public static BabyFormModel FromBaby(Baby baby) => new()
    {
        Name = baby.Name,
        BirthDate = baby.BirthDate,
        BirthType = baby.BirthType,
        BirthWeek = baby.BirthWeek,
        FeedingType = baby.FeedingType
    };
}
