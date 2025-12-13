using System;
using System.Drawing;
using SharpDX;
using SharpDX.Direct3D9;
using SharpDX.Mathematics.Interop;
using WinForms = System.Windows.Forms;
using D3D9Font = SharpDX.Direct3D9.Font;
using D3D9Line = SharpDX.Direct3D9.Line;
using DxColor = SharpDX.Mathematics.Interop.RawColorBGRA;

namespace LoneEftDmaRadar.UI.ESP
{
    internal enum DxTextSize
    {
        Small,
        Medium,
        Large
    }

    /// <summary>
    /// Minimal DX9 overlay control for high-frequency ESP rendering.
    /// </summary>
    internal sealed class Dx9OverlayControl : WinForms.Control, IDisposable
    {
        public event Action<Exception> DeviceInitFailed;

        private Direct3DEx _d3d;
        private DeviceEx _device;
        private PresentParameters _presentParameters;
        private D3D9Line _line;
        private D3D9Font _fontSmall;
        private D3D9Font _fontMedium;
        private D3D9Font _fontLarge;

        private string _fontFamily = "Segoe UI";
        private int _fontSizeSmall = 10;
        private int _fontSizeMedium = 12;
        private int _fontSizeLarge = 24;

        private readonly object _deviceLock = new();

        public Action<Dx9RenderContext> RenderFrame;

        public Dx9OverlayControl()
        {
            SetStyle(WinForms.ControlStyles.AllPaintingInWmPaint |
                     WinForms.ControlStyles.OptimizedDoubleBuffer |
                     WinForms.ControlStyles.Opaque |
                     WinForms.ControlStyles.UserPaint, true);
            DoubleBuffered = true;
            BackColor = Color.Black;
            TabStop = false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                InitializeDevice();
            }
            catch (Exception ex)
            {
                DeviceInitFailed?.Invoke(ex);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ResetDevice();
        }

        protected override void OnPaint(WinForms.PaintEventArgs e)
        {
            Render();
        }

        protected override void OnPaintBackground(WinForms.PaintEventArgs pevent)
        {
            // Suppress default background painting to avoid flicker when FPS is capped.
        }

        public void Render()
        {
            lock (_deviceLock)
            {
                if (_device is null)
                    return;

                try
                {
                    _device.Clear(ClearFlags.Target, new DxColor(0, 0, 0, 255), 1.0f, 0);
                    _device.BeginScene();

                    _device.SetRenderState(RenderState.AlphaBlendEnable, true);
                    _device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
                    _device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);

                    var ctx = new Dx9RenderContext(_device, _line, _fontSmall, _fontMedium, _fontLarge, Width, Height);
                    RenderFrame?.Invoke(ctx);

                    _device.EndScene();
                    _device.Present();
                }
                catch (SharpDXException ex) when (ex.ResultCode == ResultCode.DeviceLost || ex.ResultCode == ResultCode.DeviceNotReset)
                {
                    ResetDevice();
                }
            }
        }

        private void InitializeDevice()
        {
            DisposeDevice();

            _d3d = new Direct3DEx();
            _presentParameters = BuildPresentParameters();

            var commonFlags = CreateFlags.Multithreaded | CreateFlags.FpuPreserve;

            // Try hardware first, then fall back to software VP and unknown format.
            if (!TryCreateDevice(commonFlags | CreateFlags.HardwareVertexProcessing))
            {
                if (_presentParameters.BackBufferFormat != Format.Unknown)
                {
                    _presentParameters.BackBufferFormat = Format.Unknown;
                    if (TryCreateDevice(commonFlags | CreateFlags.HardwareVertexProcessing))
                        return;
                }

                if (!TryCreateDevice(commonFlags | CreateFlags.SoftwareVertexProcessing))
                    throw new SharpDXException(ResultCode.InvalidCall);
            }
        }

        private PresentParameters BuildPresentParameters()
        {
            return new PresentParameters
            {
                BackBufferWidth = Math.Max(Width, 1),
                BackBufferHeight = Math.Max(Height, 1),
                BackBufferFormat = Format.A8R8G8B8,
                BackBufferCount = 1,
                MultiSampleType = MultisampleType.None,
                MultiSampleQuality = 0,
                SwapEffect = SwapEffect.Discard,
                EnableAutoDepthStencil = false,
                PresentFlags = PresentFlags.None,
                Windowed = true,
                PresentationInterval = PresentInterval.Immediate,
                DeviceWindowHandle = Handle
            };
        }

