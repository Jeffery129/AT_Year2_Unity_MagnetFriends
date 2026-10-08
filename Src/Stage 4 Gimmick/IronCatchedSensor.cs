using UnityEngine;

public class IronCatchedSensor : MonoBehaviour
{
    [SerializeField] private IronArmorRock owner;

    private void Awake()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<IronArmorRock>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner == null) return;

        var catchArea = other.GetComponent<IronCatchArea>();
        if (catchArea == null) return;

        owner.TryAttachToPlayer(catchArea.Owner);
    }
}
