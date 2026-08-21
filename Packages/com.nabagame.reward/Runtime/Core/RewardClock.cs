using System;
using System.Globalization;
using UnityEngine;

namespace NabaGame.Reward
{
    // the package's only clock: every wall-clock and monotonic read goes through here, so a fake or server time is a one-file change
    public static class RewardClock
    {
        public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static DateTime UtcNow => DateTime.UtcNow;

        // the save-file day key; invariant culture because a non-Gregorian locale (th-TH) would write a different year to disk
        public static string TodayUtc => UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static long NextUtcMidnightMs => new DateTimeOffset(UtcNow.Date.AddDays(1), TimeSpan.Zero).ToUnixTimeMilliseconds();

        public static double SecondsUntil(long atUnixMs) => Math.Max(0, (atUnixMs - NowMs) / 1000.0);

        // monotonic and keeps running while suspended: accrual owners rebaseline on focus (see OnlineRewardPanel)
        public static double MonotonicSeconds => Time.realtimeSinceStartupAsDouble;

        // realtime ms until a ceil-displayed countdown shows its next value; rate = countdown seconds per real second (x5 speed-up -> 5)
        public static int MsUntilNextTick(double remainingSeconds, double rate = 1)
        {
            double wait = remainingSeconds - Math.Ceiling(remainingSeconds) + 1; // (0, 1]: a full second exactly on a boundary
            return (int)Math.Ceiling(wait / rate * 1000) + 1; // +1 ms lands past the boundary, never on it
        }
    }
}
