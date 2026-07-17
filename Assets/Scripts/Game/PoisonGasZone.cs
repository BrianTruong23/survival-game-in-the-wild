using UnityEngine;
using StarterAssets;

// A hazard trigger volume ("poison gas"): chip-damages the player once per
// PlayerHealth invincibility window while they remain inside it - that existing
// cooldown does double duty as this hazard's damage-tick rate limiter, so no
// extra timing code is needed here. Also pulses its gas-cloud visual so the
// danger reads clearly even to a player standing still inside it.
public sealed class PoisonGasZone : MonoBehaviour
{
    [SerializeField] private int damagePerTick = 1;
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.18f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.45f;
    [SerializeField] private float pulseSpeed = 1.1f;

    private Renderer[] gasRenderers;
    private MaterialPropertyBlock propertyBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public void Initialize(int damage, Transform gasVisual)
    {
        damagePerTick = Mathf.Max(1, damage);
        gasRenderers = gasVisual != null ? gasVisual.GetComponentsInChildren<Renderer>() : System.Array.Empty<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (gasRenderers == null || gasRenderers.Length == 0)
        {
            return;
        }

        float alpha = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);

        foreach (Renderer rend in gasRenderers)
        {
            if (rend == null || rend.sharedMaterial == null)
            {
                continue;
            }

            Color color = rend.sharedMaterial.color;
            color.a = alpha;

            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            rend.SetPropertyBlock(propertyBlock);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        ThirdPersonController controller = other.GetComponentInParent<ThirdPersonController>();
        if (controller == null)
        {
            return;
        }

        PlayerHealth health = controller.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.TakeDamage(damagePerTick);
        }
    }
}
