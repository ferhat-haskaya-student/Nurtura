using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using Nurtura.Web.Localization;

namespace Nurtura.Web.ModelBinding;

// Binds TimeOnly and TimeOnly? from HH:mm (24-hour clock) only, whatever the language.
// An empty value is left unbound, so the field's own [Required] message is shown.
public class TimeOnlyModelBinder : IModelBinder
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public TimeOnlyModelBinder(IStringLocalizer<SharedResource> localizer)
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

        if (TimeOnly.TryParseExact(value, TimeFormats.Display, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            bindingContext.Result = ModelBindingResult.Success(time);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, _localizer["Validation_TimeFormat"]);
        }

        return Task.CompletedTask;
    }
}
