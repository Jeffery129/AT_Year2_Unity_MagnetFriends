using UnityEngine;

public class IronArmorRock : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private enum RockPolarity
    {
        NoPolarity,
        South,
        North
    }

    [Header("Rock Data")]
    [SerializeField] private RockPolarity myRockPolarity = RockPolarity.NoPolarity;

    [Header("Rock Material")]
    [SerializeField] private Material idleMaterial;
    [SerializeField] private Material southMaterial;
    [SerializeField] private Material northMaterial;

    [Header("Glow")]
    [SerializeField] private float glowIntensity = 1.5f;
    [SerializeField] private Color southGlowColor = new Color(0.01f, 0.03f, 1.0f, 1.0f);
    [SerializeField] private Color northGlowColor = new Color(1.0f, 0.01f, 0.01f, 1.0f);
    [SerializeField] private bool isGlowing;

    [Header("Debug")]
    [SerializeField] private Collider catchSensorCollider;
    [SerializeField] private bool inheritPlayerVelocityOnDetach = true;
    [SerializeField] private bool isColored;

    private MeshRenderer _myMeshRenderer;
    private Material _runtimeMaterial;

    private Rigidbody _myRb;
    private Collider _myCollider;

    private float _mass;
    private float _linearDamping;

    private MagnetPlayer _attachedPlayer;
    private Rigidbody _attachedPlayerRb;

    private bool _isAttached;
    public bool IsAttached => _isAttached;

    private PhysicsMaterial _noFrictionMaterial;

    private void Awake()
    {
        _myRb = GetComponent<Rigidbody>();

        if (_myRb != null)
        {
            _mass = _myRb.mass;
            _linearDamping = _myRb.linearDamping;

            _myRb.useGravity = true;
            _myRb.isKinematic = false;
        }

        _myCollider = GetComponent<Collider>();

        if (catchSensorCollider == null)
        {
            var sensor = GetComponentInChildren<IronCatchedSensor>();

            if (sensor != null)
            {
                catchSensorCollider = sensor.GetComponent<Collider>();
            }
        }
    }

    private void Start()
    {
        _myMeshRenderer = GetComponent<MeshRenderer>();
        if (_myMeshRenderer == null) return;

        UpdateMaterial();
        CreateNoFrictionMaterial();
    }

    private void Update()
    {
        if (!_isAttached) return;

        if (!_attachedPlayer)
        {
            DetachFromPlayer();
            return;
        }

        // 吸着中に同極になったら解除する
        if (!IsOppositePolarity(_attachedPlayer))
        {
            DetachFromPlayer();
            return;
        }

        HandleGlow();
    }

    private void OnDestroy()
    {
        ClearRuntimeMaterial();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isAttached) return;

        var catchArea = other.GetComponent<IronCatchArea>();
        if (catchArea == null) return;

        var player = catchArea.Owner;
        if (player == null) return;

        if (!IsOppositePolarity(player)) return;

        AttachToPlayer(player);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_isAttached) return;
        if (isColored) return;

        var player = collision.gameObject.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;

        myRockPolarity = player.currentPolarity switch
        {
            MagnetPlayer.Polarity.South => RockPolarity.South,
            MagnetPlayer.Polarity.North => RockPolarity.North,
            _ => myRockPolarity
        };

        UpdateMaterial();

        isColored = true;
    }

    private void UpdateMaterial()
    {
        if (_myMeshRenderer == null) return;

        if (myRockPolarity == RockPolarity.NoPolarity)
        {
            SetGlow(false);
            ClearRuntimeMaterial();

            _myMeshRenderer.material = idleMaterial;
            return;
        }

        var sourceMaterial = myRockPolarity switch
        {
            RockPolarity.South => southMaterial,
            RockPolarity.North => northMaterial,
            _ => null
        };

        if (sourceMaterial == null) return;

        ClearRuntimeMaterial();

        _runtimeMaterial = new Material(sourceMaterial);
        _myMeshRenderer.material = _runtimeMaterial;

        ApplyGlowMaterialState();
    }

    private void ClearRuntimeMaterial()
    {
        if (_runtimeMaterial == null) return;

        if (Application.isPlaying)
        {
            Destroy(_runtimeMaterial);
        }
        else
        {
            DestroyImmediate(_runtimeMaterial);
        }

        _runtimeMaterial = null;
    }

    private bool IsOppositePolarity(MagnetPlayer player)
    {
        return
            (player.currentPolarity == MagnetPlayer.Polarity.North &&
             myRockPolarity == RockPolarity.South)
            ||
            (player.currentPolarity == MagnetPlayer.Polarity.South &&
             myRockPolarity == RockPolarity.North);
    }

    public void TryAttachToPlayer(MagnetPlayer player)
    {
        if (_isAttached) return;
        if (player == null) return;
        if (!IsOppositePolarity(player)) return;

        AttachToPlayer(player);
    }

    private void AttachToPlayer(MagnetPlayer player)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(
                SoundID.Env_IronArmor_Attach
            );
        }

        _attachedPlayer = player;
        _attachedPlayerRb = player.GetComponent<Rigidbody>();

        _attachedPlayer.AddIronArmorRock();

        AlignNegativeGreenAxisToPlayerCenter();

        if (_myRb != null)
        {
            _myRb.linearVelocity = Vector3.zero;
            _myRb.angularVelocity = Vector3.zero;

            Destroy(_myRb);
            _myRb = null;
        }

        transform.SetParent(player.transform, true);

        MoveToIronCatchRadius();

        if (_myCollider != null)
        {
            _myCollider.enabled = true;
            _myCollider.isTrigger = false;
            _myCollider.material = _noFrictionMaterial;
        }

        if (catchSensorCollider != null)
        {
            catchSensorCollider.enabled = false;
        }

        _isAttached = true;

        HandleGlow();
    }

    /// <summary>外から強制的に引きはがす。奈落に落ちた岩を元の場所へ戻すときに使う</summary>
    public void ForceDetach()
    {
        if (!_isAttached) return;
        DetachFromPlayer();
    }

    private void DetachFromPlayer()
    {
        SetGlow(false);

        var detachVelocity = Vector3.zero;

        if (_attachedPlayerRb != null && inheritPlayerVelocityOnDetach)
        {
            detachVelocity = _attachedPlayerRb.linearVelocity;
        }

        transform.SetParent(null, true);

        if (_myCollider != null)
        {
            _myCollider.enabled = true;
            _myCollider.isTrigger = false;
        }

        if (_myRb == null)
        {
            _myRb = gameObject.AddComponent<Rigidbody>();
            _myRb.mass = _mass;
            _myRb.linearDamping = _linearDamping;
        }

        if (catchSensorCollider != null)
        {
            catchSensorCollider.enabled = true;
        }

        _myRb.useGravity = true;
        _myRb.isKinematic = false;
        _myRb.linearVelocity = detachVelocity;
        _myRb.angularVelocity = Vector3.zero;

        _attachedPlayer.RemoveIronArmorRock();

        _attachedPlayer = null;
        _attachedPlayerRb = null;

        if (_myCollider != null)
        {
            _myCollider.material = null;
        }

        _isAttached = false;
    }

    private void AlignNegativeGreenAxisToPlayerCenter()
    {
        if (_attachedPlayer == null) return;

        var directionToPlayer =
            _attachedPlayer.transform.position - transform.position;

        if (directionToPlayer.sqrMagnitude <= 0.001f) return;

        directionToPlayer.Normalize();

        // Rockの緑軸の反方向、つまり -transform.up をプレイヤー中心へ向ける
        transform.rotation =
            Quaternion.FromToRotation(
                -transform.up,
                directionToPlayer
            ) * transform.rotation;
    }

    private void MoveToIronCatchRadius()
    {
        if (_attachedPlayer == null) return;

        var directionFromPlayer =
            transform.position - _attachedPlayer.transform.position;

        if (directionFromPlayer.sqrMagnitude <= 0.001f)
        {
            // AlignNegativeGreenAxisToPlayerCenter() 後は、
            // transform.up が「プレイヤー中心から石へ向かう方向」になる
            directionFromPlayer = transform.up;
        }

        directionFromPlayer.Normalize();

        transform.position =
            _attachedPlayer.transform.position
            + directionFromPlayer * _attachedPlayer.BaseIronCatchRadius;
    }

    private void HandleGlow()
    {
        if (!_isAttached || _attachedPlayer == null)
        {
            SetGlow(false);
            return;
        }

        SetGlow(_attachedPlayer.IsArmored);
    }

    private void SetGlow(bool active)
    {
        if (isGlowing == active) return;

        isGlowing = active;

        ApplyGlowMaterialState();
    }

    private void ApplyGlowMaterialState()
    {
        if (_myMeshRenderer == null) return;

        var mat = _myMeshRenderer.material;
        if (mat == null) return;
        if (!mat.HasProperty(EmissionColor)) return;

        if (isGlowing)
        {
            Color emissionColor;

            switch (myRockPolarity)
            {
                case RockPolarity.South:
                    emissionColor = southGlowColor;
                    break;

                case RockPolarity.North:
                    emissionColor = northGlowColor;
                    break;

                default:
                    emissionColor = Color.black;
                    break;
            }

            mat.EnableKeyword("_EMISSION");
            mat.SetColor(EmissionColor, emissionColor * glowIntensity);
        }
        else
        {
            mat.SetColor(EmissionColor, Color.black);
        }
    }

    private void CreateNoFrictionMaterial()
    {
        _noFrictionMaterial = new PhysicsMaterial("NoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f
        };
    }
}