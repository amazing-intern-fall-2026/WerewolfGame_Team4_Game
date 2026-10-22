# Triển khai map và sự kiện Thương — 21/10/2026

## Phạm vi

Triển khai ban đầu trên nhánh `codex/thuong-map-events`, bắt đầu từ `7ab0950`;
đã ghép `origin/main` tại `8f25ece` với thay đổi local trên nhánh `thuong` ngày
21/10/2026. Cấu hình trạm bên dưới giữ yêu cầu chỉ hoàn thành nhiệm vụ qua minigame.
Scene dùng để chơi vẫn là `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity`.
Map được instantiate trong scene này; không đổi active scene để đổi map.

Sáu layout nằm trong catalog Resources: `village`, `harvest_market`,
`lantern_cemetery`, `mist_swamp`, `woodland_camp`, `abandoned_manor`.
Đầm Sương/Trại Rừng đã nhập art cần dùng từ Dark Swamp Starter Pack v1.0 và
Rural Cabin Exterior v1.0 qua lựa chọn tải miễn phí chính thức. Có nước động,
cầu, bệ đá động, cabin và đường đất. Chưa nhập Old Town Cemetery hoặc pack
trả phí. Dinh Thự dùng sàn gỗ và các
vách có cơ chế giảm che khuất nhân vật. Đây là bản đầu cần chơi thử và cân bằng.

## Cách dùng

1. Mở scene online, Host LAN hoặc Relay như trước.
2. Trước khi tạo phòng, chọn **Ngẫu nhiên** (mặc định) hoặc **Chọn map**.
   Chọn map cho phép chọn layout và Bình thường hoặc một event cụ thể.
   Ngẫu nhiên bốc từ 42 biến thể: sáu map thường và 36 map event (sáu layout
   × sáu event), mỗi biến thể có xác suất bằng nhau.
3. Mỗi client dựng world từ registry nội bộ rồi xác nhận ID, revision và epoch.
   Chỉ khi tất cả xác nhận map/event mới được Phân vai và Bắt đầu game.
4. Trong lobby, chủ phòng bấm nút **MAP** ở góc dưới trái để đổi chế độ,
   chọn map/event và bấm **Áp dụng**. Áp dụng chế độ Ngẫu nhiên bốc lại một
   lần; Hủy giữ nguyên thiết lập. Mọi thay đổi yêu cầu client xác nhận lại.
   Client chỉ xem kết quả; sau khi phân vai, thiết lập map bị khóa.
5. Map/event đã chốt giữ nguyên trong suốt ván. Flow đọc role, Ready,
   nhiệm vụ, minigame, vote và kết quả tiếp tục dùng hệ thống hiện có.

Nếu thiếu map hoặc revision khác, lobby báo cần dùng cùng bản game và chặn
bắt đầu. Không nhận đường dẫn prefab qua mạng. Host chặn client protocol cũ
trong bước connection approval.

## Cấu hình và giao thức

- Runtime: `Assets/Scripts/Thuong/Maps` và partial session `ThuongOnlineMaps.cs`.
- Map prefab/definition/thumbnail: `Assets/Scenes/Thuong/Maps`.
- Registry đóng gói: `Resources/ThuongMapCatalog.asset`.
- Collision: swept AABB với footprint nửa rộng 0,23, nửa cao 0,13, offset chân
  `(0, 0.12)`, skin 0,003; tối đa bốn lần xử lý trượt trong mỗi bước.
  Chỉ truy vấn obstacle của definition; không truy vấn collider người chơi hoặc
  local physics scene của minigame. Các BoxCollider2D trong prefab dùng cùng footprint.
- Snapshot: `thuong.online.snapshot.v5`; roster: `thuong.online.roster.v3`.
  Header thêm map ID/revision/epoch, event content revision 2, biến thể event/ngày.
  Biến thể được công khai ngay trong lobby; không gửi role bí mật trong header.
- Map-ready: `thuong.online.map-ready.v1`. Đổi map vô hiệu hóa ack cũ; gói ack
  khác ID/revision/epoch không mở khóa bắt đầu.
- Mỗi map có 25 spawn và sáu slot minigame giống nhau. Tăng `contentRevision` khi thay
  đổi layout/collision/binding; các build cùng phòng phải có cùng nội dung.
  Đầm Sương và Trại Rừng hiện revision 3; bốn layout còn lại revision 2.

| Slot | ID ổn định | Nhiệm vụ |
| --- | --- | --- |
| 0 | thuong.task.wood | Chặt gỗ |
| 1 | thuong.task.harvest | Thu hoạch |
| 2 | thuong.task.eggs | Thu gom trứng |
| 3 | thuong.task.potion | Pha chế thuốc |
| 4 | thuong.task.defender | Bảo vệ khu vực |
| 5 | thuong.task.traps | Gỡ bẫy |

Catalog minigame schema v2 giữ mapping và luật; `additionalStations` rỗng và
`stationPrefab` của catalog bằng null. Prefab trạm và vị trí thuộc map definition.
Bốn minigame được chọn mỗi ngày từ sáu trạm, mask 16 bit,
tốc độ 3 unit/giây và luật vai/trận không đổi. Tương tác server kiểm tra cả
khoảng cách và đường tiếp cận, không nhận thao tác xuyên vật cản.
Gói `CompleteTask` cũ không cộng tiến độ; phần thưởng chỉ do server cấp sau
khi lượt minigame hợp lệ hoàn thành.

## Sự kiện

