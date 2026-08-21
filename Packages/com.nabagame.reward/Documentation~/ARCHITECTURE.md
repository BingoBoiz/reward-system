# Cách package hoạt động

Đọc trước khi sửa code package hoặc tích hợp vào game.

## 1. Phụ thuộc

```
Assembly-CSharp (game: manager kiểu Sample*, UI root, adapter)
      |  tham chiếu            (không bao giờ ngược lại)
      v
NabaGame.Reward                                   (asmdef của package)
      |  tham chiếu
      v
com.bmh.core.runtime (EventManager, Singleton), com.nabagame.ui.runtime (BaseUI, UIPanel)
UniTask, Sirenix (Odin, DLL sẵn), DOTween (DLL sẵn, nằm trong game)
```

- Package **không bao giờ tham chiếu code hay type của game**. Asmdef chặn cứng điều này: `Assembly-CSharp` thấy package, package không thấy `Assembly-CSharp`.
- Service package cần từ game (phát SFX, rewarded ads, IAP) đi vào qua **hook tĩnh** (mục 3) gán một lần lúc boot; dữ liệu thưởng đi vào qua **danh sách row** truyền cho `SetInfo` của panel (mục 2); phần thưởng đi ra qua callback **`OnClaimed`** của từng row (mục 2); đổi trạng thái được thông báo thêm bằng **event** (mục 4).
- Phụ thuộc cứng chỉ gồm những gì mọi project NabaGame luôn có: `com.nabagame.core`, `com.nabagame.ui`, UniTask, Odin. DOTween coi như có sẵn trong game (`Assets/Plugins/Demigiant/`, DLL được asmdef tự tham chiếu). Không SDK ads, không plugin lưu dữ liệu.
- Package **không** chứa hay phụ thuộc enum của game (`RewardType`, `RewardID`, ...). Mỗi game một danh sách riêng; package đứng ngoài nhờ mô hình bên dưới.

## 2. Mô hình: một panel, một danh sách row, phát thưởng qua `OnClaimed`

**Mỗi tính năng là một prefab panel, và panel lo tất cả**: lưu PlayerPrefs, timer, ads, IAP, luật nhận/quay. Game viết một manager nhỏ giữ dữ liệu và đưa cho panel:

