using Microsoft.AspNetCore.Mvc;

namespace CardActions.API.Extensions;

/// <summary>
/// Extension methods for ControllerBase to provide consistent error response formatting.
/// </summary>
public static class ControllerBaseExtensions
{
    /// <summary>
    /// Creates a ProblemDetails response with automatic correlation ID injection.
    /// </summary>
    /// <param name="controller">The controller instance</param>
    /// <param name="title">Error title</param>
    /// <param name="detail">Error detail message</param>
    /// <param name="statusCode">HTTP status code</param>
    /// <param name="correlationId">Correlation ID for tracing</param>
    /// <param name="type">RFC reference for error type</param>
    /// <returns>ObjectResult with ProblemDetails</returns>
    public static ObjectResult ProblemWithCorrelationId(
        this ControllerBase controller,
        string title,
        string detail,
        int statusCode,
        string correlationId,
        string? type = null)
    {
        var problemDetails = new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = statusCode,
            Instance = controller.HttpContext.Request.Path,
            Type = type ?? GetDefaultTypeForStatusCode(statusCode)
        };

        problemDetails.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode
        };
    }

    /// <summary>
    /// Creates a ValidationProblemDetails response with automatic traceId injection.
    /// </summary>
    /// <param name="controller">The controller instance</param>
    /// <param name="errors">Validation errors dictionary</param>
    /// <returns>BadRequestObjectResult with ValidationProblemDetails</returns>
    public static BadRequestObjectResult ValidationProblemWithTraceId(
        this ControllerBase controller,
        IDictionary<string, string[]> errors)
    {
        var problemDetails = new ValidationProblemDetails(errors)
        {
            Title = "One or more validation errors occurred",
            Status = StatusCodes.Status400BadRequest,
            Instance = controller.HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };

        problemDetails.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problemDetails);
    }

    /// <summary>
    /// Gets the default RFC 7231 type URI for a given HTTP status code.
    /// </summary>
    private static string? GetDefaultTypeForStatusCode(int statusCode)
    {
        return statusCode switch
        {
            400 => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            403 => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            404 => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            500 => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            _ => null
        };
    }
}