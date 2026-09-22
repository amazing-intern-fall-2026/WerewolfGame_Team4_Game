using UnityEngine;

public enum RoleType
{
    // Monster / Werewolf side
    DogSprit,
    DogSpirit = DogSprit,
    WhiteHound,
    WhiteWolf = WhiteHound,
    RedNosedHound = WhiteHound,
    WolfCub = DogSprit,
    WolfBoss = DogSprit,

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
