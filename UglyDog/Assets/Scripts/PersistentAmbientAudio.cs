using UnityEngine;
using UnityEngine.SceneManagement;

public class PersistentAmbientAudio : MonoBehaviour
{
    private const string RootName = "Persistent Ambient Audio";
    private const string BirdSourceName = "Bird Ambience Source";
    private const string WaterSourceName = "Water Ambience Source";

    private static PersistentAmbientAudio instance;

    private AudioSource birdSource;
    private AudioSource waterSource;
    private float birdBaseVolume;
    private float waterBaseVolume;

    public static AudioSource BirdSource => instance != null ? instance.birdSource : null;
    public static AudioSource WaterSource => instance != null ? instance.waterSource : null;

    public static void Configure(AudioClip birdClip, float birdVolume, AudioClip waterClip, float waterVolume)
    {
        PersistentAmbientAudio player = GetOrCreateInstance();
        player.ConfigureSource(ref player.birdSource, BirdSourceName, birdClip, birdVolume);
        player.ConfigureSource(ref player.waterSource, WaterSourceName, waterClip, waterVolume);
        player.EnsureAudioListener();
    }

    private static PersistentAmbientAudio GetOrCreateInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            instance = existing.GetComponent<PersistentAmbientAudio>();
            if (instance != null)
            {
                return instance;
            }
        }

        GameObject root = existing != null ? existing : new GameObject(RootName);
        instance = root.AddComponent<PersistentAmbientAudio>();
        DontDestroyOnLoad(root);
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
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameAudioSettings.VolumeChanged -= ApplySavedVolumes;
        GameAudioSettings.VolumeChanged += ApplySavedVolumes;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameAudioSettings.VolumeChanged -= ApplySavedVolumes;
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureAudioListener();
    }

    private void ConfigureSource(ref AudioSource source, string sourceName, AudioClip clip, float volume)
    {
        source = GetOrCreateSource(source, sourceName);
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        float baseVolume = Mathf.Clamp01(volume);
        if (sourceName == BirdSourceName)
        {
            birdBaseVolume = baseVolume;
        }
        else if (sourceName == WaterSourceName)
        {
            waterBaseVolume = baseVolume;
        }

        source.volume = baseVolume * GameAudioSettings.MusicVolume;
        if (clip == null || baseVolume <= 0f)
        {
            source.Stop();
            source.clip = null;
            return;
        }

        bool sameClipAlreadyPlaying = source.clip == clip && source.isPlaying;
        source.clip = clip;
        if (!sameClipAlreadyPlaying)
        {
            source.Play();
        }
    }

    private void ApplySavedVolumes()
    {
        if (birdSource != null)
        {
            birdSource.volume = birdBaseVolume * GameAudioSettings.MusicVolume;
        }

        if (waterSource != null)
        {
            waterSource.volume = waterBaseVolume * GameAudioSettings.MusicVolume;
        }
    }

    private AudioSource GetOrCreateSource(AudioSource source, string sourceName)
    {
        if (source != null)
        {
            return source;
        }

        Transform sourceTransform = transform.Find(sourceName);
        if (sourceTransform == null)
        {
            GameObject sourceObject = new GameObject(sourceName);
            sourceTransform = sourceObject.transform;
            sourceTransform.SetParent(transform, false);
        }

        source = sourceTransform.GetComponent<AudioSource>();
        return source != null ? source : sourceTransform.gameObject.AddComponent<AudioSource>();
    }

    private void EnsureAudioListener()
    {
        if (FindObjectOfType<AudioListener>() != null)
        {
            return;
        }

        Camera camera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        if (camera != null)
        {
            camera.gameObject.AddComponent<AudioListener>();
        }
    }
}
