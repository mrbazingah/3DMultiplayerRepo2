using UnityEngine;

public class FollowTarget : MonoBehaviour
{
    [SerializeField] GameObject target;

    void Update()
    {
        transform.position = target.transform.position;
    }
}
