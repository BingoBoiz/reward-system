using System;

namespace NabaGame.Reward
{
    [Serializable]
    public class DailyRewardProfile : RewardProfile
    {
        public int StreakDay;
        public string LastClaimDateUtc = "";
        public int OpenAllAdsWatched;
    }
}
