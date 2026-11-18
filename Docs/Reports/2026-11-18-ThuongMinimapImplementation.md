# Báo cáo hoàn thành bảng bản đồ thu nhỏ

Ngày: 18/11/2026
Phạm vi: giao diện định hướng trong trải nghiệm trực tuyến.

## Công việc hoàn thành

- Bổ sung một bảng bản đồ nhỏ ở góc trên bên phải màn hình. Người dùng có thể thu gọn bảng để có thêm không gian, hoặc mở rộng khi cần xem vị trí rõ hơn. Hình nền tự thay đổi theo khu vực đang sử dụng và giữ đúng tỷ lệ để các điểm trên bản đồ không bị kéo lệch.
- Đánh dấu sáu địa điểm quan trọng bằng biểu tượng riêng. Màu sắc và kích thước giúp phân biệt điểm đang cần chú ý, điểm đã hoàn tất và các điểm còn lại dùng để định hướng.
- Hiển thị vị trí hiện tại bằng mũi tên có hướng, giúp người dùng nhận biết mình đang ở đâu và đang quay về phía nào. Vị trí của những người khác chỉ xuất hiện khi được phép quan sát.
- Ẩn hoàn toàn những vị trí không được phép hiển thị. Dữ liệu thay thế do hệ thống gửi về không bị coi nhầm là một vị trí thật trên bản đồ.
- Tự cập nhật dấu vị trí khi người dùng di chuyển, làm mới nền và các điểm đánh dấu khi đổi khu vực, đồng thời dọn bảng khi rời màn hình để không giữ lại thông tin cũ.
- Giữ nguyên thao tác xem toàn bản đồ bằng phím `Tab`. Bảng bản đồ nhỏ bổ sung cách quan sát nhanh mà không đổi cách điều khiển hiện tại.

## Tệp thay đổi

- `Assets/Scripts/Thuong/Online/ThuongOnlineMinimap.cs` — dựng HUD, nền map, marker và trạng thái thu gọn.
- `Assets/Scripts/Thuong/Online/ThuongOnlineMinimap.cs.meta` — metadata Unity cho script.
- `Assets/Scripts/Thuong/Online/ThuongOnlineSession.cs` — gọi cập nhật minimap theo vòng đời session và dọn khi hủy.
- `Assets/Scripts/Thuong/Online/ThuongOnlineMaps.cs` — reset minimap khi xóa trạng thái map.

## Kiểm tra và giới hạn

- Kiểm tra tính nhất quán định dạng: đạt, không phát hiện lỗi khoảng trắng.
- Chưa mở dự án bằng công cụ xây dựng hoặc chạy phiên bản cài đặt để kiểm tra trực quan; bố cục và hoạt động trên thiết bị thực cần được xác nhận.
- Phiên bản hiện tại tập trung vào nền bản đồ, các điểm cần chú ý và vị trí được phép quan sát. Chưa có dấu đánh dấu các vị trí cần xử lý hoặc chức năng điều khiển góc nhìn trực tiếp từ bảng.
