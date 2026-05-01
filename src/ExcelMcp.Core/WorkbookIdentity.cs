namespace ExcelMcp.Core;

public static class WorkbookIdentity
{
    public static string Normalize(string workbookPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);

        var trimmed = workbookPath.Trim();
        if (TryNormalizeUriLikePath(trimmed, out var normalizedUriLikePath))
        {
            return normalizedUriLikePath;
        }

        return Path.GetFullPath(trimmed);
    }

    private static bool TryNormalizeUriLikePath(string workbookPath, out string normalizedPath)
    {
        normalizedPath = string.Empty;

        if (!LooksLikeUriLikeWorkbookPath(workbookPath, out var scheme, out var remainder))
        {
            return false;
        }

        var normalizedRemainder = remainder.Replace('\\', '/');
        if ((string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) &&
            normalizedRemainder.StartsWith("/", StringComparison.Ordinal) &&
            !normalizedRemainder.StartsWith("//", StringComparison.Ordinal))
        {
            normalizedRemainder = "/" + normalizedRemainder;
        }

        var candidate = $"{scheme.ToLowerInvariant()}:{normalizedRemainder}";
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile)
            {
                var localPath = uri.IsLoopback
                    ? Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/')
                    : uri.LocalPath;
                normalizedPath = Path.GetFullPath(localPath);
                return true;
            }

            normalizedPath = uri.AbsoluteUri;
            return true;
        }

        normalizedPath = candidate;
        return true;
    }

    private static bool LooksLikeUriLikeWorkbookPath(string workbookPath, out string scheme, out string remainder)
    {
        scheme = string.Empty;
        remainder = string.Empty;

        var separatorIndex = workbookPath.IndexOf(':');
        if (separatorIndex <= 1)
        {
            return false;
        }

        scheme = workbookPath[..separatorIndex];
        remainder = workbookPath[(separatorIndex + 1)..];

        if (!scheme.All(ch => char.IsLetterOrDigit(ch) || ch is '+' or '-' or '.'))
        {
            return false;
        }

        return true;
    }
}
