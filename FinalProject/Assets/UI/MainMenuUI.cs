using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to any GameObject in the MainMenu scene.
/// Finds the StartButton automatically and wires up the click.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    public string gameSceneName = "SampleScene";

    private void Start()
    {
        // Wire the button at runtime so the editor script never needs to reference this class
        Button btn = GameObject.Find("StartButton")?.GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(StartGame);
        else
            Debug.LogError("[MainMenuUI] StartButton not found in scene.");
    }

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }
}
