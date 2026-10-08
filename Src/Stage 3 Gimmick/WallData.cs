using System;
using System.Collections;
using UnityEngine;

public class WallData : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public enum MagnetWallPolarity { North, South }
    public enum MagnetForceDirection { Left, Right }

    private enum WallVisualState
    {
        Inactive,
        Charge,
        Active
    }

    private enum InactiveColor { On, Off }

    [Header("Wall Data")]
    public MagnetWallPolarity wallPolarity = MagnetWallPolarity.North;
    public MagnetForceDirection forceDirection = MagnetForceDirection.Right;
    [SerializeField] private InactiveColor inactiveGray = InactiveColor.On;
    public float colliderRange = 20.0f;
    public float wallForce = 10.0f;

    [Header("Wall Visual")]
    [SerializeField] private Renderer wallRenderer;

    [Space]
    [SerializeField] private Color inactiveColor = Color.gray;
    [SerializeField] private Color northColor = Color.red;
    [SerializeField] private Color southColor = Color.blue;

    [Space]
    [SerializeField] private Color northEmissionColor = new Color(1.0f, 0.01f, 0.01f, 1.0f);
    [SerializeField] private Color southEmissionColor = new Color(0.01f, 0.03f, 1.0f, 1.0f);

    [Header("Emission")]
    [SerializeField] private float inactiveEmissionIntensity = 0.0f;
    [SerializeField] private float chargeMinEmissionIntensity = 0.2f;
    [SerializeField] private float chargeMaxEmissionIntensity = 5.0f;
    [SerializeField] private float activeEmissionIntensity = 15.0f;
    [SerializeField] private float chargeBlinkSpeed = 8.0f;

    [Header("Wall Status")]
    [SerializeField] private bool isActive = false;
    public bool IsActive => isActive;

    private Material _runtimeMaterial;
    private Coroutine _chargeCoroutine;
    private WallVisualState _visualState = WallVisualState.Inactive;

    private void Awake()
    {
        InitializeRendererAndMaterial();
        ApplyWallMaterial();
    }

    private void Start()
    {
        ApplyWallMaterial();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) return;

        InitializeRendererAndMaterial();
        ApplyWallMaterial();
    }
#endif

    private void InitializeRendererAndMaterial()
    {
        if (wallRenderer == null)
        {
            wallRenderer = GetComponent<Renderer>();

            if (wallRenderer == null)
            {
                wallRenderer = GetComponentInChildren<Renderer>();
            }
        }

        if (wallRenderer == null) return;

        if (_runtimeMaterial != null) return;

        // インスペクタで割り当てたマテリアルを複製して使う。
        // ここで新規に作ってしまうと、見た目の調整がすべて無視される。
        if (wallRenderer.sharedMaterial != null)
        {
            _runtimeMaterial = wallRenderer.material;
        }
        else
        {
            // マテリアルが空のときだけ、最低限の URP/Lit を用意する
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                Debug.LogError("[WallData] " + name + " : URP/Lit シェーダーが見つかりません", this);
                return;
            }

            _runtimeMaterial = new Material(shader);
            wallRenderer.material = _runtimeMaterial;
        }

        _runtimeMaterial.EnableKeyword("_EMISSION");
        _runtimeMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    private Color GetPolarityColor()
    {
        return wallPolarity == MagnetWallPolarity.North
            ? northColor
            : southColor;
    }

    private Color GetEmissionColor()
    {
        return wallPolarity == MagnetWallPolarity.North ? northEmissionColor : southEmissionColor;
    }

    private void ApplyVisual(Color baseColor, float emissionIntensity)
    {
        InitializeRendererAndMaterial();
        if (_runtimeMaterial == null) return;

        var emissionColor = _visualState == WallVisualState.Inactive ? baseColor : GetEmissionColor();

        _runtimeMaterial.SetColor(BaseColor, baseColor);
        _runtimeMaterial.SetColor(EmissionColor, emissionColor * emissionIntensity);
        _runtimeMaterial.EnableKeyword("_EMISSION");
    }

    private void ApplyWallMaterial()
    {
        switch (_visualState)
        {
            case WallVisualState.Inactive:
                switch (inactiveGray)
                {
                    case InactiveColor.On:
                        ApplyVisual(inactiveColor, inactiveEmissionIntensity);
                        break;
                    case InactiveColor.Off:
                        ApplyVisual(GetPolarityColor(), inactiveEmissionIntensity);
                        break;
                }
                break;

            case WallVisualState.Charge:
                ApplyVisual(GetPolarityColor(), chargeMinEmissionIntensity);
                break;

            case WallVisualState.Active:
                ApplyVisual(GetPolarityColor(), activeEmissionIntensity);
                break;
        }
    }

    public void SetActiveWall(bool active)
    {
        StopChargeBlink();

        isActive = active;
        _visualState = active ? WallVisualState.Active : WallVisualState.Inactive;

        var force = GetComponentInChildren<WallForce>(true);

        if (force != null)
        {
            force.gameObject.SetActive(active);
        }

        ApplyWallMaterial();
    }

    public void SetChargeWall(bool charge)
    {
        if (charge)
        {
            StartChargeBlink();
        }
        else
        {
            StopChargeBlink();
            isActive = false;
            _visualState = WallVisualState.Inactive;
            ApplyWallMaterial();
        }
    }

    private void StartChargeBlink()
    {
        StopChargeBlink();

        isActive = false;
        _visualState = WallVisualState.Charge;

        var force = GetComponentInChildren<WallForce>(true);

        if (force != null)
        {
            force.gameObject.SetActive(false);
        }

        _chargeCoroutine = StartCoroutine(ChargeBlinkLoop());
    }

    private void StopChargeBlink()
    {
        if (_chargeCoroutine != null)
        {
            StopCoroutine(_chargeCoroutine);
            _chargeCoroutine = null;
        }
    }

    private IEnumerator ChargeBlinkLoop()
    {
        var elapsedTime = 0.0f;
        ApplyVisual(GetPolarityColor(), chargeMinEmissionIntensity);

        while (_visualState == WallVisualState.Charge)
        {
            elapsedTime += Time.deltaTime;
            var blink = (1.0f - Mathf.Cos(elapsedTime * chargeBlinkSpeed)) * 0.5f;


            var intensity = Mathf.Lerp(
                chargeMinEmissionIntensity,
                chargeMaxEmissionIntensity,
                blink
            );

            ApplyVisual(GetPolarityColor(), intensity);

            yield return null;
        }
    }

    public void RefreshVisual()
    {
        ApplyWallMaterial();
    }
}