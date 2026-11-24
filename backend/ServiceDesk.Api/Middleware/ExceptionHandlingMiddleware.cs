using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Common;

namespace ServiceDesk.Api.Middleware;

/// <summary>
/// Global exception handler: turns exceptions into consistent RFC 7807 ProblemDetails responses
/// and guarantees stack traces / internal details never reach the client.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (status, title, detail) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", ex.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", ex.Message),
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Authentication failed", ex.Message),
            BusinessRuleException => (StatusCodes.Status400BadRequest, "Business rule violation", ex.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict",
                "Someone else changed this record while you were editing. Reload it and try again."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred",
                "Something went wrong on our side. Please try again later.")
        };

        if (status >= 500)
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            logger.LogWarning("{ExceptionType} on {Method} {Path}: {Message}",
                ex.GetType().Name, context.Request.Method, context.Request.Path, ex.Message);

        if (context.Response.HasStarted)
        {
            logger.LogWarning("Response already started; cannot write the error body.");
            return;
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
