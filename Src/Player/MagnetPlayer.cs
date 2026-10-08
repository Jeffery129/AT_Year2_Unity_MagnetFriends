using System.Linq;
using UnityEngine.VFX;
using UnityEngine;

public class MagnetPlayer : MonoBehaviour
{
    public enum Polarity { South, North }

    [Header("Player Settings")]
    public int playerNumber = 1; // 1 or 2
    public Polarity currentPolarity = Polarity.South;

    [Header("Movement")]
    public float moveSpeed = 5f;

    [Tooltip("カメラ基準が変わった際、スティックを倒したままでも新基準へ強制切替するまでの秒数")]
    public float inputLatchTimeout = 1f;
    public float jumpForce = 8f;
    private float _originMoveSpeed;
    private float _originJumpForce;
    public float sphereRadius = 1.0f;
    public float lerpSpeed = 5f;
    [SerializeField] private float fallAcceleration = 8.0f;
    private bool _isInMagnetRange = false;
    private bool _isInExternalMagnetRange = false;

    [Header("Magnet Settings")]
    public float magnetForce = 15f;
    public float magnetRange = 10f;
    public float horizontalForceMultiplier = 2.5f;
    public float verticalForceMultiplier = 0.4f;

    [SerializeField] private float releaseImpulseForce = 10.0f;
    [SerializeField] private float releaseImpulseHeightThreshold = 0.1f;
    private bool _shouldApplyReleaseImpulse = false;

    [Header("Visual - Polarity Colors")]
    [SerializeField] private float connectDistance = 1.5f; // Distance to be "connected"
    [SerializeField] private float disconnectDistance = 1.3f; // Distance to be "disconnected"
    [SerializeField] private float attractDistance = 5f; // Distance to show attract effect
    public Color southColor = new Color(0.2f, 0.4f, 1f); // Blue
    public Color northColor = new Color(1f, 0.2f, 0.2f); // Red

    [Header("Iron Armor")]
    [SerializeField] private int howManyNeeded = 40;
    [SerializeField] private float baseIronCatchRadius = 0.8f;
    [SerializeField] private float armorTime = 3.0f;
    [SerializeField] private float armorBonus = 0.5f;
    [SerializeField] private float howHeavyPerRock = 0.01f;
    [SerializeField] private GameObject boostEffectPrefab;
    [SerializeField] private GameObject landingEffectPrefab;
    [SerializeField] private float landingEffectMinFallSpeed = 2.0f;
    [SerializeField] private Vector3 boostSmokeOffset = Vector3.zero;
    [SerializeField] private Vector3 landSmokeOffset = Vector3.zero;
    private readonly float _landingEffectLife = 1.0f;
    public float BaseIronCatchRadius => baseIronCatchRadius;
    public bool IsArmored { get; private set; }
    public int RockCnt { get; private set; }
    //Smoke Effect
    private GameObject _armorSmokeObj;
    private VisualEffect _armorSmokeVfx;
    private bool _isSmokePlaying;
    //Boost Status
    private float _armorT; //スピード満タン後の経過時間
    public bool ArmorBoost { get; private set; }
    private SphereCollider _ironCatchAreaCollider;

    //Magnet Catch
    private bool _isCaughtByCrane;

    //Geometry Dash
    private bool _isInGeometryDash;
    public bool InGeoDash => _isInGeometryDash;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckMargin = 0.08f;
    [SerializeField] private float minGroundCheckDistance = 1.0f;

    [Header("Audio")]
    public AudioClip jumpSound;
    public AudioClip connectSound;
    public AudioClip attractLoopSound;
    public AudioClip armoredSound;

    private Rigidbody _rb;
    private Renderer _meshRenderer;
    private MagnetPlayer _otherPlayer;

    [Header("Status")]
    public bool isGrounded;

    // オンラインで相手側から届いた速度（kinematic なので Rigidbody からは取れない）
    private Vector3 _networkVelocity;
    private AudioSource _audioSource;
    private AudioSource _attractAudioSource; // Separate source for loop sound

    // Connection state
    public bool isConnected = false;
    private bool _wasConnected = false;
    private bool _wasAttracting = false;

    // Effect objects
    private Outline _myOutline;
    private GameObject _connectGlow;
    private LineRenderer _attractLine;
    private static MagnetEffectManager _effectManager;

    // Input keys based on player number
    private Vector2 _moveInput = Vector2.zero;

    // カメラ基準入力のラッチ用
    private float _appliedInputYaw = 0f;
    private bool _inputYawInitialized = false;
    private float _inputLatchTimer = 0f;
    private Vector3 _currentVelocity = Vector3.zero;
    private bool _isPanelInputLocked = false;
    public bool PanelLocked => _isPanelInputLocked;

    // Rotation lock (for goal/result screen)
    private bool _isRotationLocked = false;
    private float _lockedRotationY = 0f;

    // Physics material for no friction
    private PhysicsMaterial _noFrictionMaterial;

    // シーソー上フラグ
    private bool _onSeesaw = false;

    // オンラインモード用フラグ
    [HideInInspector]
    public bool isLocalPlayerControlled = true; // デフォルトはtrue（ローカルモード用）

    private void Awake()
    {
        _originMoveSpeed = moveSpeed;
        _originJumpForce = jumpForce;
    }

