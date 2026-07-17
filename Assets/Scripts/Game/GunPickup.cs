using StarterAssets;
using UnityEngine;

public sealed class GunPickup : MonoBehaviour
{
    // Cached once and reused by every gun pickup - the recorded pickupItem.mp3
    // (Assets/Resources/Audio/pickupItem.mp3). Falls back to the Proton/
    // Electron-style procedural chime if that clip isn't present for some
    // reason, so a gun pickup is never silent.
    private static AudioClip pickupClip;
    private static bool pickupClipLoaded;

    private GameScoreManager game;
    private GameObject gunPrefab;
    private string weaponName;
    private Color weaponColor = Color.white;
    private bool collected;

    public void Initialize(GameScoreManager manager, GameObject prefab, string displayName, Color displayColor)
    {
        game = manager;
        gunPrefab = prefab;
        weaponName = displayName;
        weaponColor = displayColor;
    }

    private void Update()
    {
        transform.Rotate(0f, 50f * Time.deltaTime, 0f, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || other.GetComponentInParent<ThirdPersonController>() == null)
        {
            return;
        }

        collected = true;

        if (!pickupClipLoaded)
        {
            pickupClip = RuntimeSfx.LoadClip("pickupItem");
            if (pickupClip == null)
            {
                pickupClip = RuntimeSfx.CreatePickupChime("Gun Pickup Chime", 450f, 900f);
            }
            pickupClipLoaded = true;
        }
        AudioSource.PlayClipAtPoint(pickupClip, transform.position, 0.6f);

        game.EquipGun(gunPrefab, weaponName, weaponColor);
        Destroy(gameObject);
    }
}
