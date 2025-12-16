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
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Radar.Maps;
using LoneEftDmaRadar.UI.Radar.ViewModels;
using LoneEftDmaRadar.UI.Skia;
using LoneEftDmaRadar.Web.TarkovDev.Data;
using LoneEftDmaRadar.Misc;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Loot
{
    /// <summary>
    /// Type of important item found on a corpse.
    /// </summary>
    public enum CorpseImportantItemType
    {
        None,
        Wishlist,       // In-game wishlist item (uses WishlistLoot color)
        ImportantFilter // Loot filter important item (uses filter color or ValuableLoot color)
    }

    /// <summary>
    /// Result of checking for important items on a corpse.
    /// </summary>
    public readonly struct CorpseImportantItem
    {
        public string Label { get; init; }
        public CorpseImportantItemType Type { get; init; }
        public string CustomFilterColor { get; init; }
        
        public bool HasItem => Type != CorpseImportantItemType.None;
    }

    public sealed class LootCorpse : LootItem
    {
        private static readonly TarkovMarketItem _default = new();
        private readonly ulong _corpse;
        
        // Cache for custom filter paints to avoid creating new SKPaint objects every frame
        private static readonly ConcurrentDictionary<string, SKPaint> _filterPaintCache = new();
        
        /// <summary>
        /// Corpse container's associated player object (if any).
        /// </summary>
        public AbstractPlayer Player { get; set; }

        /// <summary>
        /// Get the display name for the corpse including type information.
        /// Format: "Type:Name" (e.g., "Boss:Reshala", "PMC:PlayerName", "Scav", "Rogue")
        /// </summary>
        public override string Name => GetCorpseDisplayName();

        /// <summary>
        /// Get just the player/AI type label (e.g., "PMC", "Boss", "Scav", "Rogue").
        /// </summary>
        public string TypeLabel => GetTypeLabel();

        /// <summary>
        /// Get just the player/AI name without type prefix.
        /// </summary>
        public string PlayerName => Player?.Name ?? "Unknown";

        /// <summary>
        /// Constructor.
        /// </summary>
        public LootCorpse(ulong corpse, Vector3 position) : base(_default, position)
        {
            _corpse = corpse;
        }

        /// <summary>
        /// Constructor with transform for live position updates.
        /// </summary>
        public LootCorpse(ulong corpse, Vector3 position, UnityTransform transform) : base(_default, position, transform)
        {
            _corpse = corpse;
        }
        
        /// <summary>
        /// Sync the corpse's player reference from a list of dead players.
        /// </summary>
        /// <param name="deadPlayers"></param>
        public void Sync(IReadOnlyList<AbstractPlayer> deadPlayers)
        {
            Player ??= deadPlayers?.FirstOrDefault(x => x.Corpse == _corpse);
            if (Player is not null && Player.LootObject is null)
                Player.LootObject = this;
        }

        /// <summary>
        /// Gets the type label for the corpse (PMC, Boss, Scav, Rogue, etc.).
        /// </summary>
        private string GetTypeLabel()
        {
            if (Player is null)
                return "Corpse";

            return Player.Type switch
            {
                PlayerType.PMC => Player.PlayerSide == Enums.EPlayerSide.Bear ? "Bear" : 
                                  Player.PlayerSide == Enums.EPlayerSide.Usec ? "Usec" : "PMC",
                PlayerType.Teammate => "Teammate",
                PlayerType.AIBoss => "Boss",
                PlayerType.AIRaider => "Rogue",
                PlayerType.AIScav => "Scav",
                PlayerType.PScav => "PScav",
                PlayerType.SpecialPlayer => "Special",
                PlayerType.Streamer => "Streamer",
                _ => "Corpse"
            };
        }

        /// <summary>
        /// Gets the full display name for the corpse.
        /// </summary>
        private string GetCorpseDisplayName()
        {
            if (Player is null)
                return "Corpse";

            var typeLabel = GetTypeLabel();
            var playerName = Player.Name;

            // For AI with generic names, just show the type
            if (Player.IsAI && (string.IsNullOrWhiteSpace(playerName) || 
                playerName == "AI" || playerName == "defaultAI" ||
                playerName == typeLabel))
            {
                return typeLabel;
            }

            // For PMCs and named entities, show Type:Name
            return $"{typeLabel}:{playerName}";
        }

        /// <summary>
        /// Gets important/wishlisted items from the corpse's equipment.
        /// Returns info about the most important item including its type for proper coloring.
        /// </summary>
        public CorpseImportantItem GetImportantItem()
        {
            if (Player is not ObservedPlayer obs || obs.Equipment?.Items is null)
                return new CorpseImportantItem { Type = CorpseImportantItemType.None };

            // Look for wishlisted items first (highest priority)
            foreach (var item in obs.Equipment.Items.Values)
            {
                if (LocalPlayer.WishlistItems.Contains(item.BsgId))
                {
                    return new CorpseImportantItem
                    {
                        Label = $"!! {item.ShortName}",
                        Type = CorpseImportantItemType.Wishlist,
                        CustomFilterColor = null
                    };
                }
            }

            // Then look for important items from loot filters
            foreach (var item in obs.Equipment.Items.Values)
            {
                if (item.Important)
                {
                    return new CorpseImportantItem
                    {
                        Label = $"!!! {item.ShortName}",
                        Type = CorpseImportantItemType.ImportantFilter,
                        CustomFilterColor = item.CustomFilter?.Color
                    };
                }
            }

            return new CorpseImportantItem { Type = CorpseImportantItemType.None };
        }

        /// <summary>
        /// Gets important/wishlisted items from the corpse's equipment.
        /// Returns the short name of the most important item, or null if none.
        /// </summary>
        public string GetImportantItemLabel()
        {
            var item = GetImportantItem();
            return item.HasItem ? item.Label : null;
        }

        /// <summary>
        /// Gets all important/wishlisted items from the corpse's equipment with their types.
        /// </summary>
        public IEnumerable<CorpseImportantItem> GetAllImportantItems()
        {
            if (Player is not ObservedPlayer obs || obs.Equipment?.Items is null)
                yield break;

            foreach (var item in obs.Equipment.Items.Values)
            {
                if (LocalPlayer.WishlistItems.Contains(item.BsgId))
                {
                    yield return new CorpseImportantItem
                    {
                        Label = $"!! {item.ShortName}",
                        Type = CorpseImportantItemType.Wishlist,
                        CustomFilterColor = null
                    };
                }
                else if (item.Important)
                {
                    yield return new CorpseImportantItem
                    {
                        Label = $"!!! {item.ShortName}",
                        Type = CorpseImportantItemType.ImportantFilter,
                        CustomFilterColor = item.CustomFilter?.Color
                    };
                }
            }
        }

        /// <summary>
        /// Gets all important/wishlisted items from the corpse's equipment.
        /// </summary>
        public IEnumerable<string> GetAllImportantItemLabels()
        {
            return GetAllImportantItems().Select(i => i.Label);
        }

        /// <summary>
        /// Check if this corpse has any important/wishlisted items.
        /// Only returns true for AI corpses (not PMC/human corpses).
        /// </summary>
        public bool HasImportantItems => IsAICorpse && GetAllImportantItems().Any();

        /// <summary>
        /// True if this corpse belonged to an AI (not a human player).
        /// </summary>
        private bool IsAICorpse => Player?.IsAI ?? false;

        public override void Draw(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            var heightDiff = Position.Y - localPlayer.Position.Y;
            var point = Position.ToMapPos(mapParams.Map).ToZoomedPos(mapParams);
            MouseoverPosition = new Vector2(point.X, point.Y);
            SKPaints.ShapeOutline.StrokeWidth = 2f;
            
            // Get the appropriate paint based on player type
            var (shapePaint, textPaint) = GetCorpsePaints();
            
            if (heightDiff > 1.45) // loot is above player
            {
                using var path = point.GetUpArrow(5);
                canvas.DrawPath(path, SKPaints.ShapeOutline);
                canvas.DrawPath(path, shapePaint);
            }
            else if (heightDiff < -1.45) // loot is below player
            {
                using var path = point.GetDownArrow(5);
                canvas.DrawPath(path, SKPaints.ShapeOutline);
                canvas.DrawPath(path, shapePaint);
            }
            else // loot is level with player
            {
                var size = 5 * App.Config.UI.UIScale;
                canvas.DrawCircle(point, size, SKPaints.ShapeOutline);
                canvas.DrawCircle(point, size, shapePaint);
            }

            var textPoint = new SKPoint(point.X + 7 * App.Config.UI.UIScale, point.Y + 3 * App.Config.UI.UIScale);

            // Draw ALL important items ABOVE the name (only for AI corpses, not PMC)
            if (IsAICorpse)
            {
                var importantItems = GetAllImportantItems().ToList();
                if (importantItems.Count > 0)
                {
                    // Stack items from bottom to top (so first item is closest to name)
                    float itemOffset = importantItems.Count * SKFonts.UIRegular.Spacing;
                    var importantPoint = new SKPoint(textPoint.X, textPoint.Y - itemOffset);
                    
                    foreach (var importantItem in importantItems)
                    {
                        // Get the appropriate paint based on item type
                        var importantTextPaint = GetImportantItemPaint(importantItem);
                        
                        canvas.DrawText(
                            importantItem.Label,
                            importantPoint,
                            SKTextAlign.Left,
                            SKFonts.UIRegular,
                            SKPaints.TextOutline);
                        canvas.DrawText(
                            importantItem.Label,
                            importantPoint,
                            SKTextAlign.Left,
                            SKFonts.UIRegular,
                            importantTextPaint);
                        
                        importantPoint.Offset(0, SKFonts.UIRegular.Spacing);
                    }
                }
            }

            // Draw corpse name
            canvas.DrawText(
                Name,
                textPoint,
                SKTextAlign.Left,
                SKFonts.UIRegular,
                SKPaints.TextOutline); // Draw outline
            canvas.DrawText(
                Name,
                textPoint,
                SKTextAlign.Left,
                SKFonts.UIRegular,
                textPaint);
        }

        /// <summary>
        /// Gets the appropriate paint for an important item based on its type.
        /// Wishlist items use WishlistLoot color, filter items use their custom color or ValuableLoot color.
        /// </summary>
        private static SKPaint GetImportantItemPaint(CorpseImportantItem item)
        {
            switch (item.Type)
            {
                case CorpseImportantItemType.Wishlist:
                    // Wishlist items use the WishlistLoot color (configurable via Color Picker)
                    return SKPaints.TextWishlistItem;
                
                case CorpseImportantItemType.ImportantFilter:
                    // Filter items use their custom color if set, otherwise ValuableLoot color
                    if (!string.IsNullOrEmpty(item.CustomFilterColor) && 
                        SKColor.TryParse(item.CustomFilterColor, out var filterColor))
                    {
                        // Use cached paint to avoid GC pressure
                        return _filterPaintCache.GetOrAdd(item.CustomFilterColor, _ => new SKPaint
                        {
                            Color = filterColor,
                            IsStroke = false,
                            IsAntialias = true
                        });
                    }
                    // Fallback to ImportantLoot (ValuableLoot) color
                    return SKPaints.TextImportantLoot;
                
                default:
                    return SKPaints.TextCorpse;
            }
        }

        /// <summary>
        /// Gets the appropriate paints based on the corpse's player type.
        /// </summary>
        private (SKPaint shape, SKPaint text) GetCorpsePaints()
        {
            if (Player is null)
                return (SKPaints.PaintCorpse, SKPaints.TextCorpse);

            return Player.Type switch
            {
                PlayerType.PMC => (SKPaints.PaintPMC, SKPaints.TextPMC),
                PlayerType.Teammate => (SKPaints.PaintTeammate, SKPaints.TextTeammate),
                PlayerType.AIBoss => (SKPaints.PaintBoss, SKPaints.TextBoss),
                PlayerType.AIRaider => (SKPaints.PaintRaider, SKPaints.TextRaider),
                PlayerType.AIScav => (SKPaints.PaintScav, SKPaints.TextScav),
                PlayerType.PScav => (SKPaints.PaintPScav, SKPaints.TextPScav),
                PlayerType.SpecialPlayer => (SKPaints.PaintWatchlist, SKPaints.TextWatchlist),
                PlayerType.Streamer => (SKPaints.PaintStreamer, SKPaints.TextStreamer),
                _ => (SKPaints.PaintCorpse, SKPaints.TextCorpse)
            };
        }

        public override void DrawMouseover(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            using var lines = new PooledList<string>();
            if (Player is AbstractPlayer player)
            {
                var name = App.Config.UI.HideNames && player.IsHuman ? "<Hidden>" : player.Name;
                lines.Add($"{player.Type}:{name}");
                if (player.GroupID != -1)
                    lines.Add($"G:{player.GroupID} ");
                if (Player is ObservedPlayer obs)
                {
                    // Show important items first with type indication
                    foreach (var importantItem in GetAllImportantItems())
                    {
                        var typeHint = importantItem.Type == CorpseImportantItemType.Wishlist 
                            ? "(Wishlist)" 
                            : "(Filter)";
                        lines.Add($"{importantItem.Label} {typeHint}");
                    }
                    
                    lines.Add($"Value: {Utilities.FormatNumberKM(obs.Equipment.Value)}");
                    foreach (var item in obs.Equipment.Items.OrderBy(e => e.Key))
                        lines.Add($"{item.Key.Substring(0, 5)}: {item.Value.ShortName}");
                }
            }
            else
            {
                lines.Add(Name);
            }
            Position.ToMapPos(mapParams.Map).ToZoomedPos(mapParams).DrawMouseoverText(canvas, lines.Span);
        }
    }
}
