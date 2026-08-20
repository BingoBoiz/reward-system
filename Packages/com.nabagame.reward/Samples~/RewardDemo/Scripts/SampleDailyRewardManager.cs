using System.Collections.Generic;
using NabaGame.Reward;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NabaGame.Reward.Sample
{
    public class SampleDailyRewardManager : MonoBehaviour
    {
        [TableList] public List<DailyRewardRow> rows = new List<DailyRewardRow>();

        public void SetInfo()
        {
            foreach (DailyRewardRow row in rows) row.OnClaimed = OnClaimed;

            // setup UI panel, but maybe do this in UIManager instead
            SampleUIRoot.Instance.dailyRewardPanel.SetInfo(rows);
        }

        void OnClaimed(DailyRewardRow row)
        {
            SampleRewardGranter.Grant(row.Key, row.Icon, row.Amount);
        }

        void OnValidate()
        {
            DailyRewardRow.Warn(rows, this);
        }
    }
}
