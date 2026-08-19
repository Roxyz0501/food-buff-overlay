using System;

namespace FoodBuffOverlay
{
    internal enum FoodBuffCondition { Unavailable, Active, Expiring, Missing }

    internal sealed class FoodBuffState
    {
        public FoodBuffCondition Condition { get; set; }
        public TimeSpan Remaining { get; set; }
        public DateTime SampledAtUtc { get; set; } = DateTime.UtcNow;

        public static FoodBuffState Unavailable() => new FoodBuffState { Condition = FoodBuffCondition.Unavailable };
        public static FoodBuffState Missing() => new FoodBuffState { Condition = FoodBuffCondition.Missing };
        public static FoodBuffState FromRemaining(TimeSpan remaining, int warningMinutes)
        {
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            return new FoodBuffState
            {
                Remaining = remaining,
                SampledAtUtc = DateTime.UtcNow,
                Condition = remaining <= TimeSpan.FromMinutes(warningMinutes)
                    ? FoodBuffCondition.Expiring : FoodBuffCondition.Active
            };
        }

        public FoodBuffState At(DateTime utcNow, int warningMinutes)
        {
            if (Condition != FoodBuffCondition.Active && Condition != FoodBuffCondition.Expiring)
                return this;
            return FromRemaining(Remaining - (utcNow - SampledAtUtc), warningMinutes);
        }
    }
}
