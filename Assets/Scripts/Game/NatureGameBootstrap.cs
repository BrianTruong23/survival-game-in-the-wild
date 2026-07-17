using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class NatureGameBootstrap : MonoBehaviour
{
    [SerializeField] private AudioClip ambientClip;

    private Text objectiveText;
    private Text promptText;
    private int collected;
    private int totalCollectibles;

    private void Start()
    {
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.55f, 0.66f, 0.62f);
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.ambientLight = new Color(0.62f, 0.68f, 0.58f);

        Material grass = CreateMaterial("Low Poly Grass", new Color(0.32f, 0.55f, 0.24f));
        Material dirt = CreateMaterial("Trail Dirt", new Color(0.45f, 0.34f, 0.22f));
        Material bark = CreateMaterial("Tree Bark", new Color(0.34f, 0.21f, 0.12f));
        Material leaves = CreateMaterial("Pine Leaves", new Color(0.09f, 0.38f, 0.20f));
        Material rock = CreateMaterial("Stone", new Color(0.45f, 0.47f, 0.44f));
        Material crystal = CreateMaterial("Collectible Crystal", new Color(0.2f, 0.9f, 0.95f));
        Material player = CreateMaterial("Player Blue", new Color(0.14f, 0.34f, 0.74f));
        Material gate = CreateMaterial("Gate Wood", new Color(0.27f, 0.17f, 0.09f));

        GameObject terrain = CreateTerrain(grass);
        CreateTrail(dirt);
        CreateForest(bark, leaves, rock);
        CreateGate(gate);
        CreateCollectibles(crystal);
        GameObject playerObject = CreatePlayer(player);
        SetupCamera(playerObject.transform);
        SetupUi();
        SetupAudio();

        UpdateObjective();
        promptText.text = "WASD to move, Space to jump, collect crystals, press E near the gate";
    }

    public void Collect(GameObject item)
    {
        collected++;
        Destroy(item);
        UpdateObjective();
    }

    public bool HasAllCollectibles()
    {
        return collected >= totalCollectibles;
    }

    public void SetPrompt(string message)
    {
        if (promptText != null)
        {
            promptText.text = message;
        }
    }

    private void UpdateObjective()
    {
        if (objectiveText != null)
        {
            objectiveText.text = $"Crystals: {collected}/{totalCollectibles}";
        }
    }

    private static Material CreateMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = name;
        material.color = color;
        return material;
    }

    private static GameObject CreateTerrain(Material material)
    {
        const int cells = 28;
        const float size = 70f;
        const float half = size * 0.5f;
        float step = size / cells;

        List<Vector3> vertices = new List<Vector3>(cells * cells * 6);
        List<int> triangles = new List<int>(cells * cells * 6);

        for (int z = 0; z < cells; z++)
        {
            for (int x = 0; x < cells; x++)
            {
                Vector3 a = TerrainPoint(x, z, step, half);
                Vector3 b = TerrainPoint(x + 1, z, step, half);
                Vector3 c = TerrainPoint(x, z + 1, step, half);
                Vector3 d = TerrainPoint(x + 1, z + 1, step, half);
                AddTriangle(vertices, triangles, a, c, b);
                AddTriangle(vertices, triangles, b, c, d);
            }
        }

        Mesh mesh = new Mesh { name = "Runtime Low Poly Terrain" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject terrain = new GameObject("Low Poly Nature Terrain");
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        terrain.AddComponent<MeshRenderer>().sharedMaterial = material;
        terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
        return terrain;
    }

    private static Vector3 TerrainPoint(int x, int z, float step, float half)
    {
        float worldX = x * step - half;
        float worldZ = z * step - half;
        float distanceFromTrail = Mathf.Abs(worldX);
        float hill = Mathf.Sin(worldX * 0.21f) * 1.1f + Mathf.Cos(worldZ * 0.17f) * 0.8f;
        float trailFlatten = Mathf.Clamp01(distanceFromTrail / 8f);
        float height = Mathf.Lerp(0f, hill, trailFlatten);
        return new Vector3(worldX, height, worldZ);
    }

    private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
    }

    private static void CreateTrail(Material material)
    {
        GameObject trail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trail.name = "Readable Dirt Trail";
        trail.transform.position = new Vector3(0f, 0.04f, 0f);
        trail.transform.localScale = new Vector3(5.5f, 0.05f, 64f);
        trail.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(trail.GetComponent<Collider>());
    }

    private static void CreateForest(Material bark, Material leaves, Material rock)
    {
        Vector3[] treePositions =
        {
            new Vector3(-16f, 0f, -24f), new Vector3(15f, 0f, -20f), new Vector3(-22f, 0f, -8f),
            new Vector3(19f, 0f, 4f), new Vector3(-14f, 0f, 17f), new Vector3(22f, 0f, 24f),
            new Vector3(-28f, 0f, 26f), new Vector3(28f, 0f, -28f)
        };

        foreach (Vector3 position in treePositions)
        {
            CreateTree(position, bark, leaves);
        }

        Vector3[] rockPositions =
        {
            new Vector3(-8f, 0.3f, -13f), new Vector3(10f, 0.25f, -7f), new Vector3(-11f, 0.25f, 8f),
            new Vector3(8f, 0.3f, 17f), new Vector3(17f, 0.25f, 13f)
        };

        foreach (Vector3 position in rockPositions)
        {
            GameObject rockObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rockObject.name = "Low Poly Rock";
            rockObject.transform.position = position;
            rockObject.transform.localScale = new Vector3(1.6f, 0.8f, 1.2f);
            rockObject.GetComponent<Renderer>().sharedMaterial = rock;
        }
    }

    private static void CreateTree(Vector3 position, Material bark, Material leaves)
    {
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Pine Trunk";
        trunk.transform.position = position + Vector3.up * 1.2f;
        trunk.transform.localScale = new Vector3(0.45f, 1.2f, 0.45f);
        trunk.GetComponent<Renderer>().sharedMaterial = bark;

        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        top.name = "Pine Canopy";
        top.transform.position = position + Vector3.up * 3f;
        top.transform.localScale = new Vector3(1.7f, 1.6f, 1.7f);
        top.GetComponent<Renderer>().sharedMaterial = leaves;
        Mesh mesh = top.GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            float taper = Mathf.InverseLerp(-1f, 1f, vertex.y);
            vertex.x *= 1f - taper * 0.85f;
            vertex.z *= 1f - taper * 0.85f;
            vertices[i] = vertex;
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
    }

    private void CreateCollectibles(Material material)
    {
        Vector3[] positions =
        {
            new Vector3(-5f, 1.2f, -18f),
            new Vector3(6f, 1.2f, -6f),
            new Vector3(-7f, 1.2f, 7f),
            new Vector3(5f, 1.2f, 19f),
            new Vector3(0f, 1.2f, 28f)
        };

        totalCollectibles = positions.Length;
        foreach (Vector3 position in positions)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            item.name = "Collectible Crystal";
            item.transform.position = position;
            item.transform.rotation = Quaternion.Euler(45f, 0f, 45f);
            item.transform.localScale = new Vector3(0.45f, 0.9f, 0.45f);
            item.GetComponent<Renderer>().sharedMaterial = material;
            Collider trigger = item.GetComponent<Collider>();
            trigger.isTrigger = true;
            item.AddComponent<CollectibleCrystal>().Initialize(this);
        }
    }

    private static GameObject CreatePlayer(Material material)
    {
        GameObject playerRoot = new GameObject("Player");
        playerRoot.transform.position = new Vector3(0f, 2f, -27f);
        CharacterController controller = playerRoot.AddComponent<CharacterController>();
        controller.height = 1.9f;
        controller.radius = 0.38f;
        controller.center = new Vector3(0f, 0.95f, 0f);
        playerRoot.AddComponent<SimpleThirdPersonController>();

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Player Body";
        body.transform.SetParent(playerRoot.transform);
        body.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = new Vector3(0.75f, 0.95f, 0.75f);
        body.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(body.GetComponent<Collider>());

        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Player Facing Marker";
        nose.transform.SetParent(playerRoot.transform);
        nose.transform.localPosition = new Vector3(0f, 1f, 0.45f);
        nose.transform.localScale = new Vector3(0.22f, 0.22f, 0.35f);
        nose.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(nose.GetComponent<Collider>());

        return playerRoot;
    }

    private void CreateGate(Material material)
    {
        GameObject gate = new GameObject("Ancient Gate");
        gate.transform.position = new Vector3(0f, 1.5f, 33f);

        GameObject leftPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftPost.transform.SetParent(gate.transform);
        leftPost.transform.localPosition = new Vector3(-1.5f, 0f, 0f);
        leftPost.transform.localScale = new Vector3(0.45f, 3f, 0.45f);
        leftPost.GetComponent<Renderer>().sharedMaterial = material;

        GameObject rightPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightPost.transform.SetParent(gate.transform);
        rightPost.transform.localPosition = new Vector3(1.5f, 0f, 0f);
        rightPost.transform.localScale = new Vector3(0.45f, 3f, 0.45f);
        rightPost.GetComponent<Renderer>().sharedMaterial = material;

        GameObject lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lintel.transform.SetParent(gate.transform);
        lintel.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        lintel.transform.localScale = new Vector3(3.5f, 0.45f, 0.45f);
        lintel.GetComponent<Renderer>().sharedMaterial = material;

        BoxCollider trigger = gate.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = Vector3.zero;
        trigger.size = new Vector3(5f, 4f, 5f);
        gate.AddComponent<GateInteraction>().Initialize(this);
    }

    private void SetupCamera(Transform target)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        mainCamera.transform.position = target.position + new Vector3(0f, 4f, -7f);
        mainCamera.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
        SimpleFollowCamera follow = mainCamera.gameObject.AddComponent<SimpleFollowCamera>();
        follow.SetTarget(target);
    }

    private void SetupUi()
    {
        Canvas canvas = new GameObject("Gameplay UI").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        objectiveText = CreateText(canvas.transform, "Objective Text", new Vector2(20f, -20f), TextAnchor.UpperLeft, 26);
        promptText = CreateText(canvas.transform, "Prompt Text", new Vector2(0f, 28f), TextAnchor.LowerCenter, 20);
    }

    private static Text CreateText(Transform parent, string name, Vector2 anchoredPosition, TextAnchor alignment, int size)
    {
        Text text = new GameObject(name).AddComponent<Text>();
        text.transform.SetParent(parent, false);
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : new Vector2(0.5f, 0f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = alignment == TextAnchor.UpperLeft ? new Vector2(420f, 80f) : new Vector2(760f, 80f);
        return text;
    }

    private void SetupAudio()
    {
        if (ambientClip == null)
        {
            return;
        }

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = ambientClip;
        source.loop = true;
        source.playOnAwake = true;
        source.volume = 0.35f;
        source.spatialBlend = 0f;
        source.Play();
    }
}

