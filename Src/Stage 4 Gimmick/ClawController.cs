using System.Collections;
using UnityEngine;

public class ClawController : MonoBehaviour
{
    public enum ClawState
    {
        Inactive,
        Active,
    }

    [SerializeField] private ClawState currentState = ClawState.Inactive;
    public ClawState CurrentClawState => currentState;

    [Header("Claw Status")]
    [SerializeField] private float magneticPower = 30.0f;
    public float ClawPower => magneticPower;

    [Header("Panel Data")]
    [SerializeField] private GameObject myPanel;
    [SerializeField] private Vector3 offSet = Vector3.zero;
    [SerializeField] private float posMoveSpeed = 2.0f;
    [SerializeField] private float resetRotateSpeed = 360.0f;

    // ===== ADD: Crane control data =====
    [Header("Crane Control")]
    [SerializeField] private Transform movePart;
    [SerializeField] private float turnSpeed = 60.0f;
    [SerializeField] private float maxTurnAngle = 45.0f;
    [SerializeField] private float moveSpeedY = 2.0f;
    [SerializeField] private float minMoveY = -2.0f;
    [SerializeField] private float maxMoveY = 2.0f;

    private enum PanelPolarity
    {
        NoPolarity,
        North,
        South
    }

    [Header("Panel Visual")]
    [SerializeField] private MeshRenderer myPanelMeshRenderer;
    [SerializeField] private PanelPolarity panelPolarity = PanelPolarity.NoPolarity;
    [SerializeField] private Material inactiveMaterial;
    [SerializeField] private Material northMaterial;
    [SerializeField] private Material southMaterial;

    // Runtime data
    private MagnetPlayer _usingPlayer;
    private Coroutine _moveRoutine;

    private bool _usingPanel;
    private float _nowTurnAngle;
    private Vector3 _movePartStartLocalPos;

    private void Start()
    {
        SetPanelPolarity(PanelPolarity.NoPolarity);
        SetPanelMaterial(inactiveMaterial);

        // Save start local position
        if (movePart != null)
        {
            _movePartStartLocalPos = movePart.localPosition;
        }
    }

    // Read panel input every frame
    private void Update()
    {
        if (!_usingPanel) return;
        if (!_usingPlayer || !_usingPlayer.gameObject.activeInHierarchy)
        {
            ExitPanel();
            return;
        }

        if (!CheckIfCorrectPolarity(_usingPlayer))
        {
            ExitPanel();
            return;
        }

        ControlCrane();
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;
        if (player.IsArmored) return;

        switch (player.playerNumber)
        {
            case 1:
                SetPanelPolarity(PanelPolarity.South);
                SetPanelMaterial(southMaterial);
                break;

            case 2:
                SetPanelPolarity(PanelPolarity.North);
                SetPanelMaterial(northMaterial);
                break;
        }

        if (!CheckIfCorrectPolarity(player)) return;

        SetClawState(ClawState.Active);

        // Stop old coroutine correctly
        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }

        _moveRoutine = StartCoroutine(GoToPanelCenter(player));
    }

    private void OnTriggerStay(Collider other)
    {
        var player = other.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;
        if (player.IsArmored) return;

        if (CheckIfCorrectPolarity(player))
        {
            SetClawState(ClawState.Active);

            // Stop old coroutine correctly
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            _moveRoutine = StartCoroutine(GoToPanelCenter(player));
            return;
        }

        if (_usingPlayer == player)
        {
            ExitPanel();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;

        if (_usingPlayer == player)
        {
            ExitPanel();
        }
    }

    private IEnumerator GoToPanelCenter(MagnetPlayer player)
    {
        _usingPlayer = player;

        // Use same target position for distance check and MoveTowards
        var targetPos = myPanel.transform.position + offSet;

        // Smooth reset rotation to 0,0,0
        var targetRot = Quaternion.identity;

        while (Vector3.Distance(player.transform.position, targetPos) > 0.001f && (Quaternion.Angle(player.transform.rotation, targetRot) > 0.1f))
        {
            player.transform.position = Vector3.MoveTowards(
                player.transform.position,
                targetPos,
                posMoveSpeed * Time.deltaTime
            );

            player.transform.rotation = Quaternion.RotateTowards(
                player.transform.rotation,
                targetRot,
                resetRotateSpeed * Time.deltaTime
            );

            yield return null;
        }

        player.transform.position = targetPos;
        player.transform.rotation = targetRot;

        EnterPanel(player);

        _moveRoutine = null;
    }

    // Enter panel control
    private void EnterPanel(MagnetPlayer player)
    {
        _usingPlayer = player;
        _usingPanel = true;

        player.SetPanelInputLocked(true);

        // 操作方法を画面の下に出す。押しても何も起きない、と思われるのを防ぐ
        ControlHintUI.ShowClaw(player.playerNumber);

        SetClawState(ClawState.Active);
    }

    // ADD: Exit panel control
    private void ExitPanel()
    {
        _usingPanel = false;

        ControlHintUI.Hide();

        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }

        if (_usingPlayer != null)
        {
            _usingPlayer.SetPanelInputLocked(false);
        }

        _usingPlayer = null;

        SetClawState(ClawState.Inactive);
        SetPanelPolarity(PanelPolarity.NoPolarity);
        SetPanelMaterial(inactiveMaterial);
    }

    public void ForceExitPanel()
    {
        ExitPanel();

        var detectors = GetComponentsInChildren<ClawDetector>(true);

        foreach (var detector in detectors)
        {
            if (detector == null) continue;
            detector.ForceReset();
        }
    }

    // Use current player's WASD / stick to control crane
    private void ControlCrane()
    {
        if (movePart == null) return;
        if (LocalInputAssignmentManager.Instance == null) return;

        var input = LocalInputAssignmentManager.Instance.GetMoveInput(
            _usingPlayer.playerNumber
        );

        TurnPart(input.x);
        MovePartY(input.y);
    }

    // A/D rotate same object
    private void TurnPart(float inputX)
    {
        _nowTurnAngle += inputX * turnSpeed * Time.deltaTime;
        _nowTurnAngle = Mathf.Clamp(
            _nowTurnAngle,
            -maxTurnAngle,
            maxTurnAngle
        );

        movePart.localRotation = Quaternion.Euler(
            0f,
            _nowTurnAngle,
            0f
        );
    }

    // W/S move same object up and down
    private void MovePartY(float inputY)
    {
        var localPos = movePart.localPosition;

        localPos.y += inputY * moveSpeedY * Time.deltaTime;
        localPos.y = Mathf.Clamp(
            localPos.y,
            _movePartStartLocalPos.y + minMoveY,
            _movePartStartLocalPos.y + maxMoveY
        );

        movePart.localPosition = localPos;
    }

    private void SetClawState(ClawState state)
    {
        currentState = state;
    }

    private void SetPanelPolarity(PanelPolarity polarity)
    {
        panelPolarity = polarity;
    }

    private void SetPanelMaterial(Material material)
    {
        if (myPanelMeshRenderer == null) return;
        if (material == null) return;

        myPanelMeshRenderer.material = material;
    }

    private bool CheckIfCorrectPolarity(MagnetPlayer player)
    {
        return
            (panelPolarity == PanelPolarity.South &&
             player.currentPolarity == MagnetPlayer.Polarity.North &&
             player.playerNumber == 1)
            ||
            (panelPolarity == PanelPolarity.North &&
             player.currentPolarity == MagnetPlayer.Polarity.South &&
             player.playerNumber == 2);
    }
}