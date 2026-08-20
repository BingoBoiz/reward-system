using NabaGame.Core.Runtime.EventManager;

namespace NabaGame.Reward
{
    // notifications for the host (red dots, HUD); grants travel through OnlineRewardRow.OnClaimed instead
    public class OnlineRewardChangedEvent : GameEvent
    {
    }

    public class OnlineRewardSpeedUpEvent : GameEvent
    {
        public int Multiplier;
    }

    public class OnlineRewardPanelClosedEvent : GameEvent
    {
    }
}