public sealed class SimpleThirdPersonController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float turnSpeed = 12f;
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private float gravity = -18f;

    private CharacterController controller;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(horizontal, 0f, vertical);
        input = Vector3.ClampMagnitude(input, 1f);

        Vector3 move = CameraRelativeMove(input);
        if (move.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (controller.isGrounded && Input.GetButtonDown("Jump"))
        {
            verticalVelocity = jumpForce;
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = move * moveSpeed;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private static Vector3 CameraRelativeMove(Vector3 input)
    {
        if (input.sqrMagnitude <= 0.001f || Camera.main == null)
        {
            return input;
        }

        Vector3 forward = Camera.main.transform.forward;
        Vector3 right = Camera.main.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        return forward * input.z + right * input.x;
    }
}

public sealed class SimpleFollowCamera : MonoBehaviour
{
    private Transform target;
    private readonly Vector3 offset = new Vector3(0f, 4.2f, -7.2f);

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + target.TransformDirection(offset);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, 8f * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 1.2f);
    }
}

public sealed class CollectibleCrystal : MonoBehaviour
{
    private NatureGameBootstrap game;
    private float startY;

    public void Initialize(NatureGameBootstrap bootstrap)
    {
        game = bootstrap;
        startY = transform.position.y;
    }

    private void Update()
    {
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        Vector3 position = transform.position;
        position.y = startY + Mathf.Sin(Time.time * 2.4f) * 0.25f;
        transform.position = position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<CharacterController>() != null)
        {
            game.Collect(gameObject);
        }
    }
}

public sealed class GateInteraction : MonoBehaviour
{
    private NatureGameBootstrap game;
    private bool playerNearby;
    private bool opened;

    public void Initialize(NatureGameBootstrap bootstrap)
    {
        game = bootstrap;
    }

    private void Update()
    {
        if (!playerNearby || opened || !Input.GetKeyDown(KeyCode.E))
        {
            return;
        }

        if (!game.HasAllCollectibles())
        {
            game.SetPrompt("Collect all crystals before opening the gate.");
            return;
        }

        opened = true;
        transform.position += Vector3.up * 4f;
        game.SetPrompt("Gate opened. Prototype objective complete.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<CharacterController>() == null || opened)
        {
            return;
        }

        playerNearby = true;
        game.SetPrompt(game.HasAllCollectibles()
            ? "Press E to open the gate."
            : "The gate needs every crystal.");
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<CharacterController>() == null || opened)
        {
            return;
        }

        playerNearby = false;
        game.SetPrompt("Collect crystals and follow the trail.");
    }
}
