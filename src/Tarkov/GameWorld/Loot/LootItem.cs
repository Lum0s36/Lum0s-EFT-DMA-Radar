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
using LoneEftDmaRadar.Tarkov.GameWorld.Loot.Helpers;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.Unity;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Loot;
using LoneEftDmaRadar.UI.Radar.Maps;
using LoneEftDmaRadar.UI.Skia;
using LoneEftDmaRadar.Web.TarkovDev.Data;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Loot
{
    public class LootItem : IMouseoverEntity, IMapEntity, IWorldEntity
    {
        private static EftDmaConfig Config { get; } = App.Config;
        private readonly TarkovMarketItem _item;
        private readonly bool _isQuestItem;

        // Change from readonly field to mutable field so position can be updated
        private Vector3 _position;
        
        // Store transform for position updates
        protected UnityTransform _transform;

        public LootItem(TarkovMarketItem item, Vector3 position, bool isQuestItem = false)
        {
            ArgumentNullException.ThrowIfNull(item, nameof(item));
            _item = item;
            _position = position;
            _isQuestItem = isQuestItem;
        }

        public LootItem(string id, string name, Vector3 position, bool isQuestItem = false)
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));
            ArgumentNullException.ThrowIfNull(name, nameof(name));
            _item = new TarkovMarketItem
            {
                Name = name,
                ShortName = name,
                FleaPrice = -1,
                TraderPrice = -1,
                BsgId = id
            };
            _position = position;
            _isQuestItem = isQuestItem;
        }

        // Internal constructor for LootManager to set transform
        internal LootItem(TarkovMarketItem item, Vector3 position, UnityTransform transform, bool isQuestItem = false)
            : this(item, position, isQuestItem)
        {
            _transform = transform;
        }

        internal LootItem(string id, string name, Vector3 position, UnityTransform transform, bool isQuestItem = false)
            : this(id, name, position, isQuestItem)
        {
            _transform = transform;
        }

        /// <summary>
        /// Update the position from the transform if available.
        /// </summary>
        internal void UpdatePosition()
        {
            if (_transform != null)
            {
                try
                {
                    _position = _transform.UpdatePosition();
                }
                catch
                {
                    // Position update failed, keep old position
                }
            }
        }

        /// <summary>
        /// Item's BSG ID.
        /// </summary>
        public virtual string ID => _item.BsgId;

        /// <summary>
        /// Item's Long Name.
        /// </summary>
        public virtual string Name => _item.Name;

        /// <summary>
        /// Item's Short Name.
        /// </summary>
        public string ShortName => _item.ShortName;

        /// <summary>
        /// Item's Price (In roubles).
        /// </summary>
        public int Price
        {
            get
            {
                long price;
                if (Config.Loot.PricePerSlot)
                {
                    if (Config.Loot.PriceMode is LootPriceMode.FleaMarket)
                        price = (long)((float)_item.FleaPrice / GridCount);
                    else
                        price = (long)((float)_item.TraderPrice / GridCount);
                }
                else
                {
                    if (Config.Loot.PriceMode is LootPriceMode.FleaMarket)
                        price = _item.FleaPrice;
                    else
                        price = _item.TraderPrice;
                }
                if (price <= 0)
                    price = Math.Max(_item.FleaPrice, _item.TraderPrice);
                return (int)price;
            }
        }

        /// <summary>
        /// Number of grid spaces this item takes up.
        /// </summary>
        public int GridCount => _item.Slots == 0 ? 1 : _item.Slots;

        /// <summary>
        /// Custom filter for this item (if set).
        /// </summary>
        public LootFilterEntry CustomFilter => _item.CustomFilter;

        /// <summary>
        /// True if the item is important via the UI.
        /// </summary>
        public bool Important => CustomFilter?.Important ?? false;

        /// <summary>
        /// True if this item is marked as a quest item by the game data.
        /// </summary>
        public bool IsQuestItem => _isQuestItem;

        /// <summary>
        /// True if the item is blacklisted via the UI.
        /// </summary>
        public bool Blacklisted => CustomFilter?.Blacklisted ?? false;

        /// <summary>
        /// True if this item is on the player's in-game wishlist.
        /// </summary>
        public bool IsWishlisted => App.Config.Loot.ShowWishlistedRadar && LocalPlayer.WishlistItems.Contains(ID);

        /// <summary>
        /// Checks if an item/container is important.
        /// </summary>
        public bool IsImportant
        {
            get
            {
                if (Blacklisted)
                    return false;
                return _item.Important || IsWishlisted; // Include wishlist items as important
            }
        }

        public bool IsMeds => _item.IsMed;
        public bool IsFood => _item.IsFood;
        public bool IsBackpack => _item.IsBackpack;
        public bool IsWeapon => _item.IsWeapon;
        public bool IsCurrency => _item.IsCurrency;

        /// <summary>
        /// Checks if an item exceeds regular loot price threshold.
        /// </summary>
        public bool IsRegularLoot
        {
            get
            {
                if (Blacklisted)
                    return false;
                return Price >= App.Config.Loot.MinValue;
            }
        }

        /// <summary>
        /// Checks if an item exceeds valuable loot price threshold.
        /// </summary>
        public bool IsValuableLoot
        {
            get
            {
                if (Blacklisted)
                    return false;
                return Price >= App.Config.Loot.MinValueValuable;
            }
        }

        /// <summary>
        /// True if this item contains the specified Search Predicate.
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns>True if search matches, otherwise False.</returns>
        public bool ContainsSearchPredicate(Predicate<LootItem> predicate)
        {
            return predicate(this);
        }

        public ref readonly Vector3 Position => ref _position;
        public Vector2 MouseoverPosition { get; set; }

        public virtual void Draw(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            if (IsQuestItem && !App.Config.Loot.ShowQuestItems)
                return;

            var label = GetUILabel();
            var paints = GetPaints();
            var heightDiff = Position.Y - localPlayer.Position.Y;
            var point = Position.ToMapPos(mapParams.Map).ToZoomedPos(mapParams);
            MouseoverPosition = new Vector2(point.X, point.Y);
            
            SKPaints.ShapeOutline.StrokeWidth = 2f;
            
            if (heightDiff > 1.45) // loot is above player
            {
                using var path = point.GetUpArrow(5);
                canvas.DrawPath(path, SKPaints.ShapeOutline);
                canvas.DrawPath(path, paints.Item1);
            }
            else if (heightDiff < -1.45) // loot is below player
            {
                using var path = point.GetDownArrow(5);
                canvas.DrawPath(path, SKPaints.ShapeOutline);
                canvas.DrawPath(path, paints.Item1);
            }
            else // loot is level with player
            {
                var size = 5 * App.Config.UI.UIScale;
                canvas.DrawCircle(point, size, SKPaints.ShapeOutline);
                canvas.DrawCircle(point, size, paints.Item1);
            }

            point.Offset(7 * App.Config.UI.UIScale, 3 * App.Config.UI.UIScale);

            canvas.DrawText(
                label,
                point,
                SKTextAlign.Left,
                SKFonts.UIRegular,
                SKPaints.TextOutline); // Draw outline
            canvas.DrawText(
                label,
                point,
                SKTextAlign.Left,
                SKFonts.UIRegular,
                paints.Item2);
        }

        public virtual void DrawMouseover(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            using var lines = new PooledList<string>();
            
            // For quest items, show special header
            if (IsQuestItem)
            {
                lines.Add("QUEST ITEM");
                lines.Add($"PICK UP: {Name.Replace("Q_", "")}");
            }
            else
            {
                lines.Add(GetUILabel());
            }
            
            // Show distance
            float distance = MathF.Sqrt(Vector3.DistanceSquared(localPlayer.Position, Position));
            lines.Add($"Distance: {distance:F0}m");
            
            Position.ToMapPos(mapParams.Map).ToZoomedPos(mapParams).DrawMouseoverText(canvas, lines.Span);
        }

        /// <summary>
        /// Gets a UI Friendly Label.
        /// </summary>
        /// <returns>Item Label string cleaned up for UI usage.</returns>
        public string GetUILabel()
        {
            var label = "";
            
            // Quest items get special prefix
            if (IsQuestItem)
            {
                label = "Q: " + Name.Replace("Q_", "");
                return label;
            }
            
            // "!!" for Wishlist items
            if (IsWishlisted)
                label += "!! ";
            else if (Price > 0)
                label += $"[{Utilities.FormatNumberKM(Price)}] ";
            label += ShortName;

            if (string.IsNullOrEmpty(label))
                label = "Item";
            return label;
        }

        private ValueTuple<SKPaint, SKPaint> GetPaints()
        {
            if (IsQuestItem)
                return new(SKPaints.PaintQuestItem, SKPaints.TextQuestItem);
            // Wishlist takes priority over custom filters
            if (IsWishlisted)
                return new(SKPaints.PaintWishlistItem, SKPaints.TextWishlistItem);
            if (LootFilter.ShowBackpacks && IsBackpack)
                return new(SKPaints.PaintBackpacks, SKPaints.TextBackpacks);
            if (LootFilter.ShowMeds && IsMeds)
                return new(SKPaints.PaintMeds, SKPaints.TextMeds);
            if (LootFilter.ShowFood && IsFood)
                return new(SKPaints.PaintFood, SKPaints.TextFood);
            var filterColor = CustomFilter?.Color;
            if (!string.IsNullOrEmpty(filterColor))
            {
                var filterPaints = GetFilterPaints(filterColor);
                return new(filterPaints.Item1, filterPaints.Item2);
            }
            if (IsValuableLoot || this is LootAirdrop)
                return new(SKPaints.PaintImportantLoot, SKPaints.TextImportantLoot);
            return new(SKPaints.PaintLoot, SKPaints.TextLoot);
        }

        #region Custom Loot Paints
        private static readonly ConcurrentDictionary<string, Tuple<SKPaint, SKPaint>> _paints = new();

        /// <summary>
        /// Returns the Paints for this color value.
        /// </summary>
        /// <param name="color">Color rgba hex string.</param>
        /// <returns>Tuple of paints. Item1 = Paint, Item2 = Text. Item3 = ESP Paint, Item4 = ESP Text</returns>
        private static Tuple<SKPaint, SKPaint> GetFilterPaints(string color)
        {
            if (!SKColor.TryParse(color, out var skColor))
                return new Tuple<SKPaint, SKPaint>(SKPaints.PaintLoot, SKPaints.TextLoot);

            var result = _paints.AddOrUpdate(color,
                key =>
                {
                    var paint = new SKPaint
                    {
                        Color = skColor,
                        StrokeWidth = 3f * App.Config.UI.UIScale,
                        Style = SKPaintStyle.Fill,
                        IsAntialias = true
                    };
                    var text = new SKPaint
                    {
                        Color = skColor,
                        IsStroke = false,
                        IsAntialias = true
                    };
                    return new Tuple<SKPaint, SKPaint>(paint, text);
                },
                (key, existingValue) =>
                {
                    existingValue.Item1.StrokeWidth = 3f * App.Config.UI.UIScale;
                    return existingValue;
                });

            return result;
        }

        public static void ScaleLootPaints(float newScale)
        {
            foreach (var paint in _paints)
            {
                paint.Value.Item1.StrokeWidth = 3f * newScale;
            }
        }

        #endregion
    }
}
