using LoneEftDmaRadar.UI.Misc;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Threading;
using VmmSharpEx.Extensions.Input;
using HotkeyInputMode = LoneEftDmaRadar.HotkeyInputMode;

namespace LoneEftDmaRadar.UI.Hotkeys
{
    public sealed class HotkeyManagerViewModel : INotifyPropertyChanged
    {
        private static readonly ConcurrentDictionary<Win32VirtualKey, HotkeyAction> _hotkeys = new();
        internal static IReadOnlyDictionary<Win32VirtualKey, HotkeyAction> Hotkeys => _hotkeys;
        private static readonly ConcurrentBag<HotkeyManagerViewModel> _instances = new();
        private static int _suspendCount;

        static HotkeyManagerViewModel()
        {
            LoadConfigHotkeys();
        }

        public ObservableCollection<HotkeyBindingEntry> Bindings { get; }
        public HotkeyBindingEntry ListeningEntry { get; private set; }

        /// <summary>
        /// Current hotkey input mode.
        /// </summary>
        public HotkeyInputMode InputMode
        {
            get => App.Config.HotkeyInputMode;
            private set
            {
                if (App.Config.HotkeyInputMode != value)
                {
                    App.Config.HotkeyInputMode = value;
                    App.Config.Save();
                    OnPropertyChanged(nameof(InputMode));
                    OnPropertyChanged(nameof(InputModeText));
                }
            }
        }

        /// <summary>
        /// Display text for current input mode.
        /// </summary>
        public string InputModeText => InputMode == HotkeyInputMode.RadarPC ? "Radar PC" : "Game PC";

        public HotkeyManagerViewModel()
        {
            Bindings = new ObservableCollection<HotkeyBindingEntry>();
            EnsureControllersRegistered();
            LoadConfigHotkeys();
            RefreshBindings();
            _instances.Add(this);
        }

        private bool _skipNextMouseCapture;

        public void BeginListening(HotkeyBindingEntry entry, bool skipNextMouseCapture = false)
        {
            if (entry is null)
                return;

            CancelListening();
            ListeningEntry = entry;
            ListeningEntry.IsListening = true;
            _skipNextMouseCapture = skipNextMouseCapture;
        }

        public void AssignKey(Key key)
        {
            try
            {
                if (ListeningEntry is null)
                    return;

                var vkCode = KeyInterop.VirtualKeyFromKey(key);
                AssignVirtualKey(vkCode);
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[HotkeyManager] Failed to assign key: {ex}");
            }
            finally
            {
                CancelListening();
            }
        }

        public void AssignVirtualKey(int vkCode)
        {
            if (ListeningEntry is null)
                return;

            if (_skipNextMouseCapture)
            {
                _skipNextMouseCapture = false;
                return;
            }

            var vk = (Win32VirtualKey)vkCode;

            // Check if user is trying to assign mouse buttons (left/right click)
            // These can cause issues as they're commonly used for game actions
            if (vk == Win32VirtualKey.LBUTTON || vk == Win32VirtualKey.RBUTTON)
            {
                var buttonName = vk == Win32VirtualKey.LBUTTON ? "Left Click" : "Right Click";
                var result = MessageBox.Show(
                    $"You are about to set {buttonName} as a Hotkey.\n\n" +
                    $"This may cause issues because {buttonName} is commonly used for game actions.\n\n" +
                    $"Do you want to continue?",
                    "Warning: Hotkey May Cause Issues",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No
                );

                if (result == MessageBoxResult.No)
                {
                    CancelListening();
                    return;
                }
            }

            RemoveBindingForAction(ListeningEntry.ActionName);

            if (_hotkeys.TryRemove(vk, out _))
            {
                App.Config.Hotkeys.TryRemove(vk, out _);
            }

            var action = new HotkeyAction(ListeningEntry.ActionName);
            _hotkeys[vk] = action;
            App.Config.Hotkeys[vk] = action.Name;

            ListeningEntry.Key = vk;
            RefreshBindings();

            // Stop listening after successfully assigning the key
            CancelListening();
        }

