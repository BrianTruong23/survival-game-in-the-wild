using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CompassMarkerBar : MonoBehaviour
{

    // What the compass arrow currently points at. Press U to cycle through these.
    private enum TrackTarget
    {
        Enemy,
        Proton,
        Electron,
        Vehicle
    }

    [SerializeField] private Transform target;
    [SerializeField] private RectTransform marker;
    [SerializeField] private Text headingText;
    [SerializeField] private Text modeLabel;

    // How quickly the arrow eases toward the new heading (higher = snappier). This
    // stops the arrow from teleporting to a new angle every frame, which read as
    // "sudden" flicking when the player turned or the tracked target changed.
    [SerializeField, Range(1f, 20f)] private float rotationSmoothing = 10f;

    private TrackTarget trackTarget = TrackTarget.Enemy;
    private Component nearestTracked;
    private float nextSearchTime;
    private float smoothedHeading;

    public void Initialize(Transform targetTransform, RectTransform markerTransform, Text headingLabel, Text modeLabelText = null)
    {
        target = targetTransform;
        marker = markerTransform;
        headingText = headingLabel;
        modeLabel = modeLabelText;
        UpdateModeLabel();
        UpdateHeading();
    }

    private void Update()
    {
        if (WasCycleTargetKeyPressed())
        {
            CycleTrackTarget();
        }

        UpdateHeading();
    }

    // Cycles Enemy -> Proton -> Electron -> Vehicle -> Enemy ... and forces an
    // immediate re-search so the arrow doesn't keep pointing at the stale,
    // previously-tracked kind.
    private void CycleTrackTarget()
    {
        trackTarget = trackTarget switch
        {
            TrackTarget.Enemy => TrackTarget.Proton,
            TrackTarget.Proton => TrackTarget.Electron,
            TrackTarget.Electron => TrackTarget.Vehicle,
            _ => TrackTarget.Enemy
        };

        nearestTracked = null;
        nextSearchTime = 0f;
        UpdateModeLabel();
    }

    private void UpdateModeLabel()
    {
        if (modeLabel != null)
        {
            modeLabel.text = $"Nearest {GetTrackTargetLabel()} (U to cycle)";
        }
    }

    private string GetTrackTargetLabel()
    {
        return trackTarget switch
        {
            TrackTarget.Enemy => "Enemy",
            TrackTarget.Proton => "Proton",
            TrackTarget.Electron => "Electron",
            _ => "Vehicle"
        };
    }

    private void UpdateHeading()
    {
        if (target == null)
        {
            return;
        }

        Transform tracked = GetNearestTracked();
        float rawHeading = tracked != null ? GetRelativeHeading(tracked) : smoothedHeading;

        // Ease toward the raw heading instead of snapping straight to it every frame -
        // frame-rate independent exponential smoothing.
        float t = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
        smoothedHeading = Mathf.Repeat(Mathf.LerpAngle(smoothedHeading, rawHeading, t), 360f);

        if (marker != null)
        {
            marker.localRotation = Quaternion.Euler(0f, 0f, -smoothedHeading);
        }

        if (headingText != null)
        {
            string label = GetTrackTargetLabel();
            if (tracked != null)
            {
                float distance = Vector3.Distance(target.position, tracked.position);
                headingText.text = $"{label} {Mathf.RoundToInt(distance)}m · {GetDirectionWord(rawHeading)}";
            }
            else
            {
                headingText.text = $"{label} --";
            }
        }
    }

    // Buckets a 0-360 relative heading into a plain-language direction so the
    // player doesn't have to eyeball a small arrow tilt to know which way to turn.
    private static string GetDirectionWord(float heading)
    {
        int sector = Mathf.RoundToInt(heading / 45f) % 8;
        return sector switch
        {
            0 => "Ahead",
            1 => "Ahead-Right",
            2 => "Right",
            3 => "Behind-Right",
            4 => "Behind",
            5 => "Behind-Left",
            6 => "Left",
            _ => "Ahead-Left"
        };
    }

    // True lock: once we're pointing at a specific enemy/proton/electron, keep
    // pointing at that exact object - never swap to a different one just because it
    // became closer. We only pick a new target when the locked one is gone
    // (collected/destroyed) or the player cycles tracking mode with U.
    private Transform GetNearestTracked()
    {
        if (nearestTracked != null)
        {
            return nearestTracked.transform;
        }

        if (Time.time < nextSearchTime)
        {
            return null;
        }

        nextSearchTime = Time.time + 0.25f;
        nearestTracked = trackTarget switch
        {
            TrackTarget.Enemy => FindNearest<EnemyDamage>(),
            TrackTarget.Proton => FindNearest<ProtonCollectible>(),
            TrackTarget.Electron => FindNearest<ElectronCollectible>(),
            _ => FindNearest<VehicleController>()
        };

        return nearestTracked != null ? nearestTracked.transform : null;
    }

    private T FindNearest<T>() where T : Component
    {

        #if UNITY_6000_0_OR_NEWER
                T[] candidates = FindObjectsByType<T>(FindObjectsInactive.Exclude);
        #else
                T[] candidates = FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        #endif
        float bestDistance = float.PositiveInfinity;
        T nearest = null;

        foreach (T candidate in candidates)
        {
            float distance = (candidate.transform.position - target.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private float GetRelativeHeading(Transform trackedTransform)
    {
        Vector3 direction = trackedTransform.position - target.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            return 0f;
        }

        float worldHeading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        return Mathf.Repeat(worldHeading - target.eulerAngles.y, 360f);
    }

    private static bool WasCycleTargetKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return Input.GetKeyDown(KeyCode.U);
    }
}
