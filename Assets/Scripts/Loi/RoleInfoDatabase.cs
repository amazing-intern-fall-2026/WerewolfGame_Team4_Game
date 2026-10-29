using System;
using UnityEngine;

public class RoleInfoData
{
    public RoleType Role { get; set; }
    public string DisplayName { get; set; }
    public string FactionName { get; set; }
    public FactionType Faction { get; set; }
    public string Description { get; set; }
    public string SkillName { get; set; }
    public string SkillDescription { get; set; }
    public string Restrictions { get; set; }
    public bool IsSecret { get; set; }
}

public static class RoleInfoDatabase
{
    public static RoleInfoData GetRoleInfo(RoleType role)
    {
        switch (role)
        {
            case RoleType.Villager:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Dân làng",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Người dân bình thường trong làng. Ban ngày tham gia thảo luận và bỏ phiếu tìm ra Ma sói ẩn nấp để bảo vệ sự bình yên của ngôi làng.",
                    SkillName = "Biểu quyết ban ngày",
                    SkillDescription = "Tham gia bỏ phiếu treo cổ người chơi bị nghi ngờ vào giai đoạn Bỏ phiếu.",
                    Restrictions = "Không có kỹ năng đặc biệt vào ban đêm."
                };

            case RoleType.DogSpirit:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Ma sói (Khuyển tinh)",
                    FactionName = "Phe Quái vật (Ma sói)",
                    Faction = FactionType.Monster,
                    Description = "Kẻ săn mồi bóng đêm hung tợn. Trà trộn cùng dân làng vào ban ngày và cùng bầy sói săn lùng con mồi vào ban đêm.",
                    SkillName = "Cắn xé ban đêm",
                    SkillDescription = "Chọn một người chơi còn sống để tấn công và tiêu diệt vào ban đêm.",
                    Restrictions = "Chỉ thực hiện trong giai đoạn Ban đêm. Mỗi đêm chọn 1 mục tiêu."
                };

