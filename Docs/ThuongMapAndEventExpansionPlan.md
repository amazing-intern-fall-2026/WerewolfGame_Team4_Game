# Plan mở rộng map và map sự kiện — Thương

Ngày khảo sát: **19/10/2026**. Trạng thái: **đề xuất để triển khai**, đã đối chiếu asset và code trong project, đã kiểm tra các trang asset của tác giả. Chưa tải/import asset mới, mua asset hoặc sửa gameplay trong đợt khảo sát này.

## 1. Hướng triển khai đề xuất

Giữ phong cách **làng trung cổ, pixel art top-down 3/4**, dùng bộ Hypnobius 48px làm nền và nhân vật LPC hiện có. Không cần thay bộ art đang dùng để có thêm map.

- **Đợt 1:** giữ Làng hiện tại, thêm **Chợ Mùa Gặt** và **Nghĩa Trang Đèn Lồng** bằng nguồn đã có; thêm đủ **6 biến thể sự kiện** cho ba map này.
- **Đợt 2:** thêm **Đầm Sương** và **Trại Rừng** bằng các pack cùng tác giả; ưu tiên nguồn tải miễn phí/tùy trả tiền.
- **Đợt 3:** cân nhắc **Dinh Thự Bỏ Hoang** và chế độ có luật sự kiện riêng sau khi kiểm chứng cân bằng.

Một ván chọn một map nền tại lobby. “Map sự kiện” ở đợt đầu là **biến thể của map nền**: thêm trang trí, màu sắc, animation, âm thanh và thông báo theo lịch event. Bố cục đường đi, vị trí nhiệm vụ và collider giữ ổn định trong ván. Cách này tạo thêm không khí mà không làm người đang làm nhiệm vụ bị đổi vị trí hoặc mất kết nối.

## 2. Project đã có gì để tái sử dụng

| Nguồn trong project | Có thể dùng cho map nào | Công việc còn lại |
| --- | --- | --- |
| [Hypnobius source](/D:/Project/WerewolfGame_Team4_Game/Assets/Scenes/Thuong/Prototype/Art/Medieval/Source/Hypnobius) và [World sprites](/D:/Project/WerewolfGame_Team4_Game/Assets/Scenes/Thuong/Prototype/Art/Medieval/Sprites/World) | Nhà, đường đá, cỏ, xe kéo, thùng, hàng rào, cây, đuốc; bộ nền chung cho mọi map | Ghép layout mới; chuẩn hóa prefab và footprint collider |
| [LPC Decorations sheet](/D:/Project/WerewolfGame_Team4_Game/Assets/Scenes/Thuong/Prototype/Art/Medieval/Source/LPCDecorations/decorations-medieval.png) | Đã thấy bia mộ, tượng, đèn lồng, mái quầy chợ, rèm, lều, lửa trại, giếng, xe hàng trong ảnh nguồn | Nhiều món chưa có sprite/prefab riêng; cắt bổ sung, chỉnh tỷ lệ và kiểm tra palette cạnh nhà hiện tại |
| [LPC Farming](/D:/Project/WerewolfGame_Team4_Game/Assets/Scenes/Thuong/Prototype/Art/Medieval/Source/LPCFarming/tilesets) | Ruộng, lúa, cây trồng, hàng rào, sậy, bờ nước; Chợ Mùa Gặt và rìa Đầm Sương | Tạo tile/animation cần thiết, kiểm tra chuyển tiếp nền |
| [Art manifest](/D:/Project/WerewolfGame_Team4_Game/Assets/Scenes/Thuong/Prototype/Art/Medieval/ImportManifest.json) | Quy chuẩn đang dùng: 48 PPU; nhân vật cell 64px, bốn hướng, 20 skin | Giữ tỷ lệ nhân vật và góc nhìn; kiểm tra art mới bằng nhân vật thật |
| [Minigame catalog](/D:/Project/WerewolfGame_Team4_Game/Assets/Scenes/Thuong/Prototype/Integration/Minigames/Resources/ThuongMinigameCatalog.asset) | Sáu minigame có thể đặt trong mọi map | Chuyển tọa độ trạm sang cấu hình map; giữ nguyên luật và TaskData |
| `Assets/Cainos/Pixel Art Platformer - Village Props`, bộ American Forest trong Chop Wood | Một số đồ vật rời có thể tận dụng sau kiểm tra | Đây là art platformer; không dùng nguyên bộ làm địa hình top-down |
| Farming Asset Pack của Fruit Collect và TopDown 2D RPG BE3 của Defender | Tham khảo gà, đồ nông trại, đạo cụ nhỏ | Khác nét/tỷ lệ; kiểm tra hình ảnh và notice riêng trước khi bổ sung vào map chính |

