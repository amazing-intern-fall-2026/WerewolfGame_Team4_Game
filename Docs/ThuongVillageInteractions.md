# Tương tác làng trong Thương Online

Các tính năng này chạy trong phiên Thương Online; host xác thực vị trí, phe, giai đoạn và vật phẩm trước khi ghi nhận thao tác. Người chơi cần dùng cùng phiên bản có tiền tố kết nối `THUONG13|`.

## Thiết lập trước trận

Chủ phòng mở **THIẾT LẬP VÁN CHƠI → LUẬT LÀNG / VẬT PHẨM**, chỉnh thông số, chọn **QUAY LẠI → ÁP DỤNG** để lưu cho cả phòng. Mỗi lần áp dụng sẽ hủy trạng thái Ready cũ. Thiết lập khóa sau khi phân vai; được giữ khi chơi lại trong cùng phòng. Các con số ở các mục dưới đây là giá trị mặc định.

| Thông số | Mặc định | Phạm vi |
| --- | --- | --- |
| Số cuộc họp khẩn của phòng | 3 | 0–6; 0 tắt cả chuông và còi |
| Chờ giữa hai cuộc họp | 45 giây | 15–120 giây |
| Hồi phá hoại | 45 giây | 15–120 giây |
| Thời gian mất điện | 20 giây | 5–45 giây |
| Thời gian hỏng trạm | 20 giây | 5–45 giây |
| Thời gian chặn đường | 8 giây | 3–15 giây |
| Hiệu lực đèn lồng | 20 giây | 5–60 giây |
| Hiệu lực bùa bảo vệ | 30 giây | 5–60 giây |

Host giữ một bản sao luật cho ván; client không có quyền sửa. Gói thiết lập chứa đủ tám thông số và bị từ chối nếu có giá trị ngoài phạm vi. Giới hạn một cuộc họp mỗi người, một lần phá hoại mỗi ngày, túi hai ô và giữ thao tác ba giây vẫn cố định.

## Họp khẩn cấp

- Mở **LÀNG / TÚI ĐỒ**, đi tới dấu **Chuông làng** trên quảng trường và chọn **GỌI CHUÔNG**.
- Mỗi người chỉ gọi được một lần trong ván; cả phòng có tối đa ba cuộc họp và phải chờ 45 giây giữa hai cuộc.
- Còi báo động nhận từ nhiệm vụ cho phép gọi từ xa. Dùng còi cũng tính vào lượt họp của người chơi và giới hạn chung của phòng.
- Không thể gọi họp khi đang có phá hoại chưa được sửa hoặc khi không ở ban ngày.

## Sói phá hoại

Người thuộc phe Ma Sói còn sống mở **LÀNG / TÚI ĐỒ**, chọn kiểu và vị trí, rồi bấm **PHÁ HOẠI** vào ban ngày. Mỗi ngày bầy Sói có một lượt; chỉ có một phá hoại hoạt động tại một thời điểm. Có 45 giây hồi giữa các lần dùng.

| Kiểu | Tác động | Tự kết thúc |
| --- | --- | --- |
| Mất điện | Làm tối một khu; người ngoài phe Sói chỉ thấy người trong khu ở gần. Đèn lồng mở rộng tầm nhìn. | 20 giây |
| Chặn đường | Tạo vật cản tạm trên một lối nối bản đồ. | 8 giây |
| Hỏng trạm | Chặn bắt đầu minigame tại trạm bị chọn. | 20 giây |

Dân Làng tới dấu sửa màu đỏ và đứng yên ba giây để khắc phục. Di chuyển, chết hoặc mất kết nối sẽ hủy thao tác sửa. Phá hoại cũng tự kết thúc khi hết giờ hoặc chuyển sang giai đoạn khác.

## Nhiệm vụ đôi

Hai người chơi còn sống tới hai bảng A và B trên quảng trường. Khi cả hai đứng đúng vị trí, thao tác được giữ trong ba giây; rời vị trí hoặc mất kết nối sẽ hủy tiến độ. Hoàn thành nhận thêm 5% tiến độ chung, tối đa một lần mỗi ngày.

