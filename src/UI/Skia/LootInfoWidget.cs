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

using Collections.Pooled;
using LoneEftDmaRadar.Misc;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot.Helpers;
using SkiaSharp.Views.WPF;

namespace LoneEftDmaRadar.UI.Skia
{
    /// <summary>
    /// Widget that displays all visible loot items sorted by value in a table format.
    /// </summary>
    public sealed class LootInfoWidget : AbstractSKWidget
    {
        private class LootEntry
        {
            public string Name { get; set; }
            public int Value { get; set; }
            public int Count { get; set; }
            public LootColorType ColorType { get; set; }
            public string CustomFilterColor { get; set; }
        }

        private float _scrollOffset = 0f;
        private float _maxScrollOffset = 0f;
        private List<LootEntry> _cachedSortedLoot = new();

        /// <summary>
        /// Event that fires when an item is clicked with the item name for ripple effect.
        /// </summary>
        public event EventHandler<string> ItemClickedForPing;

        public LootInfoWidget(SKGLElement parent, SKRect location, bool minimized, float scale)
            : base(parent, "Loot Info", new SKPoint(location.Left, location.Top),
                new SKSize(location.Width, location.Height), scale, true)
        {
            Minimized = minimized;
            SetScaleFactor(scale);
        }

        protected override void OnClientAreaClicked(SKPoint clickPoint)
        {
            // Check if click is on a row
            var font = SKFonts.InfoWidgetFont;
            float pad = 2.5f * ScaleFactor;
            
            // Calculate which row was clicked
            float relativeY = clickPoint.Y - ClientRectangle.Top - pad + _scrollOffset;
            float headerHeight = font.Spacing;
            
            if (relativeY < headerHeight)
                return; // Clicked on header
            
            int rowIndex = (int)((relativeY - headerHeight) / font.Spacing);
            
            // Find the item at this row index
            if (rowIndex >= 0 && rowIndex < _cachedSortedLoot.Count)
            {
                var clickedEntry = _cachedSortedLoot[rowIndex];
                ItemClickedForPing?.Invoke(this, clickedEntry.Name);
            }
        }

        public void Draw(SKCanvas canvas, IEnumerable<LootItem> lootItems)
        {
            if (Minimized)
            {
                Draw(canvas);
                return;
            }

            // Group loot by name and aggregate
            using var aggregated = new PooledDictionary<string, LootEntry>();
            
            if (lootItems != null)
            {
                foreach (var item in lootItems)
                {
                    var name = string.IsNullOrWhiteSpace(item.ShortName) ? item.Name : item.ShortName;
                    if (string.IsNullOrWhiteSpace(name))
                        name = "Unknown";

                    if (aggregated.TryGetValue(name, out var entry))
                    {
                        entry.Count++;
                    }
                    else
                    {
                        aggregated[name] = new LootEntry
                        {
                            Name = name,
                            Value = item.Price,
                            Count = 1,
                            ColorType = LootVisibilityHelper.GetLootColorType(item),
                            CustomFilterColor = LootVisibilityHelper.GetCustomFilterColor(item)
                        };
                    }
                }
            }

            // Sort by value descending
            using var sortedLoot = aggregated.Values
                .OrderByDescending(x => x.Value)
                .ToPooledList();

            // Cache for click detection
            _cachedSortedLoot.Clear();
            _cachedSortedLoot.AddRange(sortedLoot);

            // Measure column widths
            var font = SKFonts.InfoWidgetFont;
            float pad = 2.5f * ScaleFactor;
            
            string headerItem = "Item";
            string headerValue = "Value";
            string headerCount = "Count";

            float maxItemWidth = font.MeasureText(headerItem);
            float maxValueWidth = font.MeasureText(headerValue);
            float maxCountWidth = font.MeasureText(headerCount);

            foreach (var entry in sortedLoot)
            {
                float itemWidth = font.MeasureText(entry.Name);
                float valueWidth = font.MeasureText(Utilities.FormatNumberKM(entry.Value));
                float countWidth = font.MeasureText(entry.Count.ToString());

                if (itemWidth > maxItemWidth) maxItemWidth = itemWidth;
                if (valueWidth > maxValueWidth) maxValueWidth = valueWidth;
                if (countWidth > maxCountWidth) maxCountWidth = countWidth;
            }

            // Add padding between columns
            float colPadding = 10f * ScaleFactor;
            float totalWidth = maxItemWidth + maxValueWidth + maxCountWidth + (colPadding * 3) + (pad * 2);
            
            // Calculate content height (header + all rows)
            float contentHeight = (1 + sortedLoot.Count) * font.Spacing + (pad * 2);

            // Widget size stays as user set it (DON'T auto-resize)
            // But ensure minimum size
            float minWidth = totalWidth;
            float minHeight = font.Spacing * 3 + (pad * 2); // At least header + 2 rows visible

            if (Size.Width < minWidth)
                Size = new SKSize(minWidth, Size.Height);
            if (Size.Height < minHeight)
                Size = new SKSize(Size.Width, minHeight);

            Draw(canvas); // Draw background/frame

            // Calculate max scroll offset
            float availableHeight = ClientRectangle.Height - (pad * 2);
            _maxScrollOffset = Math.Max(0, contentHeight - availableHeight - (pad * 2));

            // Clamp scroll offset
            _scrollOffset = Math.Clamp(_scrollOffset, 0f, _maxScrollOffset);

            // Create clipping region for content (prevents drawing outside widget)
            canvas.Save();
            canvas.ClipRect(ClientRectangle);

            // Starting position for text (with scroll offset applied)
            var drawPt = new SKPoint(
                ClientRectangle.Left + pad,
                ClientRectangle.Top + font.Spacing / 2 + pad - _scrollOffset);

            // Column X positions
            float itemX = drawPt.X;
            float valueX = itemX + maxItemWidth + colPadding;
            float countX = valueX + maxValueWidth + colPadding;

            // Draw header row
            canvas.DrawText(headerItem, new SKPoint(itemX, drawPt.Y), SKTextAlign.Left, font, SKPaints.TextPlayersOverlay);
            canvas.DrawText(headerValue, new SKPoint(valueX, drawPt.Y), SKTextAlign.Left, font, SKPaints.TextPlayersOverlay);
            canvas.DrawText(headerCount, new SKPoint(countX, drawPt.Y), SKTextAlign.Left, font, SKPaints.TextPlayersOverlay);
            
            drawPt.Offset(0, font.Spacing);

            // Draw data rows
            int rowCount = 0;
            int visibleRows = Math.Min(sortedLoot.Count, 50); // Limit to 50 visible rows
            foreach (var entry in sortedLoot)
            {
                // Skip if row is above visible area (scrolled past)
                if (drawPt.Y < ClientRectangle.Top)
                {
                    drawPt.Offset(0, font.Spacing);
                    continue;
                }

                // Stop if row is below visible area
                if (drawPt.Y > ClientRectangle.Bottom)
                    break;

                var paint = GetPaintForEntry(entry);

                canvas.DrawText(entry.Name, new SKPoint(itemX, drawPt.Y), SKTextAlign.Left, font, paint);
                canvas.DrawText(Utilities.FormatNumberKM(entry.Value), new SKPoint(valueX, drawPt.Y), SKTextAlign.Left, font, paint);
                canvas.DrawText(entry.Count.ToString(), new SKPoint(countX, drawPt.Y), SKTextAlign.Left, font, paint);

                drawPt.Offset(0, font.Spacing);
                rowCount++;
            }

            // Draw scrollbar if needed
            if (_maxScrollOffset > 0)
            {
                DrawScrollbar(canvas);
            }

            canvas.Restore();
        }

