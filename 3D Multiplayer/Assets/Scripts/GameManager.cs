using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] Vector3 spawnPoint;
    [SerializeField] int maxPlayerCount;

    NetworkObject[] players;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnPlayerConnect;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnPlayerDisconnect;

            players = new NetworkObject[maxPlayerCount];
        }

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

    public void OnPlayerConnect(ulong clientId)
    {
        NetworkClient client = NetworkManager.Singleton.ConnectedClients[clientId];
        players[clientId] = client.PlayerObject;
    }

    private void OnPlayerDisconnect(ulong clientId)
    {
        players[clientId] = null;
    }

    int GetPlayerCount()
    {
        foreach (var player in players)
        {
            if (player )
        }
    }
}
