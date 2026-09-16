using UnityEngine;

public class WhiteHound : BaseRole
{
    public WhiteHound(PlayerData owner) : base(owner)
    {
        roleType = RoleType.WhiteHound;

        // Ban đầu WhiteHound là dân làng
        faction = FactionType.Villager;
    }

    public override void OnDeath()
    {
        // Không dùng ở đây.
    }

    public void BecomeMonster()
    {
        roleType = RoleType.WhiteHound;
        faction = FactionType.Monster;

        owner.isWhiteHoundAwakened = true;

        Debug.Log(owner.playerName + " đã trở thành WhiteHound!");
    }
}