using UnityEngine;

public class PersistentBattleMusic : MonoBehaviour
{
    private static PersistentBattleMusic instance;
    private AudioSource musicSource;
    private AudioClip configuredClip;
    private float configuredVolume = 0.22f;

    public static void Play(AudioClip clip, float volume)
    {
        PersistentBattleMusic player = GetOrCreateInstance();
        player.Configure(clip, volume);
    }

    public static void ResumeConfigured()
    {
        if (instance == null || instance.configuredClip == null)
        {
            return;
        }

        instance.Configure(instance.configuredClip, instance.configuredVolume);
    }

    public static void Stop()
    {
        if (instance == null)
        {
            return;
        }

        if (instance.musicSource != null)
        {
            instance.musicSource.Stop();
        }

        instance.configuredClip = null;
    }

    private static PersistentBattleMusic GetOrCreateInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject root = new GameObject("Persistent Battle Music");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<PersistentBattleMusic>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureMusicSource();
        GameAudioSettings.VolumeChanged -= ApplySavedVolume;
        GameAudioSettings.VolumeChanged += ApplySavedVolume;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            GameAudioSettings.VolumeChanged -= ApplySavedVolume;
            instance = null;
        }
    }

    private void Configure(AudioClip clip, float volume)
    {
        configuredClip = clip;
        configuredVolume = Mathf.Clamp01(volume);
        EnsureMusicSource();

        if (configuredClip == null)
        {
            musicSource.Stop();
            return;
        }

        if (musicSource.clip != configuredClip)
        {
            musicSource.clip = configuredClip;
            musicSource.time = 0f;
        }

        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        ApplySavedVolume();

        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    private void EnsureMusicSource()
    {
        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
        }

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void ApplySavedVolume()
    {
        if (musicSource == null)
        {
            return;
        }

        musicSource.volume = configuredVolume * GameAudioSettings.MusicVolume;
    }
}
