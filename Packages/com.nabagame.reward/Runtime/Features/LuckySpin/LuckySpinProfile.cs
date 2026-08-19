using System;

namespace NabaGame.Reward
{
    [Serializable]
    public class LuckySpinProfile : RewardProfile
    {
        public long NextFreeSpinAtMs;
    }
}
