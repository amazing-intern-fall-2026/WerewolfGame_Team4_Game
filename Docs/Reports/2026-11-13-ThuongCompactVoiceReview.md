# Cập nhật GitHub, thu gọn mic/loa và rà soát Thương

Ngày: 13/11/2026. Bản được rà soát: main `5ba8770` (15 commit mới hơn `2f0fe87`). Cập nhật bằng fast-forward, không có xung đột. Nhánh làm việc: `codex/thuong-compact-voice-review`.

## Đã chỉnh giao diện

- Chuyển mic/loa từ vùng danh sách người chơi xuống góc dưới trái, cạnh nút Chat và bên dưới các nút chính của lobby/màn chơi.
- Thu gọn cả cụm còn 94 × 36 đơn vị giao diện ở độ phân giải tham chiếu 1280 × 720. Hai nút có khoảng cách và trạng thái chữ riêng; màu xanh khi bật, đỏ khi tắt, xám khi chưa sẵn sàng.
- Dòng giải thích kênh chỉ hiện khi rê chuột lên nút; không giữ một thanh trạng thái dài trên màn hình.
- Hiện CHỜ khi thoại chưa kết nối, thay cho trạng thái loa BẬT gây hiểu nhầm. Chỉ cho thao tác khi kênh thoại và quyền trao đổi đã sẵn sàng.
- Giữ cơ chế xin quyền bật mic chủ động, tách kênh người sống/người bị loại và khóa thoại theo pha. Vị trí cụm nút tuân theo vùng hiển thị an toàn của thiết bị.

## Những điểm nên sửa tiếp

### P2 — Bảo Vệ chưa tự bảo vệ được trong mode tương tác gần

Adapter mới trên main cho phép Bảo Vệ tự chọn bản thân qua hệ thống kỹ năng có cấu hình. Tuy nhiên, mode `RealtimeProximity` đi qua đường xử lý khác: `NearestAlivePlayer()` luôn bỏ qua chính mình và `TryValidateInteraction()` từ chối mục tiêu có cùng ID với người dùng. Vì vậy, cho phép tự bảo vệ mới có tác dụng ở luồng kỹ năng thông thường, chưa áp dụng cho mode này.

Vị trí: `Assets/Scripts/Thuong/Online/ThuongOnlineRealtime.cs`, các hàm `BuildInteractionOptions`, `NearestAlivePlayer`, `TryValidateInteraction`.

Đề xuất: thêm lựa chọn “Bảo vệ bản thân” cho riêng Bảo Vệ trong danh sách tương tác; host xác thực đúng vai trò và quy tắc không bảo vệ cùng một người hai đêm liên tiếp. Tiếp tục cấm Sói/Tiên tri tự chọn mình. Cần kiểm tra cả giao diện client và xử lý host.

### P2 — Không xem lại được ghi chú cũ trong sổ tay

Sổ tay giữ tối đa 50 ghi chú, nhưng `RefreshNotebookHistory()` chỉ dựng 12 mục cuối. Phần lịch sử dùng chữ rút gọn, không có danh sách cuộn hoặc phân trang. Khi có hơn 12 mục, những mục trước đó vẫn còn trong bộ nhớ nhưng người chơi không có cách xem lại; ghi chú dài còn có thể làm các mục cuối bị cắt.

Vị trí: `Assets/Scripts/Thuong/Online/ThuongOnlineNotebook.cs`, `EnsureNotebookUi`, `RefreshNotebookHistory`.

Đề xuất: hiển thị đủ các mục đang lưu trong vùng cuộn, thêm lọc theo ngày hoặc sửa/xóa từng ghi chú. Giữ tính riêng tư hiện tại; không gửi nội dung sổ tay lên host.

### P3 — Nhắc phím biểu cảm chồng lên nút Hướng dẫn

