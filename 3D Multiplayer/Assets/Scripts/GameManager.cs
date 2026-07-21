using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] Vector3 spawnPoint;

    void Start()
    {
        //NetworkManager.Singleton.StartHost();
    }

    public override void OnNetworkSpawn()
    {
        if (player != null)
        {
            SetPlayerSpawnServerRpc(spawnPoint);
            player = null;
        }
    }

    public void AssignPlayer(Transform playerTransform)
    {
        if (!IsSpawned)
        {
            player = playerTransform;
            return;
        }

        player = playerTransform;
        SetPlayerSpawnServerRpc(spawnPoint);
    }

    [Rpc(SendTo.Server)]
    void SetPlayerSpawnServerRpc(Vector3 pos)
    {
        if (player == null) { return; }

        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;  
            playerRb.angularVelocity = Vector3.zero;
            playerRb.position = pos;                  
        }
        else
        {
            player.position = pos;
        }
    }

    public enum Team
    {
        None,
        Hunters,
        Props,
    }
}
