# Khôi phục lobby và UI online Thương

Luồng áp dụng cho `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity`. Giao diện tham chiếu là `Assets/Scenes/Thuong/Prototype/ThuongGameplayPrototype.unity`.

- Chủ phòng chọn sức chứa 2–25 bằng nút − / +, có thể đổi trước trận nhưng không giảm dưới số người đang kết nối. Danh sách phòng chờ cuộn để xem đủ người.
- Chủ phòng bấm **PHÂN VAI** để chốt danh sách. Nút đổi thành **XEM PHÂN VAI** để từng người tự mở thẻ không giới hạn thời gian, bấm OK rồi Ready. Chủ phòng có nút **BẮT ĐẦU GAME** riêng khi tất cả sẵn sàng. Trận dùng đúng danh sách kết nối đã chốt, không tạo người chơi mẫu để lấp đầy phòng.
- `RoleManager.AssignRole` và `RolePoolBuilder.Build` chọn role theo số người thực tế từ các lớp role cụ thể trong hệ thống. Ngoại hình được chọn độc lập với role bí mật.
- Mỗi máy nhận riêng role của người chơi đó. Thẻ role dùng ảnh, tên, phe và hướng dẫn chi tiết của prototype, kèm mô tả từ catalog nếu có. Thẻ không tự mở hoặc tự đóng theo thời gian; OK đưa về lobby, có thể mở lại trước trận. Gameplay chỉ chạy sau khi mọi người xác nhận thẻ, Ready và chủ phòng bấm bắt đầu.
- Canvas `GameHUD` của scene được giữ hoạt động; chỉ ẩn việc hiển thị ở phòng chờ. Khi vào trận, HUD đọc snapshot mạng. Cửa sổ `THƯƠNG ONLINE` không được vẽ trong trận. UI kỹ năng và nút vote gửi yêu cầu về host.
- Bảng vote dùng lại template, bố cục và danh sách cuộn của prototype; tên/ID/trạng thái được lấy từ roster thật. Host kiểm tra quyền bỏ phiếu, trạng thái sống, giai đoạn và chống vote lần hai. Luật bỏ phiếu cho bản thân của prototype được giữ.
- Kết quả vote gồm thông báo, số phiếu có trọng số, hòa phiếu và người bị loại. Kết quả hiện trong 10 giây và có thể mở lại bằng **Xem kết quả vote**; vẫn được lưu nếu gameplay đã chuyển sang ngày sau hoặc kết thúc ván.
- Snapshot không đặt lại phiên bản khi bắt đầu trận. Host không nhận lại snapshot của chính mình. Roster và snapshot dùng truyền tin có phân mảnh để chứa 25 người và tên UTF-8.
- Rời phòng/mất kết nối dọn role, nhiệm vụ, timer, avatar và dữ liệu UI để tạo phiên mới.

Snapshot dùng phiên bản v4 để đồng bộ trạng thái đã phân vai / đã vào game, cấu hình luật và kết quả thắng / thua sau GameOver; roster/private/action tiếp tục dùng v2. Host và client phải chạy mã/build mới cùng phiên bản. Chi tiết bố cục mới: `Docs/ThuongOnlineUIForm.md`; luật và bảng kết quả: `Docs/ThuongOnlineEndgame.md`.

## Kiểm tra

`ThuongOnlineChecks.Run` chạy trong Unity batchmode: kiểm tra bộ chia role cho 1–25 người, regression gameplay offline, 1 host + 24 transport LAN, thẻ role/HUD gốc, danh sách vote 25 dòng, quyền host bắt đầu, xác nhận role riêng, phiếu trùng, trọng số phiếu, hòa/không có phiếu, đồng bộ kết quả và tạo phòng lại.

Log và ảnh kết quả được ghi vào `Logs/OnlineRecovery` trong dự án đang chạy bài kiểm tra. Chạy trong bản dự án riêng khi dự án chính đang được Unity mở.

## Dấu vết phiên bản trước

`Logs/OnlineUIFix/verified-ui-result.txt` còn ghi nhận lần kiểm tra các chức năng tương tự. Mã hiện tại trước khi khôi phục chỉ có lobby tối đa 12, tắt toàn bộ GameHUD và dùng cửa sổ OnGUI riêng khi chơi. Không thấy commit chứa mã UI tương ứng trong lịch sử file online hiện có. Thay đổi khôi phục nằm trong mã nguồn dự án, kèm bài kiểm tra Editor để có thể kiểm tra lại sau khi hợp nhất nhánh.
