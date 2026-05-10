using UnityEngine;

public static class UpgradeShopBootstrap
{
    private const string ShopObjectName = "dogShop";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BindSceneShop()
    {
        CatPlayerController player = PreferredPlayerFinder.FindPreferredPlayer();
        if (player == null)
        {
            return;
        }

        GameObject shop = FindShopObject();
        if (shop == null)
        {
            Debug.LogWarning("UpgradeShopBootstrap could not find dogShop in the scene. No runtime shop was created.");
            return;
        }

        PlayerUpgradeManager.EnsureInstance();
        UpgradeShopZone shopZone = shop.GetComponentInChildren<UpgradeShopZone>(true);
        if (shopZone == null)
        {
            Debug.LogWarning("dogShop exists, but no UpgradeShopZone was found under it. Use Tools/Setup Dog Shop Range once in the editor.");
        }
    }

    private static GameObject FindShopObject()
    {
        GameObject exact = GameObject.Find(ShopObjectName);
        if (exact != null)
        {
            return exact;
        }

        GameObject[] allObjects = Object.FindObjectsOfType<GameObject>(true);
        for (int i = 0; i < allObjects.Length; i++)
        {
            if (allObjects[i].name.Equals(ShopObjectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return allObjects[i];
            }
        }

        return null;
    }
}
