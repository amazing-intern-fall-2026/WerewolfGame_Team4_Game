# Báo cáo triển khai map và event — 21/10/2026

Nhánh local: `codex/thuong-map-events`, nền `7ab0950`.
Plan đầu vào: `ThuongMapAndEventExpansionPlan.md` trong Downloads; nội dung
trùng với bản đang có trong Docs. Phạm vi là triển khai game tại local.

## Đã triển khai

| Mục trong plan | Kết quả |
| --- | --- |
| MAP-01 | Cắt sprite mộ, đèn, mái quầy nhiều màu, rèm, lều, lửa trại, sàn; thêm biển mặt nạ tuồng; giữ notice nguồn và credit trong build. |
| MAP-02 | Map definition, registry Resources, runtime bind/cleanup và validator. Làng hiện có đóng gói thành prefab, có 25 spawn và 10 slot nhiệm vụ. |
| MAP-03 | Swept collision tại server, xử lý trượt dọc vật cản, chống xuyên khi bước lớn; bounds và footprint từ map. Đo đường đi trên collision và kiểm tra mọi spawn tới mọi trạm. |
| MAP-04 | Chủ phòng chọn map; xác nhận ID/revision/epoch; chặn phân vai và bắt đầu trước khi đủ ack. Snapshot v5, roster v3, chặn build cũ ở connection approval. |
| MAP-05 | Chợ Mùa Gặt có quầy nhiều màu, ruộng, nhà/kho, giếng, bãi xe và đủ nhiệm vụ. |
| MAP-06 | Nghĩa Trang Đèn Lồng có cụm mộ, sân tưởng niệm, nhà người trông coi và tuyến đi thay thế. |
| EVT-01 | Event công khai theo server day/phase; không gửi lịch tương lai; mood chỉ tác động world của map, tách HUD/minigame. |
| EVT-02 | BloodMoon, Fog, ClearSky dùng cùng bộ trình bày cho mọi map. |
| EVT-03 | HarvestFestival, GhostMonth, TuongNight; đèn/cờ/sân khấu, sprite tuồng riêng và ambience nhẹ theo phase. |
| MAP-07 / đợt sau | Đầm Sương/Trại Rừng đã dùng Dark Swamp Starter v1.0 và Rural Cabin Exterior v1.0 từ lựa chọn tải miễn phí chính chủ: nước động, cầu, bệ đá động, cabin và đường đất. Có thêm Dinh Thự dùng sàn/vách sẵn có và cơ chế giảm che khuất nhân vật. |
| QA-01 | Đã chạy các nhóm tự động bên dưới. QA ngoài Internet, thiết bị thật, 25 máy riêng và cân bằng vẫn cần người chơi thực tế. |

Tất cả sáu layout có ID/thumbnail/bounds riêng và dùng chung mười task ID/slot.
Sáu event trên mỗi layout tạo 36 tổ hợp trình bày. Bốn task/ngày, luật minigame,
vai, vote, tốc độ di chuyển và thời lượng ngày được giữ theo hệ thống hiện có.
Không thêm buff Trăng Máu, hồi sinh hoặc luật sương che thông tin server.

## Kiểm tra thực tế

Unity `6000.6.0f1`, Windows 11 trên máy hiện tại.

| Nhóm | Kết quả và giới hạn |
| --- | --- |
| Nội dung, collision, đường đi và event | **PASS 1.552 assertions**: sáu map, ID/slot/task, 25 spawn, mọi spawn tới mọi trạm, ít nhất hai hướng tiếp cận mỗi trạm, quét bước lớn/đi chéo/đẩy góc, vị trí sân khấu, bind lặp, phase window, dọn qua ngày/GameOver/Leave; pivot terrain và chín biến thể tile nước động. |
| Hình ảnh | **PASS 48 ảnh**: Day, Night và sáu event cho sáu map, mỗi ảnh có 25 avatar. Đã xem ảnh để sửa crop, nước, vị trí sân khấu và phân biệt layout. |
| Hồi quy luật minigame/offline | PASS bộ luật/asset minigame; **57 assertions** gameplay offline. |
| LAN loopback 25 người | PASS qua sáu map, map-ready trước phân vai, lobby/role/Ready/vote/results/disconnect/rehost; **21 assertions** minigame online. Tổng checks lobby thay đổi theo số frame chờ mạng; xem `Logs/OnlineUIForm/result.txt` của lần cuối. |
| Hai ứng dụng Windows riêng | **PASS host 188 assertions, client 6 assertions; cả hai thoát mã 0**. Client thật được làm lệch revision Chợ, host bị chặn; sau khôi phục nội dung mới được ack, đổi đủ sáu map, phân vai/Ready/bắt đầu, nhận sáu event ở Day/Night, snapshot lặp, dọn sang ngày mới/rời scene và chờ shutdown hoàn tất. Script ngoài kiểm tra cả lỗi log sau chữ PASS. |
| Build đầy đủ | QA Player đã đóng gói/chạy executable. Bản Windows Development không có define QA được ghi ở `Logs/Maps/Player/ThuongMaps.exe`; kết quả build cuối xem `final-callback-safe-build.log`. Giữ cả thư mục Player khi chạy/chuyển bản build. |

