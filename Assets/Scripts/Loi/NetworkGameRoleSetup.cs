using System.Collections.Generic;

public static class NetworkGameRoleSetup
{
    private static readonly List<RoleAmountData> roleAmounts =
        new List<RoleAmountData>();

    public static void Save(
        List<RoleAmountData> source)
    {
        roleAmounts.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            roleAmounts.Add(
                source[i]
            );
        }
    }

    public static List<RoleAmountData> GetRoles()
    {
        List<RoleAmountData> result =
            new List<RoleAmountData>();

        for (int i = 0; i < roleAmounts.Count; i++)
        {
            result.Add(
                roleAmounts[i]
            );
        }

        return result;
    }

    public static int GetRoleAmount(
        RoleType role)
    {
        for (int i = 0; i < roleAmounts.Count; i++)
        {
            if (roleAmounts[i].Role == role)
                return roleAmounts[i].Amount;
        }

        return 0;
    }

    public static int GetTotalRoleAmount()
    {
        int total = 0;

        for (int i = 0; i < roleAmounts.Count; i++)
        {
            total += roleAmounts[i].Amount;
        }

        return total;
    }

    public static bool HasSetup()
    {
        return roleAmounts.Count > 0;
    }

    public static void Clear()
    {
        roleAmounts.Clear();
    }
}