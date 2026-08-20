using System;
using System.Collections.Generic;
using UnityEngine;

namespace NabaGame.Reward
{
    
    [Serializable]
    public class DailyRewardRow
    {
        public string Key;
        public Sprite Icon;
        public long Amount;
        public AudioClip ClaimSfx;
        public string LabelOverride;
        public bool HideIconUntilClaim;

        [NonSerialized, HideInInspector]
        public Action<DailyRewardRow> OnClaimed;

        public DailyRewardRow()
        {
        }

        public DailyRewardRow(string key, long amount, Sprite icon = null, Action<DailyRewardRow> onClaimed = null,
            AudioClip claimSfx = null, string labelOverride = null, bool hideIconUntilClaim = false)
        {
            Key = key;
            Amount = amount;
            Icon = icon;
            OnClaimed = onClaimed;
            ClaimSfx = claimSfx;
            LabelOverride = labelOverride;
            HideIconUntilClaim = hideIconUntilClaim;
        }

        public bool HasLabelOverride => !string.IsNullOrEmpty(LabelOverride);

        // incomplete rows warn and keep running; call from the host manager's OnValidate too
        public static void Warn(List<DailyRewardRow> rows, UnityEngine.Object context = null)
        {
            if (rows == null || rows.Count == 0) return;
            var gaps = new System.Text.StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.IsNullOrEmpty(rows[i].Key)) gaps.Append($" rows[{i}].Key is empty;");
                if (rows[i].Icon == null) gaps.Append($" rows[{i}].Icon is null;");
                if (rows[i].Amount <= 0) gaps.Append($" rows[{i}].Amount is {rows[i].Amount};");
            }

            if (gaps.Length > 0) Debug.LogWarning($"[DailyRewardRow] incomplete data (the feature still runs):{gaps}", context);
        }
    }
}
