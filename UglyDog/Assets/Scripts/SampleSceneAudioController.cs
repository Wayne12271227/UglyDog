using UnityEngine;

public class SampleSceneAudioController : MonoBehaviour
{
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 0.22f;

    private void Awake()
    {
        StopPersistentAmbience();
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

    private void PlayBackgroundMusic()
    {
        PersistentBattleMusic.Play(backgroundMusic, backgroundMusicVolume);
    }

    private void ApplySavedVolume()
    {
        PersistentBattleMusic.Play(backgroundMusic, backgroundMusicVolume);
    }
}
