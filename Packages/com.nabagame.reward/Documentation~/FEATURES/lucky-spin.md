# Lucky Spin

`LuckySpinPanel` lo toàn bộ tính năng; game viết một manager kiểu `SampleLuckySpinManager`. Art mẫu: sprite vòng 8 múi `spin_0001_vong-quay`, kim `spin_0000_kim-xoay`. Tài liệu này là chuẩn cho hành vi và dữ liệu.

## Là gì

Vòng quay phần thưởng có trọng số. Mỗi khoảng cooldown có một lượt free; quay thêm bằng rewarded ad. Vòng giảm tốc dừng đúng múi đã roll và phần thưởng phát qua `OnClaimed` của row.

## Giao diện

- Popup có vòng quay ở giữa, nút đóng (X) góc trên phải.
- **Vòng N múi** (N = list `wedges` dựng sẵn trong prefab, bản mẫu là 8 khớp với art; panel cảnh báo khi `rows.Count` lệch, và nếu row nhiều hơn múi thì kim có thể dừng ở múi bị quấn vòng trong khi phần thưởng `rows[index]` vẫn đúng), mỗi múi có icon + label số lượng (`1.5K`, `100B`, `X10`, ...); có thể tô một múi jackpot nổi bật. Viền vàng có đèn, tâm trắng, kim cố định ở 12 giờ.
- Nút quay dưới vòng:
  - **Có lượt free**: nút `SPIN` xanh lá. Chấm đỏ trên nút là của game (`SampleRedDot`, key `LuckySpin`), không thuộc panel.
  - **Đang cooldown**: dạng quay bằng ads: nút `SPIN` có icon video và dòng `Free spin in mm:ss` đếm ngược tới lượt free kế.
- Khi đang quay mọi nút khoá; vòng tăng tốc rồi giảm tốc dừng ở múi kết quả (DOTween ease-out); dừng xong có phản hồi (SFX + phát thưởng).

## Dữ liệu (dev điền)

Manager của game (mẫu: `Samples~/RewardDemo/Scripts/SampleLuckySpinManager.cs`) giữ `[TableList] public List<LuckySpinRow> rows`, gán `OnClaimed` cho từng row, rồi truyền list cho `LuckySpinPanel.SetInfo(rows)` lúc boot.

Row (vị trí trong list là múi: `rows[0]` ở 12 giờ, theo chiều kim đồng hồ):
`{ string Key, Sprite Icon, long Amount, int Weight, AudioClip ClaimSfx, Action<LuckySpinRow> OnClaimed }`. Constructor cho biết thứ tự điền.

Kiểm tra nhẹ tay: thiếu `Key`/`Icon`/`Amount` hoặc `Weight <= 0` sinh một cảnh báo gộp qua `LuckySpinRow.Warn(rows)`; ít hơn 2 row mới ném lỗi. Mọi trọng số đều sai thì roll đều.

Thông số trên prefab `LuckySpinPanel`: `freeSpinCooldownSeconds` (1800), `spinDurationSeconds` (4.5), `spinFullTurns` (5), `openSfx`, `closeSfx`, `spinStartSfx`, `tickSfx`, `landSfx`, `readySfx`, `wedges` (list múi dựng sẵn, theo chiều kim từ 12 giờ; vị trí đặt trong prefab), `buttonSfx`.

## Lưu (key PlayerPrefs `NabaReward.Spin`)

| Field | Ý nghĩa |
|---|---|
| `Version` | version payload |
| `NextFreeSpinAtMs` | mốc unix ms của lượt free kế (0 = có ngay); sống qua tắt app nhờ giờ thực |

## API: `LuckySpinPanel`, `#region API`

