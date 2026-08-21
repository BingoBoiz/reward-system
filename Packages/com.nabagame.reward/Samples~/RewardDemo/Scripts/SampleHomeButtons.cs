using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward.Sample
{
    // only the three home buttons; the red dots drive themselves (see SampleRedDot)
    public class SampleHomeButtons : MonoBehaviour
    {
        [SerializeField] Button dailyButton;
        [SerializeField] Button spinButton;
        [SerializeField] Button playtimeButton;

        public void SetInfo()
        {
            dailyButton.onClick.RemoveListener(OpenDaily);
            dailyButton.onClick.AddListener(OpenDaily);
            spinButton.onClick.RemoveListener(OpenSpin);
            spinButton.onClick.AddListener(OpenSpin);
            playtimeButton.onClick.RemoveListener(OpenPlaytime);
            playtimeButton.onClick.AddListener(OpenPlaytime);
        }

        void OpenDaily()
        {
            if (SampleUIRoot.Instance.HasPopup()) return;
            SampleUIRoot.Instance.dailyRewardPanel.OpenPanel();
        }

        void OpenSpin()
        {
            if (SampleUIRoot.Instance.HasPopup()) return;
            SampleUIRoot.Instance.luckySpinPanel.OpenPanel();
        }

        void OpenPlaytime()
        {
            if (SampleUIRoot.Instance.HasPopup()) return;
            SampleUIRoot.Instance.onlineRewardPanel.OpenPanel();
        }
    }
}
