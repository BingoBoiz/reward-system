using System.Collections.Generic;
using NabaGame.UI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NabaGame.Reward.Sample
{
    // the sample's own UI composition root, so importing this sample pulls in no host types
    public class SampleUIRoot : UIManagerSingleton<SampleUIRoot>
    {
        [SerializeField, FoldoutGroup("Common")]
        public DailyRewardPanel dailyRewardPanel;

        [SerializeField, FoldoutGroup("Common")]
        public LuckySpinPanel luckySpinPanel;

        [SerializeField, FoldoutGroup("Common")]
        public OnlineRewardPanel onlineRewardPanel;

        [SerializeField, FoldoutGroup("Common")]
        public SampleItemReceivedPanel itemReceivedPanel;

        [SerializeField] List<BaseUI> checkHasPopup = new List<BaseUI>();

        private void OnValidate()
        {
            if (!dailyRewardPanel) dailyRewardPanel = GetComponentInChildren<DailyRewardPanel>(true);
            if (!luckySpinPanel) luckySpinPanel = GetComponentInChildren<LuckySpinPanel>(true);
            if (!onlineRewardPanel) onlineRewardPanel = GetComponentInChildren<OnlineRewardPanel>(true);
            if (!itemReceivedPanel) itemReceivedPanel = GetComponentInChildren<SampleItemReceivedPanel>(true);
        }

        public void SetInfo()
        {
            if (dailyRewardPanel && !checkHasPopup.Contains(dailyRewardPanel)) checkHasPopup.Add(dailyRewardPanel);
            if (luckySpinPanel && !checkHasPopup.Contains(luckySpinPanel)) checkHasPopup.Add(luckySpinPanel);
            if (onlineRewardPanel && !checkHasPopup.Contains(onlineRewardPanel)) checkHasPopup.Add(onlineRewardPanel);
        }

        public bool HasPopup()
        {
            foreach (BaseUI panel in checkHasPopup)
            {
                if (panel.IsVisible()) return true;
            }

            return false;
        }
    }
}
