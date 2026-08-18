using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
                var (status, code, message) = exception switch
                {
                    NexaException nexa => (nexa.StatusCode, nexa.Code, nexa.Message),
                    UnauthorizedAccessException => (StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, "Access was denied."),
                    ArgumentException => (StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, exception.Message),
                    _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred.")
                };

                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new ErrorResponse(code, message, correlationId));
            });
        });

        return app;
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
