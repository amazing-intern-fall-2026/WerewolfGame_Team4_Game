using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Hệ thống đồng bộ tin nhắn Chat Multiplayer qua Netcode for GameObjects.
/// Quản lý gửi/nhận tin nhắn giữa Host và các Client, kiểm tra tính hợp lệ và lưu lịch sử tin nhắn.
/// </summary>
public class NetworkChatManager : NetworkBehaviour
{
    public const int MaxMessageLength = 120;
    public const int MaxHistoryCount = 60;

    [Serializable]
    public struct ChatMessageEntry : INetworkSerializable, IEquatable<ChatMessageEntry>
    {
        public ulong SenderClientId;
        public FixedString64Bytes SenderName;
        public FixedString512Bytes MessageText;
        public long TimestampTicks;
        public bool IsSystem;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SenderClientId);
            serializer.SerializeValue(ref SenderName);
            serializer.SerializeValue(ref MessageText);
            serializer.SerializeValue(ref TimestampTicks);
            serializer.SerializeValue(ref IsSystem);
        }

        public bool Equals(ChatMessageEntry other)
        {
            return SenderClientId == other.SenderClientId &&
                   TimestampTicks == other.TimestampTicks &&
                   MessageText.Equals(other.MessageText);
        }
    }

    public static NetworkChatManager Instance { get; private set; }

    public static event Action<ChatMessageEntry> OnChatMessageReceived;
    public static event Action OnChatHistoryCleared;

    private readonly List<ChatMessageEntry> messageHistory = new List<ChatMessageEntry>();
    public IReadOnlyList<ChatMessageEntry> MessageHistory => messageHistory;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log($"[CHAT] NetworkChatManager Spawned | IsServer={IsServer} | LocalClientId={NetworkManager.Singleton?.LocalClientId}");
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Gửi tin nhắn từ người chơi cục bộ lên Server.
    /// </summary>
    public void SendChatMessage(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return;

        string sanitized = rawText.Trim();
        if (sanitized.Length > MaxMessageLength)
        {
            sanitized = sanitized.Substring(0, MaxMessageLength);
        }

        if (!CanSendUnderCurrentRules(out string reason))
        {
            Debug.LogWarning($"[CHAT] Không thể gửi tin nhắn: {reason}");
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            SendMessageServerRpc(sanitized);
        }
        else
        {
            // Chế độ xem trước cục bộ (Offline/Preview trong Editor)
            var previewEntry = new ChatMessageEntry
            {
                SenderClientId = 0,
                SenderName = new FixedString64Bytes("Bạn"),
                MessageText = new FixedString512Bytes(sanitized),
                TimestampTicks = DateTime.UtcNow.Ticks,
                IsSystem = false
            };
            AddMessageInternal(previewEntry);
        }
    }

    /// <summary>
    /// ServerRpc nhận tin nhắn từ Client, xác thực và broadcast tới toàn bộ người chơi.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SendMessageServerRpc(string text, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (string.IsNullOrWhiteSpace(text))
            return;

        string sanitized = text.Trim();
        if (sanitized.Length > MaxMessageLength)
        {
            sanitized = sanitized.Substring(0, MaxMessageLength);
        }

        string senderName = ResolveSenderName(senderClientId);

        var entry = new ChatMessageEntry
        {
            SenderClientId = senderClientId,
            SenderName = new FixedString64Bytes(senderName),
            MessageText = new FixedString512Bytes(sanitized),
            TimestampTicks = DateTime.UtcNow.Ticks,
            IsSystem = false
        };

        ReceiveMessageClientRpc(entry);
    }

    /// <summary>
    /// ClientRpc gửi tin nhắn mới đến tất cả Client để cập nhật giao diện.
    /// </summary>
    [ClientRpc]
    private void ReceiveMessageClientRpc(ChatMessageEntry entry)
    {
        AddMessageInternal(entry);
    }

    /// <summary>
    /// Gửi tin nhắn thông báo hệ thống (chỉ Server/Host được gọi).
    /// </summary>
    public void BroadcastSystemMessage(string systemText)
    {
        if (string.IsNullOrWhiteSpace(systemText))
            return;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
            return;

        var entry = new ChatMessageEntry
        {
            SenderClientId = ulong.MaxValue,
            SenderName = new FixedString64Bytes("Hệ thống"),
            MessageText = new FixedString512Bytes(systemText.Trim()),
            TimestampTicks = DateTime.UtcNow.Ticks,
            IsSystem = true
        };

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            ReceiveMessageClientRpc(entry);
        }
        else
        {
            AddMessageInternal(entry);
        }
    }

    private void AddMessageInternal(ChatMessageEntry entry)
    {
        messageHistory.Add(entry);
        while (messageHistory.Count > MaxHistoryCount)
        {
            messageHistory.RemoveAt(0);
        }

        OnChatMessageReceived?.Invoke(entry);
    }

    /// <summary>
    /// Truy vấn tên hiển thị của người chơi dựa trên ClientId từ hệ thống mạng hiện có.
    /// </summary>
    public string ResolveSenderName(ulong clientId)
    {
        // 1. Tìm thông qua NetworkManager ConnectedClients
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) &&
            client != null && client.PlayerObject != null)
        {
            var nameSync = client.PlayerObject.GetComponent<NetworkPlayerNameSync>();
            if (nameSync != null && nameSync.HasValidName())
            {
                return nameSync.GetPlayerName();
            }
        }

        // 2. Kiểm tra nếu là người chơi cục bộ
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (NetworkRoleSync.LocalInstance != null)
            {
                var localNameSync = NetworkRoleSync.LocalInstance.GetComponent<NetworkPlayerNameSync>();
                if (localNameSync != null && localNameSync.HasValidName())
                    return localNameSync.GetPlayerName();
            }
        }

        // 3. Fallback định danh mặc định
        return "Player " + (clientId + 1);
    }

    /// <summary>
    /// Lấy tên người chơi cục bộ.
    /// </summary>
    public string GetLocalSenderName()
    {
        ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
        return ResolveSenderName(localId);
    }

    /// <summary>
    /// Kiểm tra luật chat hiện tại (mặc định mở tự do, kiến trúc sẵn sàng mở rộng).
    /// </summary>
    public bool CanSendUnderCurrentRules(out string reason)
    {
        // Mặc định cho phép trao đổi tự do giữa Host và Client (chưa tự ý giới hạn theo phase)
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Xóa toàn bộ lịch sử tin nhắn.
    /// </summary>
    public void ClearHistory()
    {
        messageHistory.Clear();
        OnChatHistoryCleared?.Invoke();
    }
}