**Kết luận từ nguồn có sẵn:** Chợ và Nghĩa Trang có thể làm bản đầu mà không mua pack. Chưa thấy bộ đạo cụ đặc trưng cho tuồng như mặt nạ, hoa văn hoặc phông sân khấu Việt; cần vẽ bổ sung nếu muốn thể hiện rõ bản sắc này.

## 3. Concept map

Các kích thước dưới đây là mục tiêu blockout để thử, chưa phải số đã được playtest. Map làng hiện tại có vùng nền khoảng 28 × 24 ô; bắt đầu map mới ở quy mô tương tự, tránh kéo dài thời gian chạy khi ngày online hiện được cấu hình 45 giây.

| ID đề xuất | Concept và dấu hiệu nhận diện | Bố cục / nhiệm vụ nổi bật | Nguồn và ưu tiên |
| --- | --- | --- | --- |
| `village` | **Làng Trung Tâm**: quảng trường, giếng, sáu cụm nhà, đường đá, đuốc | Giữ bản hiện tại làm mốc đối chiếu; rà lại cả 10 trạm sau tích hợp minigame | Art hiện có; bắt buộc giữ được hành vi cũ |
| `harvest_market` | **Chợ Mùa Gặt**: mái quầy nhiều màu, ruộng vàng, kho hàng, sân hội | Một vòng chợ bên trong và vòng ruộng bên ngoài; hai đường nối; Thu hoạch ở ruộng, Trứng cạnh chuồng, Pha thuốc ở quầy thảo dược | Hypnobius + LPC Decorations/Farming có sẵn; **map mới số 1** |
| `lantern_cemetery` | **Nghĩa Trang Đèn Lồng**: mộ đá, cây già, sân tưởng niệm, nhà người trông coi | Đường vòng quanh các cụm mộ, hai cổng, đường cắt qua sân giữa; nhiệm vụ nông trại đặt ở khu nhà người trông coi | Sheet Decorations đã có; Old Town Cemetery là nâng cấp tùy chọn; **map mới số 2** |
| `mist_swamp` | **Đầm Sương**: nước tối, đảo cỏ, cầu gỗ, lều thảo dược, bệ đá | Vòng đường chính trên đất/cầu; tối thiểu hai tuyến qua mỗi cụm nhiệm vụ; không có trạm trên đảo chỉ có một lối | Dark Swamp + Farming sẵn có; **đợt 2** |
| `woodland_camp` | **Trại Rừng**: nhà gỗ, lều, bãi củi, suối, chốt canh | Vòng đường quanh trại và lối xuyên rừng; cây tạo khu vực nhận diện nhưng không che điểm tương tác | LPC lều/cây/woodpile hiện có + Rural Cabin; Northern Woodland tùy chọn; **đợt 2** |
| `abandoned_manor` | **Dinh Thự Bỏ Hoang**: đại sảnh, kho, sân trong, hành lang có đèn | Hai hành lang nối các phòng, cửa rộng; tường/mái phải có giải pháp che khuất nhân vật | Medieval Interior + đồ sẵn có; **đợt 3**, cần thêm công về camera/occlusion |

### Blockout cho hai map đầu

**Chợ Mùa Gặt:** phía bắc là ruộng và kho, phía nam là cổng và bãi xe, phía tây là chuồng/giếng, phía đông là quầy thuốc/chốt canh. Quảng trường giữa để trống cho 25 người xuất phát và trang trí sự kiện. Các quầy tạo từng cụm nhỏ, không quây kín trạm nhiệm vụ.

**Nghĩa Trang Đèn Lồng:** phía bắc là công trình tưởng niệm; phía nam là nhà người trông coi, kho và vườn nhỏ; đông/tây là các cụm mộ. Đường vòng và đường cắt giữa tạo tuyến đi thay thế. Mộ/cây dùng collider ở chân; tán và phần cao được sắp lớp theo Y. Không đặt 10 trạm chung quanh một sân nhỏ.

Tiêu chí blockout chung:

