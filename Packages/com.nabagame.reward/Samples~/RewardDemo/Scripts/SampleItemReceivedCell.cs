using DG.Tweening;
using NabaGame.Reward;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward.Sample
{
    public class SampleItemReceivedCell : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text amountLabel;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] Image frameBar;
        [SerializeField] Image framePad;
        [SerializeField] Image frameGlow;
        [SerializeField] Image frameGhost;
        [SerializeField] Color frameTint = new Color(0.725f, 0.768f, 0.807f, 1f);

        Sequence frameSeq;

        public CanvasGroup Group => group;

        private void OnValidate()
        {
            if (!group) group = GetComponent<CanvasGroup>();
        }

        public void SetInfo(Sprite iconSprite, long amount)
        {
            KillTweens();

            icon.sprite = iconSprite;
            icon.enabled = iconSprite;
            icon.transform.localScale = Vector3.one;
            group.alpha = 1f;

            SetTint(frameBar, frameTint, 0f);
            frameBar.transform.localScale = new Vector3(0.2f, 1f, 1f);
            SetTint(framePad, frameTint, 0f);
            SetTint(frameGlow, frameTint, 0f);
            SetTint(frameGhost, Color.white, 0f);
            frameGhost.transform.localScale = Vector3.one;

            amountLabel.gameObject.SetActive(amount > 1);
            amountLabel.text = "x" + RewardAmountFormat.Short(amount);
            amountLabel.transform.localScale = Vector3.one;

            nameLabel.gameObject.SetActive(false);
        }

        public void PlayFrameIntro(float delay)
        {
            frameSeq?.Kill();

            frameSeq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            frameGhost.transform.localScale = Vector3.one * 2.4f;
            frameSeq.Insert(delay, frameGhost.transform.DOScale(1f, 0.26f).SetEase(Ease.OutCubic));
            frameSeq.Insert(delay, frameGhost.DOFade(0.35f, 0.08f));
            frameSeq.Insert(delay + 0.08f, frameGhost.DOFade(0f, 0.18f));
            frameSeq.Insert(delay + 0.12f, frameBar.transform.DOScaleX(1f, 0.42f).SetEase(Ease.OutExpo));
            frameSeq.Insert(delay + 0.12f, frameBar.DOFade(1f, 0.19f));
            frameSeq.Insert(delay + 0.31f, frameBar.DOFade(0.75f, 0.23f));
            frameSeq.Insert(delay + 0.09f, frameGlow.DOFade(0.45f, 0.18f));
            frameSeq.Insert(delay + 0.27f, frameGlow.DOFade(0.16f, 0.52f));
            frameSeq.Insert(delay + 0.11f, framePad.DOFade(0.6f, 0.18f));
            frameSeq.Insert(delay + 0.29f, framePad.DOFade(0.2f, 0.42f));
        }

        private static void SetTint(Image image, Color color, float alpha)
        {
            color.a = alpha;
            image.color = color;
        }

        private void KillTweens()
        {
            frameSeq?.Kill();
            frameSeq = null;
        }

        private void OnDisable()
        {
            KillTweens();
            transform.localScale = Vector3.one;
            icon.transform.localScale = Vector3.one;
        }
    }
}