        private bool TryCreateDevice(CreateFlags flags)
        {
            try
            {
                _device = new DeviceEx(
                    _d3d,
                    0,
                    DeviceType.Hardware,
                    Handle,
                    flags,
                    _presentParameters);

                BuildResources();
                return true;
            }
            catch (SharpDXException ex)
            {
                DisposeDevice();
                DeviceInitFailed?.Invoke(ex);
                return false;
            }
        }

        private void ResetDevice()
        {
            lock (_deviceLock)
            {
                if (_device is null)
                    return;

                _line?.OnLostDevice();
                _fontSmall?.OnLostDevice();
                _fontMedium?.OnLostDevice();
                _fontLarge?.OnLostDevice();

                _presentParameters.BackBufferWidth = Math.Max(Width, 1);
                _presentParameters.BackBufferHeight = Math.Max(Height, 1);

                try
                {
                    _device.ResetEx(ref _presentParameters);
                }
                catch (SharpDXException)
                {
                    DisposeDevice();
                    try
                    {
                        InitializeDevice();
                    }
                    catch (Exception ex)
                    {
                        DeviceInitFailed?.Invoke(ex);
                        return;
                    }
                }

                _line?.OnResetDevice();
                _fontSmall?.OnResetDevice();
                _fontMedium?.OnResetDevice();
                _fontLarge?.OnResetDevice();
            }
        }

        private void BuildResources()
        {
            _line = new D3D9Line(_device)
            {
                Antialias = true,
                GLLines = true,
                Width = 1.0f
            };

            RebuildFonts();
        }

        private D3D9Font CreateFont(int height)
        {
            return new D3D9Font(_device, new FontDescription
            {
                Height = height,
                FaceName = _fontFamily,
                Weight = SharpDX.Direct3D9.FontWeight.Normal,
                Quality = FontQuality.Antialiased,
                PitchAndFamily = FontPitchAndFamily.Default | FontPitchAndFamily.DontCare,
                CharacterSet = FontCharacterSet.Default
            });
        }

        /// <summary>
        /// Update font family/sizes and rebuild DX fonts. Safe to call at runtime.
        /// </summary>
        public void SetFontConfig(string fontFamily, int small, int medium, int large)
        {
            lock (_deviceLock)
            {
                if (!string.IsNullOrWhiteSpace(fontFamily))
                    _fontFamily = fontFamily.Trim();

                _fontSizeSmall = ClampFontSize(small);
                _fontSizeMedium = ClampFontSize(medium);
                _fontSizeLarge = ClampFontSize(large);

                if (_device != null)
                {
                    RebuildFonts();
                }
            }
        }

        private static int ClampFontSize(int value)
        {
            if (value <= 0) return 10;
            return Math.Clamp(value, 6, 72);
        }

        private void RebuildFonts()
        {
            _fontSmall?.Dispose();
            _fontMedium?.Dispose();
            _fontLarge?.Dispose();

            _fontSmall = CreateFont(_fontSizeSmall);
            _fontMedium = CreateFont(_fontSizeMedium);
            _fontLarge = CreateFont(_fontSizeLarge);
        }

