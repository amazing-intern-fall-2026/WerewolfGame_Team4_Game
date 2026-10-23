# Kết quả triển khai kế hoạch logic Ma Sói

Ngày đối chiếu: 22/10/2026. Nguồn yêu cầu: `Ke_hoach_phat_trien_he_thong_logic_game_Ma_Soi.docx` do người dùng cung cấp.

## Project trước khi tiếp tục

Mốc công việc gần nhất là Phase 5. Phase 1–4 đã có nền tảng và scene thử; Phase 5 đã có validator dùng chung và bộ kiểm tra. Tuy nhiên, giao diện chọn mục tiêu hiện tại đã hồi quy: hiển thị mục tiêu sai phe, bỏ mục tiêu tự bảo vệ hợp lệ và chưa cập nhật danh sách đang mở theo trạng thái mới. Danh sách sống/chết của scene logic cũng chưa được dựng lại sau khi bỏ overlay cũ.

Phase 6–13 đã có nhiều đoạn mã nền, nên không thể coi tất cả là chưa làm. Các phần còn thiếu chủ yếu là nối đúng đường thực thi, khóa UI, quản lý hiệu lực của các vai cũ, xử lý chết theo cả chuỗi, hoàn thiện điều kiện thắng và màn hình kết quả.

## Kết quả theo từng phase

| Phase | Trước khi tiếp tục | Kết quả triển khai |
| --- | --- | --- |
| 1 Trạng thái sống chết | Có dữ liệu chung; roster scene logic trống | Khôi phục danh sách Sống/Chết, quyền vote và hiệu ứng; reset sạch dữ liệu chết |
| 2 Chết cơ bản | Có DeathResolver | Ghi thêm nguồn gây chết; API thay đổi sang chết đi qua resolver khi hệ thống tồn tại |
| 3 Hiệu ứng | Có thêm, tick và refresh | Bổ sung xóa theo loại/nguồn, thanh tẩy, cập nhật cờ Cursed, dọn hiệu ứng khi chết |
| 4 Protected | Có cơ chế chặn tấn công | Giữ luật chặn theo nguyên nhân, dùng một lớp bảo vệ cho một đòn và hết hạn theo cấu hình |
| 5 Mục tiêu | API có luật; UI bị hồi quy | UI/API cùng luật; tự bảo vệ, phe, sống/chết và cập nhật mục tiêu đang mở |
| 6 Điều kiện kỹ năng | Kiểm tra chưa thống nhất | Chặn sai phase chính xác, người chết, Silenced, dùng lặp và dữ liệu giả; khóa UI; hỗ trợ kỹ năng cấu hình dùng ngoài ban đêm |
| 7 Cannot Vote | Có kiểm tra nền | Từ chối gửi phiếu, loại phiếu của người bị khóa trước khi kiểm phiếu, hiển thị lý do; hết hạn sau kiểm phiếu |
| 8 Extra Vote | Có trọng số nền | Cộng hiệu ứng vào phiếu cơ bản lúc kiểm phiếu; không sửa phiếu gốc; hiển thị số phiếu cuối cùng |
| 9 Silenced | Có validator nền | Khóa cả kỹ năng cấu hình và vai cũ; bùa hại Pháp Sư dùng hiệu ứng rõ thời hạn |
| 10 Cursed | Có nhánh hết hạn chung; vai cũ chỉ dùng cờ | Vai Cursed dùng hiệu ứng có bộ đếm; hết hạn gửi DeathRequest nguyên nhân Curse, giữ nguồn và không tự đổi trạng thái chết |
| 11 Chết đầy đủ | Có các nhánh riêng; có nguy cơ xét thắng giữa chuỗi | Gom chuỗi chết thành một lần hoàn tất; bảo vệ/passive/nguyên nhân cùng resolver; Bạch Khuyển đồng bộ phe trong PlayerData |
| 12 Điều kiện thắng | Luật offline đơn giản hơn online | Dùng bộ luật chung đã có, chặn dân thắng khi còn Sát Nhân/Bạch Khuyển đối địch; hỗ trợ các vai đặc biệt, hòa và asset luật thắng trung lập; online chỉ xét lại khi có thay đổi |
| 13 Kết thúc và kết quả | Chủ yếu có nhãn phe thắng | Dừng timer/kỹ năng/chuyển phase; chụp kết quả bất biến; hiện vai/phe/sống chết/nguyên nhân; nút chơi lại reset trận; tránh overlay cũ ghi đè |

## Quy tắc hiệu lực

