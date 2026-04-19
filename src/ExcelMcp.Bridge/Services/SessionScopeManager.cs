using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;

namespace ExcelMcp.Bridge.Services;

public sealed class SessionScopeManager
{
    private readonly IExcelApplicationHandle _application;
    private readonly object _gate = new();
    private readonly List<ScopeEntry> _scopes = [];

    public SessionScopeManager(IExcelApplicationHandle application)
    {
        _application = application;
    }

    public Task<SessionState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_application.CaptureState());
        }
    }

    public Task<ScopedSessionToken> PushOptionsAsync(SessionOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var token = ScopedSessionToken.New();
            var priorState = _application.CaptureState();
            _application.ApplyOptions(options);
            _scopes.Add(new ScopeEntry(token, priorState));
            return Task.FromResult(token);
        }
    }

    public Task PopOptionsAsync(ScopedSessionToken token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_scopes.Count == 0)
            {
                throw new InvalidOperationException("No scoped session state is active.");
            }

            var scope = _scopes[^1];
            if (scope.Token != token)
            {
                throw new InvalidOperationException("Scoped session state must be restored in LIFO order.");
            }

            _scopes.RemoveAt(_scopes.Count - 1);
            _application.RestoreState(scope.State);
            return Task.CompletedTask;
        }
    }

    private sealed record ScopeEntry(ScopedSessionToken Token, SessionState State);
}
