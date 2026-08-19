using System;
using NabaGame.Core.Runtime.EventManager;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NabaGame.Reward
{
    public enum DailyState
    {
        Locked,
        Claimable,
        Claimed
    }

    public class DailyRewardManager : MonoBehaviour
    {
        public const string SaveKey = "NabaReward.Daily";
        public const int ProfileVersion = 1;

        [SerializeField] AudioClip claimSfx;

        DailyRewardRowData config;
        RewardHooks hooks;
        RewardItem[] items;
        TimeScheduler.Handle rolloverHandle;
        readonly DailyRewardChangedEvent changedEvent = new DailyRewardChangedEvent();

        public DailyRewardProfile Profile { get; private set; }

        #region Init & Save

        public void StartClass(DailyRewardRowData dailyConfig, RewardHooks rewardHooks)
        {
            if (dailyConfig == null) throw new InvalidOperationException("DailyRewardManager: config is null");
            if (rewardHooks == null) throw new InvalidOperationException("DailyRewardManager: hooks is null");
            rewardHooks.Validate("DailyRewardManager");

            config = dailyConfig;
            hooks = rewardHooks;
            config.ValidateOrThrow();
            items = new RewardItem[config.dailyRewardRows.Count];
            for (int i = 0; i < items.Length; i++) items[i] = hooks.Catalog.Get(config.GetRow(i).Key);
            Profile = RewardProfileStore.Load<DailyRewardProfile>(SaveKey, ProfileVersion);
            ScheduleRollover();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (Profile == null) return;
            if (pauseStatus)
            {
                RewardProfileStore.Save(SaveKey, Profile);
                return;
            }

            ScheduleRollover();
            EventManager.Instance.Raise(changedEvent);
        }

        void OnApplicationQuit()
        {
            if (Profile != null) RewardProfileStore.Save(SaveKey, Profile);
        }

        void OnDestroy()
        {
            TimeScheduler.Cancel(ref rolloverHandle);
        }

        void ScheduleRollover()
        {
            TimeScheduler.Cancel(ref rolloverHandle);
            long nextMidnight = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero).ToUnixTimeMilliseconds();
            rolloverHandle = TimeScheduler.Schedule(nextMidnight, OnRollover);
        }

        void OnRollover()
        {
            rolloverHandle = null;
            ScheduleRollover();
            EventManager.Instance.Raise(changedEvent);
        }

        #endregion

        #region Queries

        public int DayCount => DailyRewardRowData.DayCount;

        public int StreakDay => Profile.StreakDay;

        public int ClaimableCount => Profile.LastClaimDateUtc != TodayUtc ? 1 : 0;

        static string TodayUtc => DateTime.UtcNow.ToString("yyyy-MM-dd");

        public DailyRewardRow GetRow(int day) => config.GetRow(day);

        public RewardItem GetItem(int day) => items[day];

        public DailyState GetState(int day)
        {
            if (day < Profile.StreakDay) return DailyState.Claimed;
            if (day == Profile.StreakDay && ClaimableCount > 0) return DailyState.Claimable;
            return DailyState.Locked;
        }

        #endregion

        #region Claim

        public bool Claim()
        {
            int day = Profile.StreakDay;
            if (GetState(day) != DailyState.Claimable) return false;

            hooks.Granter.Grant(items[day], config.GetRow(day).Amount);
            if (claimSfx) hooks.PlaySfx(claimSfx);

            Profile.StreakDay = (day + 1) % DayCount;
            Profile.LastClaimDateUtc = TodayUtc;
            RewardProfileStore.Save(SaveKey, Profile);
            EventManager.Instance.Raise(changedEvent);
            return true;
        }

        public void ResetProfile()
        {
            RewardProfileStore.Clear(SaveKey);
            Profile = RewardProfileStore.Load<DailyRewardProfile>(SaveKey, ProfileVersion);
            EventManager.Instance.Raise(changedEvent);
        }

        public void SetStreakDay(int day)
        {
            Profile.StreakDay = Mathf.Clamp(day, 0, DayCount - 1);
            Profile.LastClaimDateUtc = "";
            RewardProfileStore.Save(SaveKey, Profile);
            EventManager.Instance.Raise(changedEvent);
        }

        public void ExpireToday()
        {
            Profile.LastClaimDateUtc = "";
            RewardProfileStore.Save(SaveKey, Profile);
            EventManager.Instance.Raise(changedEvent);
        }

        #endregion

        #region Debug

        [Button, DisableInEditorMode]
        void PreviewClaim() => Debug.Log($"[DailyRewardManager] Claim() -> {Claim()}, streak now {Profile.StreakDay}");

        [Button, DisableInEditorMode]
        void PreviewResetProfile() => ResetProfile();

        [Button, DisableInEditorMode]
        void PreviewSetStreakDay(int day) => SetStreakDay(day);

        [Button, DisableInEditorMode]
        void PreviewExpireToday() => ExpireToday();

        #endregion
    }
}
