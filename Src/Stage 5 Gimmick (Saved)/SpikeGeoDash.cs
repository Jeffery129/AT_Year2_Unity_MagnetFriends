using UnityEngine;

public class SpikeGeoDash : MonoBehaviour
{
    private DashRespawnManager _myRespawnManager;

    private void Start()
    {
        _myRespawnManager = GetComponentInParent<DashRespawnManager>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.isTrigger) return;
        if (_myRespawnManager == null) return;

        var player = collision.gameObject.GetComponentInParent<MagnetPlayer>(false);
        if (player == null) return;
        if (!player.gameObject.activeInHierarchy) return;

        var playerNum = player.playerNumber;

        //if (!_myRespawnManager.CanPlayerDie(playerNum)) return;

        _myRespawnManager.GeoDashRespawn(player, playerNum);
    }
}