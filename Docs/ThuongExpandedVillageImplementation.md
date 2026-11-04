# Làng Liên Hoàn — bản triển khai đầu tiên

> Cập nhật tích hợp main ngày 27/10/2026: catalog chung có 13 layout và 91 biến thể, gồm sáu map gốc, village_large và sáu map expanded. village_large revision 3 dùng sáu minigame/slot hiện tại. Các số liệu kiểm tra ở bản báo cáo trước bên dưới thuộc từng đợt triển khai; xem ThuongExpandedMapsMainMerge.md cho kết quả bản tích hợp.

Ngày: **27/10/2026**. Phạm vi: map mới dùng sáu minigame hiện có, theo `ThuongMultiRouteMapExpansionPlan.md`.

## Nội dung

- ID: `village_expanded`; tên hiển thị: **Làng Liên Hoàn**; revision nội dung: **2**.
- Bounds: `(-20,-17)` tới `(20,17)`, kích thước 40 × 34 world unit.
- Chín khu: Rừng Bẫy, Ruộng Lúa, Chuồng Trại, Xưởng Mộc, Quảng Trường, Nhà Thuốc, Kho Hàng, Chợ/Cổng Nam, Trạm Gác.
- Mười bốn kết nối hai chiều: vòng ngoài tám cạnh, bốn đường nối trung tâm và hai lối cắt công khai.
- Sáu trạm giữ task ID/slot của catalog hiện tại; mỗi trạm nằm ở một khu riêng. Kho/Chợ và các anchor dự phòng dành cho nhiệm vụ tiếp theo, chưa bật thêm nhiệm vụ.
- Hai mươi lăm điểm xuất phát quanh quảng trường; tên khu, hàng rào, vườn, nhà, cây, xe hàng và quầy chợ giúp nhận diện tuyến.
- Những vườn khép kín có hàng rào thể hiện ranh giới và collider tương ứng ở client/server. Mái nhà dùng cơ chế giảm che khuất có sẵn.
- Hỗ trợ map thường và sáu event qua hệ thống trình bày hiện có.
- Sương lấy số dải và bề rộng theo bounds của map lớn; giữ cách trình bày map nhỏ trước đây.

Luật ngày/đêm, sáu minigame, bốn nhiệm vụ chung/ngày và thời gian Day được giữ theo luồng hiện tại. Chưa triển khai nhiệm vụ cá nhân, 10–12 trạm, phá hoại, hầm dành cho vai hoặc giết tự do.

## Mở map

1. Mở `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity` trong Unity.
2. Play, chọn **Chọn map**, chọn **Làng Liên Hoàn** và event mong muốn; hoặc tạo phòng rồi dùng nút **MAP** ở lobby trước khi phân vai.
3. Các máy dùng cùng nội dung map; chờ map-ready, phân vai, đọc vai, Ready và bắt đầu như luồng cũ.

Map nằm trong catalog chung và cả pool ngẫu nhiên. Riêng đợt Làng tạo 7 map thường và 42 biến thể event, tổng cộng 49 lựa chọn. Sau đó đã thêm năm map mở rộng còn lại: catalog hiện có 12 layout và 84 biến thể; xem [báo cáo mở rộng các map còn lại](ThuongExpandedMapsImplementation.md). Sáu layout cũ được giữ để so sánh.

Build Settings chính vẫn là luồng MainMenu/Lobby/Game/Result của Lợi. Công cụ build map đóng gói trực tiếp scene online Thương; chưa tự đổi luồng menu chính của toàn project.

## Dựng lại và kiểm tra

- `Tools/Thuong/Maps/Install Expanded Village`: dựng riêng map mới, đăng ký vào catalog và kiểm tra.
- `Tools/Thuong/Maps/Check Expanded Village`: kiểm tra topology, collision, trạm, spawn, event và lifecycle.
- `Tools/Thuong/Maps/Render Expanded Village`: xuất 8 ảnh tại `Logs/ExpandedVillage`.
- Entry point batch `ThuongExpandedVillageInstaller.VerifyAndBuild`: dựng map, xuất ảnh, kiểm tra toàn bộ map và build Windows thường/QA.
- Bộ kiểm tra hai ứng dụng riêng: `Tools/run_thuong_map_player_checks.ps1`, sau khi có QA build.

Nguồn cấu hình là `ThuongExpandedVillageInstaller.cs`. Thay đổi thủ công trong prefab sinh tự động sẽ bị dựng lại. Khi thay layout/collision/binding đã phân phối, tăng `LayoutRevision`; installer giữ revision cao hơn đang có và không hạ revision của map cũ.

Installer map gốc đã được sửa để giữ các layout thêm riêng trong catalog. Bộ chọn map, kiểm tra pool và QA duyệt map dùng số lượng nội dung thực tế. Công cụ xuất ảnh toàn bộ map cũng lấy kích thước từ bounds thay vì khung cố định của map nhỏ.

## Kết quả kiểm tra nội dung

