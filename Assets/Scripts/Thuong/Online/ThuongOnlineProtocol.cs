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
        Vote
    }

    internal sealed class ThuongOnlineLobbyMember
    {
        public ulong ClientId;
        public int PlayerId;
        public string Name;
        public int CharacterIndex;
        public bool Ready;
    }

    internal struct ThuongOnlinePublicPlayer
    {
        public ulong ClientId;
        public int PlayerId;
        public string Name;
        public int CharacterIndex;
        public bool Alive;
        public Vector2 Position;
    }

    internal sealed class ThuongOnlineServerPlayer
    {
        public ulong ClientId;
        public int PlayerId;
        public string Name;
        public int CharacterIndex;
        public bool Ready;
        public Vector2 Position;
        public Vector2 Input;
        public float LastInputTime;
        public ushort LastInputSequence;
        public uint LastRequestId;
    }

    internal static class ThuongOnlineMessages
    {
        public const string Hello = "thuong.online.hello.v1";
        public const string Ready = "thuong.online.ready.v1";
        public const string Input = "thuong.online.input.v1";
        public const string Action = "thuong.online.action.v1";
        public const string Roster = "thuong.online.roster.v1";
        public const string Snapshot = "thuong.online.snapshot.v1";
        public const string PrivateState = "thuong.online.private.v1";
    }

    internal sealed class ThuongOnlineAvatar
    {
        public GameObject Root;
        public Animator Animator;
        public Vector2 TargetPosition;
        public Vector2 PreviousPosition;
    }
}
