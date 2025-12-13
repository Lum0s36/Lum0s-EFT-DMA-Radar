using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;

namespace LoneEftDmaRadar.UI.Hotkeys
{
    /// <summary>
    /// Interaction logic for HotkeyManagerWindow.xaml
    /// </summary>
    public partial class HotkeyManagerWindow : Window
    {
        public HotkeyManagerViewModel ViewModel { get; }

        public HotkeyManagerWindow()
        {
            InitializeComponent();
            ViewModel = new HotkeyManagerViewModel();
            DataContext = ViewModel;
            Loaded += (_, __) => HotkeyManagerViewModel.SuspendHotkeys();
            Closed += (_, __) => HotkeyManagerViewModel.ResumeHotkeys();
            AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown), true);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel.ListeningEntry is null)
                return;

            var keyToUse = e.Key == Key.System ? e.SystemKey : e.Key;

            e.Handled = true;
            if (keyToUse == Key.Escape)
            {
                ViewModel.ClearListening();
                return;
            }
            ViewModel.AssignVirtualKey(KeyInterop.VirtualKeyFromKey(keyToUse));
        }

        private void OnBindingClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep && FindAncestor<Button>(dep) is not null)
                return; // ignore Clear button clicks

            if (e.OriginalSource is FrameworkElement fe && fe.DataContext is HotkeyBindingEntry entry)
            {
                ViewModel.BeginListening(entry, skipNextMouseCapture: true);
                Keyboard.Focus(this);
            }
        }

        private void OnClearBindingClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is HotkeyBindingEntry entry)
            {
                ViewModel.ClearBinding(entry);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
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
            base.OnPreviewMouseDown(e);
            if (ViewModel.ListeningEntry is null)
                return;

            e.Handled = true;
            int vk = e.ChangedButton switch
            {
                MouseButton.Left => 0x01,
                MouseButton.Right => 0x02,
                MouseButton.Middle => 0x04,
                MouseButton.XButton1 => 0x05,
                MouseButton.XButton2 => 0x06,
                _ => 0
            };
            if (vk != 0)
            {
                ViewModel.AssignVirtualKey(vk);
            }
        }
    }
}
