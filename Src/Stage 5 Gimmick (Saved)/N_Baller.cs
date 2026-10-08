using UnityEngine;

public class N_Baller : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public enum BallPolarity
    {
        North,
        South
    }

    private enum BallState
    {
        Float,
        Caught,
        Shoot
    }

    [Header("Ball Status")]
    [SerializeField] private BallState currentState = BallState.Float;
    [SerializeField] private int caughtByPlayer = 0;

    [Header("Float")]
    [SerializeField] private float targetHeight = 1.5f;
    [SerializeField] private float bob = 0.08f;
    [SerializeField] private float spring = 25.0f;
    [SerializeField] private float damping = 8.0f;
    [SerializeField] private float maxAcceleration = 40.0f;

    [Header("Ground Check")]
    [SerializeField] private float rayMaxDistance = 10.0f;
    [SerializeField] private LayerMask groundLayerMask = ~0;

    [Header("Caught Follow")]
    [SerializeField] private float followDistance = 2.0f;
    [SerializeField] private float followSpeed = 12.0f;
    [SerializeField] private float transferCooldown = 0.3f;

    [Header("Shoot")]
    [SerializeField] private float shootSpeed = 18.0f;
    [SerializeField] private float shootLinearDamping;
    [SerializeField] private float shootCatchDelay = 0.1f;
    [SerializeField] private float shootTargetSearchDistance = 12.0f;

    [Header("Line Of Sight")]
    [SerializeField] private LayerMask shootObstacleMask;

    [Header("Ball Visual")]
    [SerializeField] private BallPolarity polarity = BallPolarity.North;
    [SerializeField] private Color northColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField] private Color southColor = new Color(0.2f, 0.4f, 1f);
    [SerializeField] private float emissionIntensity = 15.0f;

    private Renderer _ballRenderer;
    private Material _runtimeMaterial;

    private Rigidbody _myRb;

    private MagnetPlayer _caughtPlayer;
    private Rigidbody _caughtPlayerRb;

    private MagnetPlayer _playerInRange1;
    private MagnetPlayer _playerInRange2;
    private MagnetPlayer _shootOwner;

    private Vector3 _recordedMoveDirection = Vector3.right;

    private float _nextTransferTime;
    private float _shootCatchEnableTime;

    private bool _defaultUseGravity;
    private float _defaultLinearDamping;

    private void Awake()
    {
        _myRb = GetComponent<Rigidbody>();

        if (_myRb == null)
        {
            Debug.LogWarning($"{nameof(N_Baller)}: Rigidbody が見つかりません。");
            enabled = false;
            return;
        }

        // 初期物理設定を記録しておく
        _defaultUseGravity = _myRb.useGravity;
        _defaultLinearDamping = _myRb.linearDamping;

        // 球の回転暴れを防ぐ
        _myRb.freezeRotation = true;

        InitializeRendererAndMaterial();
    }

    private void Start()
    {
        // ボールの極性に応じた色を適用する
        var color = polarity == BallPolarity.North
            ? northColor
            : southColor;

        ApplyVisual(color, emissionIntensity);
    }

    private void FixedUpdate()
    {
        switch (currentState)
        {
            case BallState.Float:
                // 通常浮遊状態
                FloatOnGround();
                break;

            case BallState.Caught:
                // 持たれている時も上下浮遊は維持する
                FloatOnGround();
                UpdateCaught();
                break;

            case BallState.Shoot:
                // Shoot中はRigidbodyの速度に任せる
                break;
        }
    }

    private void OnTriggerEnter(Collider collider3D)
    {
        var player = collider3D.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;

        RegisterPlayerInRange(player);

        if (!CanCatch(player)) return;

        if (currentState == BallState.Float)
        {
            SetCaughtPlayer(player);
            return;
        }

        if (currentState == BallState.Caught)
        {
            if (player == _caughtPlayer) return;
            if (Time.time < _nextTransferTime) return;

            SetCaughtPlayer(player);
            return;
        }

        if (currentState == BallState.Shoot)
        {
            if (Time.time < _shootCatchEnableTime) return;
            if (player == _shootOwner) return;

            SetCaughtPlayer(player);
        }
    }

    private void OnTriggerStay(Collider collider3D)
    {
        var player = collider3D.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;

        RegisterPlayerInRange(player);

        // Shoot中は、すでに範囲内にいるプレイヤーにも接球判定を行う
        if (currentState != BallState.Shoot) return;
        if (!CanCatch(player)) return;
        if (Time.time < _shootCatchEnableTime) return;
        if (player == _shootOwner) return;

        SetCaughtPlayer(player);
    }

    private void OnTriggerExit(Collider collider3D)
    {
        var player = collider3D.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;

        if (player == _playerInRange1)
        {
            _playerInRange1 = null;
        }

        if (player == _playerInRange2)
        {
            _playerInRange2 = null;
        }
    }

    private void FloatOnGround()
    {
        var bobOffset = Mathf.Sin(Time.time * 2.0f) * bob;

        if (currentState == BallState.Caught && _caughtPlayer != null)
        {
            // 持たれている時は、球の真下ではなくプレイヤーの高さを基準に浮遊する
            var targetY =
                _caughtPlayer.transform.position.y - 0.4f // 0.4fはプレイヤーの中心座標に対してのオフセット
                + targetHeight
                + bobOffset;

            var heightError = targetY - _myRb.position.y;
            var verticalSpeed = _myRb.linearVelocity.y;

            var acceleration =
                heightError * spring
                - verticalSpeed * damping
                + Mathf.Abs(Physics.gravity.y);

            acceleration = Mathf.Clamp(
                acceleration,
                -maxAcceleration,
                maxAcceleration
            );

            _myRb.AddForce(
                Vector3.up * acceleration,
                ForceMode.Acceleration
            );

            return;
        }

        // 通常時はボール自身の真下をRaycastして浮遊する
        if (!Physics.Raycast(
                transform.position,
                Vector3.down,
                out var hit,
                rayMaxDistance,
                groundLayerMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        var target =
            targetHeight
            + bobOffset;

        var groundHeightError = target - hit.distance;
        var groundVerticalSpeed = _myRb.linearVelocity.y;

        var groundAcceleration =
            groundHeightError * spring
            - groundVerticalSpeed * damping
            + Mathf.Abs(Physics.gravity.y);

        groundAcceleration = Mathf.Clamp(
            groundAcceleration,
            -maxAcceleration,
            maxAcceleration
        );

        _myRb.AddForce(
            Vector3.up * groundAcceleration,
            ForceMode.Acceleration
        );
    }

    private void UpdateCaught()
    {
        if (_caughtPlayer == null || _caughtPlayerRb == null)
        {
            ReleaseToFloat();
            return;
        }

        // 持ち主がS極でなくなったら、範囲内の他プレイヤーへ渡すか、Shootする
        if (!CanCatch(_caughtPlayer))
        {
            var nextPlayer = GetCatchCandidate();

            if (nextPlayer != null)
            {
                SetCaughtPlayer(nextPlayer);
            }
            else
            {
                ShootToVisiblePlayerOrFloat();
            }

            return;
        }

        FollowCaughtPlayer();
    }

    private void FollowCaughtPlayer()
    {
        var followDirection = GetCaughtFollowDirection();

        var targetPosition =
            _caughtPlayer.transform.position
            + followDirection * followDistance;

        // Y方向はFloatOnGroundに任せる
        targetPosition.y = _myRb.position.y;

        var nextPosition = Vector3.Lerp(
            _myRb.position,
            targetPosition,
            followSpeed * Time.fixedDeltaTime
        );

        _myRb.MovePosition(nextPosition);

        // 水平方向の物理速度を消して、追従と物理速度が喧嘩しないようにする
        var velocity = _myRb.linearVelocity;
        velocity.x = 0f;
        velocity.z = 0f;
        _myRb.linearVelocity = velocity;

        _myRb.angularVelocity = Vector3.zero;
    }

    private Vector3 GetCaughtFollowDirection()
    {
        var otherPlayer = FindOtherPlayer(_caughtPlayer);

        // もう一人のプレイヤーが見えているなら、その方向を優先する
        if (CanSeePlayer(_caughtPlayer, otherPlayer))
        {
            var directionToOther =
                otherPlayer.transform.position - _caughtPlayer.transform.position;

            directionToOther.y = 0f;

            if (directionToOther.sqrMagnitude > 0.001f)
            {
                return directionToOther.normalized;
            }
        }

        // 障害物がある場合は、今まで通りプレイヤーの移動方向を使う
        var playerVelocity = _caughtPlayerRb.linearVelocity;
        playerVelocity.y = 0f;

        if (playerVelocity.sqrMagnitude > 0.5f)
        {
            _recordedMoveDirection = playerVelocity.normalized;
        }

        return _recordedMoveDirection;
    }

    private void ShootToVisiblePlayerOrFloat()
    {
        if (_caughtPlayer == null)
        {
            ReleaseToFloat();
            return;
        }

        var targetPlayer = FindOtherPlayer(_caughtPlayer);

        // もう一人のプレイヤーが見えていない場合はShootせずFloatへ戻す
        if (!CanSeePlayer(_caughtPlayer, targetPlayer))
        {
            ReleaseToFloat();
            return;
        }

        var shootDirection =
            targetPlayer.transform.position - transform.position;

        shootDirection.y = 0f;

        if (shootDirection.sqrMagnitude <= 0.001f)
        {
            ReleaseToFloat();
            return;
        }

        shootDirection.Normalize();

        _shootOwner = _caughtPlayer;

        ClearCaughtPlayer();

        // Shoot中は重力と通常Dampingを切る
        _myRb.useGravity = false;
        _myRb.linearDamping = shootLinearDamping;
        _myRb.angularVelocity = Vector3.zero;
        _myRb.linearVelocity = shootDirection * shootSpeed;

        _shootCatchEnableTime = Time.time + shootCatchDelay;

        currentState = BallState.Shoot;
    }

    private bool CanSeePlayer(MagnetPlayer fromPlayer, MagnetPlayer targetPlayer)
    {
        if (fromPlayer == null) return false;
        if (targetPlayer == null) return false;

        var origin = fromPlayer.transform.position;
        var direction = targetPlayer.transform.position - origin;

        direction.y = 0f;

        var distance = direction.magnitude;

        if (distance <= 0.001f) return false;
        if (distance > shootTargetSearchDistance) return false;

        direction.Normalize();

        // 障害物Layerが設定されていない場合は、距離条件だけで見えている扱いにする
        if (shootObstacleMask.value == 0)
        {
            return true;
        }

        // プレイヤー間に障害物があるか確認する
        var blocked = Physics.Raycast(
            origin,
            direction,
            distance,
            shootObstacleMask,
            QueryTriggerInteraction.Ignore
        );

        return !blocked;
    }

    private MagnetPlayer FindOtherPlayer(MagnetPlayer owner)
    {
        var players = FindObjectsByType<MagnetPlayer>(FindObjectsSortMode.None);

        foreach (var player in players)
        {
            if (player == null) continue;
            if (player == owner) continue;

            return player;
        }

        return null;
    }

    private void SetCaughtPlayer(MagnetPlayer player)
    {
        if (!CanCatch(player)) return;

        RestoreDefaultPhysics();

        _caughtPlayer = player;
        _caughtPlayerRb = player.GetComponent<Rigidbody>();

        if (_caughtPlayerRb == null)
        {
            ReleaseToFloat();
            return;
        }

        caughtByPlayer = player.playerNumber;

        // 新しく持った瞬間の移動方向も記録する
        var playerVelocity = _caughtPlayerRb.linearVelocity;
        playerVelocity.y = 0f;

        if (playerVelocity.sqrMagnitude > 0.5f)
        {
            _recordedMoveDirection = playerVelocity.normalized;
        }

        _nextTransferTime = Time.time + transferCooldown;
        currentState = BallState.Caught;
    }

    private bool CanCatch(MagnetPlayer player)
    {
        // S極のプレイヤーだけがキャッチできる
        return player != null &&
               player.currentPolarity == MagnetPlayer.Polarity.South;
    }

    private MagnetPlayer GetCatchCandidate()
    {
        if (_playerInRange1 != null &&
            _playerInRange1 != _caughtPlayer &&
            CanCatch(_playerInRange1))
        {
            return _playerInRange1;
        }

        if (_playerInRange2 != null &&
            _playerInRange2 != _caughtPlayer &&
            CanCatch(_playerInRange2))
        {
            return _playerInRange2;
        }

        return null;
    }

    private void RegisterPlayerInRange(MagnetPlayer player)
    {
        if (player.playerNumber == 1)
        {
            _playerInRange1 = player;
        }
        else if (player.playerNumber == 2)
        {
            _playerInRange2 = player;
        }
    }

    private void ReleaseToFloat()
    {
        RestoreDefaultPhysics();
        ClearCaughtPlayer();
        currentState = BallState.Float;
    }

    public void FinishShoot()
    {
        ReleaseToFloat();
        _shootOwner = null;
    }

    private void ClearCaughtPlayer()
    {
        _caughtPlayer = null;
        _caughtPlayerRb = null;
        caughtByPlayer = 0;
    }

    private void RestoreDefaultPhysics()
    {
        _myRb.useGravity = _defaultUseGravity;
        _myRb.linearDamping = _defaultLinearDamping;
    }

    private void InitializeRendererAndMaterial()
    {
        if (_ballRenderer == null)
        {
            _ballRenderer = GetComponent<Renderer>();

            if (_ballRenderer == null)
            {
                _ballRenderer = GetComponentInChildren<Renderer>();
            }
        }

        if (_ballRenderer == null) return;

        if (_runtimeMaterial == null)
        {
            _runtimeMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _runtimeMaterial.EnableKeyword("_EMISSION");
            _ballRenderer.material = _runtimeMaterial;
        }
    }

    private void ApplyVisual(Color baseColor, float intensity)
    {
        InitializeRendererAndMaterial();

        if (_runtimeMaterial == null) return;

        _runtimeMaterial.SetColor(BaseColor, baseColor);
        _runtimeMaterial.SetColor(EmissionColor, baseColor * intensity);
        _runtimeMaterial.EnableKeyword("_EMISSION");
    }
}