- Mỗi map có ít nhất 25 điểm spawn hợp lệ, không chồng collider/trạm; ưu tiên các cụm quanh quảng trường thay vì kéo thành một hàng dài.
- Mỗi cụm nhiệm vụ có hai hướng tiếp cận; chiều rộng đường chính thử từ 2–3 world unit rồi đo lại theo collider nhân vật.
- Với tốc độ online hiện tại 3 unit/giây, thử mục tiêu từ spawn đến trạm gần trong 3–5 giây, đến trạm xa trong 8–10 giây. Đo theo đường đi thực, không dùng khoảng cách đường thẳng để nghiệm thu.
- Một map đủ 10 trạm cho 10 TaskData hiện có; giữ 4 nhiệm vụ được chọn mỗi ngày. Không tự tăng số nhiệm vụ khi thêm map.
- Cửa/cầu không được biến thành điểm nghẽn bắt buộc; khu vực nguy hiểm chỉ là hình ảnh trong bản đầu, không tự gây chết hoặc làm chậm.

## 4. Sáu map sự kiện

[GameEventType](/D:/Project/WerewolfGame_Team4_Game/Assets/Scripts/Thuong/Events-thuongnh/EventType.cs) đã có đủ sáu enum dưới đây. [EventManager](/D:/Project/WerewolfGame_Team4_Game/Assets/Scripts/Thuong/Events-thuongnh/EventManager.cs) hiện chọn **một event, một ngày cho mỗi ván**, rồi ghi log; chưa có bộ áp hiệu ứng map hoặc đồng bộ event trong snapshot online.

Tất cả nội dung hình ảnh, âm thanh và khung thời gian trong bảng là **đề xuất mới**. Bản đầu giữ luật vai, vote, task, tốc độ di chuyển, thời lượng ngày và voice hiện có.

| Enum | Concept map sự kiện | Map phù hợp nhất | Thay đổi cần làm ở bản đầu | Khung kích hoạt đề xuất |
| --- | --- | --- | --- | --- |
| `BloodMoon` | **Đêm Trăng Máu** | Nghĩa Trang / Đầm Sương | Sắc đỏ trầm, than hồng thưa, bệ đá và đuốc đỏ; biểu tượng mặt trăng trên HUD | Báo tên vào ngày được chọn; hiệu ứng chính từ Night đến hết ResolveNight |
| `Fog` | **Làng Trong Sương** | Làng / Đầm Sương / Trại Rừng | Các dải sương chậm, nền xanh xám; giữ tên người, marker và chữ nhiệm vụ rõ | Day và Night của ngày được chọn; giảm/tắt lớp sương khi Discussion/Voting |
| `HarvestFestival` | **Hội Mùa Gặt** | Chợ / Làng | Cờ dây, bàn hàng, rơm, xe hàng, đèn; animation nhẹ ở quảng trường | Trang trí trong ngày event; âm thanh hội chỉ ở Day, giảm trong Night và các pha hội thoại |
| `ClearSky` | **Ngày Trời Quang** | Làng / Chợ / Trại Rừng | Nền ấm, trời sáng, tia nắng trang trí; không phủ sương sự kiện | Day của ngày được chọn; ban đêm trở về tint đêm bình thường |
| `GhostMonth` | **Đêm Đèn Tưởng Niệm** | Nghĩa Trang / Đầm Sương | Đèn lồng, lửa nhỏ, ánh sáng trôi, sân tưởng niệm; âm thanh nền rất nhẹ | Trang trí Day + Night; hiệu ứng ánh sáng rõ hơn vào Night |
| `TuongNight` | **Đêm Diễn Tuồng** | Làng / Chợ | Sân khấu nhỏ ngoài vùng đi chính, rèm/cờ/đèn sẵn có; vẽ thêm mặt nạ và phông tuồng riêng | Dựng sân khấu trong ngày event; hiệu ứng biểu diễn ở Night, giảm khi Discussion/Voting |

### Biến thể theo map thay vì làm sáu scene độc lập

| Map nền đợt 1 | BloodMoon / Fog / ClearSky | HarvestFestival | GhostMonth | TuongNight |
| --- | --- | --- | --- | --- |
| Làng | Màu/FX tại đường và quảng trường | Hội chợ quanh giếng | Đèn tưởng niệm ở sân chung | Sân khấu quảng trường |
| Chợ | Màu/FX tại đường và khu ruộng | Hội chính giữa các quầy | Dãy đèn ở cổng chợ | Sân khấu cạnh bãi xe |
| Nghĩa Trang | Màu/FX tại cụm mộ và sân giữa | Hội nhỏ ở khu nhà người trông coi | Biến thể chính tại sân tưởng niệm | Gánh hát ở sân ngoài cổng, không dựng trên mộ |

