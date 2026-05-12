namespace ExcelMcp.Deployment.AgentConfig;

public sealed class VsCodeUserMcpConfigPathLocator
{
    public string ResolvePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "Code", "User", "mcp.json");
    }
}
