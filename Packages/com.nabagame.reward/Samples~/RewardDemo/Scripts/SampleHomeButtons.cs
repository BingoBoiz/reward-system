using NabaGame.Core.Runtime.EventManager;
using NabaGame.Reward;
using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward.Sample
{
    // the host-side red-dot recipe: listen to package events, read the panel queries, drive your own badges
    public class SampleHomeButtons : MonoBehaviour
    {
        [SerializeField] Button dailyButton;
        [SerializeField] GameObject dailyBadge;
        [SerializeField] Button spinButton;
        [SerializeField] GameObject spinBadge;
        [SerializeField] Button playtimeButton;
        [SerializeField] GameObject playtimeBadge;

        public void SetInfo()
        {
            dailyButton.onClick.RemoveListener(OpenDaily);
            dailyButton.onClick.AddListener(OpenDaily);
            spinButton.onClick.RemoveListener(OpenSpin);
            spinButton.onClick.AddListener(OpenSpin);
            playtimeButton.onClick.RemoveListener(OpenPlaytime);
            playtimeButton.onClick.AddListener(OpenPlaytime);

            EventManager.Instance.RemoveListener<DailyRewardChangedEvent>(OnDailyChanged);
            EventManager.Instance.AddListener<DailyRewardChangedEvent>(OnDailyChanged);
            EventManager.Instance.RemoveListener<LuckySpinChangedEvent>(OnSpinChanged);
            EventManager.Instance.AddListener<LuckySpinChangedEvent>(OnSpinChanged);
            EventManager.Instance.RemoveListener<OnlineRewardChangedEvent>(OnOnlineChanged);
            EventManager.Instance.AddListener<OnlineRewardChangedEvent>(OnOnlineChanged);
            Refresh();
        }

        void OnDestroy()
        {
            EventManager.Instance.RemoveListener<DailyRewardChangedEvent>(OnDailyChanged);
            EventManager.Instance.RemoveListener<LuckySpinChangedEvent>(OnSpinChanged);
            EventManager.Instance.RemoveListener<OnlineRewardChangedEvent>(OnOnlineChanged);
        }

        void Refresh()
        {
            SampleUIRoot ui = SampleUIRoot.Instance;
            dailyBadge.SetActive(ui.dailyRewardPanel.ClaimableCount > 0);
            spinBadge.SetActive(ui.luckySpinPanel.FreeSpinReady && !ui.luckySpinPanel.IsSpinning);
            playtimeBadge.SetActive(ui.onlineRewardPanel.HasClaimable);
        }

        void OnDailyChanged(DailyRewardChangedEvent e) => Refresh();

        void OnSpinChanged(LuckySpinChangedEvent e) => Refresh();

        void OnOnlineChanged(OnlineRewardChangedEvent e) => Refresh();

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
