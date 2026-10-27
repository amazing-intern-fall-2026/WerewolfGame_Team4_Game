# Năm map mở rộng còn lại — 27/10/2026

> Cập nhật tích hợp main ngày 27/10/2026: catalog chung có 13 layout và 91 biến thể, gồm sáu map gốc, village_large và sáu map expanded. village_large revision 3 dùng sáu minigame/slot hiện tại. Các số liệu kiểm tra ở bản báo cáo trước bên dưới thuộc từng đợt triển khai; xem ThuongExpandedMapsMainMerge.md cho kết quả bản tích hợp.

Đã thêm năm layout mở rộng riêng, dùng lại hệ thống nhiệm vụ, map-ready, collision và event của Làng Liên Hoàn. Sáu layout cũ và `village_expanded` được giữ nguyên nội dung để so sánh.

## Map và cách chơi thử

Mở `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity`, Play → Host → MAP → Chọn map. Chọn một tên dưới đây trước khi phân vai; client phải dùng cùng bản nội dung và xác nhận map-ready.

| Tên trong bộ chọn | ID | Thiết kế đường và điểm nhận diện |
| --- | --- | --- |
| Chợ Mùa Gặt Mở Rộng | `harvest_market_expanded` | Vòng ngoài quanh các quầy, đường xuyên sân chợ, hai hẻm ở đông bắc/tây nam; quầy màu, xe hàng, kho, ruộng và chuồng chợ |
| Nghĩa Trang Đèn Lồng Mở Rộng | `lantern_cemetery_expanded` | Vòng quanh các cụm mộ, sân giữa lệch tâm, lối chéo từ mộ cổ qua sân đèn lồng tới chốt gác; nhà giữ mộ và vườn riêng |
| Đầm Sương Liên Đảo | `mist_swamp_expanded` | Chín cụm đất nối bằng đường gỗ uốn và nhiều cầu; hai lối cắt trên mặt đầm, cabin, lều thảo dược, bệ đá động và nước động |
| Trại Rừng Nhiều Lối | `woodland_camp_expanded` | Các khu trại lệch nhau, đường đất uốn quanh rừng, vòng ngoài và hai lối cắt phía tây; cabin, lều, củi, chuồng và lửa trại |
| Dinh Thự Hai Cánh | `abandoned_manor_expanded` | Chín phòng/khu, hành lang vòng ngoài và trung tâm, hai hành lang phụ vuông góc; sàn gỗ và tường giảm che khuất nhân vật |

Mỗi map có **9 khu, 14 kết nối hai chiều, 2 lối cắt, 25 spawn và 6 trạm minigame**, kích thước **40 × 34 world unit**, revision nội dung **2**. Hai lối phụ của Dinh Thự tạo thêm lựa chọn hành lang, không cam kết luôn ngắn hơn tuyến trung tâm.

Các khu dùng tên và đạo cụ riêng; vị trí sáu nhiệm vụ được phân theo chủ đề. Ví dụ gỗ/trứng ở bãi củi/chuồng của Trại Rừng; nhiệm vụ nông trại của Nghĩa Trang nằm ở khu người trông coi. Mỗi khu chừa một anchor cho nội dung sau này, chưa bật thêm task.

Sau đợt này catalog có **12 layout**: sáu cũ và sáu mở rộng. Mỗi layout có bản thường và sáu event; pool ngẫu nhiên gồm **12 map thường + 72 map event = 84 lựa chọn**. Để xem chắc chắn map mới, dùng chế độ Chọn map.

Build Settings/menu chính của Lợi được giữ theo luồng hiện tại. Công cụ build map dùng trực tiếp scene online Thương; không tự chuyển toàn bộ game sang luồng scene này.

## Phạm vi gameplay

