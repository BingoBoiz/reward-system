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
        [SerializeField] Button closeButton;
        [SerializeField] Button spinButton;
        [SerializeField] TMP_Text spinLabel;
        [SerializeField] GameObject spinBadge;
        [SerializeField] GameObject videoIcon;
        [SerializeField] TMP_Text cooldownLabel;
        [SerializeField] RectTransform wheel;
        [SerializeField] RectTransform pointer;
        [SerializeField] RectTransform wedgesRoot;
        [SerializeField] LuckySpinWedge wedgeTemplate;
        [SerializeField] int wheelSegmentCount = 8;
        [SerializeField] float wedgeRadius = 205f;
        [SerializeField] float windUpSeconds = 0.45f;
        [SerializeField] float windUpDegrees = 22f;
        [SerializeField] float spinLabelShiftWithVideo = 22f;
        [SerializeField] Color spinDisabledTint = new Color(0.68f, 0.68f, 0.68f, 1f);
        [SerializeField] AudioClip buttonSfx;
        [SerializeField] AudioClip tickSfx;

        readonly List<LuckySpinWedge> wedges = new List<LuckySpinWedge>();
        LuckySpinManager manager;
        RewardHooks hooks;
        bool started;
        Sequence spinSequence;
        Tween pointerTween;
        CancellationTokenSource countdownCts;
        Vector2 spinLabelRestPosition;
        int lastTickWedge = -1;

        float WedgeStep => 360f / wedges.Count;

        public void StartClass(LuckySpinManager luckySpinManager, RewardHooks rewardHooks)
        {
            manager = luckySpinManager;
            hooks = rewardHooks;

            if (!started)
            {
                started = true;
                spinLabelRestPosition = spinLabel.rectTransform.anchoredPosition;
                wedgeTemplate.gameObject.SetActive(false);
                if (manager.WedgeCount != wheelSegmentCount)
                    Debug.LogError($"[LuckySpinPanel] config has {manager.WedgeCount} wedges but the wheel art has {wheelSegmentCount} segments", this);

                for (int i = 0; i < manager.WedgeCount; i++)
                {
                    LuckySpinWedge wedge = Instantiate(wedgeTemplate, wedgesRoot);
                    wedge.gameObject.SetActive(true);
                    wedge.name = $"Wedge_{i}";
                    float angle = (i + 0.5f) * (360f / manager.WedgeCount) * Mathf.Deg2Rad;
                    ((RectTransform)wedge.transform).anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * wedgeRadius;
                    wedges.Add(wedge);
                }
            }

            closeButton.onClick.RemoveListener(ClosePanel);
            closeButton.onClick.AddListener(ClosePanel);
            spinButton.onClick.RemoveListener(OnSpinClicked);
            spinButton.onClick.AddListener(OnSpinClicked);

            EventManager.Instance.RemoveListener<LuckySpinChangedEvent>(OnLuckySpinChanged);
            EventManager.Instance.AddListener<LuckySpinChangedEvent>(OnLuckySpinChanged);
            EventManager.Instance.RemoveListener<SpinStartedEvent>(OnSpinStarted);
            EventManager.Instance.AddListener<SpinStartedEvent>(OnSpinStarted);
            EventManager.Instance.RemoveListener<SpinResultEvent>(OnSpinResult);
            EventManager.Instance.AddListener<SpinResultEvent>(OnSpinResult);

            for (int i = 0; i < wedges.Count; i++) wedges[i].StartClass(i, manager.GetRow(i), manager.GetItem(i));
            RefreshAll();
        }

        void OnDestroy()
        {
            KillTweens();
            StopCountdown();
            if (EventManager.Instance == null) return;
            EventManager.Instance.RemoveListener<LuckySpinChangedEvent>(OnLuckySpinChanged);
            EventManager.Instance.RemoveListener<SpinStartedEvent>(OnSpinStarted);
            EventManager.Instance.RemoveListener<SpinResultEvent>(OnSpinResult);
        }

        public void OpenPanel()
        {
            Show();
            RefreshAll();
            StopCountdown();
            countdownCts = new CancellationTokenSource();
            RunCountdown(countdownCts.Token).Forget();
        }

        public void ClosePanel()
        {
            if (manager.IsSpinning) return;
            if (buttonSfx) hooks.PlaySfx(buttonSfx);
            StopCountdown();
            Hide();
        }

        void RefreshAll()
        {
            bool spinning = manager.IsSpinning;
            bool free = manager.FreeSpinReady;
            bool canSpin = !spinning && (free || manager.CanSpinByAd);

            closeButton.interactable = !spinning;
            spinButton.interactable = canSpin;
            spinButton.image.color = canSpin ? Color.white : spinDisabledTint;
            spinBadge.SetActive(free && !spinning);
            videoIcon.SetActive(!free);
            spinLabel.rectTransform.anchoredPosition = free ? spinLabelRestPosition : spinLabelRestPosition + Vector2.right * spinLabelShiftWithVideo;
            cooldownLabel.gameObject.SetActive(!free);
            RefreshCooldownLabel();
        }

        void RefreshCooldownLabel()
        {
            TimeSpan left = TimeSpan.FromSeconds(Math.Ceiling(manager.SecondsUntilFreeSpin));
            string clock = left.Hours > 0 ? $"{(int)left.TotalHours}:{left.Minutes:00}:{left.Seconds:00}" : $"{left.Minutes:00}:{left.Seconds:00}";
            cooldownLabel.text = "Free spin in " + clock;
        }

        async UniTaskVoid RunCountdown(CancellationToken token)
        {
            while (true)
            {
                await UniTask.Delay(1000, true, cancellationToken: token);
                if (!manager.FreeSpinReady) RefreshCooldownLabel();
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
            if (buttonSfx) hooks.PlaySfx(buttonSfx);
            if (manager.FreeSpinReady) manager.SpinFree();
            else manager.SpinByAd();
        }

        void OnSpinStarted(SpinStartedEvent e)
        {
            RefreshAll();
            if (!IsVisible()) return;
            PlaySpinTween(e.WedgeIndex, e.DurationSeconds);
        }

        void PlaySpinTween(int wedgeIndex, float duration)
        {
            KillTweens();
            float current = Mathf.Repeat(wheel.localEulerAngles.z, 360f);
            float target = (wedgeIndex + 0.5f) * WedgeStep;
            float final = current + manager.SpinFullTurns * 360f + Mathf.Repeat(target - current, 360f);
            lastTickWedge = Mathf.FloorToInt(current / WedgeStep);

            spinSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(wheel.DOLocalRotate(new Vector3(0f, 0f, current - windUpDegrees), windUpSeconds).SetEase(Ease.InOutSine))
                .Append(wheel.DOLocalRotate(new Vector3(0f, 0f, final), duration - windUpSeconds, RotateMode.FastBeyond360).SetEase(Ease.OutQuart))
                .OnUpdate(OnWheelRotated)
                .OnComplete(OnWheelStopped);
        }

        void OnWheelRotated()
        {
            float z = wheel.localEulerAngles.z;
            for (int i = 0; i < wedges.Count; i++) wedges[i].KeepUpright(z);

            int tick = Mathf.FloorToInt(Mathf.Repeat(z, 360f) / WedgeStep);
            if (tick == lastTickWedge) return;
            lastTickWedge = tick;

            pointerTween?.Kill();
            pointer.localRotation = Quaternion.identity;
            pointerTween = pointer.DOPunchRotation(new Vector3(0f, 0f, -14f), 0.2f, 6, 0.5f).SetUpdate(true).SetLink(gameObject);
            if (tickSfx) hooks.PlaySfx(tickSfx);
        }

        void OnWheelStopped()
        {
            spinSequence = null;
            OnWheelRotated();
        }

        void OnSpinResult(SpinResultEvent e)
        {
            RefreshAll();
            if (IsVisible()) wedges[e.WedgeIndex].PlayWinPunch();
        }

        void OnLuckySpinChanged(LuckySpinChangedEvent e)
        {
            if (IsVisible()) RefreshAll();
        }

        void KillTweens()
        {
            spinSequence?.Kill();
            spinSequence = null;
            pointerTween?.Kill();
            pointerTween = null;
            pointer.localRotation = Quaternion.identity;
        }

        [Button, DisableInEditorMode]
        void PreviewOpen() => OpenPanel();

        [Button, DisableInEditorMode]
        void PreviewClose() => ClosePanel();

        [Button, DisableInEditorMode]
        void PreviewSpinFree() => manager.SpinFree();

        [Button, DisableInEditorMode]
        void PreviewSpinByAd() => manager.SpinByAd();

        [Button, DisableInEditorMode]
        void PreviewForceWedge(int wedgeIndex) => manager.SpinForced(wedgeIndex);

        [Button, DisableInEditorMode]
        void PreviewSetCooldownSeconds(int seconds) => manager.SetCooldownSeconds(seconds);

        [Button, DisableInEditorMode]
        void PreviewResetAndReopen()
        {
            manager.ResetProfile();
            Hide();
            OpenPanel();
        }
    }
}