- Map mới: **PASS 1.156 assertions**.
- Bộ map chung: **PASS 1.900 assertions**, gồm các layout cũ và map mới.
- Tất cả 25 spawn đi tới cả 6 trạm.
- Kiểm tra từng đoạn đường bằng footprint thật; hai lối cắt có đường đi liên tục.
- Khi loại quảng trường khỏi đồ thị, vòng ngoài vẫn liên thông. Bỏ từng kết nối bất kỳ vẫn còn tuyến thay thế.
- Collider địa hình trong prefab khớp obstacle mà server sử dụng; collider tương tác trạm được kiểm tra riêng với runtime hiện có.
- Binding lặp lại không nhân đôi world; event snapshot lặp không nhân overlay; GameOver/Leave dọn nội dung.
- Đã xuất 8 ảnh có 25 avatar minh họa; kiểm tra trực quan riêng ban ngày, ban đêm và sương.

## Windows và hai ứng dụng độc lập

- Unity `6000.6.0f1`: build thành công bản QA và bản chơi thường; tiến trình build thoát mã 0.
- Bản chơi thường: `Logs/Maps/Player/ThuongMaps.exe`. Giữ nguyên cả thư mục `Player` khi chạy hoặc chuyển build.
- Bản QA: `Logs/Maps/QaPlayer/ThuongMaps.exe`; các lệnh/harness QA chỉ có trong build với `THUONG_MAP_QA`.
- Kiểm tra cuối: **host PASS 216 assertions; client PASS 6 assertions; cả hai thoát mã 0**. Script không tìm thấy C# exception, crash hoặc lỗi resource trong hai log.
- Hai ứng dụng đã đồng bộ cả bảy map, chặn bắt đầu khi sai revision, yêu cầu xác nhận lại sau đổi map, nhận đủ sáu event và dọn nội dung khi rời scene.
- Hai ứng dụng chạy trên cùng máy qua loopback; chưa phải hai máy thật/Relay Internet.
- Kết quả và log được lưu riêng tại `Logs/ExpandedVillage/Network`; log build cuối: `Logs/ExpandedVillage/final-polish-build.log`.
- Cấu hình `ProjectSettings` do Unity tự ghi trong lúc build được đối chiếu với bản sao trước khi chạy và trả lại, giữ nguyên các thay đổi local đã có trước đợt này. Danh sách nằm ở `Logs/ExpandedVillage/restored-settings.json`.

### Mẫu đo hiệu năng

Intel Core i5-14400F, NVIDIA GeForce RTX 5060 Ti, 1280 × 720. Mỗi cảnh có 2 người mạng và 23 avatar hình ảnh đứng yên; góc nhìn toàn map, ẩn lobby, batchmode, tắt VSync. Lấy 120 frame sau 30 frame warm-up. Đây là thời gian frame của engine từ `Time.unscaledDeltaTime`, không phải số đo GPU hoặc xác nhận hiệu năng 25 client thật.

| Cảnh | Trung bình | P95 |
| --- | --- | --- |
| Làng cũ / Day | 0,86 ms | 2,38 ms |
| Đầm Sương / Fog | 1,01 ms | 1,97 ms |
| Làng Liên Hoàn / Day | 1,25 ms | 2,41 ms |
| Làng Liên Hoàn / Fog | 1,24 ms | 3,26 ms |

Log native vẫn có cảnh báo Curl/ComputeBuffer khi khởi động hoặc thoát; script không coi những cảnh báo này là bằng chứng C# exception. Không kết luận đạt 60 FPS trên mọi thiết bị.

### Thời gian đi bộ

Đo từ spawn giữa tới node nằm trong vùng tương tác, dùng lưới 0,5 unit, tốc độ online 3 unit/giây và collision thật. Đây là thời gian đường đi ước lượng trên lưới, chưa gồm thao tác người chơi hoặc minigame.

| Nhiệm vụ | Thời gian |
| --- | --- |
| Gỗ | 5,00 giây |
| Thu hoạch | 3,71 giây |
| Trứng | 6,50 giây |
| Pha thuốc | 4,67 giây |
| Bảo vệ | 7,00 giây |
| Gỡ bẫy | 6,93 giây |

Map thử ưu tiên phù hợp Day 45 giây; chưa kéo trạm xa tới mốc thử 8–12 giây trong plan. Quyết định tăng Day hoặc mở rộng thêm cần dựa trên chơi thử và tiến độ chung của cả nhóm.

## File chính

- `Assets/Scenes/Thuong/Maps/Definitions/village_expanded.asset`
- `Assets/Scenes/Thuong/Maps/Prefabs/village_expanded.prefab`
- `Assets/Scenes/Thuong/Maps/Art/Thumbnail_village_expanded.png`
- `Assets/Scenes/Thuong/Prototype/Editor/ThuongExpandedVillageInstaller.cs`
- `Assets/Scenes/Thuong/Prototype/Editor/ThuongExpandedVillageChecks.cs`
- `Assets/Scripts/Thuong/Maps/ThuongMapNavigation.cs`: lưới có cache cạnh và hàng đợi ưu tiên phục vụ kiểm tra đường, không chạy trong vòng di chuyển gameplay.
- `Logs/ExpandedVillage/checks.txt`, `Logs/Maps/content-collision-events.txt` và log build/QA liên quan.

## Nghiệm thu còn cần người chơi

Chơi thử 8–16 người rồi 20–25 người; đo tuyến thường dùng, khu ít người tới, độ dễ nhận diện và tiến độ thắng. Kiểm tra hai máy thật, Relay/Vivox, touch và hiệu năng thiết bị đích. Avatar minh họa hoặc kết nối loopback không thay cho các bước này.

Mốc tiếp theo là chỉnh layout theo dữ liệu chơi thử, rồi mới tách task instance và tăng lên 10–12 điểm nhiệm vụ.
