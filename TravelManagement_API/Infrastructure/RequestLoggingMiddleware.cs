using System.Diagnostics;

namespace TravelManagement.API.Infrastructure
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var request = context.Request;

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                var statusCode = context.Response.StatusCode;
                var elapsed = stopwatch.ElapsedMilliseconds;

                var logLevel = statusCode >= 500 ? LogLevel.Error
                    : statusCode >= 400 ? LogLevel.Warning
                    : LogLevel.Information;

                _logger.Log(logLevel,
                    "{Method} {Path}{Query} → {StatusCode} in {Elapsed}ms",
                    request.Method,
                    request.Path,
                    request.QueryString,
                    statusCode,
                    elapsed);
            }
        }
    }

    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app) =>
            app.UseMiddleware<RequestLoggingMiddleware>();
    }
}