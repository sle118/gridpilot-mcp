using System.Text;

namespace ExcelMcp.Deployment.AgentConfig;

internal static class UnifiedTextDiff
{
    public static string Create(string originalText, string updatedText, string originalLabel = "before", string updatedLabel = "after")
    {
        originalText ??= string.Empty;
        updatedText ??= string.Empty;

        if (string.Equals(originalText, updatedText, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var originalLines = SplitLines(originalText);
        var updatedLines = SplitLines(updatedText);
        var lcs = BuildLongestCommonSubsequence(originalLines, updatedLines);
        var diffLines = BuildDiffLines(originalLines, updatedLines, lcs);

        var builder = new StringBuilder();
        builder.Append("--- ").Append(originalLabel).Append('\n');
        builder.Append("+++ ").Append(updatedLabel).Append('\n');
        builder.Append("@@ -1,").Append(originalLines.Length).Append(" +1,").Append(updatedLines.Length).Append(" @@\n");
        foreach (var (kind, text) in diffLines)
        {
            builder.Append(kind).Append(text).Append('\n');
        }

        return builder.ToString();
    }

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    private static int[,] BuildLongestCommonSubsequence(string[] originalLines, string[] updatedLines)
    {
        var lcs = new int[originalLines.Length + 1, updatedLines.Length + 1];
        for (var i = originalLines.Length - 1; i >= 0; i--)
        {
            for (var j = updatedLines.Length - 1; j >= 0; j--)
            {
                lcs[i, j] = string.Equals(originalLines[i], updatedLines[j], StringComparison.Ordinal)
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        return lcs;
    }

    private static IReadOnlyList<(char Kind, string Text)> BuildDiffLines(string[] originalLines, string[] updatedLines, int[,] lcs)
    {
        var lines = new List<(char Kind, string Text)>();
        var i = 0;
        var j = 0;

        while (i < originalLines.Length && j < updatedLines.Length)
        {
            if (string.Equals(originalLines[i], updatedLines[j], StringComparison.Ordinal))
            {
                lines.Add((' ', originalLines[i]));
                i++;
                j++;
            }
            else if (lcs[i + 1, j] >= lcs[i, j + 1])
            {
                lines.Add(('-', originalLines[i]));
                i++;
            }
            else
            {
                lines.Add(('+', updatedLines[j]));
                j++;
            }
        }

        while (i < originalLines.Length)
        {
            lines.Add(('-', originalLines[i]));
            i++;
        }

        while (j < updatedLines.Length)
        {
            lines.Add(('+', updatedLines[j]));
            j++;
        }

        return lines;
    }
}
