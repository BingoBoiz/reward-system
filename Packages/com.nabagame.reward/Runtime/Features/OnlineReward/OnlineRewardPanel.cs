using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NabaGame.Core.Runtime.EventManager;
using NabaGame.UI;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward
{
    public enum OnlineSlotState
    {
        Locked,
        Claimable,
        Claimed
    }

    public class OnlineRewardPanel : BaseUI
    {
        [Serializable]
        public class BoosterButton
        {
            public Button button; // nút bấm kích hoạt tua
            public TMP_Text label; // chữ trạng thái trên nút
            public GameObject adsIcon; // icon ads khi chưa bật
            public TMP_Text adsCountLabel; // chữ đếm ads đã xem
            [NonSerialized] public string idleText;
        }

        public const string SaveKey = "NabaReward.Online";
        public const int ProfileVersion = 1;
        public const int SpeedUpX2 = 2;
        public const int SpeedUpX5 = 5;
        public const string OpenAllPlacement = "OnlineReward_OpenAll";
        public const string X2Placement = "OnlineReward_x2Speed";
        public const string X5Placement = "OnlineReward_x5Speed";

        [ShowInInspector, ReadOnly, TableList, TabGroup("Tabs", "Rows")]
        List<OnlineRewardRow> rows = new List<OnlineRewardRow>(); // dữ liệu do manager truyền vào

        [SerializeField, TabGroup("Tabs", "Config")] float x2DurationSeconds = 120f; // giây hiệu lực tua x2
        [SerializeField, TabGroup("Tabs", "Config")] float x5DurationSeconds = 120f; // giây hiệu lực tua x5
        [SerializeField, TabGroup("Tabs", "Config")] int x5AdsRequired = 2; // số ads để kích hoạt x5

        [SerializeField, TabGroup("Tabs", "UI")] Button closeButton; // nút đóng panel
        [SerializeField, TabGroup("Tabs", "UI")] Button openAllButton; // nút ads nhận toàn bộ
        [SerializeField, TabGroup("Tabs", "UI")] BoosterButton x2Booster; // cụm nút tua nhanh x2
        [SerializeField, TabGroup("Tabs", "UI")] BoosterButton x5Booster; // cụm nút tua nhanh x5
        [SerializeField, TabGroup("Tabs", "UI")] RectTransform gridContent; // chỗ chứa lưới ô thưởng
        [SerializeField, TabGroup("Tabs", "UI")] OnlineRewardCell cellTemplate; // ô mẫu để nhân bản

        [SerializeField, TabGroup("Tabs", "FX")] float cellStaggerDelay = 0.03f; // trễ hiện lần lượt từng ô
        [SerializeField, TabGroup("Tabs", "FX")] AudioClip buttonSfx; // tiếng bấm nút chung

        readonly List<OnlineRewardCell> cells = new List<OnlineRewardCell>();
        readonly AdFlow adFlow = new AdFlow();
        readonly HashSet<int> claimedSlots = new HashSet<int>();
        double accumulatedSeconds;
        double baselineRealtime;
        double activeMultiplier = 1;
        long speedUpX2EndMs;
        long speedUpX5EndMs;
        TimeScheduler.Handle unlockHandle;
        TimeScheduler.Handle speedUpHandle;
        bool built;
        CancellationTokenSource countdownCts;

        [ShowInInspector, TabGroup("Tabs", "Profile"), HideLabel, InlineProperty]
        public OnlineRewardProfile Profile { get; private set; }

        #region API

        // one-time init with the host's data; call from Start() at boot — playtime accrues from this
        // moment even while the panel stays hidden, so never call it lazily on first open
        public void SetInfo(List<OnlineRewardRow> hostRows)
        {
            if (hostRows == null || hostRows.Count == 0) throw new InvalidOperationException("OnlineRewardPanel: rows is null or empty");
            for (int i = 0; i < hostRows.Count; i++)
            {
                int previous = i == 0 ? 0 : hostRows[i - 1].UnlockAfterSeconds;
                if (hostRows[i].UnlockAfterSeconds <= previous)
                    throw new InvalidOperationException($"OnlineRewardPanel: rows[{i}].UnlockAfterSeconds is {hostRows[i].UnlockAfterSeconds}, must exceed {previous}");
            }

            rows = hostRows;
            OnlineRewardRow.Warn(rows, this);

            Profile = RewardProfileStore.Load<OnlineRewardProfile>(SaveKey, ProfileVersion);
            ResetBaseline();
            ScheduleNextUnlock();

            // OnApplicationPause is unreliable here: the host UI framework may deactivate a hidden panel.
            // Application.focusChanged is static and fires regardless of GameObject state
            Application.focusChanged -= OnFocusChanged;
            Application.focusChanged += OnFocusChanged;

            BuildCells();
            BindUi();
            for (int i = 0; i < cells.Count && i < rows.Count; i++) cells[i].SetInfo(i, rows[i], OnCellClicked);
            RefreshAll();
        }

        public void OpenPanel()
        {
            if (Profile == null)
            {
                Debug.LogError("[OnlineRewardPanel] OpenPanel before SetInfo(rows); call SetInfo from your boot first", this);
                return;
            }

            Show();
            RefreshAll();
            for (int i = 0; i < cells.Count; i++) cells[i].PlayIntro(i * cellStaggerDelay);
            StartCountdown();
        }

        public void ClosePanel()
        {
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            StopCountdown();
            Hide();
            EventManager.Instance.Raise(new OnlineRewardPanelClosedEvent());
        }

        public int SlotCount => rows.Count;

        // red-dot query
        public bool HasClaimable
        {
            get
            {
                if (Profile == null) return false;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (GetState(i) == OnlineSlotState.Claimable) return true;
                }

                return false;
            }
        }

        public void ResetSession()
        {
            claimedSlots.Clear();
            accumulatedSeconds = 0;
            speedUpX2EndMs = 0;
            speedUpX5EndMs = 0;
            Profile.SpeedUpX5Ads = 0;
            RewardProfileStore.Save(SaveKey, Profile);
            ResetBaseline();
            ScheduleSpeedUpExpiry();
            ScheduleNextUnlock();
            RaiseChanged();
        }

        #endregion

        #region Logic

        double CurrentPlaySeconds => accumulatedSeconds + (Time.realtimeSinceStartupAsDouble - baselineRealtime) * activeMultiplier;

        OnlineSlotState GetState(int slot)
        {
            if (claimedSlots.Contains(slot)) return OnlineSlotState.Claimed;
            return CurrentPlaySeconds >= rows[slot].UnlockAfterSeconds ? OnlineSlotState.Claimable : OnlineSlotState.Locked;
        }

        float GetRemainingSeconds(int slot) => Mathf.Max(0f, (float)(rows[slot].UnlockAfterSeconds - CurrentPlaySeconds));

        void FlushPlaytime()
        {
            accumulatedSeconds = CurrentPlaySeconds;
            ResetBaseline();
        }

        void ResetBaseline()
        {
            baselineRealtime = Time.realtimeSinceStartupAsDouble;
            activeMultiplier = SpeedMultiplier;
        }

        void OnFocusChanged(bool focused)
        {
            if (Profile == null) return;
            if (!focused)
            {
                FlushPlaytime();
                RewardProfileStore.Save(SaveKey, Profile);
                return;
            }

            // suspended wall-clock time must not count as playtime
            ResetBaseline();
            ScheduleSpeedUpExpiry();
            ScheduleNextUnlock();
            RaiseChanged();
        }

        void ScheduleNextUnlock()
        {
            TimeScheduler.Cancel(ref unlockHandle);

            double best = double.MaxValue;
            for (int i = 0; i < rows.Count; i++)
            {
                if (claimedSlots.Contains(i)) continue;
                double remaining = rows[i].UnlockAfterSeconds - CurrentPlaySeconds;
                if (remaining > 0 && remaining < best) best = remaining;
            }

            if (best == double.MaxValue) return;
            // playtime runs faster than the wall clock while a speed-up is active
            unlockHandle = TimeScheduler.Schedule(TimeScheduler.NowMs + (long)(best / activeMultiplier * 1000), OnUnlockDeadline);
        }

        void OnUnlockDeadline()
        {
            unlockHandle = null;
            RaiseChanged();
            ScheduleNextUnlock();
        }

        // the x2 and x5 buffs stack: both running means playtime ticks 7x
        double SpeedMultiplier
        {
            get
            {
                double sum = (IsSpeedUpActive(SpeedUpX2) ? SpeedUpX2 : 0) + (IsSpeedUpActive(SpeedUpX5) ? SpeedUpX5 : 0);
                return sum > 0 ? sum : 1;
            }
        }

        bool IsSpeedUpActive(int multiplier) => EndMs(multiplier) > TimeScheduler.NowMs;

        float GetSpeedUpRemaining(int multiplier) => Mathf.Max(0f, (float)TimeScheduler.SecondsUntil(EndMs(multiplier)));

        int GetSpeedUpAds(int multiplier) => multiplier == SpeedUpX5 ? Profile.SpeedUpX5Ads : 0;

        int GetSpeedUpAdsRequired(int multiplier) => multiplier == SpeedUpX5 ? x5AdsRequired : 1;

        long EndMs(int multiplier)
        {
            switch (multiplier)
            {
                case SpeedUpX2: return speedUpX2EndMs;
                case SpeedUpX5: return speedUpX5EndMs;
                default: throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "OnlineRewardPanel: unknown speed-up multiplier");
            }
        }

        bool RequestSpeedUp(int multiplier)
        {
            if (IsSpeedUpActive(multiplier)) return false;

            string placement;
            switch (multiplier)
            {
                case SpeedUpX2: placement = X2Placement; break;
                case SpeedUpX5: placement = X5Placement; break;
                default: throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "OnlineRewardPanel: unknown speed-up multiplier");
            }

            return adFlow.Show(placement, () => AddSpeedUpAd(multiplier));
        }

        void AddSpeedUpAd(int multiplier)
        {
            if (IsSpeedUpActive(multiplier)) return;

            if (multiplier == SpeedUpX5 && Profile.SpeedUpX5Ads + 1 < GetSpeedUpAdsRequired(multiplier))
            {
                Profile.SpeedUpX5Ads++;
                RewardProfileStore.Save(SaveKey, Profile);
                RaiseChanged();
                return;
            }

            ActivateSpeedUp(multiplier);
        }

        // the elapsed slice must be baked at the old rate before the multiplier changes
        void ActivateSpeedUp(int multiplier)
        {
            FlushPlaytime();

            float duration = multiplier == SpeedUpX5 ? x5DurationSeconds : x2DurationSeconds;
            long endMs = TimeScheduler.NowMs + (long)(duration * 1000);
            if (multiplier == SpeedUpX5)
            {
                speedUpX5EndMs = endMs;
                Profile.SpeedUpX5Ads = 0;
                RewardProfileStore.Save(SaveKey, Profile);
            }
            else
            {
                speedUpX2EndMs = endMs;
            }

            ResetBaseline();
            ScheduleSpeedUpExpiry();
            ScheduleNextUnlock();
            EventManager.Instance.Raise(new OnlineRewardSpeedUpEvent { Multiplier = multiplier });
            RaiseChanged();
        }

        void ScheduleSpeedUpExpiry()
        {
            TimeScheduler.Cancel(ref speedUpHandle);

            long now = TimeScheduler.NowMs;
            long best = long.MaxValue;
            if (speedUpX2EndMs > now) best = speedUpX2EndMs;
            if (speedUpX5EndMs > now && speedUpX5EndMs < best) best = speedUpX5EndMs;

            if (best == long.MaxValue) return;
            speedUpHandle = TimeScheduler.Schedule(best, OnSpeedUpExpired);
        }

        void OnSpeedUpExpired()
        {
            speedUpHandle = null;
            FlushPlaytime();
            ScheduleSpeedUpExpiry();
            ScheduleNextUnlock();
            RaiseChanged();
        }

        bool Claim(int slot)
        {
            if (GetState(slot) != OnlineSlotState.Claimable) return false;

            ClaimSlot(slot, withSfx: true);
            ResetCycleIfComplete();
            RaiseChanged();
            ScheduleNextUnlock();
            return true;
        }

        bool RequestOpenAll()
        {
            if (claimedSlots.Count >= rows.Count) return false;
            return adFlow.Show(OpenAllPlacement, OpenAll);
        }

        void OpenAll()
        {
            bool first = true;
            for (int i = 0; i < rows.Count; i++)
            {
                if (claimedSlots.Contains(i)) continue;
                ClaimSlot(i, withSfx: first);
                first = false;
            }

            ResetCycleIfComplete();
            RaiseChanged();
            ScheduleNextUnlock();
        }

        void ClaimSlot(int slot, bool withSfx)
        {
            OnlineRewardRow row = rows[slot];
            claimedSlots.Add(slot);

            if (withSfx && row.ClaimSfx) RewardHooks.PlaySfx(row.ClaimSfx);

            // the audit log is mandatory: a null OnClaimed grants nothing and only this line betrays it
            Debug.Log($"[OnlineRewardPanel] claimed slot {slot + 1}: {row.Key} x{row.Amount}");
            if (row.OnClaimed != null) row.OnClaimed(row);
            else Debug.LogError($"[OnlineRewardPanel] rows[{slot}].OnClaimed is null, '{row.Key}' x{row.Amount} was NOT granted", this);

            if (IsVisible() && slot < cells.Count) cells[slot].PlayClaimedPunch();
        }

        void ResetCycleIfComplete()
        {
            if (claimedSlots.Count < rows.Count) return;
            claimedSlots.Clear();
            accumulatedSeconds = 0;
            ResetBaseline();
        }

        void RaiseChanged()
        {
            if (IsVisible()) RefreshAll();
            EventManager.Instance.Raise(new OnlineRewardChangedEvent());
        }

        void OnDestroy()
        {
            Application.focusChanged -= OnFocusChanged;
            TimeScheduler.Cancel(ref unlockHandle);
            TimeScheduler.Cancel(ref speedUpHandle);
            StopCountdown();
        }

        #endregion

        #region UI

        void BuildCells()
        {
            if (built) return;
            built = true;

            if (!cellTemplate)
            {
                Debug.LogError("[OnlineRewardPanel] cellTemplate is not assigned, no cells will be shown", this);
                return;
            }

            if (x2Booster.label) x2Booster.idleText = x2Booster.label.text;
            if (x5Booster.label) x5Booster.idleText = x5Booster.label.text;

            cellTemplate.gameObject.SetActive(false);
            Transform parent = gridContent ? gridContent : cellTemplate.transform.parent;
            for (int i = 0; i < rows.Count; i++)
            {
                OnlineRewardCell cell = Instantiate(cellTemplate, parent);
                cell.gameObject.SetActive(true);
                cell.name = $"Cell_{i}";
                cells.Add(cell);
            }
        }

        void BindUi()
        {
            RewardUi.Bind(closeButton, ClosePanel);
            RewardUi.Bind(openAllButton, OnOpenAllClicked);
            RewardUi.Bind(x2Booster.button, OnX2Clicked);
            RewardUi.Bind(x5Booster.button, OnX5Clicked);
        }

        void RefreshAll()
        {
            if (Profile == null) return;
            for (int i = 0; i < cells.Count && i < rows.Count; i++) cells[i].Refresh(GetState(i), GetRemainingSeconds(i));
            if (openAllButton) openAllButton.interactable = !adFlow.Busy;
            RefreshBooster(x2Booster, SpeedUpX2);
            RefreshBooster(x5Booster, SpeedUpX5);
        }

        void RefreshBooster(BoosterButton booster, int multiplier)
        {
            bool active = IsSpeedUpActive(multiplier);
            if (booster.button) booster.button.interactable = !active && !adFlow.Busy;

            if (active)
            {
                if (booster.label) booster.label.text = OnlineRewardCell.FormatTime(GetSpeedUpRemaining(multiplier));
                if (booster.adsIcon) booster.adsIcon.SetActive(false);
                if (booster.adsCountLabel) booster.adsCountLabel.gameObject.SetActive(false);
                return;
            }

            if (booster.label) booster.label.text = booster.idleText;
            if (booster.adsIcon) booster.adsIcon.SetActive(true);
            bool showCount = GetSpeedUpAdsRequired(multiplier) > 1;
            if (booster.adsCountLabel)
            {
                booster.adsCountLabel.gameObject.SetActive(showCount);
                if (showCount) booster.adsCountLabel.text = $"{GetSpeedUpAds(multiplier)}/{GetSpeedUpAdsRequired(multiplier)}";
            }
        }

        void StartCountdown()
        {
            StopCountdown();
            countdownCts = new CancellationTokenSource();
            RunCountdown(countdownCts.Token).Forget();
        }

        async UniTaskVoid RunCountdown(CancellationToken token)
        {
            // Hide() can arrive from outside ClosePanel, so visibility is the loop condition
            while (IsVisible())
            {
                await UniTask.Delay(1000, true, cancellationToken: token);
                for (int i = 0; i < cells.Count && i < rows.Count; i++) cells[i].RefreshTimer(GetRemainingSeconds(i));
                RefreshBooster(x2Booster, SpeedUpX2);
                RefreshBooster(x5Booster, SpeedUpX5);
            }
        }

        void StopCountdown()
        {
            countdownCts?.Cancel();
            countdownCts?.Dispose();
            countdownCts = null;
        }

        void OnCellClicked(int slot)
        {
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            Claim(slot);
        }

        void OnOpenAllClicked()
        {
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            RequestOpenAll();
            RefreshAll();
        }

        void OnX2Clicked() => OnBoosterClicked(SpeedUpX2);

        void OnX5Clicked() => OnBoosterClicked(SpeedUpX5);

        void OnBoosterClicked(int multiplier)
        {
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            RequestSpeedUp(multiplier);
            RefreshAll();
        }

        #endregion

        #region Debug

        [ButtonGroup("Tabs/Debug/Panel"), Button("Open"), DisableInEditorMode]
        void PreviewOpen() => OpenPanel();

        [ButtonGroup("Tabs/Debug/Panel"), Button("Close"), DisableInEditorMode]
        void PreviewClose() => ClosePanel();

        [ButtonGroup("Tabs/Debug/Panel"), Button("Reset Session"), DisableInEditorMode]
        void PreviewResetSession() => ResetSession();

        [ButtonGroup("Tabs/Debug/Claim"), Button("Unlock All"), DisableInEditorMode]
        void PreviewUnlockAll()
        {
            accumulatedSeconds = rows[rows.Count - 1].UnlockAfterSeconds;
            RaiseChanged();
            ScheduleNextUnlock();
        }

        [ButtonGroup("Tabs/Debug/Claim"), Button("Open All"), DisableInEditorMode]
        void PreviewOpenAll() => RequestOpenAll();

        [ButtonGroup("Tabs/Debug/Claim"), Button("Clear Speed-Ups"), DisableInEditorMode]
        void PreviewClearSpeedUps()
        {
            FlushPlaytime();
            speedUpX2EndMs = 0;
            speedUpX5EndMs = 0;
            ResetBaseline();
            ScheduleSpeedUpExpiry();
            ScheduleNextUnlock();
            RaiseChanged();
        }

        [TabGroup("Tabs", "Debug"), Button("Add Seconds"), DisableInEditorMode]
        void PreviewAddSeconds(double seconds)
        {
            accumulatedSeconds += seconds;
            RaiseChanged();
            ScheduleNextUnlock();
        }

        [TabGroup("Tabs", "Debug"), Button("Speed Up (via ad flow)"), DisableInEditorMode]
        void PreviewSpeedUp(int multiplier) => RequestSpeedUp(multiplier);

        [TabGroup("Tabs", "Debug"), Button("Activate Speed Up"), DisableInEditorMode]
        void PreviewActivateSpeedUp(int multiplier) => ActivateSpeedUp(multiplier);

        #endregion
    }
}
