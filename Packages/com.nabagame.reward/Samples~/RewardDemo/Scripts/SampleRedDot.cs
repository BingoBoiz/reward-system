using DG.Tweening;
using NabaGame.Core.Runtime.EventManager;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward.Sample
{
    // which panel state this dot watches; the panels know nothing about any of this
    public enum SampleRedDotKey
    {
        None,
        DailyReward,
        LuckySpin,
        OnlineReward,
        DailyRewardCard,
        OnlineRewardCell,
    }

    // host-side red dot: listens to the package change events, reads the public panel queries,
    // and drives its own bell-ring effect. Drop it on the dot image, pick a key, done.
    public class SampleRedDot : MonoBehaviour
    {
        [SerializeField] SampleRedDotKey key;
        [SerializeField] Image icon;
        [SerializeField] bool animate = true;
        [SerializeField] float swingAngle = 20f;
        [SerializeField] float swingScale = 1.15f;
        [SerializeField] float windDuration = 0.09f;
        [SerializeField] float settleDuration = 0.5f;
        [SerializeField] float restDuration = 1.25f;

        Tween tween;
        bool lastOn;
        DailyRewardCard card;
        OnlineRewardCell cell;

        // None means the owner drives the dot through SetOn(): a dot for some other feature of
        // the game has no key this component could evaluate
        public bool IsManual => key == SampleRedDotKey.None;

        void OnValidate()
        {
            if (!icon) icon = GetComponent<Image>();
        }

        void Awake()
        {
            card = GetComponentInParent<DailyRewardCard>(true);
            cell = GetComponentInParent<OnlineRewardCell>(true);
        }

        void OnEnable()
        {
            if (IsManual)
            {
                Apply(lastOn, true);
                return;
            }

            EventManager.Instance.RemoveListener<DailyRewardChangedEvent>(OnDailyChanged);
            EventManager.Instance.AddListener<DailyRewardChangedEvent>(OnDailyChanged);
            EventManager.Instance.RemoveListener<LuckySpinChangedEvent>(OnSpinChanged);
            EventManager.Instance.AddListener<LuckySpinChangedEvent>(OnSpinChanged);
            EventManager.Instance.RemoveListener<OnlineRewardChangedEvent>(OnOnlineChanged);
            EventManager.Instance.AddListener<OnlineRewardChangedEvent>(OnOnlineChanged);
            Apply(IsOn(), true);
        }

        // a cell is instantiated before its Slot is assigned, so OnEnable read the wrong index;
        // Start runs after the whole SetInfo chain and fixes it
        void Start()
        {
            if (IsManual) return;
            Apply(IsOn(), true);
        }

        void OnDisable()
        {
            if (!IsManual && EventManager.Instance != null)
            {
                EventManager.Instance.RemoveListener<DailyRewardChangedEvent>(OnDailyChanged);
                EventManager.Instance.RemoveListener<LuckySpinChangedEvent>(OnSpinChanged);
                EventManager.Instance.RemoveListener<OnlineRewardChangedEvent>(OnOnlineChanged);
            }

            KillTween();
        }

        public void SetOn(bool on)
        {
            Apply(on, false);
        }

        void OnDailyChanged(DailyRewardChangedEvent e) => Apply(IsOn(), false);

        void OnSpinChanged(LuckySpinChangedEvent e) => Apply(IsOn(), false);

        void OnOnlineChanged(OnlineRewardChangedEvent e) => Apply(IsOn(), false);

        bool IsOn()
        {
            SampleUIRoot ui = SampleUIRoot.Instance;
            if (!ui) return false;

            switch (key)
            {
                case SampleRedDotKey.DailyReward:
                    return ui.dailyRewardPanel && ui.dailyRewardPanel.ClaimableCount > 0;

                case SampleRedDotKey.LuckySpin:
                    return ui.luckySpinPanel && ui.luckySpinPanel.FreeSpinReady && !ui.luckySpinPanel.IsSpinning;

                case SampleRedDotKey.OnlineReward:
                    return ui.onlineRewardPanel && ui.onlineRewardPanel.HasClaimable;

                case SampleRedDotKey.DailyRewardCard:
                    return card && ui.dailyRewardPanel && ui.dailyRewardPanel.GetState(card.Day) == DailyState.Claimable;

                case SampleRedDotKey.OnlineRewardCell:
                    return cell && ui.onlineRewardPanel && ui.onlineRewardPanel.GetState(cell.Slot) == OnlineSlotState.Claimable;

                default:
                    return false;
            }
        }

        void Apply(bool on, bool force)
        {
            if (!icon) return;
            if (on == lastOn && !force) return;
            lastOn = on;

            icon.enabled = on;
            KillTween();
            if (!on || !animate) return;

            ApplySpritePivot();

            // bell-ring attractor: a fast tilt+grow strike, an elastic settle back to rest, then
            // stillness - the rest period is what makes it read as an alert instead of a metronome
            tween = DOTween.Sequence()
                .Append(transform.DOLocalRotate(new Vector3(0f, 0f, -swingAngle), windDuration).SetEase(Ease.OutQuad))
                .Join(transform.DOScale(swingScale, windDuration).SetEase(Ease.OutQuad))
                .Append(transform.DOLocalRotate(Vector3.zero, settleDuration).SetEase(Ease.OutElastic, 1.2f, 0.25f))
                .Join(transform.DOScale(1f, settleDuration * 0.6f).SetEase(Ease.OutQuad))
                .AppendInterval(restDuration)
                .SetLoops(-1).SetUpdate(true).SetLink(gameObject);
        }

        // rotation/scale must hinge on the art's authored pivot, but a RectTransform rotates around
        // its own pivot - so copy the sprite pivot over before tweening
        void ApplySpritePivot()
        {
            RectTransform rect = transform as RectTransform;
            if (rect && icon.sprite) rect.pivot = icon.sprite.pivot / icon.sprite.rect.size;
        }

        void KillTween()
        {
            tween?.Kill();
            tween = null;
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
        }

        [Button, DisableInEditorMode]
        void PreviewToggle()
        {
            Apply(!lastOn, true);
        }
    }
}
