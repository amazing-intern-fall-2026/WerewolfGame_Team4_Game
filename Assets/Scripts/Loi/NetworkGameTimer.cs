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

    public void SetTimerForState(GameState state)
    {
        if (!IsServer)
            return;

        switch (state)
        {
            case GameState.Night:
                TimeRemaining.Value = nightTime;
                break;

            case GameState.Morning:
                TimeRemaining.Value = morningTime;
                break;

            case GameState.Discussion:
                TimeRemaining.Value = discussionTime;
                break;

            case GameState.Voting:
                TimeRemaining.Value = votingTime;
                break;

            case GameState.Resolve:
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