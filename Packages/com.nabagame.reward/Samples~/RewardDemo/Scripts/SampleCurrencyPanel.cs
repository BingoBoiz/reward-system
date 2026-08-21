using DG.Tweening;
using NabaGame.Core.Runtime.EventManager;
using NabaGame.Reward;
using NabaGame.UI;
using TMPro;
using UnityEngine;

namespace NabaGame.Reward.Sample
{
    public class SampleCurrencyPanel : BaseUI
    {
        [SerializeField] TMP_Text cashLabel;
        [SerializeField] TMP_Text spinLabel;
        [SerializeField] GameObject noAdsBadge;
        [SerializeField] AudioClip coinSfx;

        long shownCash;
        long shownSpin;
        Tween cashTween;
        Tween spinTween;

        public override void SetInfo()
        {
            EventManager.Instance.RemoveListener<SampleCurrencyChangedEvent>(OnCurrencyChanged);
            EventManager.Instance.AddListener<SampleCurrencyChangedEvent>(OnCurrencyChanged);
            shownCash = SampleRewardGranter.GetAmount(SampleRewardGranter.CashKey);
            shownSpin = SampleRewardGranter.GetAmount(SampleRewardGranter.SpinKey);
            cashLabel.text = RewardAmountFormat.Short(shownCash);
            spinLabel.text = shownSpin.ToString();
            noAdsBadge.SetActive(SampleRewardGranter.HasNoAds);
        }

        public void OpenPanel()
        {
            Show();
        }

        public void ClosePanel()
        {
            Hide();
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

            // only a gain is worth a coin cue; a reset drops the numbers and must stay silent
            if (coinSfx && (cash > shownCash || spin > shownSpin)) RewardHooks.PlaySfx(coinSfx);

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
