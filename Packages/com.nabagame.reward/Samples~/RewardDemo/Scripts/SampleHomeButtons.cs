using NabaGame.Core.Runtime.EventManager;
using NabaGame.Reward;
using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward.Sample
{
    // the host-side red-dot recipe: listen to package events, drive your own badges
    public class SampleHomeButtons : MonoBehaviour
    {
        [SerializeField] Button dailyButton;
        [SerializeField] GameObject dailyBadge;
        [SerializeField] Button spinButton;
        [SerializeField] GameObject spinBadge;

        DailyRewardManager dailyRewardManager;
        LuckySpinManager luckySpinManager;

        public void StartClass(DailyRewardManager daily, LuckySpinManager spin)
        {
            dailyRewardManager = daily;
            luckySpinManager = spin;

            dailyButton.onClick.RemoveListener(OpenDaily);
            dailyButton.onClick.AddListener(OpenDaily);
            spinButton.onClick.RemoveListener(OpenSpin);
            spinButton.onClick.AddListener(OpenSpin);

            EventManager.Instance.RemoveListener<DailyRewardChangedEvent>(OnDailyChanged);
            EventManager.Instance.AddListener<DailyRewardChangedEvent>(OnDailyChanged);
            EventManager.Instance.RemoveListener<LuckySpinChangedEvent>(OnSpinChanged);
            EventManager.Instance.AddListener<LuckySpinChangedEvent>(OnSpinChanged);
            Refresh();
        }

        void OnDestroy()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.RemoveListener<DailyRewardChangedEvent>(OnDailyChanged);
            EventManager.Instance.RemoveListener<LuckySpinChangedEvent>(OnSpinChanged);
        }

        void Refresh()
        {
            dailyBadge.SetActive(dailyRewardManager.ClaimableCount > 0);
            spinBadge.SetActive(luckySpinManager.FreeSpinReady && !luckySpinManager.IsSpinning);
        }

        void OnDailyChanged(DailyRewardChangedEvent e) => Refresh();

        void OnSpinChanged(LuckySpinChangedEvent e) => Refresh();

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
    }
}
