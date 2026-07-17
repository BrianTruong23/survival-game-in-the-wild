using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor.Events;
using UnityEngine.Events;

public class AutoSetup
{
    [MenuItem("Tools/Setup Game Additions")]
    public static void Run()
    {
        Debug.Log("Starting AutoSetup...");

        // 1. Setup MainScene
        string mainScenePath = "Assets/Scenes/MainScene.unity";
        var mainScene = EditorSceneManager.OpenScene(mainScenePath);

        // -- Player Setup
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            player = GameObject.Find("PlayerCapsule");
        }
        if (player != null)
        {
            Debug.Log("Found Player: " + player.name);
            var ph = player.GetComponent<PlayerHealth>();
            if (ph == null) ph = player.AddComponent<PlayerHealth>();

            // -- UI Setup
            GameObject canvasObj = GameObject.Find("HealthCanvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("HealthCanvas");
                var canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            GameObject healthGroup = GameObject.Find("HealthGroup");
            if (healthGroup == null)
            {
                healthGroup = new GameObject("HealthGroup");
                healthGroup.transform.SetParent(canvasObj.transform, false);
            }
            var rectGroup = healthGroup.GetComponent<RectTransform>();
            if (rectGroup == null) rectGroup = healthGroup.AddComponent<RectTransform>();
            rectGroup.anchorMin = new Vector2(0, 1);
            rectGroup.anchorMax = new Vector2(0, 1);
            rectGroup.pivot = new Vector2(0, 1);
            rectGroup.anchoredPosition = new Vector2(20, -100); // Moved below the Weapon text
            rectGroup.sizeDelta = new Vector2(150, 40);

            var hlg = healthGroup.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = healthGroup.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.spacing = 15;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            ph.healthIcons = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                string iconName = "HealthIcon" + i;
                Transform existing = healthGroup.transform.Find(iconName);
                GameObject iconObj = existing != null ? existing.gameObject : new GameObject(iconName);
                iconObj.transform.SetParent(healthGroup.transform, false);
                var img = iconObj.GetComponent<Image>();
                if (img == null) img = iconObj.AddComponent<Image>();
                img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                img.color = new Color(0.9f, 0.1f, 0.2f);
                var rect = iconObj.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(24, 24); // Small round icons
                ph.healthIcons[i] = img;
            }
        }
        else
        {
            Debug.LogWarning("Player not found!");
        }

        // -- Enemies Setup
        // GameScoreManager now owns enemy population at runtime: it keeps exactly
        // one enemy alive at a time and spawns a replacement within 5m of the
        // player the instant the current one is killed. So here we just make sure
        // the "Enemies" hierarchy starts empty (no stale pre-baked enemies) and
        // wire the Husky/Wolf prefabs into GameScoreManager's enemyPrefabs field
        // so it has something to instantiate at runtime.
        GameObject enemiesParent = GameObject.Find("Enemies");
        if (enemiesParent == null) enemiesParent = new GameObject("Enemies");

        for (int i = enemiesParent.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(enemiesParent.transform.GetChild(i).gameObject);
        }

        GameScoreManager scoreManager = Object.FindAnyObjectByType<GameScoreManager>();
        if (scoreManager != null)
        {
            var husky = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/Husky.obj");
            var wolf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/Wolf.obj");

            var so = new SerializedObject(scoreManager);
            var prop = so.FindProperty("enemyPrefabs");
            if (prop != null)
            {
                prop.ClearArray();
                int idx = 0;
                foreach (var prefab in new[] { husky, wolf })
                {
                    if (prefab == null) continue;
                    prop.InsertArrayElementAtIndex(idx);
                    prop.GetArrayElementAtIndex(idx).objectReferenceValue = prefab;
                    idx++;
                }
                so.ApplyModifiedProperties();
            }
        }
        else
        {
            Debug.LogWarning("AutoSetup: no GameScoreManager found in scene - couldn't wire up enemyPrefabs.");
        }

