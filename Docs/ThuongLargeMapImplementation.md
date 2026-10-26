# Làng Mở Rộng — triển khai 26/10/2026

Bản nền local: `8f25ece`. Nhánh bàn giao: **`thuong`**, theo yêu cầu tiếp tục từ local và đẩy lên nhánh này. Không tích hợp đợt thay đổi này vào `main`.

## Cách chơi

Mở `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity`, Play và tạo phòng. Chủ phòng dùng nút MAP để chọn **Làng Mở Rộng** trước khi phân vai. Map mặc định khi mở phòng vẫn là Làng Trung Tâm. Mọi máy trong phòng cần dùng cùng bản game có `village_large` revision 1.

Map có bounds X `[-20,22]`, Y `[-17,19]`: **42×36 units**, gấp **2,25 lần** diện tích Làng Trung Tâm. Có quảng trường, ruộng/chuồng phía Bắc, chợ/kho phía Đông, xưởng gỗ phía Nam và lối tưởng niệm phía Tây; đường vòng ngoài nối các khu. Giữ 25 spawn, 10 task ID/slot và tốc độ 3 units/giây.

## Nội dung đã thêm

- Definition và prefab `village_large`, thumbnail thật và một tham chiếu bổ sung ở cuối map catalog.
- Menu **Tools → Thuong → Maps → Install Large Village Only** dựng riêng map lớn, không mở/ghi scene gameplay. Menu cài đủ maps cũng bao gồm map lớn.
- Camera preview lấy kích thước theo definition; camera gameplay sử dụng bounds hiện có của session.
- Dải sương tự giãn theo map lớn; các map kích thước 28×24 tiếp tục dùng tỷ lệ cũ.
- Bộ kiểm tra nội dung bao gồm map thứ bảy, mục tiêu thời gian đường đi và việc giữ bounds làng cũ. Harness hai tiến trình đổi đủ map, bắt đầu ván trên map lớn và so sánh hiệu năng cũ/mới.

Đối chiếu SHA256 toàn bộ file đã có trong `Assets/Scenes/Thuong/Maps` trước/sau cài riêng: chỉ **ThuongMapCatalog.asset** thay đổi để bổ sung map mới; toàn bộ prefab, definition, art và thumbnail của sáu map cũ giữ nguyên byte.

## Kết quả kiểm tra

Unity `6000.6.0f1`, Windows trên máy hiện tại.

| Kiểm tra | Kết quả |
|---|---|
| Nội dung/collision/event | **PASS 1.939 assertions** cho 7 map: đủ spawn/task, đường đi từ mọi spawn tới mọi trạm, nhiều hướng tiếp cận, chống xuyên và lifecycle sự kiện |
| Render | **56 ảnh**: Day/Night và 6 event trên 7 map, 25 avatar/map; đã xem ảnh Day và Fog của map lớn |
| Hai ứng dụng độc lập | **PASS host 216, client 6 assertions**; cả hai exit 0, không có exception C#, crash hoặc resource thiếu trong log |
| Đồng bộ | Đổi đủ 7 map, yêu cầu ack mới, chặn client lệch revision, phân vai/Ready/bắt đầu ván trên map lớn, hiển thị 6 event, dọn sạch khi rời scene |
| Windows build | QA Player và bản Development không chứa harness QA đều build thành công; bản chơi thường ở `Logs/Maps/Player/ThuongMaps.exe` (giữ cả thư mục Player khi chuyển máy) |

Hai ứng dụng chạy LAN loopback trên cùng máy; chưa thay cho Relay giữa hai máy, 25 client thực hoặc nghiệm thu cân bằng với người chơi.

### Thời gian đi tới vùng tương tác

Đo bằng walking-distance có collision từ spawn giữa, tốc độ 3 units/giây. Đây là thời gian đi, chưa cộng thời lượng minigame:

| Task | Giây |
|---|---:|
| Lấy nước | 3,29 |
| Thu hoạch | 4,70 |
| Sửa rào | 6,21 |
| Gom củi | 4,37 |
| Dọn kho | 5,35 |
| Thắp đèn | 5,65 |
| Thu gom trứng | 4,87 |
| Pha chế thuốc | 3,54 |
| Bảo vệ khu vực | 9,00 |
| Gỡ bẫy | 5,27 |

Gần nhất **3,29s**, trung vị **5,07s**, xa nhất **9,00s**: đạt các mốc kỹ thuật trong plan. Chưa đổi thời lượng ngày; cần playtest để đánh giá bốn task/ngày cùng minigame.

### Hiệu năng tham khảo

Intel i7-12700H, RTX 3050 Ti Laptop, 1280×720; cùng lần đo, VSync tắt, toàn cảnh map, 120 frame sau 30 frame warm-up, 2 người mạng + 23 avatar hình ảnh đứng yên. Engine frame time, không phải GPU timestamp.

| Tình huống | Mean | P95 | Unity allocated bytes |
|---|---:|---:|---:|
| Làng cũ / Day | 0,46 ms | 0,71 ms | 177.661.800 |
| Làng lớn / Day | 0,37 ms | 0,57 ms | 178.042.678 |
| Làng lớn / Fog | 0,42 ms | 0,59 ms | 178.978.768 |

Các mẫu ngắn chưa cho phép kết luận map lớn nhanh hơn map cũ hoặc đảm bảo FPS trên thiết bị khác.

## File và log

- Mã dựng riêng: `Assets/Scenes/Thuong/Prototype/Editor/ThuongLargeVillageInstaller.cs`.
- Asset: `Assets/Scenes/Thuong/Maps/Definitions/village_large.asset`, `Prefabs/village_large.prefab`, `Art/Thumbnail_village_large.png`.
- Kết quả checks: `Logs/Maps/content-collision-events.txt`, `visuals.txt`.
- Kết quả online: `Logs/Maps/independent-process-result.json`, `independent-host.txt`, `independent-client.txt`, `independent-host.txt.performance.txt`.
- Hình ảnh: `Logs/Maps/Views/village_large_*.png`.
- Hash baseline: `Logs/LargeMap/original-map-hashes.json`.

Phần chưa commit trên máy kia chưa có ở checkout này. Commit map mới được tách riêng để thuận tiện đối chiếu/tích hợp sau; khi mang sang máy kia cần giữ các thay đổi ở máy đó và giải quyết diff tại các file dùng chung.
