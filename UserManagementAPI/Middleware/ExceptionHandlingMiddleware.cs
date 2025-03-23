using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Pass the request to the next middleware in the pipeline
            await _next(context);
        }
        catch (Exception ex)
        {
            // Log the exception
            _logger.LogError(ex, "An unhandled exception occurred.");

            // Handle the exception by returning a consistent error response
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Generate a unique request ID for tracking purposes
        var requestId = Guid.NewGuid().ToString();

        // Create an extended error response
        var response = new
        {
            error = "Internal server error.",
            errorCode = "500", // A custom error code, you can extend for specific exceptions
            requestId,         // Unique request ID
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), // Timestamp for when the error occurred
            details = exception.Message // Include the exception message for debugging (optional, can be removed in production)
        };

        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/json";

        // Serialize the response to JSON
        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
