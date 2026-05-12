namespace ExcelMcp.Deployment.Installation;

public sealed record SetupResumeState(
    SetupOperationKind Operation,
    SetupOptions Options);
