using System.Collections.Generic;
using NabaGame.Core.Runtime.EventManager;
using NabaGame.UI;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward
{
    public class DailyRewardPanel : BaseUI
    {
        [SerializeField] Button closeButton;
        [SerializeField] Button openAllButton;
        [SerializeField] TMP_Text openAllLabel;
        [SerializeField] RectTransform cardsRoot;
        [SerializeField] DailyRewardCard cardTemplate;
        [SerializeField] Sprite[] cardBackgrounds;
        [SerializeField] float cardSpacing = 289f;
        [SerializeField] float cardStaggerDelay = 0.04f;
        [SerializeField] Color openAllDisabledTint = new Color(0.68f, 0.68f, 0.68f, 1f);
        [SerializeField] AudioClip buttonSfx;

        readonly List<DailyRewardCard> cards = new List<DailyRewardCard>();
        DailyRewardManager manager;
        RewardHooks hooks;
        bool started;

        public void StartClass(DailyRewardManager dailyRewardManager, RewardHooks rewardHooks)
        {
            manager = dailyRewardManager;
            hooks = rewardHooks;
            if (cardBackgrounds.Length != manager.DayCount)
                throw new System.InvalidOperationException($"DailyRewardPanel: cardBackgrounds has {cardBackgrounds.Length} sprites, expected {manager.DayCount}");

            if (!started)
            {
                started = true;
                cardTemplate.gameObject.SetActive(false);
                for (int i = 0; i < manager.DayCount; i++)
                {
                    DailyRewardCard card = Instantiate(cardTemplate, cardsRoot);
                    card.gameObject.SetActive(true);
                    card.name = $"Card_{i + 1}";
                    ((RectTransform)card.transform).anchoredPosition =
                        new Vector2((i - (manager.DayCount - 1) * 0.5f) * cardSpacing, 0f);
                    cards.Add(card);
                }
            }

            closeButton.onClick.RemoveListener(ClosePanel);
            closeButton.onClick.AddListener(ClosePanel);
            openAllButton.onClick.RemoveListener(OnOpenAll);
            openAllButton.onClick.AddListener(OnOpenAll);

            EventManager.Instance.RemoveListener<DailyRewardChangedEvent>(OnDailyRewardChanged);
            EventManager.Instance.AddListener<DailyRewardChangedEvent>(OnDailyRewardChanged);

            for (int i = 0; i < cards.Count; i++) cards[i].StartClass(i, manager.GetRow(i), manager.GetItem(i), cardBackgrounds[i], OnCardClicked);
            RefreshAll();
        }

        void OnDestroy()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.RemoveListener<DailyRewardChangedEvent>(OnDailyRewardChanged);
        }

        public void OpenPanel()
        {
            Show();
            RefreshAll();
            for (int i = 0; i < cards.Count; i++) cards[i].PlayIntro(i * cardStaggerDelay);
        }

        public void ClosePanel()
        {
            if (buttonSfx) hooks.PlaySfx(buttonSfx);
            Hide();
        }

        void RefreshAll()
        {
            for (int i = 0; i < cards.Count; i++) cards[i].Refresh(manager.GetState(i));
            bool claimable = manager.ClaimableCount > 0;
            openAllButton.interactable = claimable;
            openAllButton.image.color = claimable ? Color.white : openAllDisabledTint;
            openAllLabel.text = claimable ? "OPEN ALL" : "COME BACK TOMORROW";
        }

        void OnCardClicked(int day) => TryClaim();

        void OnOpenAll() => TryClaim();

        void TryClaim()
        {
            int day = manager.StreakDay;
            if (buttonSfx) hooks.PlaySfx(buttonSfx);
            if (manager.Claim()) cards[day].PlayClaimedPunch();
        }

        void OnDailyRewardChanged(DailyRewardChangedEvent e)
        {
            if (IsVisible()) RefreshAll();
        }

        [Button, DisableInEditorMode]
        void PreviewOpen() => OpenPanel();

        [Button, DisableInEditorMode]
        void PreviewClose() => ClosePanel();

        [Button, DisableInEditorMode]
        void PreviewClaimToday() => TryClaim();

        [Button, DisableInEditorMode]
        void PreviewSetStreakDay(int day) => manager.SetStreakDay(day);

        [Button, DisableInEditorMode]
        void PreviewResetAndReopen()
        {
            manager.ResetProfile();
            Hide();
            OpenPanel();
        }
    }
}
