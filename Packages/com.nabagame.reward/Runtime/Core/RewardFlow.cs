using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NabaGame.Reward
{
    // one monetized request at a time per panel, resolved exactly once.
    // the shipped ads SDK only calls back on a granted reward: a throttled, unloaded, skipped or
    // display-failed ad returns in silence. this turns that silence into an explicit failure,
    // so an ad button is never left dead and the player always learns why nothing happened
    public sealed class RewardFlow
    {
        // the ad never opened if the app never lost focus
        const float noShowSeconds = 3f;
        // focus is back: the reward callback normally lands just before the ad closes
        const float skipGraceSeconds = 0.5f;
        const float hardTimeoutSeconds = 180f;
        const float purchaseTimeoutSeconds = 300f;

        readonly MonoBehaviour owner;

        public bool Busy { get; private set; }

        Action onSucceeded;
        Action<string> onFailed;
        bool watchingFocus;
        bool displayed;
        // bumped on every start and every resolve, so exactly one path can win a request
        int generation;

        public RewardFlow(MonoBehaviour owner)
        {
            this.owner = owner;
        }

        public bool ShowAd(string placement, Action onReward, Action<string> failed = null)
        {
            if (Busy) return false;
            Busy = true;
            displayed = false;
            onSucceeded = onReward;
            onFailed = failed;
            int id = ++generation;

            watchingFocus = true;
            Application.focusChanged += OnFocusChanged;

            WatchNoShow(id).Forget();
            WatchTimeout(id, hardTimeoutSeconds, RewardHooks.AdNotAvailableMessage).Forget();
            RewardHooks.ShowRewardedAd(placement, () => Resolve(id, true, null), () => Resolve(id, false, RewardHooks.AdSkippedMessage));
            return true;
        }

        public bool Purchase(string productId, Action onPurchased, Action<string> failed = null)
        {
            if (Busy) return false;
            Busy = true;
            onSucceeded = onPurchased;
            onFailed = failed;
            int id = ++generation;

            WatchTimeout(id, purchaseTimeoutSeconds, RewardHooks.PurchaseFailedMessage).Forget();
            RewardHooks.PurchaseIap(productId, ok => Resolve(id, ok, RewardHooks.PurchaseFailedMessage));
            return true;
        }

        void Resolve(int id, bool ok, string reason)
        {
            if (id != generation) return;
            generation++;
            Busy = false;

            if (watchingFocus)
            {
                Application.focusChanged -= OnFocusChanged;
                watchingFocus = false;
            }

            Action succeeded = onSucceeded;
            Action<string> failed = onFailed;
            onSucceeded = null;
            onFailed = null;

            // the panel died while the ad was up: drop the callbacks, the guard is already clear
            if (!owner) return;

            if (ok) succeeded?.Invoke();
            else failed?.Invoke(reason);
        }

        void OnFocusChanged(bool focused)
        {
            if (!focused)
            {
                displayed = true;
                return;
            }

            if (displayed) WatchSkip(generation).Forget();
        }

        // no focus loss means the SDK swallowed the request (reward interval throttle, nothing loaded)
        async UniTaskVoid WatchNoShow(int id)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(noShowSeconds), DelayType.Realtime);
            if (!displayed) Resolve(id, false, RewardHooks.AdNotAvailableMessage);
        }

        async UniTaskVoid WatchSkip(int id)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(skipGraceSeconds), DelayType.Realtime);
            Resolve(id, false, RewardHooks.AdSkippedMessage);
        }

        async UniTaskVoid WatchTimeout(int id, float seconds, string reason)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime);
            Resolve(id, false, reason);
        }
    }
}
