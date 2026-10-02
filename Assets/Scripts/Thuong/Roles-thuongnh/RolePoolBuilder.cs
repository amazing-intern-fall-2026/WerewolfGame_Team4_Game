using System;
using System.Collections.Generic;
using UnityEngine;

// Only concrete BaseRole implementations belong in this catalog. Several
// RoleType names are aliases in the older code and must not enter the draw.
public static class RoleCatalog
{
    public static readonly RoleType[] AllConcreteRoles =
    {
        RoleType.Villager,
        RoleType.DogSpirit,
        RoleType.Seer,
        RoleType.VillageGuardian,
        RoleType.Mayor,
        RoleType.Idiot,
        RoleType.WhiteHound,
        RoleType.SerpentSpirit,
        RoleType.Ogre,
        RoleType.Hunter,
        RoleType.Shaman,
        RoleType.WeaverOfFate,
        RoleType.Cursed,
        RoleType.Brat,
        RoleType.Madman,
        RoleType.FoxSpirit,
        RoleType.Killer
    };

    public static readonly RoleType[] MonsterSpecials =
    {
        RoleType.SerpentSpirit,
        RoleType.Ogre
    };

    public static readonly RoleType[] VillagerSpecials =
    {
        RoleType.Seer,
        RoleType.VillageGuardian,
        RoleType.Mayor,
        RoleType.Idiot,
        RoleType.WhiteHound,
        RoleType.Hunter,
        RoleType.Shaman,
        RoleType.WeaverOfFate,
        RoleType.Cursed,
        RoleType.Brat
    };

    public static readonly RoleType[] NeutralSpecials =
    {
        RoleType.Madman,
        RoleType.FoxSpirit,
        RoleType.Killer
    };

    public static BaseRole Create(RoleType type, PlayerData owner)
    {
        switch (type)
        {
            case RoleType.Villager: return new VillagerRole(owner);
            case RoleType.DogSpirit: return new DogSpirit(owner);
            case RoleType.Seer: return new SeerRole(owner);
            case RoleType.VillageGuardian: return new GuardianRole(owner);
            case RoleType.Mayor: return new MayorRole(owner);
            case RoleType.Idiot: return new IdiotRole(owner);
            case RoleType.WhiteHound: return new WhiteHound(owner);
            case RoleType.SerpentSpirit: return new SerpentSpirit(owner);
            case RoleType.Ogre: return new Ogre(owner);
            case RoleType.Hunter: return new HunterRole(owner);
            case RoleType.Shaman: return new ShamanRole(owner);
            case RoleType.WeaverOfFate: return new WeaverOfFateRole(owner);
            case RoleType.Cursed: return new CursedRole(owner);
            case RoleType.Brat: return new BratRole(owner);
            case RoleType.Madman: return new MadmanRole(owner);
            case RoleType.FoxSpirit: return new FoxSpiritRole(owner);
            case RoleType.Killer: return new KillerRole(owner);
            default: throw new ArgumentOutOfRangeException(nameof(type), type,
                "Role does not have a distinct playable implementation.");
        }
    }
}

public static class RolePoolBuilder
{
    // A seed makes a draw reproducible in tests. In normal play Unity supplies it.
    public static List<RoleType> Build(int playerCount, int? seed = null)
    {
        if (playerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(playerCount));

        var random = new System.Random(seed ?? UnityEngine.Random.Range(0, int.MaxValue));
        var roles = new List<RoleType>(playerCount);
        if (playerCount == 1)
        {
            roles.Add(RoleType.Villager);
            return roles;
        }

        // Every playable match has a real attacking wolf and a villager.
        roles.Add(RoleType.DogSpirit);
        roles.Add(RoleType.Villager);

        int targetMonsters = Math.Max(1, playerCount / 4);
        int maximumMonsters = Math.Max(2, (playerCount + 3) / 4);
        int maximumNeutrals = Math.Max(1, playerCount / 8);
        int reservedWolfCopies = Math.Max(0, targetMonsters - 1 - RoleCatalog.MonsterSpecials.Length);
        int specialCapacity = playerCount - roles.Count - reservedWolfCopies;

        // Every concrete special role participates in the same draw, including
        // a four-player lobby. Caps avoid filling a small match with one faction.
        var specials = new List<RoleType>();
        specials.AddRange(RoleCatalog.MonsterSpecials);
        specials.AddRange(RoleCatalog.VillagerSpecials);
        specials.AddRange(RoleCatalog.NeutralSpecials);
        Shuffle(specials, random);
        int monsters = 1;
        int neutrals = 0;
        int selected = 0;
        foreach (RoleType type in specials)
        {
            if (selected >= specialCapacity) break;
            bool monster = Array.IndexOf(RoleCatalog.MonsterSpecials, type) >= 0;
            bool neutral = Array.IndexOf(RoleCatalog.NeutralSpecials, type) >= 0;
            if (monster && monsters >= maximumMonsters) continue;
            if (neutral && neutrals >= maximumNeutrals) continue;
            roles.Add(type);
            selected++;
            if (monster) monsters++;
            if (neutral) neutrals++;
        }

        while (roles.Count < playerCount && monsters < targetMonsters)
        {
            roles.Add(RoleType.DogSpirit);
            monsters++;
        }

        while (roles.Count < playerCount)
            roles.Add(RoleType.Villager);

        Shuffle(roles, random);
        return roles;
    }

    private static void Shuffle<T>(List<T> list, System.Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            T value = list[i];
            list[i] = list[j];
            list[j] = value;
        }
    }
}
