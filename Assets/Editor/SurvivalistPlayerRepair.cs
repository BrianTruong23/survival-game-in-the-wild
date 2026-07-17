using StarterAssets;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public static class SurvivalistPlayerRepair
{
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string ControllerPrefabPath = "Assets/Survivalist/Prefab/PlayerArmatureSurvivalist.prefab";
    private const string VisualSourcePrefabPath = "Assets/Survivalist/Prefab/Survivalist (4).prefab";
    private const string DebugMaterialPath = "Assets/Survivalist/Materials/DebugVisibleSurvivalist.mat";
    private const string InputActionsPath = "Assets/StarterAssets/InputSystem/StarterAssets.inputactions";
    private static readonly Vector3 PlayerStart = new Vector3(789.0934f, 49f, 582f);

    [MenuItem("Tools/Fix Survivalist Player")]
    public static void RepairProject()
    {
        EnsureControllerPrefabHasVisuals();
        RepairMainScenePlayer();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Survivalist player repair complete.");
    }

    [MenuItem("Tools/Rebuild Survivalist Player From Visible Mesh")]
    public static void RebuildFromVisibleMesh()
    {
        EditorSceneManager.OpenScene(MainScenePath);

        GameObject oldPlayer = GameObject.Find("PlayerArmatureSurvivalist");
        GameObject visualSource = AssetDatabase.LoadAssetAtPath<GameObject>(VisualSourcePrefabPath);
        GameObject controllerSource = AssetDatabase.LoadAssetAtPath<GameObject>(ControllerPrefabPath);
        if (visualSource == null || controllerSource == null)
        {
            Debug.LogError("Missing Survivalist player source prefabs.");
            return;
        }

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(visualSource);
        player.name = "PlayerArmatureSurvivalist";
        player.tag = "Player";
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            SetLayerRecursively(player, playerLayer);
        }

        player.transform.SetParent(null, false);
        player.transform.position = GetGroundedStartPosition();
        player.transform.rotation = Quaternion.identity;
        player.transform.localScale = Vector3.one;

        GameObject controllerTemplate = (GameObject)PrefabUtility.InstantiatePrefab(controllerSource);
        try
        {
            CopyOrAddComponent<CharacterController>(controllerTemplate, player);
            CopyOrAddComponent<StarterAssetsInputs>(controllerTemplate, player);
            CopyOrAddComponent<PlayerInput>(controllerTemplate, player);
            CopyOrAddComponent<ThirdPersonController>(controllerTemplate, player);

            PlayerHealth health = CopyOrAddComponent<PlayerHealth>(controllerTemplate, player);
            if (oldPlayer != null)
            {
                PlayerHealth oldHealth = oldPlayer.GetComponent<PlayerHealth>();
                if (oldHealth != null)
                {
                    EditorUtility.CopySerialized(oldHealth, health);
                }
            }

            Transform cameraRoot = player.transform.Find("PlayerCameraRoot");
            if (cameraRoot == null)
            {
                GameObject cameraRootObject = new GameObject("PlayerCameraRoot");
                cameraRootObject.tag = "CinemachineTarget";
                cameraRootObject.transform.SetParent(player.transform, false);
                cameraRootObject.transform.localPosition = new Vector3(0f, 1.375f, 0f);
                cameraRoot = cameraRootObject.transform;
            }

            ThirdPersonController thirdPersonController = player.GetComponent<ThirdPersonController>();
            thirdPersonController.CinemachineCameraTarget = cameraRoot.gameObject;
            thirdPersonController.GroundedOffset = -0.14f;
            thirdPersonController.GroundedRadius = 0.28f;
            thirdPersonController.GroundLayers = LayerMask.GetMask("Default");

            Animator animator = player.GetComponent<Animator>();
            Animator templateAnimator = controllerTemplate.GetComponent<Animator>();
            if (animator != null && templateAnimator != null)
            {
                animator.avatar = templateAnimator.avatar;
                animator.runtimeAnimatorController = templateAnimator.runtimeAnimatorController;
                animator.applyRootMotion = false;
            }

            CharacterController characterController = player.GetComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.28f;
            characterController.center = new Vector3(0f, 0.93f, 0f);
            characterController.stepOffset = 0.25f;
            characterController.skinWidth = 0.02f;

            foreach (SkinnedMeshRenderer renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                ActivateRenderer(renderer);
            }

            ConfigureFollowCamera(cameraRoot);
        }
        finally
        {
            Object.DestroyImmediate(controllerTemplate);
        }

        if (oldPlayer != null)
        {
            Object.DestroyImmediate(oldPlayer);
        }

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Rebuilt Survivalist player from visible mesh. Renderer count: " + player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length);
    }

    [MenuItem("Tools/Restore Survivalist Original Materials")]
    public static void RestoreOriginalMaterials()
    {
        EditorSceneManager.OpenScene(MainScenePath);

        GameObject player = GameObject.Find("PlayerArmatureSurvivalist");
        if (player == null)
        {
            Debug.LogError("No PlayerArmatureSurvivalist found in MainScene.");
            return;
        }

        int restoredCount = RestoreMaterialsFromSourcePrefab(player);
        RemoveDebugMarker(player.transform);

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Restored Survivalist original materials. Renderer count: " + restoredCount);
    }

    [MenuItem("Tools/Force Survivalist Mesh Visible")]
    public static void ForceMeshVisible()
    {
        EditorSceneManager.OpenScene(MainScenePath);

        GameObject player = GameObject.Find("PlayerArmatureSurvivalist");
        if (player == null)
        {
            Debug.LogError("No PlayerArmatureSurvivalist found in MainScene.");
            return;
        }

        int rendererCount = ForceVisibleRenderers(player);
        EnsureDebugMarker(player.transform);
        FrameSceneView(player.transform.position);

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Forced Survivalist mesh visible. Renderer count: " + rendererCount + ". A magenta capsule marker was added at the player position.");
    }

    private static void EnsureControllerPrefabHasVisuals()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ControllerPrefabPath);
        try
        {
            EnsureVisualChild(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, ControllerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void RepairMainScenePlayer()
    {
        EditorSceneManager.OpenScene(MainScenePath);

        GameObject player = GameObject.Find("PlayerArmatureSurvivalist");
        if (player == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ControllerPrefabPath);
            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "PlayerArmatureSurvivalist";
        }

        player.tag = "Player";
        player.layer = LayerMask.NameToLayer("Player") >= 0 ? LayerMask.NameToLayer("Player") : player.layer;
        player.transform.SetParent(null, true);
        player.transform.position = GetGroundedStartPosition();
        player.transform.rotation = Quaternion.identity;
        player.transform.localScale = Vector3.one;
        EnsureVisualChild(player);
        ConfigurePlayerInput(player);

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.height = 1.8f;
            controller.radius = 0.28f;
            controller.center = new Vector3(0f, 0.93f, 0f);
            controller.stepOffset = 0.25f;
            controller.skinWidth = 0.02f;
        }

        ThirdPersonController thirdPersonController = player.GetComponent<ThirdPersonController>();
        Transform cameraRoot = player.transform.Find("PlayerCameraRoot");
        if (thirdPersonController != null)
        {
            thirdPersonController.GroundedOffset = -0.14f;
            thirdPersonController.GroundedRadius = 0.28f;
            thirdPersonController.GroundLayers = LayerMask.GetMask("Default");
            thirdPersonController.CinemachineCameraTarget = cameraRoot != null ? cameraRoot.gameObject : null;
        }

        if (player.GetComponent<PlayerHealth>() == null)
        {
            player.AddComponent<PlayerHealth>();
        }

        ConfigureFollowCamera(cameraRoot);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    private static void ConfigurePlayerInput(GameObject player)
    {
        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (inputActions == null)
        {
            Debug.LogError("Missing input actions asset: " + InputActionsPath);
            return;
        }

        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            playerInput = player.AddComponent<PlayerInput>();
        }

        playerInput.actions = inputActions;
        playerInput.defaultActionMap = "Player";
        playerInput.notificationBehavior = PlayerNotifications.SendMessages;
    }

    private static void EnsureVisualChild(GameObject player)
    {
        Transform existingVisual = player.transform.Find("SurvivalistVisual");
        GameObject visual;
        if (existingVisual != null)
        {
            visual = existingVisual.gameObject;
        }
        else
        {
            GameObject visualSource = AssetDatabase.LoadAssetAtPath<GameObject>(VisualSourcePrefabPath);
            if (visualSource == null)
            {
                Debug.LogError("Missing visual source prefab: " + VisualSourcePrefabPath);
                return;
            }

            visual = (GameObject)PrefabUtility.InstantiatePrefab(visualSource, player.transform);
            visual.name = "SurvivalistVisual";
        }

        visual.SetActive(true);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        Animator rootAnimator = player.GetComponent<Animator>();
        Animator visualAnimator = visual.GetComponent<Animator>();
        if (rootAnimator != null && visualAnimator != null)
        {
            visualAnimator.avatar = rootAnimator.avatar;
            visualAnimator.runtimeAnimatorController = rootAnimator.runtimeAnimatorController;
            visualAnimator.applyRootMotion = false;
            Object.DestroyImmediate(rootAnimator);
        }

        foreach (SkinnedMeshRenderer renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            ActivateRenderer(renderer);
        }
    }

    private static Vector3 GetGroundedStartPosition()
    {
        Vector3 rayStart = PlayerStart + Vector3.up * 200f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 500f, LayerMask.GetMask("Default")))
        {
            return hit.point + Vector3.up * 0.05f;
        }

        return PlayerStart;
    }

    private static void ConfigureFollowCamera(Transform cameraRoot)
    {
        if (cameraRoot == null)
        {
            return;
        }

        GameObject followCamera = GameObject.Find("PlayerFollowCamera");
        if (followCamera == null)
        {
            return;
        }

        foreach (Component component in followCamera.GetComponents<Component>())
        {
            string typeName = component.GetType().Name;
            if (typeName != "CinemachineVirtualCamera" && typeName != "CinemachineCamera")
            {
                continue;
            }

            SerializedObject serializedObject = new SerializedObject(component);
            SetObjectReference(serializedObject, "m_Follow", cameraRoot);
            SetObjectReference(serializedObject, "m_LookAt", cameraRoot);
            SetObjectReference(serializedObject, "Target.TrackingTarget", cameraRoot);
            SetObjectReference(serializedObject, "Target.LookAtTarget", cameraRoot);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static T CopyOrAddComponent<T>(GameObject source, GameObject destination) where T : Component
    {
        T destinationComponent = destination.GetComponent<T>();
        if (destinationComponent == null)
        {
            destinationComponent = destination.AddComponent<T>();
        }

        T sourceComponent = source.GetComponent<T>();
        if (sourceComponent != null)
        {
            EditorUtility.CopySerialized(sourceComponent, destinationComponent);
        }

        return destinationComponent;
    }

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static int ForceVisibleRenderers(GameObject root)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            transform.gameObject.SetActive(true);
        }

        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            ForceRendererVisible(renderer);
        }

        return renderers.Length;
    }

    private static int RestoreMaterialsFromSourcePrefab(GameObject targetPlayer)
    {
        GameObject visualSource = AssetDatabase.LoadAssetAtPath<GameObject>(VisualSourcePrefabPath);
        if (visualSource == null)
        {
            Debug.LogError("Missing visual source prefab: " + VisualSourcePrefabPath);
            return 0;
        }

        Dictionary<string, Material[]> sourceMaterialsByRendererName = new Dictionary<string, Material[]>();
        foreach (SkinnedMeshRenderer sourceRenderer in visualSource.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!sourceMaterialsByRendererName.ContainsKey(sourceRenderer.name))
            {
                sourceMaterialsByRendererName.Add(sourceRenderer.name, sourceRenderer.sharedMaterials);
            }
        }

        int restoredCount = 0;
        foreach (SkinnedMeshRenderer targetRenderer in targetPlayer.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!sourceMaterialsByRendererName.TryGetValue(targetRenderer.name, out Material[] sourceMaterials))
            {
                continue;
            }

            targetRenderer.sharedMaterials = sourceMaterials;
            ActivateRenderer(targetRenderer);
            restoredCount++;
        }

        return restoredCount;
    }

    private static void RemoveDebugMarker(Transform player)
    {
        Transform marker = player.Find("DebugVisiblePlayerMarker");
        if (marker != null)
        {
            Object.DestroyImmediate(marker.gameObject);
        }
    }

    private static void ForceRendererVisible(SkinnedMeshRenderer renderer)
    {
        ActivateRenderer(renderer);

        Material debugMaterial = GetDebugMaterial();
        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            renderer.sharedMaterial = debugMaterial;
            return;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = debugMaterial;
        }

        renderer.sharedMaterials = materials;
    }

    private static void ActivateRenderer(SkinnedMeshRenderer renderer)
    {
        renderer.gameObject.SetActive(true);
        renderer.enabled = true;
        renderer.updateWhenOffscreen = true;
        renderer.localBounds = new Bounds(Vector3.zero, new Vector3(5f, 5f, 5f));
    }

    private static Material GetDebugMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(DebugMaterialPath);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        material = new Material(shader);
        material.name = "DebugVisibleSurvivalist";
        material.color = Color.magenta;
        AssetDatabase.CreateAsset(material, DebugMaterialPath);
        return material;
    }

    private static void EnsureDebugMarker(Transform player)
    {
        Transform existing = player.Find("DebugVisiblePlayerMarker");
        GameObject marker = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        marker.name = "DebugVisiblePlayerMarker";
        marker.transform.SetParent(player, false);
        marker.transform.localPosition = new Vector3(0f, 1f, 0f);
        marker.transform.localRotation = Quaternion.identity;
        marker.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetDebugMaterial();
        }

        Collider collider = marker.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
    }

    private static void FrameSceneView(Vector3 position)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            return;
        }

        sceneView.LookAt(position + Vector3.up, Quaternion.Euler(20f, 180f, 0f), 8f);
    }
}