```csharp
// code phía game: manager này là việc của dev; sample có sẵn một cái mỗi tính năng làm mẫu
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

**Mọi thứ dev cần điền nằm trong một class row mỗi tính năng**: định danh, hình, âm thanh và callback phát thưởng:

```csharp
// NabaGame.Reward: vị trí trong list chính là chỉ số (rows[0] = ngày 1 / múi ở 12 giờ / ô đầu tiên)
[Serializable] public class DailyRewardRow  { public string Key; public Sprite Icon; public long Amount; public AudioClip ClaimSfx; public string LabelOverride; public bool HideIconUntilClaim; [NonSerialized] public Action<DailyRewardRow> OnClaimed; }
[Serializable] public class LuckySpinRow   { public string Key; public Sprite Icon; public long Amount; public int Weight; public AudioClip ClaimSfx; [NonSerialized] public Action<LuckySpinRow> OnClaimed; }
[Serializable] public class OnlineRewardRow { public string Key; public Sprite Icon; public long Amount; public int UnlockAfterSeconds; public AudioClip ClaimSfx; [NonSerialized] public Action<OnlineRewardRow> OnClaimed; }
```

- Row là class `[Serializable]` phẳng, field public. Mỗi class row có một constructor mà tham số cho biết cần điền gì khi tạo bằng code; constructor rỗng giữ cho bảng `[TableList]` trong Inspector hoạt động. Cả hai cách đều chính thức.
- `Key` là chuỗi tuỳ ý, package không đọc hiểu; nó tồn tại để hàm `OnClaimed` của game `switch` theo. `Icon`/`LabelOverride` hiện nguyên trạng (rỗng = không ghi đè). Thông số của tính năng (`freeSpinCooldownSeconds`, `spinDurationSeconds`, tua nhanh, cấu hình Open All ads/IAP) là `[SerializeField]` trên **prefab panel**.
- **Phát thưởng đi qua `Row.OnClaimed`, không qua event**: sau khi panel đổi và lưu trạng thái, nó `Debug.Log` phần thưởng (dòng log bắt buộc để tra cứu) rồi gọi `OnClaimed` của row. `OnClaimed` null thì không phát gì và `Debug.LogError` ghi rõ row nào; hàm của game cũng phải báo lỗi to khi gặp `Key` lạ.
- **Kiểm tra dữ liệu nhẹ tay có chủ đích**: thiếu `Icon`/`Key`/`Amount` chỉ sinh một `Debug.LogWarning` gộp ghi rõ chỉ số và field (`{Feature}Row.Warn(rows)`, gọi được từ `OnValidate` của manager); tính năng vẫn chạy. Chỉ cấu trúc không thể chạy mới ném lỗi: list null/rỗng, ít hơn 2 múi, `UnlockAfterSeconds` không tăng dần. Chỉ số đã lưu trong PlayerPrefs được kiểm tra trong khoảng hợp lệ so với list hiện tại trước khi dùng.
- Panel hiện các row nhận được ở chế độ chỉ đọc trong Inspector (`[ShowInInspector, ReadOnly, TableList]`) để lúc chạy dev thấy manager đã truyền gì.

## 3. Hook: service của game, gán một lần lúc boot

`SetInfo(rows)` chỉ mang dữ liệu, nên service dùng chung toàn app nằm trên một class tĩnh mà game gán **trước** mọi `SetInfo` của panel:

```csharp
public static class RewardHooks
{
    public static Action<AudioClip> PlaySfx;                 // mặc định: không làm gì
    public static Action<string, Action, Action> ShowRewardedAd;   // (placement, onReward, onSkip)
                                                             // mặc định: LogError + coi như đã xem xong
    public static Action<string, Action<bool>> PurchaseIap;  // (productId, result)
                                                             // mặc định: LogError + coi như mua thành công
    public static Func<string, string> GetIapPrice;          // productId -> giá store đã bản địa hoá
                                                             // mặc định: "" (dùng chuỗi trên panel)
    public static Action<string> ShowMessage;                // thông báo cho người chơi, toast của game
                                                             // mặc định: LogWarning
    public static Action<string, string, string> TrackEvent; // (tên event, tên param, giá trị param)
                                                             // mặc định: Debug.Log
}
```

- **Có mặc định, không null**: prefab vừa kéo vào, chưa có code boot, vẫn chạy. Hook ads/IAP chưa gán sẽ `Debug.LogError` ghi tên hook rồi xử lý như đã xem xong/mua thành công. Không có kiểu `Validate` rồi ném lỗi; nhẹ tay là hợp đồng.
- Một hàm `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` đặt lại mặc định mỗi phiên Play. Bắt buộc, nếu không delegate trỏ tới object đã huỷ sẽ sống sót khi tắt domain reload.
- Ads là hook, không phải dependency, nên package chạy với mediation nào cũng được và giả lập rất dễ. Adapter chỉ là một dòng gọi sang SDK ads/IAP mà game dùng.
- **`ShowRewardedAd` không bắt buộc phải trả lời.** Nhiều wrapper ads chỉ gọi lại khi phát thưởng thành công và im lặng khi ads bị giới hạn tần suất, chưa load, bị bỏ qua hay lỗi hiển thị. `RewardFlow` (mục 8) tự kết thúc yêu cầu thay vì tin SDK, nên adapter của game không được thêm timeout riêng.
- **`GetIapPrice` thắng chuỗi giá nhập trên panel**: store yêu cầu giá thật đã bản địa hoá. Chuỗi nhập chỉ là dự phòng khi store chưa khởi tạo.
- **`ShowMessage` là cách ads/mua thất bại đến được người chơi.** Package truyền một trong các hằng `RewardHooks.AdNotAvailableMessage` / `AdSkippedMessage` / `PurchaseFailedMessage`, game so sánh để hiện câu đã dịch thay vì tiếng Anh.
- **`TrackEvent` là toàn bộ phần analytics.** Package tự quyết bắn event nào và lúc nào (mục 3.1); game chỉ nối hook này vào hàm gửi analytics của mình. Chưa gán thì mặc định `Debug.Log` đúng ba giá trị sắp gửi, tiện xem trong Editor vì analytics thường không chạy trong Editor.

### 3.1. Analytics: package tự bắn, game chỉ điền một chuỗi

Mỗi prefab panel có một field `trackEventName` trong tab **Config** (`daily_reward`, `lucky_spin`, `online_reward`). Đó là **tên event** gửi lên analytics; **để trống là tắt** analytics của tính năng đó. Tên phải hợp lệ (bắt đầu bằng chữ, chỉ chữ-số-gạch dưới, tối đa 40 ký tự), sai thì `SetInfo` ghi một `Debug.LogWarning`.

Package giữ đúng shape mà dashboard đọc được: **event = tính năng, param key = hành động, param value = chi tiết**.

| param key | giá trị | bắn khi |
|---|---|---|
| `open` | `"1"` | mở panel |
| `claim` | `Row.Key` | mỗi lần phát thưởng (kể cả trong Open All) |
| `spin` | `free` / `ads` | bắt đầu một lượt quay (Lucky Spin) |
| `speed_up` | `x2` / `x5` | tua nhanh được kích hoạt (Online Reward) |
| `open_all` | `ads` / `iap` | bấm Open All (Daily, Online) |
| `ads_start` | placement | bắt đầu yêu cầu rewarded ads |
| `ads_done` | placement | ads xem xong, có thưởng |
| `ads_fail` | placement | ads không mở được, bị bỏ qua hoặc hết giờ chờ |
| `iap_start` | product id | bắt đầu yêu cầu mua |
| `iap_done` | product id | mua thành công |
| `iap_fail` | product id | mua thất bại hoặc hết giờ chờ |

Nhóm `ads_*` và `iap_*` do `RewardFlow` (mục 8) bắn, nên mọi ads và mọi lượt mua của cả ba tính năng đều được ghi nhận ở cùng một chỗ, kể cả ca SDK không gọi lại callback nào.

## 4. Event: thông báo cho game

Truy vấn là **gọi trực tiếp** trên panel (`dailyRewardPanel.ClaimableCount`). Phát thưởng qua `OnClaimed` (mục 2). Còn lại trên event bus là **thông báo**, nghe hay không tuỳ bạn:

- `DailyRewardChangedEvent`, `LuckySpinChangedEvent`, `OnlineRewardChangedEvent`: trạng thái đổi; refresh badge/HUD.
- `SpinStartedEvent { int WedgeIndex; float DurationSeconds; }`, `OnlineRewardSpeedUpEvent { int Multiplier; }`, `{Feature}PanelClosedEvent`: tín hiệu vòng đời.
- Mọi `SetInfo(rows)` kết thúc bằng `RaiseChanged()`: lần load đầu cũng là một lần đổi trạng thái, nên listener đăng ký trước boot không bị bỏ sót.
- Bắn qua `EventManager.Instance.Raise(...)` với **instance event mới** mỗi lần, không dùng lại instance cũ đã sửa. Payload mang giá trị đầy đủ, không mang delta.

**Chấm đỏ hoàn toàn thuộc về game**: package không vẽ badge ở đâu cả, kể cả bên trong panel của nó. Game nghe event đổi trạng thái và đọc panel (`dailyRewardPanel.ClaimableCount > 0` / `GetState(day)`, `onlineRewardPanel.HasClaimable` / `GetState(slot)`, `luckySpinPanel.FreeSpinReady`). `SampleRedDot` là ví dụ tham khảo: thả lên hình chấm đỏ, chọn `SampleRedDotKey`, nó tự đăng ký, tự đọc, tự chạy hiệu ứng.

## 5. Thời gian: `RewardClock` + `TimeScheduler`, không `Update()`

`RewardClock` (`Runtime/Core/`) là file **duy nhất** đọc đồng hồ; không file Runtime nào khác động vào `DateTime*` hay `Time.realtimeSinceStartup*`. Muốn giả lập thời gian hay dùng giờ server chỉ cần sửa một file.

```csharp
public static class RewardClock
{
    public static long NowMs { get; }                       // giờ thực, unix ms
    public static DateTime UtcNow { get; }
    public static string TodayUtc { get; }                  // "yyyy-MM-dd", invariant culture: key ngày trong file lưu
    public static long NextUtcMidnightMs { get; }
    public static double SecondsUntil(long atUnixMs);
    public static double MonotonicSeconds { get; }          // realtimeSinceStartup; vẫn chạy khi app bị treo
    public static int MsUntilNextTick(double remainingSeconds, double rate = 1);   // ms thực cho tới khi số đếm ngược đổi
}

