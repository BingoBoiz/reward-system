using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NabaGame.Reward
{
    // public fields in sheet column order; the importer fills them by index (original) or by name (Feeder)
    [Serializable]
    public class DailyRewardRow
    {
        public int Day;
        public string Key;
        public long Amount;
        public string LabelOverride;
        public bool HideIconUntilClaim;

        // "-" because the importer rejects blank cells
        public bool HasLabelOverride => !string.IsNullOrEmpty(LabelOverride) && LabelOverride != "-";
    }

    // importer-shaped: sheet tab 'DailyReward' (A1 = DailyRewardRow) fills dailyRewardRows
    [CreateAssetMenu(menuName = "NabaGame/Reward/Daily Reward Data", fileName = "RawDailyReward")]
    public class DailyRewardRowData : ScriptableObject
    {
        public const int DayCount = 7;

        [TableList] public List<DailyRewardRow> dailyRewardRows = new List<DailyRewardRow>();

        public DailyRewardRow GetRow(int day) => dailyRewardRows[day];

        public void ValidateOrThrow()
        {
            if (dailyRewardRows.Count != DayCount)
                throw new InvalidOperationException($"[DailyRewardRowData] '{name}' has {dailyRewardRows.Count} rows, expected {DayCount}");

            for (int i = 0; i < dailyRewardRows.Count; i++)
            {
                DailyRewardRow row = dailyRewardRows[i];
                if (row.Day != i + 1)
                    throw new InvalidOperationException($"[DailyRewardRowData] '{name}' row {i} has Day {row.Day}, expected {i + 1}");
                if (string.IsNullOrEmpty(row.Key))
                    throw new InvalidOperationException($"[DailyRewardRowData] '{name}' day {row.Day} has an empty Key");
                if (row.Amount <= 0)
                    throw new InvalidOperationException($"[DailyRewardRowData] '{name}' day {row.Day} has Amount {row.Amount}");
            }
        }

        void OnValidate()
        {
            try
            {
                ValidateOrThrow();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError(e.Message, this);
            }
        }
    }
}
