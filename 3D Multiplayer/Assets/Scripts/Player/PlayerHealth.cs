using TMPro;
using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] int defaultMaxHealth;
    [SerializeField] TextMeshProUGUI healthText;
    [SerializeField] GameObject playerCanvas;

    NetworkVariable<int> health = new NetworkVariable<int>();
    NetworkVariable<int> maxHealth = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            maxHealth.Value = defaultMaxHealth;
            health.Value = defaultMaxHealth;
        }

        playerCanvas.gameObject.SetActive(IsOwner);
        healthText.text = health.Value.ToString();
        health.OnValueChanged += OnHealthChanged;
    }

    // Called on the server when the prop changes, keeps the same health percentage on the new max
    public void ApplyMaxHealth(int newMaxHealth)
    {
        if (!IsServer || newMaxHealth <= 0) { return; }

        // Works out the percentage before the max is overwritten
        float percentage = maxHealth.Value > 0 ? (float)health.Value / maxHealth.Value : 1f;

        maxHealth.Value = newMaxHealth;

        // Clamped to at least 1 so swapping to a small prop can never kill the player on its own
        health.Value = Mathf.Max(1, Mathf.RoundToInt(newMaxHealth * percentage));
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

    void OnHealthChanged(int oldValue, int newValue)
    {
        healthText.text = newValue.ToString();
    }

    void Die()
    {
        Debug.Log("Player died");
    }

    public int GetDefaultMaxHealth()
    {
        return defaultMaxHealth;
    }

    public NetworkVariable<int> GetHealth()
    {
        return health;
    }

    public NetworkVariable<int> GetMaxHealth()
    {
        return maxHealth;
    }

    public override void OnNetworkDespawn()
    {
        health.OnValueChanged -= OnHealthChanged;
    }
}