- Giữ sáu minigame: gỗ, thu hoạch, trứng, pha thuốc, bảo vệ và gỡ bẫy; ID và slot tương thích với catalog hiện có.
- Giữ bốn nhiệm vụ chung/ngày, tốc độ online 3 unit/giây và Day của scene online 45 giây.
- Nước, vườn khép kín và tường là vật cản; chưa thêm luật gây sát thương hoặc giết tự do.
- Sương và các event tiếp tục là trình bày; chưa thêm luật giấu thông tin server, buff phe hoặc cửa tự đóng.
- Đạo cụ được đặt ngoài các đoạn di chuyển, spawn, vùng tương tác trạm và anchor dự phòng; vị trí quan trọng không được bỏ qua âm thầm khi thiếu khoảng trống.

## Kiểm tra nội dung và đường đi

| Map mới | Assertions đạt | Từ spawn giữa tới vùng tương tác trạm |
| --- | --- | --- |
| Chợ | 1.170 | 3,04–6,69 giây |
| Nghĩa Trang | 1.204 | 3,80–7,72 giây |
| Đầm Sương | 1.248 | 3,41–6,89 giây |
| Trại Rừng | 1.208 | 2,90–5,98 giây |
| Dinh Thự | 1.218 | 2,71–7,76 giây |

Tổng **6.048 assertions** trong kiểm tra riêng năm map; Làng Liên Hoàn kiểm tra lại **1.156 assertions**. Bộ kiểm tra nội dung/collision/event chung cho cả 12 map đạt **3.753 assertions**. Các bộ có phần kiểm tra chồng lặp; đây không phải số kịch bản người chơi độc lập.

Các kiểm tra gồm:

- Tất cả 25 spawn tới được cả sáu trạm bằng đường có collision thật.
- Kiểm tra từng đoạn của đường thẳng, đường uốn và hành lang phụ bằng footprint nhân vật.
- Vòng ngoài vẫn liên thông khi bỏ khu trung tâm khỏi đồ thị; bỏ từng kết nối vẫn còn tuyến thay thế.
- Trạm có hai hướng tiếp cận gần, không tương tác xuyên vật cản; collider trong prefab khớp obstacle server.
- Bind lặp không sinh trùng world; event snapshot lặp không sinh trùng overlay; GameOver/Leave dọn nội dung.
- Sáu event trên mỗi map; riêng sương kiểm tra bề rộng và phân bố theo kích thước map lớn.

Số giây là đường đi ước lượng trên lưới 0,5 unit tới vùng tương tác, không gồm minigame, chờ hoặc phản xạ người chơi. Cần chơi thử để quyết định cân bằng Day và tiến độ chung.

## Windows và host/client

- Unity `6000.6.0f1`: build thành công bản QA và bản chơi thường; tiến trình build thoát mã 0.
- Chơi thường: `Logs/Maps/Player/ThuongMaps.exe`. Giữ nguyên cả thư mục `Player` khi chạy/chuyển build. Đây là build từ scene online Thương.
- QA: `Logs/Maps/QaPlayer/ThuongMaps.exe`; harness chỉ được biên dịch với `THUONG_MAP_QA`.
- Hai ứng dụng riêng: **host PASS 356 assertions; client PASS 6 assertions**; cả hai thoát mã 0. Script không tìm thấy C# exception, crash hoặc lỗi resource trong log.
- Đã đổi qua đủ 12 layout, chặn phân vai khi thiếu xác nhận map-ready/sai revision, đồng bộ các event, xử lý snapshot lặp và dọn map khi rời scene.
- Chạy cùng máy qua loopback; chưa thay cho hai máy thật hoặc Relay Internet.
- Log và kết quả cuối lưu tại `Logs/ExpandedMaps/Network`; log build: `Logs/ExpandedMaps/verify-build.log`.

### Mẫu đo hiệu năng

Intel Core i5-14400F / NVIDIA GeForce RTX 5060 Ti, 1280 × 720. Mỗi mẫu gồm 2 người mạng và 23 avatar hình ảnh đứng yên; góc nhìn toàn map, ẩn lobby, batchmode, tắt VSync; lấy 120 frame sau 30 frame warm-up. Số đo là engine frame time từ `Time.unscaledDeltaTime`, không phải GPU timestamp hay kiểm chứng 25 client thật.