        // -- NPCs Setup
        GameObject npcsParent = GameObject.Find("NPCs");
        if (npcsParent == null) npcsParent = new GameObject("NPCs");

        // Clear old NPCs so we can spawn them in the new correct locations
        for (int i = npcsParent.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(npcsParent.transform.GetChild(i).gameObject);
        }

        string[] npcPaths = { "Assets/NPC/Male_Suit.obj", "Assets/NPC/Smooth_Male_Casual.obj", "Assets/NPC/Male_Suit.obj" };
        foreach (var path in npcPaths)
        {
            Vector3 pos = new Vector3(789f + Random.Range(-8f, 8f), 0, 582f + Random.Range(-8f, 8f));
            pos.y = GetGroundHeight(pos);

            SpawnPrefab(path, npcsParent.transform, (go) => {
                // Solid collision body
                var physCol = go.GetComponent<CapsuleCollider>();
                if (physCol == null) physCol = go.AddComponent<CapsuleCollider>();
                physCol.height = 2f;
                physCol.radius = 0.4f;
                physCol.center = new Vector3(0, 1f, 0);
                physCol.isTrigger = false;

                // Trigger for talking
                var bc = go.GetComponent<BoxCollider>();
                if (bc == null) bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(4, 4, 4);
                bc.center = new Vector3(0, 1, 0);

                var rb = go.GetComponent<Rigidbody>();
                if (rb == null) rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

                // Remove old NPCInteract if it exists
                var oldNi = go.GetComponent<NPCInteract>();
                if (oldNi != null) Object.DestroyImmediate(oldNi);

                var ni = go.GetComponent<NpcDialogue>();
                if (ni == null) ni = go.AddComponent<NpcDialogue>();
                
                var wai = go.GetComponent<WanderAI>();
                if (wai == null) wai = go.AddComponent<WanderAI>();

                // Make them a bit smaller to match player size
                go.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            }, pos);
        }

        // -- Vehicles Setup
        // GameScoreManager now owns the vehicle the same way it owns the enemy:
        // it spawns exactly one at runtime (Awake -> SpawnVehicle) within
        // vehicleSpawnRadius of the player, using vehiclePrefab if assigned or
        // a procedural fallback car otherwise - so a vehicle always exists even
        // if this menu command hasn't been re-run. Here we just clear any
        // stale pre-baked vehicles from older versions of this tool and wire
        // the RedCar prefab into vehiclePrefab so the real model gets used.
        GameObject vehiclesParent = GameObject.Find("Vehicles");
        if (vehiclesParent == null) vehiclesParent = new GameObject("Vehicles");

        for (int i = vehiclesParent.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(vehiclesParent.transform.GetChild(i).gameObject);
        }

        if (scoreManager != null)
        {
            var redCar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vehicles/RedCar.obj");
            if (redCar != null)
            {
                var vehicleSo = new SerializedObject(scoreManager);
                var vehicleProp = vehicleSo.FindProperty("vehiclePrefab");
                if (vehicleProp != null)
                {
                    vehicleProp.objectReferenceValue = redCar;
                    vehicleSo.ApplyModifiedProperties();
                }
            }
        }
        else
        {
            Debug.LogWarning("AutoSetup: no GameScoreManager found in scene - couldn't wire up vehiclePrefab.");
        }

        EditorSceneManager.SaveScene(mainScene);

        // 2. Setup StartScene
        string startScenePath = "Assets/Scenes/StartScene.unity";
        MakeMenuScene(mainScenePath, startScenePath, "StartGame", "Play Game");

        // 3. Setup RestartScene
        string restartScenePath = "Assets/Scenes/RestartScene.unity";
        MakeMenuScene(mainScenePath, restartScenePath, "RestartGame", "Restart Game");