- Cùng một nguồn làm mới lớp hiệu ứng hiện có; khác nguồn giữ các lớp độc lập.
- Cannot Vote và Extra Vote trong preset hết hạn tại DayResolution, sau khi tính kết quả vote.
- Preset TestSilenced giữ hai lần Discussion, đủ để khóa kỹ năng trong đêm kế tiếp. Silenced không thay đổi quyền chat/voice trong phạm vi công việc này.
- Vai Cursed cũ đếm hai lần Discussion kể từ đêm dùng: sống qua lần thứ nhất, chết khi bắt đầu lần thứ hai. Preset TestCursed dùng hai lần ResolveNight sau đêm áp dụng. Đây là hai cấu hình thời hạn khác nhau của cùng hệ thống.
- Protected mặc định chặn Monster, Killer và Ability; không chặn Vote, Curse, Lover hay Trap. Có thể chỉnh danh sách nguyên nhân được chặn trên DeathResolver.
- Chết liên hoàn và các đòn trong cùng lần giải quyết hoàn tất trước khi kiểm tra thắng. Cả ván không còn ai sống thì hòa. Thao tác chết mới sau Game Over bị từ chối.
- Ngoại lệ Thợ Săn đã chết đặt bẫy trong Discussion được giữ theo luật hiện có của project.

## Cách kiểm tra trong Unity

1. Mở `Assets/Scenes/Thuong/Prototype/LogicPhase1To4/WerewolfLogicPrototype.unity`, Play và bấm Start Match. Tên thư mục và menu tạo scene cũ được giữ để không phá đường dẫn.
2. Gán một trong bốn asset `Roles/TestCannotVote.asset`, `TestExtraVote.asset`, `TestSilenced.asset`, `TestCursed.asset` vào `roleDefinition` của một người trước khi bắt đầu để thử cơ chế tương ứng. Các preset dùng vai Dân Làng làm nền và không thay bộ 17 vai ngẫu nhiên.
3. Bộ kiểm tra tự động: **Tools → Thuong → Check Logic Plan Phases 6-13**. Điểm gọi batch: `ThuongLogicPlanChecks.Run`. Báo cáo tại `Logs/LogicPlan/checks.txt`.
4. Scene `ThuongGameplayPrototype.unity` tiếp tục dùng vòng tự động và HUD sẵn có; màn hình kết quả đầy đủ được dựng khi chạy, không cần kéo reference mới trong Inspector.
5. Muốn thêm luật thắng cho vai trung lập, tạo asset qua **Create → Thuong → Win Rules → Neutral Survival** rồi gán vào `neutralWinRule` của role definition. Có thể cấu hình số người khác còn sống và yêu cầu diệt hết phe tấn công đối địch.

## Kiểm chứng

- Toàn bộ mã runtime và editor của project được biên dịch bằng bộ tham chiếu hiện có của Unity 6000.6.0f1.
- Bộ kiểm tra logic mới chạy trong Unity: **96 kiểm tra đạt**, bao gồm các nguyên nhân chết, khóa phiếu, phiếu tăng thêm, lời nguyền, passive, điều kiện thắng, lobby hai người, UI và tương thích luồng cũ chưa bật tích hợp.
- Vòng Play Mode trong project kiểm thử tách biệt: **14 kiểm tra đạt**, thao tác nút thực tế để dùng kỹ năng, bảo vệ, vote, kết thúc, chơi lại và chạy lời nguyền.
- Có kiểm tra ảnh màn hình kết quả; ảnh và log thử nghiệm lưu trong `Logs/LogicPlan`.

Kiểm thử dùng bản biên dịch của mã gameplay trong một project Unity tạm vì project chính đang mở. Chưa chạy lại một trận Relay gồm nhiều máy, build executable hay kiểm thử voice. Theo yêu cầu tiếp theo, các phần thích ứng với vai của Nhật và manager Game Over của Lợi được chuyển về lớp tích hợp của Thương; mã nguồn của hai phần đó đã được khôi phục. Chi tiết tích hợp scene online nằm trong `Docs/ThuongOnlineLogicIntegration.md`.

Project tạm chỉ mang các thư viện cần cho logic và UI. Log có lỗi khởi tạo dịch vụ đăng nhập do thiếu bộ cấu hình dịch vụ đầy đủ và lỗi chỉ mục tìm kiếm của Unity Editor; hai nhóm này nằm ngoài các kiểm tra gameplay đã đạt. Chưa xác nhận luồng đăng nhập qua các log này.

Các thay đổi scene, font, cấu hình project và NetworkPhaseSync đã tồn tại trước phiên làm việc được giữ nguyên. Không tạo commit hoặc đẩy mã lên máy chủ.
