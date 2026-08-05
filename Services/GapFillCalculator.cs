namespace BikePartsTracker.Services
{
    /// <summary>
    /// Pure math for ADR-0001 auto-backfill: 30-day absolute lookback + gap-only vs contiguous watermark.
    /// </summary>
    public static class GapFillCalculator
    {
        public const int LookbackDays = 30;

        public readonly record struct DateRange(DateTime From, DateTime To);

        /// <summary>
        /// Computes missing UTC date ranges to fetch for a past-dated usage period open.
        /// Returns an empty list when nothing should be fetched.
        /// </summary>
        public static IReadOnlyList<DateRange> ComputeMissingRanges(
            DateTime periodStartDate,
            DateTime utcToday,
            DateTime? coveredFrom,
            DateTime? coveredTo)
        {
            var today = utcToday.Date;
            var floor = today.AddDays(-LookbackDays);
            var needFrom = periodStartDate.Date > floor ? periodStartDate.Date : floor;
            var needTo = today;

            if (needFrom > needTo)
            {
                return Array.Empty<DateRange>();
            }

            if (!coveredFrom.HasValue || !coveredTo.HasValue)
            {
                return new[] { new DateRange(needFrom, needTo) };
            }

            var covFrom = coveredFrom.Value.Date;
            var covTo = coveredTo.Value.Date;
            if (covFrom > covTo)
            {
                return new[] { new DateRange(needFrom, needTo) };
            }

            var gaps = new List<DateRange>();

            // Older slice before contiguous coverage
            if (needFrom < covFrom)
            {
                var gapTo = Min(needTo, covFrom.AddDays(-1));
                if (needFrom <= gapTo)
                {
                    gaps.Add(new DateRange(needFrom, gapTo));
                }
            }

            // Newer slice after contiguous coverage
            if (needTo > covTo)
            {
                var gapFrom = Max(needFrom, covTo.AddDays(1));
                if (gapFrom <= needTo)
                {
                    gaps.Add(new DateRange(gapFrom, needTo));
                }
            }

            return gaps;
        }

        /// <summary>
        /// Expands a contiguous watermark to include a successfully fetched range.
        /// </summary>
        public static (DateTime From, DateTime To) ExpandWatermark(
            DateTime? coveredFrom,
            DateTime? coveredTo,
            DateTime fetchedFrom,
            DateTime fetchedTo)
        {
            var from = fetchedFrom.Date;
            var to = fetchedTo.Date;
            if (from > to)
            {
                (from, to) = (to, from);
            }

            if (!coveredFrom.HasValue || !coveredTo.HasValue)
            {
                return (from, to);
            }

            return (Min(coveredFrom.Value.Date, from), Max(coveredTo.Value.Date, to));
        }

        /// <summary>
        /// Expands the contiguous watermark for a single auto-imported day (webhook upsert)
        /// only when the day is empty-seed, already inside, or adjacent. Non-adjacent days
        /// are left alone so holes are not falsely marked covered.
        /// </summary>
        /// <returns>True when the watermark bounds changed.</returns>
        public static bool TryExpandWatermarkForDay(
            DateTime? coveredFrom,
            DateTime? coveredTo,
            DateTime activityDay,
            out DateTime newFrom,
            out DateTime newTo)
        {
            var day = activityDay.Date;

            if (!coveredFrom.HasValue || !coveredTo.HasValue)
            {
                newFrom = day;
                newTo = day;
                return true;
            }

            var from = coveredFrom.Value.Date;
            var to = coveredTo.Value.Date;
            if (from > to)
            {
                (from, to) = (to, from);
            }

            if (day >= from && day <= to)
            {
                newFrom = from;
                newTo = to;
                return false;
            }

            if (day == from.AddDays(-1))
            {
                newFrom = day;
                newTo = to;
                return true;
            }

            if (day == to.AddDays(1))
            {
                newFrom = from;
                newTo = day;
                return true;
            }

            newFrom = from;
            newTo = to;
            return false;
        }

        private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;
        private static DateTime Max(DateTime a, DateTime b) => a >= b ? a : b;
    }
}
