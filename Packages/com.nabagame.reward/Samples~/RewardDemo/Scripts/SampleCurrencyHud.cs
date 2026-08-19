using DG.Tweening;
using NabaGame.Core.Runtime.EventManager;
using NabaGame.Reward;
using TMPro;
using UnityEngine;

namespace NabaGame.Reward.Sample
{
    public class SampleCurrencyHud : MonoBehaviour
    {
        [SerializeField] TMP_Text cashLabel;
        [SerializeField] TMP_Text spinLabel;
        [SerializeField] GameObject noAdsBadge;

        long shownCash;
        long shownSpin;
        Tween cashTween;
        Tween spinTween;

        public void StartClass()
        {
            EventManager.Instance.RemoveListener<SampleCurrencyChangedEvent>(OnCurrencyChanged);
            EventManager.Instance.AddListener<SampleCurrencyChangedEvent>(OnCurrencyChanged);
            shownCash = SampleRewardGranter.GetAmount(SampleRewardGranter.CashKey);
            shownSpin = SampleRewardGranter.GetAmount(SampleRewardGranter.SpinKey);
            cashLabel.text = RewardAmountFormat.Short(shownCash);
            spinLabel.text = shownSpin.ToString();
            noAdsBadge.SetActive(SampleRewardGranter.HasNoAds);
        }

        void OnDestroy()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.RemoveListener<SampleCurrencyChangedEvent>(OnCurrencyChanged);
        }

        void OnCurrencyChanged(SampleCurrencyChangedEvent e)
        {
            long cash = SampleRewardGranter.GetAmount(SampleRewardGranter.CashKey);
            long spin = SampleRewardGranter.GetAmount(SampleRewardGranter.SpinKey);
            noAdsBadge.SetActive(SampleRewardGranter.HasNoAds);

            cashTween?.Kill();
            long fromCash = shownCash;
            shownCash = cash;
            cashTween = DOVirtual.Float(fromCash, cash, 0.5f, v => cashLabel.text = RewardAmountFormat.Short((long)v))
                .SetUpdate(true).SetLink(gameObject);

            spinTween?.Kill();
            long fromSpin = shownSpin;
            shownSpin = spin;
            spinTween = DOVirtual.Float(fromSpin, spin, 0.5f, v => spinLabel.text = ((long)v).ToString())
                .SetUpdate(true).SetLink(gameObject);
        }
    }
}