    private void Start()
    {
        _myOutline = GetComponent<Outline>();
        _rb = GetComponent<Rigidbody>();
        _meshRenderer = GetComponent<Renderer>();
        // If no renderer on this object, try to find one in children (for custom models)
        if (_meshRenderer == null)
        {
            _meshRenderer = GetComponentInChildren<Renderer>();
        }
        _ironCatchAreaCollider = GetComponentInChildren<IronCatchArea>().GetComponent<SphereCollider>();

        // シングルプレイモードの場合は必ずローカル操作を有効にする
        // オンラインプレイ後にシングルプレイを始めた時のフラグリセット用
        ResetToLocalMode();

        // Create no-friction physics material
        CreateNoFrictionMaterial();

        // Setup audio source for one-shot sounds
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;

        // Setup separate audio source for attract loop
        _attractAudioSource = gameObject.AddComponent<AudioSource>();
        _attractAudioSource.playOnAwake = false;
        _attractAudioSource.spatialBlend = 0f;
        _attractAudioSource.loop = true;

        // Find the other player
        MagnetPlayer[] players = FindObjectsByType<MagnetPlayer>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player != this)
            {
                _otherPlayer = player;
                break;
            }
        }

        // Set controls based on player number
        if (playerNumber == 1)
        {
            currentPolarity = Polarity.North;

            // Create effect manager only once (on Player1)
            if (_effectManager == null)
            {
                GameObject managerObj = new GameObject("MagnetEffectManager");
                _effectManager = managerObj.AddComponent<MagnetEffectManager>();
            }
        }
        else
        {
            currentPolarity = Polarity.South;
        }

        // Create glow effect object
        CreateGlowEffect();
        UpdateVisual();
        CreateBoostSmokeEffect();
    }

    private void CreateBoostSmokeEffect()
    {
        if (boostEffectPrefab == null) return;

        _armorSmokeObj = Instantiate(boostEffectPrefab);
        _armorSmokeObj.name = $"ArmoredSmokeEffect_P{playerNumber}";

        _armorSmokeVfx = _armorSmokeObj.GetComponentInChildren<VisualEffect>(true);

        StopArmoredSmoke();
    }

    private void CreateNoFrictionMaterial()
    {
        // Create a physics material with zero friction
        _noFrictionMaterial = new PhysicsMaterial("NoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f
        };

        // Apply to all colliders on this object
        var colliders = GetComponents<Collider>();
        foreach (var col in colliders)
        {
            col.material = _noFrictionMaterial;
        }
    }

    private void CreateGlowEffect()
    {
        _connectGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _connectGlow.name = $"ConnectGlow_P{playerNumber}";
        _connectGlow.transform.SetParent(transform);
        _connectGlow.transform.localPosition = Vector3.zero;
        _connectGlow.transform.localScale = Vector3.one * 1.5f;

        // Remove collider
        Destroy(_connectGlow.GetComponent<Collider>());

        // Create glowing material
        var glowRenderer = _connectGlow.GetComponent<Renderer>();
        var glowMat = new Material(Shader.Find("Sprites/Default"))
        {
            color = new Color(1f, 1f, 0.5f, 0.3f)
        };
        glowRenderer.material = glowMat;

        _connectGlow.SetActive(false);
    }

    private void Update()
    {
        // ポーズ中と操作説明中は何もしない。
        // 操作説明中は timeScale が 0 で物理が進まないため、ここでジャンプを受けると
        // AddForce(Impulse) が溜まりつづけ、ゲーム開始の瞬間に全部まとめて効いてしまう
        if (GameManager.Instance is not null && (GameManager.Instance.isPaused || GameManager.Instance.IsTutorialActive))
            return;

        // オンラインで相手が動かしているプレイヤーは、操作と接地判定はこちらでしない。
        // ただし見た目とエフェクトはこちらでも動かす。止めるとゲスト側で何も出なくなる
        var remote = ShouldSkipInput();

        var wasGrounded = isGrounded;
        var previousYVelocity = VisualVelocity.y;

        if (!remote)
        {
            // Check camera mode
            CheckCameraMode();

            // KeyBindManagerから自分(playerNumber)のキーを読み取る
            ReadInput();
            if (_isPanelInputLocked)
            {
                _moveInput = Vector2.zero;
            }
            HandleIronArmorSpeed();

            // Skip movement and input when rotation is locked (goal reached)
            if (!_isRotationLocked && !_isPanelInputLocked)
            {
                HandleNewMovementAndRoll();
            }
            else if (_isRotationLocked)
            {
                transform.rotation = Quaternion.Euler(0f, _lockedRotationY, 0f);
            }

            CheckGrounded();
        }

        IsArmored = HandleIsArmoredCntCheck();
        HandleLandingEffect(wasGrounded, previousYVelocity);
        HandleArmoredSmokeEffect();
        UpdateMagnetEffects();
        UpdateAudioVolume();
    }

    private void FixedUpdate()
    {
        // Don't apply forces when paused or rotation locked
        if (GameManager.Instance is not null && (GameManager.Instance.isPaused || GameManager.Instance.IsTutorialActive))
            return;

        // オンラインモードで自分の操作対象でない場合は物理処理をスキップ
        if (ShouldSkipInput())
            return;

        // Stop Rb while using panel
        if (_isPanelInputLocked)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            return;
        }

        // Stop movement when rotation locked
        if (_isRotationLocked)
        {
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            return;
        }

        var curXZ = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        var targetXZ = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
        var speed = GetMoveSpeed();
        var newXZ = Vector3.MoveTowards(
            curXZ, 
            targetXZ,
            speed * lerpSpeed * Time.fixedDeltaTime
        );
        _rb.linearVelocity = new Vector3(newXZ.x, _rb.linearVelocity.y, newXZ.z);

        _isInMagnetRange = false;

        ApplyMagnetForce();
        ApplyFallCompensation();
        HandleIronArmorGravity();

        if (!_rb.freezeRotation && isConnected)
        {
            _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, Vector3.zero, Time.fixedDeltaTime * 50.0f);
        }

        // シーソーフラグをリセット（OnCollisionStayで再設定される）
        _onSeesaw = false;
    }

    // Get key from KeyBindManager or use default
    // オンラインモードでは常に1Pのキーバインドを使用（各プレイヤーが自分のPCで操作するため）
    #region Input System Callbacks

    // KeyBindManagerから自分(playerNumber)のキーを読み取って入力を処理する
    // 1P/2P別キー・キーコンフィグに対応(KeyBindManagerが唯一の入力源)
    private void ReadInput()
    {
        LocalInputAssignmentManager inputManager = LocalInputAssignmentManager.Instance;

        if (inputManager == null)
        {
            _moveInput = Vector2.zero;
            return;
        }

        // --- 移動入力 ---
        _moveInput = inputManager.GetMoveInput(playerNumber);

        // --- ジャンプ ---
        if (inputManager.WasJumpPressedThisFrame(playerNumber) && isGrounded)
        {
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            if (jumpSound != null && GameManager.Instance != null && GameManager.Instance.IsGameplayActive)
            {
                _audioSource.PlayOneShot(jumpSound, 0.7f * GetSEVolume());
            }
        }

        // --- 極性切替 ---
        if (inputManager.WasPolarityPressedThisFrame(playerNumber))
        {
            currentPolarity = currentPolarity == Polarity.South
                ? Polarity.North
                : Polarity.South;

            UpdateVisual();

            // 切り替えた「先」の極でSEを分ける
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySE(
                    currentPolarity == Polarity.North
                        ? SoundID.Player_PolarityToNorth
                        : SoundID.Player_PolarityToSouth
                );
            }
        }
    }

    #endregion

    private void HandleNewMovementAndRoll()
    {
        var targetDir = GetMoveDirection();
        var speed = GetMoveSpeed();

        var targetInputVel = Vector3.ClampMagnitude(targetDir, 1f) * speed;

        var currentLerpSpeed = (_currentVelocity.magnitude >= targetInputVel.magnitude) 
            ? lerpSpeed 
            : lerpSpeed * 2f;

        _currentVelocity = Vector3.Lerp(
            _currentVelocity, 
            targetInputVel, 
            currentLerpSpeed * Time.deltaTime
        );

        var isActivelyMoving = _currentVelocity.sqrMagnitude > 0.01f;
        _rb.freezeRotation = isActivelyMoving;
        // ---- 移動をRigidbody速度ベースで適用(C案) ----
        // 入力ぶんの水平目標速度へ、現在のXZ速度を寄せる。全上書きしないので
        // 外部から加わる力(キューブ引力・磁石反発)が速度に残り、持てる/反発する。

        // ---- 見た目の転がり回転(実際の水平速度に合わせて回す) ----
        if (isActivelyMoving)
        {
            var curXZ = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            if (curXZ.sqrMagnitude > 0.001f)
            {
                var rollDist = curXZ.magnitude * Time.deltaTime;
                var rotationAxis = Vector3.Cross(Vector3.up, curXZ.normalized);
                var angle = (rollDist / sphereRadius) * Mathf.Rad2Deg;

                transform.Rotate(rotationAxis, angle, Space.World);
            }
        }
    }

    // カメラ基準ヨーを解決する。基準が変わっても入力中は古い値を保持する（ラッチ）。
    // TopDownView以外はGetInputYawが常に0を返すので、この処理は素通りし既存挙動と完全に同一。
    private float ResolveInputYaw()
    {
        CameraFollowTwo3D camRef = CameraFollowTwo3D.Instance;
        if (camRef == null)
        {
            return _appliedInputYaw;
        }

        float targetYaw = camRef.GetInputYaw();

        // 初回は問答無用で同期させる
        if (!_inputYawInitialized)
        {
            _appliedInputYaw = targetYaw;
            _inputYawInitialized = true;
            _inputLatchTimer = 0f;
            return _appliedInputYaw;
        }

        // 基準が変わっていなければ何もしない
        if (Mathf.Abs(Mathf.DeltaAngle(targetYaw, _appliedInputYaw)) < 0.01f)
        {
            _inputLatchTimer = 0f;
            return _appliedInputYaw;
        }

        // 基準が変わった：ニュートラルに戻ったら切替、倒しっぱなしなら保持
        bool isNeutral = _moveInput.sqrMagnitude <= 0.01f;
        _inputLatchTimer += Time.deltaTime;

        if (isNeutral || _inputLatchTimer >= inputLatchTimeout)
        {
            _appliedInputYaw = targetYaw;
            _inputLatchTimer = 0f;
        }

        return _appliedInputYaw;
    }

    private Vector3 GetMoveDirection()
    {
        if (IsGeometryDashMode)
        {
            // Geometry Dash:
            // Left / Right input moves on the world Z axis.
            return new Vector3(0f, 0f, _moveInput.x);
        }

        if (IsTopDownMode)
        {
            // Top Down:
            // Left / Right moves on X.
            // Up / Down moves on Z.
            Vector3 dir = new Vector3(_moveInput.x, 0f, _moveInput.y);
            return Quaternion.Euler(0f, ResolveInputYaw(), 0f) * dir;
        }

        // Side Scroll:
        // Left / Right moves on X.
        return new Vector3(_moveInput.x, 0f, 0f);
    }

    private void CheckCameraMode()
    {
        CameraFollowTwo3D cam = CameraFollowTwo3D.Instance;

        if (cam == null)
        {
            IsTopDownMode = false;
            IsGeometryDashMode = false;
            return;
        }

        switch (cam.currentMode)
        {
            case CameraFollowTwo3D.CameraMode.SideScroll:
                IsTopDownMode = false;
                IsGeometryDashMode = false;
                break;

            case CameraFollowTwo3D.CameraMode.GeometryDash:
                IsTopDownMode = false;
                IsGeometryDashMode = true;
                break;

            case CameraFollowTwo3D.CameraMode.TopDownView:
            case CameraFollowTwo3D.CameraMode.TopDownViewVertical:
            case CameraFollowTwo3D.CameraMode.QuarterView:
            case CameraFollowTwo3D.CameraMode.LowAngle:
            case CameraFollowTwo3D.CameraMode.LowAngle2:
                IsTopDownMode = true;
                IsGeometryDashMode = false;
                break;

            default:
                IsTopDownMode = true;
                IsGeometryDashMode = false;
                break;
        }
    }

    private float GetSEVolume()
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.seVolume * GameManager.Instance.masterVolume;
        }
        return 0.5f;
    }

    private void UpdateAudioVolume()
    {
        // Update attract loop volume in real-time
        if (_attractAudioSource != null && _attractAudioSource.isPlaying)
        {
            _attractAudioSource.volume = 0.5f * GetSEVolume();
        }
    }

    /// <summary>
    /// 本体の色を塗る。トゥーンシェーダーには _Color が無く material.color がエラーになるため、
    /// シェーダーが持っているプロパティを見て塗り分ける。
    /// </summary>
    private void ApplyBodyColor(Color c)
    {
        if (_meshRenderer == null) return;

        Material mat = _meshRenderer.material;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
    }

    private void UpdateVisual()
    {
        if (_meshRenderer != null)
        {
            ApplyBodyColor((currentPolarity == Polarity.South) ? southColor : northColor);
        }
    }

    /// <summary>
    /// Force update visual - ensures meshRenderer is initialized and updates color
    /// Called by NetworkPlayer for reliable remote polarity sync
    /// </summary>
    public void ForceUpdateVisual()
    {
        // meshRendererがnullなら再取得を試みる
        if (_meshRenderer == null)
        {
            _meshRenderer = GetComponent<Renderer>();
            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponentInChildren<Renderer>();
            }
        }

        if (_meshRenderer != null)
        {
            Color targetColor = currentPolarity == Polarity.South ? southColor : northColor;
            ApplyBodyColor(targetColor);
            Debug.Log($"[MagnetPlayer] Player{playerNumber} ForceUpdateVisual: {currentPolarity} -> {targetColor}");
        }
        else
        {
            Debug.LogWarning($"[MagnetPlayer] Player{playerNumber} ForceUpdateVisual: meshRenderer is null!");
        }
    }

    private bool _magnetEffectsSuppressed = false;

    /// <summary>
    /// ゴール時などに磁力エフェクトを完全に止めて、以降二度と出さないようにする。
    /// Time.timeScale = 0 のリザルト中はパーティクルが止まったまま残ってしまうので、ここで消しきる。
    /// </summary>
    public void SuppressMagnetEffects()
    {
        _magnetEffectsSuppressed = true;
        StopMagnetEffects();
        if (_effectManager != null) _effectManager.StopEffect();
    }

    private void UpdateMagnetEffects()
    {
        if (_magnetEffectsSuppressed) return;
        if (_otherPlayer == null) return;
        if ((IsArmored || _otherPlayer.IsArmored) || (PanelLocked || _otherPlayer.PanelLocked) || (InGeoDash || _otherPlayer.InGeoDash))
        {
            StopMagnetEffects();
            return;
        }
        if (_myOutline == null) return;

        _myOutline.OutlineColor = playerNumber == 1 ? northColor : southColor;

        var distance = Vector3.Distance(transform.position, _otherPlayer.transform.position);
        var differentPolarity = (currentPolarity != _otherPlayer.currentPolarity);

        // Check connection state (close + different polarity)
        _wasConnected = isConnected;
        if (!differentPolarity)
        {
            isConnected = false;
        }
        else if (!isConnected && distance <= connectDistance)
        {
            isConnected = true;
        }
        else if (isConnected && distance > disconnectDistance)
        {
            isConnected = false;
        }

        // connected 状態から、同極になって離れる瞬間だけ Impulse を予約する
        if (_wasConnected && !isConnected && !differentPolarity && distance <= connectDistance)
        {
            var isHigherThanOther = transform.position.y > _otherPlayer.transform.position.y + releaseImpulseHeightThreshold;
            _shouldApplyReleaseImpulse = isHigherThanOther;
        }

        // Check attracting state (medium distance + different polarity)
        _wasAttracting = IsAttracting;
        IsAttracting = differentPolarity && distance > connectDistance && distance <= attractDistance;

        // Connection effect
        if (isConnected && !_wasConnected)
        {
            // Just connected - play sound (only during gameplay)
            if (connectSound != null && playerNumber == 1 && GameManager.Instance != null && GameManager.Instance.IsGameplayActive)
            {
                AudioSource.PlayClipAtPoint(connectSound, (transform.position + _otherPlayer.transform.position) / 2f, 0.8f * GetSEVolume());
            }
        }

        // Attract loop sound (only on Player1 to avoid double)
        if (playerNumber == 1)
        {
            // Stop sound during pause/result screen
            var gameplayActive = GameManager.Instance != null && GameManager.Instance.IsGameplayActive;

            if (IsAttracting && !_wasAttracting && gameplayActive)
            {
                // Started attracting - play loop
                if (attractLoopSound != null && _attractAudioSource != null)
                {
                    _attractAudioSource.clip = attractLoopSound;
                    _attractAudioSource.volume = 0.3f * GetSEVolume();
                    _attractAudioSource.Play();
                }
            }
            else if ((!IsAttracting && _wasAttracting) || !gameplayActive)
            {
                // Stopped attracting OR game not active - stop loop
                if (_attractAudioSource != null && _attractAudioSource.isPlaying)
                {
                    _attractAudioSource.Stop();
                }
            }
        }

        // Glow effect when connected (hide during result screen)
        if (_connectGlow != null)
        {
            var showGlow = isConnected && (ResultScreen.Instance == null || !ResultScreen.Instance.isShowing);
            _connectGlow.SetActive(showGlow);
            if (showGlow)
            {
                // Pulse effect
                var pulse = 1.3f + Mathf.Sin(Time.time * 8f) * 0.2f;
                _connectGlow.transform.localScale = Vector3.one * pulse;

                // Color based on polarity
                var glowColor = (currentPolarity == Polarity.South) ?
                    new Color(0.5f, 0.7f, 1f, 0.4f) : new Color(1f, 0.5f, 0.5f, 0.4f);
                _connectGlow.GetComponent<Renderer>().material.color = glowColor;
            }
        }

        // Attract line effect (only from Player1 to avoid double)
        // Hide during result screen
        if (playerNumber == 1 && _effectManager != null)
        {
            var showEffect = IsAttracting && (ResultScreen.Instance == null || !ResultScreen.Instance.isShowing);
            _effectManager.UpdateAttractEffect(transform.position, _otherPlayer.transform.position, showEffect, distance / attractDistance);
        }
    }

    private void StopMagnetEffects()
    {
        _isInMagnetRange = false;
        isConnected = false;
        IsAttracting = false;
        _wasConnected = false;
        _wasAttracting = false;
        _shouldApplyReleaseImpulse = false;

        if (_connectGlow != null)
        {
            _connectGlow.SetActive(false);
        }

        if (_attractAudioSource != null && _attractAudioSource.isPlaying)
        {
            _attractAudioSource.Stop();
        }

        if (playerNumber == 1 && _effectManager != null && _otherPlayer != null)
        {
            _effectManager.StopEffect();
        }
    }

    private void ApplyMagnetForce()
    {
        if (_otherPlayer == null) return;

        if ((IsArmored || _otherPlayer.IsArmored) ||
            (PanelLocked || _otherPlayer.PanelLocked) ||
            (InGeoDash || _otherPlayer.InGeoDash))
        {
            _isInMagnetRange = false;
            _connectGlow.SetActive(false);
            return;
        }

        var direction = _otherPlayer.transform.position - transform.position;
        if (IsGeometryDashMode)
        {
            // Geometry Dash uses YZ
            direction.x = 0f;
        }
        else if (!IsTopDownMode)
        {
            // Side Scroll uses XY
            direction.z = 0f;
        }
        var distance = direction.magnitude;

        var samePolarity = (currentPolarity == _otherPlayer.currentPolarity);

        var effectiveRange = samePolarity
            ? magnetRange * 0.85f
            : magnetRange;

        if (distance > effectiveRange) return;
        _isInMagnetRange = true;

        var forceMagnitude = magnetForce * (1f - distance / effectiveRange);
        Vector3 totalForce;
        if (IsGeometryDashMode) 
        {
            var forceDirection = direction.normalized;
            var zForce = forceDirection.z * forceMagnitude * horizontalForceMultiplier;
            var yForce = forceDirection.y * forceMagnitude * verticalForceMultiplier;
            totalForce = new Vector3(0f, yForce, zForce);
        }
        else if (IsTopDownMode)
        {
            // Top-down mode: force on XZ plane + small vertical
            var forceDirection = direction.normalized;
            var horizontalDir = new Vector3(forceDirection.x, 0f, forceDirection.z);
            if (horizontalDir.magnitude > 0.01f) horizontalDir = horizontalDir.normalized;

            var verticalComponent = forceDirection.y;

            var horizontalForce = horizontalDir * forceMagnitude * horizontalForceMultiplier;
            var verticalForce = Vector3.up * verticalComponent * forceMagnitude * verticalForceMultiplier;

            totalForce = horizontalForce + verticalForce;
        }
        else
        {
            // Side-scroll mode: force on XY plane only (no Z force)
            var forceDirection = direction.normalized;
            var xForce = forceDirection.x * forceMagnitude * horizontalForceMultiplier;
            var yForce = forceDirection.y * forceMagnitude * verticalForceMultiplier;

            totalForce = new Vector3(xForce, yForce, 0f);
        }

        // Apply force based on polarity change
        if (samePolarity)
        {
            if (_shouldApplyReleaseImpulse)
            {
                _rb.AddForce(-totalForce.normalized * releaseImpulseForce, ForceMode.Impulse);
                _shouldApplyReleaseImpulse = false;

                // くっついた状態から同極になって弾き飛ばされる瞬間
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlaySE(SoundID.Player_RepelJump);
                }
            }

            if (distance < 0.9f) return;

            _rb.AddForce(-totalForce, ForceMode.Acceleration);
        }
        else
        {
            _shouldApplyReleaseImpulse = false;

            if (distance < 0.9f) return;

            _rb.AddForce(totalForce, ForceMode.Acceleration);
        }
    }

    private void CheckGrounded()
    {
        // プレイヤーの中心から下向きにRaycast
        var rayOrigin = transform.position;

        var rayDistance = GetGroundCheckDistance();

        isGrounded = RaycastGroundIgnoringSelf(
            rayOrigin,
            Vector3.down,
            rayDistance
        );
    }

    private void ApplyFallCompensation()
    {
        if (isGrounded) return;
        if (_rb.linearVelocity.y > 0) return;
        if (_isInGeometryDash) return;
        if (_isPanelInputLocked) return;

        var activeAcceleration = IsInAnyMagnetRange()? fallAcceleration * 0.3f : fallAcceleration;

        _rb.AddForce(
            Vector3.down * activeAcceleration,
            ForceMode.Acceleration
        );
    }

    private float GetGroundCheckDistance()
    {
        var colliders = GetComponentsInChildren<Collider>();

        var hasCollider = false;
        var minY = transform.position.y;

        foreach (var col in colliders)
        {
            if (col == null) continue;
            if (!col.enabled) continue;
            if (col.isTrigger) continue;

            if (!hasCollider)
            {
                minY = col.bounds.min.y;
                hasCollider = true;
            }
            else
            {
                minY = Mathf.Min(minY, col.bounds.min.y);
            }
        }

        if (!hasCollider)
        {
            return _onSeesaw ? 1.2f : minGroundCheckDistance;
        }

        var distanceFromCenterToBottom =
            transform.position.y - minY;

        var checkDistance =
            distanceFromCenterToBottom + groundCheckMargin;

        if (_onSeesaw)
        {
            checkDistance = Mathf.Max(checkDistance, 1.2f);
        }

        return Mathf.Max(
            checkDistance,
            minGroundCheckDistance
        );
    }

    private bool RaycastGroundIgnoringSelf(Vector3 origin, Vector3 direction, float distance)
    {
        var hits = Physics.RaycastAll(
            origin,
            direction,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;

            // プレイヤー自身と、装着された鉄ブロックなどの子Colliderは無視する
            if (hit.collider.transform.IsChildOf(transform)) continue;

            if (!CanStandOn(hit.collider)) continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// 相手の上に乗って足場にできるか。
    /// オンラインでは相手の体は kinematic な「動かない壁」なので、
    /// これを無条件に地面と認めると、お互いを踏み台にして二人とも空へ登れてしまう。
    /// そこで「相手自身が地面に立っているときだけ」足場として認める。
    /// </summary>
    private bool CanStandOn(Collider col)
    {
        if (!NetSync.IsOnline) return true;
        if (col == null) return true;

        var other = col.GetComponentInParent<MagnetPlayer>();
        if (other == null || other == this) return true;

        return other.isGrounded;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 相手が動かしているプレイヤーの接地はネットワークから届く。ここで上書きしない
        if (ShouldSkipInput()) return;

        // Check if landing on a surface (normal pointing up)
        if (collision.contacts.Length > 0 && collision.contacts[0].normal.y > 0.5f)
        {
            if (!CanStandOn(collision.collider)) return;
            isGrounded = true;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        // シーソー上にいるか検出
        if (collision.gameObject.GetComponentInParent<SeesawController>() != null)
        {
            _onSeesaw = true;
        }
    }

    private void OnDestroy()
    {
        // Stop attract sound when destroyed
        if (_attractAudioSource != null && _attractAudioSource.isPlaying)
        {
            _attractAudioSource.Stop();
        }
    }

    private void OnDisable()
    {
        _isPanelInputLocked = false;
        _isCaughtByCrane = false;
        _moveInput = Vector2.zero;
        _currentVelocity = Vector3.zero;

        if (_rb == null) return;

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    // Public methods for external control
    public void LockRotation(bool locked)
    {
        _isRotationLocked = locked;
        Debug.Log($"[MagnetPlayer] Player {playerNumber}: LockRotation({locked}), lockedRotationY={_lockedRotationY}");
    }

    public void SetTargetRotation(float rotationY)
    {
        _lockedRotationY = rotationY;
        transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        Debug.Log($"[MagnetPlayer] Player {playerNumber}: SetTargetRotation({rotationY}), actual rotation={transform.eulerAngles.y}");
    }

    // Public getters
    //public bool IsConnected => isConnected;
    //public float CurrentRotationY => currentRotationY;
    public bool IsAttracting { get; private set; } = false;
    public bool IsTopDownMode { get; private set; } = false;
    public bool IsGeometryDashMode { get; private set; } = false;

    /// <summary>
    /// NetworkPlayerから呼ばれる - このプレイヤーがローカルで操作されるか設定
    /// </summary>
    public void SetIsLocalPlayer(bool isLocal)
    {
        isLocalPlayerControlled = isLocal;
        Debug.Log($"[MagnetPlayer] Player{playerNumber} SetIsLocalPlayer: {isLocal}");
    }

    /// <summary>
    /// オンラインで相手が動かしているプレイヤーの状態を、届いた値で埋める。
    /// Rigidbody が kinematic で速度を持たないので、エフェクト用に速度を控えておく。
    /// </summary>
    public void ApplyNetworkState(Vector3 velocity, bool grounded)
    {
        _networkVelocity = velocity;
        isGrounded = grounded;
    }

    /// <summary>見た目に使う速度。相手側のプレイヤーは届いた値を使う</summary>
    private Vector3 VisualVelocity
    {
        get { return ShouldSkipInput() ? _networkVelocity : _rb.linearVelocity; }
    }

    /// <summary>
    /// シングルプレイモード用にローカル操作フラグをリセット
    /// オンラインプレイ後にシングルプレイを開始した時に呼ばれる
    /// </summary>
    private void ResetToLocalMode()
    {
        // オンラインモードでない場合（シングルプレイ）は常にローカル操作を有効にする
        if (NetworkGameManager.Instance == null || !NetworkGameManager.Instance.IsOnlineMode())
        {
            isLocalPlayerControlled = true;
            Debug.Log($"[MagnetPlayer] Player{playerNumber} ResetToLocalMode: isLocalPlayerControlled = true (Single Play Mode)");
        }
    }

    /// <summary>
    /// オンラインモードかつ自分の操作対象でない場合はtrueを返す（入力スキップ用）
    /// </summary>
    private bool ShouldSkipInput()
    {
        // オンラインモードでない場合はスキップしない
        if (NetworkGameManager.Instance == null || !NetworkGameManager.Instance.IsOnlineMode())
            return false;

        // オンラインモードで、自分が操作するプレイヤーでない場合はスキップ
        return !isLocalPlayerControlled;
    }

    // Called by ClawController
    public void SetPanelInputLocked(bool locked)
    {
        _isPanelInputLocked = locked;

        _moveInput = Vector2.zero;
        _currentVelocity = Vector3.zero;

        if (_rb == null) return;

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    public void SetSlowDownDebuff(float slowDownRate)
    {
        moveSpeed = _originMoveSpeed * slowDownRate;
        jumpForce = _originJumpForce * slowDownRate;
    }

    public void ResetSlowDownDebuff()
    {
        moveSpeed = _originMoveSpeed;
        jumpForce = _originJumpForce;
    }

    public void EnterExternalMagnetRange()
    {
        _isInExternalMagnetRange = true;
    }

    public void ExitExternalMagnetRange()
    {
        _isInExternalMagnetRange = false;
    }

    public void ResetExternalMagnetRange()
    {
        _isInExternalMagnetRange = false;
    }

    private bool IsInAnyMagnetRange()
    {
        return _isInMagnetRange || _isInExternalMagnetRange;
    }

    // Iron Armor
    public void AddIronArmorRock()
    {
        RockCnt++;
        switch (RockCnt)
        {
            case > 10 and <= 22:
                baseIronCatchRadius = 1.1f;
                _ironCatchAreaCollider.radius = 1.35f;
                jumpForce = 10.5f;
                _rb.mass = 1.25f;
                sphereRadius = 0.75f;
                break;
            case > 22 and <= 34:
                baseIronCatchRadius = 1.3f;
                _ironCatchAreaCollider.radius = 1.55f;
                moveSpeed = 9.5f;
                jumpForce = 12.5f;
                _rb.mass = 1.5f;
                sphereRadius = 0.8f;
                break;
            case > 34 and <= 50:
                baseIronCatchRadius = 1.5f;
                _ironCatchAreaCollider.radius = 1.75f;
                moveSpeed = 10.0f;
                jumpForce = 15.0f;
                _rb.mass = 2.0f;
                sphereRadius = 0.85f;
                break;
        }
    }

    public void RemoveIronArmorRock()
    {
        RockCnt = Mathf.Max(0, RockCnt - 1);

        if (RockCnt >= 10) return;
        baseIronCatchRadius = 0.9f;
        _ironCatchAreaCollider.radius = 1.1f;
        jumpForce = 10.0f;
        _rb.mass = 1.0f;
        sphereRadius = 0.7f;
    }

    private bool HandleIsArmoredCntCheck()
    {
        return RockCnt >= howManyNeeded;
    }

    private void HandleIronArmorSpeed()
    {
        if (!IsArmored)
        {
            _armorT = 0f;
            ArmorBoost = false;
            return;
        }

        var hasInput = _moveInput.sqrMagnitude > 0.01f;

        if (!hasInput)
        {
            _armorT = 0f;
            ArmorBoost = false;
            return;
        }

        var xzSpeed = new Vector3(
            _rb.linearVelocity.x,
            0f,
            _rb.linearVelocity.z
        ).magnitude;

        var isMaxSpeed = xzSpeed >= moveSpeed * 0.8f;

        if (!isMaxSpeed)
        {
            // ここで ArmorBoost を落としていなかったため、一度ブーストすると
            // 歩くだけ・止まる直前でもブースト中のままになっていた。
            // 壊せる壁がただ近づいただけで消えていたのはこれが原因。
            _armorT = 0f;
            ArmorBoost = false;
            return;
        }

        _armorT += Time.deltaTime;

        if (_armorT >= armorTime)
        {
            ArmorBoost = true;
        }
    }

    private float GetMoveSpeed()
    {
        return ArmorBoost
            ? moveSpeed * (1f + armorBonus)
            : moveSpeed;
    }

    private void HandleIronArmorGravity()
    {
        if (isGrounded) return;
        if (_rb.linearVelocity.y > 0) return;
        if (_isInGeometryDash) return;
        if (_isCaughtByCrane) return;

        var armorAcceleration = RockCnt * howHeavyPerRock;

        _rb.AddForce(
            Vector3.down * armorAcceleration,
            ForceMode.Acceleration
        );
    }

    private void HandleArmoredSmokeEffect()
    {
        if (_armorSmokeObj == null) return;
        if (_armorSmokeVfx == null) return;

        var vel = VisualVelocity;

        var xzVelocity = new Vector3(
            vel.x,
            0f,
            vel.z
        );

        // 相手側のプレイヤーは入力が届かないので、動いているかどうかで代用する
        var hasInput = ShouldSkipInput()
            ? xzVelocity.sqrMagnitude > 0.01f
            : _moveInput.sqrMagnitude > 0.01f;

        var shouldPlay =
            IsArmored
            && ArmorBoost
            && isGrounded
            && hasInput
            && xzVelocity.sqrMagnitude > 0.01f;

        if (!shouldPlay)
        {
            StopArmoredSmoke();
            return;
        }

        var dir = -1 * xzVelocity.normalized;
        var horizonOffset = new Vector3(dir.x, 0.0f, dir.z);

        _armorSmokeObj.transform.position = transform.position + boostSmokeOffset + horizonOffset;

        // prefab の赤軸、つまり local X / transform.right を速度逆方向に合わせる
        _armorSmokeObj.transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);

        PlayArmoredSmoke();
    }

    private void PlayArmoredSmoke()
    {
        if (_isSmokePlaying) return;
        if (_armorSmokeVfx == null) return;

        _armorSmokeVfx.Play();

        _isSmokePlaying = true;
    }

    private void StopArmoredSmoke()
    {
        if (_armorSmokeVfx == null) return;

        _armorSmokeVfx.Stop();

        _isSmokePlaying = false;
    }

    private void HandleLandingEffect(bool wasGrounded, float previousYVelocity)
    {
        if (landingEffectPrefab == null) return;
        if (!IsArmored) return;

        // 空中から地面に変わった瞬間だけ
        if (wasGrounded) return;
        if (!isGrounded) return;

        // 下方向に十分な速度があった時だけ
        if (previousYVelocity > -landingEffectMinFallSpeed) return;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SoundID.Player_Iron_Armor_Land);
        }

        SpawnLandingEffect();
    }

    private void SpawnLandingEffect()
    {
        var effectObj = Instantiate(
            landingEffectPrefab,
            GetLandingEffectPosition(),
            Quaternion.identity
        );

        Destroy(effectObj, _landingEffectLife);
    }

    private Vector3 GetLandingEffectPosition()
    {
        var origin = transform.position;
        var result = origin + landSmokeOffset;

        return result;
    }

    public void ResetAfterDeath()
    {
        _isPanelInputLocked = false;
        _isCaughtByCrane = false;
        _isInGeometryDash = false;
        _isRotationLocked = false;
        _moveInput = Vector2.zero;
        _currentVelocity = Vector3.zero;
        _networkVelocity = Vector3.zero;
        _isInMagnetRange = false;
        _isInExternalMagnetRange = false;
        _shouldApplyReleaseImpulse = false;
        isConnected = false;
        _wasConnected = false;
        _wasAttracting = false;
        IsAttracting = false;
        ArmorBoost = false;
        _armorT = 0f;
        _onSeesaw = false;
        isGrounded = false;
        _inputYawInitialized = false;
        _inputLatchTimer = 0f;

        StopMagnetEffects();
        StopArmoredSmoke();

        if (_rb == null) _rb = GetComponent<Rigidbody>();
        if (_rb == null) return;

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = false;
        _rb.freezeRotation = false;
    }


    public void SetIsCaughtByCrane(bool state)
    {
        _isCaughtByCrane = state;
    }

    public void SetIsInGeometryDash(bool state)
    {
        _isInGeometryDash = state;
    }
}

// Manages shared effects between players
public class MagnetEffectManager : MonoBehaviour
{
    private LineRenderer _attractLine;
    private ParticleSystem _attractParticles;
    private MagnetPlayer[] _myPlayers;

    private void Start()
    {
        FindMyPlayers();
        CreateAttractLine();
        CreateAttractParticles();
    }

    private void FindMyPlayers()
    {
        _myPlayers = FindObjectsByType<MagnetPlayer>(FindObjectsSortMode.None);
    }

    private bool CheckIfIsArmored()
    {
        return _myPlayers.Any(player => player.IsArmored);
    }

    private void CreateAttractLine()
    {
        if (CheckIfIsArmored()) return;
        var lineObj = new GameObject("AttractLine");
        lineObj.transform.SetParent(transform);
        _attractLine = lineObj.AddComponent<LineRenderer>();

        _attractLine.startWidth = 0.1f;
        _attractLine.endWidth = 0.1f;
        _attractLine.positionCount = 2;
        _attractLine.useWorldSpace = true;

        // Create material
        var lineMat = new Material(Shader.Find("Sprites/Default"));
        lineMat.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        _attractLine.material = lineMat;

        _attractLine.enabled = false;
    }

    private void CreateAttractParticles()
    {
        if (CheckIfIsArmored()) return;
        var particleObj = new GameObject("AttractParticles");
        particleObj.transform.SetParent(transform);
        _attractParticles = particleObj.AddComponent<ParticleSystem>();

        var main = _attractParticles.main;
        main.startLifetime = 0.3f;
        main.startSpeed = 5f;
        main.startSize = 0.15f;
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(1f, 0.9f, 0.3f, 0.8f);

        var emission = _attractParticles.emission;
        emission.rateOverTime = 30f;

        var shape = _attractParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var colorOverLifetime = _attractParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.2f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = gradient;

        var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

        _attractParticles.Stop();
    }

    public void UpdateAttractEffect(Vector3 pos1, Vector3 pos2, bool isAttracting, float distanceRatio)
    {
        if (CheckIfIsArmored())
        {
            StopEffect();
            return;
        }

        if (_attractLine != null)
        {
            _attractLine.enabled = isAttracting;

            if (isAttracting)
            {
                _attractLine.SetPosition(0, pos1);
                _attractLine.SetPosition(1, pos2);

                float width = Mathf.Lerp(0.15f, 0.05f, distanceRatio)
                              * (1f + Mathf.Sin(Time.time * 10f) * 0.3f);

                _attractLine.startWidth = width;
                _attractLine.endWidth = width;

                float alpha = Mathf.Lerp(0.8f, 0.3f, distanceRatio);
                _attractLine.material.color = new Color(1f, 0.8f, 0.2f, alpha);
            }
        }

        if (_attractParticles != null)
        {
            if (isAttracting && !_attractParticles.isPlaying)
            {
                _attractParticles.Play();
            }
            else if (!isAttracting && _attractParticles.isPlaying)
            {
                _attractParticles.Stop();
            }

            if (isAttracting)
            {
                Vector3 midpoint = (pos1 + pos2) / 2f;
                _attractParticles.transform.position = midpoint;

                var velocityOverLifetime = _attractParticles.velocityOverLifetime;
                velocityOverLifetime.enabled = true;
            }
        }
    }

    public void StopEffect()
    {
        if (_attractLine != null)
        {
            _attractLine.enabled = false;
        }

        if (_attractParticles != null)
        {
            // Stop() だけだと出ている粒が残り、Time.timeScale = 0 だと消えないまま固まるので消しきる
            _attractParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _attractParticles.Clear(true);
        }
    }
}
