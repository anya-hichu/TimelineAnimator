using Dalamud.Game.ClientState.Keys;
using System.Runtime.InteropServices;

namespace TimelineAnimator;

public class InputManager
{
    private readonly Configuration configuration;

    private bool wasPlaybackKeyPressed = false;
    private bool wasAddItemKeyPressed = false;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public InputManager(Configuration configuration)
    {
        this.configuration = configuration;
    }

    private bool IsKeyPressed(VirtualKey key)
    {
        if (key == VirtualKey.NO_KEY) return false;
        if (Services.KeyState[key]) return true;
        const int KEY_PRESSED = 0x8000;
        return (GetAsyncKeyState((int)key) & KEY_PRESSED) != 0;
    }

    public bool IsModifierHeld
    {
        get
        {
            if (configuration.ModifierKey == VirtualKey.NO_KEY) return true;
            return IsKeyPressed(configuration.ModifierKey);
        }
    }

    public bool shouldBlockGameInput
    {
        get
        {
            if (configuration.ModifierKey == VirtualKey.NO_KEY) return false;
            return IsKeyPressed(configuration.ModifierKey);
        }
    }

    public bool IsTogglePlaybackPressed()
    {
        if (configuration.TogglePlaybackKey == VirtualKey.NO_KEY) return false;
        if (configuration.ModifierKey != VirtualKey.NO_KEY && !IsModifierHeld)
        {
            wasPlaybackKeyPressed = false;
            return false;
        }

        bool isPressed = IsKeyPressed(configuration.TogglePlaybackKey);
        bool result = isPressed && !wasPlaybackKeyPressed;
        wasPlaybackKeyPressed = isPressed;
        return result;
    }

    public bool IsAddItemPressed()
    {
        if (configuration.AddItemKey == VirtualKey.NO_KEY) return false;

        if (configuration.ModifierKey != VirtualKey.NO_KEY && !IsModifierHeld)
        {
            wasAddItemKeyPressed = false;
            return false;
        }

        bool isPressed = IsKeyPressed(configuration.AddItemKey);
        bool result = isPressed && !wasAddItemKeyPressed;
        wasAddItemKeyPressed = isPressed;
        return result;
    }
}