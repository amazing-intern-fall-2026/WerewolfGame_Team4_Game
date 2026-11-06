# Lộ trình tính năng tiếp theo — Thương Online

Cập nhật: 06/11/2026

## Phạm vi

Kế hoạch này áp dụng cho `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity` và luồng online Thương. Vòng chơi hiện tại tiếp tục là chế độ theo phase. Tương tác thời gian thực là **mode mới tách biệt**, không thay luật hiện có cho đến khi lát cắt mới vượt qua kiểm tra gameplay và mạng.

Đã có nền để dùng lại: lobby, map/task guidance, vote có host xác thực, snapshot roster, reconnect trong phiên, luật phòng và UI kết quả. Plan tương tác E chi tiết vẫn ở `Docs/PlayerContextInteractionPlan.md`.

## Thứ tự triển khai

| Mốc | Nội dung | Phụ thuộc |
| --- | --- | --- |
| 0 | Chốt luật và ổn định nền | Sửa quyền sở hữu win evaluator; xác nhận dữ liệu phase trên client; kiểm tra protocol và vòng đời replay. |
| 1 | Bỏ phiếu “Không loại ai” | Vote model, host validation và UI/result hiện tại. |
| 2 | Xem người còn sống sau khi bị loại | Death state, public snapshot, camera và quyền hành động. |
| 3 | Sổ ghi nhớ cá nhân | Giao diện overlay và lịch sử thông tin mà chính người chơi được phép biết. |
| 4 | Mode tương tác gần bằng E | Mode flag riêng, input/range/line-of-sight và host authority. |
| 5 | Xác và báo cáo mở họp | Death records trong mode mới, phase transition và chống báo trùng. |
| 6 | Tutorial cho người mới | Luật, UI và control đã ổn định ở các mode sẽ phát hành. |

## Trạng thái triển khai — 06/11/2026

- **Đã nối vào code:** phân định quyền win evaluator/phase, vote “Không loại ai”, spectator sau quyền cuối của Hunter, sổ riêng phía client, cấu hình mode, tương tác E, xác/báo cáo và hướng dẫn có thể mở lại từ lobby hoặc HUD.
- `ClassicQueued` vẫn là mặc định. `RealtimeProximity` chỉ dùng preset Cơ bản; Khuyển tinh, Bảo vệ và Tiên tri là các role có kỹ năng E tức thời trong MVP. Host kiểm tra lại phase, quyền sống, vị trí do host mô phỏng, khoảng cách, đường nhìn, epoch và request sequence. Endpoint kỹ năng chọn người toàn map bị chặn trong mode mới.
- Giữ E ít nhất 0,3 giây mở danh sách tương tác gần khi có nhiều lựa chọn. Phím hướng chọn; thả E chỉ lưu lựa chọn; nhấn nhanh lần nữa mới gửi action. Xác do vote/disconnect không sinh; báo xác chỉ được chấp nhận một lần và mở Discussion hiện tại.
- Tutorial hiện là chuỗi trang hướng dẫn không dùng role hay thông tin riêng thật. Có thể bỏ qua và mở lại. Sổ tay không gửi qua mạng và được xóa khi rời phòng hoặc tạo ván mới.
- **Chưa mở rộng trong MVP:** các role ngoài ba role kể trên, bộ chọn mục tiêu nhiều bước, kênh chat spectator và tutorial thực hành tương tác. Cần LAN host + hai client, Relay có độ trễ, replay/reconnect, và kiểm tra UI ở 1280×720/1920×1080 trước khi coi mode mới sẵn sàng phát hành.
- Các thay đổi trong lượt này chưa được chạy trong Unity Editor hoặc kiểm tra mạng nhiều client.

## Mốc 0 — Chốt nền và ranh giới mode

- Hoàn tất lỗi quyền sở hữu điều kiện thắng trước khi thêm đường kết thúc ván mới.
- Đảm bảo day/night/phase hiển thị cho client lấy từ snapshot host; không để UI kỹ năng dựa vào singleton gameplay không chạy trên client.
- Thêm hoặc chốt định danh mode trong room settings và snapshot. Mỗi trận chỉ chạy một bộ xử lý luật.
- Giữ `ClassicQueued` làm mặc định. Không đổi cách role action, vote hoặc minigame hiện hành khi chưa chọn mode mới.

