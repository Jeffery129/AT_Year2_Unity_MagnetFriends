using System.Linq;
using UnityEngine;

public class ChunkWallManager : MonoBehaviour
{
    [SerializeField] private GameObject myLockDoor;

    [Header("Broken Slow Down")]
    [SerializeField] private float slowDownTime = 2.0f;
    [SerializeField] private float slowDownScale = 0.5f;

    private float _timer;
    private float _originTimeScale = 1.0f;
    private float _originFixedDeltaTime;

    private MagnetPlayer[] _players;

    private bool _isBroken = false;
    public bool IsBroken => _isBroken;
    private bool _isSlowing;
    private bool _isSlowed;

    private void Start()
    {
        _players = FindObjectsByType<MagnetPlayer>(FindObjectsSortMode.None);
        _originFixedDeltaTime = Time.fixedDeltaTime;
    }

    private bool _lastShown = true;

    private void Update()
    {
        if (myLockDoor != null)
        {
            if (!_isBroken)
            {
                bool boost = CheckPlayerIsArmoredAndBoost();
                bool show = !boost;

                // 壁が出たり消えたりした瞬間だけ、その理由を書き出す。
                // 「勝手に壊れる」のが破壊判定なのか、装甲ブーストの検出なのかを切り分けるため。
                if (show != _lastShown)
                {
                    _lastShown = show;
                    //Debug.Log($"[ChunkWall] 壁を{(show ? "出す" : "消す")} / 破壊された={!_isBroken} / " +
                    //          $"装甲ブースト中={boost} / 経過 {Time.timeSinceLevelLoad:0.00} 秒" + DumpPlayers());
                }

                myLockDoor.SetActive(show);
            }
            else
            {
                myLockDoor.SetActive(false);
            }
        }

        UpdateBrokenSlowDown();
    }

    private string DumpPlayers()
    {
        if (_players == null) return " / プレイヤー参照なし";
        var sb = new System.Text.StringBuilder();
        foreach (var p in _players)
        {
            if (p == null) { sb.Append(" / (消えたプレイヤー)"); continue; }
            sb.Append($" / P{p.playerNumber} 石={p.RockCnt} 装甲={p.IsArmored} ブースト={p.ArmorBoost}");
        }
        return sb.ToString();
    }

    private bool CheckPlayerIsArmoredAndBoost()
    {
        return _players != null && _players.Any(p => p.IsArmored && p.ArmorBoost);
    }

    public void SetIsBroken(bool check)
    {
        // 呼び出し元は Easy Destructible Wall の DestructionManager。
        // 破片の健康度が尽きたときに false が来る。
        Debug.Log($"[ChunkWall] 壁が壊れました / {name} / 経過 {Time.timeSinceLevelLoad:0.00} 秒");
        _isBroken = check;
    }

    public void BrokenSlowDown()
    {
        if (_isSlowed) return;

        if (!_isSlowing)
        {
            _originTimeScale = Time.timeScale;
            _originFixedDeltaTime = Time.fixedDeltaTime;
        }

        _timer = 0f;
        _isSlowing = true;

        Time.timeScale = slowDownScale;
        Time.fixedDeltaTime = _originFixedDeltaTime * slowDownScale;
    }

    private void UpdateBrokenSlowDown()
    {
        if (!_isSlowing) return;

        _timer += Time.unscaledDeltaTime;

        if (_timer < slowDownTime) return;

        EndBrokenSlowDown();
    }

    private void EndBrokenSlowDown()
    {
        Time.timeScale = _originTimeScale;
        Time.fixedDeltaTime = _originFixedDeltaTime;

        _timer = 0f;
        _isSlowing = false;
        _isSlowed = true;
    }

    private void OnDisable()
    {
        if (!_isSlowing) return;

        ResetTimeScaleOnly();
    }

    private void OnDestroy()
    {
        if (!_isSlowing) return;

        ResetTimeScaleOnly();
    }

    private void ResetTimeScaleOnly()
    {
        Time.timeScale = _originTimeScale;
        Time.fixedDeltaTime = _originFixedDeltaTime;

        _timer = 0f;
        _isSlowing = false;
    }
}