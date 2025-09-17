using affolterNET.Web.Core.Models;
using affolterNET.Web.Core.Options;
using Microsoft.Extensions.Configuration;

namespace affolterNET.Web.Core.Configuration;

/// <summary>
/// Permission caching configuration options
/// </summary>
public class PermissionCacheOptions: IConfigurableOptions<PermissionCacheOptions>
{
    /// <summary>
    /// Configuration section name for binding from appsettings.json
    /// </summary>
    public static string SectionName => "affolterNET:Web:PermissionCache";

    public static PermissionCacheOptions CreateDefaults(AppSettings settings)
    {
        return new PermissionCacheOptions(settings);
    }

    public void CopyTo(PermissionCacheOptions target)
    {
        target.DefaultExpiration = DefaultExpiration;
        target.MaxCacheSize = MaxCacheSize;
        target.PermissionCacheExpiration = PermissionCacheExpiration;
    }

    /// <summary>
    /// Parameterless constructor for options pattern compatibility
    /// </summary>
    public PermissionCacheOptions() : this(new AppSettings())
    {
    }
    
    /// <summary>
    /// Constructor with settings parameter for meaningful defaults
    /// </summary>
    /// <param name="settings">Application settings containing development mode and authentication mode</param>
    private PermissionCacheOptions(AppSettings settings)
    {
        DefaultExpiration = settings.IsDev ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(15); // Shorter cache in development
        PermissionCacheExpiration = settings.IsDev ? TimeSpan.FromMinutes(3) : TimeSpan.FromMinutes(10); // Shorter permission cache in development
        MaxCacheSize = settings.IsDev ? 100 : 1000; // Smaller cache size in development
    }

    /// <summary>
    /// Default cache expiration time
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; }
    
    /// <summary>
    /// Permission cache expiration time
    /// </summary>
    public TimeSpan PermissionCacheExpiration { get; set; }
    
    /// <summary>
    /// Maximum number of cached items
    /// </summary>
    public int MaxCacheSize { get; set; }
}