using DG.Tweening;
using NabaGame.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NabaGame.Reward
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button), typeof(UIButton), typeof(UIElement))]
    public class RewardButtonAttentionFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] UIElement visibilityOwner;
        [SerializeField] RectTransform icon;
        [SerializeField, Min(0.1f)] float intervalSeconds = 4.5f;
        [SerializeField, Min(0f)] float hopDistance = 10f;
        [SerializeField, Range(1f, 1.3f)] float iconScale = 1.12f;
        [SerializeField, Range(0f, 30f)] float iconTilt = 12f;

        Button button;
        RectTransform body;
        Vector2 bodyPosition;
        Vector3 bodyScale;
        Vector3 iconBaseScale;
        Vector3 iconBaseRotation;
        Tween delayTween;
        Sequence attentionSequence;
        bool attentionRequested;

        void Awake()
        {
            button = GetComponent<Button>();
            body = transform as RectTransform;
            if (!visibilityOwner)
            {
                BaseUI panel = GetComponentInParent<BaseUI>();
                if (panel) visibilityOwner = panel.GetComponent<UIElement>();
            }

            bodyPosition = body ? body.anchoredPosition : Vector2.zero;
            bodyScale = body ? body.localScale : Vector3.one;
            iconBaseScale = icon ? icon.localScale : Vector3.one;
            iconBaseRotation = icon ? icon.localEulerAngles : Vector3.zero;
        }

        void OnEnable()
        {
            if (!visibilityOwner) return;
            visibilityOwner.OnInAnimationsStart.RemoveListener(OnOwnerInStart);
            visibilityOwner.OnInAnimationsStart.AddListener(OnOwnerInStart);
            visibilityOwner.OnInAnimationsFinish.RemoveListener(OnOwnerInFinish);
            visibilityOwner.OnInAnimationsFinish.AddListener(OnOwnerInFinish);
            visibilityOwner.OnOutAnimationsStart.RemoveListener(OnOwnerOutStart);
            visibilityOwner.OnOutAnimationsStart.AddListener(OnOwnerOutStart);
        }

        void OnDisable()
        {
            if (visibilityOwner)
            {
                visibilityOwner.OnInAnimationsStart.RemoveListener(OnOwnerInStart);
                visibilityOwner.OnInAnimationsFinish.RemoveListener(OnOwnerInFinish);
                visibilityOwner.OnOutAnimationsStart.RemoveListener(OnOwnerOutStart);
            }

            StopAttention(true);
        }

        void OnDestroy()
        {
            StopAttention(true);
        }

        public void SetAttention(bool enabled)
        {
            if (attentionRequested == enabled)
            {
                if (enabled && delayTween == null && attentionSequence == null) ScheduleAttention();
                return;
            }

            attentionRequested = enabled;
            if (enabled) ScheduleAttention();
            else StopAttention(true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            StopAttention(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ScheduleAttention();
        }

        void OnOwnerInStart()
        {
            StopAttention(true);
        }

        void OnOwnerInFinish()
        {
            ScheduleAttention();
        }

        void OnOwnerOutStart()
        {
            StopAttention(true);
        }

        void ScheduleAttention()
        {
            StopAttention(false);
            if (!attentionRequested || !isActiveAndEnabled || !button || !button.interactable) return;
            if (visibilityOwner && !visibilityOwner.isVisible) return;

            delayTween = DOVirtual.DelayedCall(intervalSeconds, PlayAttention)
                .SetUpdate(true).SetLink(gameObject);
        }

        void PlayAttention()
        {
            delayTween = null;
            if (!attentionRequested || !isActiveAndEnabled || !button || !button.interactable)
            {
                ScheduleAttention();
                return;
            }

            if (visibilityOwner && !visibilityOwner.isVisible) return;
            ResetVisuals();

            attentionSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(body.DOAnchorPosY(bodyPosition.y + hopDistance, 0.16f).SetEase(Ease.OutQuad))
                .Join(body.DOScale(new Vector3(bodyScale.x * 1.04f, bodyScale.y * 0.96f, bodyScale.z), 0.16f).SetEase(Ease.OutQuad))
                .Append(body.DOAnchorPos(bodyPosition, 0.28f).SetEase(Ease.OutBack))
                .Join(body.DOScale(bodyScale, 0.28f).SetEase(Ease.OutBack));

            if (icon)
            {
                attentionSequence.Insert(0.04f, icon.DOScale(iconBaseScale * iconScale, 0.14f).SetEase(Ease.OutBack));
                attentionSequence.Insert(0.04f, icon.DOLocalRotate(iconBaseRotation + Vector3.forward * -iconTilt, 0.14f).SetEase(Ease.OutQuad));
                attentionSequence.Insert(0.18f, icon.DOLocalRotate(iconBaseRotation + Vector3.forward * (iconTilt * 0.55f), 0.12f).SetEase(Ease.InOutSine));
                attentionSequence.Insert(0.18f, icon.DOScale(iconBaseScale, 0.24f).SetEase(Ease.OutBack));
                attentionSequence.Insert(0.30f, icon.DOLocalRotate(iconBaseRotation, 0.16f).SetEase(Ease.OutBack));
            }

            attentionSequence.OnComplete(() =>
            {
                attentionSequence = null;
                ResetVisuals();
                ScheduleAttention();
            });
        }

        void StopAttention(bool reset)
        {
            bool wasAnimating = attentionSequence != null && attentionSequence.IsActive();
            delayTween?.Kill();
            attentionSequence?.Kill();
            delayTween = null;
            attentionSequence = null;
            if (reset && wasAnimating) ResetVisuals();
        }

        void ResetVisuals()
        {
            if (body)
            {
                body.anchoredPosition = bodyPosition;
                body.localScale = bodyScale;
            }

            if (!icon) return;
            icon.localScale = iconBaseScale;
            icon.localEulerAngles = iconBaseRotation;
        }
    }
}
