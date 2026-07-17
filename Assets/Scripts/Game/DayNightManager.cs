using UnityEngine;
using UnityEngine.Rendering;

public sealed class DayNightManager : MonoBehaviour
{
    [Header("Cycle")]
    [SerializeField, Min(1f)] private float cycleSeconds = 5f;
    [SerializeField, Range(0f, 1f)] private float startingTimeOfDay = 0.25f;
    [SerializeField] private bool runCycle = true;

    [Header("Lighting")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Gradient sunColor;
    [SerializeField] private Gradient ambientColor;
    [SerializeField] private Gradient fogColor;
    [SerializeField, Range(0f, 2f)] private float daySunIntensity = 1.15f;
    [SerializeField, Range(0f, 1f)] private float nightSunIntensity = 0.08f;
    [SerializeField, Range(0f, 0.1f)] private float dayFogDensity = 0.006f;
    [SerializeField, Range(0f, 0.1f)] private float nightFogDensity = 0.018f;

    [Header("Audio")]
    [SerializeField] private bool playDayNightAmbience = true;
    [SerializeField] private AudioClip dayAmbienceClip;
    [SerializeField] private AudioClip nightAmbienceClip;
    [SerializeField, Range(0f, 1f)] private float dayAmbienceVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float nightAmbienceVolume = 0.25f;
    [SerializeField] private bool playBackgroundMusic = true;
    [SerializeField] private AudioClip backgroundMusicClip;
    [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 0.18f;

    [Header("Debug")]
    [SerializeField, Range(0f, 1f)] private float currentTimeOfDay;

    private AudioSource dayAmbienceSource;
    private AudioSource nightAmbienceSource;
    private AudioSource musicSource;

    public float CycleSeconds
    {
        get => cycleSeconds;
        set => cycleSeconds = Mathf.Max(1f, value);
    }

    public bool IsNight => GetDayAmount(currentTimeOfDay) < 0.5f;

    private void Reset()
    {
        sunLight = FindAnyObjectByType<Light>();
        cycleSeconds = 5f;
        startingTimeOfDay = 0.25f;
        runCycle = true;
        playDayNightAmbience = true;
        playBackgroundMusic = true;
        dayAmbienceVolume = 0.6f;
        nightAmbienceVolume = 0.25f;
        backgroundMusicVolume = 0.18f;
        daySunIntensity = 1.15f;
        nightSunIntensity = 0.08f;
        dayFogDensity = 0.006f;
        nightFogDensity = 0.018f;
        ConfigureDefaultGradients();
    }

    private void Awake()
    {
        if (sunLight == null)
        {
            sunLight = FindAnyObjectByType<Light>();
        }

        if (sunColor == null || sunColor.colorKeys.Length == 0)
        {
            ConfigureDefaultGradients();
        }

        currentTimeOfDay = startingTimeOfDay;
        CreateAudioSources();
        ApplyCycleState();
    }

    private void Update()
    {
        if (runCycle && cycleSeconds > 0f)
        {
            currentTimeOfDay = Mathf.Repeat(currentTimeOfDay + Time.deltaTime / cycleSeconds, 1f);
        }

        ApplyCycleState();
    }

    private void OnValidate()
    {
        cycleSeconds = Mathf.Max(1f, cycleSeconds);
        startingTimeOfDay = Mathf.Repeat(startingTimeOfDay, 1f);
        currentTimeOfDay = Mathf.Repeat(currentTimeOfDay, 1f);

        if (sunColor == null || sunColor.colorKeys.Length == 0)
        {
            ConfigureDefaultGradients();
        }
    }

    private void CreateAudioSources()
    {
        dayAmbienceSource = CreateLoopingSource("Day Ambience Source", dayAmbienceClip);
        nightAmbienceSource = CreateLoopingSource("Night Ambience Source", nightAmbienceClip);
        musicSource = CreateLoopingSource("Background Music Source", backgroundMusicClip);

        if (dayAmbienceSource != null)
        {
            dayAmbienceSource.Play();
        }

        if (nightAmbienceSource != null)
        {
            nightAmbienceSource.Play();
        }

        if (musicSource != null)
        {
            musicSource.Play();
        }
    }

    private AudioSource CreateLoopingSource(string objectName, AudioClip clip)
    {
        GameObject sourceObject = new GameObject(objectName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        return source;
    }

    private void ApplyCycleState()
    {
        float dayAmount = GetDayAmount(currentTimeOfDay);
        bool isNight = dayAmount < 0.5f;

        if (sunLight != null)
        {
            sunLight.transform.rotation = Quaternion.Euler(currentTimeOfDay * 360f - 90f, 170f, 0f);
            sunLight.color = sunColor.Evaluate(currentTimeOfDay);
            sunLight.intensity = Mathf.Lerp(nightSunIntensity, daySunIntensity, dayAmount);
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor.Evaluate(currentTimeOfDay);
        RenderSettings.fog = true;
        RenderSettings.fogColor = fogColor.Evaluate(currentTimeOfDay);
        RenderSettings.fogDensity = Mathf.Lerp(nightFogDensity, dayFogDensity, dayAmount);

        if (dayAmbienceSource != null)
        {
            dayAmbienceSource.volume = playDayNightAmbience && !isNight ? dayAmbienceVolume : 0f;
        }

        if (nightAmbienceSource != null)
        {
            nightAmbienceSource.volume = playDayNightAmbience && isNight ? nightAmbienceVolume : 0f;
        }

        if (musicSource != null)
        {
            musicSource.volume = playBackgroundMusic ? backgroundMusicVolume : 0f;
        }
    }

    private static float GetDayAmount(float timeOfDay)
    {
        float sunHeight = Mathf.Sin(timeOfDay * Mathf.PI * 2f);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f, 0.35f, sunHeight));
    }

    private void ConfigureDefaultGradients()
    {
        sunColor = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.13f, 0.18f, 0.35f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.32f), 0.18f),
                new GradientColorKey(new Color(1f, 0.95f, 0.78f), 0.5f),
                new GradientColorKey(new Color(1f, 0.48f, 0.28f), 0.82f),
                new GradientColorKey(new Color(0.13f, 0.18f, 0.35f), 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        };

        ambientColor = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.08f, 0.10f, 0.18f), 0f),
                new GradientColorKey(new Color(0.38f, 0.34f, 0.28f), 0.2f),
                new GradientColorKey(new Color(0.62f, 0.68f, 0.58f), 0.5f),
                new GradientColorKey(new Color(0.33f, 0.28f, 0.32f), 0.82f),
                new GradientColorKey(new Color(0.08f, 0.10f, 0.18f), 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        };

        fogColor = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.04f, 0.05f, 0.12f), 0f),
                new GradientColorKey(new Color(0.72f, 0.48f, 0.38f), 0.2f),
                new GradientColorKey(new Color(0.62f, 0.79f, 0.95f), 0.5f),
                new GradientColorKey(new Color(0.44f, 0.30f, 0.40f), 0.82f),
                new GradientColorKey(new Color(0.04f, 0.05f, 0.12f), 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        };
    }
}