Hai tiến trình độc lập chạy trên cùng máy qua LAN loopback, chưa thay cho hai
máy thật hoặc Relay ngoài Internet. Không có thao tác mạng production hay mua
asset trong đợt này. Bộ harness chỉ được biên dịch khi có `THUONG_MAP_QA`.

Log hồi quy Editor có exception từ package Multiplayer PlayMode đã có trước.
Các bộ kiểm tra vẫn hoàn tất PASS. Lần Player đầu phát hiện icon dùng resource
Editor-only; đã thay bằng texture icon runtime và chạy lại host/client. Lần
chạy đã sửa không còn lỗi thiếu resource, exception C# hoặc crash được tìm thấy
trong hai log Player. Callback disconnect không gửi snapshot trong shutdown;
runtime chờ mạng dừng rồi mới hủy và luôn gỡ callback scene. Native log còn cảnh
báo Curl/URP lúc khởi động/thoát.

## Số đo tham khảo

Máy: Intel Core i7-12700H, NVIDIA RTX 3050 Ti Laptop, 1280×720.
Mỗi mẫu gồm 2 người mạng và 23 avatar hình ảnh đứng yên; không giả định là
25 client độc lập. Đo toàn cảnh map, ẩn lobby, batchmode, tắt VSync, lấy 120 frame
sau 30 frame warm-up. Đây là engine frame time từ `Time.unscaledDeltaTime`,
không phải GPU timestamp hoặc chứng nhận FPS khi chơi trên máy khác.

| Cảnh | Mean | P95 | Unity allocated bytes |
| --- | --- | --- | --- |
| Làng / Day | 0,35 ms | 0,58 ms | 172.122.914 |
| Đầm Sương / Fog | 0,37 ms | 0,61 ms | 173.080.879 |

Chưa đo GPU overdraw bằng công cụ GPU và chưa kết luận đạt 60 FPS với 25 máy.
Thời gian đi bộ đo từ spawn giữa tới vùng tương tác khoảng **1,3–3,8 giây**,
tùy map/trạm. Đây là số đo đường đi có collision; mốc 8–10 giây tới trạm xa
trong plan chưa được dùng làm tiêu chí cân bằng đã đạt.

## Mở và kiểm tra lại

- Mở `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity`, Play, Host, chọn MAP trong
  lobby rồi phân vai/Ready. Asset đã được dựng trong project.
- Hướng dẫn kiến trúc, slot, phase, tool và prompt art ở `ThuongMapImplementation.md`.
- Executable chơi bình thường: `Logs/Maps/Player/ThuongMaps.exe`.
- QA executable và log ở `Logs/Maps/QaPlayer` và `Logs/Maps`.
- Ảnh: `Logs/Maps/Views`; bảng tổng hợp: `Logs/Maps/all-map-events-contact.png`.
- Log quan trọng: `complete-plan-build-checks.log`, `shutdown-regression-build.log`, `final-callback-safe-build.log`,
  `content-collision-events.txt`, `independent-host.txt`, `independent-client.txt`,
  `independent-host.txt.performance.txt`, `independent-process-result.json`.

## Còn cần nghiệm thu thủ công

1. Relay/Vivox giữa máy và tài khoản khác nhau, voice đang gọi, mạng trễ/mất gói.
2. Chuột/touch, đổi cửa sổ/tỷ lệ trên thiết bị đích và đánh giá đọc nhãn khi chơi.
3. Hiệu năng 25 client thực, GPU overdraw, cân bằng task/minigame trong ngày 45s.
4. Chơi thử và hoàn thiện mỹ thuật ba layout đợt sau; Cemetery/Market/Woodland/
   Interior trả phí vẫn là nâng cấp tùy chọn, chưa mua.

Nguồn MAP-07: [Dark Swamp](https://hypnobius.itch.io/dark-swamp-pack) và
[Rural Cabin](https://hypnobius.itch.io/rural-cabin-exterior), bản v1.0 tải miễn phí.
SHA256 archive: Dark Swamp `43cb4478dd3b4f80cf9f8e58f66bf68f4e08d37e2618f5d5ee3d9cae5dcf660e`;
Rural Cabin `15896cfd04a6b0a4fcb4aeac519e1e50ac8e6e8c191862dce2d1596daea60af1`.

Các file font/Vivox và plan có sẵn được giữ ở local. URP/shader prefilter,
UnityConnect và Input System preloaded asset do build tự ghi được sao lưu trong Logs/Maps/GeneratedSettingsBackup
và trả về cấu hình trước đợt triển khai; không đổi chính sách analytics hay cấu
hình render toàn game.
