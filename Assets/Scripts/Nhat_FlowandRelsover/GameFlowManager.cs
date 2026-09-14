using System.Collections;
using UnityEngine;

public class PhaseManager : MonoBehaviour
{
    public static PhaseManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        StartRoleReveal();
    }

    public void StartRoleReveal()
    {
        StartCoroutine(RoleRevealRoutine());
    }

    private IEnumerator RoleRevealRoutine()
    {
        if (GameManager.Instance != null) GameManager.Instance.SetPhase(GamePhase.RoleReveal);
        Debug.Log("[GameFlow] ROLE REVEAL");
        yield return new WaitForSeconds(5);
        StartDay();
    }

    private void StartDay()
    {
        StartCoroutine(DayRoutine());
    }

    private IEnumerator DayRoutine()
    {
        if (GameManager.Instance != null) GameManager.Instance.SetPhase(GamePhase.DayStart);
        Debug.Log("[GameFlow] DAY " + (GameManager.Instance != null ? GameManager.Instance.currentDay : 1));
        yield return new WaitForSeconds(2);
        StartDiscussion();
    }

    private void StartDiscussion()
    {
        StartCoroutine(DiscussionRoutine());
    }

    private IEnumerator DiscussionRoutine()
    {
        if (GameManager.Instance != null) GameManager.Instance.SetPhase(GamePhase.Discussion);
        Debug.Log("[GameFlow] DISCUSSION START");
        yield return new WaitForSeconds(10); // Thời gian thảo luận (test 10s)
        StartVoting();
    }

    private void StartVoting()
    {
        StartCoroutine(VotingRoutine());
    }

    private IEnumerator VotingRoutine()
    {
        if (GameManager.Instance != null) GameManager.Instance.SetPhase(GamePhase.Voting);
        Debug.Log("[GameFlow] VOTING START");
        yield return new WaitForSeconds(10);

        if (GameManager.Instance != null) GameManager.Instance.SetPhase(GamePhase.ResolveVote);
        Debug.Log("[GameFlow] RESOLVING VOTES...");
        yield return new WaitForSeconds(3);

        if (WinConditionChecker.Instance != null && WinConditionChecker.Instance.CheckWin())
        {
            EndGame();
            yield break;
        }

        StartNight();
    }

    private void StartNight()
    {
        StartCoroutine(NightRoutine());
    }

    private IEnumerator NightRoutine()
    {
        if (GameManager.Instance != null) GameManager.Instance.SetPhase(GamePhase.Night);
        Debug.Log("[GameFlow] NIGHT START");
        yield return new WaitForSeconds(15);

        if (GameManager.Instance != null) GameManager.Instance.SetPhase(GamePhase.ResolveNight);
        Debug.Log("[GameFlow] RESOLVING NIGHT...");

        if (NightResolver.Instance != null)
        {
            NightResolver.Instance.ResolveNightActions();
        }
        yield return new WaitForSeconds(3);

        if (WinConditionChecker.Instance != null && WinConditionChecker.Instance.CheckWin())
        {
            EndGame();
            yield break;
        }

        if (GameManager.Instance != null) GameManager.Instance.currentDay++;
        StartDay();
    }

    private void EndGame()
    {
        if (GameManager.Instance != null) GameManager.Instance.SetPhase(GamePhase.GameOver);
        Debug.Log("[GameFlow] GAME OVER!");
    }
}