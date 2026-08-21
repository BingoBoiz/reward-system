# Daily Reward

`DailyRewardPanel` lo toàn bộ tính năng; game viết một manager kiểu `SampleDailyRewardManager`. Tài liệu này là chuẩn cho hành vi và dữ liệu.

## Là gì

Dãy 7 thẻ điểm danh: mỗi ngày (UTC) người chơi nhận thẻ kế tiếp; thẻ đã nhận giữ dấu, thẻ sau còn khoá. Phần thưởng tăng dần trong tuần; ngày 7 là phần thưởng lớn.

## Giao diện

- Popup có tiêu đề "DAILY!" (icon lịch) và nút đóng (X) góc trên phải.
- Một hàng ngang 7 thẻ. Thẻ gồm: icon phần thưởng (từ `Icon` của row), label số lượng (đã định dạng, ví dụ `+7.5K`, `+200B`), và trạng thái:
  - **Đã nhận**: mờ/xanh dương, có dấu tick xanh lá đè lên.
  - **Nhận được**: thẻ sáng, tiêu đề `CLAIM`. Chấm đỏ trên thẻ là của game (`SampleRedDot`, key `DailyRewardCard`), không thuộc thẻ.
  - **Khoá**: vẫn hiện `CLAIM` nhưng bất động; thẻ bí ẩn có thể giấu icon (bóng đen / `???`) cho tới khi nhận.
- Khung thẻ theo độ hiếm (ví dụ RARE xanh lá, EPIC cam) lấy từ cấu hình, không hard-code.
- Dưới hàng thẻ, `OpenAllRoot` chứa **hai nút OPEN ALL xếp chồng cùng vị trí (440x105); chỉ MỘT nút hiện, chọn bằng bool `openAllUseAds` trong Inspector**:
  - **Nút ads** (xanh lá, icon video): chữ `OPEN ALL` + tiến độ `X/N`; hiện khi `openAllUseAds` bật, `openAllAdsRequired > 0`, và tuần chưa mở hết.
  - **Nút IAP** (xanh dương, icon quà): chữ `OPEN ALL` + giá (`RewardHooks.GetIapPrice`, dự phòng `openAllIapPriceText`); hiện khi `openAllUseAds` tắt, `openAllIapProductId` có giá trị, và tuần chưa mở hết.
  - Khi cả tuần đã mở (`UnopenedCount == 0`) cả hai nút ẩn và hiện label tĩnh `COME BACK TOMORROW`.
- Panel và button scale/fade khi xuất hiện; mọi button có phản hồi nhấn. Chỉ nút Open All đang hiện và bấm được mới nhún lặp, icon trên nút nảy lệch pha. Thẻ nhận được pulse nhẹ trên icon.

## Dữ liệu (dev điền)

Manager của game (mẫu: `Samples~/RewardDemo/Scripts/SampleDailyRewardManager.cs`) giữ `[TableList] public List<DailyRewardRow> rows`, gán `OnClaimed` cho từng row, rồi truyền list cho `DailyRewardPanel.SetInfo(rows)` lúc boot.

Row (file duy nhất dev điền; vị trí trong list là ngày, `rows[0]` = ngày 1):
`{ string Key, Sprite Icon, long Amount, AudioClip ClaimSfx, string LabelOverride, bool HideIconUntilClaim, Action<DailyRewardRow> OnClaimed }`. Constructor cho biết thứ tự điền khi tạo bằng code; `LabelOverride` rỗng = không ghi đè.

Kiểm tra nhẹ tay: thiếu `Key`/`Icon`/`Amount` sinh một cảnh báo gộp qua `DailyRewardRow.Warn(rows)` (gọi cả từ `OnValidate` của manager); chỉ list null/rỗng mới ném lỗi. Số row khác số thẻ dựng sẵn thì cảnh báo và gắn theo số nhỏ hơn; thẻ thừa ẩn đi, row thừa không dùng được cho tới khi thêm thẻ trong prefab.

7 thẻ là **instance dựng sẵn trong prefab**, nối theo thứ tự ngày vào list `cards` của panel; sprite nền của từng thẻ đặt trên Image của chính nó. Muốn đổi layout (hàng ngang như demo hay lưới 3+3+ngày 7 to) chỉ cần sắp lại trong prefab, không sửa C#.

