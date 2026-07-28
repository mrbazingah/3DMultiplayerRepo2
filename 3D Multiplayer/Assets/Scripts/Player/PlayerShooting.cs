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
        playerMovement = GetComponent<PlayerMovement>();

        canShoot = playerMovement.GetPlayerTeam().Value == GameManager.Team.Hunters;
        cam = playerMovement.GetCurrentCam();

        ammoCount = maxAmmoCount;
    }

    public void OnShoot(InputValue value)
    {
        if (!IsOwner || isShooting || isReloading || !canShoot || ammoCount <= 0) { return; }

        StartCoroutine(ShootingDelay());
    }

    IEnumerator ShootingDelay()
    {
        isShooting = true;
        ShootServerRpc(cam.transform.position, cam.transform.forward);

        yield return new WaitForSeconds(shootDelay);

        isShooting = false;
    }

    [Rpc(SendTo.Server)]
    void ShootServerRpc(Vector3 origin, Vector3 direction)
    {
        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, shootRange, targetLayer))
        {
            Debug.Log("Hit target");
        }

        ammoCount--;
    }
}
