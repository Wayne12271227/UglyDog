using UnityEngine;

public class ResourceConsumerExample : MonoBehaviour
{
    [SerializeField] private ResourceCost[] costs;

    public bool TryUseResources()
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogWarning("No ResourceManager found in the scene.");
            return false;
        }

        bool success = ResourceManager.Instance.Spend(costs);
        if (!success)
        {
            Debug.Log("Not enough resources.");
        }

        return success;
    }
}
