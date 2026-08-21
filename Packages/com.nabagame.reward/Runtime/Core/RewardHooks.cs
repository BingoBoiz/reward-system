using System;
using UnityEngine;

namespace NabaGame.Reward
{
    // host services, assigned once at boot before any panel SetInfo.
    // defaults keep a freshly dragged prefab working: missing ads/iap reward immediately and log the gap
    public static class RewardHooks
    {
        // compare against these to localize instead of showing the English text
        public const string AdNotAvailableMessage = "No ad available right now";
        public const string AdSkippedMessage = "Watch the full ad to get the reward";
        public const string PurchaseFailedMessage = "Purchase failed";

        public static Action<AudioClip> PlaySfx = DefaultPlaySfx;
        public static Action<string, Action, Action> ShowRewardedAd = DefaultShowRewardedAd;
        public static Action<string, Action<bool>> PurchaseIap = DefaultPurchaseIap;
        public static Func<string, string> GetIapPrice = DefaultGetIapPrice;
        public static Action<string> ShowMessage = DefaultShowMessage;
        // (event name, param key, param value) - the argument order matches the usual analytics signature
        public static Action<string, string, string> TrackEvent = DefaultTrackEvent;

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

        // empty falls back to the price string authored on the panel
        static string DefaultGetIapPrice(string productId) => "";

        static void DefaultShowMessage(string message)
        {
            Debug.LogWarning($"[RewardHooks] ShowMessage is not set, the player sees nothing: {message}");
        }

        // analytics stays silent in the editor anyway, so an unassigned hook logs what it would have sent
        static void DefaultTrackEvent(string eventName, string paramName, string paramValue)
        {
            Debug.Log($"[RewardHooks] TrackEvent is not set: {eventName} / {paramName} = {paramValue}");
        }

        // statics survive play sessions when domain reload is disabled
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            PlaySfx = DefaultPlaySfx;
            ShowRewardedAd = DefaultShowRewardedAd;
            PurchaseIap = DefaultPurchaseIap;
            GetIapPrice = DefaultGetIapPrice;
            ShowMessage = DefaultShowMessage;
            TrackEvent = DefaultTrackEvent;
        }
    }
}
