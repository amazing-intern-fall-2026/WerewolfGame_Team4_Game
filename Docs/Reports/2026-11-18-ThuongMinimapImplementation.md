# Báo cáo triển khai minimap Thương Online

Ngày: 18/11/2026
Phạm vi: HUD trong phiên Thương Online.

## Công việc hoàn thành

- Thêm minimap có thể thu gọn ở góc trên bên phải HUD. Bảng dùng thumbnail và bounds của map đang chọn, giữ đúng tỉ lệ giữa các layout.
- Đặt marker cho sáu trạm theo tọa độ trong `ThuongMapDefinition`; marker đổi màu/kích thước theo trạng thái nhiệm vụ đang mở hoặc đã hoàn thành.
- Hiển thị người chơi còn sống có vị trí được phép quan sát. Marker của người chơi local là mũi tên theo hướng nhân vật; các người chơi khác dùng chấm màu riêng.
- Lọc các vị trí có cờ `Hidden` trước khi vẽ. Snapshot ẩn tọa độ được host đặt về `(0,0)`, nên minimap không tạo marker giả ở gốc map hoặc để lộ vị trí bị che.
- Cập nhật marker theo trạng thái avatar đang được nội suy, dựng lại nền/điểm trạm khi đổi map và dọn UI khi session hoặc scene kết thúc.
- Giữ nguyên thao tác `Tab` để xem toàn bản đồ.

## Tệp thay đổi

- `Assets/Scripts/Thuong/Online/ThuongOnlineMinimap.cs` — dựng HUD, nền map, marker và trạng thái thu gọn.
- `Assets/Scripts/Thuong/Online/ThuongOnlineMinimap.cs.meta` — metadata Unity cho script.
- `Assets/Scripts/Thuong/Online/ThuongOnlineSession.cs` — gọi cập nhật minimap theo vòng đời session và dọn khi hủy.
- `Assets/Scripts/Thuong/Online/ThuongOnlineMaps.cs` — reset minimap khi xóa trạng thái map.

## Kiểm tra và giới hạn

- `git diff --check`: đạt, không có lỗi khoảng trắng.
- Chưa chạy Unity Editor, build hoặc kiểm tra trực quan trong game; phần compile/layout cần được xác nhận trong Unity.
- Phiên bản này hiển thị map, trạm và người chơi nhìn thấy được; chưa thêm marker xác hoặc điều khiển camera từ minimap.