| Map mới | Day: trung bình / P95 | Fog: trung bình / P95 |
| --- | --- | --- |
| Chợ | 0,55 / 1,23 ms | 0,56 / 1,32 ms |
| Nghĩa Trang | 0,68 / 1,35 ms | 0,70 / 1,43 ms |
| Đầm Sương | 0,35 / 0,73 ms | 0,46 / 0,96 ms |
| Trại Rừng | 0,55 / 1,09 ms | 0,57 / 1,03 ms |
| Dinh Thự | 0,67 / 1,42 ms | 0,68 / 1,27 ms |

Cùng lần đo, Làng cũ/Day là 0,45 ms trung bình, P95 1,10 ms; Làng Liên Hoàn/Day là 0,59 ms và P95 1,37 ms. Không kết luận đạt 60 FPS trên mọi thiết bị từ các mẫu này.

Các cảnh báo native Curl/ComputeBuffer khi khởi động/thoát vẫn cần phân biệt với C# exception. Các cấu hình ProjectSettings và URP do Unity tự ghi được đối chiếu với bản sao trước đợt này và trả lại, giữ nguyên thay đổi local đã có của người dùng; danh sách ở `Logs/ExpandedMaps/restored-settings.json`.

## Ảnh và công cụ dựng lại

- Đã xuất **40 ảnh**: Day, Night và sáu event cho từng map, có 25 avatar minh họa mỗi cảnh; nằm trong `Logs/ExpandedMaps/<mapId>`.
- Ảnh tổng hợp sáu bản mở rộng: `Logs/ExpandedMaps/all-expanded-maps.png`. Thứ tự trái sang phải, trên xuống dưới: Làng, Chợ, Nghĩa Trang, Đầm Sương, Trại Rừng, Dinh Thự.
- Đã xem thumbnail của cả năm map để rà bố cục, nhận diện, vị trí lều/cabin/quầy và nền Nghĩa Trang.

Menu trong Unity:

- `Tools/Thuong/Maps/Install Other Expanded Maps`: dựng riêng năm map và đăng ký sau khi tất cả đạt kiểm tra.
- `Check Other Expanded Maps`: kiểm tra năm map và bộ kiểm tra map chung.
- `Render Other Expanded Maps`: xuất lại 40 ảnh.
- Entry point batch `ThuongExpandedMapsInstaller.VerifyAndBuild`: dựng, xuất ảnh, kiểm tra và build Windows QA/thường.

Nguồn thiết kế là `Assets/Scenes/Thuong/Prototype/Editor/ThuongExpandedMapsInstaller.cs`; dữ liệu tên khu, tọa độ, kết nối, minigame và landmark tập trung trong các `Layout`. Prefab, definition, tile và thumbnail nằm dưới `Assets/Scenes/Thuong/Maps` với tiền tố ID riêng, không ghi đè asset của các map cũ.

Khi thay nội dung đã phân phối, tăng `Revision`. Chỉnh tay trong prefab sinh tự động sẽ bị dựng lại ở lần chạy installer. Công cụ dựng map gốc giữ các map bổ sung trong catalog; UI và QA dùng số lượng map thực tế.

## Nghiệm thu thực tế còn lại

Chơi thử 8–16 người rồi 20–25 người; ghi lượt dùng các tuyến, khu ít người tới và thời gian đạt tiến độ thắng. Kiểm tra hai máy thật, Relay/Vivox, touch, tỷ lệ màn hình và thiết bị đích. Kết nối loopback hoặc 25 avatar minh họa không thay cho các bước này.

Phần 10–12 điểm nhiệm vụ/task instance và minigame mới tiếp tục là chặng sau, dựa trên kết quả chơi thử các layout.
