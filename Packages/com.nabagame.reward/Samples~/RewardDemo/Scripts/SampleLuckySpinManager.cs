using System.Collections.Generic;
using NabaGame.Reward;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NabaGame.Reward.Sample
{
    // this manager is the host's job: fill the rows, decide what each Key grants, feed the panel
    public class SampleLuckySpinManager : MonoBehaviour
    {
        [TableList] public List<LuckySpinRow> rows = new List<LuckySpinRow>();

        public void SetInfo()
        {
            foreach (LuckySpinRow row in rows) row.OnClaimed = OnClaimed;
            SampleUIRoot.Instance.luckySpinPanel.SetInfo(rows);
        }

        void OnClaimed(LuckySpinRow row) => SampleRewardGranter.Grant(row.Key, row.Icon, row.Amount);

        void OnValidate() => LuckySpinRow.Warn(rows, this);
    }
}
