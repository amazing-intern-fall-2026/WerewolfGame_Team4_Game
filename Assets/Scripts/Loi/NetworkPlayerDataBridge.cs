using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerDataBridge : NetworkBehaviour
{
    private bool registered;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        StartCoroutine(WaitForPlayerManager());
    }

    private IEnumerator WaitForPlayerManager()
    {
        while (!registered)
        {
            PlayerManager manager = PlayerManager.Instance;

            if (manager == null)
            {
                manager = FindFirstObjectByType<PlayerManager>();
            }

            if (manager != null)
            {
                RegisterToPlayerManager(manager);
                yield break;
            }

            yield return null;
        }
    }

    private void RegisterToPlayerManager(PlayerManager manager)
    {
        if (registered)
            return;

        int playerID = (int)OwnerClientId;

        PlayerData existingPlayer =
            manager.GetplayerByID(playerID);

        if (existingPlayer != null)
        {
            Debug.Log(
                "NETWORK PLAYER DATA BRIDGE: Player "
                + playerID
                + " đã tồn tại."
            );

            registered = true;
            return;
        }

        PlayerData player = new PlayerData
        {
            playerID = playerID,
            playerName = "Player " + playerID,
            isAlive = true,
            votePower = 1,
            hasVoted = false,
            hasUseNightAction = false,
            status = new PlayerStatus()
        };

        manager.RegisterPlayer(player);

        registered = true;

        Debug.Log(
            "NETWORK PLAYER DATA BRIDGE: "
            + "Đăng ký Player "
            + playerID
            + " vào PlayerManager."
        );

        Debug.Log(
            "NETWORK PLAYER DATA BRIDGE: "
            + "Server hiện có "
            + manager.players.Count
            + " PlayerData."
        );
    }
}