using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NabaGame.Core.Runtime.EventManager;
using NabaGame.UI;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward
{
    public class LuckySpinPanel : BaseUI
    {
        public const string SaveKey = "NabaReward.Spin";
        public const int ProfileVersion = 1;
        public const string AdPlacement = "LuckySpin_AdSpin";

        [ShowInInspector, ReadOnly, TableList, TabGroup("Tabs", "Rows")]
        List<LuckySpinRow> rows = new List<LuckySpinRow>(); // dữ liệu do manager truyền vào

        [SerializeField, Min(1), TabGroup("Tabs", "Config")] int freeSpinCooldownSeconds = 1800; // giây chờ giữa hai lượt free
        [SerializeField, Min(1f), TabGroup("Tabs", "Config")] float spinDurationSeconds = 4.5f; // thời gian quay một lượt
        [SerializeField, Min(1), TabGroup("Tabs", "Config")] int spinFullTurns = 5; // số vòng quay trọn vẹn

        [SerializeField, TabGroup("Tabs", "UI")] Button closeButton; // nút đóng panel
        [SerializeField, TabGroup("Tabs", "UI")] TMP_Text cooldownLabel; // chữ đếm ngược lượt free
        [SerializeField, FoldoutGroup("Tabs/UI/Spin Button")] Button spinButton; // nút bấm quay
        [SerializeField, FoldoutGroup("Tabs/UI/Spin Button")] TMP_Text spinLabel; // chữ trên nút quay
        [SerializeField, FoldoutGroup("Tabs/UI/Spin Button")] GameObject videoIcon; // icon ads khi hết free
        [SerializeField, FoldoutGroup("Tabs/UI/Wheel")] RectTransform wheel; // hình vòng quay sẽ xoay
        [SerializeField, FoldoutGroup("Tabs/UI/Wheel")] RectTransform pointer; // kim chỉ phần thưởng
        [SerializeField, FoldoutGroup("Tabs/UI/Wheel")] List<LuckySpinWedge> wedges = new (); // múi xếp sẵn theo chiều kim

        [SerializeField, TabGroup("Tabs", "FX")] float windUpSeconds = 0.45f; // giây kéo ngược lấy đà
        [SerializeField, TabGroup("Tabs", "FX")] float windUpDegrees = 22f; // độ kéo ngược lấy đà
        [SerializeField, TabGroup("Tabs", "FX")] float spinLabelShiftWithVideo = 22f; // dịch chữ khi icon hiện
        [SerializeField, TabGroup("Tabs", "FX")] Color spinDisabledTint = new Color(0.68f, 0.68f, 0.68f, 1f); // màu nút khi khoá quay
        [SerializeField, FoldoutGroup("Tabs/FX/SFX")] AudioClip spinStartSfx; // tiếng lúc bắt đầu quay
        [SerializeField, FoldoutGroup("Tabs/FX/SFX")] AudioClip tickSfx; // tiếng tách khi qua múi
        [SerializeField, FoldoutGroup("Tabs/FX/SFX")] AudioClip landSfx; // tiếng lúc kim dừng lại
        [SerializeField, FoldoutGroup("Tabs/FX/SFX")] AudioClip buttonSfx; // tiếng bấm nút chung

        RewardFlow flow;
        TimeScheduler.Handle cooldownHandle;
        Sequence spinSequence;
        Tween pointerTween;
        CancellationTokenSource countdownCts;
        Vector2 spinLabelRestPosition;
        int lastTickWedge = -1;

        [ShowInInspector, TabGroup("Tabs", "Profile"), HideLabel, InlineProperty]
        public LuckySpinProfile Profile { get; private set; }

        float WedgeStep => wedges.Count == 0 ? 360f : 360f / wedges.Count;

        #region API

        // one-time init with the host's data; call from Start() at boot, the panel stays hidden until OpenPanel()
        public void SetInfo(List<LuckySpinRow> hostRows)
        {
            if (hostRows == null || hostRows.Count < 2) throw new InvalidOperationException("LuckySpinPanel: rows needs at least 2 wedges");
            rows = hostRows;
            flow = new RewardFlow(this);
            LuckySpinRow.Warn(rows, this);

            Profile = RewardProfileStore.Load<LuckySpinProfile>(SaveKey, ProfileVersion);
            ScheduleCooldownEnd();
            BindWedges();
            BindUi();
            RefreshAll();

            // the initial state is a state change too: host listeners (red dots, HUD) start out empty
            RaiseChanged();
        }

        public void OpenPanel()
        {
            if (Profile == null)
            {
                Debug.LogError("[LuckySpinPanel] OpenPanel before SetInfo(rows); call SetInfo from your boot first", this);
                return;
            }

            Show();
            RefreshAll();
            StartCountdown();
        }

        public void ClosePanel()
        {
            if (IsSpinning || (flow != null && flow.Busy)) return;
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            StopCountdown();
            Hide();
            EventManager.Instance.Raise(new LuckySpinPanelClosedEvent());
        }

        // panel không vẽ chấm đỏ; host tự vẽ (xem SampleRedDot)
        // red-dot query
        public bool FreeSpinReady => Profile != null && Profile.NextFreeSpinAtMs <= RewardClock.NowMs;

        public double SecondsUntilFreeSpin => Profile == null ? 0 : RewardClock.SecondsUntil(Profile.NextFreeSpinAtMs);

        public bool IsSpinning { get; private set; }

        public bool CanSpinByAd => !IsSpinning && (flow == null || !flow.Busy);

        public void ResetProfile()
        {
            RewardProfileStore.Clear(SaveKey);
            Profile = RewardProfileStore.Load<LuckySpinProfile>(SaveKey, ProfileVersion);
            ScheduleCooldownEnd();
            RaiseChanged();
        }

        #endregion

        #region Logic

        bool SpinFree()
        {
            if (IsSpinning || !FreeSpinReady) return false;

            Profile.NextFreeSpinAtMs = RewardClock.NowMs + freeSpinCooldownSeconds * 1000L;
            RewardProfileStore.Save(SaveKey, Profile);
            ScheduleCooldownEnd();
            BeginSpin(Roll());
            return true;
        }

        bool SpinByAd()
        {
            if (!CanSpinByAd) return false;
            if (!flow.ShowAd(AdPlacement, () => BeginSpin(Roll()), OnAdFailed)) return false;
            RaiseChanged();
            return true;
        }

        void OnAdFailed(string message)
        {
            Debug.Log($"[LuckySpinPanel] ad spin did not go through: {message}");
            RewardHooks.ShowMessage(message);
            RefreshAll();
        }

        int Roll()
        {
            int total = 0;
            for (int i = 0; i < rows.Count; i++) total += Mathf.Max(0, rows[i].Weight);
            // every weight invalid: uniform pick, the Warn at SetInfo already named the rows
            if (total <= 0) return UnityEngine.Random.Range(0, rows.Count);

            int pick = UnityEngine.Random.Range(0, total);
            for (int i = 0; i < rows.Count; i++)
            {
                pick -= Mathf.Max(0, rows[i].Weight);
                if (pick < 0) return i;
            }

            return rows.Count - 1;
        }

        // the wedge is decided here; the animation only lands on it. the reward waits for the wheel to stop
        void BeginSpin(int wedgeIndex)
        {
            IsSpinning = true;
            if (spinStartSfx) RewardHooks.PlaySfx(spinStartSfx);

            EventManager.Instance.Raise(new SpinStartedEvent { WedgeIndex = wedgeIndex, DurationSeconds = spinDurationSeconds });
            if (IsVisible()) PlaySpinTween(wedgeIndex, spinDurationSeconds);
            RaiseChanged();
            FinishSpin(wedgeIndex).Forget();
        }

        async UniTaskVoid FinishSpin(int wedgeIndex)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(spinDurationSeconds), DelayType.UnscaledDeltaTime, cancellationToken: this.GetCancellationTokenOnDestroy());

            LuckySpinRow row = rows[wedgeIndex];
            if (landSfx) RewardHooks.PlaySfx(landSfx);
            if (row.ClaimSfx) RewardHooks.PlaySfx(row.ClaimSfx);
            IsSpinning = false;

            // the audit log is mandatory: a null OnClaimed grants nothing and only this line betrays it
            Debug.Log($"[LuckySpinPanel] spun wedge {wedgeIndex + 1}: {row.Key} x{row.Amount}");
            if (row.OnClaimed != null) row.OnClaimed(row);
            else Debug.LogError($"[LuckySpinPanel] rows[{wedgeIndex}].OnClaimed is null, '{row.Key}' x{row.Amount} was NOT granted", this);

            if (IsVisible() && wedgeIndex < wedges.Count && wedges[wedgeIndex]) wedges[wedgeIndex].PlayWinPunch();
            RaiseChanged();
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
            RaiseChanged();
        }

        void RaiseChanged()
        {
            if (IsVisible()) RefreshAll();
            EventManager.Instance.Raise(new LuckySpinChangedEvent());
        }

        // captured before any RefreshAll shifts the label sideways
        void Awake()
        {
            if (spinLabel) spinLabelRestPosition = spinLabel.rectTransform.anchoredPosition;
        }

        void OnDestroy()
        {
            TimeScheduler.Cancel(ref cooldownHandle);
            KillTweens();
            StopCountdown();
        }

        #endregion

        #region UI

        void BindWedges()
        {
            if (wedges.Count == 0)
            {
                Debug.LogError("[LuckySpinPanel] wedges list is empty, no wedges will be shown", this);
                return;
            }

            if (wedges.Count != rows.Count)
                Debug.LogWarning($"[LuckySpinPanel] rows has {rows.Count} wedges but the prefab has {wedges.Count}, extras stay hidden", this);

            for (int i = 0; i < wedges.Count; i++)
            {
                if (!wedges[i]) continue;
                bool used = i < rows.Count;
                wedges[i].gameObject.SetActive(used);
                if (used) wedges[i].SetInfo(i, rows[i]);
            }
        }

        void BindUi()
        {
            RewardUi.Bind(closeButton, ClosePanel);
            RewardUi.Bind(spinButton, OnSpinClicked);
        }

        void RefreshAll()
        {
            if (Profile == null) return;
            bool spinning = IsSpinning;
            bool free = FreeSpinReady;
            bool busy = flow != null && flow.Busy;
            bool canSpin = !spinning && !busy && (free || CanSpinByAd);

            if (closeButton) closeButton.interactable = !spinning && !busy;
            if (spinButton)
            {
                spinButton.interactable = canSpin;
                if (spinButton.image) spinButton.image.color = canSpin ? Color.white : spinDisabledTint;
            }

            if (videoIcon) videoIcon.SetActive(!free);
            if (spinLabel) spinLabel.rectTransform.anchoredPosition = free ? spinLabelRestPosition : spinLabelRestPosition + Vector2.right * spinLabelShiftWithVideo;
            if (cooldownLabel) cooldownLabel.gameObject.SetActive(!free);
            RefreshCooldownLabel();
        }

        void RefreshCooldownLabel()
        {
            if (!cooldownLabel) return;
            TimeSpan left = TimeSpan.FromSeconds(Math.Ceiling(SecondsUntilFreeSpin));
            string clock = left.Hours > 0 ? $"{(int)left.TotalHours}:{left.Minutes:00}:{left.Seconds:00}" : $"{left.Minutes:00}:{left.Seconds:00}";
            cooldownLabel.text = "Free spin in " + clock;
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
                await UniTask.Delay(RewardClock.MsUntilNextTick(SecondsUntilFreeSpin), DelayType.Realtime, cancellationToken: token);
                if (!FreeSpinReady) RefreshCooldownLabel();
            }
        }

        void StopCountdown()
        {
            countdownCts?.Cancel();
            countdownCts?.Dispose();
            countdownCts = null;
        }

        void OnSpinClicked()
        {
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            if (FreeSpinReady) SpinFree();
            else SpinByAd();
        }

        void PlaySpinTween(int wedgeIndex, float duration)
        {
            if (!wheel || wedges.Count == 0) return;
            KillTweens();
            float current = Mathf.Repeat(wheel.localEulerAngles.z, 360f);
            float target = (wedgeIndex + 0.5f) * WedgeStep;
            float final = current + spinFullTurns * 360f + Mathf.Repeat(target - current, 360f);
            lastTickWedge = Mathf.FloorToInt(current / WedgeStep);

            spinSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(wheel.DOLocalRotate(new Vector3(0f, 0f, current - windUpDegrees), windUpSeconds).SetEase(Ease.InOutSine))
                .Append(wheel.DOLocalRotate(new Vector3(0f, 0f, final), duration - windUpSeconds, RotateMode.FastBeyond360).SetEase(Ease.OutQuart))
                .OnUpdate(OnWheelRotated)
                .OnComplete(OnWheelStopped);
        }

        void OnWheelRotated()
        {
            if (!wheel || wedges.Count == 0) return;
            float z = wheel.localEulerAngles.z;
            for (int i = 0; i < wedges.Count; i++) if (wedges[i]) wedges[i].KeepUpright(z);

            int tick = Mathf.FloorToInt(Mathf.Repeat(z, 360f) / WedgeStep);
            if (tick == lastTickWedge) return;
            lastTickWedge = tick;

            pointerTween?.Kill();
            if (pointer)
            {
                pointer.localRotation = Quaternion.identity;
                pointerTween = pointer.DOPunchRotation(new Vector3(0f, 0f, -14f), 0.2f, 6, 0.5f).SetUpdate(true).SetLink(gameObject);
            }

            if (tickSfx) RewardHooks.PlaySfx(tickSfx);
        }

        void OnWheelStopped()
        {
            spinSequence = null;
            OnWheelRotated();
        }

        void KillTweens()
        {
            spinSequence?.Kill();
            spinSequence = null;
            pointerTween?.Kill();
            pointerTween = null;
            if (pointer) pointer.localRotation = Quaternion.identity;
        }

        #endregion

        #region Debug

        [ButtonGroup("Tabs/Debug/Panel"), Button("Open"), DisableInEditorMode]
        void PreviewOpen() => OpenPanel();

        [ButtonGroup("Tabs/Debug/Panel"), Button("Close"), DisableInEditorMode]
        void PreviewClose() => ClosePanel();

        [ButtonGroup("Tabs/Debug/Panel"), Button("Reset + Reopen"), DisableInEditorMode]
        void PreviewResetAndReopen()
        {
            ResetProfile();
            Hide();
            OpenPanel();
        }

        [ButtonGroup("Tabs/Debug/Spin"), Button("Spin Free"), DisableInEditorMode]
        void PreviewSpinFree() => Debug.Log($"[LuckySpinPanel] SpinFree() -> {SpinFree()}");

        [ButtonGroup("Tabs/Debug/Spin"), Button("Spin By Ad"), DisableInEditorMode]
        void PreviewSpinByAd() => Debug.Log($"[LuckySpinPanel] SpinByAd() -> {SpinByAd()}");

        [TabGroup("Tabs", "Debug"), Button("Force Wedge"), DisableInEditorMode]
        void PreviewForceWedge(int wedgeIndex)
        {
            if (IsSpinning) return;
            BeginSpin(Mathf.Clamp(wedgeIndex, 0, rows.Count - 1));
        }

        [TabGroup("Tabs", "Debug"), Button("Set Cooldown Seconds"), DisableInEditorMode]
        void PreviewSetCooldownSeconds(int seconds)
        {
            Profile.NextFreeSpinAtMs = RewardClock.NowMs + seconds * 1000L;
            RewardProfileStore.Save(SaveKey, Profile);
            ScheduleCooldownEnd();
            RaiseChanged();
        }

        [TabGroup("Tabs", "Debug"), Button("Roll Distribution"), DisableInEditorMode]
        void PreviewRollDistribution(int rolls = 1000)
        {
            int[] hits = new int[rows.Count];
            for (int i = 0; i < rolls; i++) hits[Roll()]++;
            var sb = new System.Text.StringBuilder($"[LuckySpinPanel] {rolls} rolls:");
            for (int i = 0; i < hits.Length; i++) sb.Append($" w{i}({rows[i].Weight})={hits[i]}");
            Debug.Log(sb.ToString());
        }

        #endregion
    }
}