Thông số trên prefab `DailyRewardPanel`: `openAllUseAds` (bật = nút ads, tắt = nút IAP), `openAllAdsRequired` (0 = tắt nút ads), `openAllIapProductId` ("" = tắt nút IAP), `openAllIapPriceText` (chuỗi hiển thị), `cards` (list thẻ dựng sẵn, thứ tự ngày), `cardStaggerDelay`, `openSfx`, `closeSfx`, `buttonSfx`.

## Lưu (key PlayerPrefs `NabaReward.Daily`)

| Field | Ý nghĩa |
|---|---|
| `Version` | version payload để migration |
| `StreakDay` | chỉ số (từ 0) của ngày chưa nhận kế tiếp (0..7); `7` = tuần đã mở hết, về 0 vào ngày UTC kế tiếp |
| `LastClaimDateUtc` | ngày `yyyy-MM-dd` UTC của lần nhận cuối; nhận được khi hôm nay (UTC) khác |
| `OpenAllAdsWatched` | số ads đã xem cho Open All; reset trong `OpenAll()`, giữ qua các tuần cho tới khi dùng |

Giữ qua các phiên. Lưu ngay mỗi lần đổi; không có bước lưu lúc pause/quit.

## API: `DailyRewardPanel`, `#region API`

- `SetInfo(List<DailyRewardRow> rows)`: khởi tạo duy nhất: kiểm tra dữ liệu, load lưu, hẹn giờ qua nửa đêm (`TimeScheduler`), gắn thẻ dựng sẵn, gắn listener. Gọi từ `Start()` lúc boot; panel vẫn ẩn.
- `OpenPanel()` / `ClosePanel()`: bật/tắt cho dev (refresh + `Show()` / `Hide()` + `DailyRewardPanelClosedEvent`).
- Truy vấn: `int DayCount`, `int StreakDay`, `int ClaimableCount` (0 hoặc 1 hôm nay, dùng cho chấm đỏ), `int UnopenedCount`, `DailyState GetState(int day)` (an toàn; trả `Locked` trước `SetInfo` hoặc ngoài khoảng).
- `ResetProfile()`: reset cho QA/debug.
- Hằng: `SaveKey`, `ProfileVersion`, `OpenAllPlacement`.

Một lần nhận: tăng streak, lưu, phát `ClaimSfx` của row, `Debug.Log` phần thưởng, rồi gọi `Row.OnClaimed`; game phát thưởng ở đó, callback null thì `LogError` "was NOT granted". Open All (ads hoặc IAP) nhận mọi ngày còn lại, mỗi ngày một `OnClaimed` trong cùng frame, nên popup gộp chỉ hiện một lần.

## Event / hook / placement

- **Phát thưởng: `Row.OnClaimed`**; không có event phát thưởng.
- `DailyRewardChangedEvent`: thông báo, bắn sau khi nhận, reset, tăng tiến độ ads, và lúc qua nửa đêm UTC (chấm đỏ / refresh).
- `DailyRewardPanelClosedEvent`: thông báo, bắn khi người chơi đóng panel.
- Hook dùng: `PlaySfx`, `ShowRewardedAd` (khi `openAllUseAds` bật và `openAllAdsRequired > 0`), `PurchaseIap` (khi `openAllUseAds` tắt và có `openAllIapProductId`; callback phải được gọi cả khi thành công, thất bại lẫn huỷ). Đều tuỳ chọn; hook chưa gán chỉ LogError rồi chạy tiếp.
- Placement: `DailyReward_OpenAll`.
- Analytics: `trackEventName` mặc định `daily_reward`. Param key bắn ra: `open`, `claim` (giá trị = `Row.Key`), `open_all` (`ads`/`iap`), cùng nhóm `ads_*` / `iap_*` với giá trị là placement / product id. Để trống `trackEventName` là tắt.

## Checklist kiểm tra

