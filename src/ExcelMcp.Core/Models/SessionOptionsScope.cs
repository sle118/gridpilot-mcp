using ExcelMcp.Core.Abstractions;

namespace ExcelMcp.Core;

public sealed class SessionOptionsScope : IAsyncDisposable
{
    private readonly IExcelSession _session;
    private readonly ScopedSessionToken _token;
    private int _disposed;

    public SessionOptionsScope(IExcelSession session, ScopedSessionToken token)
    {
        _session = session;
        _token = token;
    }

    public ScopedSessionToken Token => _token;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _session.PopOptionsAsync(_token).ConfigureAwait(false);
    }
}
