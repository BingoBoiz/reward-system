using System.Globalization;

namespace NabaGame.Reward
{
    public static class RewardAmountFormat
    {
        static readonly string[] suffixes = { "", "K", "M", "B", "T", "Q" };

        public static string Short(long amount)
        {
            double value = amount;
            int tier = 0;
            while (value >= 1000 && tier < suffixes.Length - 1)
            {
                value /= 1000;
                tier++;
            }

            string text = value < 10 && tier > 0
                ? value.ToString("0.#", CultureInfo.InvariantCulture)
                : value.ToString("0", CultureInfo.InvariantCulture);
            return text + suffixes[tier];
        }
    }
}