Như vậy đợt 1 có **3 map nền × 6 biến thể event**, nhưng dùng chung sáu bộ hiệu ứng và các anchor theo map. Đây là 18 tổ hợp trình bày, không phải 18 map địa hình độc lập.

Giữ pool sáu event và quy tắc một ngày event mỗi ván. Mỗi map phát hành phải hỗ trợ đủ sáu event bằng biến thể phù hợp; không âm thầm random lại vì map thiếu nội dung. Với map đang làm dở, dùng overlay chung đã được kiểm tra hoặc chưa cho chọn map đó trong lobby.

Quy ước lifecycle cần cài: ngày N bắt đầu tại `StartDay(N)`; các pha Night/Discussion/Voting sau đó vẫn thuộc ngày N; dọn event khi sang `StartDay(N+1)`, GameOver hoặc rời phòng. Event chỉ hiển thị trong các phase đã cấu hình. Áp snapshot lặp lại không được nhân đôi đèn, âm thanh hoặc hiệu ứng.

**Giới hạn thiết kế:** sương bản đầu là hiệu ứng hình ảnh, chưa phải luật giới hạn tầm nhìn. Nếu muốn người chơi thật sự không thấy người khác ngoài bán kính, phải có thiết kế và xử lý thông tin ở server; không chỉ che client bằng một lớp sương. BloodMoon cũng chưa tăng sức mạnh phe sói; GhostMonth chưa hồi sinh người chết. Các luật này là backlog riêng.

## 5. Asset mới đã tìm và kiểm tra

Giá tối thiểu bằng USD tại thời điểm khảo sát **19/10/2026**, chưa tính thuế. “Tùy trả tiền” là trang đang hiển thị *Name your own price*; kiểm tra lại trang khi tải. Các đánh giá về độ hợp art là nhận định sau khi đối chiếu phong cách, chưa phải kết quả import thử trong Unity.