**Nghiệm thu:** host và client thấy cùng phase/day/mode; replay tạo epoch mới; không có action cũ hoặc kết quả ván trước lọt sang trận mới.

## Mốc 1 — Bỏ phiếu “Không loại ai”

### MVP

- Thêm lựa chọn vote riêng `NoElimination`; không dùng player ID giả hoặc giá trị sentinel.
- Chỉ người sống, còn quyền vote và đang trong phase Voting mới gửi được lựa chọn. Host vẫn là nơi xác thực quyền, trọng số và vote trùng.
- Quy tắc đề xuất: “Không loại ai” được tính như một lựa chọn trong tổng phiếu. Nếu nó đứng đầu một mình thì không ai bị loại. Nếu hòa ở vị trí cao nhất, không ai bị loại. Không gửi vote khi hết giờ vẫn khác với chủ động chọn “Không loại ai”.
- Kết quả hiển thị riêng số phiếu “Không loại ai”, số người không bỏ phiếu, hòa phiếu và người bị loại; lưu kết quả qua chuyển phase như kết quả vote hiện tại.
- Kết thúc sớm khi mọi người còn quyền vote đã bỏ phiếu, nếu luật hiện tại cho phép kết thúc sớm.

### Nghiệm thu

Thử: mọi người chọn skip; skip thắng ứng viên; skip hòa ứng viên; hòa giữa ứng viên; không ai vote; vote có trọng số; request lặp/sai phase/người chết; đổi phòng và replay. Mọi client phải thấy cùng kết quả.

## Mốc 2 — Spectator sau khi bị loại

### MVP

- Người chết chuyển sang trạng thái spectator sau khi mọi quyền cuối cùng của họ đã xử lý xong. Hunter còn lượt trả thù vẫn dùng được luồng trả thù trước khi vào spectator.
- Người xem chọn một người còn sống để theo dõi hoặc quay về góc nhìn toàn map. Mục tiêu theo dõi chỉ là lựa chọn camera local, không gửi lên host.
- Spectator không di chuyển avatar, làm task/minigame, dùng skill/emote, bỏ phiếu, Ready hoặc gửi action gameplay.
- Hiện thông tin công khai: phase, đồng hồ, kết quả công khai và danh sách người còn sống. Không hiện role, phe, skill target, lịch sử riêng hoặc nguyên nhân bí mật ngoài luật công bố.
- Trong Night, không cho camera bám theo hành động bí mật. MVP hiển thị màn quan sát hạn chế/phase card đến khi trở lại phase công khai.
- Chat người sống và người chết phải tách quyền. Nếu chưa có kênh chat spectator riêng, spectator chỉ xem và không chat.

### Nghiệm thu

Thử chết do vote, đêm, disconnect và Hunter; không cho gửi action từ client spectator; giữ spectator qua phase/reconnect; không lộ thông tin đêm; replay và rời phòng xóa target camera cũ. Hai client phải nhìn thấy cùng roster sống/chết.

## Mốc 3 — Sổ ghi nhớ cá nhân

- Sổ mở/đóng được trong trận và có thể ghi chú tự do cùng vài nhãn nhanh như “đáng nghi”, “đã xác minh”, “cần hỏi”.
- Dữ liệu chỉ nằm trên client, gắn với ID trận hiện tại; không gửi trong RPC/snapshot và không xuất hiện trên host hay máy người khác.
- Ghi chú không tự khẳng định vai trò. Kết quả kỹ năng riêng chỉ được thêm nếu chính người chơi đã nhận kết quả đó.
- Xóa sổ khi rời phòng hoặc bắt đầu ván mới; có xác nhận nếu người chơi đã nhập ghi chú.

**Nghiệm thu:** kiểm tra local-only bằng hai client, reconnect, replay, rời phòng và tên/ghi chú Unicode dài; sổ không che vote/task/role modal và không nhận phím gameplay khi đang nhập.

## Mốc 4 — Mode tương tác gần bằng E

Mode này thay cách thực hiện một số skill trong một ván riêng, không mở khả năng giết tự do trong ngày.

### Lát cắt MVP

