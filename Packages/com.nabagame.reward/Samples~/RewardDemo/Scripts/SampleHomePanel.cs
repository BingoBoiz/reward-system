using NabaGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward.Sample
{
    // only the three home buttons; the red dots drive themselves (see SampleRedDot)
    public class SampleHomePanel : BaseUI
    {
        [SerializeField] Button dailyButton;
        [SerializeField] Button spinButton;
        [SerializeField] Button playtimeButton;
        [SerializeField] AudioClip buttonSfx;

        public override void SetInfo()
        {
            dailyButton.onClick.RemoveListener(OpenDaily);
            dailyButton.onClick.AddListener(OpenDaily);
            spinButton.onClick.RemoveListener(OpenSpin);
            spinButton.onClick.AddListener(OpenSpin);
            playtimeButton.onClick.RemoveListener(OpenPlaytime);
            playtimeButton.onClick.AddListener(OpenPlaytime);
        }

        public void OpenPanel()
        {
            Show();
        }

        public void ClosePanel()
        {
            Hide();
        }

        void OpenDaily()
        {
            if (SampleUIManager.Instance.HasPopup()) return;
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            SampleUIManager.Instance.dailyRewardPanel.OpenPanel();
        }

        void OpenSpin()
        {
            if (SampleUIManager.Instance.HasPopup()) return;
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            SampleUIManager.Instance.luckySpinPanel.OpenPanel();
        }

        void OpenPlaytime()
        {
            if (SampleUIManager.Instance.HasPopup()) return;
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            SampleUIManager.Instance.onlineRewardPanel.OpenPanel();
        }
    }
}
