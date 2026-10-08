using UnityEngine;

public class KillZone_MagnetWall : MonoBehaviour
{
    private WallData _myMagnetWall;

    [Header("Target Roots")]
    [SerializeField] private Transform razerLeft;
    [SerializeField] private Transform razerRight;

    [Header("Length Rate")]
    [SerializeField] private float upperRate = 0.5f * 0.6f;
    [SerializeField] private float lowerRate = 0.5f * 0.9f * 0.6f;

    [Header("Apply")]
    [SerializeField] private bool applyOnStart = true;

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyZap();
        }
    }

    [ContextMenu("☆Apply Zap Setting☆")]
    private void ApplyZap()
    {
        Initialize();

        if (_myMagnetWall == null)
        {
            Debug.LogWarning($"{nameof(KillZone_MagnetWall)}: WallData が見つかりません。");
            return;
        }

        // KillZone のX位置を Wall の幅の 2 倍に設定する
        var killZoneLocalPosX = GetWallWidth() * 2;
        transform.localPosition = new Vector3(killZoneLocalPosX, transform.localPosition.y, transform.localPosition.z);

        var wallLength = GetWallLength();
        var upperLength = wallLength * upperRate;
        var lowerLength = wallLength * lowerRate;

        ApplyToRazerRoot(razerLeft, lowerLength, upperLength);
        ApplyToRazerRoot(razerRight, lowerLength, upperLength);
    }

    private void Initialize()
    {
        if (_myMagnetWall == null)
        {
            _myMagnetWall = GetComponentInParent<WallData>();
        }

        AutoFindRazerRoots();
    }

    private float GetWallLength()
    {
        return _myMagnetWall == null ? 0f : _myMagnetWall.transform.localScale.z;
    }

    private float GetWallWidth()
    {
        return _myMagnetWall == null
            ? 0f 
            : _myMagnetWall.forceDirection == WallData.MagnetForceDirection.Right 
                ?      _myMagnetWall.transform.localScale.x
                : -1 * _myMagnetWall.transform.localScale.x;
    }

    private void AutoFindRazerRoots()
    {
        if (razerLeft == null)
        {
            razerLeft = transform.Find("Razer_Left");
        }

        if (razerRight == null)
        {
            razerRight = transform.Find("Razer_Right");
        }
    }

    private void ApplyToRazerRoot(Transform razerRoot, float lowerLength, float upperLength)
    {
        if (razerRoot == null) return;

        var particleSystems =
            razerRoot.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem ps in particleSystems)
        {
            if (!IsZapParticle(ps)) continue;

            ApplyStartSizeY(ps, lowerLength, upperLength);
        }
    }

    private bool IsZapParticle(ParticleSystem ps)
    {
        if (ps == null) return false;

        // Light Spawn は対象外
        if (ps.name.Contains("Light")) return false;

        // Zap / Zap LUT / Zap Add を対象にする
        return ps.name.Contains("Zap");
    }

    private void ApplyStartSizeY(ParticleSystem ps, float lowerLength, float upperLength)
    {
        var main = ps.main;

        // 3D Start Size を有効化
        main.startSize3D = true;

        // Transform Scale で粒子全体が太らないようにする
        main.scalingMode = ParticleSystemScalingMode.Shape;

        // Y を Random Between Two Constants にする
        main.startSizeY = new ParticleSystem.MinMaxCurve(lowerLength, upperLength);
    }
}