        private void DrawScrollbar(SKCanvas canvas)
        {
            float scrollbarWidth = 6f * ScaleFactor;
            float scrollbarX = ClientRectangle.Right - scrollbarWidth - 2f;
            float scrollbarY = ClientRectangle.Top + 2f;
            float scrollbarHeight = ClientRectangle.Height - 4f;

            // Background track
            var trackPaint = new SKPaint
            {
                Color = new SKColor(60, 60, 60, 180),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRect(scrollbarX, scrollbarY, scrollbarWidth, scrollbarHeight, trackPaint);

            // Calculate thumb size and position
            float contentHeight = _maxScrollOffset + ClientRectangle.Height;
            float thumbHeight = Math.Max(20f, (ClientRectangle.Height / contentHeight) * scrollbarHeight);
            float thumbY = scrollbarY + (_scrollOffset / _maxScrollOffset) * (scrollbarHeight - thumbHeight);

            // Draw thumb
            var thumbPaint = new SKPaint
            {
                Color = new SKColor(120, 120, 120, 220),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRoundRect(scrollbarX, thumbY, scrollbarWidth, thumbHeight, 3f, 3f, thumbPaint);
        }

        /// <summary>
        /// Scroll the content. Positive = scroll down, Negative = scroll up.
        /// </summary>
        public void Scroll(float delta)
        {
            _scrollOffset = Math.Clamp(_scrollOffset + delta, 0f, _maxScrollOffset);
        }

        /// <summary>
        /// Handle mouse wheel scrolling when widget is focused.
        /// </summary>
        protected override void OnMouseWheel(int delta)
        {
            // delta > 0 = scroll up, delta < 0 = scroll down
            // Invert and scale the delta for smooth scrolling
            float scrollAmount = -(delta / 120f) * SKFonts.InfoWidgetFont.Spacing * 3f;
            Scroll(scrollAmount);
        }

        /// <summary>
        /// Gets the paint for a loot entry using the same color logic as Radar/ESP.
        /// </summary>
        private static SKPaint GetPaintForEntry(LootEntry entry)
        {
            return entry.ColorType switch
            {
                LootColorType.Quest => SKPaints.TextQuestItem,
                LootColorType.Wishlist => SKPaints.TextWishlistItem,
                LootColorType.CustomFilter => GetCustomFilterPaint(entry.CustomFilterColor),
                LootColorType.Important => SKPaints.TextImportantLoot,
                LootColorType.Backpack => SKPaints.TextBackpacks,
                LootColorType.Meds => SKPaints.TextMeds,
                LootColorType.Food => SKPaints.TextFood,
                _ => SKPaints.TextLoot
            };
        }

        // Cache for custom filter paints in widget
        private static readonly ConcurrentDictionary<string, SKPaint> _customFilterPaintCache = new();

        private static SKPaint GetCustomFilterPaint(string colorHex)
        {
            if (string.IsNullOrEmpty(colorHex))
                return SKPaints.TextImportantLoot;

            return _customFilterPaintCache.GetOrAdd(colorHex, hex =>
            {
                if (SKColor.TryParse(hex, out var skColor))
                {
                    return new SKPaint
                    {
                        Color = skColor,
                        IsStroke = false,
                        IsAntialias = true
                    };
                }
                return SKPaints.TextImportantLoot;
            });
        }

        public override void SetScaleFactor(float newScale)
        {
            base.SetScaleFactor(newScale);
        }
    }
}
