# Làng Mở Rộng — revision 3, 27/10/2026

> Cập nhật tích hợp main ngày 27/10/2026: catalog chung có 13 layout và 91 biến thể, gồm sáu map gốc, village_large và sáu map expanded. village_large revision 3 dùng sáu minigame/slot hiện tại. Các số liệu kiểm tra ở bản báo cáo trước bên dưới thuộc từng đợt triển khai; xem ThuongExpandedMapsMainMerge.md cho kết quả bản tích hợp.

Bản tích hợp main giữ mạng đường của revision 2 và chuyển cấu hình trạm sang sáu minigame hiện tại. Vị trí trạm được ánh xạ theo task ID; station prefab lấy từ map gốc. Revision 3 phân biệt nội dung mới với bản mười nhiệm vụ trước đây.

## Cách chơi

Mở `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity`, Play và tạo phòng: **Làng Mở Rộng được chọn sẵn**. Chủ phòng vẫn có thể chọn map khác trước khi phân vai. Mọi máy trong phòng cần dùng cùng bản game có `village_large` **contentRevision 3**.

Scene lưu `initialMap` tới definition và bản xem trước dưới **Thuong Online Map Preview**. Bản xem trước tắt trước khi session dựng world/trạm thật. Menu **Tools → Thuong → Maps → Use Large Village In Online Scene** áp lại cấu hình này.

Bounds vẫn là X `[-20,22]`, Y `[-17,19]`: **42×36 units**, gấp **2,25 lần** diện tích Làng Trung Tâm. Giữ 25 spawn và sáu task ID/slot hiện tại, tốc độ 3 units/giây và luật ngày/sự kiện hiện có.

## Bố cục mới

- Ba vòng đường đá nối các khu Bắc, Nam và Tây; bảy tuyến đường đất cắt ngang hoặc vòng ngoài, cùng bốn nhánh ra mép map.
- Quảng trường nhỏ để định hướng. Có tuyến nối các khu mà không cần quay lại quảng trường.
- Bỏ bức tường dài khu tưởng niệm; thay hàng cây kín quanh biên bằng cụm cây thưa và khoảng mở.
- Nhà nằm giữa các tuyến đường; thêm component làm mờ khi nhân vật đi phía sau nhà.
- Phân bố lại nhiệm vụ dọc mạng đường. Mỗi trạm có bốn hướng tiếp cận tại khoảng cách 1,5 units; vẫn tới được theo đường đã vẽ khi đóng một đoạn tuyến chính.
- Tái sử dụng sprite hiện có. Tile đất được căn và phủ đầy ô để lối tắt liền mạch.

Menu **Install Large Village Only** dựng riêng map lớn. Đối chiếu SHA256 với snapshot trước lần sửa này: trong toàn bộ file map có sẵn, chỉ definition, prefab và thumbnail `village_large` thay đổi. Catalog và toàn bộ asset của sáu map cũ giữ nguyên byte. Thêm hai tile riêng cho đường đất/ruộng của map lớn.

## Kết quả lịch sử ở revision 2

Unity **6000.6.0f1**, Windows. Dựng lại asset, render và build trong bản sao project tại `Logs/MapRoutesV2/ValidationProject`, sau đó chép đúng các asset map/scene cuối về checkout hiện tại.

| Hạng mục | Kết quả revision 2 |
|---|---|
| Nội dung/collision/event | **PASS 1.871 assertions** cho 7 map, bao gồm 42 assertions về mạng đường của map lớn |
| Hướng tiếp cận | Cả 10 trạm có **4 hướng** thoáng quanh trạm |
| Tuyến thay thế | Cả 10 trạm vẫn có đường khi đóng vùng **5,4×5,4 units** ở giữa tuyến chính |
| Đi vòng quảng trường | Các khu nhiệm vụ vẫn liên thông khi đóng vùng quảng trường **7×7 units** |
| Render | **56 ảnh** Day/Night và 6 event trên 7 map, 25 avatar/map; đã xem Day và Fog của map lớn |
| Windows build | QA Player và bản Development dùng để chơi đều build thành công; Unity thoát với mã 0 |

Kiểm tra tuyến dùng tile đường đã vẽ cùng footprint collision thật. Số ô trong báo cáo dùng để xác nhận tuyến tồn tại, không phải thời gian di chuyển.

Bản chơi ở `Logs/Maps/Player/ThuongMaps.exe`; giữ cả thư mục **Player** khi chuyển máy. Chưa chạy lại hai tiến trình host/client trên revision 2; kết quả host 219/client 9 và benchmark đã lưu trước đó thuộc revision 1. Chưa nghiệm thu Relay giữa hai máy, 25 client thực hoặc cân bằng với người chơi.

### Thời gian tới vùng tương tác

Walking-distance có collision từ spawn giữa, tốc độ 3 units/giây; chưa cộng minigame:

| Task | Giây |
|---|---:|
| Lấy nước | 3,15 |
| Thu hoạch | 4,84 |
| Sửa rào | 5,51 |
| Gom củi | 4,51 |
| Dọn kho | 5,35 |
| Thắp đèn | 5,98 |
| Thu gom trứng | 4,54 |
| Pha chế thuốc | 2,87 |
| Bảo vệ khu vực | 7,25 |
| Gỡ bẫy | 5,04 |

Gần nhất **2,87s**, trung vị **4,94s**, xa nhất **7,25s**. Cần chơi thử với người để đánh giá định hướng và nhịp hoàn thành nhiệm vụ.

## File và log

- Mã dựng: `Assets/Scenes/Thuong/Prototype/Editor/ThuongLargeVillageInstaller.cs`.
- Kiểm tra mạng đường: `Assets/Scenes/Thuong/Prototype/Editor/ThuongLargeVillageRouteChecks.cs`.
- Asset: `Assets/Scenes/Thuong/Maps/Definitions/village_large.asset`, `Prefabs/village_large.prefab`, `Art/Thumbnail_village_large.png`.
- Báo cáo: `Logs/Maps/content-collision-events.txt`, `village-large-routes.txt`, `visuals.txt`.
- Hình ảnh: `Logs/Maps/Views/village_large_*.png`.
- Log build: `Logs/MapRoutesV2/validation-build.log`.
- Hash baseline: `Logs/MapRoutesV2/Before/map-hashes.json`.

Kết quả kiểm tra revision 3 trên bản tích hợp main được ghi tại `ThuongExpandedMapsMainMerge.md`.
