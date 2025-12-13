using System.ComponentModel;
using VmmSharpEx.Extensions.Input;

namespace LoneEftDmaRadar.UI.Hotkeys
{
    /// <summary>
    /// Represents a single hotkey binding row (action + assigned key).
    /// </summary>
    public sealed class HotkeyBindingEntry : INotifyPropertyChanged
    {
        public string ActionName { get; }
        public HotkeyAction Action { get; }

        private Win32VirtualKey? _key;
        private bool _isListening;

        public Win32VirtualKey? Key
        {
            get => _key;
            set
            {
                if (_key == value) return;
                _key = value;
                OnPropertyChanged(nameof(Key));
                OnPropertyChanged(nameof(DisplayKey));
            }
        }

        public bool IsListening
        {
            get => _isListening;
            set
            {
                if (_isListening == value) return;
                _isListening = value;
                OnPropertyChanged(nameof(IsListening));
                OnPropertyChanged(nameof(DisplayKey));
            }
        }

        public string DisplayKey
        {
            get
            {
                if (IsListening) return "Press a key...";
                return Key?.ToString() ?? "Unbound";
            }
        }

        public HotkeyBindingEntry(HotkeyActionController controller, Win32VirtualKey? key)
        {
            ActionName = controller?.Name ?? "Unknown";
            Action = new HotkeyAction(ActionName);
            _key = key;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public override string ToString() => $"{ActionName} - {DisplayKey}";
    }
}
