using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Nurtura.Web.Localization;

// ASP.NET's own messages about a value that does not fit a field, taken from Resources/en.json and pl.json. Short and
// without the field's name or the value, because they appear under the field. Each text is looked up when the message
// is made, so it follows the language of the request.
public class ModelBindingMessages : IConfigureOptions<MvcOptions>
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ModelBindingMessages(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    public void Configure(MvcOptions options)
    {
        var messages = options.ModelBindingMessageProvider;

        messages.SetValueMustBeANumberAccessor(_ => Number);
        messages.SetNonPropertyValueMustBeANumberAccessor(() => Number);

        messages.SetAttemptedValueIsInvalidAccessor((_, _) => ValueInvalid);
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => ValueInvalid);
        messages.SetUnknownValueIsInvalidAccessor(_ => ValueInvalid);
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => ValueInvalid);
        messages.SetValueIsInvalidAccessor(_ => ValueInvalid);

        messages.SetMissingBindRequiredValueAccessor(_ => ValueMissing);
        messages.SetMissingKeyOrValueAccessor(() => ValueMissing);
        messages.SetMissingRequestBodyRequiredValueAccessor(() => ValueMissing);
        messages.SetValueMustNotBeNullAccessor(_ => ValueMissing);
    }

    private string Number => _localizer["Validation_Number"];

    private string ValueInvalid => _localizer["Validation_ValueInvalid"];

    private string ValueMissing => _localizer["Validation_ValueMissing"];
}
