using System;
using UnityEngine;

namespace NabaGame.Reward
{
    public sealed class RewardHooks
    {
        public IRewardGranter Granter;
        public RewardItemData Catalog;
        public Action<AudioClip> PlaySfx;
        public Action<string, Action, Action> ShowRewardedAd;

        public void Validate(string owner, bool requireAds = false)
        {
            if (Granter == null) throw new InvalidOperationException($"{owner}: RewardHooks.Granter is null");
            if (Catalog == null) throw new InvalidOperationException($"{owner}: RewardHooks.Catalog is null");
            if (PlaySfx == null) throw new InvalidOperationException($"{owner}: RewardHooks.PlaySfx is null");
            if (requireAds && ShowRewardedAd == null) throw new InvalidOperationException($"{owner}: RewardHooks.ShowRewardedAd is null");
        }
    }
}
