using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(GameScoreManager))]
public sealed class GameScoreManagerEditor : Editor
{
    private SerializedProperty playerProperty;
    private SerializedProperty localPositionProperty;
    private SerializedProperty localEulerProperty;
    private SerializedProperty previewSizeProperty;
    private SerializedProperty prefabGunScaleProperty;
    private SerializedProperty fallbackGunScaleProperty;
    private SerializedProperty gunPrefabsProperty;
    private GameObject previewInstance;
    private GameObject previewSourcePrefab;
    private const string FallbackPreviewPath = "Assets/GunPrefab/Revolver_1.fbx";

    private void OnEnable()
    {
        playerProperty = serializedObject.FindProperty("player");
        localPositionProperty = serializedObject.FindProperty("equippedGunLocalPosition");
        localEulerProperty = serializedObject.FindProperty("equippedGunLocalEulerAngles");
        previewSizeProperty = serializedObject.FindProperty("weaponHoldGizmoSize");
        prefabGunScaleProperty = serializedObject.FindProperty("prefabGunScale");
        fallbackGunScaleProperty = serializedObject.FindProperty("fallbackGunScale");
        gunPrefabsProperty = serializedObject.FindProperty("gunPrefabs");
    }

    private void OnDisable()
    {
        DestroyPreviewInstance();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Scene View Gun Preview: select GameplaySystems, enable the Move or Rotate tool, then drag/rotate the weapon preview near the player's right hand. The handle edits Equipped Gun Local Position and Equipped Gun Local Euler Angles, so saved values are used later in Play Mode.",
            MessageType.Info);
    }

    private void OnSceneGUI()
    {
        serializedObject.Update();

        Transform holdTarget = FindHoldTarget();
        if (holdTarget == null)
        {
            return;
        }

        Vector3 localPosition = localPositionProperty.vector3Value;
        Vector3 localEuler = localEulerProperty.vector3Value;
        Vector3 worldPosition = holdTarget.TransformPoint(localPosition);
        Quaternion worldRotation = holdTarget.rotation * Quaternion.Euler(localEuler);
        float size = Mathf.Max(0.03f, previewSizeProperty.floatValue);
        float modelScale = GetPreviewModelScale();

        UpdatePreviewInstance(worldPosition, worldRotation, modelScale);
        DrawGunHandles(worldPosition, worldRotation, size);

        EditorGUI.BeginChangeCheck();
        Vector3 newWorldPosition = Handles.PositionHandle(worldPosition, worldRotation);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(target, "Move Weapon Hold Preview");
            localPositionProperty.vector3Value = holdTarget.InverseTransformPoint(newWorldPosition);
            serializedObject.ApplyModifiedProperties();
            SaveTargetChanges();
        }

        EditorGUI.BeginChangeCheck();
        Quaternion newWorldRotation = Handles.RotationHandle(worldRotation, worldPosition);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(target, "Rotate Weapon Hold Preview");
            Quaternion localRotation = Quaternion.Inverse(holdTarget.rotation) * newWorldRotation;
            localEulerProperty.vector3Value = localRotation.eulerAngles;
            serializedObject.ApplyModifiedProperties();
            SaveTargetChanges();
        }
    }

    private Transform FindHoldTarget()
    {
        Transform player = playerProperty.objectReferenceValue as Transform;
        if (player == null)
        {
            ThirdPersonController controller = Object.FindAnyObjectByType<ThirdPersonController>();
            if (controller != null)
            {
                player = controller.transform;
            }
        }

        if (player == null)
        {
            return null;
        }

        Transform hand = FindChildRecursive(player, "Right_Hand");
        return hand != null ? hand : player;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        foreach (Transform child in root)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void UpdatePreviewInstance(Vector3 position, Quaternion rotation, float modelScale)
    {
        GameObject sourcePrefab = GetPreviewSourcePrefab();
        if (sourcePrefab == null)
        {
            return;
        }

        if (previewInstance == null || previewSourcePrefab != sourcePrefab)
        {
            DestroyPreviewInstance();
            previewSourcePrefab = sourcePrefab;
            previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            previewInstance.name = "EDITOR ONLY - Gun Hold Preview";
            previewInstance.hideFlags = HideFlags.HideAndDontSave;
            foreach (Transform child in previewInstance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            foreach (Collider collider in previewInstance.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            foreach (Rigidbody body in previewInstance.GetComponentsInChildren<Rigidbody>())
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        previewInstance.transform.SetPositionAndRotation(position, rotation);
        previewInstance.transform.localScale = Vector3.one * modelScale;
    }

    private GameObject GetPreviewSourcePrefab()
    {
        if (gunPrefabsProperty != null && gunPrefabsProperty.isArray)
        {
            for (int i = 0; i < gunPrefabsProperty.arraySize; i++)
            {
                GameObject prefab = gunPrefabsProperty.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (prefab != null)
                {
                    return prefab;
                }
            }
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(FallbackPreviewPath);
    }

    private float GetPreviewModelScale()
    {
        if (GetPreviewSourcePrefab() != null)
        {
            return Mathf.Max(0.01f, prefabGunScaleProperty.floatValue);
        }

        return Mathf.Max(0.01f, fallbackGunScaleProperty.floatValue);
    }

    private void DestroyPreviewInstance()
    {
        if (previewInstance != null)
        {
            DestroyImmediate(previewInstance);
            previewInstance = null;
            previewSourcePrefab = null;
        }
    }

    private void SaveTargetChanges()
    {
        EditorUtility.SetDirty(target);
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(((GameScoreManager)target).gameObject.scene);
        }
    }

    private static void DrawGunHandles(Vector3 position, Quaternion rotation, float size)
    {
        Vector3 right = rotation * Vector3.right;
        Vector3 up = rotation * Vector3.up;
        Vector3 forward = rotation * Vector3.forward;

        Vector3 barrelEnd = position + forward * size * 2.8f;

        Handles.color = Color.red;
        Handles.DrawAAPolyLine(3f, position, position + right * size);

        Handles.color = Color.green;
        Handles.DrawAAPolyLine(3f, position, position + up * size);

        Handles.color = Color.blue;
        Handles.DrawAAPolyLine(4f, position, barrelEnd);

        Handles.Label(position + up * size * 1.6f, "Gun Hold Preview");
    }
}
