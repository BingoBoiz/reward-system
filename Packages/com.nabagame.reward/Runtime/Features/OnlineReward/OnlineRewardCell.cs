using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward
{
    public class OnlineRewardCell : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image frame;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text amountLabel;
        [SerializeField] TMP_Text timerLabel;
        [SerializeField] TMP_Text claimLabel;
        [SerializeField] GameObject claimedTick;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Sprite[] frameSprites;
        [SerializeField] float lockedAlpha = 0.72f;
        [SerializeField] float claimedIconAlpha = 0.45f;
        [SerializeField] float claimPulseScale = 1.05f;
        [SerializeField] float claimPulseDuration = 0.5f;

        Action<int> onClicked;
        Tween introTween;
        Tween pulseTween;
        OnlineSlotState state;

        public int Slot { get; private set; }

        void OnValidate()
        {
            if (!button) button = GetComponent<Button>();
            if (!frame) frame = GetComponent<Image>();
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        }

        public void SetInfo(int slot, OnlineRewardRow row, Action<int> clickedCallback)
        {
            Slot = slot;
            onClicked = clickedCallback;

            if (frame && frameSprites.Length > 0) frame.sprite = frameSprites[slot % frameSprites.Length];
            if (icon) icon.sprite = row.Icon;
            if (amountLabel) amountLabel.text = "+" + RewardAmountFormat.Short(row.Amount);

            RewardUi.Bind(button, HandleClicked);
        }

        public void Refresh(OnlineSlotState newState, float remainingSeconds)
        {
            state = newState;
            bool claimed = state == OnlineSlotState.Claimed;
            bool claimable = state == OnlineSlotState.Claimable;

            if (claimedTick) claimedTick.SetActive(claimed);
            if (claimLabel) claimLabel.gameObject.SetActive(claimable);
            if (timerLabel)
            {
                timerLabel.gameObject.SetActive(state == OnlineSlotState.Locked);
                if (state == OnlineSlotState.Locked) timerLabel.text = FormatTime(remainingSeconds);
            }

            if (icon) icon.color = new Color(1f, 1f, 1f, claimed ? claimedIconAlpha : 1f);
            if (button) button.interactable = claimable;
            if (canvasGroup) canvasGroup.alpha = state == OnlineSlotState.Locked ? lockedAlpha : 1f;

            if (introTween != null && introTween.IsActive()) return;
            ApplyPulse();
        }

        public void PlayIntro(float delay)
        {
            KillTweens();
            transform.localScale = Vector3.one * 0.6f;
            introTween = transform.DOScale(1f, 0.28f).SetDelay(delay).SetEase(Ease.OutBack)
                .SetUpdate(true).SetLink(gameObject).OnComplete(ApplyPulse);
        }

        public void PlayClaimedPunch()
        {
            KillTweens();
            transform.localScale = Vector3.one;
            if (!icon)
            {
                ApplyPulse();
                return;
            }

            icon.rectTransform.localScale = Vector3.one;
            introTween = icon.rectTransform.DOPunchScale(Vector3.one * 0.16f, 0.35f, 8, 0.8f)
                .SetUpdate(true).SetLink(gameObject).OnComplete(ApplyPulse);
        }

        public void RefreshTimer(float remainingSeconds)
        {
            if (timerLabel && timerLabel.gameObject.activeSelf) timerLabel.text = FormatTime(remainingSeconds);
        }

        public static string FormatTime(float seconds)
        {
            int total = Mathf.CeilToInt(seconds);
            int hours = total / 3600;
            int minutes = total / 60 % 60;
            int secs = total % 60;
            return hours > 0 ? $"{hours}:{minutes:00}:{secs:00}" : $"{minutes:00}:{secs:00}";
        }

        void ApplyPulse()
        {
            introTween = null;
            pulseTween?.Kill();
            pulseTween = null;
            transform.localScale = Vector3.one;
            if (icon) icon.rectTransform.localScale = Vector3.one;
            if (state != OnlineSlotState.Claimable || !icon) return;
            pulseTween = icon.rectTransform.DOScale(claimPulseScale, claimPulseDuration).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                .SetUpdate(true).SetLink(gameObject);
        }

        void KillTweens()
        {
            introTween?.Kill();
            pulseTween?.Kill();
            introTween = null;
            pulseTween = null;
        }

        void OnDisable()
        {
            KillTweens();
            transform.localScale = Vector3.one;
            if (icon) icon.rectTransform.localScale = Vector3.one;
        }

        void HandleClicked()
        {
            if (onClicked != null) onClicked(Slot);
        }
    }
}
