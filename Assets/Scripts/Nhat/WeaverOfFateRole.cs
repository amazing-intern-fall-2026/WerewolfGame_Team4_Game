using UnityEngine;

public class WeaverOfFateRole : BaseRole
{
    public WeaverOfFateRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.WeaverOfFate;
        faction = FactionType.Villager;
    }

    public override void UseNightAbility(int targetID)
    {
        TryUseNightAbility(targetID, out _);
    }

    public override bool TryUseNightAbility(int targetID, out string feedback)
    {
        if (owner == null || !owner.isAlive)
        {
            feedback = "Dệt Duyên đã bị loại.";
            return false;
        }

        if (targetID == owner.playerID)
        {
            feedback = "Dệt Duyên không thể ghép đôi chính mình.";
            return false;
        }

        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetID);
        if (target == null || !target.isAlive)
        {
            feedback = "Mục tiêu không tồn tại hoặc đã bị loại.";
            return false;
        }

        if (owner.loverID != -1 || target.loverID != -1)
        {
            feedback = "Một trong hai người đã có cặp đôi.";
            return false;
        }

        if (LoverManager.Instance == null)
        {
            feedback = "Chưa có LoverManager.";
            return false;
        }

        LoverManager.Instance.MakeLovers(owner.playerID, targetID);

        if (owner.loverID == targetID && target.loverID == owner.playerID)
        {
            feedback = $"Đã ghép đôi Player {owner.playerID + 1} và Player {targetID + 1}.";
            return true;
        }

        feedback = "Không thể ghép đôi với mục tiêu này.";
        return false;
    }

    public void MakeLovers(int playerAID, int playerBID)
    {
        if (LoverManager.Instance == null)
            return;

        LoverManager.Instance.MakeLovers(
            playerAID,
            playerBID
        );
    }
}