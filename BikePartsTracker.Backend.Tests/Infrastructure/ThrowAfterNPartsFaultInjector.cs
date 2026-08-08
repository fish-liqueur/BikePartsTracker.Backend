namespace BikePartsTracker.Backend.Tests.Infrastructure;

/// <summary>
/// Test double for ADR 0010 BE-09 — throws after N part creates inside fill-empty-slots.
/// </summary>
public sealed class ThrowAfterNPartsFaultInjector : BikePartsTracker.Services.IFillEmptySlotsFaultInjector
{
    private int _throwAfterCount = int.MaxValue;

    public void ThrowAfter(int createdCount) => _throwAfterCount = createdCount;

    public void Reset() => _throwAfterCount = int.MaxValue;

    public Task OnAfterPartAddedAsync(int createdSoFar, CancellationToken cancellationToken = default)
    {
        if (createdSoFar >= _throwAfterCount)
            throw new InvalidOperationException("Injected fill-empty-slots fault for rollback test.");
        return Task.CompletedTask;
    }
}
