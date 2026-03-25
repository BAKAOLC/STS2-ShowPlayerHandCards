using Godot;

namespace STS2ShowPlayerHandCards.Utils
{
    /// <summary>
    ///     Handles keyboard input for the mod.
    ///     Supports captured key combinations.
    /// </summary>
    public partial class InputHandler : Node
    {
        public const string DefaultToggleBinding = "Shift";

        private static InputHandler? _instance;
        private static string _currentBinding = DefaultToggleBinding;
        private static KeyBinding _currentKeyBinding = KeyBinding.Parse(DefaultToggleBinding);

        public static string CurrentBinding
        {
            get => _currentBinding;
            set
            {
                if (!TryNormalizeBinding(value, out var normalized))
                    normalized = DefaultToggleBinding;
                if (string.Equals(_currentBinding, normalized, StringComparison.OrdinalIgnoreCase)) return;
                _currentBinding = normalized;
                _currentKeyBinding = KeyBinding.Parse(normalized);
                Main.Logger.Info($"Toggle key changed to: {normalized}");
            }
        }

        public static void EnsureExists()
        {
            if (_instance != null && IsInstanceValid(_instance)) return;

            _instance = new() { Name = "ShowPlayerHandCards_InputHandler" };

            var tree = Engine.GetMainLoop() as SceneTree;
            tree?.Root.CallDeferred("add_child", _instance);
        }

        public static void Cleanup()
        {
            if (_instance == null || !IsInstanceValid(_instance)) return;
            _instance.QueueFree();
            _instance = null;
        }

        public static bool TryNormalizeBinding(string? bindingText, out string normalized)
        {
            if (string.IsNullOrWhiteSpace(bindingText))
            {
                normalized = DefaultToggleBinding;
                return false;
            }

            try
            {
                normalized = KeyBinding.Parse(bindingText).ToString();
                return true;
            }
            catch
            {
                normalized = DefaultToggleBinding;
                return false;
            }
        }

        public override void _UnhandledKeyInput(InputEvent @event)
        {
            if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.IsEcho())
                return;

            if (!_currentKeyBinding.Matches(keyEvent)) return;
            HandCardDisplayService.ToggleVisibility();
            Main.Logger.Info(
                $"Hand card display toggled: {(HandCardDisplayService.IsHidden ? "Hidden" : "Visible")}");
        }

        private readonly record struct KeyBinding(Key Keycode, bool Ctrl, bool Alt, bool Shift, bool Meta)
        {
            public static KeyBinding Parse(string bindingText)
            {
                var parts = bindingText.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 0)
                    throw new FormatException("Key binding is empty.");

                var ctrl = false;
                var alt = false;
                var shift = false;
                var meta = false;
                Key? key = null;

                foreach (var part in parts)
                    switch (part.ToLowerInvariant())
                    {
                        case "ctrl":
                        case "control":
                            ctrl = true;
                            break;
                        case "alt":
                            alt = true;
                            break;
                        case "shift":
                            if (parts.Length == 1)
                                key = Key.Shift;
                            shift = true;
                            break;
                        case "meta":
                        case "cmd":
                        case "command":
                            if (parts.Length == 1)
                                key = Key.Meta;
                            meta = true;
                            break;
                        default:
                            if (!Enum.TryParse<Key>(part, true, out var parsedKey))
                                throw new FormatException($"Unknown key binding segment '{part}'.");
                            key = parsedKey;
                            break;
                    }

                if (key == null)
                {
                    key = ctrl ? Key.Ctrl : alt ? Key.Alt : shift ? Key.Shift : Key.Meta;
                }

                return new KeyBinding(key.Value, ctrl, alt, shift, meta);
            }

            public bool Matches(InputEventKey keyEvent)
            {
                return keyEvent.Keycode == Keycode
                       && keyEvent.CtrlPressed == Ctrl
                       && keyEvent.AltPressed == Alt
                       && keyEvent.ShiftPressed == Shift
                       && keyEvent.MetaPressed == Meta;
            }

            public override string ToString()
            {
                var parts = new List<string>();
                if (Ctrl) parts.Add("Ctrl");
                if (Alt) parts.Add("Alt");
                if (Shift && Keycode != Key.Shift) parts.Add("Shift");
                if (Meta && Keycode != Key.Meta) parts.Add("Meta");
                parts.Add(Keycode.ToString());
                return string.Join('+', parts.Distinct(StringComparer.OrdinalIgnoreCase));
            }
        }
    }
}
