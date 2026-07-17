using UnityEngine;

// Shared procedural sound-effect synthesizer. This project has no imported
// SFX assets at all - the only precedent is PlayerGunController's gunshot,
// which builds an AudioClip in code via AudioClip.Create + a hand-rolled
// waveform rather than shipping a .wav. This class generalizes that same
// convention so pickups/kills/dialogue don't need any audio files either,
// and each clip is only synthesized once (callers should cache the result).
public static class RuntimeSfx
{
    private const int SampleRate = 22050;

    // A short rising chime - used for collecting a Proton/Electron. Sweeping
    // from startFreq to endFreq lets two calls sound related but distinct
    // (e.g. Proton low->warm, Electron high->zippy) without any new code path.
    public static AudioClip CreatePickupChime(string name, float startFreq, float endFreq, float duration = 0.18f)
    {
        int sampleCount = Mathf.RoundToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SampleRate;
            float progress = Mathf.Clamp01(time / duration);
            float frequency = Mathf.Lerp(startFreq, endFreq, progress);
            float envelope = Mathf.Sin(Mathf.PI * progress); // smooth fade in then out
            float tone = Mathf.Sin(2f * Mathf.PI * frequency * time);
            samples[i] = tone * envelope * 0.5f;
        }

        return BuildClip(name, samples);
    }

    // A punchy descending thud with a touch of noise - used for defeating an
    // enemy. Lower and shorter than the pickup chimes so it reads as impact
    // rather than reward.
    public static AudioClip CreateDefeatThud(string name, float duration = 0.28f)
    {
        int sampleCount = Mathf.RoundToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];
        System.Random rng = new System.Random(1234);

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SampleRate;
            float envelope = Mathf.Exp(-time * 14f);
            float frequency = Mathf.Lerp(220f, 70f, Mathf.Clamp01(time / duration));
            float tone = Mathf.Sin(2f * Mathf.PI * frequency * time);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.3f;
            samples[i] = (tone * 0.7f + noise) * envelope;
        }

        return BuildClip(name, samples);
    }

    // A soft double-blip - used when an NPC says a line. Two short pulses
    // instead of one continuous tone so it reads as a gentle "notification"
    // cue, distinct from both the pickup chimes and the gunshot/defeat sounds.
    public static AudioClip CreateTalkBlip(string name, float frequency = 520f, float duration = 0.16f)
    {
        int sampleCount = Mathf.RoundToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SampleRate;
            float progress = time / duration;

            float pulse;
            if (progress < 0.45f)
            {
                pulse = Mathf.Sin(Mathf.PI * (progress / 0.45f));
            }
            else if (progress > 0.55f)
            {
                pulse = Mathf.Sin(Mathf.PI * ((progress - 0.55f) / 0.45f));
            }
            else
            {
                pulse = 0f;
            }

            float tone = Mathf.Sin(2f * Mathf.PI * frequency * time);
            samples[i] = tone * pulse * 0.45f;
        }

        return BuildClip(name, samples);
    }

    // Loads an imported AudioClip from Assets/Resources/Audio/<clipName>.mp3
    // (no extension in the lookup key - Resources.Load strips it). Used for
    // the real recorded SFX (gunshot, enemy hit, item pickup) that replaced
    // the earlier fully-procedural placeholders. Falls back to null if the
    // clip isn't present, so callers can still fall back to a procedural
    // clip and never end up with a silent, broken feature.
    public static AudioClip LoadClip(string clipName)
    {
        return Resources.Load<AudioClip>($"Audio/{clipName}");
    }

    // Ensures the given GameObject has an AudioSource suitable for repeated
    // PlayOneShot calls (not looping, not auto-playing on Awake).
    public static AudioSource GetOrAddOneShotSource(GameObject host, float volume = 0.4f, float spatialBlend = 0.4f)
    {
        AudioSource source = host.GetComponent<AudioSource>();
        if (source == null)
        {
            source = host.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = false;
        source.volume = volume;
        source.spatialBlend = spatialBlend;
        return source;
    }

    private static AudioClip BuildClip(string name, float[] samples)
    {
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
