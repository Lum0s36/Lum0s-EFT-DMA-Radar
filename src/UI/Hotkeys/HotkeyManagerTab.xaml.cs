using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LoneEftDmaRadar.UI.Hotkeys
{
    /// <summary>
    /// Hotkey Manager embedded tab (reuses the same view model as the popup).
    /// </summary>
    public partial class HotkeyManagerTab : UserControl
    {
        public HotkeyManagerTab()
        {
            InitializeComponent();
            // Always use a dedicated HotkeyManagerViewModel; don't inherit parent DataContext.
            DataContext = new HotkeyManagerViewModel();
            Loaded += (_, __) => HotkeyManagerViewModel.SuspendHotkeys();
            Unloaded += (_, __) => HotkeyManagerViewModel.ResumeHotkeys();
            AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown), true);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not HotkeyManagerViewModel vm || vm.ListeningEntry is null)
                return;

            var keyToUse = e.Key == Key.System ? e.SystemKey : e.Key;

            e.Handled = true;
            if (keyToUse == Key.Escape)
            {
                vm.ClearListening();
                return;
            }
            vm.AssignVirtualKey(KeyInterop.VirtualKeyFromKey(keyToUse));
        }

        private void OnBindingClick(object sender, MouseButtonEventArgs e)
        {
            // Walk up the visual tree to find if we clicked on a Button
            if (e.OriginalSource is DependencyObject dep && FindAncestor<Button>(dep) is not null)
                return; // Ignore clicks on Clear buttons

            // Try to find the Border or any element with HotkeyBindingEntry as DataContext
            DependencyObject current = e.OriginalSource as DependencyObject;
            while (current is not null)
            {
                if (current is FrameworkElement fe && fe.DataContext is HotkeyBindingEntry entry && DataContext is HotkeyManagerViewModel vm)
                {
                    vm.BeginListening(entry, skipNextMouseCapture: true);
                    Keyboard.Focus(this);
                    return;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }

        private void OnClearBindingClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is HotkeyBindingEntry entry && DataContext is HotkeyManagerViewModel vm)
            {
                vm.ClearBinding(entry);
            }
        }
        
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T t)
                    return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            if (DataContext is not HotkeyManagerViewModel vm || vm.ListeningEntry is null)
            {
                base.OnPreviewMouseDown(e);
                return;
            }

            e.Handled = true;
            int vk = e.ChangedButton switch
            {
                MouseButton.Left => 0x01,   // VK_LBUTTON
                MouseButton.Right => 0x02,  // VK_RBUTTON
                MouseButton.Middle => 0x04, // VK_MBUTTON
                MouseButton.XButton1 => 0x05,
                MouseButton.XButton2 => 0x06,
                _ => 0
            };
            if (vk != 0)
            {
                vm.AssignVirtualKey(vk);
            }
            else
            {
                base.OnPreviewMouseDown(e);
            }
        }

        private void OnToggleInputModeClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is HotkeyManagerViewModel vm)
            {
                vm.ToggleInputMode();
            }
        }
    }
}
