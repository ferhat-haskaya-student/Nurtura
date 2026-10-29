using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using Nurtura.Web.Localization;

namespace Nurtura.Web.ModelBinding;

// Binds DateOnly and DateOnly? from dd-mm-yyyy only, whatever the language.
// An empty value is left unbound, so the field's own [Required] message is shown.
public class DateOnlyModelBinder : IModelBinder
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public DateOnlyModelBinder(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (result == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);

        var value = result.FirstValue?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return Task.CompletedTask;
        }

        if (DateOnly.TryParseExact(value, DateFormats.Display, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            bindingContext.Result = ModelBindingResult.Success(date);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, _localizer["Validation_DateFormat"]);
        }

        return Task.CompletedTask;
    }
}
