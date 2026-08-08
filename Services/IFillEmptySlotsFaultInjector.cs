namespace BikePartsTracker.Services
{
    /// <summary>
    /// Test seam for ADR 0010 all-or-nothing rollback (QA BE-09). Production is a no-op.
    /// </summary>
    public interface IFillEmptySlotsFaultInjector
    {
        /// <summary>
        /// Called after each new chain part is added to the context (before commit).
        /// </summary>
        Task OnAfterPartAddedAsync(int createdSoFar, CancellationToken cancellationToken = default);
    }

    public sealed class NullFillEmptySlotsFaultInjector : IFillEmptySlotsFaultInjector
    {
        public Task OnAfterPartAddedAsync(int createdSoFar, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
