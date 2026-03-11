using Godot;

namespace STS2ShowPlayerHandCards.Utils
{
    /// <summary>
    ///     Handles keyboard input for the mod.
    ///     Registers input action to Godot's InputMap and listens for toggle key.
    /// </summary>
    public partial class InputHandler : Node
    {
        /// <summary>
        ///     The input action name registered in Godot's InputMap.
        /// </summary>
        public const string ToggleHandDisplayAction = "mod_toggle_hand_display";

        /// <summary>
        ///     Default key for toggling hand card visibility.
        /// </summary>
        public const Key DefaultToggleKey = Key.Shift;

        private static InputHandler? _instance;
        private static Key _currentKey = DefaultToggleKey;

        /// <summary>
        ///     Gets or sets the current toggle key.
        /// </summary>
        public static Key CurrentKey
        {
            get => _currentKey;
            set
            {
                if (_currentKey == value) return;
                _currentKey = value;
                UpdateInputMapKey();
                Main.Logger.Info($"Toggle key changed to: {value}");
            }
        }

        public static void EnsureExists()
        {
            if (_instance != null && IsInstanceValid(_instance)) return;

            RegisterInputAction();

            _instance = new() { Name = "ShowPlayerHandCards_InputHandler" };

            var tree = Engine.GetMainLoop() as SceneTree;
            tree?.Root.CallDeferred("add_child", _instance);
        }

        public static void Cleanup()
        {
            if (InputMap.HasAction(ToggleHandDisplayAction))
                InputMap.EraseAction(ToggleHandDisplayAction);

            if (_instance == null || !IsInstanceValid(_instance)) return;
            _instance.QueueFree();
            _instance = null;
        }

        /// <summary>
        ///     Registers the toggle action to Godot's InputMap.
        /// </summary>
        private static void RegisterInputAction()
        {
            if (InputMap.HasAction(ToggleHandDisplayAction))
                InputMap.EraseAction(ToggleHandDisplayAction);

            InputMap.AddAction(ToggleHandDisplayAction);
            AddKeyToAction(_currentKey);

            Main.Logger.Info($"Registered input action '{ToggleHandDisplayAction}' with key: {_currentKey}");
        }

        /// <summary>
        ///     Updates the InputMap when the key changes.
        /// </summary>
        private static void UpdateInputMapKey()
        {
            if (!InputMap.HasAction(ToggleHandDisplayAction))
            {
                RegisterInputAction();
                return;
            }

            InputMap.ActionEraseEvents(ToggleHandDisplayAction);
            AddKeyToAction(_currentKey);
        }

        private static void AddKeyToAction(Key key)
        {
            var keyEvent = new InputEventKey
            {
                Keycode = key,
                Pressed = true,
            };
            InputMap.ActionAddEvent(ToggleHandDisplayAction, keyEvent);
        }

        public override void _UnhandledKeyInput(InputEvent @event)
        {
            if (!@event.IsActionPressed(ToggleHandDisplayAction)) return;
            HandCardDisplayService.ToggleVisibility();
            Main.Logger.Info(
                $"Hand card display toggled: {(HandCardDisplayService.IsHidden ? "Hidden" : "Visible")}");
        }
    }
}
