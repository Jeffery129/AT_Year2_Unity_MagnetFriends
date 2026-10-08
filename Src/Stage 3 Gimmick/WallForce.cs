using System;
using UnityEngine;

public class WallForce : MonoBehaviour
{
    public static WallForce Instance { get; private set; }

    [Header("Cover Check")]
    [SerializeField] private LayerMask coverLayerMask;
    [Header("Debug")]
    [SerializeField] private bool drawDebugRay = true;

    private WallData _myWall;
    private WallData.MagnetForceDirection _myWallDirection;

    [Header("For Sound")]
    private bool _isAttracting = false;
    private bool _isRepelling = false;
    private bool _wasAttracting = false;
    private bool _wasRepelling = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        _myWall = GetComponentInParent<WallData>();
        _myWallDirection = _myWall.forceDirection;
        SetLocalScalePosition(_myWallDirection);
    }

    private void FixedUpdate()
    {
        _wasAttracting = _isAttracting;
        _wasRepelling = _isRepelling;

        _isAttracting = false;
        _isRepelling = false;
    }

    private void LateUpdate()
    {
        UpdateMagnetSound();
    }

    private void OnTriggerStay(Collider collider3D)
    {
        if (_myWall == null) return;
        if (!_myWall.IsActive) return;

        if (collider3D.isTrigger) return;
        var player = collider3D.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;

        var rb = player.GetComponentInParent<Rigidbody>();
        if (rb == null) return;

        //☆遮蔽物に遮られている場合は磁力をかけない
        if (IsBlockedByCover(player)) return;

        var samePolarity =
        (player.currentPolarity == MagnetPlayer.Polarity.North &&
         _myWall.wallPolarity == WallData.MagnetWallPolarity.North)
        ||
        (player.currentPolarity == MagnetPlayer.Polarity.South &&
         _myWall.wallPolarity == WallData.MagnetWallPolarity.South);

        if (samePolarity)
        {
            _isRepelling = true;
        }
        else
        {
            _isAttracting = true;
        }

        //以下は磁力をかける部分
        var forcePower = _myWall.wallForce;
        var forceDir = GetForceDirection(player);

        rb.AddForce(forceDir * forcePower, ForceMode.Acceleration);
    }

    private bool IsBlockedByCover(MagnetPlayer player)
    {
        if (coverLayerMask.value == 0) return false;

        var origin = player.transform.position;
        var directionToWall = GetDirectionFromPlayerToWall();
        var distance = _myWall.colliderRange;

        var blocked = Physics.Raycast(
            origin,
            directionToWall,
            distance,
            coverLayerMask,
            QueryTriggerInteraction.Ignore  //Trigger colliderは無視される
        );

        if (drawDebugRay)
        {
            Debug.DrawRay(
                origin,
                directionToWall * distance,
                blocked ? Color.green : Color.red
            );
        }

        return blocked;
    }

    private Vector3 GetDirectionFromPlayerToWall()
    {
        switch (_myWallDirection)
        {
            case WallData.MagnetForceDirection.Right:
                return -1 * transform.right;

            case WallData.MagnetForceDirection.Left:
                return transform.right;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private Vector3 GetForceDirection(MagnetPlayer player)
    {
        var currentWallPolarity = _myWall.wallPolarity;

        var samePolarity =
            (player.currentPolarity == MagnetPlayer.Polarity.North && currentWallPolarity == WallData.MagnetWallPolarity.North)
            || (player.currentPolarity == MagnetPlayer.Polarity.South && currentWallPolarity == WallData.MagnetWallPolarity.South);

        var wallDir = _myWallDirection == WallData.MagnetForceDirection.Right
            ? transform.right
            : -transform.right;

        return samePolarity ? wallDir : -wallDir;
    }

    public void SetLocalScalePosition(WallData.MagnetForceDirection wallDirection)
    {
        if (_myWall == null) return;

        var range = _myWall.colliderRange;
        //磁力の方向と範囲に応じて設定コリジョンのスケールとローカルポジション
        switch (wallDirection)
        {
            case WallData.MagnetForceDirection.Left:
                transform.localScale = new Vector3(range, 1, 1);
                transform.localPosition = new Vector3(-1 * range * 0.5f, 0, 0);
                break;
            case WallData.MagnetForceDirection.Right:
                transform.localScale = new Vector3(range, 1, 1);
                transform.localPosition = new Vector3(+1 * range * 0.5f, 0, 0);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(wallDirection), wallDirection, null);
        }
    }

    private void UpdateMagnetSound()
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        if (_isAttracting && !_wasAttracting)
        {
            SoundManager.Instance.PlayLoopSE3D(
                SoundID.Env_Attraction_Loop,
                gameObject
            );
        }

        if (!_isAttracting && _wasAttracting)
        {
            SoundManager.Instance.StopLoopSE3D(
                SoundID.Env_Attraction_Loop,
                gameObject
            );
        }

        if (_isRepelling && !_wasRepelling)
        {
            SoundManager.Instance.PlayLoopSE3D(
                SoundID.Env_PushAway_Loop,
                gameObject
            );
        }

        if (!_isRepelling && _wasRepelling)
        {
            SoundManager.Instance.StopLoopSE3D(
                SoundID.Env_PushAway_Loop,
                gameObject
            );
        }
    }
}