        // 4. Update Build Settings
        var scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(startScenePath, true),
            new EditorBuildSettingsScene(mainScenePath, true),
            new EditorBuildSettingsScene(restartScenePath, true)
        };
        EditorBuildSettings.scenes = scenes;

        // 5. Force Play Button to always start from StartScene
        var startSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(startScenePath);
        if (startSceneAsset != null)
        {
            EditorSceneManager.playModeStartScene = startSceneAsset;
        }

        Debug.Log("AutoSetup Completed successfully.");
    }

    private static float GetGroundHeight(Vector3 position)
    {
        if (Physics.Raycast(new Vector3(position.x, 200f, position.z), Vector3.down, out RaycastHit hit, 300f))
        {
            return hit.point.y;
        }
        return 50f;
    }

    private static Vector3 GetFarSpawnPos(Vector3 center, float minRadius, float maxRadius)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Random.Range(minRadius, maxRadius);
        return center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
    }

    private static void SpawnPrefab(string assetPath, Transform parent, System.Action<GameObject> onSpawn, Vector3 position)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // Append a random ID to the name to allow multiple of the same type
            go.name = prefab.name + "_" + Random.Range(1000, 9999);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            onSpawn?.Invoke(go);
        }
        else
        {
            Debug.LogWarning("Could not find asset: " + assetPath);
        }
    }

    private static void MakeMenuScene(string sourcePath, string targetPath, string methodName, string buttonText)
    {
        AssetDatabase.DeleteAsset(targetPath);
        AssetDatabase.CopyAsset(sourcePath, targetPath);
        var menuScene = EditorSceneManager.OpenScene(targetPath);

        // Delete player so they don't walk around in the menu
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) player = GameObject.Find("PlayerCapsule");
        if (player != null) Object.DestroyImmediate(player);
        
        GameObject nestedPlayer = GameObject.Find("NestedParentArmature_Unpack");
        if (nestedPlayer != null) Object.DestroyImmediate(nestedPlayer);

        // Setup a static camera
        GameObject camObj = GameObject.Find("MainCamera");
        if (camObj == null) camObj = GameObject.FindWithTag("MainCamera");
        if (camObj != null)
        {
            // Remove Cinemachine Brain so it stops trying to follow the deleted player
            var brain = camObj.GetComponent("CinemachineBrain");
            if (brain != null) Object.DestroyImmediate(brain);
            
            // Set a nice scenic view looking at the terrain
            camObj.transform.position = new Vector3(789f, 65f, 570f);
            camObj.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
        }
        else
        {
            camObj = new GameObject("MainCamera");
            camObj.tag = "MainCamera";
            var cam = camObj.AddComponent<Camera>();
            camObj.transform.position = new Vector3(789f, 65f, 570f);
            camObj.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
        }

        // Clean up old canvas/gm if they copied over
        GameObject oldGm = GameObject.Find("GameManager");
        if (oldGm != null) Object.DestroyImmediate(oldGm);
        GameObject oldCanvas = GameObject.Find("HealthCanvas");
        if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);

        SetupUIButtonsScene(menuScene, methodName, buttonText);
        EditorSceneManager.SaveScene(menuScene, targetPath);
    }

    private static void SetupUIButtonsScene(Scene scene, string methodName, string buttonTextStr)
    {
        // GameManager
        var gmObj = new GameObject("GameManager");
        var gm = gmObj.AddComponent<GameManager>();

        // Canvas
        var canvasObj = new GameObject("Canvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Button (Legacy Text)
        // Creating standard UI Button
        var btnObj = new GameObject("Button");
        btnObj.transform.SetParent(canvasObj.transform, false);
        var rect = btnObj.AddComponent<RectTransform>();
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(200, 50);
        var img = btnObj.AddComponent<Image>();
        var btn = btnObj.AddComponent<Button>();

        // Text
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<Text>();
        text.text = buttonTextStr;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        // Require font
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Link button click
        if (methodName == "StartGame")
        {
            UnityEventTools.AddPersistentListener(btn.onClick, new UnityAction(gm.StartGame));
        }
        else
        {
            UnityEventTools.AddPersistentListener(btn.onClick, new UnityAction(gm.RestartGame));
        }
    }
}
