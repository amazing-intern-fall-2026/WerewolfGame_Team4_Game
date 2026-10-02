# Prototype của Thương

Mở **ThuongGameplayPrototype.unity** trong thư mục này bằng Unity 6000.6.0f1, nhấn **Play**.
Mở scene theo chế độ Single, không nạp thêm các scene gameplay khác.

## Điều khiển

- F1–F12: chọn nhanh nhân vật 1–12; PageDown/PageUp: chuyển lần lượt qua toàn bộ nhân vật.
- WASD / phím mũi tên / cần trái gamepad: di chuyển nhân vật đang chọn.
- E: hoàn thành nhiệm vụ khi đứng cách điểm đang sáng không quá 1.3 unit.
- Chuột: chọn Player trong bảng vote khi phase Voting bắt đầu.
- R: tải lại scene và chơi từ đầu, kể cả sau khi có kết quả.

## Đã gắn sẵn

- Map hình khối có nền, đường đi và tường biên.
- Số nhân vật theo `PlayerManager.prototypeLobbySize` được script test tạo khi Play từ Player gốc, mỗi nhân vật có Rigidbody2D, collider, ID và vai riêng.
- Sáu task asset và sáu station; mỗi ngày chọn ngẫu nhiên bốn nhiệm vụ, mỗi nhiệm vụ +10%.
- HUD có hai thanh: tổng tiến độ cả ván ở trên cùng, tiến độ trong ngày trên danh sách nhiệm vụ bên trái; kèm ngày/phase, đồng hồ và hướng dẫn.
- Khi Play, HUD dùng màu và nhãn rõ cho từng giai đoạn, đồng hồ dạng phút:giây, nhiệm vụ đã xong được tô màu, và thanh hướng dẫn ở cạnh dưới thay đổi theo giai đoạn. Bảng vote và màn hình kết quả cũng hiển thị trạng thái rõ hơn.

Ví dụ hoàn thành 2/4 nhiệm vụ: thanh ngày 50%, tổng ván +20%. Sang ngày mới thanh ngày về 0%, tổng giữ nguyên. Thanh ngày 100% không tự kết thúc ván; thanh tổng 100% mới thắng.
- Bảng họp và năm nút mục tiêu vote; Player 6 trở đi chưa có nút mục tiêu trong bảng này.
- GameRoleManager, PlayerManager, RoleManager, TaskManager, DayTimer, NightManager, VoteManager, DeathResolver, WinConditionManager.

Mặc định: ngày 45 giây → đêm 20 giây → thảo luận 8 giây → vote 10 giây.
Đạt tổng 100% thì dân thắng; hết ngày 7 chưa đủ thì sói thắng.
Các giá trị nằm trên object con trong Systems, có thể sửa trong Inspector.

Đây là bản thử offline trên một máy. `ThuongFourRoleOfflineTest` gắn trong `Systems` tạo đúng số nhân vật đặt ở `PlayerManager.prototypeLobbySize` trong Inspector. `RoleManager` lấy đúng số người trong lobby, rút ngẫu nhiên từ 17 class Role hiện có ở thư mục Thương và Nhật rồi gán mỗi người một vai. Vai không còn gắn cố định theo Player ID. Chỉ một nhân vật nhận input tại một thời điểm. Xem thêm `TEST_4_VAI_OFFLINE.md`.
Đổi `PlayerManager.prototypeLobbySize` trước khi Play để tạo đúng số nhân vật mong muốn. Console ghi seed và vai của từng người để có thể kiểm tra lại.
Mở **ROLE / KỸ NĂNG** ở góc phải để xem Role của Player đang chọn. Khi vào đêm, panel tự mở nếu Player đang chọn có kỹ năng chọn mục tiêu đã nối; sau khi đổi nhân vật trong đêm, bấm nút này để mở panel. Bấm **CHỌN MỤC TIÊU**, chọn một Player còn sống khác mình, hoặc bấm **HỦY / QUAY LẠI** để không dùng kỹ năng. Kết quả thực hiện hiện ngay trong panel.
Sau khi phân vai, một thẻ giới thiệu Role của Player local hiện trước ngày đầu tiên: ảnh nhân vật và tên Role ở bên trái, mô tả chi tiết ở bên phải. Thẻ tự đóng sau 25 giây (có thể chỉnh `GameRoleManager.roleRevealDuration` trong Inspector), hoặc bấm **OK / BẮT ĐẦU** để vào ngày đầu ngay. Đồng hồ ban ngày chỉ bắt đầu sau khi thẻ đóng.
Ảnh mặc định dùng `Art/Resources/RoleAbilityIcon.png`. Để dùng ảnh PNG riêng, tạo thư mục `Art/Resources/RolePortraits` rồi thêm sprite đặt tên đúng `RoleType` như `Villager.png`, `Seer.png`, `DogSpirit.png`; để Texture Type là **Sprite (2D and UI)**. Nếu không có ảnh riêng, thẻ dùng ảnh mặc định.
Tiên tri chỉ nhận kết quả **Dogspirit** hoặc **Không phải Dogspirit**; Role cụ thể của mục tiêu không được hiển thị. Role không có kỹ năng chủ động chỉ hiện mô tả kỹ năng nội tại/thảo luận/bỏ phiếu.
Các điểm được đặt cố định, tập nhiệm vụ hoạt động được chọn ngẫu nhiên.
Hình ảnh đang dùng hình khối để thử logic; bạn có thể thay sprite bằng asset thật.

