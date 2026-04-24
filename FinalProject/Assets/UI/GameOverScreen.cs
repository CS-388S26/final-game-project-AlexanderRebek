using UnityEngine;


// Shows the Game Over panel when the player dies.
public class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public GameObject gameOverPanel;

    public void Show()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
    }

    public void Hide()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }
}
