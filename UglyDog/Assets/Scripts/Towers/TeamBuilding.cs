using UnityEngine;

[RequireComponent(typeof(BuildingHealth))]
public class TeamBuilding : MonoBehaviour
{
    [SerializeField] private MinionTeam team = MinionTeam.Dog;

    private BuildingHealth health;

    public MinionTeam Team => team;
    public BuildingHealth Health
    {
        get
        {
            if (health == null)
            {
                health = GetComponent<BuildingHealth>();
            }

            return health;
        }
    }

    private void Awake()
    {
        health = GetComponent<BuildingHealth>();
    }

    public void Configure(MinionTeam newTeam)
    {
        team = newTeam;
        health = GetComponent<BuildingHealth>();
    }
}
