using System;
using System.Collections.Generic;
using NabaGame.Core.Runtime.EventManager;
using NabaGame.UI;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward
{
    public enum DailyState
    {
        Locked,
        Claimable,
        Claimed
    }

    public class DailyRewardPanel : BaseUI
    {
        public const string SaveKey = "NabaReward.Daily";
        public const int ProfileVersion = 1;
        public const string OpenAllPlacement = "DailyReward_OpenAll";

        [ShowInInspector, ReadOnly, TableList, TabGroup("Tabs", "Rows")]
        List<DailyRewardRow> rows = new List<DailyRewardRow>(); // dữ liệu do manager truyền vào

        [SerializeField, TabGroup("Tabs", "Config")] bool openAllUseAds = true; // true xài ads, false xài IAP
        [SerializeField, TabGroup("Tabs", "Config")] int openAllAdsRequired; // số ads để mở toàn bộ
        [SerializeField, TabGroup("Tabs", "Config")] string openAllIapProductId = ""; // id gói IAP mở toàn bộ
        [SerializeField, TabGroup("Tabs", "Config")] string openAllIapPriceText = ""; // giá hiển thị trên nút IAP
        [SerializeField, TabGroup("Tabs", "Config")] string trackEventName = "daily_reward"; // tên event analytics, để trống là tắt

        [SerializeField, TabGroup("Tabs", "UI")] Button closeButton; // nút đóng panel
        [SerializeField, TabGroup("Tabs", "UI")] GameObject comeBackLabel; // chữ hiện khi nhận hết
        [SerializeField, FoldoutGroup("Tabs/UI/Open All")] Button openAllAdsButton; // nút xem ads mở toàn bộ
        [SerializeField, FoldoutGroup("Tabs/UI/Open All")] TMP_Text openAllAdsCountLabel; // chữ đếm số ads đã xem
        [SerializeField, FoldoutGroup("Tabs/UI/Open All")] Button openAllIapButton; // nút mua IAP mở toàn bộ
        [SerializeField, FoldoutGroup("Tabs/UI/Open All")] TMP_Text openAllIapPriceLabel; // chữ giá tiền trên nút
        [SerializeField, FoldoutGroup("Tabs/UI/Cards")] List<DailyRewardCard> cards = new (); // thẻ ngày xếp sẵn theo thứ tự

        [SerializeField, TabGroup("Tabs", "FX")] UIElement[] animatedButtons = Array.Empty<UIElement>(); // nút hiện theo thứ tự
        [SerializeField, FoldoutGroup("Tabs/FX/Attention")] RewardButtonAttentionFx openAllAdsAttention; // nhún nút ads đặc biệt
        [SerializeField, FoldoutGroup("Tabs/FX/Attention")] RewardButtonAttentionFx openAllIapAttention; // nhún nút IAP đặc biệt
        [SerializeField, TabGroup("Tabs", "FX")] float cardStaggerDelay = 0.04f; // trễ hiện lần lượt từng thẻ
        [SerializeField, TabGroup("Tabs", "FX")] AudioClip openSfx; // tiếng lúc mở panel
        [SerializeField, TabGroup("Tabs", "FX")] AudioClip closeSfx; // tiếng lúc đóng panel
        [SerializeField, TabGroup("Tabs", "FX")] AudioClip buttonSfx; // tiếng bấm nút chung

        RewardFlow flow;
        TimeScheduler.Handle rolloverHandle;

        [ShowInInspector, TabGroup("Tabs", "Profile"), HideLabel, InlineProperty]
        public DailyRewardProfile Profile { get; private set; }

        #region API

        // one-time init with the host's data; call from Start() at boot, the panel stays hidden until OpenPanel()
        public void SetInfo(List<DailyRewardRow> hostRows)
        {
            if (hostRows == null || hostRows.Count == 0) throw new InvalidOperationException("DailyRewardPanel: rows is null or empty");
            rows = hostRows;
            flow = new RewardFlow(this, trackEventName);
            DailyRewardRow.Warn(rows, this);
            WarnConfig();

            Profile = RewardProfileStore.Load<DailyRewardProfile>(SaveKey, ProfileVersion);
            if (Profile.StreakDay < 0 || Profile.StreakDay > rows.Count)
            {
                Debug.LogError($"[DailyRewardPanel] saved StreakDay {Profile.StreakDay} is outside 0..{rows.Count}, reset to 0", this);
                Profile.StreakDay = 0;
                RewardProfileStore.Save(SaveKey, Profile);
            }

            ScheduleRollover();
            ResetWeekIfElapsed();
            BindCards();
            BindUi();
            RefreshAll();

            // the initial state is a state change too: host listeners (red dots, HUD) start out empty
            RaiseChanged();
        }

        public void OpenPanel()
        {
            if (Profile == null)
            {
                Debug.LogError("[DailyRewardPanel] OpenPanel before SetInfo(rows); call SetInfo from your boot first", this);
                return;
            }

            Show();
            if (openSfx) RewardHooks.PlaySfx(openSfx);
            RefreshAll();
            RewardUi.PlayIntro(animatedButtons);
            RewardTrack.Send(trackEventName, RewardTrack.Open, "1");
            for (int i = 0; i < cards.Count && i < rows.Count; i++) if (cards[i]) cards[i].PlayIntro(i * cardStaggerDelay);
        }

        public void ClosePanel()
        {
            if (closeSfx) RewardHooks.PlaySfx(closeSfx);
            else if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            if (openAllAdsAttention) openAllAdsAttention.SetAttention(false);
            if (openAllIapAttention) openAllIapAttention.SetAttention(false);
            Hide();
            EventManager.Instance.Raise(new DailyRewardPanelClosedEvent());
        }

        public int DayCount => rows.Count;

        public int StreakDay => Profile == null ? 0 : Profile.StreakDay;

        // red-dot query: 1 while today's card is still unclaimed; the panel draws no dot itself, the host does (see SampleRedDot)
        public int ClaimableCount => Profile != null && Profile.StreakDay < DayCount && Profile.LastClaimDateUtc != RewardClock.TodayUtc ? 1 : 0;

        public int UnopenedCount => DayCount - StreakDay;

        public DailyState GetState(int day)
        {
            if (Profile == null || day < 0 || day >= rows.Count) return DailyState.Locked;
            if (day < Profile.StreakDay) return DailyState.Claimed;
            if (day == Profile.StreakDay && ClaimableCount > 0) return DailyState.Claimable;
            return DailyState.Locked;
        }

        public void ResetProfile()
        {
            RewardProfileStore.Clear(SaveKey);
            Profile = RewardProfileStore.Load<DailyRewardProfile>(SaveKey, ProfileVersion);
            RaiseChanged();
        }

        #endregion

        #region Logic

        bool OpenAllIapEnabled => !string.IsNullOrEmpty(openAllIapProductId);

        // the store's localized price wins; the authored string is the fallback until the store is up
        string IapPriceText
        {
            get
            {
                string store = RewardHooks.GetIapPrice(openAllIapProductId);
                return string.IsNullOrEmpty(store) ? openAllIapPriceText : store;
            }
        }

        void WarnConfig()
        {
            if (openAllUseAds && openAllAdsRequired <= 0) Debug.LogWarning($"[DailyRewardPanel] openAllUseAds is on but openAllAdsRequired is {openAllAdsRequired}, OPEN ALL stays off", this);
            if (!openAllUseAds && !OpenAllIapEnabled) Debug.LogWarning("[DailyRewardPanel] openAllUseAds is off but openAllIapProductId is empty, OPEN ALL stays off", this);
            if (!openAllUseAds && OpenAllIapEnabled && string.IsNullOrEmpty(openAllIapPriceText)) Debug.LogWarning("[DailyRewardPanel] openAllIapProductId is set but openAllIapPriceText is empty", this);
            if (!string.IsNullOrEmpty(trackEventName) && !RewardTrack.IsValidEventName(trackEventName)) Debug.LogWarning($"[DailyRewardPanel] trackEventName '{trackEventName}' is not a valid analytics event name, the events will be dropped", this);
        }

        void ScheduleRollover()
        {
            TimeScheduler.Cancel(ref rolloverHandle);
            rolloverHandle = TimeScheduler.Schedule(RewardClock.NextUtcMidnightMs, OnRollover);
        }

        void OnRollover()
        {
            rolloverHandle = null;
            ScheduleRollover();
            ResetWeekIfElapsed();
            RaiseChanged();
        }

        void ResetWeekIfElapsed()
        {
            if (Profile.StreakDay != DayCount || Profile.LastClaimDateUtc == RewardClock.TodayUtc) return;
            Profile.StreakDay = 0;
            RewardProfileStore.Save(SaveKey, Profile);
        }

        void ClaimDay(int day, bool withSfx)
        {
            DailyRewardRow row = rows[day];
            Profile.StreakDay = day + 1;
            Profile.LastClaimDateUtc = RewardClock.TodayUtc;
            RewardProfileStore.Save(SaveKey, Profile);

            if (withSfx && row.ClaimSfx) RewardHooks.PlaySfx(row.ClaimSfx);

            // the audit log is mandatory: a null OnClaimed grants nothing and only this line betrays it
            Debug.Log($"[DailyRewardPanel] claimed day {day + 1}: {row.Key} x{row.Amount}");
            RewardTrack.Send(trackEventName, RewardTrack.Claim, row.Key);
            if (row.OnClaimed != null) row.OnClaimed(row);
            else Debug.LogError($"[DailyRewardPanel] rows[{day}].OnClaimed is null, '{row.Key}' x{row.Amount} was NOT granted", this);

            if (IsVisible() && day < cards.Count && cards[day]) cards[day].PlayClaimedPunch();
        }

        void TryClaim()
        {
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            int day = Profile.StreakDay;
            if (GetState(day) != DailyState.Claimable) return;
            ClaimDay(day, withSfx: true);
            RaiseChanged();
        }

        bool RequestOpenAllAds()
        {
            if (!openAllUseAds || openAllAdsRequired <= 0 || UnopenedCount == 0) return false;
            RewardTrack.Send(trackEventName, RewardTrack.OpenAll, "ads");
            return flow.ShowAd(OpenAllPlacement, OnOpenAllAdWatched, OnMonetizeFailed);
        }

        void OnOpenAllAdWatched()
        {
            if (Profile.OpenAllAdsWatched + 1 < openAllAdsRequired)
            {
                Profile.OpenAllAdsWatched++;
                RewardProfileStore.Save(SaveKey, Profile);
                RaiseChanged();
                return;
            }

            OpenAll();
        }

        bool RequestOpenAllIap()
        {
            if (openAllUseAds || !OpenAllIapEnabled || UnopenedCount == 0) return false;
            RewardTrack.Send(trackEventName, RewardTrack.OpenAll, "iap");
            return flow.Purchase(openAllIapProductId, OpenAll, OnMonetizeFailed);
        }

        void OnMonetizeFailed(string message)
        {
            Debug.Log($"[DailyRewardPanel] open-all did not go through: {message}");
            RewardHooks.ShowMessage(message);
            RefreshAll();
        }

        void OpenAll()
        {
            int firstDay = Profile.StreakDay;
            if (firstDay >= rows.Count) return;

            if (rows[firstDay].ClaimSfx) RewardHooks.PlaySfx(rows[firstDay].ClaimSfx);
            Profile.OpenAllAdsWatched = 0;
            for (int day = firstDay; day < rows.Count; day++) ClaimDay(day, withSfx: false);
            RaiseChanged();
        }

        void RaiseChanged()
        {
            if (IsVisible()) RefreshAll();
            EventManager.Instance.Raise(new DailyRewardChangedEvent());
        }

        void OnDestroy()
        {
            TimeScheduler.Cancel(ref rolloverHandle);
        }

        #endregion

        #region UI

        void BindCards()
        {
            if (cards.Count == 0)
            {
                Debug.LogError("[DailyRewardPanel] cards list is empty, no cards will be shown", this);
                return;
            }

            if (cards.Count != rows.Count)
                Debug.LogWarning($"[DailyRewardPanel] prefab has {cards.Count} cards for {rows.Count} rows, extras stay hidden", this);

            for (int i = 0; i < cards.Count; i++)
            {
                if (!cards[i]) continue;
                bool used = i < rows.Count;
                cards[i].gameObject.SetActive(used);
                if (used) cards[i].SetInfo(i, rows[i], OnCardClicked);
            }
        }

        void BindUi()
        {
            RewardUi.Bind(closeButton, ClosePanel);
            RewardUi.Bind(openAllAdsButton, OnOpenAllAds);
            RewardUi.Bind(openAllIapButton, OnOpenAllIap);
        }

        void RefreshAll()
        {
            if (Profile == null) return;
            for (int i = 0; i < cards.Count && i < rows.Count; i++) if (cards[i]) cards[i].Refresh(GetState(i));

            bool adsOn = openAllUseAds && openAllAdsRequired > 0 && UnopenedCount > 0;
            bool iapOn = !openAllUseAds && OpenAllIapEnabled && UnopenedCount > 0;

            bool busy = flow != null && flow.Busy;

            if (openAllAdsButton)
            {
                openAllAdsButton.gameObject.SetActive(adsOn);
                openAllAdsButton.interactable = !busy;
            }

            if (adsOn && openAllAdsCountLabel) openAllAdsCountLabel.text = $"{Profile.OpenAllAdsWatched}/{openAllAdsRequired}";

            if (openAllIapButton)
            {
                openAllIapButton.gameObject.SetActive(iapOn);
                openAllIapButton.interactable = !busy;
            }

            if (iapOn && openAllIapPriceLabel) openAllIapPriceLabel.text = IapPriceText;

            if (comeBackLabel) comeBackLabel.SetActive(UnopenedCount == 0);
            if (openAllAdsAttention) openAllAdsAttention.SetAttention(IsVisible() && adsOn && !busy);
            if (openAllIapAttention) openAllIapAttention.SetAttention(IsVisible() && iapOn && !busy);
        }

        void OnCardClicked(int day) => TryClaim();

        void OnOpenAllAds()
        {
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            RequestOpenAllAds();
            RefreshAll();
        }

        void OnOpenAllIap()
        {
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            RequestOpenAllIap();
            RefreshAll();
        }

        #endregion

        #region Debug

        [ButtonGroup("Tabs/Debug/Panel"), Button("Open"), DisableInEditorMode]
        void PreviewOpen() => OpenPanel();

        [ButtonGroup("Tabs/Debug/Panel"), Button("Close"), DisableInEditorMode]
        void PreviewClose() => ClosePanel();

        [ButtonGroup("Tabs/Debug/Panel"), Button("Reset + Reopen"), DisableInEditorMode]
        void PreviewResetAndReopen()
        {
            ResetProfile();
            Hide();
            OpenPanel();
        }

        [ButtonGroup("Tabs/Debug/Claim"), Button("Claim Today"), DisableInEditorMode]
        void PreviewClaimToday() => TryClaim();

        [ButtonGroup("Tabs/Debug/Claim"), Button("Expire Today"), DisableInEditorMode]
        void PreviewExpireToday()
        {
            Profile.LastClaimDateUtc = "";
            RewardProfileStore.Save(SaveKey, Profile);
            RaiseChanged();
        }

        [ButtonGroup("Tabs/Debug/Claim"), Button("Open All Ads"), DisableInEditorMode]
        void PreviewOpenAllAds() => RequestOpenAllAds();

        [ButtonGroup("Tabs/Debug/Claim"), Button("Open All IAP"), DisableInEditorMode]
        void PreviewOpenAllIap() => RequestOpenAllIap();

        [TabGroup("Tabs", "Debug"), Button("Set Streak Day"), DisableInEditorMode]
        void PreviewSetStreakDay(int day)
        {
            Profile.StreakDay = Mathf.Clamp(day, 0, DayCount);
            Profile.LastClaimDateUtc = "";
            RewardProfileStore.Save(SaveKey, Profile);
            RaiseChanged();
        }

        #endregion
    }
}
