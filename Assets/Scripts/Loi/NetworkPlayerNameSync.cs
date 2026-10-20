using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerNameSync : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> PlayerName =
        new NetworkVariable<FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public static event Action OnAnyPlayerNameChanged;

    public override void OnNetworkSpawn()
    {
        PlayerName.OnValueChanged += OnPlayerNameChanged;

        Debug.Log(
            "PLAYER NAME SYNC | Player "
            + (OwnerClientId + 1)
            + " | Name = "
            + PlayerName.Value.ToString()
        );
    }

    public override void OnNetworkDespawn()
    {
        PlayerName.OnValueChanged -= OnPlayerNameChanged;
    }

    private void OnPlayerNameChanged(
        FixedString64Bytes oldName,
        FixedString64Bytes newName)
    {
        Debug.Log(
            "PLAYER NAME SYNC | Player "
            + (OwnerClientId + 1)
            + " | "
            + oldName.ToString()
            + " → "
            + newName.ToString()
        );

        OnAnyPlayerNameChanged?.Invoke();
    }

    public void SetPlayerName(string playerName)
    {
        if (!IsServer)
            return;

        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName =
                "Player " + (OwnerClientId + 1);
        }

        playerName = playerName.Trim();

        if (playerName.Length > 20)
            playerName =
                playerName.Substring(0, 20);

        PlayerName.Value =
            new FixedString64Bytes(playerName);
    }

    public string GetPlayerName()
    {
        string name =
            PlayerName.Value.ToString();

        if (string.IsNullOrWhiteSpace(name))
        {
            return "Player " + (OwnerClientId + 1);
        }

        return name;
    }

    public bool HasValidName()
    {
        return
            !string.IsNullOrWhiteSpace(
                PlayerName.Value.ToString()
            );
    }
}