Ảnh màn chơi 1920 × 1080 cho thấy dòng “Giữ [T] · Biểu cảm” nằm ngay trên nút HƯỚNG DẪN ở giữa cạnh dưới. `PlayerEmoteWheelUI` neo dòng này vào giữa đáy màn hình, trong khi `EnsureMatchButtons()` dùng cùng vùng đó cho nút Hướng dẫn. Hai chữ chồng lên nhau dù nút vẫn bấm được.

Vị trí: `Assets/Scripts/Thuong/Emotes/PlayerEmoteWheelUI.cs` dòng tạo `Keyboard hint`; `Assets/Scripts/Thuong/Online/ThuongOnlinePresentation.cs`, `EnsureMatchButtons`.

Đề xuất: đưa nhắc phím T vào phần hướng dẫn bên trái hoặc chỉ hiện khi mở vòng biểu cảm.

Ba điểm trên đã được xử lý trong đợt triển khai tiếp theo theo yêu cầu. Chi tiết nằm ở cuối báo cáo. Hai điểm đầu ban đầu được xác định từ luồng mã; điểm thứ ba được quan sát trực tiếp trong ảnh chạy Unity.

## Chức năng/cải thiện có thể bổ sung

1. **Thiết lập âm thanh:** chọn thiết bị thu/phát, chỉnh âm lượng nghe và kiểm tra mic trong một cửa sổ mở theo nhu cầu. Có thể thêm dấu hiệu ai đang nói trên danh sách người chơi để xác định nguồn âm thanh mà không mở rộng cụm nút thường trực.
2. **Đồng bộ hình ảnh pha thuốc online:** các commit mới cập nhật scene pha thuốc gốc và prefab nguyên liệu/chai thành phẩm. Thương Online vẫn dựng minigame bằng `ThuongMinigameWindow` và prefab trong catalog riêng. Cần một hạng mục tích hợp riêng nếu muốn bản online có cùng hình ảnh, đồng thời giữ host quyết định kết quả.
3. **Trạng thái kết nối rõ hơn:** hiển thị chất lượng mạng, số giây giữ chỗ còn lại và tiến trình thử kết nối lại. Reconnect hiện đã giữ chỗ 60 giây và có thao tác thử lại; phần cần cải thiện là phản hồi dễ hiểu cho người chơi.

Ưu tiên hai lỗi về Bảo Vệ/sổ tay trước khi thêm cơ chế chơi mới.

## Kiểm tra

- Unity 6000.6.0f1 biên dịch thành công.
- **223 kiểm tra** quyền chat/thoại, khóa mic/loa và tài nguyên đạt.
- **64 kiểm tra giao diện** đạt: lobby/màn chơi ở 1280 × 720, 1920 × 1080, 1024 × 768 và 2560 × 1080; cụm mic/loa ở trong màn hình, không đè nút Chat/các nút thao tác khác hoặc các vùng nội dung chính; trạng thái CHỜ và giải thích khi rê chuột hoạt động.
- **15 kiểm tra tích hợp** đạt với host và hai client NGO qua loopback: đồng bộ luật phòng, khóa chỉnh luật khi bắt đầu, bỏ phiếu/Thợ Săn trả thù và gửi nhật ký sau trận.
- Đã xem ảnh lobby, màn chơi và trạng thái rê chuột. Ảnh/log: `Logs/ThuongCompactVoice20261113`, log cuối `unity-ui-final.log`, kết quả bố cục `ui-checks.txt`.

Các fixture độc lập tự chạy trên main vẫn báo lỗi: `NetworkGameOverTests` dừng tại TC48; `ThuongOnlineWinConditionTests` có Case 5/7 không đạt; `ThuongOnlineGuardianTestExecution` có TC05 không đạt. Những lỗi này được giữ trong log và không tính vào 302 kiểm tra đã đạt ở trên. Harness tách thông báo từ các fixture này khỏi kịch bản online, dọn một canvas kết quả do fixture để lại trước khi mở phòng, rồi xác nhận canvas đó không xuất hiện lại trong lobby/màn chơi. Không kết luận toàn bộ test suite đều đạt.

