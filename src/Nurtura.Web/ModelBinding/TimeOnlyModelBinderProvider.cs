using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace Nurtura.Web.ModelBinding;

// Registered first in Program.cs, so every TimeOnly in the app goes through TimeOnlyModelBinder.
public class TimeOnlyModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
        context.Metadata.UnderlyingOrModelType == typeof(TimeOnly)
            ? new BinderTypeModelBinder(typeof(TimeOnlyModelBinder))
            : null;
}
