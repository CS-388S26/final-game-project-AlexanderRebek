using UnityEngine;
using TMPro;
using System.Collections;


// Displays the current round, player health, and break countdown in the HUD
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Initialize health display in case the event fired before GameUI was ready
        if (PlayerHealth.Instance != null)
            UpdateHealth(PlayerHealth.Instance.CurrentHealth);
    }

    [Header("HUD Text fields")]
    public TextMeshProUGUI roundText;

    public TextMeshProUGUI healthText;

    public TextMeshProUGUI countdownText;

    private Coroutine _countdownCoroutine;

    // Called by RoundManager each time the round state changes

    public void UpdateRound(int round, bool inBreak, float breakDuration)
    {
        if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);

        if (inBreak)
        {
            if (round == 0)
                roundText?.SetText("Get ready!");
            else
                roundText?.SetText($"Round {round} complete!");

            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(true);
                _countdownCoroutine = StartCoroutine(CountdownRoutine(breakDuration));
            }
        }
        else
        {
            roundText?.SetText($"Round {round}");
            countdownText?.gameObject.SetActive(false);
        }
    }

    // Called by PlayerHealth.onHealthChanged

    public void UpdateHealth(int health)
    {
        healthText?.SetText($"HP: {health}");
    }

    public void UpdateHealth(float health)
    {
        UpdateHealth((int)health);
    }

    private IEnumerator CountdownRoutine(float duration)
    {
        float timer = duration;
        while (timer > 0f)
        {
            countdownText?.SetText($"Next round in {Mathf.CeilToInt(timer)}s");
            yield return null;
            timer -= Time.deltaTime;
        }
        countdownText?.gameObject.SetActive(false);
    }
}
