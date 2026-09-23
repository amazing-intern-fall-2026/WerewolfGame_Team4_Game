using UnityEngine;

public enum RoleType
{
    // Monster / Werewolf side
    DogSpirit,
    DogSprit = DogSpirit,
    WhiteHound,
    WhiteWolf = WhiteHound,
    RedNosedHound = WhiteHound,
    WolfCub = DogSpirit,
    WolfBoss = DogSpirit,

    SerpentSpirit,
    Ogre,

    // Villager side
    Villager,
    Mayor,
    Seer,
    VillageGuardian,
    Hunter,
    Shaman,
    WeaverOffate,
    WeaverOfFate = WeaverOffate,
    Idiot,
    Cursed,
    Brat,
    TuongMaster,
    Magistrate,

    // Third party / special
    Madman,
    Jester = Madman,
    Lover = Madman,
    FoxSpirit,
    Piper = FoxSpirit,
    Killer,
    SerialKiller = Killer,
    DeathHerald
} 
