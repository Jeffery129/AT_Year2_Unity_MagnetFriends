using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CircleEffect : MonoBehaviour
{
    [Header("Circle Decal")]
    [SerializeField] private GameObject circleDecalPrefab;
    [SerializeField] private LayerMask circleSurfaceMask;

    [Header("Ray cast")]
    [SerializeField] private float rayDistance = 10f;
    [SerializeField] private float surfaceOffset = 0.05f;

    [Header("Scale")]
    [SerializeField] private float minHeight = 0.5f;
    [SerializeField] private float maxHeight = 8.0f;
    [SerializeField] private float minSize = 0.8f;
    [SerializeField] private float maxSize = 4.0f;

    [Header("Decal")]
    [SerializeField] private float projectionDepth = 3.0f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRay = false;

    private MagnetPlayer _magnetPlayer;
    private GameObject _circleInstance;
    private DecalProjector _decalProjector;

    private void Awake()
    {
        _magnetPlayer = GetComponent<MagnetPlayer>();
    }

    private void LateUpdate()
    {
        UpdateCircle();
    }

    private void UpdateCircle()
    {
        if (_magnetPlayer != null && _magnetPlayer.isGrounded)
        {
            DestroyCircle();
            return;
        }

        var hitSurface = Physics.Raycast(
            transform.position,
            Vector3.down,
            out RaycastHit hit,
            rayDistance,
            circleSurfaceMask,
            QueryTriggerInteraction.Ignore
        );

        if (drawDebugRay)
        {
            Debug.DrawRay(
                transform.position,
                Vector3.down * rayDistance,
                hitSurface ? Color.green : Color.red
            );
        }

        if (!hitSurface)
        {
            DestroyCircle();
            return;
        }

        ShowCircle(hit);
    }

    private void ShowCircle(RaycastHit hit)
    {
        CreateCircleIfNeeded();

        if (_circleInstance == null || _decalProjector == null) return;

        _circleInstance.transform.position = hit.point + hit.normal * surfaceOffset;

        _circleInstance.transform.rotation = Quaternion.LookRotation(-hit.normal, transform.forward);

        var size = GetCircleSize(hit.distance);

        _decalProjector.size = new Vector3(
            size,
            size,
            projectionDepth
        );
    }

    private void CreateCircleIfNeeded()
    {
        if (circleDecalPrefab == null) return;
        if (_circleInstance != null) return;

        _circleInstance = Instantiate(circleDecalPrefab);
        _circleInstance.name = $"CircleDecal_{gameObject.name}";

        _decalProjector = _circleInstance.GetComponent<DecalProjector>();

        if (_decalProjector == null)
        {
            Debug.LogWarning($"{nameof(CircleEffect)}: prefab に DecalProjector がありません。");
        }
    }

    private float GetCircleSize(float height)
    {
        var t = Mathf.InverseLerp(minHeight, maxHeight, height);
        return Mathf.Lerp(minSize, maxSize, t);
    }

    private void DestroyCircle()
    {
        if (_circleInstance == null) return;

        Destroy(_circleInstance);
        _circleInstance = null;
        _decalProjector = null;
    }

    private void OnDisable()
    {
        DestroyCircle();
    }

    private void OnDestroy()
    {
        DestroyCircle();
    }
}