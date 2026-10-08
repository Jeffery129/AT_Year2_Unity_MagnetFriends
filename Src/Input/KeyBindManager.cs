using UnityEngine;
using System;

public class KeyBindManager : MonoBehaviour
{
    public static KeyBindManager Instance { get; private set; }
    
    // Static action names for UI
    public static readonly string[] ActionNames = { "Left", "Right", "Up", "Down", "Jump", "Polarity" };
    public static readonly string[] ActionDisplayNames = { "左移動", "右移動", "上移動", "下移動", "ジャンプ", "極性切替" };
    
    // Player 1 Keys
    public KeyCode p1Left = KeyCode.A;
    public KeyCode p1Right = KeyCode.D;
    public KeyCode p1Up = KeyCode.W;
    public KeyCode p1Down = KeyCode.S;
    public KeyCode p1Jump = KeyCode.Space;
    public KeyCode p1Polarity = KeyCode.F;
    
    // Player 2 Keys
    public KeyCode p2Left = KeyCode.LeftArrow;
    public KeyCode p2Right = KeyCode.RightArrow;
    public KeyCode p2Up = KeyCode.UpArrow;
    public KeyCode p2Down = KeyCode.DownArrow;
    public KeyCode p2Jump = KeyCode.RightControl;
    public KeyCode p2Polarity = KeyCode.RightShift;
    
    // Events
    public event Action OnKeyBindingsChanged;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadKeyBindings();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void SetKeyBinding(int playerNum, string action, KeyCode key)
    {
        if (playerNum == 1)
        {
            switch (action)
            {
                case "Left": p1Left = key; break;
                case "Right": p1Right = key; break;
                case "Up": p1Up = key; break;
                case "Down": p1Down = key; break;
                case "Jump": p1Jump = key; break;
                case "Polarity": p1Polarity = key; break;
            }
        }
        else
        {
            switch (action)
            {
                case "Left": p2Left = key; break;
                case "Right": p2Right = key; break;
                case "Up": p2Up = key; break;
                case "Down": p2Down = key; break;
                case "Jump": p2Jump = key; break;
                case "Polarity": p2Polarity = key; break;
            }
        }
        
        SaveKeyBindings();
        OnKeyBindingsChanged?.Invoke();
    }
    
    public KeyCode GetKey(int playerNum, string action)
    {
        if (playerNum == 1)
        {
            switch (action)
            {
                case "Left": return p1Left;
                case "Right": return p1Right;
                case "Up": return p1Up;
                case "Down": return p1Down;
                case "Jump": return p1Jump;
                case "Polarity": return p1Polarity;
            }
        }
        else
        {
            switch (action)
            {
                case "Left": return p2Left;
                case "Right": return p2Right;
                case "Up": return p2Up;
                case "Down": return p2Down;
                case "Jump": return p2Jump;
                case "Polarity": return p2Polarity;
            }
        }
        return KeyCode.None;
    }
    
    public void SaveKeyBindings()
    {
        PlayerPrefs.SetInt("P1_Left", (int)p1Left);
        PlayerPrefs.SetInt("P1_Right", (int)p1Right);
        PlayerPrefs.SetInt("P1_Up", (int)p1Up);
        PlayerPrefs.SetInt("P1_Down", (int)p1Down);
        PlayerPrefs.SetInt("P1_Jump", (int)p1Jump);
        PlayerPrefs.SetInt("P1_Polarity", (int)p1Polarity);
        
        PlayerPrefs.SetInt("P2_Left", (int)p2Left);
        PlayerPrefs.SetInt("P2_Right", (int)p2Right);
        PlayerPrefs.SetInt("P2_Up", (int)p2Up);
        PlayerPrefs.SetInt("P2_Down", (int)p2Down);
        PlayerPrefs.SetInt("P2_Jump", (int)p2Jump);
        PlayerPrefs.SetInt("P2_Polarity", (int)p2Polarity);
        
        PlayerPrefs.Save();
    }
    
    public void LoadKeyBindings()
    {
        p1Left = (KeyCode)PlayerPrefs.GetInt("P1_Left", (int)KeyCode.A);
        p1Right = (KeyCode)PlayerPrefs.GetInt("P1_Right", (int)KeyCode.D);
        p1Up = (KeyCode)PlayerPrefs.GetInt("P1_Up", (int)KeyCode.W);
        p1Down = (KeyCode)PlayerPrefs.GetInt("P1_Down", (int)KeyCode.S);
        p1Jump = (KeyCode)PlayerPrefs.GetInt("P1_Jump", (int)KeyCode.Space);
        p1Polarity = (KeyCode)PlayerPrefs.GetInt("P1_Polarity", (int)KeyCode.F);
        
        p2Left = (KeyCode)PlayerPrefs.GetInt("P2_Left", (int)KeyCode.LeftArrow);
        p2Right = (KeyCode)PlayerPrefs.GetInt("P2_Right", (int)KeyCode.RightArrow);
        p2Up = (KeyCode)PlayerPrefs.GetInt("P2_Up", (int)KeyCode.UpArrow);
        p2Down = (KeyCode)PlayerPrefs.GetInt("P2_Down", (int)KeyCode.DownArrow);
        p2Jump = (KeyCode)PlayerPrefs.GetInt("P2_Jump", (int)KeyCode.RightControl);
        p2Polarity = (KeyCode)PlayerPrefs.GetInt("P2_Polarity", (int)KeyCode.RightShift);
    }
    
    public void ResetToDefaults()
    {
        p1Left = KeyCode.A;
        p1Right = KeyCode.D;
        p1Up = KeyCode.W;
        p1Down = KeyCode.S;
        p1Jump = KeyCode.Space;
        p1Polarity = KeyCode.F;
        
        p2Left = KeyCode.LeftArrow;
        p2Right = KeyCode.RightArrow;
        p2Up = KeyCode.UpArrow;
        p2Down = KeyCode.DownArrow;
        p2Jump = KeyCode.RightControl;
        p2Polarity = KeyCode.RightShift;
        
        SaveKeyBindings();
        OnKeyBindingsChanged?.Invoke();
    }
    
    public string GetKeyName(KeyCode key)
    {
        return KeyCodeToString(key);
    }
    
    public static string KeyCodeToString(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.LeftArrow: return "←";
            case KeyCode.RightArrow: return "→";
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";
            case KeyCode.Space: return "Space";
            case KeyCode.LeftShift: return "L-Shift";
            case KeyCode.RightShift: return "R-Shift";
            case KeyCode.LeftControl: return "L-Ctrl";
            case KeyCode.RightControl: return "R-Ctrl";
            case KeyCode.LeftAlt: return "L-Alt";
            case KeyCode.RightAlt: return "R-Alt";
            case KeyCode.Return: return "Enter";
            case KeyCode.Backspace: return "Back";
            case KeyCode.Tab: return "Tab";
            default: return key.ToString();
        }
    }
    
    // Alias for SetKeyBinding
    public void SetKey(int playerNum, string action, KeyCode key)
    {
        SetKeyBinding(playerNum, action, key);
    }
}