- Thêm `RealtimeProximity` riêng trong cấu hình phòng; lobby chỉ cho bật khi role pool tương thích.
- Input E thống nhất: nhấn nhanh thực hiện một action đang hiển thị; giữ E mở lựa chọn khi có nhiều action. Không để một lần nhấn gọi hai handler.
- Bắt đầu với DogSpirit (tấn công), VillageGuardian (bảo vệ) và Seer (soi). Chỉ cho skill ở phase đã định; giữ giới hạn lượt hiện có.
- Client gửi action ID, target ID, epoch và request sequence. Host xác minh người gửi, role, trạng thái sống, phase, range, line-of-sight, charge/cooldown và vị trí có thể tin cậy trước khi xử lý.
- Host xử lý qua một đường duy nhất dùng `DeathResolver`/hệ thống hiệu ứng hiện có; phản hồi kết quả riêng đúng client. Không chạy lại action trong bước ResolveNight.
- Chặn request từ endpoint skill cũ khi mode realtime bật để tránh bỏ qua kiểm tra range.

### Nghiệm thu

Host + ít nhất hai client: mục tiêu vào/ra tầm, sau tường, chết giữa lúc chọn, request trùng/cũ sau replay, giả vị trí, hai action cùng target, bảo vệ trước/sau đòn và phản hồi private của Seer. Sau đó mới mở rộng role; Brat và role chưa có execution hoàn chỉnh chưa vào pool.

## Mốc 5 — Xác và báo cáo mở họp

- Host tạo death record có match epoch, victim ID, vị trí và trạng thái đã báo. Không tạo xác cho vote hoặc disconnect trừ khi luật mode quy định.
- Hiện xác và prompt `E · Báo xác` khi người báo còn sống, trong range và có line-of-sight.
- Host chỉ chấp nhận báo xác một lần. Hai report gần đồng thời chỉ mở một meeting; request ở epoch cũ hoặc xác đã dọn bị từ chối.
- Mở Discussion theo flow hiện tại, đóng menu/action/minigame, khóa di chuyển theo luật họp và sau đó chuyển Voting đúng một lần.
- Không công khai người gây án qua death record, prompt, VFX hoặc meeting payload. Chốt cooldown/tài nguyên trong họp và thời điểm dọn xác trước khi bật cho role pool.

**Nghiệm thu:** hai người báo cùng xác, báo xác đã được báo, ngoài tầm/sau tường, report đồng thời với action, replay, disconnect, Hunter đang chờ trả thù và nhiều xác. Mỗi trường hợp chỉ tạo tối đa một meeting.

## Mốc 6 — Tutorial người mới

- Tutorial có thể bỏ qua, chạy lại từ menu và tách theo mode.
- Phần chung dạy di chuyển, HUD/phase, map và task. Phần vote dạy chọn người hoặc “Không loại ai”. Phần spectator dạy cách theo dõi và giới hạn thông tin. Mode realtime mới có bài riêng về E, range và báo xác.
- Dùng môi trường hướng dẫn an toàn hoặc trận mẫu không ảnh hưởng lobby/matchmaking; không dùng role thật hay kết quả private của người chơi khác.
- Tooltip không che màn hình, có hỗ trợ bàn phím/gamepad và luôn có nhãn chữ bên cạnh icon.

**Nghiệm thu:** người mới hoàn thành từng bài không cần hướng dẫn ngoài game; có thể bỏ qua, thoát giữa chừng, chạy lại; cỡ màn hình 1280×720 và 1920×1080, văn bản tiếng Việt dài.

## Cổng phát hành

1. Unit/Editor checks cho luật vote, spectator permission và host action validation.
2. Host + hai client LAN cho từng luồng; sau đó kiểm tra Relay trên ít nhất hai máy và có độ trễ.
3. Kiểm tra replay, reconnect, leave, người chết/thoát, ván mới và thứ tự nhiều action cùng lúc.
4. Không bật `RealtimeProximity` mặc định cho đến khi role pool, privacy, authority, UI và meeting đều đạt.
5. Cập nhật protocol/build version khi packet hoặc luật mode thay đổi; giữ client khác version không thể vào chung phòng.

## Việc nên làm trước

Triển khai theo chuỗi: **Mốc 0 → bỏ phiếu skip → spectator → sổ ghi nhớ → lát cắt E → báo xác → tutorial**. Sổ có thể làm song song sau khi UI modal được chốt. Tutorial để cuối để không phải viết lại khi luật và thao tác thay đổi.
