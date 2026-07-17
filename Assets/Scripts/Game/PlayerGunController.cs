using UnityEngine;

public sealed class PlayerGunController : MonoBehaviour
{
    [SerializeField] private float bulletSpeed = 36f;
    [SerializeField] private float bulletLifetime = 2.5f;
    [SerializeField] private float fireCooldown = 0.2f;
    [SerializeField] private float bulletHeightOffset = 0.35f;

    private Transform equippedGun;
    private Transform recoilBone;
    private float nextFireTime;
    private Material bulletMaterial;
    private Color bulletColor = new Color(1f, 0.92f, 0.2f);
    private int weaponIndex;
    private AudioSource shotAudioSource;
    private AudioClip shotClip;

    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;
    private Vector3 originalRecoilBoneLocalPos;
    private Quaternion originalRecoilBoneLocalRot;
    private float recoilTimer = 0f;
    [SerializeField] private float recoilDuration = 0.24f;
    [SerializeField] private float recoilUpDistance = 0.09f;
    [SerializeField] private float recoilBackDistance = 0.04f;
    [SerializeField] private float recoilPitchDegrees = 22f;
    [SerializeField] private float shoulderRecoilUpDistance = 0.08f;
    [SerializeField] private float shoulderRecoilForwardDistance = 0.04f;
    [SerializeField] private float shoulderRecoilPitchDegrees = 18f;

    public bool HasGun => equippedGun != null;

    public void SetEquippedGun(Transform gun)
    {
        SetEquippedGun(gun, bulletColor, weaponIndex);
    }

    public void SetEquippedGun(Transform gun, Color weaponColor, int inventoryIndex)
    {
        // Unequipping: restore the arm to its rest pose so holstering doesn't leave the
        // shoulder/hand frozen mid-recoil, then clear state.
        if (gun == null)
        {
            ResetShoulderPose();
            recoilTimer = 0f;
            recoilBone = null;
            equippedGun = null;
            return;
        }

        equippedGun = gun;
        bulletColor = weaponColor;
        weaponIndex = Mathf.Max(0, inventoryIndex);
        bulletMaterial = null;
        shotClip = GetShotClip(weaponIndex);

        originalLocalPos = gun.localPosition;
        originalLocalRot = gun.localRotation;
        recoilBone = FindRecoilBone(gun.parent);
        if (recoilBone != null && recoilBone != transform)
        {
            originalRecoilBoneLocalPos = recoilBone.localPosition;
            originalRecoilBoneLocalRot = recoilBone.localRotation;
        }
    }

    public void RefreshEquippedGunRestPose()
    {
        if (equippedGun == null)
        {
            return;
        }

        originalLocalPos = equippedGun.localPosition;
        originalLocalRot = equippedGun.localRotation;
    }

    private void Update()
    {
        if (equippedGun == null)
        {
            return;
        }

        if (Time.time < nextFireTime)
        {
            return;
        }

        bool shootPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame)
        {
            shootPressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.F))
        {
            shootPressed = true;
        }