## Vật phẩm dùng một lần

Hoàn thành minigame có thể nhận vật phẩm nếu túi còn chỗ. Túi chứa tối đa hai món; mỗi người nhận tối đa hai phần thưởng mỗi ngày.

| Vật phẩm | Cách dùng |
| --- | --- |
| Đèn lồng | Dùng khi đang ở khu mất điện; tăng tầm nhìn trong 20 giây. |
| Còi báo động | Gọi họp từ xa, theo cùng giới hạn họp khẩn. |
| Bùa bảo vệ | Chặn một đòn từ Sói, Sát Nhân hoặc kỹ năng trong 30 giây; không chặn phiếu loại. |

## Nhật ký sau trận

Sau khi trận kết thúc, màn hình kết quả có nút **DIỄN BIẾN VÁN**. Nhật ký xếp theo thứ tự sự kiện, gồm phân vai, kỹ năng được chấp nhận, phiếu đã chọn, kết quả vote, người bị loại, tương tác làng và thời điểm kết thúc. Nhật ký không được gửi cho client trước khi ván kết thúc; tối đa 512 mốc được lưu trong một ván.

Trong nhật ký, chọn **LƯU TXT** để có bản đọc trực tiếp, **LƯU JSON** để lưu dữ liệu có cấu trúc hoặc **MỞ THƯ MỤC** để xem tệp. Mỗi lần lưu tạo tên mới; tên người chơi không được dùng làm đường dẫn. Tệp lưu ở `Application.persistentDataPath/MatchJournals`. Chỉ xuất khi đã nhận đủ nhật ký và đang ở màn kết quả; tệp gồm vai trò cuối, kết quả từng người, luật làng và các mốc theo thứ tự. Nhật ký bị lược bớt có cờ cảnh báo trong cả hai định dạng.

## Thực hành và thao tác

Mở **HƯỚNG DẪN → THỰC HÀNH LÀNG** tại lobby hoặc trong trận. Bài tập có bốn bước: đi tới chuông rồi gọi họp; kích hoạt bảng A và phối hợp với người mô phỏng tại B; giữ ba giây để sửa mất điện; dùng đèn lồng trong khu mất điện. Dùng WASD/phím mũi tên hoặc nút trên màn hình để di chuyển, E hoặc nút tương tác để thao tác. Có nút thử lại và thoát bất kỳ lúc nào.

Bài tập dùng luật mặc định và trạng thái riêng trên máy, không gửi thao tác gameplay hay cộng tiến độ/vật phẩm vào ván thật. Nếu mở trong trận, ván thật vẫn tiếp tục; bài tập đóng khi người chơi bị loại hoặc trận kết thúc. Khi mở túi đồ hoặc bài tập, phím di chuyển, tương tác E và emote không tác động vào nhân vật trong ván. Nhấn di chuyển sẽ hủy thao tác sửa/nhiệm vụ đôi ngay cả khi vẫn còn trong bán kính.

## Thợ săn và kết nối lại

Trong Thương Online, Thợ săn bị loại do vote hoặc kết quả đêm được mở lượt trả thù riêng dài **20 giây** trước khi đánh giá kết quả hoặc chuyển ngày. Chọn mục tiêu qua thẻ vai trò; đạn có hiệu lực ngay sau khi host xác nhận. Hết giờ thì mất lượt; nếu bắn chết một Thợ săn khác, người đó được dùng quyền trong thời gian còn lại của cùng lượt chờ. Các scene khác tiếp tục dùng luồng hiện có, trừ khi chủ động bật cơ chế này.

Sổ tay giữ cả ghi chú và nội dung đang gõ khi mất kết nối tạm thời, rồi đồng bộ lại cùng mã trận và vé kết nối. Đổi ván, đổi vé phòng hoặc chủ động rời phòng sẽ xóa sổ. Dữ liệu chỉ nằm trong phiên chạy ứng dụng, không được gửi cho người khác hay lưu qua việc tắt game.
