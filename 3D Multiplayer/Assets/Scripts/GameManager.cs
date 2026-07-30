using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] Vector3 spawnPoint;
    [SerializeField] int maxPlayerCount;
    [SerializeField] int hunterValue;

    NetworkObject[] players;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {

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

    public void StartGame()
    {
        if (!IsServer) { return; }

        AssignTeam();

        // Teleport players to map
    }

    public void AssignTeam()
    {
        if (!IsServer) { return; }

        List<PlayerMovement> playerList = new List<PlayerMovement>();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent(out PlayerMovement pm))
            {
                playerList.Add(pm);
            }
        }

        // Randomise player list indexes
        for (int i = playerList.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i  + 1);

            // Assign value to the random index in list
            (playerList[i], playerList[randomIndex]) = (playerList[randomIndex], playerList[i]);
        }

        int hunterCount = Mathf.Max(1, playerList.Count / hunterValue);
        for (int i = 0; i < playerList.Count; i++)
        {
            playerList[i].SetPlayerTeam(i < hunterCount ? Team.Hunters : Team.Props);
        }
    }
}
