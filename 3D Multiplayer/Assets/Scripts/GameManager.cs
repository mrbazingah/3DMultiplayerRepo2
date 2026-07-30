using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [SerializeField] Transform spawnTransform;
    [SerializeField] int maxPlayerCount;
    [SerializeField] int hunterValue;

    List<PlayerMovement> playerList = new List<PlayerMovement>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {

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

        SetPlayerList();
        AssignTeam();
        SetPlayerPositions();
    }

    void SetPlayerList()
    {
        playerList = new List<PlayerMovement>();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent(out PlayerMovement pm))
            {
                playerList.Add(pm);
            }
        }
    }

    void AssignTeam()
    {
        // Randomise player list indexes
        for (int i = playerList.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i  + 1);

            // Assign value to the random index in list
            (playerList[i], playerList[randomIndex]) = (playerList[randomIndex], playerList[i]);
        }

        // Divide hunter count
        int hunterCount = Mathf.Max(1, playerList.Count / hunterValue);
        for (int i = 0; i < playerList.Count; i++)
        {
            playerList[i].SetPlayerTeam(i < hunterCount ? Team.Hunters : Team.Props);
        }
    }

    void SetPlayerPositions()
    {
        foreach (PlayerMovement player in playerList)
        {
            player.SetPlayerSpawnServerRpc(spawnTransform.position);
        }
    }
}
