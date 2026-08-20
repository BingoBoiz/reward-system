using UnityEngine.Events;
using UnityEngine.UI;

namespace NabaGame.Reward
{
    // serialized UI references are optional by design: the host may disable or delete any button
    internal static class RewardUi
    {
        internal static void Bind(Button button, UnityAction handler)
        {
            if (!button) return;
            button.onClick.RemoveListener(handler);
            button.onClick.AddListener(handler);
        }
    }
}
