using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Scene Names")]
    public string mainGameScene = "MainScene";
    public string startScene = "StartScene";

    private void Start()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        // Freeze time entirely if we are in a menu scene so background characters stop moving
        if (sceneName == startScene || sceneName == "RestartScene")
        {
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    // Call this function from the Play button in StartScene
    public void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainGameScene);
    }

    // Call this function from the Restart button in RestartScene
    public void RestartGame()
    {
        Time.timeScale = 1f;
        // You can either go back to the StartScene or directly into the MainScene
        // SceneManager.LoadScene(startScene); 
        SceneManager.LoadScene(mainGameScene);
    }

    // Call this to Quit the game
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit Game"); // This just shows in the editor when testing
    }
}
