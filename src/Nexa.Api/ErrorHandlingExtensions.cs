using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexa.Application.Common;
using Nexa.Contracts;

namespace Nexa.Api;

public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-ID";
}

public static class ErrorHandlingExtensions
{
    public static IApplicationBuilder UseNexaExceptionHandler(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var correlationId = context.TraceIdentifier;
                context.Response.Headers[CorrelationId.HeaderName] = correlationId;

                var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                var exception = feature?.Error;
                var logger = context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Nexa.ExceptionHandler");
                logger?.LogError(exception, "Unhandled exception {CorrelationId} for {Path}", correlationId, context.Request.Path);

                var (status, code, message) = exception switch
                {
                    NexaException nexa => (nexa.StatusCode, nexa.Code, nexa.Message),
                    UnauthorizedAccessException => (StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, "Access was denied."),
                    ArgumentException => (StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, exception.Message),
                    _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred.")
                };

                var environment = context.RequestServices.GetService<IWebHostEnvironment>();
                if (environment?.IsDevelopment() == true && exception is not null && code == "INTERNAL_ERROR")
                {
                    message = exception.Message;
                }

                context.Response.StatusCode = status;
                var wantsJson = context.Request.Path.StartsWithSegments("/api")
                    || AcceptsJson(context);
                if (wantsJson)
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new ErrorResponse(code, message, correlationId));
                    return;
                }

                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync(message);
            });
        });

        return app;
    }

    private static bool AcceptsJson(HttpContext context)
    {
        var accept = context.Request.Headers.Accept.ToString();
        return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }

    public static IApplicationBuilder UseNexaCorrelationId(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var incoming = context.Request.Headers[CorrelationId.HeaderName].ToString();
            if (string.IsNullOrWhiteSpace(incoming))
            {
                incoming = Activity.Current?.Id ?? context.TraceIdentifier;
            }

            context.TraceIdentifier = incoming;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationId.HeaderName] = incoming;
                return Task.CompletedTask;
            });

            await next();
        });

        return app;
    }
}
