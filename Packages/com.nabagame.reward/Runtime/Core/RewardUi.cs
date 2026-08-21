using NabaGame.UI;
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

        internal static void PlayIntro(UIElement[] elements)
        {
            if (elements == null) return;
            for (int i = 0; i < elements.Length; i++)
            {
                UIElement element = elements[i];
                if (!element || !element.gameObject.activeInHierarchy) continue;
                element.HideUiElement(true);
                element.ShowUiElement();
            }
        }
    }
}
