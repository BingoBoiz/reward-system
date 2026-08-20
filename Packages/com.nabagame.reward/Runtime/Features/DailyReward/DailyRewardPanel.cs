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

        [SerializeField, TabGroup("Tabs", "UI")] Button closeButton; // nút đóng panel
        [SerializeField, TabGroup("Tabs", "UI")] GameObject comeBackLabel; // chữ hiện khi nhận hết
        [SerializeField, FoldoutGroup("Tabs/UI/Open All")] Button openAllAdsButton; // nút xem ads mở toàn bộ
        [SerializeField, FoldoutGroup("Tabs/UI/Open All")] TMP_Text openAllAdsCountLabel; // chữ đếm số ads đã xem
        [SerializeField, FoldoutGroup("Tabs/UI/Open All")] Button openAllIapButton; // nút mua IAP mở toàn bộ
        [SerializeField, FoldoutGroup("Tabs/UI/Open All")] TMP_Text openAllIapPriceLabel; // chữ giá tiền trên nút
        [SerializeField, FoldoutGroup("Tabs/UI/Cards")] List<DailyRewardCard> cards = new List<DailyRewardCard>(); // thẻ ngày xếp sẵn theo thứ tự

        [SerializeField, TabGroup("Tabs", "FX")] float cardStaggerDelay = 0.04f; // trễ hiện lần lượt từng thẻ
        [SerializeField, TabGroup("Tabs", "FX")] AudioClip buttonSfx; // tiếng bấm nút chung

        readonly AdFlow adFlow = new AdFlow();
        TimeScheduler.Handle rolloverHandle;
        bool iapBusy;

        [ShowInInspector, TabGroup("Tabs", "Profile"), HideLabel, InlineProperty]
        public DailyRewardProfile Profile { get; private set; }

        #region API

        // one-time init with the host's data; call from Start() at boot, the panel stays hidden until OpenPanel()
        public void SetInfo(List<DailyRewardRow> hostRows)
        {
            if (hostRows == null || hostRows.Count == 0) throw new InvalidOperationException("DailyRewardPanel: rows is null or empty");
            rows = hostRows;
            DailyRewardRow.Warn(rows, this);
            WarnConfig();

            Profile = RewardProfileStore.Load<DailyRewardProfile>(SaveKey, ProfileVersion);
            if (Profile.StreakDay < 0 || Profile.StreakDay > rows.Count)
            {
                Debug.LogError($"[DailyRewardPanel] saved StreakDay {Profile.StreakDay} is outside 0..{rows.Count}, reset to 0", this);
                Profile.StreakDay = 0;
                RewardProfileStore.Save(SaveKey, Profile);
            }

            ResetWeekIfElapsed();
            ScheduleRollover();
            BindCards();
            BindUi();
            RefreshAll();
        }

        public void OpenPanel()
        {
            if (Profile == null)
            {
                Debug.LogError("[DailyRewardPanel] OpenPanel before SetInfo(rows); call SetInfo from your boot first", this);
                return;
            }

            Show();
            RefreshAll();
            for (int i = 0; i < cards.Count && i < rows.Count; i++) if (cards[i]) cards[i].PlayIntro(i * cardStaggerDelay);
        }

        public void ClosePanel()
        {
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            Hide();
            EventManager.Instance.Raise(new DailyRewardPanelClosedEvent());
        }

        public int DayCount => rows.Count;

        public int StreakDay => Profile == null ? 0 : Profile.StreakDay;

        // red-dot query: 1 while today's card is still unclaimed
        public int ClaimableCount => Profile != null && Profile.StreakDay < DayCount && Profile.LastClaimDateUtc != TodayUtc ? 1 : 0;

        public int UnopenedCount => DayCount - StreakDay;

        public void ResetProfile()
        {
            RewardProfileStore.Clear(SaveKey);
            Profile = RewardProfileStore.Load<DailyRewardProfile>(SaveKey, ProfileVersion);
            RaiseChanged();
        }

        #endregion

        #region Logic

        static string TodayUtc => DateTime.UtcNow.ToString("yyyy-MM-dd");

        bool OpenAllIapEnabled => !string.IsNullOrEmpty(openAllIapProductId);

        DailyState GetState(int day)
        {
            if (day < Profile.StreakDay) return DailyState.Claimed;
            if (day == Profile.StreakDay && ClaimableCount > 0) return DailyState.Claimable;
            return DailyState.Locked;
        }

        void WarnConfig()
        {
            if (openAllUseAds && openAllAdsRequired <= 0) Debug.LogWarning($"[DailyRewardPanel] openAllUseAds is on but openAllAdsRequired is {openAllAdsRequired}, OPEN ALL stays off", this);
            if (!openAllUseAds && !OpenAllIapEnabled) Debug.LogWarning("[DailyRewardPanel] openAllUseAds is off but openAllIapProductId is empty, OPEN ALL stays off", this);
            if (!openAllUseAds && OpenAllIapEnabled && string.IsNullOrEmpty(openAllIapPriceText)) Debug.LogWarning("[DailyRewardPanel] openAllIapProductId is set but openAllIapPriceText is empty", this);
        }

        void ScheduleRollover()
        {
            TimeScheduler.Cancel(ref rolloverHandle);
            long nextMidnight = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero).ToUnixTimeMilliseconds();
            rolloverHandle = TimeScheduler.Schedule(nextMidnight, OnRollover);
        }

        void OnRollover()
        {
            rolloverHandle = null;
            ResetWeekIfElapsed();
            ScheduleRollover();
            RaiseChanged();
        }

        void ResetWeekIfElapsed()
        {
            if (Profile.StreakDay != DayCount || Profile.LastClaimDateUtc == TodayUtc) return;
            Profile.StreakDay = 0;
            RewardProfileStore.Save(SaveKey, Profile);
        }

        void ClaimDay(int day, bool withSfx)
        {
            DailyRewardRow row = rows[day];
            Profile.StreakDay = day + 1;
            Profile.LastClaimDateUtc = TodayUtc;
            RewardProfileStore.Save(SaveKey, Profile);

            if (withSfx && row.ClaimSfx) RewardHooks.PlaySfx(row.ClaimSfx);

            // the audit log is mandatory: a null OnClaimed grants nothing and only this line betrays it
            Debug.Log($"[DailyRewardPanel] claimed day {day + 1}: {row.Key} x{row.Amount}");
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
            return adFlow.Show(OpenAllPlacement, OnOpenAllAdWatched);
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
            if (openAllUseAds || !OpenAllIapEnabled || UnopenedCount == 0 || iapBusy) return false;
            iapBusy = true;
            RewardHooks.PurchaseIap(openAllIapProductId, OnOpenAllPurchase);
            return true;
        }

        void OnOpenAllPurchase(bool success)
        {
            iapBusy = false;
            if (!success)
            {
                Debug.Log($"[DailyRewardPanel] open-all purchase failed or cancelled: {openAllIapProductId}");
                return;
            }

            OpenAll();
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

            if (openAllAdsButton) openAllAdsButton.gameObject.SetActive(adsOn);
            if (adsOn && openAllAdsCountLabel) openAllAdsCountLabel.text = $"{Profile.OpenAllAdsWatched}/{openAllAdsRequired}";

            if (openAllIapButton) openAllIapButton.gameObject.SetActive(iapOn);
            if (iapOn && openAllIapPriceLabel) openAllIapPriceLabel.text = openAllIapPriceText;

            if (comeBackLabel) comeBackLabel.SetActive(UnopenedCount == 0);
        }

        void OnCardClicked(int day) => TryClaim();

        void OnOpenAllAds()
        {
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            RequestOpenAllAds();
        }

        void OnOpenAllIap()
        {
            if (buttonSfx) RewardHooks.PlaySfx(buttonSfx);
            RequestOpenAllIap();
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
