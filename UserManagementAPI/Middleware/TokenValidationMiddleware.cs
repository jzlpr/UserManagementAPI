using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace UserManagementAPI.Middleware
{

    public class TokenValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenValidationMiddleware> _logger;

        public TokenValidationMiddleware(RequestDelegate next, ILogger<TokenValidationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip token validation for specific routes
            if (context.Request.Path.Equals("/login", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context); // Allow the request to continue
                return;
            }

            try
            {
                // Validate the Authorization header and token
                var authorizationHeader = context.Request.Headers["Authorization"].ToString();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    _logger.LogWarning("Authorization header missing or invalid.");
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Unauthorized: Missing or invalid token.");
                    return;
                }

                var token = authorizationHeader.Substring("Bearer ".Length).Trim();
                if (!ValidateToken(token))
                {
                    _logger.LogWarning("Token validation failed.");
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Unauthorized: Invalid token.");
                    return;
                }

                await _next(context); // Pass request to the next middleware if token is valid
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during token validation.");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("Internal server error.");
            }
        }

        private bool ValidateToken(string token)
        {
            try
            {
                var jwtTokenHandler = new JwtSecurityTokenHandler();
                if (!jwtTokenHandler.CanReadToken(token))
                    return false;

                var jwtToken = jwtTokenHandler.ReadJwtToken(token);
                if (jwtToken.ValidTo < DateTime.UtcNow)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
