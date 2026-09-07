using Gma.System.MouseKeyHook;
using System.Windows.Forms;

namespace InputLogic
{
    internal class InputBindingManager
    {
        private IKeyboardMouseEvents? _mEvents;
        private readonly Dictionary<string, string> bindings = [];
        private static readonly Dictionary<string, bool> isHolding = [];
        private string? settingBindingId = null;

        public event Action<string, string>? OnBindingSet;
        public event Action<string>? OnBindingPressed;
        public event Action<string>? OnBindingReleased;
        public event Action<Keys>? OnAnyKeyDown;

        private static readonly HashSet<string> currentlyPressedKeys = [];

        public static bool IsHoldingBinding(string bindingId) => isHolding.TryGetValue(bindingId, out bool holding) && holding;

        public void SetupDefault(string bindingId, string keyCode)
        {
            bindings[bindingId] = keyCode;
            isHolding[bindingId] = false;
            OnBindingSet?.Invoke(bindingId, keyCode);
            EnsureHookEvents();
        }

        public string GetBinding(string bindingId) => bindings.GetValueOrDefault(bindingId, "None");

        public void StartListeningForBinding(string bindingId)
        {
            settingBindingId = bindingId;
            EnsureHookEvents();
        }

        private void EnsureHookEvents()
        {
            if (_mEvents == null)
            {
                _mEvents = Hook.GlobalEvents();
                _mEvents.KeyDown += GlobalHookKeyDown!;
                _mEvents.MouseDown += GlobalHookMouseDown!;
                _mEvents.KeyUp += GlobalHookKeyUp!;
                _mEvents.MouseUp += GlobalHookMouseUp!;
            }
        }

        private bool IsModifier(Keys key)
        {
            return key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey ||
                   key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey ||
                   key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu;
        }

        private void GlobalHookKeyDown(object sender, KeyEventArgs e)
        {
            currentlyPressedKeys.Add(e.KeyCode.ToString());
            OnAnyKeyDown?.Invoke(e.KeyCode);

            if (settingBindingId != null)
            {
                string combo = "";
                string keyPart = e.KeyCode.ToString();
                
                // If it's a modifier key, we don't want to prefix it with itself (e.g. no "Shift+LShiftKey")
                bool isControl = (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.RControlKey);
                bool isAlt = (e.KeyCode == Keys.Menu || e.KeyCode == Keys.LMenu || e.KeyCode == Keys.RMenu);
                bool isShift = (e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.LShiftKey || e.KeyCode == Keys.RShiftKey);

                if (e.Control && !isControl) combo += "Control+";
                if (e.Alt && !isAlt) combo += "Alt+";
                if (e.Shift && !isShift) combo += "Shift+";
                
                combo += keyPart;

                bindings[settingBindingId] = combo;
                OnBindingSet?.Invoke(settingBindingId, combo);
                settingBindingId = null;
            }
            
            CheckAndTriggerBindings();
        }

        private void GlobalHookMouseDown(object sender, MouseEventArgs e)
        {
            currentlyPressedKeys.Add(e.Button.ToString());

            if (settingBindingId != null)
            {
                string combo = "";
                if ((Control.ModifierKeys & Keys.Control) == Keys.Control) combo += "Control+";
                if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt) combo += "Alt+";
                if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift) combo += "Shift+";
                combo += e.Button.ToString();

                bindings[settingBindingId] = combo;
                OnBindingSet?.Invoke(settingBindingId, combo);
                settingBindingId = null;
            }

            CheckAndTriggerBindings();
        }

        private void GlobalHookKeyUp(object sender, KeyEventArgs e)
        {
            currentlyPressedKeys.Remove(e.KeyCode.ToString());
            CheckAndTriggerBindings();
        }

        private void GlobalHookMouseUp(object sender, MouseEventArgs e)
        {
            currentlyPressedKeys.Remove(e.Button.ToString());
            CheckAndTriggerBindings();
        }

        private void CheckAndTriggerBindings()
        {
            foreach (var binding in bindings)
            {
                bool pressed = IsComboHeld(binding.Value);
                bool wasHeld = isHolding.GetValueOrDefault(binding.Key, false);

                if (pressed && !wasHeld)
                {
                    isHolding[binding.Key] = true;
                    OnBindingPressed?.Invoke(binding.Key);
                }
                else if (!pressed && wasHeld)
                {
                    isHolding[binding.Key] = false;
                    OnBindingReleased?.Invoke(binding.Key);
                }
            }
        }

        private bool IsComboHeld(string combo)
        {
            if (string.IsNullOrEmpty(combo) || combo == "None") return false;
            var parts = combo.Split('+');
            foreach (var part in parts)
            {
                if (part == "Control") { if ((Control.ModifierKeys & Keys.Control) != Keys.Control) return false; }
                else if (part == "Alt") { if ((Control.ModifierKeys & Keys.Alt) != Keys.Alt) return false; }
                else if (part == "Shift") { if ((Control.ModifierKeys & Keys.Shift) != Keys.Shift) return false; }
                else if (!currentlyPressedKeys.Contains(part)) return false;
            }
            return true;
        }

        public void StopListening()
        {
            if (_mEvents != null)
            {
                _mEvents.KeyDown -= GlobalHookKeyDown!;
                _mEvents.MouseDown -= GlobalHookMouseDown!;
                _mEvents.KeyUp -= GlobalHookKeyUp!;
                _mEvents.MouseUp -= GlobalHookMouseUp!;
                _mEvents.Dispose();
                _mEvents = null;
            }
        }
    }
}