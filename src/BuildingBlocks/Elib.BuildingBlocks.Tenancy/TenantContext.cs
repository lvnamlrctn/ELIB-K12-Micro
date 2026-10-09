namespace Elib.BuildingBlocks.Tenancy;

/// <inheritdoc cref="ITenantContext"/>
public sealed class TenantContext : ITenantContext
{
    private State _state = State.Unresolved;

    public long? TenantId => _state.TenantId;
    public IReadOnlyList<long>? ReadScope => _state.ReadScope;
    public IReadOnlyList<long> AllowedReadScope { get; private set; } = [];
    public bool IsSystem => _state.IsSystem;

    public long RequireTenantId() => TenantId ?? throw new TenantRequiredException();

    /// <summary>Gán ngữ cảnh ban đầu của request — chỉ middleware gọi, một lần.</summary>
    public void Initialize(long? tenantId, bool isSystem, IReadOnlyList<long>? allowedReadScope)
    {
        _state = new State(tenantId, null, isSystem && tenantId is null);
        AllowedReadScope = allowedReadScope ?? [];
    }

    public IDisposable Use(long tenantId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenantId);
        return Push(new State(tenantId, null, false));
    }

    public IDisposable UseSystem() => Push(new State(null, null, true));

    public IDisposable ReadAcross(IReadOnlyList<long> scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Count == 0) throw new ArgumentException("Phạm vi đọc chéo không được rỗng.", nameof(scope));
        if (!IsSystem)
        {
            var denied = scope.Where(id => id != TenantId && !AllowedReadScope.Contains(id)).ToArray();
            if (denied.Length > 0)
                throw new TenantAccessDeniedException($"Không có quyền đọc dữ liệu đơn vị: {string.Join(", ", denied)}.");
        }
        return Push(_state with { ReadScope = scope.Distinct().ToArray() });
    }

    private Restore Push(State next)
    {
        var previous = _state;
        _state = next;
        return new Restore(this, previous);
    }

    private sealed record State(long? TenantId, IReadOnlyList<long>? ReadScope, bool IsSystem)
    {
        public static readonly State Unresolved = new(null, null, false);
    }

    private sealed class Restore(TenantContext owner, State previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner._state = previous;
        }
    }
}
