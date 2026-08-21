# Online Reward

`OnlineRewardPanel` lo toàn bộ tính năng; game viết một manager kiểu `SampleOnlineRewardManager`. Lưới **theo phiên chơi**. Tài liệu này là chuẩn cho hành vi và dữ liệu.

## Là gì

Lưới phần thưởng mở khoá lần lượt theo thời gian trong lúc người chơi còn mở game. Xem rewarded ad để bật tua nhanh x2/x5 hoặc mở cả lưới một lần. Thoát game là mất tiến độ: "Reward reset if you leave!".

## Giao diện

- Popup tiêu đề "REWARDS!" (icon quà), phụ đề "Reward reset if you leave!", nút đóng (X) góc trên phải, bảng tối trên nền mờ.
- Lưới thẻ (mẫu: **3 hàng x 6 cột = 18**; số row quyết định lưới qua `GridLayoutGroup`). Thẻ gồm: khung màu (đổi theo cột từ `frameSprites`), label số lượng trên, icon giữa, đếm ngược `mm:ss` (hoặc `h:mm:ss`) dưới.
  - **Nhận được**: chữ `CLAIM!`, nhấp nháy. Chấm đỏ trên ô là của game (`SampleRedDot`, key `OnlineRewardCell`), không thuộc ô.
  - **Đang đếm**: mờ, hiện thời gian còn lại.
  - **Đã nhận**: dấu tick, icon tối đi, không bấm được.
- Nút dưới: `OPEN ALL`, `X2 SPEED` / `X5 SPEED` (cầu vồng, icon tua nhanh, badge ads; mỗi nút hiện đếm `n/required` khi cần hơn 1 ads). Khi tua nhanh đang bật, nút hiện thời gian buff còn lại và không bấm được; timer các ô đếm từng giây hiển thị (năm nhịp mỗi giây thật ở x5), không nhảy số.
- `OPEN ALL` chiếm một chỗ với **hai nút dựng sẵn xếp chồng cùng vị trí**: `OpenAllAdsButton` (xanh lá, icon video, đếm `n/required`) và `OpenAllIapButton` (xanh dương, icon quà, label giá). Chỉ một nút hiện, chọn bằng `openAllUseAds`; cùng kiểu với OPEN ALL của Daily Reward.

## Dữ liệu (dev điền)

Manager của game (mẫu: `Samples~/RewardDemo/Scripts/SampleOnlineRewardManager.cs`) giữ `[TableList] public List<OnlineRewardRow> rows`, gán `OnClaimed` cho từng row, rồi truyền list cho `OnlineRewardPanel.SetInfo(rows)` **lúc boot**; thời gian chơi tính từ lệnh đó, nên không được khởi tạo muộn lúc mở lần đầu.

Row (vị trí trong list là ô: `rows[0]` mở trước):
`{ string Key, Sprite Icon, long Amount, int UnlockAfterSeconds, AudioClip ClaimSfx, Action<OnlineRewardRow> OnClaimed }`. Mốc thời gian cộng dồn trong phiên, phải tăng dần; constructor cho biết thứ tự điền.

Kiểm tra: thiếu `Key`/`Icon`/`Amount` sinh một cảnh báo gộp qua `OnlineRewardRow.Warn(rows)`; list rỗng hoặc `UnlockAfterSeconds` không tăng dần thì ném lỗi (máy timer không chạy được).

Thông số trên prefab `OnlineRewardPanel`, tab Config:

| Thông số | Mặc định | Ý nghĩa |
|---|---|---|
| `x2DurationSeconds` | 120 | buff x2 kéo dài bao lâu |
| `x2AdsRequired` | 1 | số ads để bật x2 (đếm được lưu, hiện `n/required` khi lớn hơn 1) |
| `x5DurationSeconds` | 120 | buff x5 kéo dài bao lâu |
| `x5AdsRequired` | 2 | số ads để bật x5 |
| `openAllUseAds` | bật | bật hiện nút OPEN ALL ads, tắt hiện nút IAP |
| `openAllAdsRequired` | 1 | số ads cho OPEN ALL khi `openAllUseAds` bật |
| `openAllIapProductId` | "" | product IAP cho OPEN ALL khi `openAllUseAds` tắt |
| `openAllIapPriceText` | "" | giá dự phòng trên nút IAP; `RewardHooks.GetIapPrice` thắng khi store trả lời |

Tab FX: `cellStaggerDelay`, `openSfx`, `closeSfx`, `unlockSfx`, `speedUpSfx`, `buttonSfx`. Sample điền 18 row trên `Prefabs/SampleOnlineRewardManager.prefab`.

