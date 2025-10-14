using Microsoft.AspNetCore.Mvc;

namespace CardActions.API.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        var problemDetails = CreateProblemDetails(context, exception);

        _logger.LogError(exception, "Unhandled exception occurred. TraceId: {TraceId}", traceId);

        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        var problemDetails = new ProblemDetails
        {
            Title = "An unexpected error occurred",
            Status = StatusCodes.Status500InternalServerError,
            Instance = context.Request.Path,
            Extensions = { ["traceId"] = context.TraceIdentifier }
        };

        switch (exception)
        {
            case ArgumentException argEx:
                problemDetails.Title = "Invalid request";
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Detail = argEx.Message;
                break;

            case KeyNotFoundException keyEx:
                problemDetails.Title = "Resource not found";
                problemDetails.Status = StatusCodes.Status404NotFound;
                problemDetails.Detail = keyEx.Message;
                break;

            case UnauthorizedAccessException authEx:
                problemDetails.Title = "Access denied";
                problemDetails.Status = StatusCodes.Status403Forbidden;
                problemDetails.Detail = authEx.Message;
                break;

            default:
                problemDetails.Detail = _environment.IsDevelopment()
                    ? exception.ToString()
                    : "Please try again later. If the problem persists, contact support.";
                break;
        }

        return problemDetails;
    }
}