## Các thư mục

- Art: sprite khối, icon Role Ability trong `Art/Resources`, material unlit, font UI riêng.
- Data: sáu ScriptableObject nhiệm vụ.
- Scripts: cầu nối input và chơi lại dành riêng cho prototype.
- Editor: công cụ tạo scene và kiểm tra tự động.

Không cần thêm scene này vào Build Settings để thử trong Editor. Nếu build executable, thêm scene vào scene list của Build Profile.
Không gắn GameFlowManager/PhaseManager của Nhat hoặc PlayerController của Loi vào scene này.
Hai thư mục của Loi và Nhat được giữ nguyên.

## Phase 1: Trạng thái sống/chết

- Nguồn trạng thái duy nhất là `PlayerData.isAlive`. Các API `PlayerManager.IsAlive`, `GetAlivePlayers` và `SetAliveState` đọc/cập nhật nguồn này; `AliveStateChanged` thông báo khi trạng thái thực sự thay đổi.
- `DeathResolver` chuyển trạng thái qua manager rồi giữ nguyên đồng bộ mạng, callback Role và xử lý Lover hiện có. Gọi chết lặp không phát thêm callback.
- Player chết không di chuyển, làm nhiệm vụ, vote hoặc dùng kỹ năng yêu cầu còn sống. Bẫy sau khi chết của Hunter vẫn được giữ theo luật hiện tại.
- `TaskManager.TryCompleteTask(playerID, task)` kiểm tra người thực hiện tại API; entry `CompleteTask(task)` cũ vẫn tương thích với Player local của HUD.
- HUD tự thêm `PlayerRosterUI` khi chạy. Nút PLAYERS mở danh sách toàn lobby có nhãn ALIVE/DEAD, màu trạng thái và cuộn; không lộ Role. Không cần sửa Inspector hay tạo lại scene.
- Chạy `Tools > Thuong > Check Phase 1 (Alive-Dead)` ngoài Play Mode. Khi đạt, Console có `THUONG_PHASE1_PASSED`; ảnh kiểm tra nằm trong `Logs/thuong-phase1-roster*.png`.

Hướng dẫn đầy đủ cho 13 phase: [HUONG_DAN_TRIEN_KHAI_LOGIC_GAME_MA_SOI.md](HUONG_DAN_TRIEN_KHAI_LOGIC_GAME_MA_SOI.md).

## Phạm vi kiểm tra

Scene đã được Unity tạo/lưu và kiểm tra hình ảnh, có đầy đủ tham chiếu hai thanh tiến độ.
Bộ kiểm tra Play Mode tự động dùng bàn phím ảo cho di chuyển, gọi API nhiệm vụ/vote/tải lại để kiểm tra logic.
Bộ kiểm tra hiện xác minh thẻ Role xuất hiện sau khi phân vai, có ảnh và mô tả, nút OK bắt đầu ngày đầu ngay; sau khi tải lại scene, thẻ tự đóng theo thời gian cấu hình. Ngoài ra vẫn kiểm tra tiến độ nhiệm vụ và vòng ngày/đêm/vote. Khi chạy đạt, log sẽ có `THUONG_PROTOTYPE_PLAYMODE_PASSED`.
Phím E để tương tác và R để chơi lại cần thử thủ công trong Game view có focus; batchmode không mô phỏng ổn định sự kiện nhấn của hai phím này.

## Bỏ phiếu

Trong Discussion, bảng chỉ hiển thị đếm ngược chờ bỏ phiếu. Khi tiêu đề đổi sang BỎ PHIẾU, bấm Player còn sống; dòng trạng thái sẽ xác nhận người được chọn.
Mỗi vòng chỉ bỏ một phiếu; đã chết hoặc đã bỏ phiếu thì nút bị khóa và có thông báo lý do.
Sau khi cập nhật script trong Unity, dừng Play, chờ biên dịch xong rồi Play lại.
Nút đăng ký lại sự kiện trong OnEnable, tránh mất sự kiện khi Unity nạp lại script.
Kiểm tra đường raycast/click và việc bật lại nút tại Tools → Thuong → Check Vote Click (ngoài Play Mode); kết quả trong Logs/vote-click-result.txt.
