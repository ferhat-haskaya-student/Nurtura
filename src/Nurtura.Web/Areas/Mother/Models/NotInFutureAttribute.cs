using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Nurtura.Core;

namespace Nurtura.Web.Areas.Mother.Models;

// A date that is not after today in Poland. Empty passes; [Required] handles that.
// A property attribute, so its message shows together with the other field errors.
// MVC only localizes the built-in attributes, so this one looks up its ErrorMessage key itself.
public sealed class NotInFutureAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateOnly date || date <= PolandTime.Today)
        {
            return ValidationResult.Success;
        }

        var localizer = validationContext.GetRequiredService<IStringLocalizer<SharedResource>>();
        return new ValidationResult(localizer[ErrorMessage!]);
    }
}
