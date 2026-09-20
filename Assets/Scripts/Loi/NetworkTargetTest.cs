using Unity.Netcode;
using UnityEngine;

public class NetworkTargetTest : MonoBehaviour
{
    private bool guardianMode;
    private bool dogSpiritMode;

    private void Update()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsClient)
            return;

        // =========================
        // CHỌN ROLE ACTION
        // =========================

        // G = Guardian
        if (Input.GetKeyDown(KeyCode.G))
        {
            guardianMode = true;
            dogSpiritMode = false;

            Debug.Log(
                "[TARGET TEST] Guardian mode | " +
                "Z/X/C/V/B để chọn TargetID 0-4"
            );
        }

        // Q = DogSpirit
        if (Input.GetKeyDown(KeyCode.Q))
        {
            dogSpiritMode = true;
            guardianMode = false;

            Debug.Log(
                "[TARGET TEST] DogSpirit mode | " +
                "Z/X/C/V/B để chọn TargetID 0-4"
            );
        }

        // =========================
        // CHỌN TARGET
        // =========================

        if (Input.GetKeyDown(KeyCode.Z))
            SelectTarget(0);

        if (Input.GetKeyDown(KeyCode.X))
            SelectTarget(1);

        if (Input.GetKeyDown(KeyCode.C))
            SelectTarget(2);

        if (Input.GetKeyDown(KeyCode.V))
            SelectTarget(3);

        if (Input.GetKeyDown(KeyCode.B))
            SelectTarget(4);
    }

    private void SelectTarget(ulong targetID)
    {
        if (!guardianMode && !dogSpiritMode)
        {
            Debug.Log(
                "[TARGET TEST] Chưa chọn Action. " +
                "Bấm Q hoặc G trước."
            );

            return;
        }

        NetworkObject localPlayer =
            NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();

        if (localPlayer == null)
        {
            Debug.LogWarning(
                "[TARGET TEST] Không tìm thấy Local Player!"
            );

            return;
        }

        NetworkPlayerAction action =
            localPlayer.GetComponent<NetworkPlayerAction>();

        if (action == null)
        {
            Debug.LogWarning(
                "[TARGET TEST] Local Player không có NetworkPlayerAction!"
            );

            return;
        }

        if (guardianMode)
        {
            Debug.Log(
                "[TARGET TEST] Guardian → Target Player "
                + targetID
            );
        }

        if (dogSpiritMode)
        {
            Debug.Log(
                "[TARGET TEST] DogSpirit → Target Player "
                + targetID
            );
        }

        // Gửi Action thật qua Networking
        action.RequestActionServerRpc(targetID);

        // Reset mode
        guardianMode = false;
        dogSpiritMode = false;
    }
}