using UnityEngine;

[RequireComponent(typeof(BuildingHealth))]
public class TeamBuilding : MonoBehaviour
{
    [SerializeField] private MinionTeam team = MinionTeam.Dog;

    public MinionTeam Team => team;
    public BuildingHealth Health { get; private set; }

    private void Awake()
    {
        Health = GetComponent<BuildingHealth>();
    }

    public void Configure(MinionTeam newTeam)
    {
        team = newTeam;
        Health = GetComponent<BuildingHealth>();
    }
}
