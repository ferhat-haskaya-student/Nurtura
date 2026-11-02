using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Nurtura.Web.Areas.Mother.Models;

// An enum value that is one of the defined options. Empty passes; [Required] handles that.
// MVC's enum binder already refuses most undefined values; this keeps the rule in the form itself.
// MVC only localizes the built-in attributes, so this one looks up its ErrorMessage key itself.
public sealed class DefinedEnumAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not Enum enumValue || Enum.IsDefined(enumValue.GetType(), enumValue))
        {
            return ValidationResult.Success;
        }

        var localizer = validationContext.GetRequiredService<IStringLocalizer<SharedResource>>();
        return new ValidationResult(localizer[ErrorMessage!]);
    }
}
