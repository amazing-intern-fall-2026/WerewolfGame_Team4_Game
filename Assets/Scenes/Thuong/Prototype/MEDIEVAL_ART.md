# Medieval top-down prototype của Thương

Scene chính: `ThuongGameplayPrototype.unity` (Unity 6000.6.0f1).

## Chơi

Mở scene ở chế độ Single và nhấn Play. Đóng thẻ vai bằng **OK / BẮT ĐẦU**.

- WASD / mũi tên / cần trái: di chuyển trên mặt phẳng XY.
- E: thực hiện một nhiệm vụ đang hoạt động trong phạm vi 1.3 unit.
- F1–F12, PageUp/PageDown: đổi người chơi và mục tiêu camera.
- Tab: chuyển giữa camera theo người chơi và toàn cảnh làng.
- R: tải lại scene và bắt đầu ván mới.
- NGUỒN ASSET: xem giấy phép và ghi nguồn các tác giả trong game.

Lobby size, vai ngẫu nhiên, kỹ năng, lịch event, 4 nhiệm vụ/ngày, tiến độ tổng,
thảo luận/bỏ phiếu và điều kiện thắng vẫn do các manager hiện có điều khiển.
Skin trên map chọn theo Player ID, độc lập với phe/vai bí mật.

## Art và scene

- `Art/Medieval/Source`: PNG gốc của Hypnobius, các lớp walk/hurt LPC của
  wulax, Decorations và Farming; giữ README/LICENSE/CREDITS gốc.
- `Art/Medieval/Sprites`: các crop đồ vật, nhà ghép từ tiles của Hypnobius,
  20 bộ sprite đi 4 hướng và bị hạ được ghép từ các lớp LPC.
- `Art/Medieval/Resources`: 28 hình thẻ vai, khung giấy/gỗ và văn bản ghi nguồn.
- `Art/Medieval/Unity`: tiles, catalog skin và Animator Controller/clip.
- `Map/Medieval Village`: terrain tilemap, đường, nhà, cây, quầy chợ và
  collider. Những hình khối cũ trong Map được ẩn, để bản gốc có thể đối chiếu.
- Sáu TaskStation gốc giữ TaskData/input/reference; art là object con.
- Player và GameplayPlayer prefab giữ identity, PlayerMovement,
  PrototypeControls và Rigidbody2D; collider nhỏ đặt ở chân, gravity = 0.

Camera orthographic nhìn theo trục Z, movement trên XY, nhân vật có bốn hướng
Up/Left/Down/Right. Sprite có pivot tại chân; player và props được sắp lớp theo
tọa độ Y. Góc nhìn của asset là kiểu top-down 3/4 thường dùng trong RPG 2D.
Ban đêm thay tint của terrain/nhân vật/props; đuốc vẫn sáng và có animation.

## Chỉnh skin hoặc map

- Đổi sprite/controller trong `Art/Medieval/Unity/MedievalCast.asset`.
- Di chuyển/nhân các props dưới `Map/Medieval Village` như các object Unity
  bình thường; chỉnh collider theo footprint ở chân.
- Camera có Gameplay Size, Smoothing và giới hạn map trong Inspector.
- Chỉnh palette/lớp trang phục/crop trong `Tools/prepare_thuong_medieval_assets.py`.
  Công cụ ghép/cắt từ asset gốc, không tạo art bằng AI.
- Để áp dụng lại cấu hình, dùng **Tools → Thuong → Apply Medieval Top-Down Art**.
  Lệnh tạo backup ở Logs trước khi cập nhật và dựng lại phần làng/art do lệnh
  quản lý; sao lưu các chỉnh sửa thủ công trong phần đó trước khi chạy lại.

## Kiểm tra

- **Tools → Thuong → Check Medieval Top-Down Scene**: kiểm tra sprites,
  controllers, camera, references và đường đi tới cả sáu nhiệm vụ.
- Batch entry point `ThuongMedievalSceneInstaller.InstallAndCheck`: import,
  apply, kiểm tra tích hợp và click vote, rồi kiểm tra Play Mode bằng bàn phím
  ảo cho di chuyển bốn hướng, F2, E, ngày/đêm/vote và R.
- `ThuongPrototypeChecks.Run` vẫn dùng được để chạy regression gameplay và
  các kỹ năng/target rules đã có.
- Ảnh QA được lưu ở `Logs/thuong-medieval-*.png`.

## Giấy phép

Đọc `Art/Medieval/ATTRIBUTION.txt` và các notice gốc trong Source. Bản credit
cũng được đưa vào Resources để có trong build và màn hình NGUỒN ASSET.

- [Hypnobius Medieval Village Exterior](https://hypnobius.itch.io/medieval-village-exterior-tileset):
  giấy phép riêng của tác giả cho dùng/chỉnh sửa trong game, kể cả thương mại.
- [wulax LPC Characters](https://opengameart.org/content/lpc-medieval-fantasy-character-sprites),
  [LPC Decorations](https://opengameart.org/content/lpc-medieval-village-decorations),
  [Daniel Eddeland LPC Farming](https://opengameart.org/content/lpc-farming-tilesets-magic-animations-and-ui-elements):
  các sprite dẫn xuất được ghi nguồn và giữ CC BY-SA 3.0.

Không đổi giấy phép code của dự án hoặc giấy phép của các asset khác.
