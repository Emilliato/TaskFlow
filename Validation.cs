using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow;

// ======================= LAYER 2: FluentValidation =========================
// Data annotations answer "is this field well formed?" — they are attributes
// on a property, so that is all they can see. Real rules are rarely that
// local: they compare two fields, or they need to ask a service something.
//
// A FluentValidation validator is an ordinary class, so it can take
// ITaskService in its constructor and go and look. That is the difference,
// not the syntax.
// ===========================================================================
public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator(ITaskService tasks)
    {
        // Cross-field: a rule about the relationship BETWEEN two properties.
        // No single attribute can express this, because an attribute only ever
        // sees the property it is sitting on.
        RuleFor(request => request.DueDate)
            .NotNull()
            .When(request => request.Priority == "high")
            .WithErrorCode("HIGH_PRIORITY_NEEDS_DUE_DATE")
            .WithMessage("A high-priority task must have a due date.");

        RuleFor(request => request.DueDate)
            .Must(due => due is null || due >= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithErrorCode("DUE_DATE_IN_PAST")
            .WithMessage("Due date must not be in the past.");

        RuleFor(request => request.DueDate)
            .Must(due => due is null || due <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)))
            .WithErrorCode("DUE_DATE_TOO_FAR")
            .WithMessage("Due date is more than 90 days out — split the task instead.");

        // Stateful: this rule needs the injected service. Attributes cannot.
        RuleFor(request => request.Title)
            .Must(title => !tasks.GetAll().Any(existing =>
                string.Equals(existing.Title.Trim(), title.Trim(), StringComparison.OrdinalIgnoreCase)))
            .WithErrorCode("TITLE_ALREADY_EXISTS")
            .WithMessage("A task called '{PropertyValue}' already exists.");
    }
}

// An endpoint filter runs after model binding and before the endpoint. It is
// the natural home for validation: the model is bound, nothing has happened
// yet, and returning a result here means the endpoint never runs at all.
public class FluentValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (model is null || validator is null) return await next(context);

        ValidationResult result = await validator.ValidateAsync(model);
        if (result.IsValid) return await next(context);

        // Same RFC 7807 envelope as the framework's own validation problem —
        // one error shape for the whole API — plus the detail FluentValidation
        // knows and data annotations do not: a stable error CODE per rule.
        var problem = new HttpValidationProblemDetails(result.ToDictionary())
        {
            Type = "https://taskflow.example/errors/business-rules",
            Title = "The request broke a business rule.",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path,
        };
        problem.Extensions["failures"] = result.Errors
            .Select(failure => new
            {
                property = failure.PropertyName,
                code = failure.ErrorCode,
                severity = failure.Severity.ToString(),
                attemptedValue = failure.AttemptedValue,
            })
            .ToArray();
        problem.Extensions["correlationId"] =
            context.HttpContext.Items[CorrelationIdMiddleware.HeaderName] as string;

        return TypedResults.Problem(problem);
    }
}
