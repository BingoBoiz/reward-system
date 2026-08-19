using NabaGame.Core.Runtime.EventManager;

namespace NabaGame.Reward
{
    public class DailyRewardChangedEvent : GameEvent
    {
    }

    public class LuckySpinChangedEvent : GameEvent
    {
    }

    public class SpinStartedEvent : GameEvent
    {
        public int WedgeIndex;
        public float DurationSeconds;
    }

    public class SpinResultEvent : GameEvent
    {
        public int WedgeIndex;
        public RewardItem Item;
        public long Amount;
    }
}
