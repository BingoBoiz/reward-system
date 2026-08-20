using System;
using System.Collections.Generic;
using UnityEngine;

namespace NabaGame.Reward
{
    // everything the host fills per slot lives here: identity, art, audio, unlock time, and the grant callback.
    // list position is the slot: rows[0] unlocks first, UnlockAfterSeconds must strictly increase
    [Serializable]
    public class OnlineRewardRow
    {
        public string Key;
        public Sprite Icon;
        public long Amount;
        public int UnlockAfterSeconds;
        public AudioClip ClaimSfx;

        // the host grants here; assign before SetInfo or nothing is granted
        [NonSerialized, HideInInspector]
        public Action<OnlineRewardRow> OnClaimed;

        public OnlineRewardRow()
        {
        }

        public OnlineRewardRow(string key, long amount, int unlockAfterSeconds, Sprite icon = null,
            Action<OnlineRewardRow> onClaimed = null, AudioClip claimSfx = null)
        {
            Key = key;
            Amount = amount;
            UnlockAfterSeconds = unlockAfterSeconds;
            Icon = icon;
            OnClaimed = onClaimed;
            ClaimSfx = claimSfx;
        }

        // incomplete rows warn and keep running; call from the host manager's OnValidate too
        public static void Warn(List<OnlineRewardRow> rows, UnityEngine.Object context = null)
        {
            if (rows == null || rows.Count == 0) return;
            var gaps = new System.Text.StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.IsNullOrEmpty(rows[i].Key)) gaps.Append($" rows[{i}].Key is empty;");
                if (rows[i].Icon == null) gaps.Append($" rows[{i}].Icon is null;");
                if (rows[i].Amount <= 0) gaps.Append($" rows[{i}].Amount is {rows[i].Amount};");
            }

            if (gaps.Length > 0) Debug.LogWarning($"[OnlineRewardRow] incomplete data (the feature still runs):{gaps}", context);
        }
    }
}
