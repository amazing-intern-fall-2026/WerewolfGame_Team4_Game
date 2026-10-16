# Form UI lobby và thẻ role

Luồng theo bản phác thảo UI:

1. Host/join để tập hợp danh sách thật; chủ phòng tùy chỉnh sức chứa tối đa 25. Người chơi chọn ngoại hình.
2. Chủ phòng bấm **PHÂN VAI**. Hệ thống random role theo số người hiện có rồi khóa danh sách, sức chứa và ngoại hình. Nút đổi thành **XEM PHÂN VAI**, có trên máy của từng người; không tự mở thẻ.
3. Mỗi người bấm **XEM PHÂN VAI** để mở thẻ riêng: cột trái là ảnh PNG, tên và phe; cột phải là mô tả/hướng dẫn. **Không giới hạn thời gian**, không đếm ngược và không tự đóng. Nút **OK / ĐÃ ĐỌC** xác nhận rồi trở về lobby. Có thể mở lại thẻ trước khi bắt đầu trận mà không random lại role.
4. Lobby hiển thị bảng **AVATAR | TÊN NGƯỜI CHƠI | TRẠNG THÁI**, cuộn bằng chuột hoặc kéo thanh cuộn để xem đủ 25 người. Avatar lấy từ ngoại hình từng người đã chọn, không lấy từ role bí mật. Trạng thái là **READY / NOT READY**.
5. Người chơi bấm Ready sau khi xem thẻ và bấm OK. Không có tự xác nhận đọc role, tự Ready hoặc tự bắt đầu gameplay theo thời gian.
6. Khi tất cả Ready, chủ phòng bấm **BẮT ĐẦU GAME**. Lobby ẩn hoàn toàn; HUD gameplay và kỹ năng hiện ra. Những role đã công bố riêng được giữ nguyên, không random lại.

Nút sẵn sàng ghi **CHỜ PHÂN VAI** trước khi chủ phòng phân vai, **ĐANG NHẬN ROLE…** khi chưa nhận dữ liệu riêng và **XEM PHÂN VAI TRƯỚC** khi chưa xác nhận thẻ. Sau OK, Ready được mở ngay, không chờ cờ xác nhận đọc role từ server. Server nhận Ready cũng ghi nhận xác nhận role và trả roster mới; không tự bắt đầu game. **BẮT ĐẦU GAME** là nút riêng của chủ phòng sau khi đã phân vai. Các Canvas gameplay / kỹ năng bị ẩn đồng thời tắt nhận chuột.

Thực hiện trên scene `OnlineSceneGamePlay` đang có, dùng màu giấy/gỗ và portrait của prototype. Canvas lobby được tạo ở runtime bởi `ThuongOnlineLobbyView.cs`, không thay scene hoặc prefab gốc. Form host/join tiếp tục dùng thông tin LAN/Relay hiện tại.

Online không dùng bộ đếm giới thiệu. GameRoleManager được giữ tắt trong bước đọc role/lobby Ready, rồi mới bật và bắt đầu ngày đầu khi chủ phòng xác nhận. Countdown role của prototype offline không đổi. Nếu có người rời phòng sau khi chốt role, phiên kết thúc để tạo lại danh sách và bộ role hợp lệ.

`ThuongOnlineChecks.Run` đã được điều chỉnh cho một host cùng 24 client LAN: 25 avatar/tên/trạng thái, thanh cuộn, nhận role riêng, mở thẻ không có countdown, OK về lobby, chờ Ready, host bắt đầu, giữ nguyên role, HUD, voting và kết quả. Chưa chạy lại bộ kiểm tra sau thay đổi không giới hạn thời gian; log/ảnh cũ trong `Logs/OnlineUIForm` là của luồng 25 giây trước đó.
