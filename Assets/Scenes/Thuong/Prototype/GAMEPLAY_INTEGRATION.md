# Gameplay prototype + Phase 1–4

Scene chính vẫn là `ThuongGameplayPrototype.unity`. Nhấn Play, đọc thẻ role và nhấn OK. Lobby size, random role, 6 điểm nhiệm vụ, 4 nhiệm vụ/ngày, timer, tổng tiến độ, F1–F12/PageUp/PageDown, lịch event và phím R được giữ.

Chỉ `GameRoleManager` điều phối trận này. Luồng: RoleReveal → Day/Task → Night → ResolveNight → Discussion → Voting → ResolveVote/DayResolution → Day tiếp theo. `ThuongLogicMatchController` chỉ ở scene test riêng, không bật `manualLogicPrototype` trên scene chính.

Trong Systems, `StatusEffectSystem` và `AbilitySystem` được nối vào controller cũ. Tick hiệu ứng cũ trước xử lý đêm; protection xử lý trước kill. Phiếu và bẫy Hunter xử lý trước expiry tại DayResolution, rồi mới kiểm tra thắng sau toàn bộ lượt.

`Integration/GameplayRoles.asset` ánh xạ RoleType sang role asset. Ba role nền đã chuyển: Villager, DogSpirit và VillageGuardian. Không thay random role bằng đội hình test 4 người. Các role đặc biệt vẫn dùng class/UI cũ khi chưa có asset, hoặc khi asset bật Use Legacy Ability. Đừng gán một kỹ năng generic cho role có luật riêng nếu chưa chuyển luật đó.

Cả phe sói có một mục tiêu chung mỗi đêm, lựa chọn cuối thay lựa chọn trước (kể cả Ogre/Xà Tinh). Phase 5 cấu hình DogSpirit chỉ chọn người sống khác phe và cấm tự chọn. Bảo Vệ không tự chọn mình và không chọn cùng người hai đêm liên tiếp. Cấu hình Doctor/Wolf ở scene test vẫn độc lập: Doctor được tự bảo vệ. Thắng bằng 100% nhiệm vụ và giới hạn 7 ngày vẫn là mặc định; không tự bật Use Elimination Win.

UI role/skill cũ ưu tiên Display Name, Description, Ability Name, Portrait, Ability Icon trong role asset; nếu thiếu vẫn dùng nội dung/ảnh fallback cũ. Roster hiện hiệu ứng và nguyên nhân chết. Bảng họp giữ mẫu nút cũ nhưng có scroll để bỏ phiếu cho toàn bộ lobby, không chỉ vài hàng đã đặt trong scene. Dòng dưới phase cue hiển thị kết quả kỹ năng/bảo vệ/chết/phiếu.

## Thay asset sau này

- `Integration/CharacterAppearance.asset`: thay Sprite, Tint, Visual Scale, Animator Controller. Child Visual của `Integration/Prefabs/GameplayPlayer.prefab` chỉ hiển thị; root giữ PlayerMovement, PlayerID, PrototypeControls, Rigidbody2D và collider. Scene Player đầu tiên cũng dùng cùng Appearance. Animator dùng các parameter hiện có: `isWalking` (bool), `LastInputX`, `LastInputY` (float). Chỉnh collider theo kích thước ảnh mới nếu cần, không thay root bằng một object chỉ có sprite.
- `Integration/Prefabs/TaskStation.prefab`: mẫu điểm nhiệm vụ giữ TaskStation, marker và nhãn. Đổi sprite/material/nhãn hoặc nhân prefab rồi gán TaskData; sáu điểm và task reference trong scene hiện tại không bị thay.
- Role assets trong `Integration/Roles`: gán portrait/icon mà không sửa luật chơi. AbilityType/target/duration là cấu hình gameplay, không phải skin.
- Map, HUD và các điểm nhiệm vụ cũ vẫn ở scene, không được dựng lại hoặc xóa. Các cơ chế/phase mới chưa có vẫn cần triển khai trước khi có thể chỉ thay art. Ghép asset không thay thế kiểm thử animation, collider, scale, sorting và UI.

## Kiểm thử và phục hồi

Menu Tools → Thuong → Check Integrated Gameplay kiểm tra wiring, random role, hai thứ tự bảo vệ/tấn công, pack target, luật Guardian, effect/vote expiry, UI, nhiệm vụ và Hunter/Fox cũ. Batch `ThuongGameplayIntegrationChecks.InstallAndCheck` cài phần ghép, chạy kiểm tra này, click regression rồi chạy toàn bộ gameplay Play Mode cũ. Bộ `ThuongLogicPhaseChecks.Run` vẫn kiểm tra scene Phase 1–4 riêng.

Installer chỉ nâng cấp scene, không gọi builder dựng lại. Trước khi ghi scene nó tạo bản sao lưu có timestamp tại `Logs/ThuongGameplayPrototype-before-integration-*.unity`. Chạy lại installer không ghi đè các role assets/appearance/prefabs đã có. Không có commit/push tự động.

Phase 5 dùng `TargetValidator` trong `AbilityValidator.cs` cho UI và API. UI cập nhật mục tiêu sống/chết và phe trong lúc panel đang mở. `AbilitySystem` chỉ nhận instance người chơi đã đăng ký; `NightManager` kiểm tra lại mục tiêu lúc resolve để không tấn công người vừa đổi phe. Các role đặc biệt chưa có role asset tiếp tục dùng luật legacy.
Menu `Tools → Thuong → Check Phase 5 (Targets)` kiểm tra ma trận phe, self/dead target, request giả, UI và lý do từ chối. Batch `ThuongPhaseFiveChecks.InstallAndCheck` chạy thêm kiểm tra tích hợp, click vote và Play Mode của scene gameplay; đạt khi có `THUONG_PHASE5_EDIT_PASSED`, `THUONG_PHASE5_PLAY_PASSED`, `THUONG_PROTOTYPE_PLAYMODE_PASSED`.
