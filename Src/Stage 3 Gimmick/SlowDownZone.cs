using UnityEngine;

public class SlowDownZone : MonoBehaviour
{
    [Header("Slow Down Rate")]
    [SerializeField] private float slowDownRate = 0.75f;

    [Header("Effect")]
    [SerializeField] private GameObject slowDownEffectPrefab;

    [Header("Effect Follow")]
    [SerializeField] private Vector3 effectOffset = Vector3.zero;
    [SerializeField] private bool keepWorldRotation = true;

    private MagnetPlayer player1;
    private MagnetPlayer player2;

    private GameObject player1Effect;
    private GameObject player2Effect;

    [Header("砂の音")]
    [Tooltip("この速さ以上で動いているときだけ砂の音を鳴らす")]
    [SerializeField] private float soundMoveThreshold = 0.6f;

    [Tooltip("止まってから音を止めるまでの猶予。細かく途切れるのを防ぐ")]
    [SerializeField] private float soundStopDelay = 0.15f;

    private bool _isSoundPlaying = false;
    private float _lastMovingTime = -99f;

    private void OnTriggerEnter(Collider collider3D)
    {
        MagnetPlayer player = collider3D.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;

        if (player.playerNumber == 1)
        {
            if (player1 != null) return;

            player1 = player;
            player1.SetSlowDownDebuff(slowDownRate);
            player1Effect = SpawnEffect(player1);
        }
        else if (player.playerNumber == 2)
        {
            if (player2 != null) return;

            player2 = player;
            player2.SetSlowDownDebuff(slowDownRate);
            player2Effect = SpawnEffect(player2);
        }

        UpdateIronSandSound();
    }

    private void OnTriggerExit(Collider collider3D)
    {
        MagnetPlayer player = collider3D.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;

        if (player.playerNumber == 1)
        {
            ResetPlayer(ref player1, ref player1Effect);
        }
        else if (player.playerNumber == 2)
        {
            ResetPlayer(ref player2, ref player2Effect);
        }

        UpdateIronSandSound();
    }

    private void Update()
    {
        // ゾーンに入っているだけでは鳴らさず、実際に動いている間だけ鳴らす
        if (IsAnyPlayerMoving())
        {
            _lastMovingTime = Time.time;
        }

        UpdateIronSandSound();
    }

    private bool IsAnyPlayerMoving()
    {
        return IsMoving(player1) || IsMoving(player2);
    }

    private bool IsMoving(MagnetPlayer player)
    {
        if (player == null) return false;
        if (!player.gameObject.activeInHierarchy) return false;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null) return false;

        // 落下や跳ねは無視して、床を擦っている水平方向の速さだけ見る
        Vector3 v = rb.linearVelocity;
        v.y = 0f;

        return v.magnitude >= soundMoveThreshold;
    }

    private void LateUpdate()
    {
        FollowEffect(player1, player1Effect);
        FollowEffect(player2, player2Effect);
    }

    private GameObject SpawnEffect(MagnetPlayer player)
    {
        if (slowDownEffectPrefab == null) return null;

        return Instantiate(
            slowDownEffectPrefab,
            player.transform.position + effectOffset,
            Quaternion.identity
        );
    }

    private void FollowEffect(MagnetPlayer player, GameObject effect)
    {
        if (player == null || effect == null) return;

        effect.transform.position = player.transform.position + effectOffset;

        if (keepWorldRotation)
        {
            effect.transform.rotation = Quaternion.identity;
        }
    }

    private void ResetPlayer(ref MagnetPlayer player, ref GameObject effect)
    {
        if (player != null)
        {
            player.ResetSlowDownDebuff();
        }

        if (effect != null)
        {
            Destroy(effect);
        }

        player = null;
        effect = null;
    }

    private void UpdateIronSandSound()
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        bool hasPlayer =
            player1 != null ||
            player2 != null;

        // 直前まで動いていたか。止まった瞬間にプツッと切れないよう猶予をもたせる
        bool moving = (Time.time - _lastMovingTime) <= soundStopDelay;

        bool shouldPlay = hasPlayer && moving;

        if (shouldPlay && !_isSoundPlaying)
        {
            SoundManager.Instance.PlaySE(
                SoundID.Env_IronSand_Loop
            );

            _isSoundPlaying = true;
        }
        else if (!shouldPlay && _isSoundPlaying)
        {
            SoundManager.Instance.StopSE(
                SoundID.Env_IronSand_Loop
            );

            _isSoundPlaying = false;
        }
    }

    public void ForceResetZone()
    {
        ResetPlayer(ref player1, ref player1Effect);
        ResetPlayer(ref player2, ref player2Effect);

        UpdateIronSandSound();
    }

    private void OnDisable()
    {
        ForceResetZone();
    }
}