# Hoàn thiện Thương Online sau review — 12/11/2026

Nền triển khai: `main` tại `9fd7f46`. Nhánh làm việc: `codex/thuong-review-improvements`.

## Sửa lỗi

- Thợ săn có phase trả thù riêng dài 20 giây sau khi bị loại do vote hoặc giải quyết đêm. Giữ nguyên ngày cho tới khi bắn hoặc hết giờ; đạn xử lý ngay, hỗ trợ chuỗi Thợ săn và chỉ tiếp tục phase một lần. Bộ đánh giá kết quả cũng chờ phase này hoàn tất để phát bắn cuối được ghi vào nhật ký trước khi công bố. Cơ chế chỉ được bật bởi phiên Thương Online.
- Tra ảnh vai trò tách đường đọc ảnh đã gán khỏi đường tìm ảnh dự phòng, loại bỏ vòng gọi lặp khi thiếu ảnh.
- Sổ tay giữ nội dung đã lưu và bản nháp khi mất kết nối tạm thời; chỉ xóa khi khác mã ván/vé phòng hoặc chủ động rời phòng.
- Túi đồ chặn di chuyển, tương tác E và emote; thao tác di chuyển cũng hủy giữ bảng/sửa phá hoại ngay. Modal mới nằm trên thanh chat/thoại.
- Mô tả vai trò dùng luật online hiện hành cho Sát nhân, Cáo tinh, Bạch Khuyển và Thợ săn; mode tương tác gần hướng dẫn dùng E. Giao diện cập nhật khi luật mô tả thay đổi và tự mở quyền trả thù mới trong chuỗi Thợ săn.

## Bổ sung

- Tám thông số làng trong lobby: số cuộc họp, thời gian chờ họp, hồi phá hoại, ba thời gian phá hoại và hai thời gian vật phẩm. Có mặc định, giới hạn hợp lệ, bản nháp độc lập và đồng bộ tới client. Khóa khi phân vai; giữ khi chơi lại.
- Bài thực hành làng có di chuyển, tương tác và trạng thái mô phỏng riêng: chuông, hai bảng, sửa phá hoại và dùng đèn. Không gửi thao tác gameplay hoặc cộng thưởng vào ván thật.
- Xuất nhật ký TXT/JSON chỉ sau kết quả và khi nhận đủ dữ liệu; hỗ trợ tiếng Việt, tên chứa dấu nháy, cờ lược bớt và tên tệp không trùng. Có nút mở thư mục lưu.
- Tiền tố kết nối `THUONG13|`, room state `v3`, journal `v2`. Từ chối bản cũ ngay tại lobby. Journal truyền thêm cờ lược bớt.
- Hai bộ chạy kiểm tra UI/nền của Lợi không tự mở scene khi batch hoặc chuyển vào Play Mode; vẫn chạy được thủ công qua menu.

Hướng dẫn thao tác và phạm vi thông số: [ThuongVillageInteractions.md](../ThuongVillageInteractions.md).

## Cách kiểm tra

Unity **6000.6.0f1** biên dịch thành công, không có lỗi C#. Lượt kiểm tra hồi quy cuối thoát mã 0; smoke online báo PASS. Tổng **430 kiểm tra đạt**:

| Nhóm | Đạt |
| --- | ---: |
| Hồi quy cải thiện, gồm nhật ký ba phần đến sai thứ tự | 97 |
| Cấu hình phân vai | 9 |
| Phân vai và quyền thao tác | 246 |
| Luật làng và phiên bản lobby | 63 |
| Host + hai client, luồng luật/Thợ săn/nhật ký/UI | 15 |

Log xác nhận: `Logs/ThuongReviewImprovements20261112/unity-final.log`, `regressions.txt`, `online-smoke-4.log` và `online-smoke.txt`. Các lượt thử chưa đạt trước đó được giữ riêng; không dùng làm kết quả xác nhận.

- `ThuongReviewImprovementChecks.Run`: hồi quy Thợ săn, ảnh thiếu, reconnect/sổ tay, bàn phím/modal, mô tả vai, luật/wire, thực hành, tệp xuất và ghép nhật ký; sau đó chạy lại các kiểm tra phân vai/quyền/luật làng.
- `ThuongRoomUpgradeOnlineChecks.RunReviewSmoke`: host và hai client NGO qua loopback trong cùng tiến trình Unity Editor; dùng bộ đọc production và gửi phát bắn bằng đường gửi action thực tế. Kiểm tra thiết lập phòng, quyền sửa, khóa luật, luyện tập không sửa tiến độ, chờ trả thù trước parity, công bố nhật ký và nút xuất.
- Ảnh giao diện tại `Logs/RoomUpgrades/review-*.png`, hai kích thước 1280×720 và 1920×1080: luật làng, thực hành, dùng vật phẩm và nhật ký/xuất.

Chưa nghiệm thu Relay Internet/hai máy hoặc player build mới. Các kiểm tra GameOver cũ có ca thất bại và lỗi fixture TC48 như bản nền; không cộng vào số kiểm tra đạt của đợt này.
