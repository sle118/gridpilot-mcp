using ExcelMcp.LiveTests.Infrastructure;
using System.Runtime.Versioning;

namespace ExcelMcp.LiveTests;

[SupportedOSPlatform("windows")]
public sealed class LiveCalculationDiagnosticsTests
{
    private const string ScratchSheetName = "Sheet1";

    [LiveExcelFact]
    public async Task RecalculateAsync_ReevaluatesRandFormulaForRangeScope()
    {
        await AssertRandFormulaRecalculationAsync("range", "B20");
    }

    [LiveExcelFact]
    public async Task RecalculateAsync_ReevaluatesRandFormulaForWorksheetScope()
    {
        await AssertRandFormulaRecalculationAsync("worksheet", "B21");
    }

    [LiveExcelFact]
    public async Task RecalculateAsync_ReevaluatesRandFormulaForWorkbookScope()
    {
        await AssertRandFormulaRecalculationAsync("workbook", "B22");
    }

    private static async Task AssertRandFormulaRecalculationAsync(string scope, string address)
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        var formulaWrite = await context.WorkbookService.WriteRangeFormulasAsync(
            context.WorkbookPath,
            new RangeFormulaWriteRequest(
            [
                new RangeFormulaWriteTarget(ScratchSheetName, address, new string?[,] { { "=RAND()" } })
            ]));
        Assert.True(formulaWrite.Succeeded);

        var before = await context.WorkbookService.ReadRangeAsync(context.WorkbookPath, ScratchSheetName, address);
        var initialValue = Convert.ToDouble(before.Values[0][0]);

        var recalculation = await context.WorkbookService.RecalculateAsync(
            context.WorkbookPath,
            scope switch
            {
                "range" => new CalculationRequest(scope, ScratchSheetName, address),
                "worksheet" => new CalculationRequest(scope, ScratchSheetName),
                _ => new CalculationRequest(scope)
            });

        Assert.True(recalculation.Succeeded);
        Assert.Equal(scope, recalculation.Scope);

        var after = await context.WorkbookService.ReadRangeAsync(context.WorkbookPath, ScratchSheetName, address);
        var recalculatedValue = Convert.ToDouble(after.Values[0][0]);
        Assert.NotEqual(initialValue, recalculatedValue);
    }

    [LiveExcelFact]
    public async Task InspectErrorsAsync_FindsFormulaEvaluatedErrors()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        var write = await context.WorkbookService.WriteRangeFormulasAsync(
            context.WorkbookPath,
            new RangeFormulaWriteRequest(
            [
                new RangeFormulaWriteTarget(ScratchSheetName, "C20", new string?[,] { { "=1/0" } })
            ]));
        Assert.True(write.Succeeded);

        var inspection = await context.WorkbookService.InspectErrorsAsync(
            context.WorkbookPath,
            new ErrorInspectionRequest("range", ScratchSheetName, "C20"));

        Assert.True(inspection.Succeeded);
        var hit = Assert.Single(inspection.Hits);
        Assert.True(hit.HasFormula);
        Assert.Equal("formula_error", hit.ValueKind);
        Assert.Equal("#DIV/0!", hit.ErrorCode);
    }

    [LiveExcelFact]
    public async Task InspectErrorsAsync_WorksheetScope_SucceedsForKnownLoadedWorksheet()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        var inspection = await context.WorkbookService.InspectErrorsAsync(
            context.WorkbookPath,
            new ErrorInspectionRequest("worksheet", "tbleWithErrorOnChangedTypeLoade"));

        Assert.True(inspection.Succeeded);
        Assert.NotNull(inspection.Hits);
    }

    [LiveExcelFact]
    public async Task RecalculateAsync_DoesNotImplicitlySaveWorkbook()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        await using (var workbook = await context.Session.OpenWorkbookAsync(context.WorkbookPath))
        {
            await workbook.WriteRangeFormulasAsync("B30", new string?[,] { { "=RAND()" } }, ScratchSheetName);

            var recalculation = await context.WorkbookService.RecalculateAsync(
                context.WorkbookPath,
                new CalculationRequest("range", ScratchSheetName, "B30"));

            Assert.True(recalculation.Succeeded);
        }

        await using var reopenedWorkbook = await context.Session.OpenWorkbookAsync(context.WorkbookPath);
        var formulas = await reopenedWorkbook.ReadRangeFormulasAsync("B30", ScratchSheetName);
        Assert.Null(formulas.Values[0, 0]);
    }
}
