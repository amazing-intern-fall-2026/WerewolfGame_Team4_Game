using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkRoleManager : NetworkBehaviour
{
    [Header("Role Settings")]
    [SerializeField] private bool assignRolesAutomatically = true;

    private bool rolesAssigned = false;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        Debug.Log("ROLE MANAGER: Server đã Spawn.");

        if (assignRolesAutomatically)
        {
            AssignRoles();
        }
    }

    public void AssignRoles()
    {
        if (!IsServer)
            return;

        if (rolesAssigned)
            return;

        List<PlayerController> players =
            new List<PlayerController>();

        foreach (
            ulong clientId
            in NetworkManager.Singleton.ConnectedClientsIds
        )
        {
            NetworkClient client =
                NetworkManager.Singleton.ConnectedClients[clientId];

            if (client.PlayerObject == null)
                continue;

            PlayerController player =
                client.PlayerObject.GetComponent<PlayerController>();

            if (player != null)
            {
                players.Add(player);
            }
        }

        Debug.Log(
            "ROLE MANAGER: Tìm thấy " +
            players.Count +
            " Player."
        );

        if (players.Count == 0)
        {
            Debug.LogWarning(
                "ROLE MANAGER: Chưa tìm thấy Player."
            );

            return;
        }

        // Trộn danh sách Player
        Shuffle(players);

        // Mặc định tất cả là Villager
        foreach (PlayerController player in players)
        {
            player.Role.Value = PlayerRole.Villager;
        }

        // Player đầu tiên = Wolf
        if (players.Count >= 1)
        {
            players[0].Role.Value = PlayerRole.Wolf;
        }

        // Player thứ hai = Seer
        if (players.Count >= 2)
        {
            players[1].Role.Value = PlayerRole.Seer;
        }

        // Player thứ ba = Guardian
        if (players.Count >= 3)
        {
            players[2].Role.Value = PlayerRole.Guardian;
        }

        // Player thứ tư = Witch
        if (players.Count >= 4)
        {
            players[3].Role.Value = PlayerRole.Witch;
        }

        rolesAssigned = true;

        Debug.Log("ROLE MANAGER: Đã chia Role.");

        foreach (PlayerController player in players)
        {
            Debug.Log(
                "Player " +
                player.OwnerClientId +
                " → " +
                player.Role.Value
            );
        }
    }

    private void Shuffle(List<PlayerController> players)
    {
        for (int i = players.Count - 1; i > 0; i--)
        {
            int randomIndex =
                Random.Range(0, i + 1);

            PlayerController temp =
                players[i];

            players[i] =
                players[randomIndex];

            players[randomIndex] =
                temp;
        }
    }
}