using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MagneticIron : MonoBehaviour
{
    public enum IronSize
    {
        Small,
        Large
    }

    public enum Polarity
    {
        None,
        South,
        North
    }

    public enum PolarityInheritance
    {
        Same,
        Opposite
    }

    [Header("基本設定")]

    [Tooltip("Smallはプレイヤーから影響を受け、LargeはSmallから影響を受ける")]
    [SerializeField]
    private IronSize size = IronSize.Small;

    [Header("Small専用：プレイヤーとの磁力")]

    [Tooltip("プレイヤーと同じ極性になるか、反対の極性になるか")]
    [SerializeField]
    private PolarityInheritance inheritanceMode =
        PolarityInheritance.Opposite;

    [Tooltip("プレイヤーから磁力を受ける範囲")]
    [Min(0.01f)]
    [SerializeField]
    private float playerInfluenceRange = 6.0f;

    [Tooltip("プレイヤーとSmallの磁力の強さ")]
    [Min(0.0f)]
    [SerializeField]
    private float playerForce = 15.0f;

    [Tooltip("異極の場合に安定しようとする中心間距離")]
    [Min(0.0f)]
    [SerializeField]
    private float playerAttractionRestDistance = 1.0f;

    [Tooltip("プレイヤーへ引き寄せられる際の振動を抑える値")]
    [Min(0.0f)]
    [SerializeField]
    private float playerAttractionDamping = 4.0f;

    [Tooltip("プレイヤー磁力の最大加速度")]
    [Min(0.0f)]
    [SerializeField]
    private float maxPlayerAcceleration = 40.0f;

    [Tooltip("プレイヤー磁力の横方向倍率")]
    [Min(0.0f)]
    [SerializeField]
    private float playerHorizontalMultiplier = 2.5f;

    [Tooltip("プレイヤー磁力の上下方向倍率")]
    [Min(0.0f)]
    [SerializeField]
    private float playerVerticalMultiplier = 0.2f;

    [Header("Small専用：非磁化時の停止")]

    [Tooltip("プレイヤーが範囲外へ出た後の水平減速度")]
    [Min(0.0f)]
    [SerializeField]
    private float idleDeceleration = 20.0f;

    [Tooltip("この速度以下になったら水平速度を完全に0にする")]
    [Min(0.0f)]
    [SerializeField]
    private float stopSpeedThreshold = 0.05f;

    [Header("Large専用：Smallとの磁力")]

    [Tooltip("Largeが常に持つ極性。Noneにすると磁力が発生しない")]
    [SerializeField]
    private Polarity fixedLargePolarity = Polarity.North;

    [Tooltip("SmallとLargeの磁力が作用する範囲")]
    [Min(0.01f)]
    [SerializeField]
    private float smallInteractionRange = 6.0f;

    [Tooltip("異極のSmallとLargeが安定しようとする中心間距離")]
    [Min(0.0f)]
    [SerializeField]
    private float attractionRestDistance = 1.2f;

    [Tooltip("異極のSmallとLargeが引き合う強さ")]
    [Min(0.0f)]
    [SerializeField]
    private float attractionSpringStrength = 20.0f;

    [Tooltip("SmallとLargeが引き合う際の振動を抑える値")]
    [Min(0.0f)]
    [SerializeField]
    private float attractionDamping = 8.0f;

    [Tooltip("同極のSmallとLargeが反発する強さ")]
    [Min(0.0f)]
    [SerializeField]
    private float repulsionForce = 40.0f;

    [Tooltip("SmallとLarge間で発生する最大加速度")]
    [Min(0.0f)]
    [SerializeField]
    private float maxInteractionAcceleration = 60.0f;

    [Tooltip("SmallとLarge間の横方向倍率")]
    [Min(0.0f)]
    [SerializeField]
    private float interactionHorizontalMultiplier = 1.0f;

    [Tooltip("SmallとLarge間の上下方向倍率")]
    [Min(0.0f)]
    [SerializeField]
    private float interactionVerticalMultiplier = 0.2f;

    [Tooltip("Largeが動くために必要な、範囲内の磁化済みSmallの数")]
    [Min(1)]
    [SerializeField]
    private int requiredSmallCount = 1;

    [Header("Large専用：直接押し防止")]

    [Tooltip("Playerとの物理衝突で動きにくくするLargeの質量")]
    [Min(1.0f)]
    [SerializeField]
    private float largeContactMass = 500.0f;

    [Tooltip("Largeの磁力移動が止まりやすくなる水平減速度")]
    [Min(0.0f)]
    [SerializeField]
    private float largeMotionDamping = 5.0f;

    [Header("移動制限")]

    [Tooltip("この鉄オブジェクトの水平最高速度")]
    [Min(0.0f)]
    [SerializeField]
    private float maxSpeed = 3.0f;

    [Header("Sound")]
    [Min(0.0f)]
    [SerializeField] private float moveSoundSpeedThreshold = 0.05f; // 移動音を鳴らす速度の閾値

    [Header("見た目")]

    [Tooltip("極性に応じて色を変更するRenderer")]
    [SerializeField]
    private Renderer targetRenderer;

    [SerializeField]
    private Color noneColor = Color.gray;

    [SerializeField]
    private Color southColor =
        new Color(0.2f, 0.4f, 1.0f);

    [SerializeField]
    private Color northColor =
        new Color(1.0f, 0.2f, 0.2f);

    [Header("実行時確認用")]

    [SerializeField]
    private Polarity currentPolarity = Polarity.None;

    private Rigidbody _rigidbody;
    private MaterialPropertyBlock _materialPropertyBlock;

    // Largeの水平速度は磁力システムだけで管理する。
    // Playerとの衝突によって加わった水平速度は毎FixedUpdateで上書きされる。
    private Vector3 _largeControlledHorizontalVelocity =
        Vector3.zero;

    private bool _wasMoving = false;

    private static readonly List<MagneticIron> _allIrons =
        new List<MagneticIron>();

    public IronSize Size => size;

    public Polarity CurrentPolarity =>
        currentPolarity;

    internal static IReadOnlyList<MagneticIron> AllIrons =>
        _allIrons;

    internal Rigidbody Body =>
        _rigidbody;

    internal PolarityInheritance InheritanceMode =>
        inheritanceMode;

    internal float PlayerInfluenceRange =>
        playerInfluenceRange;

    internal float PlayerForce =>
        playerForce;

    internal float PlayerAttractionRestDistance =>
        playerAttractionRestDistance;

    internal float PlayerAttractionDamping =>
        playerAttractionDamping;

    internal float MaxPlayerAcceleration =>
        maxPlayerAcceleration;

    internal float PlayerHorizontalMultiplier =>
        playerHorizontalMultiplier;

    internal float PlayerVerticalMultiplier =>
        playerVerticalMultiplier;

    internal Polarity FixedLargePolarity =>
        fixedLargePolarity;

    internal float SmallInteractionRange =>
        smallInteractionRange;

    internal float AttractionRestDistance =>
        attractionRestDistance;

    internal float AttractionSpringStrength =>
        attractionSpringStrength;

    internal float AttractionDamping =>
        attractionDamping;

    internal float RepulsionForce =>
        repulsionForce;

    internal float MaxInteractionAcceleration =>
        maxInteractionAcceleration;

    internal float InteractionHorizontalMultiplier =>
        interactionHorizontalMultiplier;

    internal float InteractionVerticalMultiplier =>
        interactionVerticalMultiplier;

    internal int RequiredSmallCount =>
        Mathf.Max(1, requiredSmallCount);

    internal Vector3 InteractionVelocity
    {
        get
        {
            if (_rigidbody == null)
            {
                return Vector3.zero;
            }

            if (size == IronSize.Large)
            {
                return new Vector3(
                    _largeControlledHorizontalVelocity.x,
                    _rigidbody.linearVelocity.y,
                    _largeControlledHorizontalVelocity.z
                );
            }

            return _rigidbody.linearVelocity;
        }
    }

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();

        if (size == IronSize.Large)
        {
            _rigidbody.mass =
                Mathf.Max(1.0f, largeContactMass);

            _largeControlledHorizontalVelocity =
                Vector3.zero;
        }

        if (targetRenderer == null)
        {
            targetRenderer =
                GetComponent<Renderer>();

            if (targetRenderer == null)
            {
                targetRenderer =
                    GetComponentInChildren<Renderer>();
            }
        }

        _materialPropertyBlock =
            new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (!_allIrons.Contains(this))
        {
            _allIrons.Add(this);
        }

        if (size == IronSize.Large)
        {
            SetRuntimePolarity(
                fixedLargePolarity
            );
        }
        else
        {
            SetRuntimePolarity(
                Polarity.None
            );
        }
    }

    private void OnDisable()
    {
        _allIrons.Remove(this);
        _largeControlledHorizontalVelocity =
            Vector3.zero;

        // オブジェクトが無効になった場合、移動音を止める
        _wasMoving = false;
    }

    private void OnValidate()
    {
        playerInfluenceRange =
            Mathf.Max(0.01f, playerInfluenceRange);

        playerForce =
            Mathf.Max(0.0f, playerForce);

        playerAttractionRestDistance =
            Mathf.Max(
                0.0f,
                playerAttractionRestDistance
            );

        playerAttractionDamping =
            Mathf.Max(
                0.0f,
                playerAttractionDamping
            );

        maxPlayerAcceleration =
            Mathf.Max(
                0.0f,
                maxPlayerAcceleration
            );

        playerHorizontalMultiplier =
            Mathf.Max(
                0.0f,
                playerHorizontalMultiplier
            );

        playerVerticalMultiplier =
            Mathf.Max(
                0.0f,
                playerVerticalMultiplier
            );

        idleDeceleration =
            Mathf.Max(
                0.0f,
                idleDeceleration
            );

        stopSpeedThreshold =
            Mathf.Max(
                0.0f,
                stopSpeedThreshold
            );

        smallInteractionRange =
            Mathf.Max(
                0.01f,
                smallInteractionRange
            );

        attractionRestDistance =
            Mathf.Max(
                0.0f,
                attractionRestDistance
            );

        attractionSpringStrength =
            Mathf.Max(
                0.0f,
                attractionSpringStrength
            );

        attractionDamping =
            Mathf.Max(
                0.0f,
                attractionDamping
            );

        repulsionForce =
            Mathf.Max(
                0.0f,
                repulsionForce
            );

        maxInteractionAcceleration =
            Mathf.Max(
                0.0f,
                maxInteractionAcceleration
            );

        interactionHorizontalMultiplier =
            Mathf.Max(
                0.0f,
                interactionHorizontalMultiplier
            );

        interactionVerticalMultiplier =
            Mathf.Max(
                0.0f,
                interactionVerticalMultiplier
            );

        requiredSmallCount =
            Mathf.Max(
                1,
                requiredSmallCount
            );

        largeContactMass =
            Mathf.Max(
                1.0f,
                largeContactMass
            );

        largeMotionDamping =
            Mathf.Max(
                0.0f,
                largeMotionDamping
            );

        maxSpeed =
            Mathf.Max(
                0.0f,
                maxSpeed
            );

        moveSoundSpeedThreshold =
            Mathf.Max(
                0.0f,
                moveSoundSpeedThreshold
            );

        if (Application.isPlaying &&
            _rigidbody != null &&
            size == IronSize.Large)
        {
            _rigidbody.mass =
                largeContactMass;

            SetRuntimePolarity(
                fixedLargePolarity
            );
        }
    }

    internal void SetRuntimePolarity(
        Polarity newPolarity
    )
    {
        currentPolarity = newPolarity;
        UpdateVisual();
    }

    internal void ApplyIdleBrake()
    {
        if (size != IronSize.Small)
        {
            return;
        }

        // プレイヤーから磁化されている間は停止処理をしない。
        if (currentPolarity != Polarity.None)
        {
            return;
        }

        if (_rigidbody == null ||
            _rigidbody.isKinematic)
        {
            return;
        }

        Vector3 velocity =
            _rigidbody.linearVelocity;

        Vector3 horizontalVelocity =
            new Vector3(
                velocity.x,
                0.0f,
                velocity.z
            );

        horizontalVelocity =
            Vector3.MoveTowards(
                horizontalVelocity,
                Vector3.zero,
                idleDeceleration *
                Time.fixedDeltaTime
            );

        if (horizontalVelocity.sqrMagnitude <=
            stopSpeedThreshold *
            stopSpeedThreshold)
        {
            horizontalVelocity =
                Vector3.zero;
        }

        _rigidbody.linearVelocity =
            new Vector3(
                horizontalVelocity.x,
                velocity.y,
                horizontalVelocity.z
            );
    }

    internal void ApplyLargeControlledMotion(
     Vector3 magneticAcceleration,
     bool magnetActive
 )
    {
        if (size != IronSize.Large)
        {
            return;
        }

        if (_rigidbody == null ||
            _rigidbody.isKinematic)
        {
            // 動かせない状態になった場合は音を停止
            _wasMoving = false;
            return;
        }

        float fixedDeltaTime = Time.fixedDeltaTime;

        Vector3 horizontalAcceleration =
            new Vector3(
                magneticAcceleration.x,
                0.0f,
                magneticAcceleration.z
            );

        if (!magnetActive)
        {
            // 磁力が働いていないときはLargeを停止させる。
            // Playerとの衝突によって発生した水平速度も消す。
            _largeControlledHorizontalVelocity =
                Vector3.zero;
        }
        else if (horizontalAcceleration.sqrMagnitude > 0.0001f)
        {
            // 磁力が働いている間は減速させず、
            // 磁力による速度をそのまま追加する。
            _largeControlledHorizontalVelocity +=
                horizontalAcceleration *
                fixedDeltaTime;
        }
        else
        {
            // Smallが範囲内にいるが、磁力がほぼ0の場合だけ減速する。
            _largeControlledHorizontalVelocity =
                Vector3.MoveTowards(
                    _largeControlledHorizontalVelocity,
                    Vector3.zero,
                    largeMotionDamping *
                    fixedDeltaTime
                );
        }

        // Largeの最高速度を制限する。
        if (maxSpeed > 0.0f &&
            _largeControlledHorizontalVelocity.magnitude >
            maxSpeed)
        {
            _largeControlledHorizontalVelocity =
                _largeControlledHorizontalVelocity.normalized *
                maxSpeed;
        }

        Vector3 currentVelocity =
            _rigidbody.linearVelocity;

        // Playerとの衝突で発生したXZ速度は使用せず、
        // 磁力による速度だけをLargeへ適用する。
        _rigidbody.linearVelocity =
            new Vector3(
                _largeControlledHorizontalVelocity.x,
                currentVelocity.y,
                _largeControlledHorizontalVelocity.z
            );

        // 上下方向の磁力は通常の加速度として適用する。
        if (Mathf.Abs(magneticAcceleration.y) >
            0.0001f)
        {
            _rigidbody.AddForce(
                new Vector3(
                    0.0f,
                    magneticAcceleration.y,
                    0.0f
                ),
                ForceMode.Acceleration
            );
        }

        // 移動音の再生/停止
        UpdateIronMoveSound();
    }

    private void UpdateIronMoveSound()
    {
        if (size != IronSize.Large)
        {
            return;
        }

        bool isMoving = _largeControlledHorizontalVelocity.sqrMagnitude >
            moveSoundSpeedThreshold * moveSoundSpeedThreshold;

        if(isMoving && !_wasMoving)
        {
            if(SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySE3D(SoundID.Env_IronBlockMove, transform.position);
            }
        }

        _wasMoving = isMoving;
    }

    internal void ClampSmallHorizontalSpeed()
    {
        if (size != IronSize.Small)
        {
            return;
        }

        if (_rigidbody == null ||
            maxSpeed <= 0.0f)
        {
            return;
        }

        Vector3 horizontalVelocity =
            new Vector3(
                _rigidbody.linearVelocity.x,
                0.0f,
                _rigidbody.linearVelocity.z
            );

        if (horizontalVelocity.magnitude <=
            maxSpeed)
        {
            return;
        }

        horizontalVelocity =
            horizontalVelocity.normalized *
            maxSpeed;

        _rigidbody.linearVelocity =
            new Vector3(
                horizontalVelocity.x,
                _rigidbody.linearVelocity.y,
                horizontalVelocity.z
            );
    }

    private void UpdateVisual()
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (_materialPropertyBlock == null)
        {
            _materialPropertyBlock =
                new MaterialPropertyBlock();
        }

        Color targetColor;

        switch (currentPolarity)
        {
            case Polarity.South:
                targetColor = southColor;
                break;

            case Polarity.North:
                targetColor = northColor;
                break;

            default:
                targetColor = noneColor;
                break;
        }

        targetRenderer.GetPropertyBlock(
            _materialPropertyBlock
        );

        // URP/Lit
        _materialPropertyBlock.SetColor(
            "_BaseColor",
            targetColor
        );

        // Standard Shaderなど
        _materialPropertyBlock.SetColor(
            "_Color",
            targetColor
        );

        targetRenderer.SetPropertyBlock(
            _materialPropertyBlock
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (size == IronSize.Small)
        {
            Gizmos.color = Color.yellow;

            Gizmos.DrawWireSphere(
                transform.position,
                playerInfluenceRange
            );
        }
        else
        {
            Gizmos.color = Color.cyan;

            Gizmos.DrawWireSphere(
                transform.position,
                smallInteractionRange
            );
        }
    }
}