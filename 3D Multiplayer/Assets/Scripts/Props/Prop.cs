using Unity.Netcode;
using UnityEngine;

public class Prop : NetworkBehaviour
{
    [field:SerializeField] public PropType propType { get; private set; }

    public enum PropType
    {
        None,
        Lantern,
        Campfire,
        SodaCan,
        Books,
        Homeplant_01,
        Homeplant_02,
    }
}
