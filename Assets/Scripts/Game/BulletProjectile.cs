using UnityEngine;

public sealed class BulletProjectile : MonoBehaviour
{
    // Cached once and reused by every bullet. Prefers the recorded
    // enemyHit.mp3 (Assets/Resources/Audio/enemyHit.mp3); falls back to the
    // earlier procedural thud if that clip isn't present for some reason.
    private static AudioClip defeatClip;
    private static bool defeatClipLoaded;

    private Vector3 velocity;
    private float lifeRemaining;

    public void Initialize(Vector3 direction, float speed, float lifetime)
    {
        velocity = direction.normalized * speed;
        lifeRemaining = lifetime;
    }

    private void Update()
    {
        Vector3 movement = velocity * Time.deltaTime;

        if (Physics.Raycast(transform.position, movement.normalized, out RaycastHit hit, movement.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            // Ignore collisions with the player themselves
            if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<PlayerHealth>() != null)
            {
                transform.position += movement;
                lifeRemaining -= Time.deltaTime;
                return;
            }

            transform.position = hit.point;
            
            // If we hit an enemy, destroy it and update the enemy objective counter.
            EnemyDamage enemy = hit.collider.GetComponentInParent<EnemyDamage>();
            if (enemy != null)
            {
                if (!defeatClipLoaded)
                {
                    defeatClip = RuntimeSfx.LoadClip("enemyHit");
                    if (defeatClip == null)
                    {
                        defeatClip = RuntimeSfx.CreateDefeatThud("Enemy Defeat Thud");
                    }
                    defeatClipLoaded = true;
                }
                AudioSource.PlayClipAtPoint(defeatClip, enemy.transform.position, 0.7f);

                Destroy(enemy.gameObject);

                GameScoreManager scoreManager = FindAnyObjectByType<GameScoreManager>();
                if (scoreManager != null)
                {
                    scoreManager.AddEnemyDefeat();
                }
            }

            Destroy(gameObject);
            return;
        }

        transform.position += movement;
        lifeRemaining -= Time.deltaTime;

        if (lifeRemaining <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
