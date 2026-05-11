namespace GridPilot.Tray;

internal static class Program
{
    private const string MutexName = "Local\\GridPilot.Tray";

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var context = new TrayApplicationContext(TrayProfileContext.Resolve(args));
        Application.Run(context);
    }
}
