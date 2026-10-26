using System.ComponentModel.DataAnnotations;

namespace StoryCups.Web.Api;

/// <summary>
/// Проверява DataAnnotations на тялото на заявката и връща 400 ValidationProblem.
/// </summary>
internal sealed class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Липсва тяло на заявката.");
        }

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
        {
            var errors = results
                .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty), (r, member) => (member, r.ErrorMessage ?? "Невалидна стойност."))
                .GroupBy(e => e.member, e => e.Item2)
                .ToDictionary(g => g.Key, g => g.ToArray());
            return TypedResults.ValidationProblem(errors, title: "Невалидни данни.");
        }

        return await next(context);
    }
}

internal static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder)
        where T : class => builder.AddEndpointFilter<ValidationFilter<T>>();
}
