using System;
using UnityEngine;

namespace NabaGame.Reward
{
    // host services, assigned once at boot before any panel SetInfo.
    // defaults keep a freshly dragged prefab working: missing ads/iap reward immediately and log the gap
    public static class RewardHooks
    {
        public static Action<AudioClip> PlaySfx = DefaultPlaySfx;
        public static Action<string, Action, Action> ShowRewardedAd = DefaultShowRewardedAd;
        public static Action<string, Action<bool>> PurchaseIap = DefaultPurchaseIap;

        static void DefaultPlaySfx(AudioClip clip)
        {
        }

        static void DefaultShowRewardedAd(string placement, Action onReward, Action onSkip)
        {
            Debug.LogError($"[RewardHooks] ShowRewardedAd is not set; '{placement}' rewards immediately");
            onReward?.Invoke();
        }

        static void DefaultPurchaseIap(string productId, Action<bool> onResult)
        {
            Debug.LogError($"[RewardHooks] PurchaseIap is not set; '{productId}' succeeds");
            onResult?.Invoke(true);
        }

        // statics survive play sessions when domain reload is disabled
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            PlaySfx = DefaultPlaySfx;
            ShowRewardedAd = DefaultShowRewardedAd;
            PurchaseIap = DefaultPurchaseIap;
        }
    }
}
