# Kiểm tra tích hợp chức năng làng vào main — 11/11/2026

- Nhánh chức năng: `codex/thuong-village-interactions`.
- Nền đã đối chiếu với GitHub: `553ba3f262c191b59ca10e90462e05cfaa4875ea`.
- Phạm vi: họp khẩn, ba kiểu phá hoại, nhiệm vụ hai người, vật phẩm dùng một lần và nhật ký công bố sau trận. Hướng dẫn tại `Docs/ThuongVillageInteractions.md`.
- Phòng chờ chính và kết nối trực tiếp/reconnect dùng cùng tiền tố `THUONG12|`. Bản `THUONG11|lobby` bị từ chối trước khi tải gameplay; snapshot mới có cờ ẩn vị trí theo người nhận.

## Kết quả trước khi đưa lên main

Unity **6000.6.0f1** đã biên dịch lại mã runtime và Editor từ thư mục Assets thực tế. Tiến trình batch thoát mã **0**, không có lỗi biên dịch C#.

| Nhóm | Kết quả |
| --- | --- |
| Luật làng và tương thích phiên bản lobby | PASS 63 kiểm tra |
| Phân vai chính xác, ngẫu nhiên, quyền vote và đổi phiếu | PASS 246 kiểm tra |
| Cấu hình vai trong lobby | PASS 9 kiểm tra |

Kiểm tra làng bao gồm giới hạn lượt họp, thời gian chờ, khoảng cách, còi gọi từ xa, quyền/giai đoạn phá hoại, hết hạn và sửa phá hoại, hai người khác nhau tại hai bảng, hủy tiến độ khi mất người, chống nhận thưởng nhiệm vụ đôi nhiều lần trong ngày, sức chứa túi, giới hạn thưởng, hiệu lực và tiêu thụ bùa, cùng việc từ chối lobby phiên bản cũ.

Entry point chạy thủ công: `ThuongVillageMergeChecks.Run`. Kiểm tra không tự chạy khi Editor tải và không dựng player. Log cục bộ tại `Logs/VillageMerge20261111/unity-checks.log`; danh sách kiểm tra làng tại `Logs/VillageMerge20261111/village-results.txt`.

Fixture cũ `NetworkGameOverTests.RunAllTests` vẫn báo `NullReferenceException` khi tự chạy lúc Editor tải, giống lần kiểm tra main trước đó. Lỗi này không được tính vào các kiểm tra đạt ở bảng trên. Chưa kiểm tra đủ năm chức năng bằng nhiều client, Relay Internet hoặc nghiệm thu hình ảnh/UI; lần này xác nhận biên dịch và các luật nêu trên.

Ba tệp font/cấu hình do Unity tự ghi khi thoát đã được sao lưu dưới `Logs/VillageMerge20261111/Generated` rồi khôi phục về nội dung trước khi kiểm tra. Mã chức năng và tài liệu được giữ nguyên. Các thay đổi ban đầu cũng được sao lưu kèm SHA-256 trước khi tích hợp.
