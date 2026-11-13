# Luồng chơi chung, tài nguyên và chat/voice Thương

Ngày triển khai: 05/11/2026. Bắt đầu từ main `4fd7309`.

## Cách sử dụng

1. Mở MainMenu, đăng nhập/Chơi ngay và tạo hoặc vào phòng bằng giao diện chính.
2. Chủ phòng bấm **TIẾP TỤC / CHỌN MAP** khi có 2–25 người. Cả phòng chuyển sang phần chuẩn bị Thương bằng kết nối đang dùng.
3. Chọn map/event và luật phòng, phân vai, đọc thẻ vai, Ready rồi bắt đầu như trước. Không Host/Join thêm lần nữa.
4. Dùng nút Chat ở góc dưới bên trái. Nút mic/loa nằm ở góc trên bên phải, kèm trạng thái quyền trao đổi.
5. Khi kết thúc, chủ phòng bấm **VỀ LOBBY** để đưa cả phòng về lobby chính và chơi ván tiếp theo. Tên và mã phòng được giữ.
6. **RỜI PHÒNG** dọn kênh thoại và phiên phòng, đóng kết nối rồi về menu. Client giữ bảng kết quả nếu host rời sau khi đã có kết quả.

Nút Thương Online riêng trong menu được thay bằng đường đi qua lobby chung. Mở trực tiếp scene online vẫn dùng được chế độ Host/Join riêng phục vụ phát triển. Có thể tắt `LobbyManager.useThuongGameplay` để đối chiếu luồng Game cũ trong quá trình phát triển.

## Quyền trao đổi mặc định

| Trạng thái | Chat và voice |
| --- | --- |
| Phòng chờ, chọn map, đọc vai/Ready | Kênh chung của phòng |
| Người sống trong ban ngày, thảo luận, bỏ phiếu | Kênh người còn sống |
| Người sống trong ban đêm/các bước xử lý kết quả pha | Tạm ngưng gửi chat và tắt nghe/nói |
| Người đã bị loại trong ván | Kênh người bị loại, tách khỏi người sống |
| Bảng kết quả cuối ván | Cả phòng trao đổi chung |
| Mất kết nối hoặc rời scene | Dừng trao đổi, khóa mic và dọn kênh |

Micro không tự bật khi vào kênh, đổi pha, kết nối lại hoặc quay về lobby. Người chơi cần chủ động bật khi được phép. Khi đang gõ chat, các phím di chuyển, E, T và Tab không tác động tới nhân vật/bản đồ/biểu cảm.

## Phần đã thay đổi

- Khôi phục pipeline URP và renderer 2D đúng GUID mà các mức chất lượng đang tham chiếu; gán pipeline mặc định trong Graphics Settings.
- Khôi phục hai ảnh Green/Yellow còn được catalog Potion và nguyên liệu trung tính sử dụng, cùng metadata. Giữ các tham chiếu hiện có.
- Lobby gửi roster trước khi NGO tải scene. Thương tiếp quản NetworkManager, transport, tên và mã phòng; các player object của luồng Game cũ được dọn để không chạy song song với avatar Thương.
- Khi về lobby, khôi phục tên theo roster hiện tại, kể cả client đã đổi ClientId sau reconnect. Không áp lại toàn bộ phần local cũ: minigame gốc và những thay đổi khác vẫn nằm trong bản sao lưu trước đó.
- Giữ chức năng luật phòng, nhiệm vụ hợp tác và reconnect trên main. Vé reconnect được gửi riêng cho đúng người; đường đi từ lobby sử dụng lại transport hiện có để kết nối lại.
- Kiểm tra bản tương thích ngay khi kết nối lobby; prefix hiện tại của Thương là `THUONG11|`. Host và client cần cùng bản build.
- Chat đi qua thông điệp có epoch, phiên bản quyền và số thứ tự. Host tự xác định người gửi, trạng thái sống và pha; chỉ gửi tới những người thuộc đúng kênh. Có giới hạn 120 ký tự, tần suất và lịch sử 60 tin; văn bản không thể chèn định dạng TMP.
- Chặn endpoint chat broadcast cũ khi Thương đang quản lý trận để tránh bỏ qua luật nhận tin.
- Quyền voice dùng cùng phân nhóm; tên kênh ngẫu nhiên được gửi riêng, thay khi phân nhóm/pha thay đổi. Thiết bị bị khóa trước khi chuyển kênh; thao tác join/leave được xếp lần lượt và bỏ các yêu cầu cũ.
- Voice giữ lựa chọn loa của người dùng; chuyển quyền không tự bật lại micro. Khi dọn scene, quyền được trả lại sau khi rời kênh.

