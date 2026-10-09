using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ELIBAPI.API.Infrastructure;

public class BoolToIntModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext ctx)
    {
        var vpr = ctx.ValueProvider.GetValue(ctx.ModelName);
        if (vpr == ValueProviderResult.None) return Task.CompletedTask;

        var raw = vpr.FirstValue;
        if (string.IsNullOrEmpty(raw))
        {
            ctx.Result = ModelBindingResult.Success(ctx.ModelType == typeof(int?) ? (int?)null : 0);
            return Task.CompletedTask;
        }
        if (bool.TryParse(raw, out var b))
            ctx.Result = ModelBindingResult.Success(b ? 1 : 0);
        else if (int.TryParse(raw, out var i))
            ctx.Result = ModelBindingResult.Success(i);
        else
            ctx.ModelState.TryAddModelError(ctx.ModelName, $"The value '{raw}' is not valid.");

        return Task.CompletedTask;
    }
}

public class BoolToIntModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext ctx)
        => (ctx.Metadata.ModelType == typeof(int?) || ctx.Metadata.ModelType == typeof(int))
            ? new BoolToIntModelBinder()
            : null;
}
