using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputSlot : IDisposable
{
    public enum SlotSource
    {
        KeyboardP1,
        KeyboardP2,
        Gamepad
    }

    private readonly PlayerControls _controls;

    private SlotSource _source;
    private Gamepad _assignedGamepad;

    public SlotSource Source => _source;
    public Gamepad AssignedGamepad => _assignedGamepad;

    public bool IsUsingGamepad =>
        _source == SlotSource.Gamepad && _assignedGamepad != null;

    public PlayerInputSlot(string bindingOverridesJson = "")
    {
        _controls = new PlayerControls();

        ApplyBindingOverrides(bindingOverridesJson);

        _controls.Gameplay.Enable();
    }

    public void SetKeyboardP1()
    {
        _source = SlotSource.KeyboardP1;
        _assignedGamepad = null;

        _controls.asset.bindingMask = InputBinding.MaskByGroup("KeyboardP1");

        if (Keyboard.current != null)
        {
            _controls.asset.devices = new InputDevice[] { Keyboard.current };
        }
        else
        {
            _controls.asset.devices = null;
        }
    }

    public void SetKeyboardP2()
    {
        _source = SlotSource.KeyboardP2;
        _assignedGamepad = null;

        _controls.asset.bindingMask = InputBinding.MaskByGroup("KeyboardP2");

        if (Keyboard.current != null)
        {
            _controls.asset.devices = new InputDevice[] { Keyboard.current };
        }
        else
        {
            _controls.asset.devices = null;
        }
    }

    public void SetGamepad(Gamepad gamepad)
    {
        if (gamepad == null) return;

        _source = SlotSource.Gamepad;
        _assignedGamepad = gamepad;

        _controls.asset.bindingMask = InputBinding.MaskByGroup("Gamepad");
        _controls.asset.devices = new InputDevice[] { gamepad };
    }

    public Vector2 ReadMove()
    {
        return _controls.Gameplay.Move.ReadValue<Vector2>();
    }

    public bool WasJumpPressedThisFrame()
    {
        return _controls.Gameplay.Jump.WasPressedThisFrame();
    }

    public bool WasPolarityPressedThisFrame()
    {
        return _controls.Gameplay.Polarity.WasPressedThisFrame();
    }

    public void ApplyBindingOverrides(string bindingOverridesJson)
    {
        if (string.IsNullOrEmpty(bindingOverridesJson)) return;

        _controls.asset.LoadBindingOverridesFromJson(bindingOverridesJson);
    }

    public string GetBindingOverridesJson()
    {
        return _controls.asset.SaveBindingOverridesAsJson();
    }

    public void ResetBindingOverrides()
    {
        foreach (InputAction action in _controls.asset)
        {
            action.RemoveAllBindingOverrides();
        }
    }

    public string GetBindingDisplayName(string uiAction, string bindingGroup)
    {
        InputAction action = GetInputActionFromUiAction(uiAction);

        if (action == null)
        {
            return "---";
        }

        int bindingIndex = FindBindingIndex(uiAction, bindingGroup);

        if (bindingIndex < 0)
        {
            return "---";
        }

        return action.GetBindingDisplayString(bindingIndex);
    }

    public void StartInteractiveRebind(string uiAction, string bindingGroup, Action onComplete, Action onCancel)
    {
        InputAction action = GetInputActionFromUiAction(uiAction);

        if (action == null)
        {
            onCancel?.Invoke();
            return;
        }

        int bindingIndex = FindBindingIndex(uiAction, bindingGroup);

        if (bindingIndex < 0)
        {
            onCancel?.Invoke();
            return;
        }

        action.Disable();

        bool gamepad = string.Equals(bindingGroup, "Gamepad", StringComparison.OrdinalIgnoreCase);

        var op = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>");

        if (gamepad)
        {
            // パッドの欄。キーボードは拾わず、ESC でやめられるようにする
            op = op.WithControlsExcluding("<Keyboard>")
                   .WithControlsExcluding("<Gamepad>/leftStick")
                   .WithControlsExcluding("<Gamepad>/rightStick")
                   .WithControlsExcluding("<Gamepad>/dpad")
                   .WithCancelingThrough("<Keyboard>/escape");
        }
        else
        {
            // キーボードの欄。パッドは拾わない
            op = op.WithControlsExcluding("<Gamepad>")
                   .WithCancelingThrough("<Keyboard>/escape");
        }

        op
            .OnCancel(operation =>
            {
                action.Enable();
                operation.Dispose();
                onCancel?.Invoke();
            })
            .OnComplete(operation =>
            {
                action.Enable();
                operation.Dispose();
                onComplete?.Invoke();
            })
            .Start();
    }

    private InputAction GetInputActionFromUiAction(string uiAction)
    {
        switch (uiAction)
        {
            case "Left":
            case "Right":
            case "Up":
            case "Down":
                return _controls.Gameplay.Move;

            case "Jump":
                return _controls.Gameplay.Jump;

            case "Polarity":
                return _controls.Gameplay.Polarity;

            default:
                return null;
        }
    }

    private int FindBindingIndex(string uiAction, string bindingGroup)
    {
        InputAction action = GetInputActionFromUiAction(uiAction);

        if (action == null)
        {
            return -1;
        }

        string movePartName = GetMovePartName(uiAction);

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if (!BindingHasGroup(binding, bindingGroup))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(movePartName))
            {
                if (!binding.isPartOfComposite)
                {
                    continue;
                }

                if (string.Equals(binding.name, movePartName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            else
            {
                if (binding.isComposite || binding.isPartOfComposite)
                {
                    continue;
                }

                return i;
            }
        }

        return -1;
    }

    private string GetMovePartName(string uiAction)
    {
        switch (uiAction)
        {
            case "Left":
                return "left";

            case "Right":
                return "right";

            case "Up":
                return "up";

            case "Down":
                return "down";

            default:
                return "";
        }
    }

    private bool BindingHasGroup(InputBinding binding, string bindingGroup)
    {
        if (string.IsNullOrEmpty(binding.groups))
        {
            return false;
        }

        string[] groups = binding.groups.Split(';');

        foreach (string group in groups)
        {
            if (string.Equals(group, bindingGroup, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        _controls.Gameplay.Disable();
        _controls.Dispose();
    }
}