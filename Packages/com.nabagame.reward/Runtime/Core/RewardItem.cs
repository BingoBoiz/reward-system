using System;
using UnityEngine;

namespace NabaGame.Reward
{
    // one row of the host's reward catalog; the package only ever reads Key and Icon
    [Serializable]
    public class RewardItem
    {
        public string Key;
        public string DisplayName;
        public Sprite Icon;
    }
}
