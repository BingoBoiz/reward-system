# Sample Reward Demo

Một scene chạy được với đủ 3 màn Daily Reward, Lucky Spin và Online Reward, dùng art mẫu đi kèm, độ phân giải thiết kế 2400x1080. Mọi thứ có tiền tố `Sample` là code phía game, import xong là của bạn; các `Sample*Manager` chính là mẫu để game copy.

## Chạy thử

1. Import sample qua Package Manager.
2. Mở `Scenes/RewardSample.unity`, bấm Play. Ba nút bên trái (Daily / Spin / Playtime) mở từng panel; chấm đỏ trên nút là component `SampleRedDot` tự theo dõi event và trạng thái panel.
3. HUD tiền hiện mỗi lần phát thưởng, đồng thời popup "nhận quà" bật lên (chạm để đóng). Spin: lượt đầu free, sau đó nút chuyển sang dạng xem ads kèm đếm ngược `Free spin in mm:ss`. "Ads" trong sample là giả lập trên `SampleGameController` (`adResult`: Reward / Skip / Swallow).

## Có gì trong đây

| Thư mục | Nội dung |
|---|---|
| `Scripts/` | `SampleGameController` (điểm khởi động: gán các hook `RewardHooks` rồi gọi lần lượt mọi `SetInfo()` trong `Start()`), `SampleDailyRewardManager` / `SampleLuckySpinManager` / `SampleOnlineRewardManager` (manager phía game: `[TableList] rows` + `OnClaimed` -> `SampleRewardGranter.Grant`), `SampleRewardGranter` (đổi `Row.Key` thành tiền trong PlayerPrefs, bắn `SampleItemGrantedEvent`), `SampleUIManager` (UI root, tự tìm panel), `SampleCurrencyPanel` / `SampleHomePanel` (hai panel HUD luôn hiện: cũng là `BaseUI` như mọi panel khác, `useBackground = false`, `startHidden = false`, không đăng ký vào `checkHasPopup`), `SampleRedDot` (chấm đỏ tự chạy: tự nghe event, tự đọc trạng thái panel theo `SampleRedDotKey`, tự chạy hiệu ứng rung chuông; package không vẽ badge), `SampleItemReceivedPanel` + `SampleItemReceivedCell` + `SampleItemReceivedBurstFx` (popup nhận quà, gộp các lần phát cùng frame, cộng dồn key trùng) |
| `Prefabs/` | `SampleUIManager` (Canvas gốc 2400x1080 ScaleWithScreenSize match 0.5, HUD, nút home, toàn bộ panel), `DailyRewardPanel` với 7 `DailyRewardCard` dựng sẵn (nối vào `cards`), `LuckySpinPanel` với 8 `LuckySpinWedge` dựng sẵn (nối vào `wedges`), `OnlineRewardPanel` với template `OnlineRewardCell`, `SampleOnlineRewardManager` (18 dòng mẫu), `SampleItemReceivedPanel` (sorting 250) + template `SampleItemReceivedCell` |
| `Audio/` | 11 clip SFX đã nối sẵn cho thao tác nút, mở/đóng panel, quay, mở khoá, tua nhanh, popup nhận quà và HUD tiền |
| `Art/`, `Fonts/` | Sprite mẫu và font TMP PassionOne mà prefab dùng; `Art/ItemReceived/` là sprite hiệu ứng popup |

Package không có asset dữ liệu: các dòng phần thưởng nằm trên `Sample*Manager` (Daily 7 dòng, Spin 8 múi có trọng số, Online 18 dòng trên prefab manager, mỗi phút mở một ô). Mỗi dòng có `Key`, `Icon`, `Amount` và `ClaimSfx` (tuỳ chọn). Thông số của tính năng nằm trên **prefab panel**: cấu hình Open All của Daily (`openAllAdsRequired` 3, product IAP + `$4.99`), cooldown 30 phút và thời gian quay của Spin, thời lượng x2/x5 của Online, `x2AdsRequired` 1 / `x5AdsRequired` 2 và bộ Open All riêng của nó.

## Đem vào game của bạn

`SampleRewardGranter.Grant` là chỗ duy nhất biết một phần thưởng nghĩa là gì. Copy một `Sample*Manager`, điền `rows` bằng key/icon/số lượng của game bạn, rồi phát thưởng theo `Row.Key` trong `OnClaimed`. Giữ nhánh `default` báo lỗi: key lạ phải hiện lên Console, không được im lặng bỏ qua. Dòng nào `OnClaimed` null thì không phát gì và log `was NOT granted`; mọi lần phát đều có log.

Lúc khởi động, thay các adapter giả trong `SampleGameController.SetInfo()` bằng service thật của bạn (mẫu một dòng trong INTEGRATION-GUIDE mục 6) và gọi `SetInfo()` của các manager từ `Start()`. **Online Reward phải được khởi tạo ngay lúc boot**, nếu không thời gian chơi không được tính.

Popup nhận quà là của phía game: granter bắn `SampleItemGrantedEvent` (`Key`, `Icon`, `Amount`) và `SampleItemReceivedPanel` lắng nghe; package không bao giờ tự mở nó.

Sample phát toàn bộ SFX qua một `AudioSource` và `PlayOneShot` trong `RewardHooks.PlaySfx`. Khi tích hợp game thật, thay hook này bằng sound manager của game; panel và row không cần sửa.

Mọi thứ còn lại (luật nhận, streak, reset theo ngày UTC, quay có trọng số, cooldown, thời gian quay, tua nhanh, lưu dữ liệu) nằm trong panel của package, không cần sửa. Panel mở bằng `OpenPanel()`, đóng bằng `ClosePanel()`; `#region API` trong mỗi panel là toàn bộ những gì bạn cần dùng. Nút hay label nào trên panel cũng có thể tắt/xoá, tính năng tự giảm bớt chứ không lỗi. Art vòng quay có 8 múi, khớp 8 `LuckySpinWedge` dựng sẵn trong `LuckySpinPanel.wedges`; số row lệch với số múi sẽ có cảnh báo. Các nút `[Button]` debug trên panel cho phép ép múi trúng, chỉnh cooldown, cộng giây, xem phân phối quay và reset dữ liệu.
