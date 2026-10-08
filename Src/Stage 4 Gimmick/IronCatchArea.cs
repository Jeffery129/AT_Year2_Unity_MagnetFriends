using UnityEngine;

public class IronCatchArea : MonoBehaviour
{
    [SerializeField] private MagnetPlayer owner;

    public MagnetPlayer Owner => owner;

    private void Awake()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<MagnetPlayer>();
        }
    }
}
