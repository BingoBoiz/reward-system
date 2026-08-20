using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward.Sample
{
    public class SampleItemReceivedBurstFx : MonoBehaviour
    {
        [SerializeField] Image glowOuter;
        [SerializeField] Image glowInner;
        [SerializeField] Image starA;
        [SerializeField] Image starB;
        [SerializeField] Image[] rays;
        [SerializeField] Image shaftL;
        [SerializeField] Image shaftR;
        [SerializeField] Image beam;

        readonly List<Tween> tweens = new();

        public RectTransform Rect => (RectTransform)transform;

        private void OnValidate()
        {
            if (!glowOuter) glowOuter = FindChild("GlowOuter");
            if (!glowInner) glowInner = FindChild("GlowInner");
            if (!starA) starA = FindChild("StarA");
            if (!starB) starB = FindChild("StarB");
            if (!shaftL) shaftL = FindChild("ShaftL");
            if (!shaftR) shaftR = FindChild("ShaftR");
            if (!beam) beam = FindChild("Beam");
            if (rays == null || rays.Length == 0)
            {
                List<Image> found = new();
                for (int i = 0; i < 6; i++)
                {
                    Image ray = FindChild($"Ray{i}");
                    if (ray) found.Add(ray);
                }
                rays = found.ToArray();
            }
        }

        private Image FindChild(string childName)
        {
            Transform child = transform.Find(childName);
            return child ? child.GetComponent<Image>() : null;
        }

        public void Play()
        {
            Kill();

            Flare(glowOuter, 0f, 0.41f, 0.08f, 1.05f, 0.30f, 1.32f, 0.55f, 0.5f, 0.95f, 0.8f);
            Flare(glowInner, 0f, 0.36f, 0.1f, 1.1f, 0.28f, 1.1f, 0.28f, 0.35f, 1f, 1f);
            Star(starA, 0.01f, 0.32f, 0.04f, 1f, 0.34f, 1.16f, 0.6f, 0.2f, 1f, 0.9f, -14f, 17f);
            Star(starB, 0.022f, 0.28f, 0.05f, 1f, 0.36f, 1f, 0.36f, 0.22f, 0.95f, 0.95f, 31f, 58f);

            for (int i = 0; i < rays.Length; i++) Ray(rays[i], 0.018f + i * 0.008f);

            Beam();
            Shaft(shaftL, 0.05f);
            Shaft(shaftR, 0.08f);

            tweens.Add(DOVirtual.DelayedCall(0.8f, () => gameObject.SetActive(false), false)
                .SetUpdate(true).SetLink(gameObject));
        }

        private void Flare(Image image, float delay, float duration, float a, float b, float ka, float c, float kb, float d,
            float pa, float pb)
        {
            if (!image) return;

            Transform t = image.transform;
            t.localScale = Vector3.one * a;
            SetAlpha(image, 0f);

            Sequence seq = DOTween.Sequence().SetDelay(delay).SetUpdate(true).SetLink(gameObject);
            seq.Append(t.DOScale(b, duration * ka).SetEase(Ease.OutQuad))
                .Join(image.DOFade(pa, duration * ka).SetEase(Ease.OutQuad));
            if (kb > ka)
                seq.Append(t.DOScale(c, duration * (kb - ka)).SetEase(Ease.OutQuad))
                    .Join(image.DOFade(pb, duration * (kb - ka)).SetEase(Ease.OutQuad));
            seq.Append(t.DOScale(d, duration * (1f - kb)).SetEase(Ease.OutQuad))
                .Join(image.DOFade(0f, duration * (1f - kb)).SetEase(Ease.OutQuad));
            tweens.Add(seq);
        }

        private void Star(Image image, float delay, float duration, float a, float b, float ka, float c, float kb, float d,
            float pa, float pb, float rotFrom, float rotTo)
        {
            Flare(image, delay, duration, a, b, ka, c, kb, d, pa, pb);
            if (!image) return;

            Transform t = image.transform;
            t.localRotation = Quaternion.Euler(0f, 0f, rotFrom);
            tweens.Add(t.DORotate(new Vector3(0f, 0f, rotTo), duration).SetDelay(delay).SetEase(Ease.Linear)
                .SetUpdate(true).SetLink(gameObject));
        }

        private void Ray(Image image, float delay)
        {
            if (!image) return;

            const float Duration = 0.29f;
            Transform t = image.transform;
            t.localScale = new Vector3(0.15f, 0.1f, 1f);
            SetAlpha(image, 0f);

            Sequence seq = DOTween.Sequence().SetDelay(delay).SetUpdate(true).SetLink(gameObject);
            seq.Append(t.DOScaleX(1f, Duration * 0.35f).SetEase(Ease.OutQuad))
                .Join(t.DOScaleY(1.05f, Duration * 0.35f).SetEase(Ease.OutQuad))
                .Join(image.DOFade(0.9f, Duration * 0.35f).SetEase(Ease.OutQuad));
            seq.Append(t.DOScaleX(0.5f, Duration * 0.65f).SetEase(Ease.OutQuad))
                .Join(t.DOScaleY(1.3f, Duration * 0.65f).SetEase(Ease.OutQuad))
                .Join(image.DOFade(0f, Duration * 0.65f).SetEase(Ease.OutQuad));
            tweens.Add(seq);
        }

        private void Beam()
        {
            if (!beam) return;

            const float Duration = 0.41f;
            Transform t = beam.transform;
            t.localScale = new Vector3(0.15f, 0.4f, 1f);
            SetAlpha(beam, 0f);

            Sequence seq = DOTween.Sequence().SetDelay(0.05f).SetUpdate(true).SetLink(gameObject);
            seq.Append(t.DOScaleX(1.1f, Duration * 0.28f).SetEase(Ease.OutQuart))
                .Join(t.DOScaleY(1f, Duration * 0.28f).SetEase(Ease.OutQuart))
                .Join(beam.DOFade(1f, Duration * 0.28f).SetEase(Ease.OutQuart));
            seq.Append(t.DOScaleX(1.75f, Duration * 0.32f).SetEase(Ease.OutQuart))
                .Join(t.DOScaleY(0.6f, Duration * 0.32f).SetEase(Ease.OutQuart))
                .Join(beam.DOFade(0.55f, Duration * 0.32f).SetEase(Ease.OutQuart));
            seq.Append(t.DOScaleX(2.4f, Duration * 0.4f).SetEase(Ease.OutQuart))
                .Join(t.DOScaleY(0.3f, Duration * 0.4f).SetEase(Ease.OutQuart))
                .Join(beam.DOFade(0f, Duration * 0.4f).SetEase(Ease.OutQuart));
            tweens.Add(seq);
        }

        private void Shaft(Image image, float delay)
        {
            if (!image) return;

            const float Duration = 0.56f;
            Transform t = image.transform;
            t.localScale = new Vector3(0.5f, 0.25f, 1f);
            SetAlpha(image, 0f);

            Sequence seq = DOTween.Sequence().SetDelay(delay).SetUpdate(true).SetLink(gameObject);
            seq.Append(t.DOScale(new Vector3(1f, 1f, 1f), Duration * 0.3f).SetEase(Ease.OutQuad))
                .Join(image.DOFade(0.4f, Duration * 0.3f).SetEase(Ease.OutQuad));
            seq.Append(t.DOScale(new Vector3(1.25f, 1.15f, 1f), Duration * 0.7f).SetEase(Ease.OutQuad))
                .Join(image.DOFade(0f, Duration * 0.7f).SetEase(Ease.OutQuad));
            tweens.Add(seq);
        }

        private static void SetAlpha(Image image, float alpha)
        {
            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }

        private void Kill()
        {
            for (int i = 0; i < tweens.Count; i++) tweens[i]?.Kill();
            tweens.Clear();
        }

        private void OnDisable()
        {
            Kill();
        }
    }
}
