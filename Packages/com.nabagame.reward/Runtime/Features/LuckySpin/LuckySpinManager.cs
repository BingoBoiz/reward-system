using System;
using Cysharp.Threading.Tasks;
using NabaGame.Core.Runtime.EventManager;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NabaGame.Reward
{
    public class LuckySpinManager : MonoBehaviour
    {
        public const string SaveKey = "NabaReward.Spin";
        public const int ProfileVersion = 1;
        public const string AdPlacement = "LuckySpin_AdSpin";

        [SerializeField] AudioClip spinStartSfx;
        [SerializeField] AudioClip landSfx;

        LuckySpinRowData config;
        RewardHooks hooks;
        RewardItem[] items;
        AdFlow adFlow;
        TimeScheduler.Handle cooldownHandle;
        readonly LuckySpinChangedEvent changedEvent = new LuckySpinChangedEvent();
        readonly SpinStartedEvent startedEvent = new SpinStartedEvent();
        readonly SpinResultEvent resultEvent = new SpinResultEvent();

        public LuckySpinProfile Profile { get; private set; }

        #region Init & Save

        public void StartClass(LuckySpinRowData spinConfig, RewardHooks rewardHooks)
        {
            if (spinConfig == null) throw new InvalidOperationException("LuckySpinManager: config is null");
            if (rewardHooks == null) throw new InvalidOperationException("LuckySpinManager: hooks is null");
            rewardHooks.Validate("LuckySpinManager", requireAds: true);

            config = spinConfig;
            hooks = rewardHooks;
            config.ValidateOrThrow();
            items = new RewardItem[config.WedgeCount];
            for (int i = 0; i < items.Length; i++) items[i] = hooks.Catalog.Get(config.GetRow(i).Key);
            adFlow = new AdFlow(hooks);
            Profile = RewardProfileStore.Load<LuckySpinProfile>(SaveKey, ProfileVersion);
            ScheduleCooldownEnd();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (Profile == null) return;
            if (pauseStatus)
            {
                RewardProfileStore.Save(SaveKey, Profile);
                return;
            }

            ScheduleCooldownEnd();
            EventManager.Instance.Raise(changedEvent);
        }

        void OnApplicationQuit()
        {
            if (Profile != null) RewardProfileStore.Save(SaveKey, Profile);
        }

        void OnDestroy()
        {
            TimeScheduler.Cancel(ref cooldownHandle);
        }

        void ScheduleCooldownEnd()
        {
            TimeScheduler.Cancel(ref cooldownHandle);
            if (FreeSpinReady) return;
            cooldownHandle = TimeScheduler.Schedule(Profile.NextFreeSpinAtMs, OnCooldownEnd);
        }

        void OnCooldownEnd()
        {
            cooldownHandle = null;
            EventManager.Instance.Raise(changedEvent);
        }

        #endregion

        #region Queries

        public int WedgeCount => config.WedgeCount;

        public LuckySpinRow GetRow(int index) => config.GetRow(index);

        public RewardItem GetItem(int index) => items[index];

        public float SpinDurationSeconds => config.SpinDurationSeconds;

        public int SpinFullTurns => config.SpinFullTurns;

        public bool FreeSpinReady => Profile.NextFreeSpinAtMs <= TimeScheduler.NowMs;

        public double SecondsUntilFreeSpin => TimeScheduler.SecondsUntil(Profile.NextFreeSpinAtMs);

        public bool IsSpinning { get; private set; }

        public bool CanSpinByAd => !IsSpinning && !adFlow.Busy;

        #endregion

        #region Spin

        public bool SpinFree()
        {
            if (IsSpinning || !FreeSpinReady) return false;

            Profile.NextFreeSpinAtMs = TimeScheduler.NowMs + config.FreeSpinCooldownSeconds * 1000L;
            RewardProfileStore.Save(SaveKey, Profile);
            ScheduleCooldownEnd();
            BeginSpin(config.Roll());
            return true;
        }

        public bool SpinByAd()
        {
            if (!CanSpinByAd) return false;
            adFlow.Show(AdPlacement, () => BeginSpin(config.Roll()));
            EventManager.Instance.Raise(changedEvent);
            return true;
        }

        // the wedge is decided here; the panel only animates onto it. grant waits for the wheel to stop
        void BeginSpin(int wedgeIndex)
        {
            IsSpinning = true;
            if (spinStartSfx) hooks.PlaySfx(spinStartSfx);

            startedEvent.WedgeIndex = wedgeIndex;
            startedEvent.DurationSeconds = config.SpinDurationSeconds;
            EventManager.Instance.Raise(startedEvent);
            EventManager.Instance.Raise(changedEvent);
            FinishSpin(wedgeIndex).Forget();
        }

        async UniTaskVoid FinishSpin(int wedgeIndex)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(config.SpinDurationSeconds), DelayType.UnscaledDeltaTime, cancellationToken: this.GetCancellationTokenOnDestroy());

            LuckySpinRow row = config.GetRow(wedgeIndex);
            hooks.Granter.Grant(items[wedgeIndex], row.Amount);
            if (landSfx) hooks.PlaySfx(landSfx);
            IsSpinning = false;

            resultEvent.WedgeIndex = wedgeIndex;
            resultEvent.Item = items[wedgeIndex];
            resultEvent.Amount = row.Amount;
            EventManager.Instance.Raise(resultEvent);
            EventManager.Instance.Raise(changedEvent);
        }

        public bool SpinForced(int wedgeIndex)
        {
            if (IsSpinning) return false;
            BeginSpin(Mathf.Clamp(wedgeIndex, 0, WedgeCount - 1));
            return true;
        }

        public void ResetProfile()
        {
            RewardProfileStore.Clear(SaveKey);
            Profile = RewardProfileStore.Load<LuckySpinProfile>(SaveKey, ProfileVersion);
            ScheduleCooldownEnd();
            EventManager.Instance.Raise(changedEvent);
        }

        public void SetCooldownSeconds(int seconds)
        {
            Profile.NextFreeSpinAtMs = TimeScheduler.NowMs + seconds * 1000L;
            RewardProfileStore.Save(SaveKey, Profile);
            ScheduleCooldownEnd();
            EventManager.Instance.Raise(changedEvent);
        }

        #endregion

        #region Debug

        [Button, DisableInEditorMode]
        void PreviewSpinFree() => Debug.Log($"[LuckySpinManager] SpinFree() -> {SpinFree()}");

        [Button, DisableInEditorMode]
        void PreviewSpinByAd() => Debug.Log($"[LuckySpinManager] SpinByAd() -> {SpinByAd()}");

        [Button, DisableInEditorMode]
        void PreviewForceWedge(int wedgeIndex) => SpinForced(wedgeIndex);

        [Button, DisableInEditorMode]
        void PreviewResetProfile() => ResetProfile();

        [Button, DisableInEditorMode]
        void PreviewSetCooldownSeconds(int seconds) => SetCooldownSeconds(seconds);

        [Button, DisableInEditorMode]
        void PreviewRollDistribution(int rolls = 1000)
        {
            int[] hits = new int[WedgeCount];
            for (int i = 0; i < rolls; i++) hits[config.Roll()]++;
            var sb = new System.Text.StringBuilder($"[LuckySpinManager] {rolls} rolls:");
            for (int i = 0; i < hits.Length; i++) sb.Append($" w{i}({GetRow(i).Weight})={hits[i]}");
            Debug.Log(sb.ToString());
        }

        #endregion
    }
}
