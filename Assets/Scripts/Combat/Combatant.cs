using UnityEngine;

[DisallowMultipleComponent]
public class Combatant : MonoBehaviour
{
    [SerializeField] private Team team = Team.Enemy;
    public Team Team => team;
}
