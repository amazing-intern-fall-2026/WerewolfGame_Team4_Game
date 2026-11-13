using System;
using UnityEngine;

namespace Thuong.Online
{
    internal enum ThuongOnlineAction : byte
    {
        CompleteTask,
        NightAbility,
        ShamanGoodCharm,
        ShamanBadCharm,
        HunterTrap,
        Vote,
        RevealRead,
        MakeLovers,
        NoEliminationVote,
        RealtimeAbility,
        ReportCorpse,
        EmergencyMeeting,
        Sabotage,
        RepairSabotage,
        PairConsole,
        UseItem
    }

    internal sealed class ThuongOnlineLobbyMember
    {
        public ulong ClientId;
        public int PlayerId;
        public string Name;
        public int CharacterIndex;
        public bool Ready;
        public bool MapReady;
        public bool Offline;
    }

    internal struct ThuongOnlinePublicPlayer
    {
        public ulong ClientId;
        public int PlayerId;
        public string Name;
        public int CharacterIndex;
        public bool Alive;
        public bool Hidden;
        public Vector2 Position;
    }

    internal sealed class ThuongOnlineServerPlayer
    {
        public ulong ClientId;
        public int PlayerId;
        public string Name;
        public int CharacterIndex;
        public bool Ready;
        public bool MapReady;
        public Vector2 Position;
        public bool RevealRead;
        public Vector2 Input;
        public float LastInputTime;
        public ushort LastInputSequence;
        public uint LastRequestId;
        public double ActionWindow;
        public int ActionsInWindow;
        public ulong LastRoomStateRevision;
        public Guid ReconnectToken;
        public double ReconnectUntil;
        public bool Offline;
    }

    internal static class ThuongOnlineMessages
    {
        public const string Hello = "thuong.online.hello.v1";
        public const string Ready = "thuong.online.ready.v1";
        public const string Input = "thuong.online.input.v1";
        public const string Action = "thuong.online.action.v5";
        public const string Roster = "thuong.online.roster.v4";
        public const string Snapshot = "thuong.online.snapshot.v9";
        public const string PrivateState = "thuong.online.private.v4";
    }

    internal sealed class ThuongOnlineAvatar
    {
        public GameObject Root;
        public Animator Animator;
        public Vector2 TargetPosition;
        public Vector2 PreviousPosition;
        public int CharacterIndex;
    }
}
