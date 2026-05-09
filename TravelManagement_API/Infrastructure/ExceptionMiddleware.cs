using System.Net;
using System.Text.Json;
using TravelManagement.Core.Common;

namespace TravelManagement.API.Infrastructure
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var (statusCode, prodMessage) = ex switch
            {
                KeyNotFoundException        => (HttpStatusCode.NotFound,            ex.Message),
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized,        ex.Message),
                InvalidOperationException   => (HttpStatusCode.BadRequest,          ex.Message),
                ArgumentNullException       => (HttpStatusCode.BadRequest,          ex.Message),
                ArgumentException           => (HttpStatusCode.BadRequest,          ex.Message),
                _                           => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later.")
            };

            // Always log the full chain so server logs always show the real cause
            Console.WriteLine($"[ExceptionMiddleware] ERROR: {ex}");
            Console.WriteLine($"[ExceptionMiddleware] INNER: {ex.InnerException}");

            _logger.LogError(ex,
                "Unhandled exception on {Method} {Path} — {StatusCode}: {Message}",
                context.Request.Method,
                context.Request.Path,
                (int)statusCode,
                ex.Message);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode  = (int)statusCode;

            string message;
            IEnumerable<string>? errors;

            if (_env.IsDevelopment())
            {
                // In Development: expose the real exception chain so the frontend error toast shows the cause
                message = ex.Message;
                errors  = new[]
                {
                    ex.InnerException?.Message    ?? "",
                    ex.InnerException?.InnerException?.Message ?? "",
                    ex.StackTrace                 ?? "",
                }.Where(s => !string.IsNullOrEmpty(s));
            }
            else
            {
                message = prodMessage;
                errors  = null;
            }

            var response = ApiResponse.Fail(message, errors);

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }

    public static class ExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app) =>
            app.UseMiddleware<ExceptionMiddleware>();
    }
}