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

    NetworkVariable<bool> isShooting = new NetworkVariable<bool>();
    bool isReloading;
    bool canShoot;

    Camera cam;

    public override void OnNetworkSpawn()
    {
        isShooting.OnValueChanged += OnIsShootingChanged;

        ammoCount = maxAmmoCount;
    }

    public void OnShoot(InputValue value)
    {
        if (!IsOwner || isShooting.Value || isReloading || !canShoot) { return; }

        StartCoroutine(ShootingDelay());
    }

    IEnumerator ShootingDelay()
    {
        // Shoots due to value being changed
        isShooting.Value = true;

        yield return new WaitForSeconds(shootDelay);

        isShooting.Value = false;
    }

    void OnIsShootingChanged(bool oldValue, bool newValue)
    {
        Shoot();
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
