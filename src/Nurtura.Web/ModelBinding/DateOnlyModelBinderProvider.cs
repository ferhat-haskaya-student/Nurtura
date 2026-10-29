using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace Nurtura.Web.ModelBinding;

// Registered first in Program.cs, so every DateOnly in the app goes through DateOnlyModelBinder.
public class DateOnlyModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
        context.Metadata.UnderlyingOrModelType == typeof(DateOnly)
            ? new BinderTypeModelBinder(typeof(DateOnlyModelBinder))
            : null;
}