Biến thể map đã bốc quyết định event cho cả ván. Map thường (`None`) giữ
nguyên trạng thái bình thường trong mọi ngày. Map event giữ cùng loại event
qua các ngày và hiển thị theo pha phù hợp bên dưới. Client nhận kết quả từ
server, không tự random riêng. Luồng offline vẫn giữ lịch event một ngày cũ.
Cùng sáu bộ presentation dùng cho mọi map:

- BloodMoon: tint đỏ và than hồng trong Night/ResolveNight.
- Fog: dải sương chuyển động ở Day/Night; tắt trong các pha hội thoại.
- HarvestFestival: cờ/đèn, ambience rất nhẹ; tắt âm thanh ban đêm và hội thoại.
- ClearSky: tint ấm/ánh sáng trang trí ban ngày; ban đêm dùng tint đêm thường.
- GhostMonth: đèn tưởng niệm, chuyển động trôi nhẹ và tint đêm xanh.
- TuongNight: sân khấu và biển mặt nạ ngoài tuyến chính; nhịp biểu diễn ban đêm,
  âm thanh giảm ở ban ngày, tắt trong hội thoại/vote.

Overlay không có collider hoặc manager riêng. Áp lại snapshot không nhân đôi
world, đèn hoặc audio. Qua ngày, overlay được dựng lại cho cùng biến thể;
GameOver, Leave hoặc đổi map đều dọn overlay, dừng audio và trả màu gốc.
Tint chỉ áp SpriteRenderer/Tilemap thuộc world, giữ
marker/nhãn trạm sáng; không chạm HUD hoặc RenderTexture của minigame.
Sương không giới hạn thông tin server, Trăng Máu không buff sói và không hồi sinh.

## Công cụ

- `Tools/Thuong/Maps/Install Maps And Events`: dựng prefab/definition/thumbnail.
- `Check Content Collision And Events`: cấu hình, collision, đường đi, lifecycle.
- `Render All Map Event Combinations`: 48 ảnh với 25 avatar/layout.
- `Build Independent Multiplayer QA`: build Windows với harness chỉ bật khi có
  tham số `--thuong-map-qa host|client <đường-dẫn-output>`.
- `Build Windows Player`: build Windows không chứa harness QA.
- `Tools/Thuong/Minigames/Check Online With 25 Players`: hồi quy LAN gồm đổi map.

Sau khi build QA Player, chạy `Tools/run_thuong_map_player_checks.ps1` để khởi
động host/client ở hai tiến trình riêng. Script kiểm tra cả mã thoát và log:
ghi chữ PASS trước khi ứng dụng crash vẫn bị coi là thất bại. Kết quả tổng hợp
ở `Logs/Maps/independent-process-result.json`.

Chạy lại cắt sprite bằng Python có Pillow: `Tools/prepare_thuong_map_assets.py`
nếu sửa nguồn crop. Hai archive v1.0 cần có tại `Logs/Maps/Downloads`,
sau đó chạy installer. Installer chỉ dựng asset trong thư mục Maps, không ghi
đè scene prototype hoặc generator làng cũ. Thay đổi thủ công trong prefab do
installer quản lý sẽ bị dựng lại; cấu hình map cần được đưa vào installer.

## Art và provenance

Sprite cắt từ LPC/Hypnobius đã có và hai pack v1.0 bổ sung; giữ credit và notice riêng.
Nguồn chính chủ: https://hypnobius.itch.io/dark-swamp-pack và
https://hypnobius.itch.io/rural-cabin-exterior. Không nhập demo/controller của pack.
Notice cũng được đóng gói tại `StreamingAssets/ThuongCredits`.
Biển tuồng: `Assets/Scenes/Thuong/Maps/Art/TuongTheatreSign.png`, PNG alpha,
1536×1024; importer Point, 384 PPU. Công cụ: built-in imagegen.

Prompt đã dùng nguyên văn:

“Create one finished game-ready decorative hanging wooden theatre sign for a
Vietnamese Tuong theatrical stage in a medieval top-down 3/4 village pixel-art
game. A compact horizontal sign displays two authentic Vietnamese Tuong painted
theatrical faces: one noble red and white face with black arched eyebrows and a
black beard, one white and black dramatic face with a red accent. They are painted
theatrical faces on flat masks, NOT Japanese noh/oni masks, no horns. Rich muted
crimson cloth swag above a dark aged wooden frame, modest gold ornamental trim.
Make the masks clearly legible at small game scale. Single isolated prop,
centered, transparent background, no floor, no scene, no characters, no text, no
lettering, no shadow outside object. Crisp true pixel-art look, deliberately
coarse pixel grid equivalent to a 192x128 sprite enlarged with nearest neighbor,
limited earthy palette, hard edges, no antialiasing. Front-facing sign compatible
with 3/4 overhead medieval LPC-style props, restrained colors. Entire prop fully
inside frame with generous transparent margins. Output PNG with real alpha.”

## Kiểm tra và giới hạn

Log kiểm tra thực tế nằm trong `Logs/Maps`; xem báo cáo kết quả cùng thư mục Docs.
Kết quả tự động không thay cho chơi thử nhiều máy, Relay ngoài Internet, voice
đang gọi thật, touch trên thiết bị và đánh giá mỹ thuật/cân bằng. Chưa kết luận
đạt 60 FPS trên mọi máy. Mục tiêu quãng đường trong plan là mốc thử; phải dùng
số đo đường đi thực và kết quả chơi thử để quyết định chỉnh layout.
