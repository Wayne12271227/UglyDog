using UnityEngine;

public class SampleSceneAudioController : MonoBehaviour
{
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 0.22f;

    private AudioSource musicSource;

    private void Awake()
    {
        StopPersistentAmbience();
        EnsureMusicSource();
        PlayBackgroundMusic();

        GameAudioSettings.VolumeChanged -= ApplySavedVolume;
        GameAudioSettings.VolumeChanged += ApplySavedVolume;
    }

    private void OnDestroy()
    {
        GameAudioSettings.VolumeChanged -= ApplySavedVolume;
    }

    private void StopPersistentAmbience()
    {
        PersistentAmbientAudio.Configure(null, 0f, null, 0f);
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

        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
    }

    private void PlayBackgroundMusic()
    {
        if (backgroundMusic == null)
        {
            if (musicSource != null)
            {
                musicSource.Stop();
            }

            return;
        }

        musicSource.clip = backgroundMusic;
        ApplySavedVolume();

        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    private void ApplySavedVolume()
    {
        if (musicSource == null)
        {
            return;
        }

        musicSource.volume = Mathf.Clamp01(backgroundMusicVolume) * GameAudioSettings.MusicVolume;
    }
}
