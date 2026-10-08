using UnityEngine;

public class ClawDetector : MonoBehaviour
{
    [Header("掴む条件")]
    [Tooltip("入れると装甲状態のプレイヤーだけを掴む。" +
             "鉄片置き場のように装甲前に使うクレーンは外す")]
    [SerializeField] private bool requireArmored = true;

    private ClawController _myController;

    private bool _isTransportSoundPlaying = false;

    private void Start()
    {
        _myController = GetComponentInParent<ClawController>();
        if (_myController == null) return;
    }

    private void OnTriggerStay(Collider other)
    {
        if (_myController.CurrentClawState is ClawController.ClawState.Inactive)
        {
            StopTransportSound();
            return;
        }
        if (other.isTrigger) return;

        var player = other.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;
        if (requireArmored && !player.IsArmored)
        {
            player.SetIsCaughtByCrane(false);
            StopTransportSound();
            return;
        }

        player.SetIsCaughtByCrane(true);
        StartTransportSound();
        var rb = player.GetComponent<Rigidbody>();
        var dir = (transform.position - rb.position).normalized;
        var clawPower = _myController.ClawPower;

        rb.AddForce(dir * clawPower, ForceMode.Acceleration);
    }

    private void OnTriggerExit(Collider other)
    {

        var player = other.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;
        if (requireArmored && !player.IsArmored) return;

        player.SetIsCaughtByCrane(false);

        if (_myController.CurrentClawState is ClawController.ClawState.Inactive) return;

        StopTransportSound();
    }

    private void StartTransportSound()
    {
        if(_isTransportSoundPlaying)
        {
            return;
        }

        if(SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.PlayLoopSE3D(SoundID.Env_ClusterTransport_Loop, gameObject);

        _isTransportSoundPlaying = true;
    }

    private void StopTransportSound()
    {
        if (!_isTransportSoundPlaying)
        {
            return;
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopLoopSE3D(SoundID.Env_ClusterTransport_Loop, gameObject);
        }

        _isTransportSoundPlaying = false;
    }

    private void OnDisable()
    {
        StopTransportSound();
    }

    private void OnDestroy()
    {
        StopTransportSound();
    }

    public void ForceReset()
    {
        StopTransportSound();
    }
}