using UnityEngine;
using UnityEngine.Events;

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
        IsDead = true;
        onDeath?.Invoke();
        Debug.Log("[PlayerHealth] Game Over!");
    }
}
