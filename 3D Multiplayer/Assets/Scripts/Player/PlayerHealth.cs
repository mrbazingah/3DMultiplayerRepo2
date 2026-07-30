using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] int maxHealth;
    [SerializeField] NetworkVariable<int> health = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            health.Value = maxHealth;
        }
    }

    public void TakeDamage(int dmg)
    {
        if (!IsServer) { return; }

        health.Value -= dmg;

        if (health.Value <= 0)
        {
            health.Value = 0;
            Die();
        }
    }

    void Die()
    {
        Debug.Log("Player died");
    }
}
