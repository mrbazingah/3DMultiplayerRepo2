using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [SerializeField] Transform mapSpawnTransform;
    [SerializeField] Transform lobbySpawnTransform;
    [SerializeField] int maxPlayerCount;
    [SerializeField] int hunterValue;

    List<PlayerMovement> playerList = new List<PlayerMovement>();

    public enum Team
    {
        None,
        Hunters,
        Props,
    }

    public void RegisterPlayer(PlayerMovement player)
    {
        if (!IsServer || player == null || playerList.Contains(player)) { return; }

        playerList.Add(player);

        // Only position the player that just joined, everyone else stays put
        player.TeleportTo(lobbySpawnTransform.position);
    }

    public void UnregisterPlayer(PlayerMovement player)
    {
        if (!IsServer) { return; }

        playerList.Remove(player);
    }

    public void StartGame()
    {
        if (!IsServer /*|| playerList.Count < 2*/) { return; }

        AssignTeam();
        SetPlayerPositions(mapSpawnTransform.position);
    }

    void AssignTeam()
    {
        // Randomise player list indexes
        for (int i = playerList.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);

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

    void SetPlayerPositions(Vector3 pos)
    {
        foreach (PlayerMovement player in playerList)
        {
            if (player != null)
            {
                player.TeleportTo(pos);
            }
        }
    }
}