Kiểm tra tự động chạy trên máy cục bộ; nghiệm thu âm thanh Vivox bằng mic/loa thật và kết nối Relay trên hai máy vẫn là phần kiểm tra riêng.

## Triển khai tiếp theo: Bảo Vệ, sổ tay và nhắc phím biểu cảm

- **Bảo Vệ trong mode tương tác gần:** thêm lựa chọn “Bảo vệ bản thân”. Khi có nhiều tương tác, giữ E để chọn; khi chỉ còn lựa chọn tự bảo vệ, nhấn E để dùng. Host xác thực vai trò, trạng thái sống, pha đêm và lượt dùng. Sói/Tiên Tri vẫn không được tự chọn mình. Lượt đã dùng được gửi về đúng client để ẩn tương tác không còn hợp lệ.
- Ghi lại mục tiêu theo từng đêm: cấm bảo vệ cùng người ở hai đêm liên tiếp, kể cả bản thân; được chọn lại sau khi bảo vệ người khác hoặc bỏ qua một đêm. Lịch sử này được giữ trên host qua reconnect và xóa khi reset ván. Thẻ vai giải thích cách tự bảo vệ và quy tắc mỗi đêm một lượt.
- **Sổ tay:** hiển thị đủ tối đa 50 ghi chú trong vùng cuộn, hỗ trợ con lăn và thanh cuộn. Chữ giữ cỡ đọc được, nội dung dài tự xuống dòng. Khi lưu sẽ cuộn về ghi chú mới; mục thứ 51 thay mục cũ nhất để giữ giới hạn 50.
- Tách vị trí ô nhập khỏi hai nút Ghi/Đóng và đặt sổ tay phía trên các điều khiển Làng để nút Ghi nhận được thao tác. Cơ chế ghi chú riêng tư và giữ qua reconnect của cùng ván được giữ nguyên.
- **Nhắc phím biểu cảm:** chuyển vào bảng hướng dẫn bên trái. Vòng chọn biểu cảm vẫn giải thích giữ/thả phím khi mở; cạnh dưới không còn dòng chữ đè lên nút Hướng dẫn. Nội dung hướng dẫn và số người tự cập nhật khi đổi mode hoặc số người ở ván kế tiếp.

Kịch bản kiểm tra bổ sung mở một ván tương tác gần với host và ba client NGO trên cùng máy qua loopback. Các yêu cầu kỹ năng đi qua thông điệp production; kiểm tra tự bảo vệ, chặn đòn Sói, chống dùng hai lần, cấm Sói/Tiên Tri tự chọn mình, cấm bảo vệ lặp ở đêm sau, giữ giới hạn khoảng cách, chọn lại sau một đêm nghỉ và xóa lịch sử khi reset. Một ca họp bổ sung ban ngày xác nhận quy tắc dựa trên số đêm thực tế, không thể bỏ qua hạn chế bằng cách chuyển ngày qua cuộc họp.

Sổ tay được kiểm tra với 50 ghi chú dài, mục thứ 51, cuộn tới đầu/cuối, nội dung chữ nằm trong vùng nhìn thấy và thao tác bấm nút Ghi. Ảnh được kiểm tra ở 1280 × 720, 1920 × 1080, 1024 × 768 và 2560 × 1080. Kết quả/log bổ sung nằm tại `Logs/ThuongFollowup20261113`.

Kết quả cuối: Unity biên dịch thành công; **585 kiểm tra trong phạm vi đã chạy đạt** gồm 53 biểu cảm, 97 hồi quy, 9 cấu hình role, 246 quyền phân vai/bỏ phiếu, 63 tương tác làng, 64 bố cục mic/loa và 53 kiểm tra tích hợp/giao diện của kịch bản hai ván. Log cuối: `Logs/ThuongFollowup20261113/unity-final.log`. Các lỗi fixture độc lập nêu ở phần kiểm tra trước vẫn được ghi riêng, không được coi là toàn bộ suite đã đạt. Không chạy nghiệm thu Relay trên hai máy trong đợt này.
