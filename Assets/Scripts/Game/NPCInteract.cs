using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // In case TextMeshPro is used
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class NPCInteract : MonoBehaviour
{
    [Header("Mission Settings")]
    [TextArea(3, 10)]
    public string missionText = "Find 3 collectibles to win the game!";
    
    [Header("UI References")]
    public Text uiTextElement; // Can be legacy UI Text
    public TextMeshProUGUI tmpTextElement; // Can be TextMeshPro
    public GameObject uiPanel; // Optional background panel for text

    private bool isPlayerNearby = false;

    void Start()
    {
        if (uiPanel != null) uiPanel.SetActive(false);
        if (uiTextElement != null) uiTextElement.text = "";
        if (tmpTextElement != null) tmpTextElement.text = "";
    }

    void Update()
    {
        if (isPlayerNearby)
        {
            bool interactPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                interactPressed = true;
            }
#else
            if (Input.GetKeyDown(KeyCode.E))
            {
                interactPressed = true;
            }
#endif

            if (interactPressed)
            {
                ShowMission();
            }
        }
    }

    private void ShowMission()
    {
        if (uiPanel != null) uiPanel.SetActive(true);
        if (uiTextElement != null) uiTextElement.text = missionText;
        if (tmpTextElement != null) tmpTextElement.text = missionText;
        
        // Hide after 5 seconds
        StopAllCoroutines();
        StartCoroutine(HideMissionCoroutine());
    }

    private IEnumerator HideMissionCoroutine()
    {
        yield return new WaitForSeconds(5f);
        if (uiPanel != null) uiPanel.SetActive(false);
        if (uiTextElement != null) uiTextElement.text = "";
        if (tmpTextElement != null) tmpTextElement.text = "";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            // Optionally hide text when walking away
            if (uiPanel != null) uiPanel.SetActive(false);
            if (uiTextElement != null) uiTextElement.text = "";
            if (tmpTextElement != null) tmpTextElement.text = "";
        }
    }
}