Phần này sửa tài nguyên hiển thị cho Potion hiện có. Việc chuyển toàn bộ giao diện pha thuốc mới với nguyên liệu rơi/chai thành phẩm vào online là hạng mục riêng trong kế hoạch pha thuốc.

## Kiểm tra

- `Tools → Thuong → Communications → Check Rules Voice And Assets`: kiểm tra toàn bộ tổ hợp trạng thái/pha, khóa mic/loa, pipeline/renderer và hai ảnh đang được tham chiếu.
- `Tools → Thuong → Main Flow → Build Multiplayer Verification`: tạo bản Windows Development từ Build Settings chính, thêm harness chỉ trong define `THUONG_MAIN_FLOW_QA`.
- Entry `ThuongMainFlowChecks.VerifyAndBuild` còn chạy kiểm tra emote, minigame và luật phòng/route trước khi build.

Kịch bản ba tiến trình dùng các tham số:

```text
-batchmode -nographics -communication-qa -flow-slot 0|1|2 -flow-port 18589 -flow-output <đường dẫn tuyệt đối tới kết quả> -logFile <đường dẫn log>
```

Slot 0 là host, slot 1 và 2 là client. Cùng dùng một cổng. Bỏ `-nographics`, thêm `-force-d3d11 -screen-width 1280 -screen-height 720 -flow-visual 1` cho slot 0 để xuất ảnh lobby và chat ở hai độ phân giải.

Kịch bản kiểm tra ba người đi từ MainMenu qua lobby thật, tên Unicode, chọn map, đọc vai/Ready, truyền chat ba chiều, chống chèn định dạng, chặn gửi ban đêm, hai người bị loại trao đổi riêng và người sống không nhận tin đó. Slot 2 ngắt/nối lại trong ván đầu, giữ ID người chơi và kết nối gốc. Cuối ván quay về lobby, chơi ván thứ hai rồi kiểm tra host rời và client giữ kết quả.

Harness dừng đồng hồ tại các pha cần kiểm tra quyền, đặt trạng thái sống/chết và tiến độ nhiệm vụ để gọi đường xét thắng production. Đây là kiểm tra tích hợp có kiểm soát, không thay thế một trận chơi đầy đủ. Bộ kiểm tra giới hạn tốc độ 30 khung hình/giây để giảm tải máy.

Log và ảnh của lần triển khai nằm trong `Logs/ThuongIntegration20261105`. Bản QA ở `Logs/MainFlow/Player`.

## Giới hạn nghiệm thu

- Các kiểm tra tự động dùng LAN loopback, không thay thế Relay trên hai máy thật.
- Kiểm tra voice tự động xác nhận luật, khóa thiết bị và vòng đời chuyển kênh. Âm thanh Vivox thực tế cần hai tài khoản đã đăng nhập và thiết bị thu/phát để nghiệm thu riêng.
- Bộ kiểm tra dùng chung `NetworkGameOverTests` tự chạy khi Unity khởi tạo còn dừng ở TC48 với lỗi trong `GameRoleManager.FinishVoting`; không được tính là một bộ kiểm tra đạt trong đợt này.

## Kết quả ngày 05/11/2026

- Build Windows Development thành công. Kiểm tra emote đạt 53, minigame đạt 367, luật phòng/route đạt 1.401 và quyền trao đổi/tài nguyên đạt 207.
- Kịch bản ba người qua hai ván đạt **84 kiểm tra**: host 45, client thứ nhất 18, client có reconnect 21. Log cuối nằm trong `Logs/ThuongIntegration20261105/Verified/slot-0-result.txt`, `slot-1-result.txt` và `slot-2-result.txt`.
- Đã kiểm tra ảnh lobby và chat ở 1280×720, 1920×1080. Ảnh cuối nằm trong `Logs/ThuongIntegration20261105/Verified/lobby-1.png`, `lobby-2.png`, `chat-1280x720.png` và `chat-1920x1080.png`.
- Log build và các bài kiểm tra nằm trong `Logs/ThuongIntegration20261105/final-build.log`; bản build sau khi chỉnh vị trí thanh mic/loa nằm trong `visual-build.log`.
