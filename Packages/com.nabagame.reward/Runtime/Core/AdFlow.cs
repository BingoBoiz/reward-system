using System;
using Cysharp.Threading.Tasks;

namespace NabaGame.Reward
{
    // one rewarded-ad request at a time; the guard is released a frame later because
    // a mediation SDK can drop a request without firing either callback
    public sealed class AdFlow
    {
        const float busyTimeoutSeconds = 15f;

        public bool Busy { get; private set; }

        int generation;

        public bool Show(string placement, Action onReward, Action onSkip = null)
        {
            if (Busy) return false;
            Busy = true;
            int id = ++generation;
            ReleaseAfterTimeout(id).Forget();
            RewardHooks.ShowRewardedAd(placement, () => Release(id, onReward), () => Release(id, onSkip));
            return true;
        }

        void Release(int id, Action callback)
        {
            ReleaseNextFrame(id).Forget();
            callback?.Invoke();
        }

        async UniTaskVoid ReleaseNextFrame(int id)
        {
            await UniTask.NextFrame();
            if (id == generation) Busy = false;
        }

        // a host SDK can swallow both callbacks (interval throttle); never leave the buttons dead
        async UniTaskVoid ReleaseAfterTimeout(int id)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(busyTimeoutSeconds), DelayType.Realtime);
            if (id == generation) Busy = false;
        }
    }
}
