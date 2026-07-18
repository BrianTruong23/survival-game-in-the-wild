using UnityEngine;
using UnityEngine.UI;

public class WanderAI : MonoBehaviour
{
    public enum State { Wander, Chase }

    public float moveSpeed = 2f;
    public float changeDirectionInterval = 3f;
    public float wanderRadius = 15f;

    [Header("Chase Settings")]
    public float chaseSpeed = 3f;    // Speed used when chasing the player (slow, deliberate approach)

    [Header("Status")]
    public State currentState = State.Wander;  // Current behavior, visible in the Inspector

    // Shared across all enemies: press Q to toggle chase mode on/off.
    private static bool chaseMode = false;
    private static int lastToggleFrame = -1;
    private static Text statusLabel;

    private Transform player;
    private Vector3 startPos;
    private float timer;
    private Vector3 randomDirection;
    private bool isStopped = false;
    private bool isEnemy;   // Only real enemies chase; NPCs just keep wandering.

    void Start()
    {
        startPos = transform.position;

        // Enemies always carry an EnemyDamage component; NPCs do not. Use that
        // to decide who is allowed to chase the player.
        isEnemy = GetComponent<EnemyDamage>() != null;

        AcquirePlayer();
        PickNewDirection();
    }

    // Robustly locate the player: try the "Player" tag first, then fall back to
    // the PlayerHealth component (the tag isn't always set), then by name.
    void AcquirePlayer()
    {
        GameObject playerObj = null;
        try { playerObj = GameObject.FindGameObjectWithTag("Player"); } catch { }

        if (playerObj == null)
        {
            PlayerHealth ph = FindAnyObjectByType<PlayerHealth>();
            if (ph != null) playerObj = ph.gameObject;
        }

        if (playerObj == null)
        {
            playerObj = GameObject.Find("Player");
        }

        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    void Update()
    {
        HandleToggleAndStatusLabel();

        // The player may not exist yet when the enemy spawns - keep trying.
        if (player == null)
        {
            AcquirePlayer();
        }

        // Chase mode: move straight toward the player (enemies only, not NPCs).
        if (chaseMode && isEnemy && player != null)
        {
            currentState = State.Chase;
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            Vector3 chaseDirection = toPlayer.normalized;
            Move(chaseDirection, chaseSpeed);
            FaceDirection(chaseDirection);
            return;
        }

        // Normal wandering behavior when the player is far enough away.
        currentState = State.Wander;
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

        Move(randomDirection, moveSpeed);
        FaceDirection(randomDirection);
    }

    // Reads the Q key and refreshes the status label. Guarded so it only runs
    // once per frame no matter how many enemies exist.
    void HandleToggleAndStatusLabel()
    {
        if (lastToggleFrame == Time.frameCount)
        {
            return;
        }
        lastToggleFrame = Time.frameCount;

        if (Input.GetKeyDown(KeyCode.Q))
        {
            chaseMode = !chaseMode;
            Debug.Log($"[WanderAI] Q pressed -> chaseMode = {chaseMode}");
        }

        if (statusLabel == null)
        {
            CreateStatusLabel();
        }

        if (statusLabel != null)
        {
            statusLabel.text = chaseMode ? "Status: Chase" : "Status: Wander";
        }
    }

    // Builds a single lower-left HUD label used to show the current status.
    void CreateStatusLabel()
    {
        Canvas canvas = new GameObject("Enemy Status Canvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;

        CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);

        GameObject textObject = new GameObject("Enemy Status Text");
        textObject.transform.SetParent(canvas.transform, false);

        statusLabel = textObject.AddComponent<Text>();
        statusLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (statusLabel.font == null)
        {
            statusLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        statusLabel.fontSize = 22;
        statusLabel.alignment = TextAnchor.LowerLeft;
        statusLabel.color = Color.white;
        statusLabel.text = "Status: Wander";

        // Anchor to the lower-left corner with a 20px margin.
        RectTransform rect = statusLabel.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(300f, 34f);
        rect.anchoredPosition = new Vector2(20f, 20f);
    }

    void Move(Vector3 direction, float speed)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            Vector3 targetPosition = rb.position + direction * speed * Time.deltaTime;
            rb.MovePosition(targetPosition);
        }
        else
        {
            transform.position += direction * speed * Time.deltaTime;
        }
    }

    void FaceDirection(Vector3 direction)
    {
        // Smoothly rotate to face the movement direction
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);
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
