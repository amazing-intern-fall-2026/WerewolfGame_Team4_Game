# Tích hợp main và kiểm chứng trước khi đẩy GitHub — 11/11/2026

Nhánh đích: `main`. Nhánh công việc: `codex/thuong-mainflow-communications`.

## Nội dung tích hợp

- Lưu toàn bộ phần luồng chơi chung, chat/voice, tài nguyên bị thiếu và các bản sửa phân vai, vote, kết thúc phòng, reconnect vào commit `6b1b966`.
- Tích hợp main `2a68085` (PR #87: sửa cấu hình và đồng bộ vai trò). Xử lý xung đột tại `ThuongOnlineRoleConfigAdapter.cs`, giữ cơ chế phân vai một lần do session quản lý và bố cục lobby hai hàng của main.
- Trong lúc kiểm chứng, GitHub có thêm main `eec17bf` (PR #88: cập nhật minigame pha thuốc). Đã tích hợp bản này và chạy lại build cùng kiểm tra online trên nội dung cuối.
- Giữ các cập nhật scene, font, input actions và minigame của main; không thay thế toàn bộ tệp xung đột bằng phiên bản cũ.
- Giữ đầy đủ metadata, pipeline/renderer 2D và hai ảnh Potion mà catalog hiện có đang tham chiếu. Ảnh được quản lý qua Git LFS.

## Cách xử lý phần phân vai trùng nhau

Host lưu cơ cấu vai; `ThuongOnlineSession` kiểm tra cấu hình và giao cho `RoleManager` phân ngẫu nhiên người nhận trước khi gửi thẻ vai riêng. Không đăng ký thêm callback để phân lại sau RoleReveal, không tự cân bằng lại cơ cấu host đã chọn.

Adapter giữ việc chỉnh/xem cấu hình, đồng bộ có xác thực và giới hạn vai theo chế độ. Giữ API kiểm tra `DoesCurrentPlayersMatchHostConfig` dưới dạng chỉ đọc. Không tự phục hồi vai mỗi lần `PlayerData.NotifyChanged`, tránh ghi đè các chuyển đổi vai hợp lệ trong trận.

Giữ các sửa lỗi mới từ main cho đường chơi cũ: `NetworkRoleManager` nhường cấu hình lobby, `NetworkRoleActionBridge` đồng bộ `PlayerController.Role`, `NetworkRoleSync` không chạy thay session Thương hoặc báo thành công với danh sách rỗng.

Bộ kiểm tra cấu hình vai từ main được cập nhật để chạy chủ động trong preview scene, tạo đủ người trước khi lưu cấu hình, khôi phục dữ liệu khi kết thúc và trả lỗi thật khi thất bại. Hai tình huống chống ghi đè kiểm tra trực tiếp việc chặn đường phân vai mặc định và giữ nguyên đối tượng role qua các thông báo lặp. Không còn tự reset phòng khi mở Unity.

## Kết quả trên bản tích hợp eec17bf

Unity **6000.6.0f1**, Windows Development build:

| Kiểm tra | Kết quả |
| --- | --- |
| Cấu hình vai, thay đổi cơ cấu, chơi lại, đường phân vai mặc định | 9 đạt |
| Luật phân vai và quyền bỏ phiếu | 246 đạt |
| Biểu cảm | 53 đạt |
| Minigame | 367 đạt |
| Luật phòng, nhiệm vụ, route | 1.401 đạt |
| Quyền chat/voice và tài nguyên | 207 đạt |
| Một host và hai client chạy riêng | 163 đạt: 84 + 38 + 41 |
| Build Windows | Thành công, Unity thoát mã 0 |
| Ba tiến trình kiểm tra online | Tất cả thoát mã 0 |

Kịch bản online kiểm tra hai ván qua MainMenu/lobby chung, cấu hình vai giữ qua chơi lại, ID/vai giữ qua reconnect, phiếu riêng và đổi phiếu, từ chối yêu cầu giả mạo hoặc sai mã ván, giữ bảng kết quả khi host rời. Sau đó tạo phòng mới, xác nhận cấu hình cũ được xóa và thông báo host rời giữa ván vẫn được xử lý đúng.

Log cuối tại `Logs/MergeMain20261111/final-main-build.log`, `role-config-results.txt` và `Final/slot-0-result.txt` đến `slot-2-result.txt`. Log nằm trong thư mục được Git bỏ qua, không đưa bản build hoặc cache lên kho mã nguồn.

## Sao lưu và giới hạn

Đã sao lưu 69 tệp đang thay đổi, patch và thông tin nhánh trước khi commit tại `Logs/MergeMain20261111/Before`, `before.patch`, `before-manifest.json` và `before-refs.txt`. Các tệp Unity tự sinh sau kiểm tra được lưu riêng rồi trả về nội dung đã tích hợp.

Kiểm tra online dùng ba tiến trình trên cùng máy qua loopback, có điều khiển pha và tiến độ để kiểm tra các nhánh. Chưa nghiệm thu Relay qua Internet, âm thanh Vivox thực tế hoặc toàn bộ giao diện minigame pha thuốc mới. Bộ GameOver cũ tự chạy lúc khởi tạo Editor vẫn có lỗi fixture TC48; bộ này không được tính vào các kết quả đạt ở trên.

Các máy chơi cùng nhau cần dùng cùng bản build với giao thức `THUONG11`.