1. Cài mới: ngày 1 nhận được, ngày 2-7 khoá. Nhận: `OnClaimed` vào hàm của game (tiền đổi) và Console có log phát thưởng; thẻ chuyển sang đã nhận, `ClaimableCount == 0`.
2. Cùng ngày: không nhận thêm được; bấm liên tục chỉ phát đúng một lần.
3. Đổi ngày máy/UTC (hoặc debug): ngày kế nhận được; chấm đỏ của game theo `ClaimableCount` / `GetState(day)`.
4. Tắt app mở lại: streak, trạng thái thẻ và tiến độ ads khôi phục từ PlayerPrefs.
5. Mở/đóng panel nhiều lần: không trùng listener, không phát đôi.
6. Open All bằng ads: `openAllAdsRequired = 3`, bấm nút ads 3 lần: label 0/3, 1/3, 2/3; ads thứ ba xong thì nhận hết các ngày còn lại (mỗi ngày một log + một `OnClaimed`), nút ẩn, hiện `COME BACK TOMORROW`.
7. Open All bằng IAP: tắt `openAllUseAds`, bấm nút IAP: `RewardHooks.PurchaseIap` chạy; `cb(true)` nhận hết, `cb(false)` log và không đổi gì, nút vẫn bấm được.
8. Nhận thẻ hôm nay trước rồi Open All: chỉ các ngày còn lại được phát (không phát đôi).
9. Nhận riêng ngày 7: cả tuần hiện đã nhận, nút Open All ẩn ngay trong ngày; ngày UTC kế về ngày 1.
10. Cấu hình của chế độ đang chọn rỗng (`openAllUseAds` bật mà `openAllAdsRequired = 0`, hoặc tắt mà không có product id): không có nút Open All và một cảnh báo cấu hình, nhận từng thẻ vẫn bình thường; hook chưa gán chỉ LogError.
11. Thử xoá nút: tắt/xoá `openAllAdsButton`, `openAllIapButton`, `comeBackLabel`, thành phần con của một thẻ, hay cả một thẻ dựng sẵn (phần tử `cards` thành null): không lỗi, panel vẫn mở, các thẻ khác vẫn nhận được.
12. Xoá `OnClaimed` của một row: nhận row đó log `was NOT granted` và không phát gì; phần còn lại không ảnh hưởng.
13. Mở panel và chờ hơn 4.5 giây: chỉ Open All đang hiện nhún; chuyển ads/IAP, bắt đầu request, đóng panel hoặc khoá button thì tween cũ dừng và scale trở về đúng giá trị.

## Quy tắc đã chốt

- **OPEN ALL:** nhận **mọi ngày còn lại của tuần đang hiện trong một lần**, mở bằng ads hoặc IAP. Hai nút cùng vị trí, **chỉ một nút hiện, chọn bằng `openAllUseAds` (bật = ads, tắt = IAP)**, không bao giờ cả hai; luồng của chế độ không chọn cũng bị từ chối. Package tự chạy luồng ads / gọi hook IAP; dev chỉ điền `openAllUseAds`, `openAllAdsRequired`, `openAllIapProductId`, `openAllIapPriceText`. Nút vẫn dùng được sau khi đã nhận thẻ hôm nay; chỉ ẩn khi tuần đã mở hết.
- **Sau ngày 7:** nhận ngày 7 (hoặc Open All) đặt `StreakDay = 7`; cả tuần hiện đã nhận tới hết ngày và Open All biến mất. Về ngày 1 vào ngày UTC kế tiếp (`ResetWeekIfElapsed`). Nếu dùng `% 7` sẽ bị lợi dụng: tuần trông như chưa nhận ngay lập tức.
- **Tiến độ ads:** `OpenAllAdsWatched` giữ qua các tuần cho tới khi `OpenAll()` dùng; không reset lúc qua ngày.
- **Đứt streak:** bỏ lỡ một ngày không reset streak. Người chơi nhận tiếp ngày chưa nhận kế tiếp. `ClaimableCount` là 1 khi `StreakDay < DayCount` và `LastClaimDateUtc != hôm nay (UTC)`.

## Ghi chú layout

- Bộ art mẫu không có icon gem, nên bảng 7 ngày dựng từ các icon có sẵn (tiền / lucky-spin / no-ads); nút Open All dùng `checkpoint_0002_button-green` / `checkpoint_0003_button-blue` cắt 9-slice về 440x105.
- Mỗi lúc chỉ một nút OPEN ALL hiện, dạng ads/IAP do cấu hình quyết định.
- Thẻ khoá hiện đủ độ đậm; thẻ nhận được tự báo bằng nhịp phóng to (chấm đỏ trên thẻ là của game).
