using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooting : NetworkBehaviour
{
    [Header("Shooting Settings")]
    [SerializeField] float shootRange;
    [SerializeField] int damage;
    [SerializeField] float shootDelay;
    [SerializeField] LayerMask targetLayer;

    [Header("Reload")]
    [SerializeField] int ammoCount;
    [SerializeField] int maxAmmoCount;
    [SerializeField] float reloadDelay;
    [SerializeField] TextMeshProUGUI ammoText;

    bool isShooting;
    bool isReloading;
    bool canShoot;

    Camera cam;

    PlayerMovement playerMovement;

    public override void OnNetworkSpawn()
    {
        playerMovement = FindFirstObjectByType<PlayerMovement>();

        canShoot = playerMovement.GetPlayerTeam().Value == GameManager.Team.Hunters;

        ammoCount = maxAmmoCount;
    }

    public void OnShoot(InputValue value)
    {
        if (!IsOwner || isShooting || isReloading || !canShoot) { return; }

        StartCoroutine(ShootingDelay());
    }

    IEnumerator ShootingDelay()
    {
        // Shoots due to value being changed
        isShooting = true;

        yield return new WaitForSeconds(shootDelay);

        isShooting = false;
    }

    void Shoot()
    {
        RaycastHit hit;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, shootRange, targetLayer))
        {
            Debug.Log("Hit target");
        }

        ammoCount--;
    }
}
