using System.Reflection;

namespace CardActions.API.Diagnostics;

/// <summary>
/// Runtime assembly diagnostics for troubleshooting version conflicts.
/// ENH-032: Assembly resolution audit utility.
/// </summary>
public static class AssemblyAudit
{
    /// <summary>
    /// Logs all loaded assemblies with versions and locations.
    /// Use at application startup for diagnostic purposes.
    /// </summary>
    public static void LogLoadedAssemblies(ILogger logger)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .OrderBy(a => a.GetName().Name)
            .ToList();

        logger.LogInformation("=== Assembly Audit: {Count} assemblies loaded ===", assemblies.Count);

        foreach (var assembly in assemblies)
        {
            var name = assembly.GetName();
            logger.LogInformation(
                "Assembly: {Name}, Version: {Version}, Location: {Location}",
                name.Name,
                name.Version,
                assembly.IsDynamic ? "<dynamic>" : assembly.Location);
        }

        logger.LogInformation("=== End Assembly Audit ===");
    }

    /// <summary>
    /// Verifies specific assembly version at runtime.
    /// Throws exception if version mismatch detected.
    /// </summary>
    public static void VerifyAssemblyVersion(string assemblyName, string expectedVersion, ILogger logger)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == assemblyName);

        if (assembly == null)
        {
            logger.LogWarning("Assembly {AssemblyName} not loaded", assemblyName);
            return;
        }

        var actualVersion = assembly.GetName().Version?.ToString() ?? "unknown";
        var expected = new Version(expectedVersion);

        if (assembly.GetName().Version != expected)
        {
            var message = $"Assembly version mismatch: {assemblyName} " +
                         $"expected {expectedVersion}, got {actualVersion}";
            logger.LogError(message);
            throw new InvalidOperationException(message);
        }

        logger.LogInformation(
            "Assembly {AssemblyName} version verified: {Version}",
            assemblyName, actualVersion);
    }
}