using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("UI Settings")]
    public Image[] healthIcons; // Array of the 3 health icons
    public float invincibilityTime = 1.5f; // Time after getting hit where player can't be hurt
    private bool isInvincible = false;

    private Image damageScreenFlash;
    private Text damageText;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthUI();
        CreateDamageUI();
    }

    private void CreateDamageUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        
        GameObject flashObj = new GameObject("DamageFlash");
        flashObj.transform.SetParent(canvas.transform, false);
        damageScreenFlash = flashObj.AddComponent<Image>();
        damageScreenFlash.color = new Color(1f, 0f, 0f, 0f); // Transparent red
        RectTransform rect = damageScreenFlash.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        damageScreenFlash.raycastTarget = false;
        
        GameObject textObj = new GameObject("DamageText");
        textObj.transform.SetParent(flashObj.transform, false);
        damageText = textObj.AddComponent<Text>();
        damageText.text = "YOU GOT HIT!";
        damageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (damageText.font == null) damageText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        damageText.fontSize = 80;
        damageText.color = new Color(1f, 1f, 1f, 0f); // Transparent white
        damageText.alignment = TextAnchor.MiddleCenter;
        RectTransform txtRect = damageText.rectTransform;
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;
        damageText.raycastTarget = false;
    }

    public void TakeDamage(int damageAmount)
    {
        if (isInvincible) return; // Ignore damage if invincible

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(currentHealth, 0); // Prevent health from dropping below 0

        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(InvincibilityCoroutine());
        }
    }

    private void UpdateHealthUI()
    {
        for (int i = 0; i < healthIcons.Length; i++)
        {
            if (healthIcons[i] != null)
            {
                // Enable icon if health is greater than index
                healthIcons[i].enabled = i < currentHealth;
            }
        }
    }

    private IEnumerator InvincibilityCoroutine()
    {
        isInvincible = true;
        
        if (damageScreenFlash != null) damageScreenFlash.color = new Color(1f, 0f, 0f, 0.45f);
        if (damageText != null) damageText.color = new Color(1f, 1f, 1f, 1f);

        float elapsed = 0f;
        while (elapsed < invincibilityTime)
        {
            elapsed += Time.deltaTime;
            
            if (damageScreenFlash != null) 
                damageScreenFlash.color = new Color(1f, 0f, 0f, Mathf.Lerp(0.45f, 0f, elapsed / invincibilityTime));
            
            if (damageText != null) 
                damageText.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, elapsed / invincibilityTime));
            
            yield return null;
        }

        isInvincible = false;
    }

    private void Die()
    {
        Debug.Log("Player Died!");
        // Load the Restart Scene (assuming its name is "RestartScene")
        // Alternatively, you can use the build index if you know it
        SceneManager.LoadScene("RestartScene");
    }
}
