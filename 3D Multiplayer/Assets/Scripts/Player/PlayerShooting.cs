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

    public override void OnNetworkSpawn()
    {
        
    }

    public void OnShoot(InputValue value)
    {
        if (!IsOwner || isShooting || isReloading || !canShoot) { return; }

        StartCoroutine(ShootingDelay());
    }

    IEnumerator ShootingDelay()
    {
        isShooting = true;
        Shoot();

        yield return new WaitForSeconds(shootDelay);
    }

    void Shoot()
    {
        RaycastHit hit;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, shootRange, targetLayer))
        {
            Debug.Log("Hit target");
        }
    }
}
