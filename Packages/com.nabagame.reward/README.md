# NabaGame Reward

Bộ tính năng thưởng dùng lại được cho game Unity. Package **không giữ dữ liệu**: mỗi tính năng là **một prefab panel** tự lo logic, timer, lưu (PlayerPrefs), ads/IAP và giao diện. Game viết một manager nhỏ giữ danh sách phần thưởng (`rows`); mỗi lần người chơi nhận thưởng, panel gọi ngược về callback `OnClaimed` của dòng đó để game phát thưởng. Âm thanh, ads, IAP, giá store, thông báo cho người chơi và analytics cắm vào qua 6 hook tĩnh, gán một lần lúc khởi động.

## Tính năng

| Tính năng | Mô tả | Chi tiết |
|---|---|---|
| Daily Reward | Dãy 7 thẻ điểm danh theo ngày, có streak và nút Open All bằng ads hoặc IAP | [FEATURES/daily-reward.md](Documentation~/FEATURES/daily-reward.md) |
| Online Reward | Lưới phần thưởng mở dần theo thời gian chơi, tua nhanh x2/x5 bằng ads, reset khi thoát game | [FEATURES/online-reward.md](Documentation~/FEATURES/online-reward.md) |
| Lucky Spin | Vòng quay có trọng số, một lượt free theo cooldown, quay thêm bằng ads | [FEATURES/lucky-spin.md](Documentation~/FEATURES/lucky-spin.md) |

## Yêu cầu

Unity 2022.3 trở lên. Unity không tự kéo dependency dạng git, nên project phải có sẵn:

| Dependency | Dùng cho | Tham chiếu asmdef |
|---|---|---|
| `com.nabagame.core` | EventManager, Singleton | `com.bmh.core.runtime` |
| `com.nabagame.ui` | BaseUI, UIPanel, UIManagerSingleton | `com.nabagame.ui.runtime` |
| `com.cysharp.unitask` | luồng async, timer | `UniTask` |
| Odin Inspector (`Assets/Plugins/Sirenix/`) | bảng `[TableList]`, nút debug | DLL biên dịch sẵn |
| DOTween (`Assets/Plugins/Demigiant/`) | tween UI | DLL biên dịch sẵn |

Không cần SDK ads hay plugin lưu dữ liệu: ads/IAP đi qua hook, lưu bằng PlayerPrefs.

## Cài đặt

Package Manager > Add package from git URL:

    https://github.com/<owner>/<repo>.git?path=Packages/com.nabagame.reward#v1.0.0

hoặc copy thư mục `Packages/com.nabagame.reward/` vào `Packages/` của project. Sau đó import sample **Reward Demo** trong mục Samples của package.

## Bắt đầu nhanh

1. Kéo prefab panel (ví dụ `DailyRewardPanel` trong sample) vào dưới UI root của bạn.
2. Copy manager mẫu tương ứng (`Sample*Manager`, khoảng 15 dòng) và điền bảng `rows` trong Inspector hoặc bằng code. Mỗi dòng gồm key, icon, số lượng, SFX khi nhận và callback `OnClaimed`.
3. Lúc khởi động, trong `Start()`, gán `RewardHooks.PlaySfx`, `ShowRewardedAd`, `PurchaseIap`, `GetIapPrice`, `ShowMessage`, `TrackEvent`, rồi gọi `SetInfo()` của manager (manager gọi tiếp `panel.SetInfo(rows)`).
4. Gắn nút mở panel vào `panel.OpenPanel()`. Bấm Play. Dữ liệu điền thiếu chỉ cảnh báo chứ không lỗi; dòng nào chưa gán `OnClaimed` thì khi nhận sẽ log `was NOT granted`.

Hướng dẫn đầy đủ: [Documentation~/INTEGRATION-GUIDE.md](Documentation~/INTEGRATION-GUIDE.md).
Cách package hoạt động: [Documentation~/ARCHITECTURE.md](Documentation~/ARCHITECTURE.md).
