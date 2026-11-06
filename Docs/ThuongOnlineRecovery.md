# Khôi phục lobby và UI online Thương

Luồng áp dụng cho `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity`. Giao diện tham chiếu là `Assets/Scenes/Thuong/Prototype/ThuongGameplayPrototype.unity`.

## Vào Thương từ menu chính

1. Đăng nhập hoặc chọn **Chơi ngay** ở MainMenu, rồi bấm **THƯƠNG ONLINE**.
2. Chọn Host hoặc Join trong màn hình kết nối Thương. Có thể dùng địa chỉ LAN hoặc mã Relay.
3. Bấm **RỜI PHÒNG** để về màn hình kết nối Thương, sau đó bấm **Về menu chính**.

Mã Thương thêm nút khi MainMenu được tải. Build Settings thêm scene Thương sau bốn scene hiện có. Thương thay NetworkManager lúc chuyển scene khi menu đang rảnh và dọn phiên riêng trước khi về MainMenu.

## Mất kết nối và trở lại ván

- Trước phân vai, host bỏ người rớt mạng khỏi lobby. Sau phân vai, host giữ chỗ trong **60 giây**. Trong ván, đồng hồ vẫn chạy và các lượt hợp lệ đã gửi vẫn được xử lý theo luật của phase. Người rớt mạng vẫn có thể bị loại trong thời gian giữ chỗ.
- Lượt minigame đang mở của người rớt mạng được hủy và giải phóng chỗ. Tiến độ đã ghi nhận, lượt kỹ năng đã dùng, phiếu và trạng thái nhân vật vẫn thuộc về đúng ID người chơi trên host.
- Trên máy vừa rớt mạng, bấm **Kết nối lại**. LAN dùng lại địa chỉ và cổng, Relay dùng lại mã phòng. Host kiểm tra token giữ trong phiên Thương trên máy người chơi, rồi gắn kết nối mới vào ID cũ và gửi lại dữ liệu công khai, role riêng, luật phòng và tiến độ. Token giữ trong bộ nhớ nên phải thử lại từ cùng phiên game đang mở.
- Nếu hết hạn ở màn phân vai, host mở lại lobby để những người còn lại phân vai lại. Nếu hết hạn trong ván, người đó bị loại với lý do mất kết nối và luật thắng/thua chạy tiếp. Nếu host đóng hoặc mất kết nối, các client mất phiên do host quản lý.
- Nút **Rời phòng** gửi yêu cầu từ bỏ chỗ nếu còn kết nối. Khi đường truyền đã mất, host xử lý theo hạn giữ chỗ. Chơi lại sau khi ván kết thúc sẽ bỏ các chỗ đã mất kết nối.

Host và client cần cùng bản `THUONG10|`. Roster `thuong.online.roster.v4` có cờ mất kết nối. Snapshot `thuong.online.snapshot.v8` đồng bộ mode và xác; gói trạng thái riêng `thuong.online.private.v4` không đổi. Token chỉ được gửi trong handshake và không nằm trong roster hay snapshot.

## Luồng lobby

- Chủ phòng chọn sức chứa 2–25 bằng nút − / +, có thể đổi trước trận nhưng không giảm dưới số người đang kết nối. Danh sách phòng chờ cuộn để xem đủ người.
- Chủ phòng bấm **PHÂN VAI** để chốt danh sách. Nút đổi thành **XEM PHÂN VAI** để từng người tự mở thẻ không giới hạn thời gian, bấm OK rồi Ready. Chủ phòng có nút **BẮT ĐẦU GAME** riêng khi tất cả sẵn sàng. Trận dùng đúng danh sách kết nối đã chốt, không tạo người chơi mẫu để lấp đầy phòng.
- `RoleManager.AssignRole` và `RolePoolBuilder.Build` chọn role theo số người thực tế từ các lớp role cụ thể trong hệ thống. Ngoại hình được chọn độc lập với role bí mật.
- Mỗi máy nhận riêng role của người chơi đó. Thẻ role dùng ảnh, tên, phe và hướng dẫn chi tiết của prototype, kèm mô tả từ catalog nếu có. Thẻ không tự mở hoặc tự đóng theo thời gian; OK đưa về lobby, có thể mở lại trước trận. Gameplay chỉ chạy sau khi mọi người xác nhận thẻ, Ready và chủ phòng bấm bắt đầu.
- Canvas `GameHUD` của scene được giữ hoạt động; chỉ ẩn việc hiển thị ở phòng chờ. Khi vào trận, HUD đọc snapshot mạng. Cửa sổ `THƯƠNG ONLINE` không được vẽ trong trận. UI kỹ năng và nút vote gửi yêu cầu về host.
- Bảng vote dùng lại template, bố cục và danh sách cuộn của prototype; tên/ID/trạng thái được lấy từ roster thật. Host kiểm tra quyền bỏ phiếu, trạng thái sống, giai đoạn và chống vote lần hai. Luật bỏ phiếu cho bản thân của prototype được giữ.
- Kết quả vote gồm thông báo, số phiếu có trọng số, hòa phiếu và người bị loại. Kết quả hiện trong 10 giây và có thể mở lại bằng **Xem kết quả vote**; vẫn được lưu nếu gameplay đã chuyển sang ngày sau hoặc kết thúc ván.
- Snapshot không đặt lại phiên bản khi bắt đầu trận. Host không nhận lại snapshot của chính mình. Roster và snapshot dùng truyền tin có phân mảnh để chứa 25 người và tên UTF-8.
- Chi tiết bố cục có trong `Docs/ThuongOnlineUIForm.md`. Luật và bảng kết quả có trong `Docs/ThuongOnlineEndgame.md`.

## Kiểm tra

`ThuongOnlineChecks.Run` chạy trong Unity batchmode: kiểm tra bộ chia role cho 1–25 người, regression gameplay offline, 1 host + 24 transport LAN, thẻ role/HUD gốc, danh sách vote 25 dòng, quyền host bắt đầu, xác nhận role riêng, phiếu trùng, trọng số phiếu, hòa/không có phiếu, đồng bộ kết quả và tạo phòng lại.

Log và ảnh kết quả được ghi vào `Logs/OnlineRecovery` trong dự án đang chạy bài kiểm tra. Chạy trong bản dự án riêng khi dự án chính đang được Unity mở.

`ThuongRoomUpgradeOnlineChecks.Run` có thêm kịch bản rớt và nối lại trong phòng đầy, giữ tiến độ cá nhân, tiếp tục minigame của người khác, từ chối token sai và mở lại lobby khi hết hạn ở màn phân vai. Ca kiểm tra thủ công gồm chuyển MainMenu → Thương → MainMenu rồi vào luồng Lợi, thử mất mạng và hết hạn trong ván qua LAN, sau đó lặp lại reconnect Relay trên hai máy.

## Dấu vết phiên bản trước

`Logs/OnlineUIFix/verified-ui-result.txt` còn ghi nhận lần kiểm tra các chức năng tương tự. Mã hiện tại trước khi khôi phục chỉ có lobby tối đa 12, tắt toàn bộ GameHUD và dùng cửa sổ OnGUI riêng khi chơi. Không thấy commit chứa mã UI tương ứng trong lịch sử file online hiện có. Thay đổi khôi phục nằm trong mã nguồn dự án, kèm bài kiểm tra Editor để có thể kiểm tra lại sau khi hợp nhất nhánh.