`WarnConfig()` chạy trong `SetInfo` và cảnh báo (không ném lỗi) khi cấu hình của chế độ đang chọn rỗng: `x2AdsRequired`/`x5AdsRequired` nhỏ hơn hoặc bằng 0 thì tắt booster đó, `openAllUseAds` bật mà `openAllAdsRequired` nhỏ hơn hoặc bằng 0 thì tắt OPEN ALL, `openAllUseAds` tắt mà `openAllIapProductId` rỗng cũng vậy, có product id mà chuỗi giá rỗng thì cảnh báo riêng.

## Trạng thái và lưu

Theo phiên có chủ đích; trạng thái lưới chỉ nằm trong bộ nhớ:

- Thời gian chơi trong phiên dùng **mốc baseline** (ARCHITECTURE.md mục 5): `accumulated + (RewardClock.MonotonicSeconds - baseline) * activeMultiplier`, chốt theo hệ số cũ trước khi đổi hệ số; không cộng mỗi frame, không `Update()`. Ô khoá gần nhất được hẹn qua `TimeScheduler` (mốc chia theo hệ số đang bật); một handle thứ hai hẹn lúc buff hết sớm nhất.
- **Mất focus thì chốt + lưu; lấy lại focus thì đặt lại baseline** để thời gian app bị treo không tính là thời gian chơi. Panel nghe `Application.focusChanged` tĩnh, không nghe `OnApplicationPause`; event tĩnh vẫn bắn kể cả khi UI framework của game deactivate GameObject panel đang ẩn. Mốc hết buff là giờ thực, nên buff có thể hết trong lúc app treo. Tắt app reset cả lưới (chạy nền không kết thúc phiên, chỉ tắt app mới kết thúc). Trong Editor, alt-tab tính là mất focus; thời gian alt-tab không cộng.
- Khi mọi ô đã nhận, chu kỳ reset (xoá đã nhận, thời gian về 0, timer chạy lại) để panel không bao giờ chết.
- Key PlayerPrefs `NabaReward.Online` (Version 1) chỉ lưu `Version`, `SpeedUpX2Ads`, `SpeedUpX5Ads` và `OpenAllAdsWatched`; số ads đã xem dở sống qua các phiên, buff và lưới thì không. `ResetSession()` xoá cả ba.

## API: `OnlineRewardPanel`, `#region API`

- `SetInfo(List<OnlineRewardRow> rows)`: khởi tạo duy nhất: kiểm tra dữ liệu, load lưu, bắt đầu tính giờ, hẹn mốc mở khoá/buff, dựng ô, gắn listener. **Gọi từ `Start()` lúc boot.**
- `OpenPanel()` / `ClosePanel()`: bật/tắt cho dev. Một vòng lặp UniTask refresh label đếm ngược và nút booster chỉ khi đang hiện, thức dậy đúng mốc số hiển thị đổi (`RewardClock.MsUntilNextTick`, `DelayType.Realtime`) nên buff x5 đếm 59, 58, 57... thay vì nhảy số; mỗi lần đổi trạng thái nhịp được đặt lại.
- Truy vấn: `int SlotCount`, `bool HasClaimable` (dùng cho chấm đỏ), `OnlineSlotState GetState(int slot)` (an toàn; trả `Locked` trước `SetInfo` hoặc ngoài khoảng).
- `ResetSession()`: reset cho QA/debug.
- Hằng: `SaveKey`, `ProfileVersion`, `SpeedUpX2`, `SpeedUpX5`, `OpenAllPlacement`, `X2Placement`, `X5Placement`.

Một lần nhận: đánh dấu ô, phát `ClaimSfx` của row, `Debug.Log` phần thưởng, rồi gọi `Row.OnClaimed`; game phát thưởng ở đó. OPEN ALL nhận mọi ô chưa nhận trong một frame (mỗi ô một `OnClaimed`, nên popup gộp hiện một lần), mở bằng `openAllAdsRequired` rewarded ads (đếm được lưu) hoặc mua `openAllIapProductId`, tuỳ `openAllUseAds`; luồng của chế độ không chọn bị từ chối. X2/X5 chạy luồng ads (`RewardFlow`; mỗi nút cần đủ `x2AdsRequired`/`x5AdsRequired` ads, đếm được lưu), cộng dồn thành x7, hết theo mốc giờ thực.

## Event / hook / placement

