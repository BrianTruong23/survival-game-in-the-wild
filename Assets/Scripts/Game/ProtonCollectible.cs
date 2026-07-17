using UnityEngine;
using StarterAssets;

public sealed class ProtonCollectible : MonoBehaviour
{
    // Cached once and reused by every Proton instance - a warm, lower-pitched
    // sweep (300Hz -> 600Hz) to distinguish it from Electron's brighter chime.
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
            pickupClip = RuntimeSfx.CreatePickupChime("Proton Pickup Chime", 300f, 600f);
        }
        AudioSource.PlayClipAtPoint(pickupClip, transform.position, 0.6f);

        if (scoreManager != null)
        {
            scoreManager.AddProton();
        }

        Destroy(gameObject);
    }
}
