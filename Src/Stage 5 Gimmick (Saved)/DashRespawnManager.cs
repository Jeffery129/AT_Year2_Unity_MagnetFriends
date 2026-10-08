using System.Collections;
using UnityEngine;

public class DashRespawnManager : MonoBehaviour
{
    private enum PlayerStatus
    {
        Alive,
        Respawning
    }

    private struct PlayerStruct
    {
        public PlayerStatus Mstatus;
        public bool MIsImmortal;
    }

    private PlayerStruct[] _players;

    [Header("Parameter")]
    [SerializeField] private float respawnWaitTime = 2.0f; // 2 seconds CD
    //[SerializeField] private float immortalTime = 1.0f; // 無敵時間

    private SpikeGeoDash[] _mySpikes;

    private void Start()
    {
        _mySpikes = GetComponentsInChildren<SpikeGeoDash>();

        _players = new PlayerStruct[2];
        for (var index = 0; index < 2; index++)
        {
            _players[index].Mstatus = PlayerStatus.Alive;
            _players[index].MIsImmortal = false;
        }
    }

    public void GeoDashRespawn(MagnetPlayer player, int playerNum)
    {
        GameManager.SpawnDeathEffect(player.transform.position, playerNum);
        player.gameObject.SetActive(false);
        switch (playerNum)
        {
            case 1:
                if (_players[0].Mstatus == PlayerStatus.Alive)
                {
                    _players[0].Mstatus = PlayerStatus.Respawning;
                }
                break;
            case 2:
                if (_players[1].Mstatus == PlayerStatus.Alive)
                {
                    _players[1].Mstatus = PlayerStatus.Respawning;
                }
                break;
        }
    }

    private IEnumerator WaitingForRespawn()
    {
        yield return new WaitForSeconds(respawnWaitTime);
    }
}