- **Phát thưởng: `Row.OnClaimed`**, gọi cho từng ô (nhận lẻ và từng ô của OPEN ALL, cùng frame); không có `OnlineRewardClaimedEvent`.
- `OnlineRewardChangedEvent`: thông báo, bắn khi mở khoá ô, nhận, reset chu kỳ, đổi hệ số.
- `OnlineRewardSpeedUpEvent { int Multiplier; }`: thông báo, bắn khi tua nhanh bật sau luồng ads.
- `OnlineRewardPanelClosedEvent`: thông báo, bắn khi người chơi đóng panel.
- Hook dùng: `PlaySfx`, `ShowRewardedAd`, `PurchaseIap` (chỉ khi `openAllUseAds` tắt). Tuỳ chọn; hook chưa gán chỉ LogError rồi chạy tiếp.
- Placement: `OnlineReward_x2Speed`, `OnlineReward_x5Speed`, `OnlineReward_OpenAll`.
- Analytics: `trackEventName` mặc định `online_reward`. Param key bắn ra: `open`, `claim` (giá trị = `Row.Key`), `speed_up` (`x2`/`x5`), `open_all` (`ads`/`iap`), cùng nhóm `ads_*` / `iap_*` với giá trị là placement / product id. Để trống `trackEventName` là tắt.

## Checklist kiểm tra

1. Phiên mới: ô 1 đếm ngược từ mốc của row; các ô mở đúng thứ tự; `CLAIM!` hiện khi mở và nhận thì vào `OnClaimed` của game (tiền đổi, có log, popup nhận quà).
2. X2 rồi X5: timer tăng tốc rõ; x2+x5 cộng thành x7; mỗi booster cần đủ số ads đã cấu hình (đếm `n/required`, được lưu, ẩn khi chỉ cần 1); bỏ qua ads thì hệ số không đổi; trong Editor bật ngay.
3. Bấm liên tục nút nhận và nút tua: chỉ một lần phát / một yêu cầu ads (`RewardFlow` chặn); ads bị bỏ qua, bị giới hạn hay không có thì có thông báo `ShowMessage` và số đếm không đổi (ARCHITECTURE mục 8).
4. OPEN ALL với `openAllUseAds` bật: xem đủ `openAllAdsRequired` ads (số đếm tăng trên nút), mọi ô chưa nhận được nhận trong một frame (một popup gộp), rồi chu kỳ reset và số đếm về 0. Tắt `openAllUseAds` và có product id: nút IAP xanh dương kèm giá thay nút xanh lá, huỷ mua thì không phát và có log, mua thành công thì mở hết.
5. Tắt app mở lại: lưới về đầu (theo phiên), console sạch.
6. Chạy nền / mất focus vài phút, **panel đang ẩn và kể cả khi tick `disableWhenHidden`**, rồi quay lại: timer không nhảy vì thời gian treo (`Application.focusChanged` chốt).
7. Mở/đóng panel nhiều lần: không trùng listener; vòng đếm ngược dừng khi ẩn, kể cả gọi `Hide()` trần.
8. Thử xoá nút: tắt/xoá `openAllAdsButton`, `openAllIapButton`, label open-all bất kỳ, `button`/`label`/`adsCountLabel` của một booster, hay `cellTemplate`: không lỗi, không kẹt (thiếu template = panel trống + một lỗi log).

## Quy tắc đã chốt

- Kết thúc phiên: **chỉ khi tắt app**; chạy nền tạm dừng tính giờ (đặt lại baseline khi lấy lại focus) nhưng giữ lưới.
- Cộng dồn x2/x5: cả hai cùng bật thì thời gian chơi chạy x7.
- Ads cho X5: **cần 2 ads** (`x5AdsRequired`, serialized); số đếm dở được lưu trong `NabaReward.Online`.
- Ads cho X2: **serialized như X5** (`x2AdsRequired`, mặc định 1) thay vì cố định một ads; số đếm dở cũng được lưu, badge `n/required` tự ẩn khi chỉ cần 1.
- OPEN ALL: **ads hoặc IAP, giống Daily Reward**; `openAllUseAds` chọn chế độ, prefab dựng sẵn cả hai nút cùng chỗ và panel chỉ hiện một (placement `OnlineReward_OpenAll`, product `openAllIapProductId`). Ô đang khoá cũng được nhận.
- Xử lý pause: **`Application.focusChanged` thay cho `OnApplicationPause`**: message của Unity chết trên GameObject bị deactivate; event tĩnh thì không, nên lời hứa "không tính thời gian treo" đúng bất kể game ẩn panel kiểu gì.
