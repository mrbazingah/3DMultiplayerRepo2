using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] int maxHealth;
    [SerializeField] int health;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            health = maxHealth;
        }
    }

    public void TakeDamage(int dmg)
    {
        DamagePlayerServerRpc(health, dmg);
    }

    [Rpc(SendTo.Server)]
    void DamagePlayerServerRpc(int currentHealth, int dmg)
    {
        currentHealth -= dmg;
        health = currentHealth;
    }
}
