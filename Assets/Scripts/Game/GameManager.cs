using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum Outcome { None, Win, Lose }

    // Survives the scene change into RestartScene so the restart screen can
    // report whether the player won or lost. Set by GameScoreManager / PlayerHealth
    // just before they load the RestartScene.
    public static Outcome LastOutcome = Outcome.None;

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

        if (sceneName == "RestartScene")
        {
            ShowOutcomeMessage();
        }
    }

    // Displays "You Win!" or "You Lose!" above the Restart Game button, based on
    // how the previous play session ended.
    private void ShowOutcomeMessage()
    {
        if (LastOutcome == Outcome.None)
        {
            return;
        }

        bool won = LastOutcome == Outcome.Win;
        string message = won ? "You Win!" : "You Lose!";
        Color color = won ? new Color(0.35f, 0.9f, 0.4f) : new Color(0.95f, 0.35f, 0.3f);

        Canvas canvas = new GameObject("Outcome Canvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // draw on top of everything else

        CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);

        GameObject textObject = new GameObject("Outcome Text");
        textObject.transform.SetParent(canvas.transform, false);

        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
        {
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        text.text = message;
        text.fontSize = 64;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;

        // Centered horizontally, sitting above the Restart Game button.
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(700f, 120f);
        rect.anchoredPosition = new Vector2(0f, 120f);

        // Consume the result so a manual reload of the scene doesn't keep showing it.
        LastOutcome = Outcome.None;
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
