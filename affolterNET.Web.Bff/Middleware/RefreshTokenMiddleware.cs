using affolterNET.Web.Bff.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace affolterNET.Web.Bff.Middleware;

/// <summary>
/// Middleware that automatically refreshes authentication tokens when they are expired or about to expire
/// This is BFF-specific middleware as it handles cookie authentication and OIDC sign-out
/// </summary>
public class RefreshTokenMiddleware(
    RequestDelegate next,
    ILogger<RefreshTokenMiddleware> logger,
    string oidcScheme = "OpenIdConnect")
{
    /// <summary>
    /// Processes the HTTP request and refreshes tokens if necessary
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <param name="tokenRefreshService">The token refresh service (injected per request)</param>
    public async Task InvokeAsync(HttpContext context, TokenRefreshService tokenRefreshService)
    {
        // If the user is not authenticated or no access token is present, skip to next middleware
        var tokenExpiresAt = await tokenRefreshService.ExpiresAt(context);
        if (context.User.Identity?.IsAuthenticated != true || tokenExpiresAt == null)
        {
            await next(context);
            return;
        }

        if (await tokenRefreshService.IsExpired(context))
        {
            logger.LogDebug("Access token is expired, attempting to refresh");
            var result = await tokenRefreshService.RefreshTokensAsync(context);
            if (result)
            {
                logger.LogDebug("Tokens have been refreshed successfully");
                await next(context);
                return;
            }

            // Refresh failed (e.g., expired refresh token). Sign the user out to force re-authentication.
            logger.LogWarning("Token refresh failed, signing user out to force re-authentication");
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignOutAsync(oidcScheme);
            // After sign-out, short-circuit the pipeline to avoid using an invalid principal
            return;
        }
 
        // Continue to the next middleware in the pipeline
        await next(context);
    }
}