        private void DisposeDevice()
        {
            _line?.Dispose();
            _fontSmall?.Dispose();
            _fontMedium?.Dispose();
            _fontLarge?.Dispose();
            _device?.Dispose();
            _d3d?.Dispose();

            _line = null;
            _fontSmall = null;
            _fontMedium = null;
            _fontLarge = null;
            _device = null;
            _d3d = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_deviceLock)
                {
                    DisposeDevice();
                }
            }

            base.Dispose(disposing);
        }
    }

    internal readonly struct Dx9RenderContext
    {
        private readonly DeviceEx _device;
        private readonly D3D9Line _line;
        private readonly D3D9Font _fontSmall;
        private readonly D3D9Font _fontMedium;
        private readonly D3D9Font _fontLarge;

        public int Width { get; }
        public int Height { get; }

        public Dx9RenderContext(
            DeviceEx device,
            D3D9Line line,
            D3D9Font fontSmall,
            D3D9Font fontMedium,
            D3D9Font fontLarge,
            int width,
            int height)
        {
            _device = device;
            _line = line;
            _fontSmall = fontSmall;
            _fontMedium = fontMedium;
            _fontLarge = fontLarge;
            Width = width;
            Height = height;
        }

        public void Clear(DxColor color)
        {
            _device.Clear(ClearFlags.Target, color, 1.0f, 0);
        }

        public void DrawLine(RawVector2 p1, RawVector2 p2, DxColor color, float thickness)
        {
            _line.Width = Math.Max(1.0f, thickness);
            _line.Begin();
            _line.Draw(new[] { p1, p2 }, color);
            _line.End();
        }

        public void DrawRect(RectangleF rect, DxColor color, float thickness)
        {
            var pts = new[]
            {
                new RawVector2(rect.Left, rect.Top),
                new RawVector2(rect.Right, rect.Top),
                new RawVector2(rect.Right, rect.Bottom),
                new RawVector2(rect.Left, rect.Bottom),
                new RawVector2(rect.Left, rect.Top)
            };

            _line.Width = Math.Max(1.0f, thickness);
            _line.Begin();
            _line.Draw(pts, color);
            _line.End();
        }

        public void DrawFilledRect(RectangleF rect, DxColor color)
        {
            var verts = new ColoredVertex[4]
            {
                new ColoredVertex(rect.Left, rect.Top, color),
                new ColoredVertex(rect.Right, rect.Top, color),
                new ColoredVertex(rect.Left, rect.Bottom, color),
                new ColoredVertex(rect.Right, rect.Bottom, color)
            };

            _device.VertexFormat = ColoredVertex.Format;
            _device.DrawUserPrimitives(PrimitiveType.TriangleStrip, 2, verts);
        }

        public void DrawCircle(RawVector2 center, float radius, DxColor color, bool filled)
        {
            if (radius <= 0.5f)
                return;

            const int segments = 32;

            if (filled)
            {
                var verts = new ColoredVertex[segments + 2];
                verts[0] = new ColoredVertex(center.X, center.Y, color);

                float increment = (float)(Math.PI * 2.0 / segments);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * increment;
                    float x = center.X + MathF.Cos(angle) * radius;
                    float y = center.Y + MathF.Sin(angle) * radius;
                    verts[i + 1] = new ColoredVertex(x, y, color);
                }

                _device.VertexFormat = ColoredVertex.Format;
                _device.DrawUserPrimitives(PrimitiveType.TriangleFan, segments, verts);
            }
            else
            {
                var pts = new RawVector2[segments + 1];
                float increment = (float)(Math.PI * 2.0 / segments);

                for (int i = 0; i < segments; i++)
                {
                    float angle = i * increment;
                    pts[i] = new RawVector2(
                        center.X + MathF.Cos(angle) * radius,
                        center.Y + MathF.Sin(angle) * radius);
                }

                pts[segments] = pts[0];

                _line.Width = 1.5f;
                _line.Begin();
                _line.Draw(pts, color);
                _line.End();
            }
        }

        public RawRectangle MeasureText(string text, DxTextSize size)
        {
            var font = GetFont(size);
            return font.MeasureText(null, text, FontDrawFlags.Left);
        }

        public void DrawText(string text, float x, float y, DxColor color, DxTextSize size, bool centerX = false, bool centerY = false)
        {
            var font = GetFont(size);
            var bounds = font.MeasureText(null, text, FontDrawFlags.Left);

            int textWidth = Math.Max(1, bounds.Right - bounds.Left);
            int textHeight = Math.Max(1, bounds.Bottom - bounds.Top);

            float drawX = x;
            float drawY = y;

            if (centerX)
                drawX -= textWidth / 2f;
            if (centerY)
                drawY -= textHeight / 2f;

            var rect = new RawRectangle(
                (int)drawX,
                (int)drawY,
                (int)drawX + textWidth + 2,
                (int)drawY + textHeight + 2);

            font.DrawText(null, text, rect, FontDrawFlags.NoClip, color);
        }

        private D3D9Font GetFont(DxTextSize size) => size switch
        {
            DxTextSize.Small => _fontSmall,
            DxTextSize.Large => _fontLarge,
            _ => _fontMedium
        };

        private struct ColoredVertex
        {
            public float X;
            public float Y;
            public float Z;
            public float Rhw;
            public int Color;

            public ColoredVertex(float x, float y, DxColor color)
            {
                X = x;
                Y = y;
                Z = 0.5f;
                Rhw = 1.0f;
                Color = PackColor(color);
            }

            public const VertexFormat Format = VertexFormat.PositionRhw | VertexFormat.Diffuse;

            private static int PackColor(DxColor color)
            {
                return (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
            }
        }
    }
}
