using MessengerWebhook.Services;

namespace MessengerWebhook.Middleware;

/// <summary>
/// Middleware to validate HMAC-SHA256 signatures on webhook POST requests
/// </summary>
public class SignatureValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ISignatureValidator _validator;
    private readonly ILogger<SignatureValidationMiddleware> _logger;

    public SignatureValidationMiddleware(
        RequestDelegate next,
        ISignatureValidator validator,
        ILogger<SignatureValidationMiddleware> logger)
    {
        _next = next;
        _validator = validator;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only validate POST requests to /webhook
        if (context.Request.Method == "POST" && context.Request.Path == "/webhook")
        {
            // Check request size limit (10MB max to prevent memory exhaustion)
            const long maxRequestSize = 10 * 1024 * 1024;
            if (context.Request.ContentLength > maxRequestSize)
            {
                _logger.LogWarning("Request body too large: {Size} bytes", context.Request.ContentLength);
                context.Response.StatusCode = 413;
                await context.Response.WriteAsync("Request body too large");
                return;
            }

            // Enable buffering to allow reading the body multiple times
            context.Request.EnableBuffering();

            // Read raw body
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0; // Reset for next middleware

            // Extract signature header
            var signature = context.Request.Headers["X-Hub-Signature-256"].FirstOrDefault();

            if (string.IsNullOrEmpty(signature))
            {
                _logger.LogWarning("Missing X-Hub-Signature-256 header");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Missing signature");
                return;
            }

            // Validate signature
            if (!await _validator.ValidateAsync(rawBody, signature))
            {
                _logger.LogWarning("Invalid signature from {RemoteIp}", context.Connection.RemoteIpAddress);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid signature");
                return;
            }

            _logger.LogDebug("Signature validated successfully");
        }

        await _next(context);
    }
}
