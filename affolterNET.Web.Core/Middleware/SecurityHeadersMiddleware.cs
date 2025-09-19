using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using affolterNET.Web.Core.Configuration;

namespace affolterNET.Web.Core.Middleware;

/// <summary>
/// Middleware that adds security headers to HTTP responses for improved security posture.
/// Includes CSP, HSTS, X-Frame-Options, and other security-related headers.
/// </summary>
public class SecurityHeadersMiddleware(
    RequestDelegate next,
    IOptionsMonitor<SecurityHeadersOptions> securityHeadersOptions)
{
    private const string NonceKey = "csp-nonce";

    public async Task Invoke(HttpContext context)
    {
        var options = securityHeadersOptions.CurrentValue;
        if (options.Enabled)
        {
            // Generate nonce for this request
            var nonce = GenerateNonce();
            context.Items[NonceKey] = nonce;

            AddSecurityHeaders(context, options, nonce);
        }

        await next(context);
    }

    private static void AddSecurityHeaders(HttpContext context, SecurityHeadersOptions options, string nonce)
    {
        var headers = context.Response.Headers;

        // Remove server header if configured
        if (options.RemoveServerHeader)
        {
            headers.Remove("Server");
        }

        // X-Frame-Options: Prevent clickjacking
        if (!string.IsNullOrEmpty(options.XFrameOptions))
        {
            headers.Append("X-Frame-Options", options.XFrameOptions);
        }

        // X-Content-Type-Options: Prevent MIME type sniffing
        if (!string.IsNullOrEmpty(options.XContentTypeOptions))
        {
            headers.Append("X-Content-Type-Options", options.XContentTypeOptions);
        }

        // Referrer-Policy: Control referrer information
        if (!string.IsNullOrEmpty(options.ReferrerPolicy))
        {
            headers.Append("Referrer-Policy", options.ReferrerPolicy);
        }

        // Cross-Origin-Opener-Policy: Isolate browsing context
        if (!string.IsNullOrEmpty(options.CrossOriginOpenerPolicy))
        {
            headers.Append("Cross-Origin-Opener-Policy", options.CrossOriginOpenerPolicy);
        }

        // Cross-Origin-Resource-Policy: Control resource sharing
        if (!string.IsNullOrEmpty(options.CrossOriginResourcePolicy))
        {
            headers.Append("Cross-Origin-Resource-Policy", options.CrossOriginResourcePolicy);
        }

        // Cross-Origin-Embedder-Policy: Enable SharedArrayBuffer
        if (!string.IsNullOrEmpty(options.CrossOriginEmbedderPolicy))
        {
            headers.Append("Cross-Origin-Embedder-Policy", options.CrossOriginEmbedderPolicy);
        }

        // Permissions-Policy: Control browser features
        if (!string.IsNullOrEmpty(options.PermissionsPolicy))
        {
            headers.Append("Permissions-Policy", options.PermissionsPolicy);
        }

        // Strict-Transport-Security: Enforce HTTPS
        if (options.EnableHsts && context.Request.IsHttps)
        {
            var hstsValue = $"max-age={options.HstsMaxAge}";
            if (options.HstsIncludeSubDomains)
                hstsValue += "; includeSubDomains";
            if (options.HstsPreload)
                hstsValue += "; preload";
            headers.Append("Strict-Transport-Security", hstsValue);
        }

        // Content-Security-Policy: Comprehensive CSP
        var csp = BuildContentSecurityPolicy(options, nonce);
        
        // Debug logging for CSP (remove in production)
        Console.WriteLine($"[DEBUG] Generated CSP: {csp}");
        Console.WriteLine($"[DEBUG] Custom directives count: {options.CustomCspDirectives.Count}");
        foreach (var (key, value) in options.CustomCspDirectives)
        {
            Console.WriteLine($"[DEBUG] Custom directive: {key} = {value}");
        }
        
        headers.Append("Content-Security-Policy", csp);
    }

    private static string BuildContentSecurityPolicy(SecurityHeadersOptions options, string nonce)
    {
        var directives = new List<string>
        {
            "default-src 'self'",
            "object-src 'none'",
            "base-uri 'self'",
            "frame-ancestors 'none'"
        };

        // Image sources
        var imgSrc = "'self'";
        if (options.AllowDataImages)
            imgSrc += " data:";
        if (options.AllowBlobImages)
            imgSrc += " blob:";
        if (options.AllowedImageSources.Count > 0)
            imgSrc += " " + string.Join(" ", options.AllowedImageSources);
        directives.Add($"img-src {imgSrc}");

        // Font sources
        var fontSrc = "'self'";
        if (options.AllowedFontSources.Count > 0)
            fontSrc += " " + string.Join(" ", options.AllowedFontSources);
        directives.Add($"font-src {fontSrc}");

        // Form actions
        var formAction = "'self'";
        if (!string.IsNullOrEmpty(options.IdpHost))
            formAction += $" {options.IdpHost}";
        directives.Add($"form-action {formAction}");

        // Script sources
        var scriptSrc = "'self'";
        if (options.AllowedScriptSources.Count > 0)
            scriptSrc += " " + string.Join(" ", options.AllowedScriptSources);
        if (!string.IsNullOrEmpty(options.FrontendUrl))
            scriptSrc += $" {options.FrontendUrl}";
        directives.Add($"script-src {scriptSrc}");

        // Style sources
        Console.WriteLine($"[DEBUG] Checking for custom style-src directive...");
        Console.WriteLine($"[DEBUG] CustomCspDirectives contains style-src: {options.CustomCspDirectives.ContainsKey("style-src")}");
        
        if (!options.CustomCspDirectives.ContainsKey("style-src"))
        {
            var styleSrc = "'self'";
            if (options.AllowInlineStyles)
            {
                styleSrc += " 'unsafe-inline'";
                Console.WriteLine($"[DEBUG] Adding 'unsafe-inline' for development");
            }
            
            // Always add style hashes if they exist (for both dev and prod)
            if (options.AllowedStyleHashes.Count > 0)
            {
                // Add specific style hashes (e.g., for Swagger) and 'unsafe-hashes' directive
                styleSrc += " " + string.Join(" ", options.AllowedStyleHashes) + " 'unsafe-hashes'";
                Console.WriteLine($"[DEBUG] Adding style hashes: {string.Join(" ", options.AllowedStyleHashes)}");
            }
            
            if (options.AllowedStyleSources.Count > 0)
                styleSrc += " " + string.Join(" ", options.AllowedStyleSources);
            if (!string.IsNullOrEmpty(options.FrontendUrl))
                styleSrc += $" {options.FrontendUrl}";
            Console.WriteLine($"[DEBUG] Final style-src: {styleSrc}");
            directives.Add($"style-src {styleSrc}");
        }
        else
        {
            Console.WriteLine($"[DEBUG] Using custom style-src directive");
        }

        // Connect sources (for API calls, WebSocket, etc.)
        var connectSrc = "'self'";
        if (options.AllowedConnectSources.Count > 0)
            connectSrc += " " + string.Join(" ", options.AllowedConnectSources);
        if (!string.IsNullOrEmpty(options.IdpHost))
            connectSrc += $" {options.IdpHost}";
        if (!string.IsNullOrEmpty(options.FrontendUrl))
        {
            connectSrc += $" {options.FrontendUrl}";
            // Also add WebSocket variant for Vite HMR
            var wsUrl = options.FrontendUrl.Replace("http://", "ws://").Replace("https://", "wss://");
            if (wsUrl != options.FrontendUrl)
                connectSrc += $" {wsUrl}";
        }
        directives.Add($"connect-src {connectSrc}");

        // Add custom directives
        foreach (var (directive, value) in options.CustomCspDirectives)
        {
            directives.Add($"{directive} {value}");
        }

        return string.Join("; ", directives);
    }

    private static string GenerateNonce()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}

/// <summary>
/// Extension methods for HttpContext to support nonce functionality
/// </summary>
public static class HttpContextSecurityExtensions
{
    private const string NonceKey = "csp-nonce";

    /// <summary>
    /// Gets the CSP nonce for the current request
    /// </summary>
    public static string GetNonce(this HttpContext context)
    {
        return context.Items[NonceKey]?.ToString() ?? string.Empty;
    }
}