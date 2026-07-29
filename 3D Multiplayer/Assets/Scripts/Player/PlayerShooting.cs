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

    PlayerMovement myPlayerMovement;

    public override void OnNetworkSpawn()
    {
        myPlayerMovement = GetComponent<PlayerMovement>();

        cam = myPlayerMovement.GetCurrentCam();

        if (IsServer)
        {
            ammo.Value = maxAmmo;
        }

        ammo.OnValueChanged += OnAmmoChanged;
        OnAmmoChanged(0, ammo.Value);
    }

    public void SetCanShoot(GameManager.Team team)
    {
        canShoot = team == GameManager.Team.Hunters;
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
        if (ammo.Value <= 0 || isReloading) { return; }

        ammo.Value--;

        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, shootRange, targetLayer))
        {
            Debug.Log("Hit target");

            PlayerHealth targetHealth = hit.transform.GetComponentInParent<PlayerHealth>();
            PlayerMovement targetMovement = hit.transform.GetComponentInParent<PlayerMovement>();

            if (targetHealth != null && targetMovement != null)
            {
                if (targetMovement.GetPlayerTeam().Value == myPlayerMovement.GetPlayerTeam().Value) { return; }

                targetHealth.TakeDamage(damage); 
            }
        }
    }

    void OnAmmoChanged(int oldValue, int newValue)
    {
        UpdateAmmoText(newValue.ToString());
    }

    void UpdateAmmoText(string newValue)
    {
        if (ammoText != null)
        {
            ammoText.text = newValue + " / " + maxAmmo;
        }
    }

    public void OnReload(InputValue value)
    {
        if (!IsOwner || isReloading || isShooting || !canShoot || ammo.Value == maxAmmo) { return; }

        StartCoroutine(ReloadingDelay());
    }

    IEnumerator ReloadingDelay()
    {
        isReloading = true;

        UpdateAmmoText("...");

        // Might want to change delay to server side
        yield return new WaitForSeconds(reloadDelay);

        ReloadServerRpc();

        isReloading = false;
    }

    [Rpc(SendTo.Server)]
    void ReloadServerRpc()
    {
        ammo.Value = maxAmmo;
    }
}
