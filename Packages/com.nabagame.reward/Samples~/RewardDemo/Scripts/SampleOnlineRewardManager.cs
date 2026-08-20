using System.Collections.Generic;
using NabaGame.Reward;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NabaGame.Reward.Sample
{
    // this manager is the host's job: fill the rows, decide what each Key grants, feed the panel.
    // SetInfo must run at boot: playtime accrues from that moment, never init this lazily
    public class SampleOnlineRewardManager : MonoBehaviour
    {
        [TableList] public List<OnlineRewardRow> rows = new List<OnlineRewardRow>();

        public void SetInfo()
        {
            foreach (OnlineRewardRow row in rows) row.OnClaimed = OnClaimed;
            SampleUIRoot.Instance.onlineRewardPanel.SetInfo(rows);
        }

        void OnClaimed(OnlineRewardRow row) => SampleRewardGranter.Grant(row.Key, row.Icon, row.Amount);

        void OnValidate() => OnlineRewardRow.Warn(rows, this);
    }
}