#endif

        if (shootPressed)
        {
            Shoot();
        }
    }

    private void LateUpdate()
    {
        if (equippedGun == null)
        {
            return;
        }

        if (recoilTimer <= 0f)
        {
            equippedGun.localPosition = originalLocalPos;
            equippedGun.localRotation = originalLocalRot;
            ResetShoulderPose();
            return;
        }

        float t = 1f - Mathf.Clamp01(recoilTimer / recoilDuration);
        float kick = Mathf.Sin(t * Mathf.PI);

        equippedGun.localPosition = originalLocalPos + new Vector3(0f, recoilUpDistance * kick, -recoilBackDistance * kick);
        equippedGun.localRotation = originalLocalRot * Quaternion.Euler(-recoilPitchDegrees * kick, 0f, 0f);
        ApplyShoulderRecoil(kick);
        recoilTimer -= Time.deltaTime;
    }

    private void ApplyShoulderRecoil(float kick)
    {
        if (recoilBone == null || recoilBone == transform)
        {
            return;
        }

        recoilBone.localPosition = originalRecoilBoneLocalPos + new Vector3(0f, shoulderRecoilUpDistance * kick, shoulderRecoilForwardDistance * kick);
        recoilBone.localRotation = originalRecoilBoneLocalRot * Quaternion.Euler(-shoulderRecoilPitchDegrees * kick, 0f, 0f);
    }

    private void ResetShoulderPose()
    {
        if (recoilBone == null || recoilBone == transform)
        {
            return;
        }

        recoilBone.localPosition = originalRecoilBoneLocalPos;
        recoilBone.localRotation = originalRecoilBoneLocalRot;
    }

    private Transform FindRecoilBone(Transform start)
    {
        Transform fallbackUpperArm = null;
        Transform current = start;

        while (current != null && current != transform)
        {
            // Match both StarterAssets ("Right_Shoulder") and Mixamo/Unreal ("clavicle_r") rigs.
            if (current.name.Contains("Right_Shoulder") || current.name.Contains("clavicle_r"))
            {
                return current;
            }

            if (fallbackUpperArm == null && (current.name.Contains("Right_UpperArm") || current.name.Contains("upperarm_r")))
            {
                fallbackUpperArm = current;
            }

            current = current.parent;
        }

        return fallbackUpperArm != null ? fallbackUpperArm : start;
    }

    private void Shoot()
    {
        nextFireTime = Time.time + fireCooldown;
        recoilTimer = recoilDuration;

        // Use the player's facing direction instead of the camera's
        Vector3 direction = transform.forward;
        // Spawn the bullet slightly ahead of the gun in the facing direction
        Vector3 origin = equippedGun.position + direction.normalized * 0.7f + Vector3.up * bulletHeightOffset;

        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "Bullet";
        bullet.transform.position = origin;
        bullet.transform.localScale = Vector3.one * 0.16f;

        Collider collider = bullet.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        Renderer renderer = bullet.GetComponent<Renderer>();
        renderer.sharedMaterial = GetBulletMaterial();

        CreateMuzzleFlash(origin, direction);
        PlayShotSound();
        bullet.AddComponent<BulletProjectile>().Initialize(direction, bulletSpeed, bulletLifetime);
    }

    private void CreateMuzzleFlash(Vector3 origin, Vector3 direction)
    {
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "Muzzle Flash";
        flash.transform.position = origin + direction.normalized * 0.25f;
        flash.transform.localScale = Vector3.one * (0.32f + weaponIndex * 0.04f);

        Collider collider = flash.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        Renderer renderer = flash.GetComponent<Renderer>();
        renderer.sharedMaterial = GetBulletMaterial();
        Destroy(flash, 0.08f);
    }

    private void PlayShotSound()
    {
        if (shotAudioSource == null)
        {
            shotAudioSource = gameObject.AddComponent<AudioSource>();
            shotAudioSource.playOnAwake = false;
            shotAudioSource.spatialBlend = 0.25f;
            shotAudioSource.volume = 0.35f;
        }

        if (shotClip == null)
        {
            shotClip = GetShotClip(weaponIndex);
        }

        shotAudioSource.pitch = 0.9f + weaponIndex * 0.08f;
        shotAudioSource.PlayOneShot(shotClip);
    }

    // Cached once, shared by every weapon index - the recorded shoot.mp3
    // (Assets/Resources/Audio/shoot.mp3) replaced the earlier per-weapon
    // procedural click. Falls back to the old synthesized clip if the
    // recorded one isn't present for some reason.
    private static AudioClip sharedShotClip;

    private static AudioClip GetShotClip(int index)
    {
        if (sharedShotClip == null)
        {
            sharedShotClip = RuntimeSfx.LoadClip("shoot");
        }

        return sharedShotClip != null ? sharedShotClip : CreateShotClip(index);
    }

    private Material GetBulletMaterial()
    {
        if (bulletMaterial != null)
        {
            return bulletMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        bulletMaterial = new Material(shader);
        bulletMaterial.name = "Runtime Bullet Material";
        bulletMaterial.color = bulletColor;
        return bulletMaterial;
    }

    private static AudioClip CreateShotClip(int index)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.RoundToInt(sampleRate * 0.12f);
        float[] samples = new float[sampleCount];
        float frequency = 160f + index * 85f;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float envelope = Mathf.Exp(-time * (22f + index * 2f));
            float tone = Mathf.Sin(2f * Mathf.PI * frequency * time);
            float click = i < sampleRate * 0.015f ? 0.6f : 0f;
            samples[i] = (tone * 0.45f + click) * envelope;
        }

        AudioClip clip = AudioClip.Create($"Runtime Gun Shot {index + 1}", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
