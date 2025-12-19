/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.UI.Radar.Maps;
using LoneEftDmaRadar.UI.Skia;
using SkiaSharp;

namespace LoneEftDmaRadar.UI.Radar.ViewModels.Helpers
{
    /// <summary>
    /// Manages ping/ripple effects for loot items on the radar.
    /// </summary>
    public sealed class RadarPingEffectManager
    {
        private readonly ConcurrentDictionary<string, PingEffect> _activePings = new();

        private sealed class PingEffect
        {
            public Vector2 Position { get; set; }
            public float Radius { get; set; }
            public float MaxRadius { get; set; }
            public float Alpha { get; set; }
            public Stopwatch Timer { get; } = Stopwatch.StartNew();
        }

        /// <summary>
        /// Creates ping effects for all loot items matching the given name.
        /// </summary>
        public void CreatePingsForItem(string itemName, IEnumerable<LootItem> lootItems, IEftMap map)
        {
            if (string.IsNullOrWhiteSpace(itemName) || lootItems == null || map == null)
                return;

            foreach (var item in lootItems)
            {
                var name = string.IsNullOrWhiteSpace(item.ShortName) ? item.Name : item.ShortName;
                if (string.Equals(name, itemName, StringComparison.Ordinal))
                {
                    var mapPos = item.Position.ToMapPos(map.Config);
                    var key = $"{itemName}_{item.Position.X:F1}_{item.Position.Y:F1}_{item.Position.Z:F1}";

                    _activePings[key] = new PingEffect
                    {
                        Position = mapPos,
                        Radius = 0f,
                        MaxRadius = 50f * App.Config.UI.UIScale,
                        Alpha = 1f
                    };
                }
            }
        }

        /// <summary>
        /// Draws all active ping effects on the canvas.
        /// </summary>
        public void DrawPingEffects(SKCanvas canvas, EftMapParams mapParams)
        {
            var toRemove = new List<string>();
            const float duration = 2f;

            foreach (var kvp in _activePings)
            {
                var ping = kvp.Value;
                float elapsed = (float)ping.Timer.Elapsed.TotalSeconds;

                if (elapsed >= duration)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                float progress = elapsed / duration;
                ping.Radius = ping.MaxRadius * progress;
                ping.Alpha = 1f - progress;

                var zoomedPos = ping.Position.ToZoomedPos(mapParams);

                using var ripplePaint = new SKPaint
                {
                    Color = SKColors.Yellow.WithAlpha((byte)(ping.Alpha * 255)),
                    StrokeWidth = 3f * App.Config.UI.UIScale,
                    Style = SKPaintStyle.Stroke,
                    IsAntialias = true
                };

                canvas.DrawCircle(zoomedPos, ping.Radius, ripplePaint);

                // Second ripple for more effect
                if (progress > 0.3f)
                {
                    float secondProgress = (progress - 0.3f) / 0.7f;
                    float secondRadius = ping.MaxRadius * secondProgress;
                    float secondAlpha = (1f - secondProgress) * 0.6f;

                    using var secondPaint = new SKPaint
                    {
                        Color = SKColors.Yellow.WithAlpha((byte)(secondAlpha * 255)),
                        StrokeWidth = 2f * App.Config.UI.UIScale,
                        Style = SKPaintStyle.Stroke,
                        IsAntialias = true
                    };

                    canvas.DrawCircle(zoomedPos, secondRadius, secondPaint);
                }
            }

            foreach (var key in toRemove)
                _activePings.TryRemove(key, out _);
        }

        /// <summary>
        /// Clears all active ping effects.
        /// </summary>
        public void Clear()
        {
            _activePings.Clear();
        }
    }
}
