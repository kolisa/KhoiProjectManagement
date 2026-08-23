using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KhoiProjectManagementApi.Filters
{
    // Runs every action argument through its registered FluentValidation IValidator<T> (if one exists)
    // before the action executes, short-circuiting with 400 + field-level errors on failure - the same
    // spot ASP.NET Core's own [ApiController] ModelState validation would run, just backed by explicit
    // FluentValidation rules instead of DataAnnotations. An argument with no registered validator (most
    // response/query DTOs - see Application/Validators/) passes through untouched. Registered globally
    // in ServiceCollectionExtensions rather than added per-controller, so a new validator takes effect
    // the moment it's registered with no controller changes needed.
    public class ValidationActionFilter : IAsyncActionFilter
    {
        private readonly IServiceProvider _serviceProvider;

        public ValidationActionFilter(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            foreach (var (name, argument) in context.ActionArguments)
            {
                if (argument == null)
                    continue;

                // A handful of endpoints (SetPermissions, SetWidgetAllowlist, SetPreferences, ...) take
                // List<TDto> directly rather than one object - validate each element against IValidator<TDto>
                // in that case, since there's no IValidator<List<TDto>> to look up.
                if (argument is System.Collections.IEnumerable enumerable && argument is not string)
                {
                    var index = 0;
                    foreach (var item in enumerable)
                    {
                        if (item == null) { index++; continue; }
                        var itemResult = await TryValidateAsync(item);
                        if (itemResult is { IsValid: false })
                        {
                            context.Result = BadRequest(itemResult, $"{name}[{index}]");
                            return;
                        }
                        index++;
                    }
                    continue;
                }

                var result = await TryValidateAsync(argument);
                if (result is { IsValid: false })
                {
                    context.Result = BadRequest(result, name);
                    return;
                }
            }

            await next();
        }

        private async Task<ValidationResult?> TryValidateAsync(object argument)
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_serviceProvider.GetService(validatorType) is not IValidator validator)
                return null;

            return await validator.ValidateAsync(new ValidationContext<object>(argument));
        }

        private static Microsoft.AspNetCore.Mvc.BadRequestObjectResult BadRequest(ValidationResult result, string argumentName)
        {
            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => $"{argumentName}.{g.Key}", g => g.Select(e => e.ErrorMessage).ToArray());

            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { errors });
        }
    }
}
