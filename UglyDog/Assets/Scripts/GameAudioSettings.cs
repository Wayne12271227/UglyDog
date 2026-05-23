using System;
using UnityEngine;

public static class GameAudioSettings
{
    private const string MusicVolumeKey = "UglyDog.MusicVolume";
    private const string SfxVolumeKey = "UglyDog.SfxVolume";
    private const float DefaultMusicVolume = 1f;
    private const float DefaultSfxVolume = 1f;

    public static event Action VolumeChanged;

    public static float MusicVolume => PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
    public static float SfxVolume => PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume);

    public static void SetMusicVolume(float value)
    {
        SetVolume(MusicVolumeKey, value);
    }

    public static void SetSfxVolume(float value)
    {
        SetVolume(SfxVolumeKey, value);
    }

    private static void SetVolume(string key, float value)
    {
        PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        VolumeChanged?.Invoke();
    }
}
