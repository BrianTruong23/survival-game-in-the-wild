using UnityEngine;

public class WanderAI : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float changeDirectionInterval = 3f;
    public float wanderRadius = 15f;
    
    private Vector3 startPos;
    private float timer;
    private Vector3 randomDirection;
    private bool isStopped = false;

    void Start()
    {
        startPos = transform.position;
        PickNewDirection();
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            isStopped = !isStopped;
            if (isStopped)
            {
                timer = 3f; // Stop for exactly 3 seconds
                randomDirection = Vector3.zero;
            }
            else
            {
                PickNewDirection();
            }
        }
        
        // Move forward in the random direction using physics
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            Vector3 targetPosition = rb.position + randomDirection * moveSpeed * Time.deltaTime;
            rb.MovePosition(targetPosition);
        }
        else
        {
            transform.position += randomDirection * moveSpeed * Time.deltaTime;
        }
        
        // Smoothly rotate to face the movement direction
        if (randomDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(randomDirection), Time.deltaTime * 5f);
        }
    }

    void PickNewDirection()
    {
        // Randomize timer slightly so they don't all turn at once
        timer = changeDirectionInterval + Random.Range(-1f, 1f);
        
        // Pick a random point within a circle around the start position
        Vector2 randomPoint = Random.insideUnitCircle * wanderRadius;
        Vector3 targetPos = startPos + new Vector3(randomPoint.x, 0f, randomPoint.y);
        
        // Calculate flat direction vector
        randomDirection = (targetPos - transform.position).normalized;
        randomDirection.y = 0; 
    }
}
