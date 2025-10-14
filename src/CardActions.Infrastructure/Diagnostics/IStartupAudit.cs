namespace CardActions.Infrastructure.Diagnostics;

public interface IStartupAudit
{
    void VerifyAssemblyVersion(string assemblyName, string expectedVersion);
}