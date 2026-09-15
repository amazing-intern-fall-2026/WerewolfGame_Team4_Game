using Unity.Netcode;
using UnityEngine;

public class NetworkGameTimer : NetworkBehaviour
{
    [Header("Thời gian mỗi State")]
    [SerializeField] private float nightTime = 30f;
    [SerializeField] private float morningTime = 10f;
    [SerializeField] private float discussionTime = 60f;
    [SerializeField] private float votingTime = 30f;
    [SerializeField] private float resolveTime = 10f;

    public NetworkVariable<float> TimeRemaining =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private void Update()
    {
        if (!IsServer)
            return;

        if (TimeRemaining.Value <= 0)
            return;

        TimeRemaining.Value -= Time.deltaTime;

        if (TimeRemaining.Value < 0)
            TimeRemaining.Value = 0;
    }

    public void SetTimerForState(NetworkGameState state)
    {
        if (!IsServer)
            return;

        switch (state)
        {
            case NetworkGameState.Night:
                TimeRemaining.Value = nightTime;
                break;

            case NetworkGameState.Morning:
                TimeRemaining.Value = morningTime;
                break;

            case NetworkGameState.Discussion:
                TimeRemaining.Value = discussionTime;
                break;

            case NetworkGameState.Voting:
                TimeRemaining.Value = votingTime;
                break;

            case NetworkGameState.Resolve:
                TimeRemaining.Value = resolveTime;
                break;

            default:
                TimeRemaining.Value = 0;
                break;
        }

        Debug.Log(
            "SERVER TIMER: " +
            state +
            " | " +
            TimeRemaining.Value +
            " giây"
        );
    }
}