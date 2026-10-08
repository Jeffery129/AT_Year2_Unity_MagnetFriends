using UnityEngine;

public class GeometryDashZoneManager : MonoBehaviour
{
    [Header("Dash Data")]
    [SerializeField] private float rightPushPower = 2.0f;
    [SerializeField] private float cameraAngleLimit = 2.0f;

    [Header("Debug")]
    [SerializeField] private int playerCnt = 0;
    [SerializeField] private bool isWindOn = false;

    private FloorData[] _myFloors;
    private MagnetPlayer[] _myPlayers;

    private Rigidbody _player1Rb;
    private Rigidbody _player2Rb;

    private void Start()
    {
        _myPlayers = FindObjectsByType<MagnetPlayer>(
            FindObjectsSortMode.None
        );

        _myFloors = GetComponentsInChildren<FloorData>();
    }

    private void FixedUpdate()
    {
        if (!isWindOn) return;

        var cam = CameraFollowTwo3D.Instance;
        if (cam is null) return;

        if (cam.currentMode != CameraFollowTwo3D.CameraMode.GeometryDash) return;

        var angleDiff = Mathf.Abs(
            Mathf.DeltaAngle(
                cam.transform.eulerAngles.y,
                cam.dashRotation.y
            )
        );

        if (angleDiff > cameraAngleLimit) return;

        PushPlayer(_player1Rb);
        PushPlayer(_player2Rb);
    }

    private void PushPlayer(Rigidbody rb)
    {
        if (rb == null) return;

        rb.AddForce(
            Vector3.forward * rightPushPower,
            ForceMode.Acceleration
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;
        var player = other.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;
        var rb = player.GetComponent<Rigidbody>();
        if (rb == null) return;

        if (!AddPlayer(player, rb)) return;

        playerCnt = Mathf.Min(playerCnt + 1, 2);

        if (!CheckPlayerCount()) return;

        EnterGeometryDash();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.isTrigger) return;
        var player = other.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;

        if (!RemovePlayer(player)) return;

        playerCnt = Mathf.Max(playerCnt - 1, 0);

        if (CheckPlayerCount()) return;

        ExitGeometryDash();
    }

    private bool AddPlayer(MagnetPlayer player, Rigidbody rb)
    {
        switch (player.playerNumber)
        {
            case 1:
                // Player 1 is already inside.
                if (_player1Rb != null) return false;

                _player1Rb = rb;
                return true;

            case 2:
                // Player 2 is already inside.
                if (_player2Rb != null) return false;

                _player2Rb = rb;
                return true;
        }

        return false;
    }

    private bool RemovePlayer(MagnetPlayer player)
    {
        switch (player.playerNumber)
        {
            case 1:
                // Player 1 was not registered.
                if (_player1Rb == null) return false;

                _player1Rb = null;
                return true;

            case 2:
                // Player 2 was not registered.
                if (_player2Rb == null) return false;

                _player2Rb = null;
                return true;
        }

        return false;
    }

    private bool CheckPlayerCount()
    {
        return playerCnt >= 2;
    }

    private void EnterGeometryDash() 
    {
        if (isWindOn) return;
        isWindOn = true;
        SetCameraMode(CameraFollowTwo3D.CameraMode.GeometryDash);
        ChangeInGeoDash(true);
    }

    private void ExitGeometryDash()
    {
        if (!isWindOn) return;
        isWindOn = false;
        SetCameraMode(CameraFollowTwo3D.CameraMode.QuarterView);
        ChangeInGeoDash(false);
    }

    private static void SetCameraMode(CameraFollowTwo3D.CameraMode mode)
    {
        var cam = CameraFollowTwo3D.Instance;
        if (cam == null) return;

        cam.SetCameraMode(mode);
    }

    private void ChangeInGeoDash(bool inGeoDash)
    {
        if (_myPlayers == null) return;

        foreach (var player in _myPlayers)
        {
            if (player == null) continue;

            player.SetIsInGeometryDash(inGeoDash);
        }
    }

    public Vector3 GetTopDownPosInChildren(FloorData.MagnetFloorPolarity polarity)
    {
        if (_myFloors == null)
        {
            return Vector3.zero;
        }

        foreach (var floor in _myFloors)
        {
            if (floor == null) continue;

            if (floor.floorPolarity == polarity)
            {
                return floor.transform.position;
            }
        }

        return Vector3.zero;
    }
}