using System;
using System.Collections.Generic;
using UnityEngine;

namespace NabaGame.Reward
{
    // everything the host fills per wedge lives here: identity, art, audio, weight, and the grant callback.
    // list position is the wedge: rows[0] sits at 12 o'clock, clockwise
    [Serializable]
    public class LuckySpinRow
    {
        public string Key;
        public Sprite Icon;
        public long Amount;
        public int Weight = 1;
        public AudioClip ClaimSfx;

        // the host grants here; assign before SetInfo or nothing is granted
        [NonSerialized, HideInInspector]
        public Action<LuckySpinRow> OnClaimed;

        public LuckySpinRow()
        {
        }

        public LuckySpinRow(string key, long amount, int weight = 1, Sprite icon = null,
            Action<LuckySpinRow> onClaimed = null, AudioClip claimSfx = null)
        {
            Key = key;
            Amount = amount;
            Weight = weight;
            Icon = icon;
            OnClaimed = onClaimed;
            ClaimSfx = claimSfx;
        }

        // incomplete rows warn and keep running; call from the host manager's OnValidate too
        public static void Warn(List<LuckySpinRow> rows, UnityEngine.Object context = null)
        {
            if (rows == null || rows.Count == 0) return;
            var gaps = new System.Text.StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.IsNullOrEmpty(rows[i].Key)) gaps.Append($" rows[{i}].Key is empty;");
                if (rows[i].Icon == null) gaps.Append($" rows[{i}].Icon is null;");
                if (rows[i].Amount <= 0) gaps.Append($" rows[{i}].Amount is {rows[i].Amount};");
                if (rows[i].Weight <= 0) gaps.Append($" rows[{i}].Weight is {rows[i].Weight};");
            }

            if (gaps.Length > 0) Debug.LogWarning($"[LuckySpinRow] incomplete data (the feature still runs):{gaps}", context);
        }
    }
}
