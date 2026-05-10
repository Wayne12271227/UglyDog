using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DogShopRangeSetup
{
    private const string ShopObjectName = "dogShop";
    private const string RangeObjectName = "DogShopRange";
    private const string LegacyVisualObjectName = "Shop Interaction Range Visual";
    private const string SetupRequestPath = "Temp/SetupDogShopRange.request";

    [InitializeOnLoadMethod]
    private static void SetupWhenRequested()
    {
        if (!File.Exists(SetupRequestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(SetupRequestPath))
            {
                return;
            }

            File.Delete(SetupRequestPath);
            SetupDogShopRange();
        };
    }

    [MenuItem("Tools/Setup Dog Shop Range")]
    public static void SetupDogShopRange()
    {
        GameObject shop = FindDogShop();
        if (shop == null)
        {
            EditorUtility.DisplayDialog("Setup Dog Shop Range", "Could not find a scene object named dogShop.", "OK");
            return;
        }

        UpgradeShopZone oldRootZone = shop.GetComponent<UpgradeShopZone>();
        GameObject rangeObject = FindDirectChild(shop.transform, RangeObjectName);
        if (rangeObject == null)
        {
            rangeObject = new GameObject(RangeObjectName);
            Undo.RegisterCreatedObjectUndo(rangeObject, "Create Dog Shop Range");
            rangeObject.transform.SetParent(shop.transform, true);
        }

        Undo.RecordObject(rangeObject.transform, "Setup Dog Shop Range");
        rangeObject.transform.position = shop.transform.position;
        rangeObject.transform.rotation = Quaternion.identity;
        rangeObject.transform.localScale = Vector3.one;

        UpgradeShopZone zone = rangeObject.GetComponent<UpgradeShopZone>();
        if (zone == null)
        {
            zone = Undo.AddComponent<UpgradeShopZone>(rangeObject);
        }

        if (oldRootZone != null)
        {
            EditorUtility.CopySerialized(oldRootZone, zone);
            Undo.DestroyObjectImmediate(oldRootZone);
            RemoveRootTriggerBoxColliders(shop);
        }

        RemoveExtraShopZones(shop, zone);
        RemoveLegacyVisualChild(shop.transform);
        RemoveLegacyVisualChild(rangeObject.transform);

        BoxCollider collider = rangeObject.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = Undo.AddComponent<BoxCollider>(rangeObject);
        }

        Undo.RecordObject(collider, "Setup Dog Shop Range");
        collider.isTrigger = true;

        Undo.RecordObject(zone, "Setup Dog Shop Range");
        zone.RefreshRangeVisual();

        EditorUtility.SetDirty(zone);
        EditorUtility.SetDirty(rangeObject);
        EditorSceneManager.MarkSceneDirty(shop.scene);
        Selection.activeGameObject = rangeObject;
    }

    private static GameObject FindDogShop()
    {
        GameObject exact = GameObject.Find(ShopObjectName);
        if (exact != null)
        {
            return exact;
        }

        GameObject[] objects = Object.FindObjectsOfType<GameObject>(true);
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i].name.Equals(ShopObjectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return objects[i];
            }
        }

        return null;
    }

    private static GameObject FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static void RemoveExtraShopZones(GameObject shop, UpgradeShopZone keepZone)
    {
        UpgradeShopZone[] zones = shop.GetComponentsInChildren<UpgradeShopZone>(true);
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null && zones[i] != keepZone)
            {
                Undo.DestroyObjectImmediate(zones[i]);
            }
        }
    }

    private static void RemoveLegacyVisualChild(Transform parent)
    {
        Transform legacyVisual = parent.Find(LegacyVisualObjectName);
        if (legacyVisual != null)
        {
            Undo.DestroyObjectImmediate(legacyVisual.gameObject);
        }
    }

    private static void RemoveRootTriggerBoxColliders(GameObject shop)
    {
        BoxCollider[] colliders = shop.GetComponents<BoxCollider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].isTrigger)
            {
                Undo.DestroyObjectImmediate(colliders[i]);
            }
        }
    }
}
