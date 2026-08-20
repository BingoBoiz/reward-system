using NabaGame.Core.Runtime.EventManager;

namespace NabaGame.Reward
{
    // notifications for the host (red dots, HUD); grants travel through DailyRewardRow.OnClaimed instead
    public class DailyRewardChangedEvent : GameEvent
    {
    }

    public class DailyRewardPanelClosedEvent : GameEvent
    {
    }
}
