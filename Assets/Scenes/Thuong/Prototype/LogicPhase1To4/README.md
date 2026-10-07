# Phase 1–4 logic prototype của Thương

Mở `WerewolfLogicPrototype.unity` trong thư mục này và nhấn Play, sau đó Start Match.
Nếu scene chưa được tạo, dùng menu Tools → Thuong → Create Logic Phase 1-4 Scene.

- Phase 1: `PlayerData` là dữ liệu chung hiện có, thêm role asset, death record, effect và event. `PlayerRosterUI` hiển thị Alive/Dead, role ẩn và effect theo nguồn/thời hạn.
- Phase 2: `DeathResolver.TryKill(DeathRequest)` xử lý mọi yêu cầu chết, phát `DeathResolved` và giữ các overload `TryKillPlayer` cho code hiện tại.
- Phase 3: `StatusEffectSystem` cộng effect khác nguồn, refresh cùng nguồn, tick đúng phase, thông báo thay đổi và gửi curse hết hạn qua `DeathResolver`.
- Phase 4: `AbilitySystem` gửi hành động vào `NightManager`. `ThuongLogicMatchController` điều phối bằng các manager sẵn có, tick effect cũ trước action mới, resolve Protected trước Kill và kiểm tra thắng sau cả lượt.

`VoteManager` giữ lựa chọn theo Player ID và tính sức nặng ở lúc resolve, trước khi effect hết hạn tại DayResolution. `votePower` của role cũ vẫn được giữ làm trọng số cơ bản; effect chỉ được cộng tạm thời, không sửa dữ liệu gốc. Các role/UI chi tiết Cannot Vote, Extra Vote và các phase sau chưa được tạo.
Mỗi lượt bỏ phiếu chỉ resolve một lần; gọi lại giữ nguyên kết quả và không loại thêm người.

Prototype xử lý cả Fast Enter Play Mode và script reload: bind lại reference hệ thống, nối lại button/event khi OnEnable và loại bỏ hàng UI cũ trước khi rebuild roster.

Doctor P01 có Protect (cho tự bảo vệ), Wolf P02 có Kill (chỉ chọn phe khác), P03/P04 là Villager. Role assets nằm trong `Roles`.
Chọn P01 rồi P03 và nhấn Dùng kỹ năng; chọn P02 rồi P03 và gửi tiếp. Next Phase vào NightResolution: P03 sống, nhật ký ghi bảo vệ. Thử lại khi gửi Wolf trước Doctor.
Để kiểm tra expiry, bảo vệ P04 nhưng không tấn công P04. Sau NightResolution, effect còn trên roster; sang DayDiscussion thì hết.
Start Match/Bắt đầu lại tạo lại dữ liệu trận: bỏ action chờ, vote, effect, death record và lượt sử dụng.

Luồng: Setup → Night → NightResolution → DayDiscussion → Voting → DayResolution → Night vòng tiếp theo.
Trong code cũ của Thương, `ResolveNight` tương ứng `NightResolution`, `Discussion` tương ứng `DayDiscussion`; `DeathCause.Monster` tương ứng WolfAttack, `Killer` là nguyên nhân riêng đang có, `Ability`/`Curse`/`Special` được thêm ở cuối enum để giữ ID cũ.

Hệ thống và references được lưu trong scene. Button callbacks được nối tại `ThuongLogicPrototypeUI.Initialize` để không cần kéo lại trong Inspector.
Scene này chạy offline bằng nút chuyển phase; `ThuongGameplayPrototype` đã ghép logic mới vào flow tự động, vẫn giữ nhiệm vụ và role ngẫu nhiên. Xem `../GAMEPLAY_INTEGRATION.md` để biết các điểm nối và cách thay asset.
Chưa triển khai các role/checklist riêng của Phase 5–13; validator và win nền là dependency được tài liệu yêu cầu ngay ở Phase 4.

Kiểm thử: menu Tools → Thuong → Check Logic Phase 1-4. Batch Play Mode entrypoint: `ThuongLogicPhaseChecks.Run`.
Kiểm tra cũ cho Phase 1 được chạy trước. Các kiểm tra mới bao gồm nguồn effect, duration, chết lặp, protection, hai thứ tự Doctor/Wolf, bỏ phiếu, GameOver nền và reset trận; sau đó chạy lại UI trong Play Mode và tạo ảnh kiểm tra ở Logs.
