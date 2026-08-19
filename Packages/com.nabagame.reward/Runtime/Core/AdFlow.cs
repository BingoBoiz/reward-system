using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NabaGame.Reward
{
    // one rewarded-ad request at a time; the guard is released a frame later because
    // a mediation SDK can drop a request without firing either callback
    public sealed class AdFlow
    {
        readonly RewardHooks hooks;

        public bool Busy { get; private set; }

        public AdFlow(RewardHooks rewardHooks)
        {
            hooks = rewardHooks;
        }

        public bool Show(string placement, Action onReward, Action onSkip = null)
        {
            if (Busy) return false;
            Busy = true;

            if (Application.isEditor)
            {
                Release(onReward);
                return true;
            }

            hooks.ShowRewardedAd(placement, () => Release(onReward), () => Release(onSkip));
            return true;
        }

        void Release(Action callback)
        {
            ReleaseNextFrame().Forget();
            callback?.Invoke();
        }

        async UniTaskVoid ReleaseNextFrame()
        {
            await UniTask.NextFrame();
            Busy = false;
        }
    }
}
