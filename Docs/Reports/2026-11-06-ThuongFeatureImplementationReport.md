# Báo cáo triển khai tính năng Thương Online

**Ngày:** 06/11/2026

**Phạm vi:** `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity` và luồng Thương Online
**Tài liệu gốc:** [`Docs/ThuongFeatureRoadmap.md`](../ThuongFeatureRoadmap.md)

## Tóm tắt

Đã triển khai các mục trong lộ trình theo thứ tự: ổn định nền, vote “Không loại ai”, spectator, sổ ghi nhớ, mode tương tác gần bằng E, xác/báo xác và hướng dẫn người mới. Mode cổ điển vẫn là mặc định; mode tương tác gần được bật riêng trong thiết lập phòng.

## Công việc hoàn thành

### 1. Ổn định nền và ranh giới mode

- Giới hạn win evaluator của Thương/Lợi theo scene để tránh evaluator của scene khác thay đổi luật trận.
- Dùng phase/day từ snapshot host cho kiểm tra kỹ năng trên client.
- Thêm định danh `ClassicQueued` và `RealtimeProximity` vào luật phòng và snapshot.
- Giữ `ClassicQueued` làm mặc định; chỉ một mode được dùng trong mỗi trận.

### 2. Vote “Không loại ai”

- Tách lựa chọn bỏ phiếu thành chọn người chơi hoặc không loại ai.
- Host xác thực phase, trạng thái sống, quyền bỏ phiếu và request lặp.
- Tính phiếu không loại ai như một lựa chọn trong kết quả; hòa cao nhất không loại ai.
- Bổ sung nút trong giao diện họp, trạng thái phiếu và kết quả hiển thị số phiếu skip.

### 3. Spectator sau khi bị loại

- Người chơi bị loại chuyển sang chế độ xem sau khi quyền Hunter cuối cùng được xử lý.
- Có thể theo dõi người còn sống hoặc xem toàn bản đồ bằng camera local.
- Chặn di chuyển và action gameplay của người chết; che góc nhìn spectator vào ban đêm.
- Không đưa role, phe hay mục tiêu kỹ năng bí mật vào dữ liệu spectator công khai.

### 4. Sổ ghi nhớ riêng

- Thêm overlay ghi chú cá nhân, lưu trong bộ nhớ client và không gửi qua mạng.
- Chặn di chuyển và tương tác E khi đang nhập ghi chú.
- Xóa ghi chú khi rời phòng hoặc tạo ván mới.

### 5. Mode tương tác gần bằng E

- Thêm lựa chọn mode trong thiết lập phòng và giới hạn role pool của mode này ở preset Cơ bản.
- Cho phép đi lại trong Night của mode mới; host mô phỏng vị trí và kiểm tra tầm cùng đường nhìn.
- Nhấn nhanh E thực hiện tương tác hiện trên HUD. Giữ E ít nhất 0,3 giây mở danh sách khi có nhiều lựa chọn; phím hướng chọn, thả E chỉ lưu lựa chọn, nhấn E lần nữa mới gửi action.
- MVP áp dụng kỹ năng tức thời cho Khuyển tinh, Bảo vệ và Tiên tri. Tấn công Khuyển tinh dùng chung một lượt mỗi đêm; kết quả soi gửi riêng cho Tiên tri.
- Chặn các endpoint kỹ năng chọn mục tiêu toàn bản đồ trong mode mới.

### 6. Xác và báo xác

- Host tạo record xác từ cái chết trong gameplay; không tạo xác cho vote hoặc disconnect.
- Đồng bộ marker xác công khai trong snapshot, không đồng bộ người gây án.
- Host kiểm tra người báo còn sống, phase, khoảng cách, đường nhìn và trạng thái xác.
- Báo thành công chỉ mở một cuộc họp; xác đã báo bị gỡ và phase chuyển sang Discussion hiện có.

### 7. Hướng dẫn người mới

- Thêm hướng dẫn có thể bỏ qua và mở lại từ lobby hoặc HUD trong trận.
- Nội dung gồm điều khiển, nhiệm vụ, phase, vote/skip, spectator, quyền riêng tư và phần riêng cho từng mode.
- Hỗ trợ điều hướng bằng phím hướng/Enter/Esc và gamepad D-pad/nút.

## Giao thức mạng

Đã nâng phiên bản để host và client không dùng lẫn cấu trúc packet cũ:

| Thành phần | Phiên bản mới |
| --- | --- |
| Connection prefix | `THUONG10|` |
| Action | `thuong.online.action.v5` |
| Snapshot | `thuong.online.snapshot.v8` |
| Room state | `thuong.online.room-state.v2` |

Các tài liệu triển khai/recovery và các Editor harness đọc packet trực tiếp đã được cập nhật theo phiên bản mới.

## Phạm vi MVP và việc còn lại

- Mode tương tác gần hiện chỉ hỗ trợ ba role có kỹ năng E; role nâng cao, Hunter trong mode này, và các tương tác nhiều mục tiêu chưa được mở.
- Tutorial hiện là hướng dẫn dạng trang, chưa có sandbox thực hành.
- Chưa xác nhận hình ảnh UI thực tế ở 1280×720 và 1920×1080.
- Chưa chạy host + hai client LAN, Relay, độ trễ mạng, replay hoặc reconnect trong Unity.

## Kiểm tra đã thực hiện

- Rà soát tĩnh các luồng protocol, action validation, reset epoch và vòng đời lobby/replay.
- Chạy `git diff --check`; không phát hiện lỗi whitespace.
- Không chạy Unity Editor hoặc test gameplay/mạng trong lượt triển khai này.
