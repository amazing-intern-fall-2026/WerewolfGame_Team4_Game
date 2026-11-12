using System;
using System.Collections.Generic;
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
    public Sprite RoleSprite { get; set; }
}

public static class RoleInfoDatabase
{
    private static readonly Dictionary<RoleType, Sprite> roleSpriteCache =
        new Dictionary<RoleType, Sprite>();

    public static void RegisterRoleSprite(RoleType role, Sprite sprite)
    {
        if (sprite != null)
        {
            roleSpriteCache[role] = sprite;
        }
    }

    public static void ClearSpriteCache()
    {
        roleSpriteCache.Clear();
    }

    public static Sprite GetRoleSprite(RoleType role)
    {
        if (roleSpriteCache.TryGetValue(role, out Sprite sprite) && sprite != null)
        {
            return sprite;
        }

        // 1. Tải từ RoleArtCatalog trong thư mục Resources nếu có
        RoleArtCatalog catalog = Resources.Load<RoleArtCatalog>("RoleArtCatalog");
        if (catalog != null)
        {
            Sprite s = catalog.GetSprite(role);
            if (s != null)
            {
                roleSpriteCache[role] = s;
                return s;
            }
        }

        // 2. Lấy từ NetworkRoleDetailUI nếu đã khởi tạo trên Scene
        if (NetworkRoleDetailUI.Instance != null)
        {
            Sprite s = NetworkRoleDetailUI.Instance.GetAssignedRoleSprite(role);
            if (s != null)
            {
                roleSpriteCache[role] = s;
                return s;
            }
        }

        // 3. Tra cứu từ NetworkPlayerIdentityCardUI trong Scene
        var identityCard = UnityEngine.Object.FindAnyObjectByType<NetworkPlayerIdentityCardUI>();
        if (identityCard != null && identityCard.RoleImages != null)
        {
            foreach (var entry in identityCard.RoleImages)
            {
                if (entry != null && entry.role == role && entry.image != null)
                {
                    roleSpriteCache[role] = entry.image;
                    return entry.image;
                }
            }
        }

#if UNITY_EDITOR
        // 4. Fallback Unity Editor: Nạp trực tiếp từ thư mục Assets/Art/
        if (editorArtPaths.TryGetValue(role, out string artPath))
        {
            Sprite s = LoadEditorSprite(artPath);
            if (s != null)
            {
                roleSpriteCache[role] = s;
                return s;
            }
        }
#endif

        // 5. Fallback: tra cứu từ RoleManager.Instance.roleDefinitions nếu có
        if (RoleManager.Instance?.roleDefinitions != null)
        {
            var def = RoleManager.Instance.roleDefinitions.Find(role);
            if (def != null && def.portrait != null)
            {
                roleSpriteCache[role] = def.portrait;
                return def.portrait;
            }
        }

        // 6. Fallback: Resources/RolePortraits/<Role>
        Sprite resourcePortrait = Resources.Load<Sprite>("RolePortraits/" + role);
        if (resourcePortrait != null)
        {
            roleSpriteCache[role] = resourcePortrait;
            return resourcePortrait;
        }

        return null;
    }

#if UNITY_EDITOR
    private static readonly Dictionary<RoleType, string> editorArtPaths = new Dictionary<RoleType, string>
    {
        // Phe Quái vật (Monster / Werewolf)
        { RoleType.DogSpirit, "Assets/Art/yeu_quai/khuyen tinh render.png" },
        { RoleType.WhiteHound, "Assets/Art/yeu_quai/bach khuyen tinh render.png" },
        { RoleType.WhiteWolf, "Assets/Art/yeu_quai/khuyen tinh render.png" },
        { RoleType.RedNosedHound, "Assets/Art/yeu_quai/khuyen tinh render.png" },
        { RoleType.WolfCub, "Assets/Art/yeu_quai/khuyen tinh render.png" },
        { RoleType.WolfBoss, "Assets/Art/yeu_quai/khuyen tinh render.png" },
        { RoleType.SerpentSpirit, "Assets/Art/yeu_quai/xa tinh render.png" },
        { RoleType.Ogre, "Assets/Art/yeu_quai/chan tinh render.png" },

        // Phe Dân làng (Villager)
        { RoleType.Villager, "Assets/Art/dan/dan lang render.png" },
        { RoleType.Mayor, "Assets/Art/dan/truong lang render.png" },
        { RoleType.Seer, "Assets/Art/dan/thay boi render.png" },
        { RoleType.VillageGuardian, "Assets/Art/dan/nguoi giu lang render.png" },
        { RoleType.Hunter, "Assets/Art/dan/tho san render.png" },
        { RoleType.Shaman, "Assets/Art/dan/thay phap render.png" },
        { RoleType.WeaverOfFate, "Assets/Art/dan/o to ba nguyet render.png" },
        { RoleType.Idiot, "Assets/Art/dan/thang kho render.png" },
        { RoleType.Cursed, "Assets/Art/dan/ke bi nguyen render.png" },
        { RoleType.Brat, "Assets/Art/dan/dua tre hu render.png" },
        { RoleType.TuongMaster, "Assets/Art/dan/dan lang render.png" },
        { RoleType.Magistrate, "Assets/Art/dan/truong lang render.png" },

        // Phe Thứ ba / Trung lập (Neutral / Third Party)
        { RoleType.Madman, "Assets/Art/Phe_thu3/thang dien render.png" },
        { RoleType.Jester, "Assets/Art/Phe_thu3/thang dien render.png" },
        { RoleType.Lover, "Assets/Art/Phe_thu3/doi tinh nhan render.png" },
        { RoleType.FoxSpirit, "Assets/Art/Phe_thu3/cao tinh render.png" },
        { RoleType.Piper, "Assets/Art/Phe_thu3/thang dien render.png" },
        { RoleType.Killer, "Assets/Art/Phe_thu3/sat thu mau.png" },
        { RoleType.SerialKiller, "Assets/Art/Phe_thu3/sat thu mau.png" },
        { RoleType.DeathHerald, "Assets/Art/yeu_quai/chan tinh render.png" }
    };

