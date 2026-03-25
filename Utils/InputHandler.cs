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

        private static bool IsModifierKey(Key key)
        {
            return TryGetModifierKind(key, out _);
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
                        case "leftctrl":
                        case "rightctrl":
                        case "lctrl":
                        case "rctrl":
                            ctrl = true;
                            break;
                        case "alt":
                        case "leftalt":
                        case "rightalt":
                        case "lalt":
                        case "ralt":
                            alt = true;
                            break;
                        case "shift":
                        case "leftshift":
                        case "rightshift":
                        case "lshift":
                        case "rshift":
                            if (parts.Length == 1)
                                key = Key.Shift;
                            shift = true;
                            break;
                        case "meta":
                        case "cmd":
                        case "command":
                        case "leftmeta":
                        case "rightmeta":
                        case "lmeta":
                        case "rmeta":
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
                var modifiersMatch = keyEvent.CtrlPressed == Ctrl
                                     && keyEvent.AltPressed == Alt
                                     && keyEvent.ShiftPressed == Shift
                                     && keyEvent.MetaPressed == Meta;
                if (!modifiersMatch)
                    return false;

                if (!TryMatchesPrimaryKey(keyEvent.Keycode))
                    return false;

                if (!HasOnlyModifierKeys())
                    return true;

                return InputHandler.IsModifierKey(keyEvent.Keycode) && IsIncludedModifier(keyEvent.Keycode);
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

            private bool HasOnlyModifierKeys()
            {
                return InputHandler.IsModifierKey(Keycode);
            }

            private bool IsIncludedModifier(Key key)
            {
                return GetModifierKind(key) switch
                {
                    ModifierKind.Ctrl => Ctrl,
                    ModifierKind.Alt => Alt,
                    ModifierKind.Shift => Shift,
                    ModifierKind.Meta => Meta,
                    _ => false,
                };
            }

            private bool TryMatchesPrimaryKey(Key key)
            {
                if (key == Keycode)
                    return true;

                if (!InputHandler.IsModifierKey(Keycode))
                    return false;

                if (InputHandler.IsSpecificModifierSide(Keycode))
                    return false;

                return GetModifierKind(key) == GetModifierKind(Keycode);
            }
        }

        private enum ModifierKind
        {
            None = 0,
            Ctrl = 1,
            Alt = 2,
            Shift = 3,
            Meta = 4,
        }

        private static bool TryGetModifierKind(Key key, out ModifierKind kind)
        {
            kind = GetModifierKind(key);
            return kind != ModifierKind.None;
        }

        private static ModifierKind GetModifierKind(Key key)
        {
            var name = key.ToString().ToLowerInvariant();
            if (name.Contains("ctrl") || name.Contains("control"))
                return ModifierKind.Ctrl;
            if (name.Contains("shift"))
                return ModifierKind.Shift;
            if (name.Contains("alt"))
                return ModifierKind.Alt;
            if (name.Contains("meta") || name.Contains("cmd") || name.Contains("command"))
                return ModifierKind.Meta;
            return ModifierKind.None;
        }

        private static bool IsSpecificModifierSide(Key key)
        {
            var name = key.ToString().ToLowerInvariant();
            return (name.Contains("left") || name.StartsWith("l")) && IsModifierKeyName(name)
                   || (name.Contains("right") || name.StartsWith("r")) && IsModifierKeyName(name);
        }

        private static bool IsModifierKeyName(string keyName)
        {
            return keyName.Contains("ctrl")
                   || keyName.Contains("control")
                   || keyName.Contains("shift")
                   || keyName.Contains("alt")
                   || keyName.Contains("meta")
                   || keyName.Contains("cmd")
                   || keyName.Contains("command");
        }
    }
}
