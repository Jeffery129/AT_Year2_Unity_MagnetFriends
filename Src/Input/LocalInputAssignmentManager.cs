using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LocalInputAssignmentManager : MonoBehaviour
{
    private static LocalInputAssignmentManager _instance;

    /// <summary>
    /// シーンに置き忘れていても動くように、無ければその場で作る。
    /// （ステージ3やタイトルには置かれていないため）
    /// </summary>
    public static LocalInputAssignmentManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<LocalInputAssignmentManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("LocalInputAssignmentManager");
                    _instance = go.AddComponent<LocalInputAssignmentManager>();
                }
            }
            return _instance;
        }
        private set { _instance = value; }
    }

    private const string BindingOverridesKey = "PlayerControls_BindingOverrides";

    [Header("Debug")]
    [SerializeField] private string player1Source;
    [SerializeField] private string player2Source;

    private PlayerInputSlot _player1Input;
    private PlayerInputSlot _player2Input;

    private readonly HashSet<Gamepad> _assignedGamepads = new HashSet<Gamepad>();

    private bool _ready;

    private void Awake()
    {
        if (_instance == null || _instance == this)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureInit();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 入力の枠を用意する。Awake が走らない経路で作られても動くように、
    /// 外から使うところは必ずここを通す。
    /// </summary>
    private void EnsureInit()
    {
        if (_ready && _player1Input != null && _player2Input != null) return;
        _ready = true;

        string bindingOverridesJson = LoadBindingOverridesJson();

        if (_player1Input == null) _player1Input = new PlayerInputSlot(bindingOverridesJson);
        if (_player2Input == null) _player2Input = new PlayerInputSlot(bindingOverridesJson);

        AssignInitialInputs();

        InputSystem.onDeviceChange -= OnDeviceChange;
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void Update()
    {
        if (_player1Input == null || _player2Input == null) EnsureInit();
        AssignNewGamepadIfInputDetected();
        UpdateDebugText();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            InputSystem.onDeviceChange -= OnDeviceChange;

            _player1Input?.Dispose();
            _player2Input?.Dispose();

            _instance = null;
        }
    }

    private void AssignInitialInputs()
    {
        _assignedGamepads.Clear();

        int gamepadCount = Gamepad.all.Count;

        if (gamepadCount <= 0)
        {
            AssignKeyboardBoth();
            return;
        }

        if (gamepadCount == 1)
        {
            AssignKeyboardAndOneGamepad(Gamepad.all[0]);
            return;
        }

        AssignTwoGamepadsRandomly();
    }

    private void AssignKeyboardBoth()
    {
        _player1Input.SetKeyboardP1();
        _player2Input.SetKeyboardP2();
    }

    private void AssignKeyboardAndOneGamepad(Gamepad gamepad)
    {
        _player1Input.SetKeyboardP1();

        _player2Input.SetGamepad(gamepad);
        _assignedGamepads.Add(gamepad);
    }

    private void AssignTwoGamepadsRandomly()
    {
        List<Gamepad> gamepads = new List<Gamepad>();

        foreach (var gamepad in Gamepad.all)
        {
            if (gamepad != null)
            {
                gamepads.Add(gamepad);
            }
        }

        if (gamepads.Count <= 0)
        {
            AssignKeyboardBoth();
            return;
        }

        if (gamepads.Count == 1)
        {
            AssignKeyboardAndOneGamepad(gamepads[0]);
            return;
        }

        int firstIndex = UnityEngine.Random.Range(0, gamepads.Count);
        Gamepad first = gamepads[firstIndex];

        gamepads.RemoveAt(firstIndex);

        int secondIndex = UnityEngine.Random.Range(0, gamepads.Count);
        Gamepad second = gamepads[secondIndex];

        _player1Input.SetGamepad(first);
        _player2Input.SetGamepad(second);

        _assignedGamepads.Add(first);
        _assignedGamepads.Add(second);
    }

    private void AssignNewGamepadIfInputDetected()
    {
        foreach (var gamepad in Gamepad.all)
        {
            if (gamepad == null) continue;
            if (_assignedGamepads.Contains(gamepad)) continue;
            if (!IsGamepadInputDetected(gamepad)) continue;

            AssignNewGamepadByPriority(gamepad);
            break;
        }
    }

    private void AssignNewGamepadByPriority(Gamepad gamepad)
    {
        if (gamepad == null) return;

        if (!_player2Input.IsUsingGamepad)
        {
            _player2Input.SetGamepad(gamepad);
            _assignedGamepads.Add(gamepad);
            return;
        }

        if (!_player1Input.IsUsingGamepad)
        {
            _player1Input.SetGamepad(gamepad);
            _assignedGamepads.Add(gamepad);
            return;
        }
    }

    private bool IsGamepadInputDetected(Gamepad gamepad)
    {
        if (gamepad == null) return false;

        if (gamepad.buttonSouth.wasPressedThisFrame) return true;
        if (gamepad.buttonEast.wasPressedThisFrame) return true;
        if (gamepad.buttonWest.wasPressedThisFrame) return true;
        if (gamepad.buttonNorth.wasPressedThisFrame) return true;

        if (gamepad.startButton.wasPressedThisFrame) return true;
        if (gamepad.selectButton.wasPressedThisFrame) return true;

        if (gamepad.leftShoulder.wasPressedThisFrame) return true;
        if (gamepad.rightShoulder.wasPressedThisFrame) return true;

        if (gamepad.leftTrigger.ReadValue() > 0.5f) return true;
        if (gamepad.rightTrigger.ReadValue() > 0.5f) return true;

        if (gamepad.leftStick.ReadValue().sqrMagnitude > 0.25f) return true;
        if (gamepad.rightStick.ReadValue().sqrMagnitude > 0.25f) return true;

        if (gamepad.dpad.ReadValue().sqrMagnitude > 0.25f) return true;

        return false;
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad gamepad) return;

        switch (change)
        {
            case InputDeviceChange.Disconnected:
            case InputDeviceChange.Removed:
                HandleGamepadDisconnected(gamepad);
                break;
        }
    }

    private void HandleGamepadDisconnected(Gamepad disconnectedGamepad)
    {
        if (disconnectedGamepad == null) return;

        if (_player1Input.AssignedGamepad == disconnectedGamepad)
        {
            _player1Input.SetKeyboardP1();
            _assignedGamepads.Remove(disconnectedGamepad);
        }

        if (_player2Input.AssignedGamepad == disconnectedGamepad)
        {
            _player2Input.SetKeyboardP2();
            _assignedGamepads.Remove(disconnectedGamepad);
        }
    }

    public Vector2 GetMoveInput(int playerNumber)
    {
        EnsureInit();
        return GetSlot(playerNumber).ReadMove();
    }

    public bool WasJumpPressedThisFrame(int playerNumber)
    {
        EnsureInit();
        return GetSlot(playerNumber).WasJumpPressedThisFrame();
    }

    public bool WasPolarityPressedThisFrame(int playerNumber)
    {
        EnsureInit();
        return GetSlot(playerNumber).WasPolarityPressedThisFrame();
    }

    private PlayerInputSlot GetSlot(int playerNumber)
    {
        return playerNumber == 1 ? _player1Input : _player2Input;
    }

    private void UpdateDebugText()
    {
        player1Source = GetSourceText(_player1Input);
        player2Source = GetSourceText(_player2Input);
    }

    private string GetSourceText(PlayerInputSlot slot)
    {
        if (slot == null) return "None";

        if (slot.IsUsingGamepad)
        {
            return $"Gamepad: {slot.AssignedGamepad.displayName}";
        }

        return slot.Source.ToString();
    }

    private string LoadBindingOverridesJson()
    {
        return PlayerPrefs.GetString(BindingOverridesKey, "");
    }

    public void SaveBindingOverrides()
    {
        if (_player1Input == null) return;

        string json = _player1Input.GetBindingOverridesJson();

        PlayerPrefs.SetString(BindingOverridesKey, json);
        PlayerPrefs.Save();
    }

    public void ApplyBindingOverridesToAll(string bindingOverridesJson)
    {
        if (string.IsNullOrEmpty(bindingOverridesJson)) return;

        _player1Input?.ApplyBindingOverrides(bindingOverridesJson);
        _player2Input?.ApplyBindingOverrides(bindingOverridesJson);
    }

    public void ResetBindingOverridesToDefault()
    {
        EnsureInit();
        _player1Input?.ResetBindingOverrides();
        _player2Input?.ResetBindingOverrides();

        PlayerPrefs.DeleteKey(BindingOverridesKey);
        PlayerPrefs.Save();
    }

    public string GetKeyboardBindingDisplayName(int playerNumber, string uiAction)
    {
        EnsureInit();
        string group = playerNumber == 1 ? "KeyboardP1" : "KeyboardP2";

        return _player1Input.GetBindingDisplayName(uiAction, group);
    }

    /// <summary>パッド側の割り当て表示。パッドの操作割り当ては1P/2Pで共通</summary>
    public string GetGamepadBindingDisplayName(string uiAction)
    {
        EnsureInit();
        return _player1Input.GetBindingDisplayName(uiAction, "Gamepad");
    }

    /// <summary>パッドの割り当て直し。キーボードと同じく、決まったら両方の枠に反映して保存する</summary>
    public void StartGamepadRebind(string uiAction, Action onComplete, Action onCancel)
    {
        EnsureInit();
        _player1Input.StartInteractiveRebind(
            uiAction,
            "Gamepad",
            () =>
            {
                string json = _player1Input.GetBindingOverridesJson();

                _player2Input.ApplyBindingOverrides(json);

                SaveBindingOverrides();

                onComplete?.Invoke();
            },
            () =>
            {
                onCancel?.Invoke();
            }
        );
    }

    public void StartKeyboardRebind(int playerNumber, string uiAction, Action onComplete, Action onCancel
    )
    {
        EnsureInit();

        string group = playerNumber == 1 ? "KeyboardP1" : "KeyboardP2";

        _player1Input.StartInteractiveRebind(
            uiAction,
            group,
            () =>
            {
                string json = _player1Input.GetBindingOverridesJson();

                _player2Input.ApplyBindingOverrides(json);

                SaveBindingOverrides();

                onComplete?.Invoke();
            },
            () =>
            {
                onCancel?.Invoke();
            }
        );
    }
}