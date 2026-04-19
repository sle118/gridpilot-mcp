namespace ExcelMcp.Core;

public sealed record QueryProbeRequest(
    string TargetQueryName,
    string TempQueryName,
    int MaxRows = 20,
    bool CleanupAfterRun = true,
    bool StopOnError = true);