        public void ClearListening()
        {
            if (ListeningEntry is null)
                return;

            CancelListening();
        }

        public void ClearBinding(HotkeyBindingEntry entry)
        {
            if (entry is null)
                return;

            RemoveBindingForAction(entry.ActionName);
            entry.Key = null;

            if (ListeningEntry == entry)
            {
                CancelListening();
            }

            RefreshBindings();
        }

        public void CancelListening()
        {
            if (ListeningEntry is not null)
            {
                ListeningEntry.IsListening = false;
                ListeningEntry = null;
                _skipNextMouseCapture = false;
            }
        }

        private void RemoveBindingForAction(string actionName)
        {
            var existing = _hotkeys.FirstOrDefault(kvp => kvp.Value.Name.Equals(actionName, StringComparison.Ordinal));
            if (!existing.Equals(default(KeyValuePair<Win32VirtualKey, HotkeyAction>)))
            {
                _hotkeys.TryRemove(existing.Key, out _);
                App.Config.Hotkeys.TryRemove(existing.Key, out _);
            }

            var entry = Bindings.FirstOrDefault(b => b.ActionName.Equals(actionName, StringComparison.Ordinal));
            if (entry is not null)
            {
                entry.Key = null;
            }
        }

        private void RefreshBindings()
        {
            Bindings.Clear();
            EnsureControllersRegistered();

            foreach (var controller in HotkeyAction.Controllers.OrderBy(c => c.Name))
            {
                Win32VirtualKey? key = null;
                var existing = _hotkeys.FirstOrDefault(kvp => kvp.Value.Name.Equals(controller.Name, StringComparison.Ordinal));
                if (!existing.Equals(default(KeyValuePair<Win32VirtualKey, HotkeyAction>)))
                {
                    key = existing.Key;
                }
                Bindings.Add(new HotkeyBindingEntry(controller, key));
            }

            OnPropertyChanged(nameof(Bindings));
        }

        private static void EnsureControllersRegistered()
        {
            if (HotkeyAction.Controllers.Any())
                return;

            // try to use existing MainWindow instance on UI thread only
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
                return;

            if (dispatcher.CheckAccess())
            {
                MainWindowViewModel.EnsureHotkeysRegisteredStatic();
            }
            else
            {
                dispatcher.BeginInvoke(new Action(MainWindowViewModel.EnsureHotkeysRegisteredStatic));
                return;
            }

            // If still empty, and MainWindow exists, force registration now
            if (!HotkeyAction.Controllers.Any() && Application.Current?.MainWindow is MainWindow mw)
            {
                mw.ViewModel?.EnsureHotkeysRegistered();
            }

            if (HotkeyAction.Controllers.Any())
            {
                NotifyControllersRegistered();
            }
        }

        private static void LoadConfigHotkeys()
        {
            if (_hotkeys.Count > 0)
                return;

            foreach (var kvp in App.Config.Hotkeys)
            {
                var action = new HotkeyAction(kvp.Value);
                _hotkeys.TryAdd(kvp.Key, action);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        internal static void NotifyControllersRegistered()
        {
            foreach (var vm in _instances)
            {
                vm.RefreshBindings();
            }
        }

        public static void SuspendHotkeys() => Interlocked.Increment(ref _suspendCount);

        public static void ResumeHotkeys()
        {
            while (true)
            {
                int current = Volatile.Read(ref _suspendCount);
                if (current <= 0) return;
                if (Interlocked.CompareExchange(ref _suspendCount, current - 1, current) == current)
                    return;
            }
        }

        internal static bool HotkeysSuspended => Volatile.Read(ref _suspendCount) > 0;

        /// <summary>
        /// Toggles between Radar PC and Game PC input modes.
        /// </summary>
        public void ToggleInputMode()
        {
            InputMode = InputMode == HotkeyInputMode.RadarPC 
                ? HotkeyInputMode.GamePC 
                : HotkeyInputMode.RadarPC;
        }
    }
}
