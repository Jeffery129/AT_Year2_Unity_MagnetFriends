using System;
using System.Collections;
using UnityEngine;

public class MagnetWallManager : MonoBehaviour
{
    public enum WallMode
    {
        Single,
        Double,
        Multiple
    }

    public enum PolarityMode
    {
        Single,
        Random
    }

    [Header("Mode")]
    [SerializeField] private WallMode activeMode = WallMode.Double;
    [SerializeField] private PolarityMode polarityMode = PolarityMode.Random;

    [Header("My Walls")]
    [SerializeField] private WallData[] magnetWalls;

    private WallData _singleWall;
    private WallData _wallLeft;
    private WallData _wallRight;

    private int _multipleIndex = 0;

    [Header("Cycle Time")]
    [SerializeField] private float offTime = 2.0f;
    [SerializeField] private float chargeTime = 2.0f;
    [SerializeField] private float activeTime = 2.0f;

    [Header("Status For Debug")]
    [SerializeField] private bool isPlayerInZone = false;

    private bool _nextIsLeft = true;
    private Coroutine _wallLoopCoroutine;

    private void Start()
    {
        InitializeWalls();
        SetAllWallsActive(false);
    }


    private void InitializeWalls()
    {
        _singleWall = null;
        _wallLeft = null;
        _wallRight = null;
        _multipleIndex = 0;
        _nextIsLeft = true;

        if (magnetWalls == null || magnetWalls.Length == 0)
        {
            Debug.LogWarning($"{nameof(MagnetWallManager)}: Inspector で WallData を登録してください。");
            return;
        }

        switch (activeMode)
        {
            case WallMode.Single:
                InitializeSingleMode();
                break;

            case WallMode.Double:
                InitializeDoubleMode();
                break;

            case WallMode.Multiple:
                InitializeMultipleMode();
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void InitializeSingleMode()
    {
        _singleWall = magnetWalls[0];

        if (_singleWall == null)
        {
            Debug.LogWarning($"{nameof(MagnetWallManager)}: Single モードですが magnetWalls[0] が null です。");
        }
    }
    private void InitializeDoubleMode()
    {
        foreach (var wall in magnetWalls)
        {
            if (wall == null) continue;

            switch (wall.forceDirection)
            {
                case WallData.MagnetForceDirection.Right:
                    _wallLeft = wall;
                    break;

                case WallData.MagnetForceDirection.Left:
                    _wallRight = wall;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (_wallLeft == null)
        {
            Debug.LogWarning($"{nameof(MagnetWallManager)}: Double モードですが Right 方向の WallData が見つかりません。");
        }

        if (_wallRight == null)
        {
            Debug.LogWarning($"{nameof(MagnetWallManager)}: Double モードですが Left 方向の WallData が見つかりません。");
        }
    }
    private void InitializeMultipleMode()
    {
        var hasValidWall = false;

        foreach (var wall in magnetWalls)
        {
            if (wall == null) continue;

            hasValidWall = true;
            break;
        }

        if (!hasValidWall)
        {
            Debug.LogWarning($"{nameof(MagnetWallManager)}: Multiple モードですが有効な WallData がありません。");
        }

        _multipleIndex = 0;
    }

    public void SetPlayerInZone(bool b)
    {
        isPlayerInZone = b;

        if (isPlayerInZone)
        {
            StartWallLoop();
        }
        else
        {
            StopWallLoop();
        }
    }

    //磁力壁ループの開始と停止
    private void StartWallLoop()
    {
        // オンラインではホストだけが順番と時間を決める。クライアントは配られた通りに映す
        if (!NetSync.IsHost) return;
        if (_wallLoopCoroutine != null) return;

        _wallLoopCoroutine = StartCoroutine(WallActiveLoop());
    }

    private void StopWallLoop()
    {
        if (_wallLoopCoroutine != null)
        {
            StopCoroutine(_wallLoopCoroutine);
            _wallLoopCoroutine = null;
        }

        SetAllWallsActive(false);
    }

    private IEnumerator WallActiveLoop()
    {
        while (isPlayerInZone)
        {
            //-------------------------------------
            // Off
            SetAllWallsActive(false);
            yield return new WaitForSeconds(offTime);

            if (!isPlayerInZone) break;
            //-------------------------------------
            // Charge
            var targetWall = GetNextWall();
            ApplyPolarityMode(targetWall);
            SetAllWallsActive(false);
            targetWall.SetChargeWall(true);
            Broadcast(targetWall, 1);

            if(SoundManager.Instance != null)   // telegraph sound
            {
                SoundManager.Instance.PlaySE3D(SoundID.Env_MagneticTelegraph, targetWall.transform.position);
            }

            yield return new WaitForSeconds(chargeTime);
            targetWall.SetChargeWall(false);

            if (!isPlayerInZone) break;
            //-------------------------------------
            // Active
            SetOneWallActive(targetWall);
            Broadcast(targetWall, 2);

            if(SoundManager.Instance != null)   // activate sound
            {
                SoundManager.Instance.PlaySE3D(SoundID.Env_MagneticTrigger, targetWall.transform.position);
            }

            yield return new WaitForSeconds(activeTime);
            //-------------------------------------
            // End
            SetAllWallsActive(false);
            Broadcast(null, 0);
        }

        SetAllWallsActive(false);
        Broadcast(null, 0);
        _wallLoopCoroutine = null;
    }

    private WallData GetNextWall()
    {
        switch (activeMode)
        {
            case WallMode.Single:
                return GetNextSingleWall();

            case WallMode.Double:
                return GetNextDoubleWall();

            case WallMode.Multiple:
                return GetNextMultipleWall();

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    private WallData GetNextSingleWall()
    {
        return _singleWall;
    }
    private WallData GetNextDoubleWall()
    {
        WallData targetWall = _nextIsLeft ? _wallLeft : _wallRight;

        _nextIsLeft = !_nextIsLeft;

        if (targetWall == null)
        {
            targetWall = _wallLeft != null ? _wallLeft : _wallRight;
        }

        return targetWall;
    }
    private WallData GetNextMultipleWall()
    {
        if (magnetWalls == null || magnetWalls.Length == 0)
        {
            return null;
        }

        for (var i = 0; i < magnetWalls.Length; i++)
        {
            var index = _multipleIndex % magnetWalls.Length;

            _multipleIndex++;
            _multipleIndex %= magnetWalls.Length;

            var wall = magnetWalls[index];

            if (wall != null)
            {
                return wall;
            }
        }

        return null;
    }

    /// <summary>ホストが今どの壁をどの段階にしたかを配る（0=全部オフ 1=チャージ 2=作動）</summary>
    private void Broadcast(WallData wall, int phase)
    {
        if (!NetSync.IsOnline || NetworkGameManager.Instance == null) return;
        string wallId = wall != null ? NetSync.PathId(wall.transform) : "";
        NetworkGameManager.Instance.SetWallPhaseServerRpc(NetSync.PathId(transform), wallId, phase);
    }

    /// <summary>クライアント側。配られた段階をそのまま映す</summary>
    public void ApplyPhaseFromNetwork(string wallId, int phase)
    {
        WallData w = string.IsNullOrEmpty(wallId) ? null : NetSync.Find<WallData>(wallId);
        switch (phase)
        {
            case 1:
                SetAllWallsActive(false);
                if (w != null) w.SetChargeWall(true);
                break;
            case 2:
                if (w != null)
                {
                    w.SetChargeWall(false);
                    SetOneWallActive(w);
                }
                break;
            default:
                SetAllWallsActive(false);
                if (w != null) w.SetChargeWall(false);
                break;
        }
    }

    private void SetOneWallActive(WallData targetWall)
    {
        if (magnetWalls == null) return;
        foreach (var wall in magnetWalls)
        {
            if (wall == null) continue;

            wall.SetActiveWall(wall == targetWall);
        }
    }

    private void SetAllWallsActive(bool active)
    {
        if (magnetWalls == null) return;
        foreach (var wall in magnetWalls)
        {
            if (wall == null) continue;

            wall.SetActiveWall(active);
        }
    }

    private void ApplyPolarityMode(WallData wall)
    {
        if (wall == null) return;

        switch (polarityMode)
        {
            case PolarityMode.Random:
                // オンラインで各自が乱数を引くと、同じ壁が片方は引き寄せ・片方は反発になる。
                // ホストだけが引いて、結果を両者へ配る。
                if (NetSync.IsOnline)
                {
                    if (NetSync.IsHost)
                    {
                        int roll = UnityEngine.Random.value < 0.5f ? 0 : 1;
                        NetworkGameManager.Instance.SetWallPolarityServerRpc(NetSync.PathId(wall.transform), roll);
                    }
                    break;
                }
                wall.wallPolarity = UnityEngine.Random.value < 0.5f
                    ? WallData.MagnetWallPolarity.North
                    : WallData.MagnetWallPolarity.South;
                break;

            case PolarityMode.Single:
                // WallData 側で設定されている wallPolarity をそのまま使う。
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        wall.RefreshVisual();
    }
}