public static class TimeScheduler
{
    public static Handle Schedule(long atUnixMs, Action callback);
    public static void Cancel(ref Handle handle);           // gán null cho field của bên gọi
}
```

`TimeScheduler` làm việc "gọi tôi lúc unix time T": một vòng lặp chung `UniTask.Delay(1000, DelayType.Realtime)` kiểm tra lại mọi deadline so với `RewardClock.NowMs` mỗi giây, nên app treo/mở lại không cần panel nào tự bù giờ. Callback chạy trong `try/catch`: một callback ném lỗi không được giết vòng lặp chung. Có reset `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` để đúng khi tắt domain reload.

Không `Update()`. Label đếm ngược chạy một vòng lặp UniTask `DelayType.Realtime` **chỉ khi `IsVisible()`** (nên `Hide()` từ bên ngoài không làm rò rỉ), thời gian chờ tính lại mỗi vòng bằng `RewardClock.MsUntilNextTick(remaining, rate)`: vòng lặp thức dậy đúng lúc số hiển thị đổi, nên tua x5 đếm 59, 58, 57 với năm nhịp mỗi giây thật thay vì nhảy 59, 54, 49, và không trôi ở bất kỳ tốc độ nào. Online Reward khởi động lại vòng lặp từ `RaiseChanged()` vì đổi trạng thái có thể đổi hệ số hoặc pha dưới giây giữa lúc ngủ.

Tính năng cộng dồn thời gian (Online Reward) dùng **mốc baseline**: elapsed = `(RewardClock.MonotonicSeconds - baseline) * multiplier`, chốt lại khi đổi hệ số và khi mất focus. Chốt theo **`Application.focusChanged`** tĩnh, không theo `OnApplicationPause`: message của Unity im lặng nếu UI framework của game deactivate panel đang ẩn, còn event tĩnh vẫn bắn bất kể trạng thái GameObject.

## 6. Lưu: PlayerPrefs + JsonUtility

Không plugin lưu. Mỗi tính năng có một class profile serializable, lưu dạng JSON trong một key PlayerPrefs:

- Key: `NabaReward.Daily`, `NabaReward.Online`, `NabaReward.Spin`.
- `RewardProfileStore` lo load/save: `JsonUtility.FromJson` lúc `SetInfo` (chưa có key = profile mới), `JsonUtility.ToJson` + `PlayerPrefs.Save()` ở **mỗi lần đổi trạng thái**. Không có bước lưu lúc pause/quit vì lưu-khi-đổi đã đủ và message pause trên panel không đáng tin (mục 5).
- Mỗi profile có `int Version`; load version cũ thì chạy migration rõ ràng hoặc log lỗi ghi tên key, không bao giờ âm thầm đọc sai field.
- Trạng thái theo phiên (lưới của Online Reward) đơn giản nằm ngoài profile.
- PlayerPrefs chỉ chứa chuỗi nhỏ; profile phải phẳng và gọn.

## 7. UI: panel sở hữu tính năng

- Mọi popup của package kế thừa `NabaGame.UI.BaseUI` (`Show()`/`Hide()` là nguồn sự thật về hiển thị). Mỗi tính năng một popup độc lập, không có khung tab chung, nên game lấy một tính năng chỉ kéo đúng một prefab.
- **Panel giữ toàn bộ luật**: lưu, timer, ads, IAP, máy trạng thái nhận/quay. Không có manager phía package để đặt, nối hay khởi tạo, nên không còn kiểu manager và panel lệch nhau.
- **Vòng đời**: `SetInfo(List<{Feature}Row>)` là lệnh khởi tạo **duy nhất**: kiểm tra dữ liệu, load lưu, chạy timer, gắn row lên danh sách thẻ/múi đã dựng sẵn (bảng cố định) hoặc nhân bản template (danh sách động), gắn listener không trùng (`RemoveListener` trước `AddListener`). `OpenPanel()` / `ClosePanel()` là API bật/tắt cho dev (refresh + `Show()` / `Hide()` + event đóng).
  - Gọi `SetInfo(rows)` từ `Start()` lúc boot, không từ `Awake()` (`UIPanel` áp `startHidden` trong `Start()` của nó), và không mở panel ngay trong frame đó.
  - Tên `OpenPanel`/`ClosePanel` là cố định: inspector processor của `NabaGame.UI` so khớp đúng chuỗi đó để vẽ nút mở/đóng Odin.
- **Mọi tham chiếu UI serialized đều tuỳ chọn**: dev tắt hay xoá nút, label, badge, template, thẻ/múi dựng sẵn nào cũng không lỗi, không kẹt. Mọi chỗ dùng đều kiểm tra null, thiếu template hay list rỗng thì `LogError` rồi bỏ qua, phần tử null trong list bị bỏ qua im lặng, chỉ số ô/múi được kiểm tra biên. Mở panel trước `SetInfo` thì log một lỗi và từ chối, không spam NRE.
- `UnityEngine.UI.Button` luôn giữ listener nghiệp vụ. `NabaGame.UI.UIButton` chỉ chạy phản hồi nhấn; `UIElement` chạy scale/fade khi button cấp panel xuất hiện. Thẻ Daily và ô Online không gắn `UIElement` để tránh nested Canvas ảnh hưởng layout, intro của chúng vẫn chạy bằng DOTween.
- CTA đặc biệt dùng `RewardButtonAttentionFx.SetAttention(bool)`: tween unscaled, tự dừng khi panel ẩn hoặc button bị khoá. Mỗi panel chỉ bật attention cho một nút: Open All đang hiện hoặc Spin đang bấm được.
- Thành viên mỗi panel nằm trong bốn region cố định, `API` đầu tiên: `#region API` (SetInfo/OpenPanel/ClosePanel + truy vấn chấm đỏ + reset: tất cả những gì dev cần), `#region Logic`, `#region UI`, `#region Debug`. Dev chỉ đọc region API là dùng được.
- Panel dựng trong prefab; tham chiếu là `[SerializeField]`. Không tạo UI bằng `new GameObject()` lúc chạy. Hai kiểu danh sách: **bảng cố định** (7 thẻ Daily, 8 múi Spin) là instance dựng sẵn nối vào `List<>` serialized, nên layout và skin nằm trong prefab; **danh sách động** (ô Online, pool popup nhận quà) nhân bản một template đang tắt vì số lượng theo dữ liệu của game.

