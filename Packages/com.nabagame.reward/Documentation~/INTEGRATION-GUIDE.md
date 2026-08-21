# Hướng dẫn tích hợp

Cách đưa `com.nabagame.reward` vào game, từ số không đến lúc tính năng chạy. Sample Reward Demo (`Samples~/RewardDemo`) là ví dụ sống cho từng bước.

Tóm tắt toàn bộ hợp đồng trong một câu: **kéo một prefab panel, viết một manager nhỏ, gán 5 hook tĩnh lúc khởi động.** Panel lo hết (lưu, timer, ads, IAP, luật chơi); manager của bạn giữ dữ liệu (`rows`) và phát thưởng (`OnClaimed`). Chỉ cần đọc `#region API` của panel là đủ.

## 1. Chuẩn bị

Cài trước các thứ sau (Unity không tự kéo dependency dạng git):

- `com.nabagame.core` (git): EventManager, Singleton
- `com.nabagame.ui` (git): BaseUI, UIPanel, UIManagerSingleton
- `com.cysharp.unitask` (git)
- Odin Inspector trong `Assets/Plugins/Sirenix/`
- DOTween trong `Assets/Plugins/Demigiant/`

Unity 2022.3 trở lên. Package không cần SDK ads; adapter bên dưới chỉ là một dòng gọi sang SDK mà game đang dùng.

## 2. Thêm package

Package Manager > *Add package from git URL* (`https://github.com/<owner>/<repo>.git?path=Packages/com.nabagame.reward#v1.0.0`), hoặc copy thư mục package vào `Packages/`. Kiểm tra "NabaGame Reward" xuất hiện trong Package Manager.

## 3. Import sample Reward Demo

Package Manager > NabaGame Reward > Samples > **Reward Demo** > Import. Một scene (`RewardSample.unity`), các script `Sample*`, prefab và dữ liệu mẫu nằm ở `Assets/Samples/NabaGame Reward/...`. Mọi thứ có tiền tố `Sample` là code phía game, bạn copy và sửa thoải mái.

## 4. Viết manager và điền rows

Package không giữ dữ liệu và không có manager. Manager của bạn khoảng 15 dòng; `SampleDailyRewardManager` trong sample là mẫu:

```csharp
public class SampleDailyRewardManager : MonoBehaviour
{
    [TableList] public List<DailyRewardRow> rows = new List<DailyRewardRow>();

    public void SetInfo()
    {
        foreach (DailyRewardRow row in rows) row.OnClaimed = OnClaimed;
        SampleUIManager.Instance.dailyRewardPanel.SetInfo(rows);
    }

    void OnClaimed(DailyRewardRow row) => SampleRewardGranter.Grant(row.Key, row.Icon, row.Amount);

    void OnValidate() => DailyRewardRow.Warn(rows, this);
}
```

Điền `rows` trong Inspector (bảng Odin `[TableList]`) hoặc bằng code; constructor cho biết cần điền gì:

```csharp
rows = new List<DailyRewardRow>
{
    new DailyRewardRow("cash", 7500000, icon: cashIcon),
    new DailyRewardRow("spin", 1, icon: spinIcon, labelOverride: "RARE"),
    // ... vị trí trong list chính là ngày; không có field Day/Wedge/Slot
};
```

Mọi thứ cần điền nằm gọn trong một class row: `Key` (từ vựng của riêng bạn, package không đọc hiểu nó), `Icon`, `Amount`, `ClaimSfx` (âm thanh khi nhận), `OnClaimed` (callback phát thưởng), cộng vài field riêng từng tính năng (`LabelOverride`/`HideIconUntilClaim`, `Weight`, `UnlockAfterSeconds`).

**Điền thiếu chỉ cảnh báo, không lỗi**: thiếu icon/key sẽ có một cảnh báo gộp trên Console ghi rõ thiếu ở đâu (`DailyRewardRow.Warn`), tính năng vẫn chạy; bạn điền nốt lúc nào cũng được. Chỉ những cấu trúc không thể chạy mới ném lỗi: list rỗng, ít hơn 2 múi, thời gian mở khoá không tăng dần.

## 5. Phát thưởng: `Row.OnClaimed`, bắt buộc

Không có event phát thưởng. **Callback `OnClaimed` của dòng là đường phát thưởng duy nhất**; manager gán nó trước khi gọi `SetInfo` (bước 4). Nếu dòng được nhận mà `OnClaimed` null thì không phát gì và Console hiện `'key' xN was NOT granted`. Mỗi lần phát cũng có một dòng log key + số lượng để tra cứu.

Hàm xử lý `switch` theo `Key` và phải báo lỗi to khi gặp key lạ:

```csharp
public static void Grant(string key, Sprite icon, long amount)
{
    switch (key)
    {
        case "cash": Wallet.AddCash((int)amount); break;
        // ... mọi key bạn dùng trong rows
        default: Debug.LogError($"Unknown reward key '{key}' x{amount}: no grant mapping"); return;
    }
    // sau đó: event nhận quà / popup / tracking của bạn
}
```

