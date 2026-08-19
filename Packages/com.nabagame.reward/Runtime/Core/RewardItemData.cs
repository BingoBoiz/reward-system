using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NabaGame.Reward
{
    // importer-shaped: sheet tab 'RewardItem' (A1 = RewardItem) fills rewardItems
    [CreateAssetMenu(menuName = "NabaGame/Reward/Reward Item Data", fileName = "RawRewardItem")]
    public class RewardItemData : ScriptableObject
    {
        [TableList] public List<RewardItem> rewardItems = new List<RewardItem>();

        public RewardItem Get(string key)
        {
            for (int i = 0; i < rewardItems.Count; i++)
                if (rewardItems[i].Key == key) return rewardItems[i];

            throw new InvalidOperationException($"[RewardItemData] '{name}' has no item with key '{key}'");
        }

        void OnValidate()
        {
            for (int i = 0; i < rewardItems.Count; i++)
            {
                string key = rewardItems[i].Key;
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogError($"[RewardItemData] '{name}' row {i} has an empty Key", this);
                    continue;
                }

                for (int j = 0; j < i; j++)
                    if (rewardItems[j].Key == key)
                        Debug.LogError($"[RewardItemData] '{name}' rows {j} and {i} share key '{key}'", this);

                if (rewardItems[i].Icon == null)
                    Debug.LogError($"[RewardItemData] '{name}' key '{key}' has no Icon", this);
            }
        }
    }
}
