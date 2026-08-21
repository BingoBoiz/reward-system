# Changelog

Mọi thay đổi đáng chú ý của package được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), đánh số theo [SemVer](https://semver.org/).

## [1.1.0] - YYYY-MM-DD

### Added

- Analytics chạy sẵn trong package: mỗi prefab panel có field `trackEventName` trong tab Config (`daily_reward`, `lucky_spin`, `online_reward`), để trống là tắt tính năng đó. Package tự bắn các param key `open`, `claim`, `spin`, `speed_up`, `open_all`, `ads_start` / `ads_done` / `ads_fail`, `iap_start` / `iap_done` / `iap_fail`; giá trị là chi tiết (`Row.Key`, placement, product id).
- `RewardHooks.TrackEvent`: hook thứ 6, `(tên event, tên param, giá trị param)`. Chưa gán thì `Debug.Log` đúng ba giá trị sắp gửi. Nhóm `ads_*` / `iap_*` do `RewardFlow` bắn nên bắt được cả ca SDK ads không gọi lại callback nào.
- `SetInfo` cảnh báo khi `trackEventName` không hợp lệ (phải bắt đầu bằng chữ, chỉ chữ-số-gạch dưới, tối đa 40 ký tự).
- Sample Reward Demo có 11 clip được nối sẵn cho nút, mở/đóng panel, quay, mở khoá, tua nhanh, popup nhận quà, HUD tiền và hai `ClaimSfx` theo row. Ba panel có `openSfx` và `closeSfx`; Lucky Spin có `readySfx`; Online Reward có `unlockSfx` và `speedUpSfx`.

### Changed

- `RewardFlow` nhận thêm tên event ở constructor (`RewardFlow(owner, trackEventName)`). Panel tự truyền; game không dựng `RewardFlow` nên không ảnh hưởng gì.

## [1.0.0] - YYYY-MM-DD

### Added

- Daily Reward: dãy 7 thẻ điểm danh theo ngày UTC, có streak, nút Open All mở bằng ads hoặc IAP (`openAllUseAds` chọn chế độ).
- Online Reward: lưới phần thưởng mở dần theo thời gian chơi trong phiên, tua nhanh x2/x5 và Open All bằng ads hoặc IAP, dừng tính giờ khi app chạy nền.
- Lucky Spin: vòng quay có trọng số, một lượt free theo cooldown, quay thêm bằng ads; danh sách múi dựng sẵn trong prefab.
- Mỗi tính năng là một prefab panel tự lo luật, timer, lưu dữ liệu và ads/IAP. Game truyền `List<{Feature}Row>` vào `SetInfo(rows)` và phát thưởng qua callback `OnClaimed` của từng dòng; dữ liệu điền thiếu chỉ cảnh báo.
- `RewardHooks`: 5 hook tĩnh (`PlaySfx`, `ShowRewardedAd`, `PurchaseIap`, `GetIapPrice`, `ShowMessage`) có mặc định an toàn, gán một lần lúc khởi động.
- `RewardFlow`: mỗi lúc chỉ một yêu cầu ads/IAP, luôn kết thúc đúng một lần; ads không mở hoặc bị bỏ qua được phát hiện qua focus của app và báo qua `ShowMessage`.
- `RewardClock` và `TimeScheduler`: một nguồn thời gian duy nhất, hẹn giờ không cần `Update()`, label đếm ngược cập nhật đúng lúc số hiển thị đổi.
- Lưu: mỗi tính năng một key PlayerPrefs, JSON qua `JsonUtility`, profile có version, lưu ngay mỗi lần đổi trạng thái.
- Event thông báo cho từng tính năng (`{Feature}ChangedEvent`, `SpinStartedEvent`, `OnlineRewardSpeedUpEvent`, `{Feature}PanelClosedEvent`) để làm HUD và chấm đỏ.
- Sample Reward Demo: một scene (`RewardSample.unity`) với các `Sample*Manager`, adapter ads/IAP giả lập, hai panel HUD luôn hiện (`50_HomePanel`, `60_CurrencyPanel`), chấm đỏ tự chạy (`SampleRedDot`) và popup nhận quà.
- Tài liệu tiếng Việt: cách hoạt động, hướng dẫn tích hợp và mô tả từng tính năng trong `Documentation~/`.