- `SetInfo(List<LuckySpinRow> rows)`: khởi tạo duy nhất: kiểm tra dữ liệu, load lưu, hẹn mốc cooldown (`TimeScheduler`), gắn múi dựng sẵn, gắn listener. Gọi từ `Start()` lúc boot; panel vẫn ẩn.
- `OpenPanel()` / `ClosePanel()`: bật/tắt cho dev. `ClosePanel()` từ chối khi `IsSpinning`.
- Truy vấn: `bool FreeSpinReady` (dùng cho chấm đỏ), `double SecondsUntilFreeSpin`, `bool IsSpinning`, `bool CanSpinByAd`.
- `ResetProfile()`: reset cho QA/debug.
- Hằng: `SaveKey`, `ProfileVersion`, `AdPlacement`.

Nút quay chạy lượt free khi sẵn sàng, không thì quay bằng ads (`RewardFlow`, placement `LuckySpin_AdSpin`). Roll theo trọng số trên rows và chốt **trước** khi chạy hiệu ứng: panel bắn `SpinStartedEvent`, tween lấy đà rồi giảm tốc về múi đã chốt, chờ `spinDurationSeconds` theo unscaled time, rồi lúc dừng phát `landSfx` + `ClaimSfx` của row, `Debug.Log` phần thưởng và gọi `Row.OnClaimed`; game phát thưởng ở đó.

## Event / hook / placement

- **Phát thưởng: `Row.OnClaimed`**, gọi lúc vòng dừng; không có `SpinResultEvent`.
- `SpinStartedEvent { int WedgeIndex; float DurationSeconds; }`: thông báo, bắn khi bắt đầu quay (múi đã roll xong).
- `LuckySpinChangedEvent`: thông báo, bắn khi đổi trạng thái lượt free / đang quay (chấm đỏ / refresh).
- `LuckySpinPanelClosedEvent`: thông báo, bắn khi người chơi đóng panel.
- Hook dùng: `PlaySfx`, `ShowRewardedAd`. Tuỳ chọn; hook chưa gán chỉ LogError rồi chạy tiếp.
- Placement: `LuckySpin_AdSpin`.
- Analytics: `trackEventName` mặc định `lucky_spin`. Param key bắn ra: `open`, `spin` (`free`/`ads`), `claim` (giá trị = `Row.Key`, bắn lúc vòng dừng), cùng nhóm `ads_*` với giá trị là placement. Để trống `trackEventName` là tắt.

## Checklist kiểm tra

1. Lượt free: vòng dừng đúng múi đã roll; đúng một `OnClaimed` vào hàm của game (tiền đổi, có log trên Console); label cooldown bắt đầu (`Free spin in mm:ss`).
2. Phân phối: `PreviewRollDistribution`; tần suất múi theo trọng số.
3. Quay bằng ads: dùng được trong cooldown; không đóng panel được khi ads đang mở; ads bị bỏ qua, bị giới hạn hay không có thì không quay, có thông báo `ShowMessage`, nút mở lại (`RewardFlow`, ARCHITECTURE mục 8); trong Editor quay ngay; bấm liên tục khi `IsSpinning` hoặc đang chờ ads chỉ quay một lần.
4. Tắt app mở lại giữa cooldown: thời gian còn lại đúng theo mốc giờ thực; tắt app giữa lúc quay: không phát đôi (phát khi dừng; tắt giữa chừng thì mất lượt đó).
5. Mở/đóng panel nhiều lần: không trùng listener; tween bị kill khi đóng; `Hide()` dừng vòng đếm ngược.
6. Thử xoá nút: tắt/xoá `spinButton`, `cooldownLabel`, `pointer`: không lỗi, không kẹt.

## Quy tắc đã chốt

- Múi jackpot: phát thưởng bình thường, không hiệu ứng riêng (`OnClaimed` của game tự thêm nếu muốn).
- Quay bằng ads không giới hạn trong cooldown.
- Tắt app giữa lúc quay: phát thưởng khi vòng dừng; tắt giữa chừng thì mất lượt, không bao giờ phát đôi.
- Nội dung múi luôn đứng thẳng khi vòng xoay (`LuckySpinWedge.KeepUpright`) để chữ đọc được ở mọi góc dừng.
