using Fusion.Photon.Realtime;

public static class UglyDogPhotonSettings
{
    public static FusionAppSettings GetPhotonAppSettingsForCurrentPlatform()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!PhotonAppSettings.TryGetGlobal(out PhotonAppSettings settings) || settings.AppSettings == null)
        {
            return null;
        }

        FusionAppSettings appSettings = settings.AppSettings.GetCopy();
        appSettings.Protocol = ExitGames.Client.Photon.ConnectionProtocol.WebSocketSecure;
        appSettings.Port = 0;
        appSettings.EnableProtocolFallback = true;
        return appSettings;
#else
        return null;
#endif
    }
}