## 8. Luồng ads/IAP: `RewardFlow`

Mọi rewarded ad **và** mọi IAP đều đi qua một helper Runtime cho mỗi panel, vì cả hai cùng một rủi ro: service của game không bao giờ trả lời. Mỗi lúc một yêu cầu, **kết thúc đúng một lần** (thành công, thất bại hoặc hết giờ), và luôn xoá `Busy` *trước* khi gọi callback, nên `RefreshAll()` gọi từ trong callback vẽ đúng trạng thái mới.

Vì nhiều SDK chỉ báo thành công (mục 3), `RewardFlow` suy phần còn lại từ focus của app:

| Tín hiệu | Kết luận |
|---|---|
| SDK gọi lại | đã phát thưởng |
| app không mất focus trong ~3s | ads chưa từng mở: `AdNotAvailableMessage` |
| mất focus rồi quay lại, ~0.5s sau vẫn không có thưởng | người chơi bỏ qua: `AdSkippedMessage` |
| quá ~180s (ads) / ~300s (mua) | bỏ cuộc, mở lại nút |

Focus là tín hiệu trung thực duy nhất: ads toàn màn luôn đẩy app xuống nền, và hầu hết SDK mediation bắn event thưởng trước khi ads đóng, nên nửa giây sau khi lấy lại focus là đủ. `Application.focusChanged` được đăng ký lúc hiện và **luôn huỷ đăng ký khi kết thúc** (event tĩnh; không huỷ sẽ rò rỉ qua domain reload). Kết thúc khi panel sở hữu đã bị huỷ thì bỏ callback.

- Hook chưa gán: mặc định của `RewardHooks` log lỗi và coi như đã xem xong, nên test được luồng trong Editor và trên máy mà không cần SDK.
- Quy ước đặt tên placement: `"<Feature>_<Action>"`: `OnlineReward_x2Speed`, `OnlineReward_x5Speed`, `OnlineReward_OpenAll`, `LuckySpin_AdSpin`, `DailyReward_OpenAll`. Placement là `public const` trong region API của từng panel và game phải đăng ký; phiên bản này chưa cho game đổi tên chúng.
