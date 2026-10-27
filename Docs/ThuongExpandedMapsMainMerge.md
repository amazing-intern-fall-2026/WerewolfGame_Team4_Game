# Tích hợp map vào main — 27/10/2026

Nền tích hợp: `origin/main` tại `abad65b`, gộp `origin/thuong` tại `a60323a` và các map mở rộng trong đợt làm việc này.

Catalog có **13 layout và 91 lựa chọn map/event**: sáu map gốc, `village_large` và sáu map có hậu tố `_expanded`. Scene online Thương giữ làng lớn làm mặc định; người chơi vẫn chọn được map/event khác. Build Settings và menu chính hiện tại được giữ nguyên.

Làng lớn chuyển từ mười nhiệm vụ cũ sang sáu task ID/slot của minigame catalog hiện tại, tăng content revision lên **3**. Mạng đường và các tuyến vòng của làng lớn được giữ; vị trí trạm được ánh xạ theo task ID. Installer map gốc giữ các layout bổ sung. UI, pool event và QA lấy số lượng map thực tế.

Kết quả trên bản tích hợp, Unity **6000.6.0f1**, Windows:

- Nội dung/collision/event chung: **PASS 4.051 assertions** trên 13 map, gồm **26** kiểm tra tuyến của làng lớn với sáu trạm.
- Làng Liên Hoàn: **PASS 1.156 assertions**.
- Năm map mở rộng còn lại: **PASS 6.048 assertions**. Các bộ kiểm tra có phần chồng lặp.
- Windows QA và bản chơi thường: **PASS cả hai build**.
- Hai tiến trình độc lập: **host PASS 387 assertions; client PASS 9 assertions; exit code 0/0, không có C# exception, crash hoặc thiếu resource trong log**.

Host/client chạy trên cùng máy qua loopback. Các avatar minh họa trong benchmark không phải 25 client thật; chưa thay thế nghiệm thu Relay/hai máy hoặc cân bằng với người chơi.

Tái kiểm tra bằng entry point `ThuongExpandedMapsInstaller.VerifyIntegrationAndBuild`, sau đó chạy `Tools/run_thuong_map_player_checks.ps1`. Entry point dựng lại riêng làng lớn với schema hiện tại, kiểm tra sáu map expanded và toàn bộ catalog, rồi build cả hai bản Windows.

Log trong worktree tích hợp: `Logs/MainMerge/verify-build-lfs.log`, `Logs/Maps/content-collision-events.txt`, `Logs/Maps/village-large-routes.txt`, `Logs/Maps/independent-process-result.json` và log host/client cùng thư mục. Lần chạy đầu chưa materialize media LFS đã bị loại khỏi kết quả; lượt xác nhận cuối dùng đầy đủ media thật.

Chỉ nội dung map, phần tích hợp map và tài liệu được đưa vào PR. Các thay đổi local khác ngoài phần map được giữ tại checkout gốc. Các thiết lập Unity tự ghi trong worktree được đối chiếu và trả về bản nền trước khi commit.
