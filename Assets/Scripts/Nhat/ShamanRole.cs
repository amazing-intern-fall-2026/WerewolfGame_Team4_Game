using UnityEngine;

public class ShamanRole : BaseRole
{
    private bool hasGoodCharm = true;
    private bool hasBadCharm = true;
    private bool selectedGoodCharm;
    private bool hasSelectedCharm;

    public bool HasGoodCharm => hasGoodCharm;
    public bool HasBadCharm => hasBadCharm;

    public ShamanRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.Shaman;
        faction = FactionType.Villager;
    }

    public bool SelectCharm(bool goodCharm)
    {
        if ((goodCharm && !hasGoodCharm) || (!goodCharm && !hasBadCharm))
            return false;

        selectedGoodCharm = goodCharm;
        hasSelectedCharm = true;
        return true;
    }

    public override void UseNightAbility(int targetID)
    {
        TryUseNightAbility(targetID, out _);
    }

    public override bool TryUseNightAbility(int targetID, out string feedback)
    {
        if (!hasSelectedCharm)
        {
            feedback = "Hãy chọn bùa lợi hoặc bùa hại trước.";
            return false;
        }

        if (selectedGoodCharm && !hasGoodCharm || !selectedGoodCharm && !hasBadCharm)
        {
            feedback = "Loại bùa này đã được sử dụng.";
            return false;
        }

        if (owner == null || !owner.isAlive)
        {
            feedback = "Pháp Sư đã bị loại.";
            return false;
        }

        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetID);
        if (target == null || !target.isAlive)
        {
            feedback = "Mục tiêu không tồn tại hoặc đã bị loại.";
            return false;
        }

        target.status ??= new PlayerStatus();
        if (selectedGoodCharm)
        {
            target.status.isProtected = true;
            hasGoodCharm = false;
            feedback = $"Đã dùng bùa lợi lên Player {targetID + 1}.";
        }
        else
        {
            target.status.isSilenced = true;
            hasBadCharm = false;
            feedback = $"Đã dùng bùa hại lên Player {targetID + 1}.";
        }

        hasSelectedCharm = false;
        return true;
    }

    public void UseGoodCharm(int targetID)
    {
        if (!hasGoodCharm || owner == null || !owner.isAlive)
            return;

        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetID);

        if (target == null || !target.isAlive)
            return;

        target.status ??= new PlayerStatus();
        target.status.isProtected = true;
        hasGoodCharm = false;

        Debug.Log("Shaman dùng bùa lợi lên " + target.playerName);
    }

    public void UseBadCharm(int targetID)
    {
        if (!hasBadCharm || owner == null || !owner.isAlive)
            return;

        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetID);

        if (target == null || !target.isAlive)
            return;

        target.status ??= new PlayerStatus();
        target.status.isSilenced = true;
        hasBadCharm = false;

        Debug.Log("Shaman dùng bùa hại lên " + target.playerName);
    }
}
