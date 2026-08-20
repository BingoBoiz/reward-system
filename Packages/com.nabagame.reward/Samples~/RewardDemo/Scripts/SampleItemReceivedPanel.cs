using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NabaGame.Core.Runtime.EventManager;
using NabaGame.Reward;
using NabaGame.UI;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward.Sample
{
    public class SampleItemReceivedPanel : BaseUI
    {
        const string CongratulationTitle = "CONGRATULATION";

        const float RefDimFade = 0.07f;
        const float RefBurstStart = 0.10f;
        const float RefTitleFade = 0.13f;
        const float RefTitleFadeDur = 0.10f;
        const float RefTitleRise = 0.34f;
        const float RefTitleRiseDur = 0.30f;
        const float RefOrnamentDur = 0.38f;
        const float RefCardStart = 0.40f;
        const float RefCardDur = 0.26f;
        const float RefCardStagger = 0.07f;
        const float RefTitleFromY = 24f;
        const float RefTitleToY = 294f;
        const float RefCardFromScale = 1.9f;
        const float RefFlashStart = 0.15f;

        [Serializable]
        public struct Entry
        {
            public string Key;
            public Sprite Icon;
            public long Amount;
        }

        [SerializeField] Button tapButton;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] CanvasGroup titleGroup;
        [SerializeField] Image ornament;
        [SerializeField] GridLayoutGroup grid;
        [SerializeField] SampleItemReceivedCell cellTemplate;
        [SerializeField] Vector2 singleCellSize = new Vector2(320f, 320f);
        [SerializeField] Vector2 cellSpacing = new Vector2(26f, 22f);
        [SerializeField] Image dim;
        [SerializeField] SampleItemReceivedBurstFx burstFx;
        [SerializeField] Image whiteFlash;
        [SerializeField] CanvasGroup tapGroup;
        [SerializeField, Range(0f, 1f)] float dimAlpha = 0.95f;
        [SerializeField] AudioClip openSfx;
        [SerializeField] AudioClip landSfx;

        enum Phase : byte { Idle, Playing, AwaitTap }

        readonly List<SampleItemReceivedCell> cells = new();
        readonly List<Entry> pending = new();
        readonly List<Entry> shownEntries = new();
        readonly List<Tween> ceremonyTweens = new();

        Phase phase;
        CancellationTokenSource ceremonyCts;
        Tween tapTween;
        Tween dimTween;
        bool started;
        bool flushQueued;

        #region Init

        private void OnValidate()
        {
            if (!uiPanel) uiPanel = GetComponent<UIPanel>();
            if (!grid) grid = GetComponentInChildren<GridLayoutGroup>(true);
            if (!burstFx) burstFx = GetComponentInChildren<SampleItemReceivedBurstFx>(true);
            if (!titleGroup && titleLabel) titleGroup = titleLabel.GetComponent<CanvasGroup>();
        }

        public override void SetInfo()
        {
            if (!started)
            {
                started = true;
                cellTemplate.gameObject.SetActive(false);
            }

            tapButton.onClick.RemoveListener(HandleTap);
            tapButton.onClick.AddListener(HandleTap);

            EventManager.Instance.RemoveListener<SampleItemGrantedEvent>(OnItemGranted);
            EventManager.Instance.AddListener<SampleItemGrantedEvent>(OnItemGranted);
        }

        private void OnDestroy()
        {
            CancelCeremony();
            ceremonyCts?.Dispose();
            ceremonyCts = null;
            tapTween?.Kill();

            if (EventManager.Instance == null) return;
            EventManager.Instance.RemoveListener<SampleItemGrantedEvent>(OnItemGranted);
        }

        #endregion

        #region Grant Queue

        private void OnItemGranted(SampleItemGrantedEvent granted)
        {
            pending.Add(new Entry { Key = granted.Key, Icon = granted.Icon, Amount = granted.Amount });
            if (flushQueued) return;

            flushQueued = true;
            FlushNextFrame().Forget();
        }

        private async UniTaskVoid FlushNextFrame()
        {
            await UniTask.NextFrame(this.GetCancellationTokenOnDestroy());

            flushQueued = false;
            if (pending.Count == 0) return;

            OpenPanel(pending);
            pending.Clear();
        }

        #endregion

        #region Open & Close

        public void OpenPanel(List<Entry> entries)
        {
            if (entries == null || entries.Count == 0) return;

            CancelCeremony();
            ResetCeremonyVisuals();
            MergeEntries(entries);
            BuildCells();
            titleLabel.text = CongratulationTitle;

            Show();
            PlayCue(openSfx);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)grid.transform);

            phase = Phase.Playing;
            ceremonyCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            RunCeremony(ceremonyCts.Token).Forget();
        }

        public void ClosePanel()
        {
            CancelCeremony();
            phase = Phase.Idle;
            Hide();
        }

        private void HandleTap()
        {
            if (phase != Phase.AwaitTap) return;

            ClosePanel();
        }

        private void CancelCeremony()
        {
            ceremonyCts?.Cancel();
            ceremonyCts?.Dispose();
            ceremonyCts = null;
            for (int i = 0; i < ceremonyTweens.Count; i++) ceremonyTweens[i]?.Kill();
            ceremonyTweens.Clear();
            tapTween?.Kill();
            tapTween = null;
        }

        #endregion

        #region Cells

        private void MergeEntries(List<Entry> entries)
        {
            shownEntries.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                if (TryStack(entries[i])) continue;

                shownEntries.Add(entries[i]);
            }
        }

        private bool TryStack(Entry entry)
        {
            for (int i = 0; i < shownEntries.Count; i++)
            {
                Entry shown = shownEntries[i];
                if (shown.Key != entry.Key) continue;

                shown.Amount += entry.Amount;
                shownEntries[i] = shown;
                return true;
            }

            return false;
        }

        private void BuildCells()
        {
            while (cells.Count < shownEntries.Count)
            {
                SampleItemReceivedCell cell = Instantiate(cellTemplate, cellTemplate.transform.parent);
                cell.transform.localScale = Vector3.one;
                cells.Add(cell);
            }

            for (int i = 0; i < cells.Count; i++)
            {
                bool used = i < shownEntries.Count;
                cells[i].gameObject.SetActive(used);
                if (!used) continue;

                cells[i].SetInfo(shownEntries[i].Icon, shownEntries[i].Amount);
                cells[i].transform.localScale = Vector3.one * RefCardFromScale;
                cells[i].Group.alpha = 0f;
            }

            ApplyLayout(shownEntries.Count);
        }

        private void ApplyLayout(int count)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.spacing = cellSpacing;

            float side = count <= 1 ? singleCellSize.x : count <= 4 ? 244f : count <= 8 ? 200f : 168f;
            int rows = count <= 5 ? 1 : Mathf.CeilToInt(count / 6f);
            Vector2 box = ((RectTransform)grid.transform).rect.size;
            side = Mathf.Min(side, (box.y - cellSpacing.y * (rows - 1)) / rows);
            grid.constraintCount = Mathf.CeilToInt((float)count / rows);
            grid.cellSize = Vector2.one * side;
        }

        #endregion

        #region Ceremony

        private void ResetCeremonyVisuals()
        {
            SetDim(0f);
            tapButton.gameObject.SetActive(true);
            burstFx.gameObject.SetActive(false);
            SetImageAlpha(whiteFlash, 0f);
            tapGroup.alpha = 0f;
            tapGroup.transform.localScale = Vector3.one;
            titleLabel.transform.localScale = Vector3.one;
            titleGroup.alpha = 0f;
            RectTransform titleRect = titleLabel.rectTransform;
            titleRect.anchoredPosition = new Vector2(titleRect.anchoredPosition.x, RefTitleFromY);
            SetImageAlpha(ornament, 0f);
            ornament.transform.localScale = new Vector3(0.5f, 1f, 1f);
        }

        private async UniTask RunCeremony(CancellationToken ct)
        {
            try
            {
                await PlayRefCeremony(ct);
                phase = Phase.AwaitTap;
                ShowTapLabel();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask PlayRefCeremony(CancellationToken ct)
        {
            int count = Mathf.Min(shownEntries.Count, cells.Count);

            TweenDim(dimAlpha, RefDimFade);

            Track(DG.Tweening.DOTweenModuleUI.DOFade(titleGroup, 1f, RefTitleFadeDur).SetDelay(RefTitleFade)
                .SetUpdate(true).SetLink(titleGroup.gameObject));
            titleLabel.transform.localScale = Vector3.one * 1.05f;
            Track(DG.Tweening.DOTweenModuleUI.DOAnchorPosY(titleLabel.rectTransform, RefTitleToY, RefTitleRiseDur).SetEase(Ease.OutCubic)
                .SetDelay(RefTitleRise).SetUpdate(true).SetLink(titleLabel.gameObject));
            Track(titleLabel.transform.DOScale(1f, RefTitleRiseDur).SetEase(Ease.OutCubic)
                .SetDelay(RefTitleRise).SetUpdate(true).SetLink(titleLabel.gameObject));

            Track(ornament.transform.DOScaleX(1f, RefOrnamentDur).SetEase(Ease.OutCubic)
                .SetDelay(RefTitleRise).SetUpdate(true).SetLink(ornament.gameObject));
            FadeImage(ornament, 1f, RefOrnamentDur).SetDelay(RefTitleRise);

            Track(DOVirtual.DelayedCall(RefBurstStart, () =>
            {
                burstFx.gameObject.SetActive(true);
                burstFx.Play();
            }, false).SetUpdate(true));
            Track(DOVirtual.DelayedCall(RefFlashStart, FlashWhite, false).SetUpdate(true));

            for (int i = 0; i < count; i++)
            {
                SampleItemReceivedCell cell = cells[i];
                float delay = RefCardStart + i * RefCardStagger;
                Track(cell.transform.DOScale(1f, RefCardDur).SetEase(Ease.OutCubic).SetDelay(delay)
                    .SetUpdate(true).SetLink(cell.gameObject));
                Track(DG.Tweening.DOTweenModuleUI.DOFade(cell.Group, 1f, RefCardDur).SetEase(Ease.OutCubic).SetDelay(delay)
                    .SetUpdate(true).SetLink(cell.gameObject));
                cell.PlayFrameIntro(delay);

                Track(DOVirtual.DelayedCall(delay + RefCardDur, () => PlayCue(landSfx), false).SetUpdate(true));
            }

            float lastCardStart = RefCardStart + (count - 1) * RefCardStagger;
            await Delay(lastCardStart + RefCardDur, ct);
        }

        #endregion

        #region Bits

        private Tween Track(Tween tween)
        {
            ceremonyTweens.Add(tween);
            return tween;
        }

        private static UniTask Delay(float seconds, CancellationToken ct)
            => UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true, cancellationToken: ct);

        private void PlayCue(AudioClip clip)
        {
            if (clip != null) RewardHooks.PlaySfx(clip);
        }

        private void SetDim(float alpha)
        {
            dimTween?.Kill();
            dimTween = null;
            SetImageAlpha(dim, alpha);
        }

        private Tween FadeImage(Image image, float alpha, float duration)
        {
            return Track(DOVirtual.Float(image.color.a, alpha, duration, a => SetImageAlpha(image, a))
                .SetUpdate(true).SetLink(image.gameObject));
        }

        private void TweenDim(float alpha, float duration)
        {
            dimTween?.Kill();
            dimTween = FadeImage(dim, alpha, duration).SetEase(Ease.OutQuad)
                .OnComplete(() => dimTween = null);
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }

        private void FlashWhite()
        {
            SetImageAlpha(whiteFlash, 0f);
            Track(DOTween.Sequence()
                .Append(DOVirtual.Float(0f, 0.1f, 0.06f, a => SetImageAlpha(whiteFlash, a)))
                .Append(DOVirtual.Float(0.1f, 0f, 0.12f, a => SetImageAlpha(whiteFlash, a)))
                .SetUpdate(true).SetLink(whiteFlash.gameObject));
        }

        private void ShowTapLabel()
        {
            tapTween?.Kill();
            tapGroup.alpha = 0f;
            tapTween = DOVirtual.Float(0f, 1f, 0.25f, a => tapGroup.alpha = a)
                .SetUpdate(true).SetLink(tapGroup.gameObject)
                .OnComplete(() => tapTween = DOVirtual.Float(1f, 0.6f, 0.5f, a => tapGroup.alpha = a)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true).SetLink(tapGroup.gameObject));
        }

        #endregion

        #region Debug

        [Button, DisableInEditorMode]
        private void PreviewSingle(Sprite icon = null, long amount = 10000)
        {
            OpenPanel(new List<Entry> { new Entry { Key = "preview", Icon = icon, Amount = amount } });
        }

        [Button, DisableInEditorMode]
        private void PreviewMergeDuplicates()
        {
            OpenPanel(new List<Entry>
            {
                new Entry { Key = "cash", Amount = 100 },
                new Entry { Key = "spin", Amount = 1 },
                new Entry { Key = "cash", Amount = 900 },
                new Entry { Key = "spin", Amount = 1 },
            });
        }

        [Button, DisableInEditorMode]
        private void PreviewManyCards(Sprite icon = null, int count = 12)
        {
            List<Entry> entries = new();
            for (int i = 0; i < count; i++) entries.Add(new Entry { Key = $"debug{i}", Icon = icon, Amount = (i + 1) * 111 });

            OpenPanel(entries);
        }

        [Button, DisableInEditorMode]
        private void TestRaiseGrant(string key = "cash", long amount = 5)
        {
            EventManager.Instance.Raise(new SampleItemGrantedEvent { Key = key, Amount = amount });
        }

        #endregion
    }
}
