using System.Collections.Generic;
using UnityEngine;

public class WallZoneDetector : MonoBehaviour
{
    private MagnetWallManager _myManager;
    [SerializeField] private int playerCntInZone = 0;

    private const int RequiredPlayerCount = 2;

    private readonly HashSet<MagnetPlayer> _playersInZone = new HashSet<MagnetPlayer>();

    private void Awake()
    {
        _myManager = GetComponentInParent<MagnetWallManager>();
    }


    private void OnTriggerEnter(Collider collider3D)
    {
        var player = collider3D.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;

        _playersInZone.Add(player);
        UpdateZoneState();
    }

    private void OnTriggerExit(Collider collider3D)
    {
        var player = collider3D.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;

        _playersInZone.Remove(player);
        UpdateZoneState();
    }

    private void UpdateZoneState()
    {
        playerCntInZone = _playersInZone.Count;

        if (_myManager == null) return;

        _myManager.SetPlayerInZone(playerCntInZone >= RequiredPlayerCount);
    }

    public void ForceResetZone()
    {
        _playersInZone.Clear();
        playerCntInZone = 0;

        if (_myManager != null)
        {
            _myManager.SetPlayerInZone(false);
        }
    }
}