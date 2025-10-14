using Microsoft.Extensions.Logging;

namespace CardActions.Infrastructure.Diagnostics;

public class StartupAudit(ILogger<StartupAudit> logger) : IStartupAudit
{
    private readonly ILogger<StartupAudit> _logger = logger;

    public void VerifyAssemblyVersion(string assemblyName, string expectedVersion)
    {
        AssemblyAudit.VerifyAssemblyVersion(assemblyName, expectedVersion, _logger);
    }
}