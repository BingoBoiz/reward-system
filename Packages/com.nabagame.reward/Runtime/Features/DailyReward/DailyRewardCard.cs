using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward
{
    public class DailyRewardCard : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image background;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text claimLabel;
        [SerializeField] TMP_Text amountLabel;
        [SerializeField] GameObject claimedTick;
        [SerializeField] GameObject badge;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] float lockedAlpha = 0.75f;

        Action<int> onClicked;
        Tween introTween;
        Tween pulseTween;
        bool hideIconUntilClaim;
        DailyState state;

        public int Day { get; private set; }

        void OnValidate()
        {
            if (!button) button = GetComponent<Button>();
            if (!background) background = GetComponent<Image>();
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        }

        public void StartClass(int day, DailyRewardRow row, RewardItem item, Sprite cardBackground, Action<int> clickedCallback)
        {
            Day = day;
            onClicked = clickedCallback;
            hideIconUntilClaim = row.HideIconUntilClaim;

            if (cardBackground) background.sprite = cardBackground;
            icon.sprite = item.Icon;
            amountLabel.text = row.HasLabelOverride ? row.LabelOverride : "+" + RewardAmountFormat.Short(row.Amount);

            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }

        public void Refresh(DailyState newState)
        {
            state = newState;
            bool claimed = state == DailyState.Claimed;
            bool claimable = state == DailyState.Claimable;

            claimedTick.SetActive(claimed);
            claimLabel.gameObject.SetActive(!claimed);
            badge.SetActive(claimable);
            icon.gameObject.SetActive(icon.sprite && (claimed || !hideIconUntilClaim));
            button.interactable = claimable;
            canvasGroup.alpha = state == DailyState.Locked ? lockedAlpha : 1f;

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
            introTween = transform.DOPunchScale(Vector3.one * 0.18f, 0.35f, 8, 0.8f)
                .SetUpdate(true).SetLink(gameObject).OnComplete(ApplyPulse);
        }

        // the pulse and the intro/punch both drive localScale, so only one may ever be alive
        void ApplyPulse()
        {
            introTween = null;
            pulseTween?.Kill();
            pulseTween = null;
            transform.localScale = Vector3.one;
            if (state != DailyState.Claimable) return;
            pulseTween = transform.DOScale(1.04f, 0.55f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
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
        }

        void HandleClicked() => onClicked(Day);
    }
}
