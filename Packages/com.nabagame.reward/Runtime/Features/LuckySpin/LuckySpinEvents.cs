using NabaGame.Core.Runtime.EventManager;

namespace NabaGame.Reward
{
    // notifications for the host (red dots, HUD); grants travel through LuckySpinRow.OnClaimed instead
    public class LuckySpinChangedEvent : GameEvent
    {
    }

    public class SpinStartedEvent : GameEvent
    {
        public int WedgeIndex;
        public float DurationSeconds;
    }

    public class LuckySpinPanelClosedEvent : GameEvent
    {
    }
}