Popup nhận quà là của phía game: granter trong sample bắn `SampleItemGrantedEvent`, `SampleItemReceivedPanel` (sorting 250) gộp các lần phát cùng frame, cộng dồn key trùng và chạy hiệu ứng. Package không bao giờ tự mở nó.

## 6. Gán hook: 6 static, một lần, lúc khởi động

```csharp
// những dòng đầu tiên trong Start() của boot, trước mọi SetInfo của panel
RewardHooks.PlaySfx        = clip => { if (clip) SoundManager.Instance.sfxSource.PlayOneShot(clip); };
RewardHooks.ShowRewardedAd = (placement, onReward, onSkip) => Ads.ShowRewarded(placement, onReward, onSkip);
RewardHooks.PurchaseIap    = (productId, result) => Iap.Purchase(productId, ok => result(ok));
RewardHooks.GetIapPrice    = Iap.GetLocalizedPrice;
RewardHooks.ShowMessage    = message => Toast.Show(message);   // toast/popup của bạn
RewardHooks.TrackEvent     = Analytics.LogEvent;               // (tên event, tên param, giá trị param)
```

Hook chưa gán không bao giờ ném lỗi: mặc định sẽ `Debug.LogError` ghi tên hook rồi coi như đã xem xong ads / mua thành công, nên prefab vừa kéo vào đã chạy được trước khi bạn nối gì.

**Bốn điều cần kiểm tra ở SDK ads/IAP của bạn:**

1. **Nhiều SDK rewarded chỉ gọi lại khi thành công.** Ads bị giới hạn tần suất, chưa load, bị bỏ qua hay lỗi hiển thị đều im lặng; ads thứ hai trong luồng OPEN ALL nhiều ads là ca hay dính nhất. Package tự phát hiện các trường hợp đó (ARCHITECTURE mục 8) và báo người chơi qua `ShowMessage`. **Đừng bọc hook bằng timeout hay kiểm tra sẵn sàng của riêng bạn**, sẽ xung đột với `RewardFlow` và báo hai lần.
2. **Chặn app-open ad quanh lúc mua.** Nếu SDK hiện app-open ad mỗi khi app lấy lại focus thì lúc đóng màn thanh toán của store cũng là một lần lấy lại focus; không chặn thì người chơi vừa trả tiền xong đã gặp quảng cáo toàn màn.
3. **"Remove ads" không được tắt rewarded.** Tắt banner/interstitial/app-open, giữ rewarded. Tắt cả rewarded là mọi nút ads trong package chết. Nếu cờ đó chỉ sống trong runtime thì phải gán lại từ dữ liệu đã lưu ở **mỗi** lần boot.
4. **Đẩy lùi interstitial sau mỗi lần xem rewarded.** Người chơi vừa xem xong rewarded không nên gặp interstitial hai giây sau. Package không quản interstitial; việc này của bạn.

Product id của `PurchaseIap` (ví dụ `DailyRewardPanel.openAllIapProductId`) phải có trong catalog IAP. Hiển thị giá: `GetIapPrice` được đọc mỗi lần refresh, `openAllIapPriceText` trên panel chỉ là dự phòng khi store chưa khởi tạo xong; trả `""` từ hook để giữ chuỗi đã nhập.

`ShowMessage` nhận một trong `RewardHooks.AdNotAvailableMessage` / `AdSkippedMessage` / `PurchaseFailedMessage`. So sánh với các hằng đó để hiện câu chữ đã dịch của bạn thay vì tiếng Anh mặc định.

## 7. Gắn panel

1. Kéo prefab panel (ví dụ `DailyRewardPanel` trong sample) vào dưới UI root; thêm một field trỏ tới nó trên `UIManagerSingleton` của bạn (hoặc dùng `SampleUIManager` của sample, nó tự tìm panel trong `OnValidate`).
2. Chỉnh thông số trên prefab panel nếu cần: Daily `openAllUseAds` (bật = nút ads, tắt = nút IAP)/`openAllAdsRequired`/`openAllIapProductId`/`openAllIapPriceText`; Spin `freeSpinCooldownSeconds`/`spinDurationSeconds`; Online `x2DurationSeconds`/`x5DurationSeconds`/`x2AdsRequired`/`x5AdsRequired` cộng bộ Open All giống Daily.
3. Gọi `SetInfo()` của manager **từ `Start()` lúc boot** (xem `SampleGameController`), không gọi trong `Awake()` (`UIPanel` áp `startHidden` trong `Start()` của nó), và không mở panel ngay trong frame vừa khởi tạo.
   - **Online Reward phải khởi tạo lúc boot, không đợi đến lần mở đầu tiên**: thời gian chơi tính từ `SetInfo`; khởi tạo muộn thì lưới không mở khoá khi panel đang đóng.
4. Mở từ nút bất kỳ bằng `OpenPanel()`, đóng bằng `ClosePanel()`:
   `public void OpenDaily() { SampleUIManager.Instance.dailyRewardPanel.OpenPanel(); }`

Nút/label/badge nào trên panel cũng có thể tắt hoặc xoá; panel kiểm tra null mọi tham chiếu và chỉ bỏ chức năng đó, không ném lỗi.

