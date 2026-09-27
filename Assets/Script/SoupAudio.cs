using UnityEngine;

public class SoupAudio : MonoBehaviour
{
    public static SoupAudio Instance;

    private AudioSource sfxSource;
    private AudioSource spillLoopSource;

    private AudioClip splashClip;
    private AudioClip stepWalkClip;
    private AudioClip stepSprintClip;
    private AudioClip jumpClip;
    private AudioClip landClip;
    private AudioClip braceClip;
    private AudioClip hitClip;
    private AudioClip pickupSoupClip;
    private AudioClip pickupChiliClip;
    private AudioClip pickupLidClip;
    private AudioClip alarmClip;
    private AudioClip gameOverClip;
    private AudioClip whooshClip;

    private float lastStepTime = 0f;
    private float lastAlarmTime = 0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        spillLoopSource = gameObject.AddComponent<AudioSource>();
        spillLoopSource.playOnAwake = false;
        spillLoopSource.loop = true;
        spillLoopSource.spatialBlend = 0f;

        GenerateClips();
    }

    private void GenerateClips()
    {
        int sampleRate = 44100;

        splashClip = CreateSoftSplash(sampleRate, 0.2f, 0.45f);
        stepWalkClip = CreateDecayingTone(sampleRate, 0.05f, 65f, 0.25f);
        stepSprintClip = CreateDecayingTone(sampleRate, 0.06f, 85f, 0.35f);
        jumpClip = CreateSweepTone(sampleRate, 0.12f, 150f, 320f, 0.3f);
        landClip = CreateDecayingTone(sampleRate, 0.18f, 50f, 0.5f);
        braceClip = CreateClickTone(sampleRate, 0.05f, 0.3f);
        hitClip = CreateNoiseTone(sampleRate, 0.22f, 100f, 50f, 0.55f);
        pickupSoupClip = CreateArpeggio(sampleRate, new float[] { 523.25f, 783.99f }, 0.08f, 0.4f);
        pickupChiliClip = CreateArpeggio(sampleRate, new float[] { 440f, 554.37f, 659.25f, 880f }, 0.05f, 0.45f);
        pickupLidClip = CreateArpeggio(sampleRate, new float[] { 659.25f, 987.77f }, 0.09f, 0.4f);
        alarmClip = CreateArpeggio(sampleRate, new float[] { 880f, 660f }, 0.07f, 0.3f);
        gameOverClip = CreateSweepTone(sampleRate, 0.55f, 300f, 100f, 0.5f);
        whooshClip = CreateSoftWhoosh(sampleRate, 0.35f, 0.25f);

        AudioClip spillLoop = CreateSoftSplash(sampleRate, 0.5f, 0.35f);
        if (spillLoopSource != null && spillLoop != null)
        {
            spillLoopSource.clip = spillLoop;
            spillLoopSource.volume = 0f;
        }
    }

    public void PlaySplash()
    {
        PlayClip(splashClip, 0.5f, Random.Range(0.9f, 1.15f));
    }

    public void SetSpillSoundActive(bool isSpilling)
    {
        if (spillLoopSource == null) return;

        if (isSpilling)
        {
            if (!spillLoopSource.isPlaying) spillLoopSource.Play();
            spillLoopSource.volume = Mathf.Lerp(spillLoopSource.volume, 0.4f, Time.deltaTime * 10f);
        }
        else
        {
            spillLoopSource.volume = Mathf.Lerp(spillLoopSource.volume, 0f, Time.deltaTime * 10f);
            if (spillLoopSource.volume < 0.02f && spillLoopSource.isPlaying)
            {
                spillLoopSource.Stop();
            }
        }
    }

    public void PlayWhoosh()
    {
        PlayClip(whooshClip, 0.35f, 1f);
    }

    public void PlayFootstep(bool isSprint, float minInterval)
    {
        if (Time.time - lastStepTime < minInterval) return;
        lastStepTime = Time.time;

        AudioClip clip = isSprint ? stepSprintClip : stepWalkClip;
        float vol = isSprint ? 0.35f : 0.22f;
        PlayClip(clip, vol, Random.Range(0.92f, 1.08f));
    }

    public void PlayJump()
    {
        PlayClip(jumpClip, 0.35f, 1f);
    }

    public void PlayLand()
    {
        PlayClip(landClip, 0.55f, 1f);
    }

    public void PlayBrace()
    {
        PlayClip(braceClip, 0.35f, 1f);
    }

    public void PlayHit()
    {
        PlayClip(hitClip, 0.65f, Random.Range(0.95f, 1.05f));
    }

    public void PlayPickupSoup()
    {
        PlayClip(pickupSoupClip, 0.45f, 1f);
    }

    public void PlayPickupChili()
    {
        PlayClip(pickupChiliClip, 0.5f, 1f);
    }

    public void PlayPickupLid()
    {
        PlayClip(pickupLidClip, 0.45f, 1f);
    }

    public void PlayAlarm()
    {
        if (Time.time - lastAlarmTime < 0.8f) return;
        lastAlarmTime = Time.time;
        PlayClip(alarmClip, 0.35f, 1f);
    }

    public void PlayGameOver()
    {
        if (spillLoopSource != null) spillLoopSource.Stop();
        PlayClip(gameOverClip, 0.65f, 1f);
    }

    private void PlayClip(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, volume);
    }

    private AudioClip CreateDecayingTone(int sampleRate, float duration, float freq, float maxVol)
    {
        int count = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 30f);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
            samples[i] = wave * env * maxVol;
        }

        AudioClip clip = AudioClip.Create("Tone", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateSweepTone(int sampleRate, float duration, float startFreq, float endFreq, float maxVol)
    {
        int count = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[count];
        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float curFreq = Mathf.Lerp(startFreq, endFreq, t);
            phase += 2f * Mathf.PI * curFreq / sampleRate;
            float env = 1f - t;
            samples[i] = Mathf.Sin(phase) * env * maxVol;
        }

        AudioClip clip = AudioClip.Create("Sweep", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateSoftSplash(int sampleRate, float duration, float maxVol)
    {
        int count = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[count];
        float filtered = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float raw = Random.Range(-1f, 1f);
            filtered = Mathf.Lerp(filtered, raw, 0.04f);
            float bubble = Mathf.Sin((float)i * 0.03f) * 0.3f;
            float env = Mathf.Sin(t * Mathf.PI);
            samples[i] = (filtered + bubble) * env * maxVol;
        }

        AudioClip clip = AudioClip.Create("Splash", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateSoftWhoosh(int sampleRate, float duration, float maxVol)
    {
        int count = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[count];
        float filtered = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float raw = Random.Range(-1f, 1f);
            filtered = Mathf.Lerp(filtered, raw, 0.02f);
            float env = Mathf.Sin(t * Mathf.PI);
            samples[i] = filtered * env * maxVol;
        }

        AudioClip clip = AudioClip.Create("Whoosh", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateNoiseTone(int sampleRate, float duration, float baseFreq, float endFreq, float maxVol)
    {
        int count = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[count];
        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float curFreq = Mathf.Lerp(baseFreq, endFreq, t);
            phase += 2f * Mathf.PI * curFreq / sampleRate;

            float env = Mathf.Exp(-t * 8f);
            float noise = Random.Range(-0.3f, 0.3f);
            float wave = (Mathf.Sin(phase) * 0.7f) + noise;
            samples[i] = wave * env * maxVol;
        }

        AudioClip clip = AudioClip.Create("NoiseTone", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateClickTone(int sampleRate, float duration, float maxVol)
    {
        int count = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float env = Mathf.Exp(-t * 35f);
            float wave = Mathf.Sin(2f * Mathf.PI * 800f * (float)i / sampleRate);
            samples[i] = wave * env * maxVol;
        }

        AudioClip clip = AudioClip.Create("Click", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateArpeggio(int sampleRate, float[] freqs, float noteDuration, float maxVol)
    {
        int noteSamples = Mathf.CeilToInt(sampleRate * noteDuration);
        int totalSamples = noteSamples * freqs.Length;
        float[] samples = new float[totalSamples];

        for (int n = 0; n < freqs.Length; n++)
        {
            float freq = freqs[n];
            int offset = n * noteSamples;

            for (int i = 0; i < noteSamples; i++)
            {
                float t = (float)i / noteSamples;
                float env = 1f - Mathf.Pow(t, 2f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * (float)i / sampleRate);
                samples[offset + i] = wave * env * maxVol;
            }
        }

        AudioClip clip = AudioClip.Create("Arp", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
