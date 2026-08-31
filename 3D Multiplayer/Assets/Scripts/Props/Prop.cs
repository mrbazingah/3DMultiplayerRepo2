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
        Plushie,
        Console_01,
        Tv_01,
        Chair_01,
        Chest,
        Lamp_01,
        TrashCan_01,
    }
}
