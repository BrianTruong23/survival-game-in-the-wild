using UnityEngine;
using StarterAssets;

public sealed class ElectronCollectible : MonoBehaviour
{
    // Cached once and reused by every Electron instance - a brighter, faster
    // sweep (700Hz -> 1300Hz) to distinguish it from Proton's warmer chime.
    private static AudioClip pickupClip;

    private GameScoreManager scoreManager;
    private bool collected;

    public void Initialize(GameScoreManager manager)
    {
        scoreManager = manager;
    }

    private void Update()
    {
        transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || other.GetComponentInParent<ThirdPersonController>() == null)
        {
            return;
        }

        collected = true;

        if (pickupClip == null)
        {
            pickupClip = RuntimeSfx.CreatePickupChime("Electron Pickup Chime", 700f, 1300f);
        }
        AudioSource.PlayClipAtPoint(pickupClip, transform.position, 0.6f);

        if (scoreManager != null)
        {
            scoreManager.AddElectron();
        }

        Destroy(gameObject);
    }
}