            case RoleType.Seer:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Tiên tri",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Nhà tiên tri có khả năng thấu thị tâm linh, nhìn thấu chân tướng của những kẻ đội lốt dân thường.",
                    SkillName = "Soi danh tính",
                    SkillDescription = "Mỗi đêm chọn một người chơi để soi xem người đó có thuộc phe Ma sói hay không.",
                    Restrictions = "Chỉ soi được 1 người mỗi đêm vào giai đoạn Ban đêm."
                };

            case RoleType.VillageGuardian:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Bảo vệ",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Vị hộ vệ dũng cảm âm thầm tuần tra trong đêm để che chở cho dân làng khỏi nanh vuốt loài sói.",
                    SkillName = "Khiên hộ vệ",
                    SkillDescription = "Chọn một người chơi (có thể chọn chính mình) để bảo vệ khỏi đòn tấn công ban đêm.",
                    Restrictions = "Không thể bảo vệ cùng một người chơi trong hai đêm liên tiếp."
                };

            case RoleType.Mayor:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Thị trưởng",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Vị thủ lĩnh được dân làng kính trọng, có tiếng nói quyết định trong các cuộc biểu quyết của làng.",
                    SkillName = "Lá phiếu quyền lực",
                    SkillDescription = "Phiếu bầu của Thị trưởng có giá trị tương đương 2 phiếu trong giai đoạn Bỏ phiếu.",
                    Restrictions = "Hiệu lực tự động trong giai đoạn Bỏ phiếu ban ngày."
                };

            case RoleType.Idiot:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Kẻ ngốc",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Kẻ khờ khạo vô hại của làng. Dù bị nghi ngờ nhưng sự ngây thơ đã cứu sống hắn.",
                    SkillName = "Miễn tử khi bị treo",
                    SkillDescription = "Nếu bị dân làng bỏ phiếu treo cổ, thân phận Kẻ ngốc sẽ được công khai và được tha chết.",
                    Restrictions = "Sau khi lộ diện sẽ mất quyền bỏ phiếu; chỉ kích hoạt 1 lần khi bị treo cổ."
                };

            case RoleType.WhiteHound:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Bạch khuyển",
                    FactionName = "Dân làng (Ban đầu) / Phe DogSpirit (Sau khi bị cắn)",
                    Faction = FactionType.Villager,
                    Description = "Linh thú ngụy trang trong lốt dân lành. Khi bị DogSpirit cắn trong đêm, sang ngày hôm sau sẽ thức tỉnh và chuyển qua phe DogSpirit.",
                    SkillName = "Thức tỉnh & Tấn công ban đêm",
                    SkillDescription = "Khi chưa bị cắn, tính là Dân làng. Sau khi bị DogSpirit cắn, hôm sau chuyển qua phe DogSpirit và mở khóa chức năng tấn công ban đêm.",
                    Restrictions = "Chức năng tấn công ban đêm được mở từ ngày hôm sau khi bị DogSpirit cắn."
                };

            case RoleType.SerpentSpirit:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Xà tinh",
                    FactionName = "Phe Quái vật (Ma sói)",
                    Faction = FactionType.Monster,
                    Description = "Loài quái vật xảo quyệt tích tụ độc khí trong bóng tối, chờ thời cơ để đoạt mạng con mồi.",
                    SkillName = "Đoạt mạng bóng đêm",
                    SkillDescription = "Chọn một người chơi để hạ gục vào ban đêm sau khi đã đủ sức mạnh.",
                    Restrictions = "Cần tích lũy tà khí trong 2 đêm đầu tiên, bắt đầu tấn công từ đêm thứ 3."
                };

            case RoleType.Ogre:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Yêu tinh (Ogre)",
                    FactionName = "Phe Quái vật (Ma sói)",
                    Faction = FactionType.Monster,
                    Description = "Quái vật hung tợn với sức mạnh tàn phá khủng khiếp, đồng minh đắc lực của bầy sói.",
                    SkillName = "Nghiền nát mục tiêu",
                    SkillDescription = "Chọn và tiêu diệt một người chơi còn sống trong đêm.",
                    Restrictions = "Chỉ sử dụng vào ban đêm lên người chơi còn sống."
                };

            case RoleType.Hunter:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Thợ săn",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Tay săn lão luyện với khẩu súng và những chiếc bẫy chết người luôn sẵn sàng trả thù.",
                    SkillName = "Bẫy báo thù",
                    SkillDescription = "Khi bị chết (bị sói cắn hoặc bị treo cổ), Thợ săn có thể đặt bẫy kéo theo một người chơi khác cùng chết.",
                    Restrictions = "Chỉ kích hoạt sau khi Thợ săn bị loại; chọn mục tiêu đặt bẫy trong giai đoạn Thảo luận."
                };

            case RoleType.Shaman:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Pháp sư",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Phù thủy huyền bí nắm giữ hai loại bùa chú cổ xưa: bùa hộ mệnh cứu người và bùa hại phong ấn.",
                    SkillName = "Bùa hộ mệnh / Bùa phong ấn",
                    SkillDescription = "Dùng bùa lợi để bảo vệ một người chơi khỏi cái chết, hoặc bùa hại để làm câm lặng (ngăn nói và bỏ phiếu).",
                    Restrictions = "Mỗi loại bùa (Bùa lợi / Bùa hại) chỉ được sử dụng duy nhất một lần trong toàn bộ ván chơi."
                };

            case RoleType.WeaverOfFate:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Dệt duyên",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Thần kết nối tơ duyên, se chỉ đỏ gắn kết sinh mệnh của hai con người.",
                    SkillName = "Se duyên sinh tử",
                    SkillDescription = "Vào ban đêm, chọn hai người chơi để kết đôi thành Cặp đôi (Lovers). Nếu một người chết, người kia cũng chết theo.",
                    Restrictions = "Chỉ sử dụng duy nhất một lần trong cả ván đấu vào ban đêm."
                };

            case RoleType.Cursed:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Kẻ bị nguyền",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Người mang lời nguyền hắc ám, có thể truyền lời nguyền để phong ấn năng lực của kẻ khác.",
                    SkillName = "Nguyền rủa câm lặng",
                    SkillDescription = "Chọn một người chơi vào ban đêm để nguyền rủa, khiến mục tiêu bị câm lặng (Silenced) vào ngày hôm sau.",
                    Restrictions = "Chỉ sử dụng vào ban đêm lên mục tiêu còn sống."
                };

            case RoleType.Brat:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Trẻ ranh (Brat)",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Đứa trẻ tinh nghịch thích lén lút nhìn trộm những bí mật diễn ra giữa màn đêm.",
                    SkillName = "Nhìn trộm ban đêm",
                    SkillDescription = "Có thể hé mắt nhìn trộm các hành động trong đêm để thu thập thông tin.",
                    Restrictions = "Nguy hiểm cực độ: nếu bị quái vật phát hiện khi đang nhìn trộm sẽ bị giết ngay tại chỗ."
                };

            case RoleType.Madman:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Kẻ điên",
                    FactionName = "Phe Thứ ba (Trung lập)",
                    Faction = FactionType.Neutral,
                    Description = "Kẻ mất trí với khao khát điên rồ: làm mọi cách để dân làng nghi ngờ và treo cổ chính mình.",
                    SkillName = "Kích động treo cổ",
                    SkillDescription = "Hành xử đáng ngờ để bị dân làng bỏ phiếu treo cổ.",
                    Restrictions = "Giành chiến thắng đặc biệt nếu bị dân làng biểu quyết treo cổ thành công ngay trong Ngày 1 (Day 1)."
                };

            case RoleType.FoxSpirit:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Cáo tinh (Hồ ly)",
                    FactionName = "Phe Thứ ba (Trung lập)",
                    Faction = FactionType.Neutral,
                    Description = "Hồ ly chín đuôi quyến rũ, dùng mị thuật mê hoặc tâm trí các người chơi khác.",
                    SkillName = "Mê hoặc tâm trí",
                    SkillDescription = "Mỗi đêm chọn mê hoặc (Charm) hai người chơi khác nhau.",
                    Restrictions = "Giành chiến thắng độc lập nếu còn sống và có ít nhất 2 người chơi bị mê hoặc sống sót tới cuối trận."
                };

            case RoleType.Killer:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Kẻ sát nhân (Killer)",
                    FactionName = "Phe Thứ ba (Trung lập)",
                    Faction = FactionType.Neutral,
                    Description = "Tên sát nhân cô độc cuồng sát, không thuộc phe Dân làng cũng không thuộc phe Ma sói.",
                    SkillName = "Ám sát ban đêm",
                    SkillDescription = "Chọn một người chơi bất kỳ để hạ sát trong đêm.",
                    Restrictions = "Chỉ có thể ra tay vào các đêm chẵn (Đêm 2, Đêm 4, ...). Thắng khi là người sống sót duy nhất."
                };

            default:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = role.ToString(),
                    FactionName = "Không rõ",
                    Faction = FactionType.Villager,
                    Description = "Thông tin vai trò đang được cập nhật.",
                    SkillName = "Chưa có",
                    SkillDescription = "Không có thông tin kỹ năng.",
                    Restrictions = "Không có."
                };
        }
    }

    public static RoleInfoData GetSecretRoleInfo(string playerName = "")
    {
        return new RoleInfoData
        {
            Role = RoleType.Villager,
            DisplayName = "Bí mật",
            FactionName = "Chưa được tiết lộ",
            Faction = FactionType.Villager,
            Description = $"Thông tin vai trò của {(string.IsNullOrEmpty(playerName) ? "người chơi này" : playerName)} được giữ bí mật theo luật chơi. Chỉ người chơi sở hữu mới có quyền xem.",
            SkillName = "Bí mật",
            SkillDescription = "Bạn không thể xem kỹ năng của người chơi khác khi chưa được phép.",
            Restrictions = "Thông tin được bảo mật bởi hệ thống phòng chống gian lận.",
            IsSecret = true
        };
    }

    public static string GetLocalizedRoleName(RoleType role)
    {
        return GetRoleInfo(role).DisplayName;
    }
}