## 8. Analytics

Package tự bắn event, bạn chỉ điền **một chuỗi** cho mỗi tính năng: field `trackEventName` trong tab **Config** của prefab panel (mặc định `daily_reward`, `lucky_spin`, `online_reward`). Đó là tên event; **để trống là tắt** analytics của tính năng đó. Đổi thành `<tên game>_daily_reward` nếu muốn tách theo game.

Còn lại chỉ cần gán `RewardHooks.TrackEvent` ở mục 6 vào hàm gửi analytics của bạn. Chữ ký `(string tên event, string tên param, string giá trị param)` cố ý khớp dạng hàm quen thuộc để gán thẳng, không cần lambda.

Shape gửi lên: **event = tính năng, param key = hành động, param value = chi tiết** (đăng ký các param key này trên dashboard):

| param key | giá trị | bắn khi |
|---|---|---|
| `open` | `"1"` | mở panel |
| `claim` | `Row.Key` | mỗi lần phát thưởng, kể cả trong Open All |
| `spin` | `free` / `ads` | bắt đầu một lượt quay (Lucky Spin) |
| `speed_up` | `x2` / `x5` | tua nhanh được kích hoạt (Online Reward) |
| `open_all` | `ads` / `iap` | bấm Open All (Daily, Online) |
| `ads_start` / `ads_done` / `ads_fail` | placement | vòng đời một lần rewarded ads |
| `iap_start` / `iap_done` / `iap_fail` | product id | vòng đời một lần mua |

Ví dụ một lần nhận thưởng ngày 3: `daily_reward` / `claim` / `day_3_coin`.

Tên event phải hợp lệ với dashboard analytics: bắt đầu bằng chữ, chỉ chữ-số-gạch dưới, tối đa 40 ký tự. Điền sai thì `SetInfo` ghi một `Debug.LogWarning`; các dịch vụ analytics thường **im lặng bỏ** tên sai chứ không báo lỗi.

Chưa gán `RewardHooks.TrackEvent` thì package `Debug.Log` đúng ba giá trị sắp gửi. Đó cũng là cách kiểm tra trong Editor, vì analytics phần lớn không chạy trong Editor.

## 9. Đăng ký placement ads

Chuỗi placement là `public const` trong `#region API` của từng panel (`OnlineReward_x2Speed`, `OnlineReward_x5Speed`, `OnlineReward_OpenAll`, `LuckySpin_AdSpin`, `DailyReward_OpenAll`). Đăng ký chúng trên dashboard mediation như các placement khác của game.

## 10. Chấm đỏ (tuỳ chọn)

Package không vẽ badge nào: không trên nút home, không trên thẻ Daily, không trên ô Online. Mọi chấm đỏ là của bạn.

Sample gói sẵn thành một component: đặt `SampleRedDot` lên `Image` của chấm đỏ và chọn `SampleRedDotKey`. Nó tự đăng ký event, tự đọc trạng thái panel và tự chạy hiệu ứng rung chuông. Không ai gọi nó, không ai tham chiếu nó.

| key | điều kiện sáng |
|---|---|
| `DailyReward` | `dailyRewardPanel.ClaimableCount > 0` |
| `LuckySpin` | `luckySpinPanel.FreeSpinReady && !IsSpinning` |
| `OnlineReward` | `onlineRewardPanel.HasClaimable` |
| `DailyRewardCard` | `dailyRewardPanel.GetState(card.Day) == DailyState.Claimable` (đọc `DailyRewardCard` cha của nó) |
| `OnlineRewardCell` | `onlineRewardPanel.GetState(cell.Slot) == OnlineSlotState.Claimable` (đọc `OnlineRewardCell` cha của nó) |
| `None` | không gì cả; bạn tự gọi `SetOn(bool)`, dành cho chấm đỏ của tính năng khác |

Tự viết thì chỉ ba dòng:

```csharp
EventManager.Instance.AddListener<DailyRewardChangedEvent>(OnDailyChanged);
// điều kiện: dailyRewardPanel.ClaimableCount > 0, luckySpinPanel.FreeSpinReady, onlineRewardPanel.HasClaimable
```

## 11. Kiểm tra

Chạy checklist trong mô tả từng tính năng (`FEATURES/<feature>.md`). Các điểm chung:

- Nhận thưởng đi vào đúng hàm `OnClaimed` **của bạn** (tiền đổi) và Console có dòng log phát thưởng. Có log mà tiền không đổi nghĩa là gán `OnClaimed` hoặc map key sai.
- Cooldown/timer sống sót khi app chạy nền (mất focus); tắt app mở lại khôi phục đúng dữ liệu đã lưu (trạng thái theo phiên thì reset như mô tả).
- Trong Editor, luồng ads hoàn tất ngay mà không cần SDK.
- Mở/đóng panel nhiều lần: không trùng listener, bấm liên tục không phát thưởng đôi.
- Tắt hoặc xoá nút/label bất kỳ trên panel: tính năng tự giảm bớt, không lỗi, không kẹt.