    private static Sprite LoadEditorSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;

        var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        if (all != null)
        {
            foreach (var obj in all)
            {
                if (obj is Sprite subSprite)
                    return subSprite;
            }
        }
        return null;
    }
#endif

    public static RoleInfoData GetRoleInfo(RoleType role)
    {
        RoleInfoData data = GetBaseRoleInfo(role);
        data.RoleSprite = GetRoleSprite(role);
        return data;
    }

    private static RoleInfoData GetBaseRoleInfo(RoleType role)
    {
        switch (role)
        {
            // ==========================================
            // PHE MA SÓI / QUÁI VẬT (MONSTER FACTION)
            // ==========================================
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
                    Restrictions = "Chỉ thực hiện trong giai đoạn Ban đêm. Cả bầy sói thống nhất 1 mục tiêu mỗi đêm."
                };

            case RoleType.WhiteHound:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Bạch khuyển",
                    FactionName = "Dân làng (Ban đầu) / Phe DogSpirit (Sau khi thức tỉnh)",
                    Faction = FactionType.Villager,
                    Description = "Linh thú ngụy trang trong lốt dân lành. Khi bị DogSpirit cắn trong đêm, sang ngày hôm sau sẽ thức tỉnh và chuyển qua phe DogSpirit.",
                    SkillName = "Thức tỉnh & Tấn công ban đêm",
                    SkillDescription = "Khi chưa bị cắn, tính là Dân làng. Sau khi bị DogSpirit cắn, hôm sau chuyển qua phe DogSpirit và mở khóa chức năng tấn công ban đêm.",
                    Restrictions = "Chức năng tấn công ban đêm được mở từ ngày hôm sau khi bị DogSpirit cắn."
                };

            case RoleType.WhiteWolf:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Sói trắng",
                    FactionName = "Phe Sói trắng (Đơn độc)",
                    Faction = FactionType.Monster,
                    Description = "Sói trắng cô độc ẩn mình trong bầy sói. Hắn có tham vọng tiêu diệt toàn bộ loài người lẫn đồng loại sói để trở thành kẻ thống trị duy nhất.",
                    SkillName = "Cắn xé đơn độc",
                    SkillDescription = "Cùng bầy sói cắn người mỗi đêm. Vào các đêm chẵn, Sói trắng có thể thức giấc riêng và cắn thêm 1 con sói khác.",
                    Restrictions = "Chỉ thức giấc cắn sói vào đêm chẵn. Thắng khi là kẻ duy nhất sống sót."
                };

            case RoleType.RedNosedHound:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Khuyển mũi đỏ",
                    FactionName = "Phe Quái vật (Ma sói)",
                    Faction = FactionType.Monster,
                    Description = "Khuyển tinh mang khứu giác siêu phàm, có khả năng đánh hơi phát hiện vị trí các vai trò quan trọng của làng.",
                    SkillName = "Đánh hơi con mồi",
                    SkillDescription = "Cùng bầy sói đi săn vào ban đêm. Có thể đánh hơi kiểm tra một người chơi.",
                    Restrictions = "Chỉ thực hiện trong giai đoạn Ban đêm."
                };

            case RoleType.WolfCub:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Sói con",
                    FactionName = "Phe Quái vật (Ma sói)",
                    Faction = FactionType.Monster,
                    Description = "Đứa con bé bỏng của bầy sói hoang. Cái chết của Sói con sẽ khơi dậy cơn thịnh nộ tột cùng của bầy sói.",
                    SkillName = "Cơn thịnh nộ bầy sói",
                    SkillDescription = "Nếu Sói con bị giết (bị treo cổ hoặc bị thợ săn bắn), đêm tiếp theo bầy sói sẽ được phép cắn 2 nạn nhân để trả thù.",
                    Restrictions = "Kỹ năng nội tại tự động kích hoạt sau khi Sói con bị hạ gục."
                };

            case RoleType.WolfBoss:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Sói đầu đàn",
                    FactionName = "Phe Quái vật (Ma sói)",
                    Faction = FactionType.Monster,
                    Description = "Thủ lĩnh tối cao của bầy sói với sức mạnh áp đảo và nanh vuốt tàn bạo, dẫn dắt bầy sói gieo rắc kinh hoàng.",
                    SkillName = "Thống lĩnh bầy sói",
                    SkillDescription = "Có quyền chỉ định mục tiêu tấn công ưu tiên của bầy sói trong đêm.",
                    Restrictions = "Thực hiện trong giai đoạn Ban đêm."
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
                    SkillDescription = "Chọn một người chơi để hạ gục vào ban đêm sau khi đã tích lũy đủ sức mạnh hắc ám.",
                    Restrictions = "Cần tích lũy tà khí trong 2 đêm đầu tiên, bắt đầu tấn công từ đêm thứ 3."
                };

            case RoleType.Ogre:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Chằn tinh (Ogre)",
                    FactionName = "Phe Quái vật (Ma sói)",
                    Faction = FactionType.Monster,
                    Description = "Quái vật hung tợn với sức mạnh tàn phá khủng khiếp, đồng minh đắc lực của bầy sói trong đêm tối.",
                    SkillName = "Nghiền nát mục tiêu",
                    SkillDescription = "Chọn và tiêu diệt một người chơi còn sống trong đêm.",
                    Restrictions = "Chỉ sử dụng vào ban đêm lên người chơi còn sống."
                };

            // ==========================================
            // PHE DÂN LÀNG (VILLAGER FACTION)
            // ==========================================
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

            case RoleType.Mayor:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Thị trưởng",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Vị lãnh đạo uy tín của ngôi làng, có tiếng nói đặc biệt quan trọng trong các cuộc biểu quyết.",
                    SkillName = "Lá phiếu kép",
                    SkillDescription = "Phiếu biểu quyết treo cổ của Thị trưởng có trọng số gấp đôi (tính bằng 2 phiếu).",
                    Restrictions = "Áp dụng tự động trong toàn bộ các phiên Bỏ phiếu ban ngày."
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
                    DisplayName = "Người giữ làng (Bảo vệ)",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Vị hộ vệ dũng cảm âm thầm tuần tra trong đêm để che chở cho dân làng khỏi nanh vuốt loài sói.",
                    SkillName = "Khiên hộ vệ",
                    SkillDescription = "Chọn một người chơi (có thể chọn chính mình) để bảo vệ khỏi đòn tấn công ban đêm.",
                    Restrictions = "Không thể bảo vệ cùng một người chơi trong hai đêm liên tiếp."
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
                    DisplayName = "Thầy pháp (Pháp sư)",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Thầy pháp nắm giữ bùa chú bí truyền: sở hữu 1 bùa lợi để cứu người và 1 bùa hại để tiêu diệt kẻ ác.",
                    SkillName = "Bùa cứu / Bùa hại",
                    SkillDescription = "Vào ban đêm, có 1 bùa lợi để cứu người chơi khỏi cái chết, và 1 bùa hại để tiêu diệt một người chơi.",
                    Restrictions = "Mỗi loại bùa (1 bùa cứu, 1 bùa hại) chỉ được sử dụng duy nhất một lần trong toàn bộ ván chơi."
                };

            case RoleType.WeaverOfFate:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Dệt duyên (Ô Thố Ba Nguyệt)",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Vị thần kết nối tơ duyên, se sợi chỉ đỏ gắn kết sinh mệnh của hai con người lại với nhau.",
                    SkillName = "Se duyên sinh tử",
                    SkillDescription = "Vào ban đêm, chọn hai người chơi để kết đôi thành Cặp đôi (Lovers). Nếu một người chết, người kia cũng chết theo.",
                    Restrictions = "Chỉ sử dụng duy nhất một lần trong cả ván đấu vào ban đêm."
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
                    SkillDescription = "Nếu bị dân làng bỏ phiếu treo cổ, thân phận Thằng khờ sẽ được công khai và được tha chết.",
                    Restrictions = "Sau khi lộ diện sẽ mất quyền bỏ phiếu; chỉ kích hoạt 1 lần khi bị treo cổ."
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
                    DisplayName = "Đứa trẻ hư (Trẻ ranh)",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Đứa trẻ tinh nghịch thích lén lút nhìn trộm những bí mật diễn ra giữa màn đêm tăm tối.",
                    SkillName = "Nhìn trộm ban đêm",
                    SkillDescription = "Có thể hé mắt nhìn trộm các hành động trong đêm để thu thập thông tin về bầy sói.",
                    Restrictions = "Nguy hiểm cực độ: nếu bị quái vật phát hiện khi đang nhìn trộm sẽ bị giết ngay tại chỗ."
                };

            case RoleType.TuongMaster:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Tướng quân (Võ sư)",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Bậc thầy võ nghệ uy dũng, kiên cường đứng mũi chịu sào để bảo vệ bá tánh khỏi tai ương.",
                    SkillName = "Phòng thủ kiên cố",
                    SkillDescription = "Có khả năng chống cự lại đòn tấn công đầu tiên trong đêm.",
                    Restrictions = "Kỹ năng nội tại tự động kích hoạt."
                };

            case RoleType.Magistrate:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Phán quan (Quan huyện)",
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Vị quan công minh nắm giữ quyền hành tư pháp, có thể can thiệp vào các phiên tòa xét xử.",
                    SkillName = "Lệnh tha bổng",
                    SkillDescription = "Có thể hủy bỏ một quyết định treo cổ nếu nhận thấy bằng chứng chưa thuyết phục.",
                    Restrictions = "Chỉ sử dụng 1 lần trong cả trận vào giai đoạn Biểu quyết."
                };

            // ==========================================
            // PHE THỨ BA / TRUNG LẬP (NEUTRAL FACTION)
            // ==========================================
            case RoleType.Madman:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Thằng điên (Kẻ điên)",
                    FactionName = "Phe Thứ ba (Trung lập)",
                    Faction = FactionType.Neutral,
                    Description = "Kẻ mất trí với khao khát điên rồ: làm mọi cách để dân làng nghi ngờ và treo cổ chính mình.",
                    SkillName = "Kích động treo cổ",
                    SkillDescription = "Hành xử đáng ngờ để bị dân làng bỏ phiếu treo cổ.",
                    Restrictions = "Giành chiến thắng đặc biệt nếu bị dân làng biểu quyết treo cổ thành công ngay trong Ngày 1 (Day 1)."
                };

            case RoleType.Jester:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Chú hề (Jester)",
                    FactionName = "Phe Thứ ba (Trung lập)",
                    Faction = FactionType.Neutral,
                    Description = "Kẻ mua vui bí hiểm, biến phiên tòa xét xử thành sân khấu của riêng mình.",
                    SkillName = "Treo cổ thắng cuộc",
                    SkillDescription = "Lừa gạt mọi người bỏ phiếu treo cổ bản thân trong phiên tòa ban ngày.",
                    Restrictions = "Thắng khi bị dân làng treo cổ ban ngày."
                };

            case RoleType.Lover:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Đôi tình nhân (Lovers)",
                    FactionName = "Phe Tình nhân (Thứ ba)",
                    Faction = FactionType.Neutral,
                    Description = "Cặp đôi được thần Dệt Duyên se tơ hồng, cùng chung số phận sinh tử.",
                    SkillName = "Đồng cam cộng khổ",
                    SkillDescription = "Biết danh tính của nhau và bảo vệ nhau. Nếu một người chết, người kia cũng chết theo.",
                    Restrictions = "Giành chiến thắng nếu là hai người cuối cùng còn sống sót trong làng."
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

            case RoleType.Piper:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Người thổi sáo",
                    FactionName = "Phe Thứ ba (Trung lập)",
                    Faction = FactionType.Neutral,
                    Description = "Nghệ sĩ lang thang mang theo tiếng sáo ma mị, mê hoặc tâm hồn của tất cả những ai nghe thấy.",
                    SkillName = "Tiếng sáo thôi miên",
                    SkillDescription = "Mỗi đêm thổi sáo thôi miên 2 người chơi còn sống.",
                    Restrictions = "Thắng khi toàn bộ người chơi còn sống đều bị thôi miên."
                };

            case RoleType.Killer:
            case RoleType.SerialKiller:
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

            case RoleType.DeathHerald:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = "Sứ giả tử thần",
                    FactionName = "Phe Quái vật (Thứ ba)",
                    Faction = FactionType.Monster,
                    Description = "Điềm báo của cái chết và tai họa, gieo rắc nỗi khiếp sợ lên toàn thể ngôi làng.",
                    SkillName = "Điềm báo tử vong",
                    SkillDescription = "Đánh dấu mục tiêu tử vong trong đêm.",
                    Restrictions = "Sử dụng trong giai đoạn Ban đêm."
                };

            default:
                return new RoleInfoData
                {
                    Role = role,
                    DisplayName = role.ToString(),
                    FactionName = "Phe Dân làng",
                    Faction = FactionType.Villager,
                    Description = "Người dân trong làng tham gia thảo luận và bỏ phiếu tìm ra Ma sói.",
                    SkillName = "Biểu quyết",
                    SkillDescription = "Tham gia bỏ phiếu biểu quyết vào ban ngày.",
                    Restrictions = "Không có kỹ năng đặc biệt trong đêm."
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

[Serializable]
public class RoleArtCatalog : ScriptableObject
{
    [Serializable]
    public class RoleArtEntry
    {
        public RoleType role;
        public Sprite sprite;
    }

    [SerializeField]
    public List<RoleArtEntry> entries = new List<RoleArtEntry>();

    public Sprite GetSprite(RoleType role)
    {
        if (entries == null) return null;
        var entry = entries.Find(e => e != null && e.role == role);
        return entry?.sprite;
    }

    public void SetSprite(RoleType role, Sprite sprite)
    {
        if (sprite == null) return;
        if (entries == null) entries = new List<RoleArtEntry>();
        var entry = entries.Find(e => e != null && e.role == role);
        if (entry != null)
        {
            entry.sprite = sprite;
        }
        else
        {
            entries.Add(new RoleArtEntry { role = role, sprite = sprite });
        }
    }
}
