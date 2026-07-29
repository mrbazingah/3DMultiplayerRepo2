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
    [SerializeField] int maxAmmo;
    [SerializeField] float reloadDelay;
    [SerializeField] TextMeshProUGUI ammoText;

    NetworkVariable<int> ammo = new NetworkVariable<int>();

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

        if (IsServer)
        {
            ammo.Value = maxAmmo;
        }

        ammo.OnValueChanged += OnAmmoChanged;
        OnAmmoChanged(0, ammo.Value);
    }

    public void OnShoot(InputValue value)
    {
        if (!IsOwner || isShooting || isReloading || !canShoot || ammo.Value <= 0) { return; }

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
        if (ammo.Value <= 0) { return; }

        ammo.Value--;

        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, shootRange, targetLayer))
        {
            Debug.Log("Hit target");
        }
    }

    void OnAmmoChanged(int oldValue, int newValue)
    {
        if (ammoText != null)
        {
            ammoText.text = newValue + " / " + maxAmmo;
        }
    }
}
