using Microsoft.Extensions.Logging;

namespace CardActions.Infrastructure.Diagnostics;

public static class AssemblyAudit
{
    public static void VerifyAssemblyVersion(string assemblyName, string expectedVersion, ILogger logger)
    {
        var assembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == assemblyName);

        if (assembly == null)
        {
            logger.LogWarning("Assembly {AssemblyName} not found.", assemblyName);
            return;
        }

        var actualVersion = assembly.GetName().Version?.ToString();
        if (actualVersion != expectedVersion)
        {
            logger.LogWarning("Assembly {AssemblyName} version mismatch. Expected: {ExpectedVersion}, Actual: {ActualVersion}",
                assemblyName, expectedVersion, actualVersion);

        }
        else
        {
            logger.LogInformation("Assembly {AssemblyName} version verified: {ActualVersion}", assemblyName, actualVersion);

        }
    }
}