| Ưu tiên | Asset / trang chính chủ có preview | Giá niêm yết | Dùng vào việc gì | Lưu ý tích hợp |
| --- | --- | --- | --- | --- |
| Cao | [Dark Swamp — Hypnobius](https://hypnobius.itch.io/dark-swamp-pack) | Tùy trả tiền | Đầm Sương; có nước động, cầu, cỏ 48px, bệ đá động | Cùng tác giả bộ làng; dùng PNG raw, tạo animation/tile Unity |
| Cao | [Old Town Cemetery — Hypnobius](https://hypnobius.itch.io/old-town-cemetery) | Tùy trả tiền | Nâng cấp Nghĩa Trang: cổng đá, lăng, hàng rào sắt, mộ, ánh sáng trôi | 48px; tùy chọn thay cho một phần đồ mộ LPC để đồng bộ nét |
| Cao | [Rural Cabin Exterior — Hypnobius](https://hypnobius.itch.io/rural-cabin-exterior) | Tùy trả tiền | Nhà gỗ Trại Rừng, chòi ven đầm | 48px; gói vẫn ghi In development, cần kiểm tra nội dung nhận được |
| Vừa | [Rustic Town Market Props — Hypnobius](https://hypnobius.itch.io/rustic-market-props) | **$3.99+** | Nâng cấp Chợ/Hội Mùa Gặt: quầy hàng, mái che, hàng hóa | Tùy chọn; source LPC hiện có đã đủ làm chợ bản đầu |
| Vừa | [Northern Woodland — Hypnobius](https://hypnobius.itch.io/northern-woodland-tileset) | **$5.99+** | Rừng, suối, vách đá, cầu thang cho Trại Rừng | Tác giả xác nhận 48px; bản v1.1 có cầu thang, vẫn cần collision Unity |
| Sau | [Medieval Interior — Hypnobius](https://hypnobius.itch.io/medieval-interior) | **$4.99+** | Dinh thự, kho, đại sảnh | 48px, phong cách Gothic; không phải bộ kiến trúc/sân khấu tuồng Việt |
| Dự phòng | [LPC Terrains — bluecarrot16](https://opengameart.org/content/lpc-terrains) | Miễn phí | Mở thêm nền bùn/nước/tuyết về sau | Trang liệt kê CC BY-SA 4.0 và 3.0; giữ đầy đủ `CREDITS-terrain.txt` và backlink |
| Dự phòng | [Pixel Art Top Down – Basic — Cainos](https://cainos.itch.io/pixel-art-top-down-basic) | Tùy trả tiền | Tàn tích đá, tượng, đường cho concept phụ | Bộ 32px; khác bộ Cainos platformer đang có trong repo; cần thử tỷ lệ/palette trước |

Nguồn gốc bộ nền đang dùng: [Medieval Village Exterior — Hypnobius](https://hypnobius.itch.io/medieval-village-exterior-tileset). Không cần nhập lại bộ này.

**Ngân sách gợi ý:** đợt 1 có thể dùng hoàn toàn asset hiện có. Đợt 2 có thể bắt đầu bằng Dark Swamp + Rural Cabin ở mức tải miễn phí nếu trang vẫn cho phép. Ba nâng cấp trả phí Market + Woodland + Interior có tổng giá tối thiểu đang thấy là **$14.97 trước thuế**, có thể chọn từng pack; đây chưa bao gồm công làm map hoặc art tuồng riêng.

### Giấy phép và cách nhập

Các pack Hypnobius nêu trên cho dùng trong game cá nhân/thương mại và chỉnh sửa, nhưng hạn chế phân phối lại file asset/pack. Giữ notice gốc, nguồn và phiên bản; kiểm tra quyền phân phối file nguồn trước khi đưa PNG lên repository công khai. Cainos cũng có hạn chế phân phối lại. Không gộp các pack này dưới giấy phép LPC.

Đồ LPC đang có dùng notice/credit trong [ATTRIBUTION.txt](/D:/Project/WerewolfGame_Team4_Game/Assets/Scenes/Thuong/Prototype/Art/Medieval/ATTRIBUTION.txt) và [CREDITS Decorations](/D:/Project/WerewolfGame_Team4_Game/Assets/Scenes/Thuong/Prototype/Art/Medieval/Source/LPCDecorations/CREDITS-decorations-medieval.txt). Khi cắt thêm sprite, giữ tác giả, nguồn, giấy phép của phần dẫn xuất. LPC Terrains mới yêu cầu ghi đủ tác giả theo file credit của chính gói đó. Điều kiện cụ thể xem các trang nguồn được dẫn ngay trong bảng.

Chỉ nhập PNG/notice cần thiết trước; tránh nhập demo scene, controller, input và manager của pack. Gói “RPG Maker ready” vẫn cần chuyển sang tile/animation Unity, không phải Unity prefab dùng ngay. Với bộ 48px dùng 48 PPU, filter Point và pivot phù hợp. Với bộ 32px, thử PPU/tỷ lệ riêng và kiểm tra mật độ pixel; không tự phóng ảnh 1.5 lần rồi trộn với mọi sprite hiện tại.

## 6. Các điểm kỹ thuật phải làm trước khi thêm địa hình

| Hiện trạng đã đọc trong code | Tác động khi thêm map | Hướng xử lý |
| --- | --- | --- |
| [SimulateServerMovement](/D:/Project/WerewolfGame_Team4_Game/Assets/Scripts/Thuong/Online/ThuongOnlineSession.cs:215) chỉ cộng vị trí và clamp vào bounds | Collider của nhà, mộ, nước hoặc cầu chưa chặn di chuyển online ở server | Thêm phép kiểm tra va chạm/đường đi ở server, dùng footprint nhân vật và layer địa hình riêng |
| [PrepareScene](/D:/Project/WerewolfGame_Team4_Game/Assets/Scripts/Thuong/Online/ThuongOnlineSession.cs:101) tìm trạm toàn scene, sắp theo tên task rồi tọa độ | Dời/nhân trạm có thể đổi index dùng trong task mask | Mỗi trạm có ID/slot tường minh, giống nhau trên host và client |
| [PrepareMinigames](/D:/Project/WerewolfGame_Team4_Game/Assets/Scripts/Thuong/Online/ThuongOnlineMinigames.cs:34) tạo bốn trạm theo tọa độ trong catalog | Map mới có thể sinh trạm giữa nhà/nước hoặc trùng trạm | Map chịu trách nhiệm bố trí đủ 10 trạm; catalog giữ mapping minigame và luật |
| `SpawnPosition` dùng một lưới quanh player template | 25 người có thể xuất phát vào vật cản ở layout mới | Dùng spawn anchor đã được kiểm tra trong map definition |
| [ThuongVillageMood](/D:/Project/WerewolfGame_Team4_Game/Assets/Scenes/Thuong/Prototype/Scripts/ThuongVillageMood.cs) lấy SpriteRenderer toàn cảnh một lần và đọc GameRoleManager | Có thể bỏ sót map mới, lấy nhầm sprite, hoặc hiển thị phase không đúng trên client | Bind vào MapRoot mới; lấy phase hiển thị từ session; quản lý màu gốc và hiệu ứng theo lớp |
| [Snapshot v4](/D:/Project/WerewolfGame_Team4_Game/Assets/Scripts/Thuong/Online/ThuongOnlineProtocol.cs:59) chưa có map/event | Client chưa đủ thông tin để dựng cùng map/event với host | Bổ sung phiên bản protocol, map revision và trạng thái event công khai |
| Session xử lý rời `OnlineSceneGamePlay` bằng shutdown | Load map bằng đổi active scene có thể đóng phòng | Instantiate prefab map trong scene online hiện tại |
| [Medieval installer](/D:/Project/WerewolfGame_Team4_Game/Assets/Scenes/Thuong/Prototype/Editor/ThuongMedievalSceneInstaller.cs) dựng lại subtree làng do nó quản lý | Chỉnh thủ công trong subtree có thể mất khi chạy Apply lại | Tạo thư mục/prefab map mới riêng; bổ sung công cụ map thay vì sửa generator cũ tùy tiện |

Collision online là điều kiện để nghiệm thu map có vật cản. Cần quét footprint theo bước di chuyển, xử lý trượt dọc tường và góc; chỉ kiểm tra điểm đích không đủ. Thử đi chéo, đẩy lâu vào góc, delta lớn và đường cầu. Bản đầu không thêm va chạm người-người nếu game hiện chưa có luật này.

## 7. Cấu trúc tích hợp đề xuất

Các tên lớp/thư mục dưới đây là đề xuất, chưa được tạo trong đợt khảo sát.

```text
D:/Project/WerewolfGame_Team4_Game/
  Assets/Scenes/Thuong/Maps/
    Art/                         sprite, tile, atlas, notice bổ sung
    Definitions/                 Village, HarvestMarket, LanternCemetery...
    Prefabs/                     một world prefab cho mỗi map
    Events/                      sáu bộ overlay + cấu hình theo map
  Assets/Scripts/Thuong/Maps/
    ThuongMapDefinition.cs
    ThuongMapRuntime.cs
    ThuongMapValidator.cs
  Assets/Scripts/Thuong/Events-thuongnh/
    ThuongEventPresentationCatalog.cs
    ThuongEventPresentation.cs
```

### Dữ liệu map và nhiệm vụ

`ThuongMapDefinition` dự kiến chứa:

- `mapId`, `contentRevision`, tên hiển thị, thumbnail, `worldPrefab`.
- Bounds camera và vùng di chuyển; tập collider/walkability của map.
- Tối thiểu 25 spawn anchor hợp lệ.
- 10 station binding: `stationId`, `maskSlot`, `TaskData`, anchor và prefab hình ảnh.
- Anchor trang trí event: cổng, sân giữa, sân khấu, mép đường, điểm đèn; các vùng không được che nhiệm vụ.

Giữ một bảng slot chung cho cả ba map đầu: mỗi TaskData có đúng một slot trong khoảng 0–15. Sáu task minigame đã có task ID trong catalog; bốn task tức thời còn lại cần bổ sung ID ổn định trong bảng binding chung. Không suy ID từ tên hiển thị hay tọa độ. Bản đầu giữ tổng 10 task; nếu vượt 16 trạm sau này phải nâng protocol/mask trước.

Ví dụ bố trí, không đổi luật nhiệm vụ:

| Task hiện có | Chợ Mùa Gặt | Nghĩa Trang Đèn Lồng |
| --- | --- | --- |
| Lấy nước | Giếng cạnh chuồng | Giếng nhà người trông coi |
| Thu hoạch — Harvest | Ruộng phía bắc | Vườn lúa nhỏ phía nam |
| Sửa rào | Cổng ruộng | Hàng rào sân ngoài |
| Gom củi — Wood | Bãi xe/chẻ củi | Bãi củi cạnh kho |
| Dọn kho | Kho nông sản | Kho dụng cụ |
| Thắp đèn | Cổng chợ | Lối vào sân tưởng niệm |
| Thu gom trứng — Eggs | Chuồng gà phía tây | Chuồng nhà người trông coi |
| Pha chế thuốc — Potion | Quầy thảo dược | Bàn thuốc trong khu sinh hoạt |
| Bảo vệ khu vực — Defender | Chốt cổng phía đông | Chốt ngoài cổng |
| Gỡ bẫy — Traps | Mép đường ra ruộng | Mép đường ngoài khu mộ |

### Trình tự load và đồng bộ

1. Host chọn map đã có trong build tại lobby. Gửi `mapId` + `contentRevision` cho client; client dùng registry nội bộ, không nhận đường dẫn prefab tùy ý từ mạng.
2. `ThuongMapRuntime` dọn map cũ trong trạng thái lobby, instantiate MapRoot, bind camera/spawn/trạm/collision/visual. Map prefab không chứa NetworkManager, GameRoleManager, EventSystem, camera hoặc AudioListener riêng.
3. Client xác nhận đúng map revision và đã bind xong. Host chỉ cho bắt đầu khi mọi thành viên cần thiết đã sẵn sàng; thiếu hoặc lệch nội dung thì báo lỗi rõ, không cho vào với layout khác.
4. Bắt đầu ván, server dùng map đã khóa để spawn và kiểm tra di chuyển/tương tác. Không thay topology trong Day/Night ở bản đầu.
5. EventManager giữ vai trò lập lịch trên server. Một bộ chuyển trạng thái dùng day/phase để xác định event đang hiển thị; client chỉ áp trạng thái server gửi.
6. Nâng snapshot từ v4 lên phiên bản mới khi thêm payload. Dự kiến có map ID/revision, event enum/revision, phase window hoặc mốc thời gian server; cập nhật cả writer, reader và kiểm tra tương thích build. Không cần gửi lịch tương lai nếu event dự kiến là bất ngờ.
7. `ThuongEventPresentation` áp/dọn overlay một cách idempotent. Khi đổi map ở lobby, GameOver hoặc Leave: trả màu nền về gốc, dừng âm thanh, xóa overlay và bỏ đăng ký callback.

Giữ minigame trong local physics scene/RenderTexture như hiện tại. Collision map không truy vấn nhầm collider minigame; tint/sương map không tác động lên cửa sổ minigame hoặc UI. Một camera/audio listener chính tiếp tục phục vụ phiên online.

## 8. Backlog triển khai theo thứ tự

| Mã | Việc cần làm | Kết quả nghiệm thu | Phụ thuộc |
| --- | --- | --- | --- |
| MAP-01 | Lập danh mục sprite sẵn có; cắt mộ, đèn, mái quầy, rèm, lều; lưu notice và thiết lập import | Một bảng mẫu art đặt cạnh nhân vật/nhà hiện tại, có prefab và pivot đúng | Không |
| MAP-02 | Tạo map definition/runtime/validator; đóng gói Làng hiện tại thành map đầu | Làng load qua cấu hình mới, đủ 10 trạm và 25 spawn, hành vi cũ còn chạy | MAP-01 |
| MAP-03 | Collision server, bounds theo map, kiểm tra reachability từ spawn tới mọi trạm | Không đi xuyên nhà/nước; không có trạm bị khóa bởi địa hình | MAP-02 |
| MAP-04 | Lobby chọn map, map-ready acknowledgement, protocol mới và bind trạm ổn định | Host/client dùng cùng revision và slot; lệch nội dung được chặn trước match | MAP-02 |
| MAP-05 | Dựng và chơi thử Chợ Mùa Gặt, đo đường đi rồi hoàn thiện art | Layout mới đủ 10 nhiệm vụ, không trùng trạm, đọc được khi nhiều người đứng gần | MAP-03, MAP-04 |
| MAP-06 | Dựng Nghĩa Trang Đèn Lồng từ source hiện có; cân nhắc pack nâng cấp | Hai cổng/tuyến thay thế, mộ/đèn không che interaction hoặc tên | MAP-03, MAP-04 |
| EVT-01 | Tách state event công khai khỏi phần trình bày; nối day/phase/snapshot; sửa mood scope | Host/client cùng event và thời điểm; qua ngày dọn sạch | MAP-04 |
| EVT-02 | Làm BloodMoon, Fog, ClearSky trước | Ba overlay chạy trên cả ba map; UI và minigame rõ | MAP-05, MAP-06, EVT-01 |
| EVT-03 | Làm HarvestFestival, GhostMonth, TuongNight; bổ sung art tuồng cần thiết | Đủ 18 tổ hợp; audio không lấn voice; sân khấu ngoài vùng đi chính | EVT-02 |
| QA-01 | Kiểm tra asset/build, multiplayer thật, thời gian nhiệm vụ và hiệu năng | Hoàn tất checklist ở phần 9, lưu ảnh và log theo map/event | Các mục trên |
| MAP-07 | Thêm Dark Swamp/Rural Cabin, dựng Đầm Sương/Trại Rừng | Tái dùng hệ thống map/event, không thêm manager hoặc luật đặc biệt | Đợt 1 đã nghiệm thu |

**Mốc bàn giao:** A = Làng qua hệ thống map mới + collision + đồng bộ; B = hai map mới chơi đủ nhiệm vụ; C = sáu event + QA. Có thể phát hành mốc B trước trong khi hoàn thiện sân khấu tuồng. Công art tuồng và kiểm thử nhiều máy cần được tính riêng khi chia người; số pack ít không đồng nghĩa ít công dựng map.

## 9. Checklist nghiệm thu

### Nội dung và đường đi

- [ ] Ba map đầu có ID/revision duy nhất, thumbnail, bounds, đủ 25 spawn và đúng 10 trạm.
- [ ] Không task ID/slot trùng, không thiếu task, không vượt mask 16 bit; mọi trạm có vị trí tiếp cận hợp lệ.
- [ ] Đo đường đi trên collision thực; hoàn thành được sáu minigame và bốn task tức thời trong flow ngày hiện tại.
- [ ] Thử đủ bốn task được chọn mỗi ngày, trạm đã hoàn thành/đang có người dùng, range và hủy khi hết ngày/chết.
- [ ] Nhà, cây, cổng, mộ không gây sai thứ tự Y; marker, tên và chữ vẫn đọc rõ ở Night/Fog/BloodMoon.

### Online và lifecycle

- [ ] Host + ít nhất một client độc lập kiểm tra map-ready, revision lệch và bắt đầu ván; không chỉ test nhiều client trong cùng Editor.
- [ ] Server chặn xuyên tường/nước, đi chéo và góc; client không tự quyết vị trí hợp lệ hay kết quả task.
- [ ] Mỗi event đúng ngày/phase; qua ngày, GameOver, Leave, đổi map ở lobby không còn overlay, tint hoặc âm thanh cũ.
- [ ] Snapshot lặp/đến trễ không tạo overlay trùng; người vào lobby sau nhận đúng map được chọn. Nếu có hỗ trợ reconnect thì xác thực lại map/event trước khi nhận điều khiển.
- [ ] Không lộ vai bí mật từ skin, marker, event, map snapshot hoặc FX riêng theo phe.
- [ ] Flow phân vai, ngày/đêm, kỹ năng, vote, kết quả và Relay/voice hiện tại vẫn chạy; active scene online giữ nguyên.
- [ ] Sáu minigame mở/đóng bình thường trên mọi map; không thêm EventSystem, AudioListener, callback hoặc local scene bị rò.

### Hiệu năng và hồ sơ

- [ ] Kiểm tra 25 avatar; đo frame time, bộ nhớ và overdraw ở map/event nặng nhất, so với Làng trước thay đổi trên cùng máy.
- [ ] Đặt mục tiêu 60 FPS trên máy kiểm thử được chỉ định; nếu chưa có thiết bị mục tiêu thì lưu số đo, chưa kết luận đạt cho mọi máy.
- [ ] Thử chỉnh cửa sổ/tỷ lệ màn hình; khói/sương không phủ UI và âm thanh nền có mức đủ nhỏ để nghe voice.
- [ ] Player build chứa prefab/map/catalog/notice cần thiết, không dựa vào AssetDatabase hoặc demo scene của pack.
- [ ] Có ảnh QA Day/Night và sáu event, log test, credit cập nhật và danh sách hạn chế còn lại.

Đợt khảo sát này chưa chạy các kiểm thử trên vì chưa thay đổi runtime hoặc tạo map. Checklist là điều kiện cho các đợt triển khai kế tiếp, không phải kết quả đã đạt.

## 10. Quyết định mặc định để bắt đầu

1. Làm hai map mới **Chợ Mùa Gặt → Nghĩa Trang Đèn Lồng** trước, dùng art có sẵn.
2. Dựng sáu event bằng overlay trên map nền, giữ quy tắc **một ngày event mỗi ván** và luật gameplay hiện tại.
3. Ưu tiên **collision online + cấu hình map/trạm + đồng bộ event** trước khi trang trí chi tiết.
4. Asset bổ sung ưu tiên **Dark Swamp → Rural Cabin → Old Town Cemetery**; pack trả phí là nâng cấp tùy chọn.
5. Đưa **Đầm Sương → Trại Rừng → Dinh Thự** vào backlog sau đợt đầu; luật sương giới hạn tầm nhìn, buff Trăng Máu hoặc hồi sinh thuộc một plan cân bằng riêng.
