using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Manages the player health.
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    [Header("Stats")]
    public int maxHealth = 100;

    [Header("Game Over")]
    public string mainMenuScene = "MainMenu";
    public float gameOverDelay = 3f;

    [Header("Events")]
    public UnityEvent<int> onHealthChanged;
    public UnityEvent      onDeath;

    public int  CurrentHealth { get; private set; }
    public bool IsDead        { get; private set; }

    private void Start()
    {
        CurrentHealth = maxHealth;
        onHealthChanged?.Invoke(CurrentHealth);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        onHealthChanged?.Invoke(CurrentHealth);

        if (CurrentHealth <= 0)
            Die();
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        onDeath?.Invoke();
        Debug.Log("[PlayerHealth] Game Over!");

        if (GameOverScreen.Instance != null)
            GameOverScreen.Instance.Show();
        else
            Debug.LogError("[PlayerHealth] GameOverScreen.Instance is null!");

        StartCoroutine(ReturnToMenuRoutine());
    }

    private IEnumerator ReturnToMenuRoutine()
    {
        yield return new WaitForSeconds(gameOverDelay);
        SceneManager.LoadScene(mainMenuScene);
    }
}
