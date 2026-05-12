using ExcelMcp.Deployment.Installation;

namespace GridPilot.Setup;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var resumeState = ParseResumeState(args);
        Application.Run(new SetupForm(resumeState));
    }

    private static SetupResumeState? ParseResumeState(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            if (string.Equals(args[index], "--resume", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Count &&
                !string.IsNullOrWhiteSpace(args[index + 1]))
            {
                return SetupResumeCodec.Decode(args[index + 1]);
            }
        }

        return null;
    }
}
