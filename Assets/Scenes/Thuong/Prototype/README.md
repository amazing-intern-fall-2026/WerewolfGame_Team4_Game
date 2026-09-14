# Prototype của Thương

Mở **ThuongGameplayPrototype.unity** trong thư mục này bằng Unity 6000.6.0f1, nhấn **Play**.
Mở scene theo chế độ Single, không nạp thêm các scene gameplay khác.

## Điều khiển

- WASD / phím mũi tên / cần trái gamepad: di chuyển nhân vật màu xanh.
- E: hoàn thành nhiệm vụ khi đứng cách điểm đang sáng không quá 1.3 unit.
- Chuột: chọn Player trong bảng vote khi phase Voting bắt đầu.
- R: tải lại scene và chơi từ đầu, kể cả sau khi có kết quả.

## Đã gắn sẵn

- Map hình khối có nền, đường đi và tường biên.
- Một nhân vật, Rigidbody2D, collider và input; liên kết PlayerData ID 0.
- Sáu task asset và sáu station; mỗi ngày chọn ngẫu nhiên bốn nhiệm vụ, mỗi nhiệm vụ +10%.
- HUD có hai thanh: tổng tiến độ cả ván ở trên cùng, tiến độ trong ngày trên danh sách nhiệm vụ bên trái; kèm ngày/phase, đồng hồ và hướng dẫn.

Ví dụ hoàn thành 2/4 nhiệm vụ: thanh ngày 50%, tổng ván +20%. Sang ngày mới thanh ngày về 0%, tổng giữ nguyên. Thanh ngày 100% không tự kết thúc ván; thanh tổng 100% mới thắng.
- Bảng họp và năm nút vote, bảng kết quả.
- GameManager, PlayerManger, RoleManger, TaskManager, DayTimer, NightManager, VoteManger, DeathResolver, WinConditionManager.

Mặc định: ngày 45 giây → đêm 5 giây → thảo luận 8 giây → vote 10 giây.
Đạt tổng 100% thì dân thắng; hết ngày 7 chưa đủ thì sói thắng.
Các giá trị nằm trên object con trong Systems, có thể sửa trong Inspector.

Đây là bản thử offline. Bốn người còn lại chỉ là dữ liệu để thử vote, chưa có AI hoặc nhân vật điều khiển.
Role được phân ngẫu nhiên; Idiot không chết bởi vote. Đêm hiện là thời gian chờ, chưa có UI chọn kỹ năng.
Các điểm được đặt cố định, tập nhiệm vụ hoạt động được chọn ngẫu nhiên.
Hình ảnh đang dùng hình khối để thử logic; bạn có thể thay sprite bằng asset thật.

## Các thư mục

- Art: sprite khối, material unlit, font UI riêng.
- Data: sáu ScriptableObject nhiệm vụ.
- Scripts: cầu nối input và chơi lại dành riêng cho prototype.
- Editor: công cụ tạo scene và kiểm tra tự động.

Không cần thêm scene này vào Build Settings để thử trong Editor. Nếu build executable, thêm scene vào scene list của Build Profile.
Không gắn GameFlowManager/PhaseManager của Nhat hoặc PlayerController của Loi vào scene này.
Hai thư mục của Loi và Nhat được giữ nguyên.

## Phạm vi kiểm tra

Scene đã được Unity tạo/lưu và kiểm tra hình ảnh, có đầy đủ tham chiếu hai thanh tiến độ.
Bộ kiểm tra Play Mode tự động dùng bàn phím ảo cho di chuyển, gọi API nhiệm vụ/vote/tải lại để kiểm tra logic.
Kết quả lần chạy cuối: PASS (mã thoát 0, THUONG_PROTOTYPE_PLAYMODE_PASSED trong Logs/thuong-prototype-playmode.log). Đã xác minh thanh ngày 25% khi làm 1/4, reset về 0% ngày sau, thanh tổng giữ 10%, vòng ngày/đêm/vote và tải lại về ngày 1.
Phím E để tương tác và R để chơi lại cần thử thủ công trong Game view có focus; batchmode không mô phỏng ổn định sự kiện nhấn của hai phím này.

## Bỏ phiếu

Trong Discussion, bảng chỉ hiển thị đếm ngược chờ bỏ phiếu. Khi tiêu đề đổi sang BỎ PHIẾU, bấm Player còn sống; dòng trạng thái sẽ xác nhận người được chọn.
Mỗi vòng chỉ bỏ một phiếu; đã chết hoặc đã bỏ phiếu thì nút bị khóa và có thông báo lý do.
Sau khi cập nhật script trong Unity, dừng Play, chờ biên dịch xong rồi Play lại.
Nút đăng ký lại sự kiện trong OnEnable, tránh mất sự kiện khi Unity nạp lại script.
Kiểm tra đường raycast/click và việc bật lại nút tại Tools → Thuong → Check Vote Click (ngoài Play Mode); kết quả trong Logs/vote-click-result.txt.
