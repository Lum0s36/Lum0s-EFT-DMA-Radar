/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
MIT License

Copyright (c) 2025 Lone DMA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/

using System.Windows.Media;

namespace LoneEftDmaRadar.UI.Misc
{
    public sealed partial class LoadingWindow : Window, IDisposable
    {
        public LoadingViewModel ViewModel { get; }

        public LoadingWindow()
        {
            InitializeComponent();
            DataContext = ViewModel = new LoadingViewModel(this);
            
            // Subscribe to progress changes for smooth animation
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            // Wait for window to load before accessing named elements
            Loaded += LoadingWindow_Loaded;
            
            this.Show();
        }

        private void LoadingWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Setup RenderTransform for pulse ellipse after window is loaded
            var pulseEllipse = FindName("PulseEllipse") as System.Windows.Shapes.Ellipse;
            if (pulseEllipse != null && pulseEllipse.RenderTransform == null)
            {
                pulseEllipse.RenderTransform = new ScaleTransform();
                pulseEllipse.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LoadingViewModel.Progress))
            {
                // Trigger smooth progress animation
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    var progressFill = FindName("ProgressFill") as System.Windows.Controls.Border;
                    if (progressFill != null)
                    {
                        var storyboard = (System.Windows.Media.Animation.Storyboard)Resources["ProgressAnimation"];
                        if (storyboard != null)
                        {
                            // Clone storyboard to avoid conflicts
                            var clonedStoryboard = storyboard.Clone();
                            System.Windows.Media.Animation.Storyboard.SetTarget(clonedStoryboard, progressFill);
                            clonedStoryboard.Begin();
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private bool _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, true) == false)
            {
                if (ViewModel != null)
                {
                    ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                }
                Loaded -= LoadingWindow_Loaded;
                this.Close();
            }
        }
    }
}
