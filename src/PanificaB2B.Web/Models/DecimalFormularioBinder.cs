using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace PanificaB2B.Web.Models;
public sealed class DecimalFormularioBinder : IModelBinder, IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context) => (Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType) == typeof(decimal) ? this : null;
    public Task BindModelAsync(ModelBindingContext context)
    {
        var valor = context.ValueProvider.GetValue(context.ModelName);
        if (valor == ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, valor);
        var texto = valor.FirstValue?.Trim();
        if (string.IsNullOrEmpty(texto) && context.ModelMetadata.IsNullableValueType) context.Result = ModelBindingResult.Success(null);
        else if (decimal.TryParse(texto?.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numero)) context.Result = ModelBindingResult.Success(numero);
        else context.ModelState.TryAddModelError(context.ModelName, "Informe um número válido, sem separador de milhares.");
        return Task.CompletedTask;
    }
}
