using LoneEftDmaRadar.Misc;
using LoneEftDmaRadar.Tarkov.GameWorld;
using LoneEftDmaRadar.Tarkov.GameWorld.Exits;
using LoneEftDmaRadar.Tarkov.GameWorld.Explosives;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Windows.Input;
using System.Windows.Threading;
using LoneEftDmaRadar.UI.Skia;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.DMA;
using SharpDX;
using SharpDX.Mathematics.Interop;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Forms.Integration;
using WinForms = System.Windows.Forms;
using SkiaSharp;
using DxColor = SharpDX.Mathematics.Interop.RawColorBGRA;
using CameraManagerNew = LoneEftDmaRadar.Tarkov.GameWorld.Camera.CameraManager;

namespace LoneEftDmaRadar.UI.ESP
{
    public partial class ESPWindow : Window
    {
        #region Fields/Properties

        public static bool ShowESP { get; set; } = true;
        private bool _dxInitFailed;

        private readonly System.Diagnostics.Stopwatch _fpsSw = new();
        private int _fpsCounter;
        private int _fps;
        private long _lastFrameTicks;
        private Timer _highFrequencyTimer;
        private int _renderPending;

        // Render surface
        private Dx9OverlayControl _dxOverlay;
        private WindowsFormsHost _dxHost;
        private bool _isClosing;

        // Cached Fonts/Paints
        private readonly SKPaint _skeletonPaint;
        private readonly SKPaint _boxPaint;
        private readonly SKPaint _crosshairPaint;
        private static readonly SKColor[] _espGroupPalette = new SKColor[]
        {
            SKColors.MediumSlateBlue,
            SKColors.MediumSpringGreen,
            SKColors.CadetBlue,
            SKColors.MediumOrchid,
            SKColors.PaleVioletRed,
            SKColors.SteelBlue,
            SKColors.DarkSeaGreen,
            SKColors.Chocolate
        };
        private static readonly ConcurrentDictionary<int, SKPaint> _espGroupPaints = new();

        private bool _isFullscreen;

        // Notification system
        private string _notificationMessage = string.Empty;
        private System.Diagnostics.Stopwatch _notificationTimer = new();
        private const int NOTIFICATION_DURATION_MS = 2000; // 2 seconds

        /// <summary>
        /// LocalPlayer (who is running Radar) 'Player' object.
        /// </summary>
        private static LocalPlayer LocalPlayer => Memory.LocalPlayer;

        /// <summary>
        /// All Players in Local Game World (including dead/exfil'd) 'Player' collection.
        /// </summary>
        private static IReadOnlyCollection<AbstractPlayer> AllPlayers => Memory.Players;

        private static IReadOnlyCollection<IExitPoint> Exits => Memory.Exits;

        private static IReadOnlyCollection<IExplosiveItem> Explosives => Memory.Explosives;

        private static bool InRaid => Memory.InRaid;

        // Bone Connections for Skeleton
        private static readonly (Bones From, Bones To)[] _boneConnections = new[]
        {
            (Bones.HumanHead, Bones.HumanNeck),
            (Bones.HumanNeck, Bones.HumanSpine3),
            (Bones.HumanSpine3, Bones.HumanSpine2),
            (Bones.HumanSpine2, Bones.HumanSpine1),
            (Bones.HumanSpine1, Bones.HumanPelvis),
            
            // Left Arm
            (Bones.HumanNeck, Bones.HumanLUpperarm), // Shoulder approx
            (Bones.HumanLUpperarm, Bones.HumanLForearm1),
            (Bones.HumanLForearm1, Bones.HumanLForearm2),
            (Bones.HumanLForearm2, Bones.HumanLPalm),
            
            // Right Arm
            (Bones.HumanNeck, Bones.HumanRUpperarm), // Shoulder approx
            (Bones.HumanRUpperarm, Bones.HumanRForearm1),
            (Bones.HumanRForearm1, Bones.HumanRForearm2),
            (Bones.HumanRForearm2, Bones.HumanRPalm),
            
            // Left Leg
            (Bones.HumanPelvis, Bones.HumanLThigh1),
            (Bones.HumanLThigh1, Bones.HumanLThigh2),
            (Bones.HumanLThigh2, Bones.HumanLCalf),
            (Bones.HumanLCalf, Bones.HumanLFoot),
            
            // Right Leg
            (Bones.HumanPelvis, Bones.HumanRThigh1),
            (Bones.HumanRThigh1, Bones.HumanRThigh2),
            (Bones.HumanRThigh2, Bones.HumanRCalf),
            (Bones.HumanRCalf, Bones.HumanRFoot),
        };

        #endregion

        public ESPWindow()
        {
            InitializeComponent();
            InitializeRenderSurface();
            
            // Initial sizes
            this.Width = 400;
            this.Height = 300;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Cache paints/fonts
            _skeletonPaint = new SKPaint
            {
                Color = SKColors.White,
                StrokeWidth = 1.5f,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            _boxPaint = new SKPaint
            {
                Color = SKColors.White,
                StrokeWidth = 1.0f,
                IsAntialias = false, // Crisper boxes
                Style = SKPaintStyle.Stroke
            };

            _crosshairPaint = new SKPaint
            {
                Color = SKColors.White,
                StrokeWidth = 1.5f,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            _fpsSw.Start();
            _lastFrameTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            _highFrequencyTimer = new System.Threading.Timer(
                callback: HighFrequencyRenderCallback,
                state: null,
                dueTime: 0,
                period: 4); // 4ms = ~250 FPS max capability, actual FPS controlled by EspMaxFPS setting
        }

        private void InitializeRenderSurface()
        {
            RenderRoot.Children.Clear();

            _dxOverlay = new Dx9OverlayControl
            {
                Dock = WinForms.DockStyle.Fill
            };

            ApplyDxFontConfig();
            _dxOverlay.RenderFrame = RenderSurface;
            _dxOverlay.DeviceInitFailed += Overlay_DeviceInitFailed;
            _dxOverlay.MouseDown += GlControl_MouseDown;
            _dxOverlay.DoubleClick += GlControl_DoubleClick;
            _dxOverlay.KeyDown += GlControl_KeyDown;

            _dxHost = new WindowsFormsHost
            {
                Child = _dxOverlay
            };

            RenderRoot.Children.Add(_dxHost);
        }

        private void HighFrequencyRenderCallback(object state)
        {
            try
            {
                if (_isClosing || _dxOverlay == null)
                    return;

                int maxFPS = App.Config.UI.EspMaxFPS;
                long currentTicks = System.Diagnostics.Stopwatch.GetTimestamp();

                // FPS limiting: Skip frame if not enough time has elapsed
                if (maxFPS > 0)
                {
                    double elapsedMs = (currentTicks - _lastFrameTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    double targetMs = 1000.0 / maxFPS;
                    if (elapsedMs < targetMs)
                        return; // Skip this frame to maintain FPS cap
                }

                _lastFrameTicks = currentTicks;

                // Render DirectX on dedicated timer thread (DirectX 9 is thread-safe)
                // This removes WPF Dispatcher bottleneck - ESP no longer competes with Radar for UI thread
                if (System.Threading.Interlocked.CompareExchange(ref _renderPending, 1, 0) == 0)
                {
                    try
                    {
                        _dxOverlay.Render(); // DirectX render happens on timer thread
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref _renderPending, 0);
                    }
                }
            }
            catch { /* Ignore errors during shutdown */ }
        }

        #region Rendering Methods

        /// <summary>
        /// Record the Rendering FPS.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SetFPS()
        {
            if (_fpsSw.ElapsedMilliseconds >= 1000)
            {
                _fps = System.Threading.Interlocked.Exchange(ref _fpsCounter, 0);
                _fpsSw.Restart();
            }
            else
            {
                _fpsCounter++;
            }
        }

        private bool _lastInRaidState = false;

        /// <summary>
        /// Main ESP Render Event.
        /// </summary>
        private void RenderSurface(Dx9RenderContext ctx)
        {
            if (_dxInitFailed)
                return;

            float screenWidth = ctx.Width;
            float screenHeight = ctx.Height;

            SetFPS();

            // Clear with black background (transparent for fuser)
            ctx.Clear(new DxColor(0, 0, 0, 255));

            try
            {
                // Detect raid state changes and reset camera/state when leaving raid
                if (_lastInRaidState && !InRaid)
                {
                    CameraManagerNew.Reset();
                    DebugLogger.LogInfo("ESP: Detected raid end - reset all state");
                }
                _lastInRaidState = InRaid;

                if (!InRaid)
                    return;

                var localPlayer = LocalPlayer;
                var allPlayers = AllPlayers;
                
                if (localPlayer is not null && allPlayers is not null)
                {
                    if (!ShowESP)
                    {
                        DrawNotShown(ctx, screenWidth, screenHeight);
                    }
                    else
                    {
                        ApplyResolutionOverrideIfNeeded();

                        // Render Loot (background layer)
                        // ESP Loot is independent from Radar's "Show Loot" setting
                        // Draw loot if ESP is enabled AND any ESP loot category is enabled
                        if (App.Config.UI.ShowESP && 
                            (App.Config.UI.EspLootFilterOnly ||
                            App.Config.UI.EspLoot || 
                            App.Config.UI.EspFood || 
                            App.Config.UI.EspMeds || 
                            App.Config.UI.EspBackpacks || 
                            App.Config.UI.EspQuestLoot || 
                            App.Config.UI.EspCorpses ||
                            App.Config.UI.EspShowWishlisted))
                        {
                            DrawLoot(ctx, screenWidth, screenHeight, localPlayer);
                        }

                        if (App.Config.UI.EspContainers)
                        {
                            DrawStaticContainers(ctx, screenWidth, screenHeight, localPlayer);
                        }

                        // Render Exfils - ALWAYS rendered regardless of distance settings
                        // They are important navigation points and should never be hidden by distance sliders
                        if (Exits is not null && App.Config.UI.EspExfils)
                        {
                            foreach (var exit in Exits)
                            {
                                if (exit is Exfil exfil)
                                {
                                     if (WorldToScreen2WithScale(exfil.Position, out var screen, out float scale, screenWidth, screenHeight))
                                     {
                                         float distance = Vector3.Distance(localPlayer.Position, exfil.Position);
                                         var dotColor = ToColor(SKPaints.PaintExfilOpen);
                                         var textColor = GetExfilColorForRender();

                                         // AIMVIEW SCALING: Scale radius with perspective (includes UIScale)
                                         float radius = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);
                                         ctx.DrawCircle(ToRaw(screen), radius, dotColor, true);
                                         
                                         // AIMVIEW SCALING: Scale text with perspective
                                         DxTextSize textSize = scale > 1.5f ? DxTextSize.Medium : DxTextSize.Small;
                                         ctx.DrawText($"{exfil.Name} D:{distance:F0}m", screen.X + radius + 3, screen.Y + 4, textColor, textSize);
                                     }
                                }
                            }
                        }

                        // Render Tripwires - ALWAYS check if enabled
                        if (Explosives is not null && App.Config.UI.EspTripwires)
                        {
                            DrawTripwires(ctx, screenWidth, screenHeight, localPlayer);
                        }

                        // Render Grenades - ALWAYS check if enabled
                        if (Explosives is not null && App.Config.UI.EspGrenades)
                        {
                            DrawGrenades(ctx, screenWidth, screenHeight, localPlayer);
                        }

                        // Render players
                        foreach (var player in allPlayers)
                        {
                            DrawPlayerESP(ctx, player, localPlayer, screenWidth, screenHeight);
                        }

                        DrawDeviceAimbotTargetLine(ctx, screenWidth, screenHeight);

                        if (App.Config.Device.Enabled)
                        {
                            DrawDeviceAimbotFovCircle(ctx, screenWidth, screenHeight);
                        }

                        if (App.Config.UI.EspCrosshair)
                        {
                            DrawCrosshair(ctx, screenWidth, screenHeight);
                        }

                        DrawDeviceAimbotDebugOverlay(ctx, screenWidth, screenHeight);
                        DrawFPS(ctx, screenWidth, screenHeight);
                        DrawNotification(ctx, screenWidth, screenHeight);
                        DrawNearestPlayerInfo(ctx, screenWidth, screenHeight, localPlayer, allPlayers);
                        if (App.Config.UI.EspLootDebug)
                        {
                            DrawLootDebugOverlay(ctx, screenWidth, screenHeight, localPlayer);
                        }
                        if (App.Config.UI.EspLootDebug)
                        {
                            DrawLootDebugOverlay(ctx, screenWidth, screenHeight, localPlayer);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.LogDebug($"ESP RENDER ERROR: {ex}");
            }
        }

        private void DrawLoot(Dx9RenderContext ctx, float screenWidth, float screenHeight, LocalPlayer localPlayer)
        {
            if (!ShowESP || !App.Config.UI.ShowESP)
                return;

            // AllLoot from Memory.Game.Loot (all items from game memory, not filtered by Radar)
            var lootItems = Memory.Game?.Loot?.AllLoot;
            if (lootItems is null) return;

            // Position from LocalPlayer.Position
            var camPos = localPlayer?.Position ?? Vector3.Zero;

            foreach (var item in lootItems)
            {
                // Containers drawn separately in DrawStaticContainers()
                if (item is StaticLootContainer or LootAirdrop)
                    continue;

                // Type checks from LootItem properties
                bool isCorpse = item is LootCorpse;
                bool isQuest = item.IsQuestItem; // From LootItem.IsQuestItem
                bool isFood = item.IsFood; // From LootItem.IsFood
                bool isMeds = item.IsMeds; // From LootItem.IsMeds
                bool isBackpack = item.IsBackpack; // From LootItem.IsBackpack
                // isInFilter: item.IsImportant (from LootItem.IsImportant) includes IsWishlisted
                // So we need to exclude wishlist: CustomFilter OR (IsImportant AND NOT IsWishlisted)
                bool isInFilter = item.CustomFilter != null || (item.IsImportant && !item.IsWishlisted);

                // Check if item should be displayed
                if (!ShouldShowLootItem(item, isCorpse, isQuest, isFood, isMeds, isBackpack, isInFilter))
                    continue;

                // Render the item
                DrawLootItem(ctx, item, camPos, screenWidth, screenHeight, isCorpse, isQuest, item.IsWishlisted, isInFilter, isBackpack, isMeds, isFood);
            }
        }

        /// <summary>
        /// Determines if a loot item should be displayed based on ESP settings and item properties.
        /// </summary>
        private bool ShouldShowLootItem(LootItem item, bool isCorpse, bool isQuest, bool isFood, bool isMeds, bool isBackpack, bool isInFilter)
        {
            // Priority order: Quest > Corpse > Wishlist > Filter > Meds > Food > Regular Loot > Backpack
            // Wishlist has higher priority than Filter items
            
            // Always check Quest, Corpse, and Wishlist first (these take priority)
            if (ShouldShowQuestItem(isQuest) || ShouldShowCorpse(isCorpse) || ShouldShowWishlistItem(item))
                return true;

            // Check Filter items (they have higher priority than Meds/Food/Regular loot/Backpack)
            if (ShouldShowFilterItem(item, isInFilter))
                return true;

            // Check all other categories regardless of EspLootFilterOnly setting
            // EspLootFilterOnly only controls filter items display, not other categories
            return ShouldShowMedsItem(item, isMeds, isInFilter) ||
                   ShouldShowFoodItem(item, isFood, isInFilter) ||
                   ShouldShowRegularLootItem(item, isInFilter) ||
                   ShouldShowBackpackItem(item, isBackpack, isInFilter);
        }

        /// <summary>
        /// Checks if quest items should be displayed.
        /// </summary>
        private bool ShouldShowQuestItem(bool isQuest)
        {
            return isQuest && App.Config.UI.EspQuestLoot;
        }

        /// <summary>
        /// Checks if corpses should be displayed.
        /// </summary>
        private bool ShouldShowCorpse(bool isCorpse)
        {
            return isCorpse && App.Config.UI.EspCorpses;
        }

        /// <summary>
        /// Checks if wishlist items should be displayed.
        /// </summary>
        private bool ShouldShowWishlistItem(LootItem item)
        {
            return App.Config.UI.EspShowWishlisted && item.IsWishlisted;
        }

        /// <summary>
        /// Checks if filter items should be displayed.
        /// Filter items are only shown when EspLootFilterOnly is ON.
        /// When EspLootFilterOnly is OFF, filter items are not displayed.
        /// Note: Wishlist items are excluded here to give ShouldShowWishlistItem higher priority.
        /// </summary>
        private bool ShouldShowFilterItem(LootItem item, bool isInFilter)
        {
            // Don't show filter items if they are wishlist items (wishlist has higher priority)
            if (item.IsWishlisted)
                return false;

            if (!isInFilter)
                return false;

            // Only show filter items when EspLootFilterOnly is ON
            return App.Config.UI.EspLootFilterOnly;
        }

        /// <summary>
        /// Checks if meds items should be displayed.
        /// Meds items in filter are excluded here to give ShouldShowFilterItem higher priority.
        /// </summary>
        private bool ShouldShowMedsItem(LootItem item, bool isMeds, bool isInFilter)
        {
            // Don't show meds if they are in filter (filter has higher priority)
            if (isInFilter)
                return false;

            return isMeds && App.Config.UI.EspMeds;
        }

        /// <summary>
        /// Checks if food items should be displayed.
        /// Food items in filter are excluded here to give ShouldShowFilterItem higher priority.
        /// </summary>
        private bool ShouldShowFoodItem(LootItem item, bool isFood, bool isInFilter)
        {
            // Don't show food if they are in filter (filter has higher priority)
            if (isInFilter)
                return false;

            return isFood && App.Config.UI.EspFood;
        }

        /// <summary>
        /// Checks if regular loot items should be displayed.
        /// Regular loot excludes filter and wishlist items.
        /// </summary>
        private bool ShouldShowRegularLootItem(LootItem item, bool isInFilter)
        {
            if (!App.Config.UI.EspLoot)
                return false;

            // Exclude filter and wishlist items from regular loot
            if (isInFilter || item.IsWishlisted)
                return false;

            // Show regular or valuable loot
            return item.IsRegularLoot || item.IsValuableLoot;
        }

        /// <summary>
        /// Checks if backpack items should be displayed.
        /// Backpack items in filter are excluded here to give ShouldShowFilterItem higher priority.
        /// </summary>
        private bool ShouldShowBackpackItem(LootItem item, bool isBackpack, bool isInFilter)
        {
            // Don't show backpacks if they are in filter (filter has higher priority)
            if (isInFilter)
                return false;

            return isBackpack && App.Config.UI.EspBackpacks;
        }

        /// <summary>
        /// Gets the color for a loot item based on its type and properties.
        /// </summary>
        private DxColor GetLootItemColor(LootItem item, bool isQuest, bool isWishlisted, bool isInFilter, bool isBackpack, bool isMeds, bool isFood, bool isCorpse, bool isValuableLoot)
        {
            if (isQuest)
                return ToColor(SKPaints.PaintQuestItem);
            else if (isWishlisted)
                return ToColor(SKPaints.PaintWishlistItem);
            else if (isInFilter && SKColor.TryParse(item.CustomFilter?.Color ?? "", out var skFilterColor))
                return ToColor(skFilterColor);
            else if (isInFilter && item.IsImportant)
                return ToColor(SKPaints.PaintImportantLoot);
            else if (isBackpack)
                return ToColor(SKPaints.PaintBackpacks);
            else if (isMeds)
                return ToColor(SKPaints.PaintMeds);
            else if (isFood)
                return ToColor(SKPaints.PaintFood);
            else if (isValuableLoot)
                return ToColor(SKPaints.PaintImportantLoot);
            else if (isCorpse)
                return ToColor(SKPaints.PaintCorpse);
            else
                return GetLootColorForRender(); // Uses App.Config.UI.EspColorLoot
        }

        /// <summary>
        /// Gets the ESP loot item label, respecting EspLootPrice setting.
        /// </summary>
        private string GetESPLootItemLabel(LootItem item)
        {
            var label = "";
            
            // Wishlist items always show "!!" prefix
            if (item.IsWishlisted)
            {
                label += "!!";
            }
            // Show price if EspLootPrice is enabled and item has a price
            else if (App.Config.UI.EspLootPrice && item.Price > 0)
            {
                label += $"[{LoneEftDmaRadar.Misc.Utilities.FormatNumberKM(item.Price)}] ";
            }
            
            label += item.ShortName;
            
            if (string.IsNullOrEmpty(label))
                label = "Item";
                
            return label;
        }

        /// <summary>
        /// Checks if a screen position is within the ESP loot cone filter.
        /// </summary>
        private bool IsInConeFilter(SKPoint screen, float screenWidth, float screenHeight)
        {
            if (!App.Config.UI.EspLootConeEnabled || App.Config.UI.EspLootConeAngle <= 0f)
                return true;

            float centerX = screenWidth / 2f;
            float centerY = screenHeight / 2f;
            float dx = screen.X - centerX;
            float dy = screen.Y - centerY;
            float fov = App.Config.UI.FOV;
            float screenAngleX = MathF.Abs(dx / centerX) * (fov / 2f);
            float screenAngleY = MathF.Abs(dy / centerY) * (fov / 2f);
            float screenAngle = MathF.Sqrt(screenAngleX * screenAngleX + screenAngleY * screenAngleY);
            return screenAngle <= App.Config.UI.EspLootConeAngle;
        }

        /// <summary>
        /// Renders a single loot item on the ESP overlay.
        /// </summary>
        private void DrawLootItem(Dx9RenderContext ctx, LootItem item, Vector3 camPos, float screenWidth, float screenHeight, bool isCorpse, bool isQuest, bool isWishlisted, bool isInFilter, bool isBackpack, bool isMeds, bool isFood)
        {
            // Distance: camPos (LocalPlayer.Position) to item.Position (from LootItem)
            float distance = Vector3.Distance(camPos, item.Position);
            float maxRenderDistance = App.Config.UI.EspLootMaxDistance;
            bool unlimitedDistance = maxRenderDistance <= 0f;
            if (!unlimitedDistance && distance > maxRenderDistance)
                return;

            // WorldToScreen2WithScale: converts item.Position (Vector3) to screen coordinates
            if (!WorldToScreen2WithScale(item.Position, out var screen, out float scale, screenWidth, screenHeight))
                return;

            // Cone filter: EspLootConeEnabled, EspLootConeAngle, FOV from App.Config.UI
            bool inCone = IsInConeFilter(screen, screenWidth, screenHeight);

            // Color selection: isQuest, item.IsWishlisted, isInFilter from above
            // item.CustomFilter.Color from LootItem.CustomFilter
            // item.IsImportant, isBackpack, isMeds, isFood, item.IsValuableLoot, isCorpse from above
            DxColor circleColor = GetLootItemColor(item, isQuest, isWishlisted, isInFilter, isBackpack, isMeds, isFood, isCorpse, item.IsValuableLoot);
            DxColor textColor = circleColor;

            // Draw circle: UIScale from App.Config.UI, scale from WorldToScreen2WithScale
            float radius = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);
            ctx.DrawCircle(ToRaw(screen), radius, circleColor, true);

            // Draw text: item.IsWishlisted from LootItem, inCone from above
            // isCorpse, corpse.Player.Name from LootCorpse, item.GetUILabel() from LootItem
            // distance from Vector3.Distance above
            if (item.IsWishlisted || inCone)
            {
                string text;
                if (isCorpse && item is LootCorpse corpse && !string.IsNullOrWhiteSpace(corpse.Player?.Name))
                {
                    text = corpse.Player.Name;
                }
                else
                {
                    // Build label with price if EspLootPrice is enabled
                    text = GetESPLootItemLabel(item);
                }
                text = $"{text} D:{distance:F0}m";

                // textSize based on scale from WorldToScreen2WithScale
                DxTextSize textSize = scale > 1.5f ? DxTextSize.Medium : DxTextSize.Small;
                ctx.DrawText(text, screen.X + radius + 4, screen.Y + 4, textColor, textSize);
            }
        }

        private void DrawStaticContainers(Dx9RenderContext ctx, float screenWidth, float screenHeight, LocalPlayer localPlayer)
        {
            // ESP uses its own separate checkbox (App.Config.UI.EspContainers)
            if (!App.Config.UI.EspContainers)
                return;

            var containers = Memory.Game?.Loot?.AllLoot?.OfType<StaticLootContainer>();
            if (containers is null)
                return;

            bool selectAll = App.Config.Containers.SelectAll;
            var selected = App.Config.Containers.Selected;
            bool hideSearched = App.Config.Containers.HideSearched;
            float maxRenderDistance = App.Config.Containers.EspDrawDistance;
            bool unlimitedDistance = maxRenderDistance <= 0f; // 0 = unlimited
            var color = GetContainerColorForRender();

            foreach (var container in containers)
            {
                var id = container.ID ?? "UNKNOWN";
                if (!selectAll && !selected.ContainsKey(id))
                    continue;

                // Hide searched containers if enabled
                if (hideSearched && container.Searched)
                    continue;

                float distance = Vector3.Distance(localPlayer.Position, container.Position);
                if (!unlimitedDistance && distance > maxRenderDistance)
                    continue;

                // Use WorldToScreen2WithScale (same as loot) for perspective-based scaling
                if (!WorldToScreen2WithScale(container.Position, out var screen, out float scale, screenWidth, screenHeight))
                    continue;

                // AIMVIEW SCALING: Scale radius with perspective (includes UIScale)
                float radius = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);
                ctx.DrawCircle(ToRaw(screen), radius, color, true);
                
                // AIMVIEW SCALING: Scale text with perspective
                DxTextSize textSize = scale > 1.5f ? DxTextSize.Medium : DxTextSize.Small;
                ctx.DrawText(container.Name ?? "Container", screen.X + radius + 4, screen.Y + 4, color, textSize);
            }
        }

        private void DrawTripwires(Dx9RenderContext ctx, float screenWidth, float screenHeight, LocalPlayer localPlayer)
        {
            if (Explosives is null)
                return;

            // Active tripwires are ALWAYS rendered regardless of distance settings
            // They are safety-critical and should never be hidden by distance sliders

            foreach (var explosive in Explosives)
            {
                if (explosive is null || explosive is not Tripwire tripwire || !tripwire.IsActive)
                    continue;

                try
                {
                    if (tripwire.Position == Vector3.Zero)
                        continue;

                    if (!WorldToScreen2WithScale(tripwire.Position, out var screen, out float scale, screenWidth, screenHeight))
                        continue;

                    float distance = Vector3.Distance(localPlayer.Position, tripwire.Position);

                    // AIMVIEW SCALING: Scale radius with perspective (includes UIScale)
                    float radius = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);
                    ctx.DrawCircle(ToRaw(screen), radius, GetTripwireColorForRender(), true);

                    // AIMVIEW SCALING: Scale text with perspective
                    DxTextSize textSize = scale > 1.5f ? DxTextSize.Medium : DxTextSize.Small;
                    ctx.DrawText($"Tripwire D:{distance:F0}m", screen.X + radius + 4, screen.Y, GetTripwireColorForRender(), textSize);
                }
                catch
                {
                    continue;
                }
            }
        }

        private void DrawGrenades(Dx9RenderContext ctx, float screenWidth, float screenHeight, LocalPlayer localPlayer)
        {
            if (Explosives is null)
                return;

            // Active grenades are ALWAYS rendered regardless of distance settings
            // They are safety-critical and should never be hidden by distance sliders

            foreach (var explosive in Explosives)
            {
                if (explosive is null || explosive is not Grenade grenade)
                    continue;

                try
                {
                    if (grenade.Position == Vector3.Zero)
                        return;

                    if (!WorldToScreen2WithScale(grenade.Position, out var screen, out float scale, screenWidth, screenHeight))
                        return;

                    float distance = Vector3.Distance(localPlayer.Position, grenade.Position);

                    // AIMVIEW SCALING: Scale radius with perspective (includes UIScale)
                    float radius = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);
                    ctx.DrawCircle(ToRaw(screen), radius, GetGrenadeColorForRender(), true);

                    // AIMVIEW SCALING: Scale text with perspective
                    DxTextSize textSize = scale > 1.5f ? DxTextSize.Medium : DxTextSize.Small;
                    ctx.DrawText($"Grenade D:{distance:F0}m", screen.X + radius + 4, screen.Y, GetGrenadeColorForRender(), textSize);
                }
                catch
                {
                    continue;
                }
            }
        }

        /// <summary>
        /// Renders player on ESP
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void DrawPlayerESP(Dx9RenderContext ctx, AbstractPlayer player, LocalPlayer localPlayer, float screenWidth, float screenHeight)
        {
            if (player is null || player == localPlayer || !player.IsAlive || !player.IsActive)
                return;

            // Validate player position is valid (not zero or NaN/Infinity)
            var playerPos = player.Position;
            if (playerPos == Vector3.Zero || 
                float.IsNaN(playerPos.X) || float.IsNaN(playerPos.Y) || float.IsNaN(playerPos.Z) ||
                float.IsInfinity(playerPos.X) || float.IsInfinity(playerPos.Y) || float.IsInfinity(playerPos.Z))
                return;

            // Check if this is AI or player
            bool isAI = player.Type is PlayerType.AIScav or PlayerType.AIRaider or PlayerType.AIBoss or PlayerType.PScav;

            // Optimization: Skip players/AI that are too far before W2S
            float distance = Vector3.Distance(localPlayer.Position, player.Position);
            float maxDistance = isAI ? App.Config.UI.EspAIMaxDistance : App.Config.UI.EspPlayerMaxDistance;

            // If maxDistance is 0, it means unlimited, otherwise check distance
            if (maxDistance > 0 && distance > maxDistance)
                return;

            // Fallback to old MaxDistance if the new settings aren't configured
            if (maxDistance == 0 && distance > App.Config.UI.MaxDistance)
                return;

            // Get Color
            var color = GetPlayerColorForRender(player);
            bool isDeviceAimbotLocked = MemDMA.DeviceAimbot?.LockedTarget == player;
            if (isDeviceAimbotLocked)
            {
                color = ToColor(new SKColor(0, 200, 255, 220));
            }

            bool drawSkeleton = isAI ? App.Config.UI.EspAISkeletons : App.Config.UI.EspPlayerSkeletons;
            bool drawBox = isAI ? App.Config.UI.EspAIBoxes : App.Config.UI.EspPlayerBoxes;
            bool drawName = isAI ? App.Config.UI.EspAINames : App.Config.UI.EspPlayerNames;
            bool drawHealth = isAI ? App.Config.UI.EspAIHealth : App.Config.UI.EspPlayerHealth;
            bool drawDistance = isAI ? App.Config.UI.EspAIDistance : App.Config.UI.EspPlayerDistance;
            bool drawGroupId = isAI ? App.Config.UI.EspAIGroupIds : App.Config.UI.EspGroupIds;
            bool drawLabel = drawName || drawDistance || drawHealth || drawGroupId;

            // Draw Skeleton (only if not in error state to avoid frozen bones)
            if (drawSkeleton && !player.IsError)
            {
                DrawSkeleton(ctx, player, screenWidth, screenHeight, color, _skeletonPaint.StrokeWidth);
            }
            
            RectangleF bbox = default;
            bool hasBox = false;
            if (drawBox || drawLabel)
            {
                hasBox = TryGetBoundingBox(player, screenWidth, screenHeight, out bbox);
            }

            // Draw Box
            if (drawBox && hasBox)
            {
                DrawBoundingBox(ctx, bbox, color, _boxPaint.StrokeWidth);
            }

            // Draw head marker
            bool drawHeadCircle = isAI ? App.Config.UI.EspHeadCircleAI : App.Config.UI.EspHeadCirclePlayers;
            if (drawHeadCircle && player.Skeleton?.BoneTransforms != null)
            {
                // Use Skeleton.BoneTransforms directly (same as DeviceAimbot)
                if (player.Skeleton.BoneTransforms.TryGetValue(Bones.HumanHead, out var headBone))
                {
                    var head = headBone.Position;
                    if (head != Vector3.Zero && !float.IsNaN(head.X) && !float.IsNaN(head.Y) && !float.IsNaN(head.Z) && !float.IsInfinity(head.X) && !float.IsInfinity(head.Y) && !float.IsInfinity(head.Z))
                    {
                        if (TryProject(head, screenWidth, screenHeight, out var headScreen))
                        {
                            var headTop = head;
                            headTop.Y += 0.18f;

                            if (TryProject(headTop, screenWidth, screenHeight, out var headTopScreen))
                            {
                                var dy = MathF.Abs(headTopScreen.Y - headScreen.Y);
                                float radius = dy * 0.65f;
                                radius = Math.Clamp(radius, 2f, 12f);
                                
                                ctx.DrawCircle(ToRaw(headScreen), radius, color, filled: false);
                            }
                        }
                    }
                }
            }

            if (drawLabel)
            {
                DrawPlayerLabel(ctx, player, distance, color, hasBox ? bbox : (RectangleF?)null, screenWidth, screenHeight, drawName, drawDistance, drawHealth, drawGroupId);
            }
        }

        private void DrawSkeleton(Dx9RenderContext ctx, AbstractPlayer player, float w, float h, DxColor color, float thickness)
        {
            // Check if skeleton exists (same as DeviceAimbot)
            if (player.Skeleton?.BoneTransforms == null)
                return;

            // ? Calculate distance-based scale for line thickness (like Aimview)
            var localPlayer = LocalPlayer;
            if (localPlayer != null)
            {
                float distance = Vector3.Distance(localPlayer.Position, player.Position);
                float distanceScale = Math.Clamp(50f / Math.Max(distance, 5f), 0.5f, 2.5f);
                thickness *= distanceScale; // Scale thickness with distance
            }

            foreach (var (from, to) in _boneConnections)
            {
                // Use Skeleton.BoneTransforms directly (same as DeviceAimbot) for fresh positions
                if (!player.Skeleton.BoneTransforms.TryGetValue(from, out var bone1) ||
                    !player.Skeleton.BoneTransforms.TryGetValue(to, out var bone2))
                    continue;

                var p1 = bone1.Position;
                var p2 = bone2.Position;

                // Skip if either bone position is invalid (zero or NaN)
                if (p1 == Vector3.Zero || p2 == Vector3.Zero)
                    continue;

                if (TryProject(p1, w, h, out var s1) && TryProject(p2, w, h, out var s2))
                {
                    ctx.DrawLine(ToRaw(s1), ToRaw(s2), color, thickness);
                }
            }
        }

            private bool TryGetBoundingBox(AbstractPlayer player, float w, float h, out RectangleF rect)
        {
            rect = default;
            
            // Validate player position before calculating bounding box
            var playerPos = player.Position;
            if (playerPos == Vector3.Zero || 
                float.IsNaN(playerPos.X) || float.IsInfinity(playerPos.X))
                return false;
            
            var projectedPoints = new List<SKPoint>();
            var mins = new Vector3((float)-0.4, 0, (float)-0.4);
            var maxs = new Vector3((float)0.4, (float)1.75, (float)0.4);

            mins = playerPos + mins;
            maxs = playerPos + maxs;

            var points = new List<Vector3> {
                new Vector3(mins.X, mins.Y, mins.Z),
                new Vector3(mins.X, maxs.Y, mins.Z),
                new Vector3(maxs.X, maxs.Y, mins.Z),
                new Vector3(maxs.X, mins.Y, mins.Z),
                new Vector3(maxs.X, maxs.Y, maxs.Z),
                new Vector3(mins.X, maxs.Y, maxs.Z),
                new Vector3(mins.X, mins.Y, maxs.Z),
                new Vector3(maxs.X, mins.Y, maxs.Z)
            };

            foreach (var position in points)
            {
                if (TryProject(position, w, h, out var s))
                    projectedPoints.Add(s);
            }

            if (projectedPoints.Count < 2)
                return false;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var point in projectedPoints)
            {
                if (point.X < minX) minX = point.X;
                if (point.X > maxX) maxX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.Y > maxY) maxY = point.Y;
            }

            float boxWidth = maxX - minX;
            float boxHeight = maxY - minY;

            if (boxWidth < 1f || boxHeight < 1f || boxWidth > w * 2f || boxHeight > h * 2f)
                return false;

            minX = Math.Clamp(minX, -50f, w + 50f);
            maxX = Math.Clamp(maxX, -50f, w + 50f);
            minY = Math.Clamp(minY, -50f, h + 50f);
            maxY = Math.Clamp(maxY, -50f, h + 50f);

            float padding = 2f;
            rect = new RectangleF(minX - padding, minY - padding, (maxX - minX) + padding * 2f, (maxY - minY) + padding * 2f);
            return true;
        }

        private void DrawBoundingBox(Dx9RenderContext ctx, RectangleF rect, DxColor color, float thickness)
        {
            ctx.DrawRect(rect, color, thickness);
        }

        /// <summary>
        /// Determines player color based on type
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static SKPaint GetPlayerColor(AbstractPlayer player)
        {
             if (player.IsFocused)
                return SKPaints.PaintAimviewWidgetFocused;
            if (player is LocalPlayer)
                return SKPaints.PaintAimviewWidgetLocalPlayer;

            if (player.Type == PlayerType.PMC)
            {
                if (App.Config.UI.EspGroupColors && player.GroupID >= 0 && !(player is LocalPlayer))
                {
                    return _espGroupPaints.GetOrAdd(player.GroupID, id =>
                    {
                        var color = _espGroupPalette[Math.Abs(id) % _espGroupPalette.Length];
                        return new SKPaint
                        {
                            Color = color,
                            StrokeWidth = SKPaints.PaintAimviewWidgetPMC.StrokeWidth,
                            Style = SKPaints.PaintAimviewWidgetPMC.Style,
                            IsAntialias = SKPaints.PaintAimviewWidgetPMC.IsAntialias
                        };
                    });
                }

                if (App.Config.UI.EspFactionColors)
                {
                    if (player.PlayerSide == Enums.EPlayerSide.Bear)
                        return SKPaints.PaintPMCBear;
                    if (player.PlayerSide == Enums.EPlayerSide.Usec)
                        return SKPaints.PaintPMCUsec;
                }

                return SKPaints.PaintPMC;
            }

            return player.Type switch
            {
                PlayerType.Teammate => SKPaints.PaintAimviewWidgetTeammate,
                PlayerType.AIScav => SKPaints.PaintAimviewWidgetScav,
                PlayerType.AIRaider => SKPaints.PaintAimviewWidgetRaider,
                PlayerType.AIBoss => SKPaints.PaintAimviewWidgetBoss,
                PlayerType.PScav => SKPaints.PaintAimviewWidgetPScav,
                PlayerType.SpecialPlayer => SKPaints.PaintAimviewWidgetWatchlist,
                PlayerType.Streamer => SKPaints.PaintAimviewWidgetStreamer,
                _ => SKPaints.PaintAimviewWidgetPMC
            };
        }

        /// <summary>
        /// Draws player label (name/distance) relative to the bounding box or head fallback.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void DrawPlayerLabel(Dx9RenderContext ctx, AbstractPlayer player, float distance, DxColor color, RectangleF? bbox, float screenWidth, float screenHeight, bool showName, bool showDistance, bool showHealth, bool showGroup)
        {
            if (!showName && !showDistance && !showHealth && !showGroup)
                return;

            var name = showName ? player.Name ?? "Unknown" : null;
            var distanceText = showDistance ? $"{distance:F0}m" : null;

            string healthText = null;
            if (showHealth && player is ObservedPlayer observed && observed.HealthStatus is not Enums.ETagStatus.Healthy)
                healthText = observed.HealthStatus.ToString();

            string factionText = null;
            if (App.Config.UI.EspPlayerFaction && player.IsPmc)
                factionText = player.PlayerSide.ToString();

            string groupText = null;
            if (showGroup && player.GroupID != -1 && player.IsPmc && !player.IsAI)
                groupText = $"G:{player.GroupID}";

            string text = name;
            if (!string.IsNullOrWhiteSpace(healthText))
                text = string.IsNullOrWhiteSpace(text) ? healthText : $"{text} ({healthText})";
            if (!string.IsNullOrWhiteSpace(distanceText))
                text = string.IsNullOrWhiteSpace(text) ? distanceText : $"{text} ({distanceText})";
            if (!string.IsNullOrWhiteSpace(groupText))
                text = string.IsNullOrWhiteSpace(text) ? groupText : $"{text} [{groupText}]";
            if (!string.IsNullOrWhiteSpace(factionText))
                text = string.IsNullOrWhiteSpace(text) ? factionText : $"{text} [{factionText}]";

            if (string.IsNullOrWhiteSpace(text))
                return;

            float drawX;
            float drawY;

            var bounds = ctx.MeasureText(text, DxTextSize.Medium);
            int textHeight = Math.Max(1, bounds.Bottom - bounds.Top);
            int textPadding = 6;

            var labelPos = player.IsAI ? App.Config.UI.EspLabelPositionAI : App.Config.UI.EspLabelPosition;

            if (bbox.HasValue)
            {
                var box = bbox.Value;
                drawX = box.Left + (box.Width / 2f);
                drawY = labelPos == EspLabelPosition.Top
                    ? box.Top - textHeight - textPadding
                    : box.Bottom + textPadding;
            }
            else if (TryProject(player.GetBonePos(Bones.HumanHead), screenWidth, screenHeight, out var headScreen))
            {
                drawX = headScreen.X;
                drawY = labelPos == EspLabelPosition.Top
                    ? headScreen.Y - textHeight - textPadding
                    : headScreen.Y + textPadding;
            }
            else
            {
                return;
            }

            ctx.DrawText(text, drawX, drawY, color, DxTextSize.Medium, centerX: true);
        }

        /// <summary>
        /// Draw 'ESP Hidden' notification.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void DrawNotShown(Dx9RenderContext ctx, float width, float height)
        {
            ctx.DrawText("ESP Hidden", width / 2f, height / 2f, new DxColor(255, 255, 255, 255), DxTextSize.Large, centerX: true, centerY: true);
        }

        private void DrawCrosshair(Dx9RenderContext ctx, float width, float height)
        {
            float centerX = width / 2f;
            float centerY = height / 2f;
            float length = MathF.Max(2f, App.Config.UI.EspCrosshairLength);

            var color = GetCrosshairColor();
            ctx.DrawLine(new RawVector2(centerX - length, centerY), new RawVector2(centerX + length, centerY), color, _crosshairPaint.StrokeWidth);
            ctx.DrawLine(new RawVector2(centerX, centerY - length), new RawVector2(centerX, centerY + length), color, _crosshairPaint.StrokeWidth);
        }

        private void DrawDeviceAimbotTargetLine(Dx9RenderContext ctx, float width, float height)
        {
            var DeviceAimbot = MemDMA.DeviceAimbot;
            if (DeviceAimbot?.LockedTarget is not { } target)
                return;

            var headPos = target.GetBonePos(Bones.HumanHead);
            if (!WorldToScreen2(headPos, out var screen, width, height))
                return;

            var center = new RawVector2(width / 2f, height / 2f);
            bool engaged = DeviceAimbot.IsEngaged;
            var skColor = engaged ? new SKColor(0, 200, 255, 200) : new SKColor(255, 210, 0, 180);
            ctx.DrawLine(center, ToRaw(screen), ToColor(skColor), 2f);
        }

        private void DrawDeviceAimbotFovCircle(Dx9RenderContext ctx, float width, float height)
        {
            var cfg = App.Config.Device;
            if (!cfg.ShowFovCircle || cfg.FOV <= 0)
                return;

            float radius = Math.Clamp(cfg.FOV, 5f, Math.Min(width, height));
            bool engaged = MemDMA.DeviceAimbot?.IsEngaged == true;

            // Parse color from config using SKColor.Parse (supports #AARRGGBB and #RRGGBB formats)
            var colorStr = engaged ? cfg.FovCircleColorEngaged : cfg.FovCircleColorIdle;
            var skColor = SKColor.TryParse(colorStr, out var parsed)
                ? parsed
                : new SKColor(255, 255, 255, 180); // Fallback to semi-transparent white

            ctx.DrawCircle(new RawVector2(width / 2f, height / 2f), radius, ToColor(skColor), filled: false);
        }

        private void DrawDeviceAimbotDebugOverlay(Dx9RenderContext ctx, float width, float height)
        {
            if (!App.Config.Device.ShowDebug)
                return;

            var snapshot = MemDMA.DeviceAimbot?.GetDebugSnapshot();

            var lines = snapshot == null
                ? new[] { "Device Aimbot: no data" }
                : new[]
                {
                    "=== Device Aimbot ===",
                    $"Status: {snapshot.Status}",
                    $"Key: {(snapshot.KeyEngaged ? "ENGAGED" : "Idle")} | Enabled: {snapshot.Enabled} | Device: {(snapshot.DeviceConnected ? "Connected" : "Disconnected")}",
                    $"InRaid: {snapshot.InRaid} | FOV: {snapshot.ConfigFov:F0}px | MaxDist: {snapshot.ConfigMaxDistance:F0}m | Mode: {snapshot.TargetingMode}",
                    $"Candidates t:{snapshot.CandidateTotal} type:{snapshot.CandidateTypeOk} dist:{snapshot.CandidateInDistance} skel:{snapshot.CandidateWithSkeleton} w2s:{snapshot.CandidateW2S} final:{snapshot.CandidateCount}",
                    $"Target: {(snapshot.LockedTargetName ?? "None")} [{snapshot.LockedTargetType?.ToString() ?? "-"}] valid={snapshot.TargetValid}",
                    snapshot.LockedTargetDistance.HasValue ? $"  Dist {snapshot.LockedTargetDistance.Value:F1}m | FOV { (float.IsNaN(snapshot.LockedTargetFov) ? "n/a" : snapshot.LockedTargetFov.ToString("F1")) } | Bone {snapshot.TargetBone}" : string.Empty,
                    $"Fireport: {(snapshot.HasFireport ? snapshot.FireportPosition?.ToString() : "None")}",
                    $"Ballistics: {(snapshot.BallisticsValid ? $"OK (Speed {(snapshot.BulletSpeed.HasValue ? snapshot.BulletSpeed.Value.ToString("F1") : "?")} m/s, Predict {(snapshot.PredictionEnabled ? "ON" : "OFF")})" : "Invalid/None")}"
                }.Where(l => !string.IsNullOrEmpty(l)).ToArray();

            float x = 10f;
            float y = 40f;
            float lineStep = 16f;
            var color = ToColor(SKColors.White);

            foreach (var line in lines)
            {
                ctx.DrawText(line, x, y, color, DxTextSize.Small);
                y += lineStep;
            }
        }

        private void DrawFPS(Dx9RenderContext ctx, float width, float height)
        {
            var fpsText = $"FPS: {_fps}";
            ctx.DrawText(fpsText, 10, 10, new DxColor(255, 255, 255, 255), DxTextSize.Small);
        }

        private void DrawLootDebugOverlay(Dx9RenderContext ctx, float width, float height, LocalPlayer localPlayer)
        {
            if (!App.Config.UI.ShowESP)
                return;

            var lootItems = Memory.Game?.Loot?.AllLoot;
            if (lootItems is null)
            {
                ctx.DrawText("Loot Debug: No loot items", 10, 30, new DxColor(255, 255, 255, 255), DxTextSize.Small);
                return;
            }

            int totalItems = 0;
            int shownItems = 0;
            int filteredItems = 0;
            int medItems = 0;
            int foodItems = 0;
            int regularLootItems = 0;
            int inFilterItems = 0;
            int wishlistedItems = 0;
            int shownMeds = 0;
            int shownFood = 0;
            int shownRegular = 0;
            int shownInFilter = 0;
            int shownWishlist = 0;
            int itemsWithCustomFilter = 0;
            int itemsWithImportant = 0;
            int itemsWithImportantNotWishlist = 0;

            var camPos = localPlayer?.Position ?? Vector3.Zero;
            float maxRenderDistance = App.Config.UI.EspLootMaxDistance;
            bool unlimitedDistance = maxRenderDistance <= 0f;

            foreach (var item in lootItems)
            {
                if (item is StaticLootContainer or LootAirdrop)
                    continue;

                totalItems++;

                bool isCorpse = item is LootCorpse;
                bool isQuest = item.IsQuestItem;
                bool isFood = item.IsFood;
                bool isMeds = item.IsMeds;
                bool isBackpack = item.IsBackpack;
                // isInFilter: exclude wishlist from IsImportant (IsImportant includes IsWishlisted)
                bool isInFilter = item.CustomFilter != null || (item.IsImportant && !item.IsWishlisted);
                bool isWishlisted = item.IsWishlisted;

                // Debug counters
                if (item.CustomFilter != null) itemsWithCustomFilter++;
                if (item.IsImportant) itemsWithImportant++;
                if (item.IsImportant && !item.IsWishlisted) itemsWithImportantNotWishlist++;
                if (isInFilter) inFilterItems++;
                if (isWishlisted) wishlistedItems++;
                if (isMeds) medItems++;
                if (isFood) foodItems++;
                if (!isMeds && !isFood && !isQuest && !isCorpse) regularLootItems++;

                // Check distance
                float distance = Vector3.Distance(camPos, item.Position);
                if (!unlimitedDistance && distance > maxRenderDistance)
                    continue;

                // Apply same filtering logic as DrawLoot
                if (isQuest && !App.Config.UI.EspQuestLoot)
                    continue;
                if (isCorpse && !App.Config.UI.EspCorpses)
                    continue;

                bool shouldShow = false;
                string reason = "";

                if (App.Config.UI.EspLootFilterOnly)
                {
                    if (isInFilter)
                    {
                        shouldShow = true;
                        reason = "IN_FILTER";
                        if (isInFilter) shownInFilter++;
                    }
                    else if (App.Config.UI.EspShowWishlisted && isWishlisted)
                    {
                        shouldShow = true;
                        reason = "WISHLIST";
                        shownWishlist++;
                    }
                    else
                    {
                        reason = "NOT_IN_FILTER";
                    }
                }
                else if (App.Config.UI.EspShowWishlisted && isWishlisted)
                {
                    // Wishlist items have higher priority than other categories
                    shouldShow = true;
                    reason = "WISHLIST";
                    shownWishlist++;
                }
                else if (isMeds)
                {
                    // Show wishlisted meds if EspShowWishlisted is enabled
                    if (App.Config.UI.EspShowWishlisted && isWishlisted)
                    {
                        shouldShow = true;
                        reason = "WISHLIST_MEDS";
                        shownMeds++;
                        shownWishlist++;
                    }
                    else if (App.Config.UI.EspMeds)
                    {
                        shouldShow = true;
                        reason = "MEDS_ON";
                        shownMeds++;
                    }
                    else
                    {
                        reason = "MEDS_OFF";
                    }
                }
                else if (isFood)
                {
                    // Show wishlisted food if EspShowWishlisted is enabled
                    if (App.Config.UI.EspShowWishlisted && isWishlisted)
                    {
                        shouldShow = true;
                        reason = "WISHLIST_FOOD";
                        shownFood++;
                        shownWishlist++;
                    }
                    else if (App.Config.UI.EspFood)
                    {
                        shouldShow = true;
                        reason = "FOOD_ON";
                        shownFood++;
                    }
                    else
                    {
                        reason = "FOOD_OFF";
                    }
                }
                else if (App.Config.UI.EspLoot)
                {
                    // EspLoot should ONLY show regular loot, exclude filter and wishlist items
                    if (!isInFilter && !isWishlisted && (item.IsRegularLoot || item.IsValuableLoot))
                    {
                        shouldShow = true;
                        reason = "LOOT_ON";
                        shownRegular++;
                    }
                    else if (isInFilter || isWishlisted)
                    {
                        // Items in filter or wishlist should NOT show in EspLoot
                        reason = "FILTER/WISHLIST_EXCLUDED_FROM_LOOT";
                    }
                    else
                    {
                        reason = "BELOW_MIN_VALUE";
                    }
                }
                else
                {
                    reason = "ALL_OFF";
                }

                if (shouldShow)
                {
                    shownItems++;
                    if (isInFilter) filteredItems++;
                }
            }

            // Draw debug info
            float y = 30f;
            float lineHeight = 16f;
            var textColor = new DxColor(255, 255, 255, 255);
            var highlightColor = new DxColor(0, 255, 0, 255);
            var errorColor = new DxColor(255, 0, 0, 255);
            var warningColor = new DxColor(255, 255, 0, 255);

            ctx.DrawText("=== ESP Loot Debug ===", 10, y, textColor, DxTextSize.Small);
            y += lineHeight;

            ctx.DrawText($"EspLootFilterOnly: {App.Config.UI.EspLootFilterOnly}", 10, y, 
                App.Config.UI.EspLootFilterOnly ? highlightColor : textColor, DxTextSize.Small);
            y += lineHeight;

            ctx.DrawText($"EspShowWishlisted: {App.Config.UI.EspShowWishlisted}", 10, y, 
                App.Config.UI.EspShowWishlisted ? highlightColor : textColor, DxTextSize.Small);
            y += lineHeight;

            ctx.DrawText($"EspLoot: {App.Config.UI.EspLoot}", 10, y, 
                App.Config.UI.EspLoot ? highlightColor : textColor, DxTextSize.Small);
            y += lineHeight;

            ctx.DrawText($"EspMeds: {App.Config.UI.EspMeds}", 10, y, 
                App.Config.UI.EspMeds ? highlightColor : textColor, DxTextSize.Small);
            y += lineHeight;

            ctx.DrawText($"EspFood: {App.Config.UI.EspFood}", 10, y, 
                App.Config.UI.EspFood ? highlightColor : textColor, DxTextSize.Small);
            y += lineHeight;

            y += lineHeight;
            ctx.DrawText($"Total Items: {totalItems}", 10, y, textColor, DxTextSize.Small);
            y += lineHeight;

            ctx.DrawText($"Shown Items: {shownItems}", 10, y, 
                shownItems > 0 ? highlightColor : errorColor, DxTextSize.Small);
            y += lineHeight;

            ctx.DrawText($"  - In Filter: {shownInFilter} | Wishlist: {shownWishlist}", 10, y, textColor, DxTextSize.Small);
            y += lineHeight;

            ctx.DrawText($"  - Meds: {shownMeds} | Food: {shownFood} | Regular: {shownRegular}", 10, y, textColor, DxTextSize.Small);
            y += lineHeight;

            ctx.DrawText($"Total In Filter: {inFilterItems} | Wishlisted: {wishlistedItems}", 10, y, 
                inFilterItems > 0 ? highlightColor : errorColor, DxTextSize.Small);
            y += lineHeight;
            
            ctx.DrawText($"  - CustomFilter: {itemsWithCustomFilter} | Important: {itemsWithImportant} | Important(!Wishlist): {itemsWithImportantNotWishlist}", 10, y, textColor, DxTextSize.Small);
            y += lineHeight;
            
            ctx.DrawText($"Shown Wishlist: {shownWishlist} (out of {wishlistedItems} total)", 10, y,
                shownWishlist > 0 ? highlightColor : textColor, DxTextSize.Small);
            y += lineHeight;

            ctx.DrawText($"Total - Meds: {medItems} | Food: {foodItems} | Regular: {regularLootItems}", 10, y, textColor, DxTextSize.Small);
            y += lineHeight;

            y += lineHeight;
            if (App.Config.UI.EspLootFilterOnly && !App.Config.UI.EspLoot)
            {
                ctx.DrawText("Mode: FILTER_ONLY (should only show filter items)", 10, y, highlightColor, DxTextSize.Small);
            }
            else if (!App.Config.UI.EspLootFilterOnly && App.Config.UI.EspLoot)
            {
                ctx.DrawText("Mode: LOOT_ONLY (showing regular loot)", 10, y, warningColor, DxTextSize.Small);
            }
            else if (!App.Config.UI.EspLootFilterOnly && !App.Config.UI.EspLoot)
            {
                ctx.DrawText("Mode: ALL_OFF (should hide all)", 10, y, 
                    shownItems > 0 ? errorColor : highlightColor, DxTextSize.Small);
            }
            else
            {
                ctx.DrawText("Mode: MIXED", 10, y, warningColor, DxTextSize.Small);
            }
        }

        /// <summary>
        /// Finds the nearest player (excluding teammates), prioritizing PMC as highest priority.
        /// </summary>
        private AbstractPlayer FindNearestPlayer(LocalPlayer localPlayer, IReadOnlyCollection<AbstractPlayer> allPlayers)
        {
            if (localPlayer == null || allPlayers == null)
                return null;

            AbstractPlayer nearestPMC = null;
            AbstractPlayer nearestOtherPlayer = null;
            AbstractPlayer nearestAI = null;
            float nearestPMCDistance = float.MaxValue;
            float nearestOtherPlayerDistance = float.MaxValue;
            float nearestAIDistance = float.MaxValue;

            foreach (var player in allPlayers)
            {
                // Skip invalid players
                if (player == null || player == localPlayer || !player.IsAlive || !player.IsActive)
                    continue;

                // Skip teammates
                if (player.Type == PlayerType.Teammate)
                    continue;

                // Skip players with invalid positions
                var playerPos = player.Position;
                if (playerPos == Vector3.Zero ||
                    float.IsNaN(playerPos.X) || float.IsNaN(playerPos.Y) || float.IsNaN(playerPos.Z) ||
                    float.IsInfinity(playerPos.X) || float.IsInfinity(playerPos.Y) || float.IsInfinity(playerPos.Z))
                    continue;

                float distance = Vector3.Distance(localPlayer.Position, player.Position);

                // Priority 1: PMC (highest priority)
                if (player.Type == PlayerType.PMC && distance < nearestPMCDistance)
                {
                    nearestPMC = player;
                    nearestPMCDistance = distance;
                }
                // Priority 2: Other human players (PScav, SpecialPlayer, Streamer)
                else if (player.IsHuman && (player.Type == PlayerType.PScav || 
                                            player.Type == PlayerType.SpecialPlayer || 
                                            player.Type == PlayerType.Streamer) && 
                         distance < nearestOtherPlayerDistance)
                {
                    nearestOtherPlayer = player;
                    nearestOtherPlayerDistance = distance;
                }
                // Priority 3: AI
                else if (player.IsAI && distance < nearestAIDistance)
                {
                    nearestAI = player;
                    nearestAIDistance = distance;
                }
            }

            // Return PMC first, then other players, then AI
            return nearestPMC ?? nearestOtherPlayer ?? nearestAI;
        }

        /// <summary>
        /// Draws nearest player information in the center-bottom of the ESP window.
        /// </summary>
        private void DrawNearestPlayerInfo(Dx9RenderContext ctx, float width, float height, LocalPlayer localPlayer, IReadOnlyCollection<AbstractPlayer> allPlayers)
        {
            // Check if feature is enabled
            if (!App.Config.UI.EspNearestPlayerInfo)
                return;

            var nearestPlayer = FindNearestPlayer(localPlayer, allPlayers);
            if (nearestPlayer == null)
                return;

            float distance = Vector3.Distance(localPlayer.Position, nearestPlayer.Position);

            // Build single line text
            var parts = new List<string>();
            
            // Name
            string name = nearestPlayer.Name ?? "Unknown";
            parts.Add(name);

            // Distance
            parts.Add($"{distance:F1}m");

            // Type
            string typeText = nearestPlayer.Type switch
            {
                PlayerType.PMC => "PMC",
                PlayerType.PScav => "PScav",
                PlayerType.AIScav => "Scav",
                PlayerType.AIRaider => "Raider",
                PlayerType.AIBoss => "Boss",
                PlayerType.SpecialPlayer => "Special",
                PlayerType.Streamer => "Streamer",
                _ => nearestPlayer.Type.ToString()
            };
            parts.Add(typeText);

            // Health status (if available)
            if (nearestPlayer is ObservedPlayer observed && observed.HealthStatus != Enums.ETagStatus.Healthy)
            {
                parts.Add(observed.HealthStatus.ToString());
            }

            // Faction (if PMC)
            if (nearestPlayer.IsPmc)
            {
                parts.Add(nearestPlayer.PlayerSide.ToString());
            }

            // Group ID (if available)
            if (nearestPlayer.GroupID >= 0 && nearestPlayer.IsPmc && !nearestPlayer.IsAI)
                parts.Add($"G:{nearestPlayer.GroupID}");

            // Combine all parts into single line
            string text = string.Join(" | ", parts);

            // Position in center-bottom (higher up to be more visible)
            float padding = 150f; // Increased from 20f to make it more visible
            float textY = height - padding;

            // Get player color for text (matches team/faction color)
            var textColor = GetPlayerColorForRender(nearestPlayer);

            // Draw text (centered horizontally)
            ctx.DrawText(text, width / 2f, textY, textColor, DxTextSize.Large, centerX: true);
        }

        /// <summary>
        /// Shows a notification message in the bottom-right corner of the ESP window.
        /// </summary>
        public void ShowNotification(string message)
        {
            if (_isClosing)
                return;

            _notificationMessage = message;
            _notificationTimer.Restart();
            RefreshESP();
        }

        /// <summary>
        /// Draws the notification message in the bottom-right corner.
        /// </summary>
        private void DrawNotification(Dx9RenderContext ctx, float width, float height)
        {
            if (string.IsNullOrEmpty(_notificationMessage) || !_notificationTimer.IsRunning)
                return;

            long elapsedMs = _notificationTimer.ElapsedMilliseconds;
            if (elapsedMs > NOTIFICATION_DURATION_MS)
            {
                _notificationMessage = string.Empty;
                _notificationTimer.Stop();
                return;
            }

            // Calculate fade-in/fade-out opacity
            float opacity = 1.0f;
            if (elapsedMs < 200) // Fade in over 200ms
            {
                opacity = elapsedMs / 200.0f;
            }
            else if (elapsedMs > NOTIFICATION_DURATION_MS - 300) // Fade out over last 300ms
            {
                float fadeOutStart = NOTIFICATION_DURATION_MS - 300;
                opacity = 1.0f - ((elapsedMs - fadeOutStart) / 300.0f);
            }

            opacity = Math.Clamp(opacity, 0.0f, 1.0f);
            byte alpha = (byte)(opacity * 255);

            // Measure text to position it correctly
            var textBounds = ctx.MeasureText(_notificationMessage, DxTextSize.Large);
            int textWidth = Math.Max(1, textBounds.Right - textBounds.Left);
            int textHeight = Math.Max(1, textBounds.Bottom - textBounds.Top);

            // Position in bottom-right with padding
            float padding = 20f;
            float x = width - textWidth - padding;
            float y = height - textHeight - padding;

            // Draw background rectangle with semi-transparent black
            float bgPadding = 14f;
            float bgX = x - bgPadding;
            float bgY = y - bgPadding;
            float bgWidth = textWidth + bgPadding * 2;
            float bgHeight = textHeight + bgPadding * 2;

            // Draw background (semi-transparent black)
            var bgColor = new DxColor(0, 0, 0, (byte)(alpha * 0.7f));
            ctx.DrawFilledRect(new RectangleF(bgX, bgY, bgWidth, bgHeight), bgColor);

            // Draw text
            var textColor = new DxColor(255, 255, 255, alpha);
            ctx.DrawText(_notificationMessage, x, y, textColor, DxTextSize.Large);
        }

        private static RawVector2 ToRaw(SKPoint point) => new(point.X, point.Y);

        private static DxColor ToColor(SKPaint paint) => ToColor(paint.Color);

        private static DxColor ToColor(SKColor color) => new(color.Blue, color.Green, color.Red, color.Alpha);

        #endregion

        private DxColor GetPlayerColorForRender(AbstractPlayer player)
        {
            var cfg = App.Config.UI;
            var basePaint = GetPlayerColor(player);

            // Preserve special colouring (local, focused, watchlist/streamer, teammates).
            if (player is LocalPlayer || player.IsFocused ||
                player.Type is PlayerType.SpecialPlayer or PlayerType.Streamer or PlayerType.Teammate)
            {
                return ToColor(basePaint);
            }

            // Respect group/faction colours when enabled.
            if (!player.IsAI)
            {
                if (cfg.EspGroupColors && player.GroupID >= 0)
                    return ToColor(basePaint);
                if (cfg.EspFactionColors && player.IsPmc)
                {
                    var factionColor = player.PlayerSide switch
                    {
                        Enums.EPlayerSide.Bear => ColorFromHex(cfg.EspColorFactionBear),
                        Enums.EPlayerSide.Usec => ColorFromHex(cfg.EspColorFactionUsec),
                        _ => ColorFromHex(cfg.EspColorPlayers)
                    };
                    return ToColor(factionColor);
                }
            }

            if (player.IsAI)
            {
                var aiHex = player.Type switch
                {
                    PlayerType.AIBoss => cfg.EspColorBosses,
                    PlayerType.AIRaider => cfg.EspColorRaiders,
                    _ => cfg.EspColorAI
                };

                return ToColor(ColorFromHex(aiHex));
            }

            // Handle Player Scavs specifically.
            if (player.Type == PlayerType.PScav)
            {
                return ToColor(ColorFromHex(cfg.EspColorPlayerScavs));
            }

            // Fallback to user-configured player colours.
            return ToColor(ColorFromHex(cfg.EspColorPlayers));
        }

        private DxColor GetLootColorForRender() => ToColor(ColorFromHex(App.Config.UI.EspColorLoot));
        private DxColor GetExfilColorForRender() => ToColor(ColorFromHex(App.Config.UI.EspColorExfil));
        private DxColor GetTripwireColorForRender() => ToColor(ColorFromHex(App.Config.UI.EspColorTripwire));
        private DxColor GetGrenadeColorForRender() => ToColor(ColorFromHex(App.Config.UI.EspColorGrenade));
        private DxColor GetContainerColorForRender() => ToColor(ColorFromHex(App.Config.UI.EspColorContainers));
        private DxColor GetCrosshairColor() => ToColor(ColorFromHex(App.Config.UI.EspColorCrosshair));

        private static SKColor ColorFromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return SKColors.White;
            try { return SKColor.Parse(hex); }
            catch { return SKColors.White; }
        }

        private void ApplyDxFontConfig()
        {
            var ui = App.Config.UI;
            _dxOverlay?.SetFontConfig(
                ui.EspFontFamily,
                ui.EspFontSizeSmall,
                ui.EspFontSizeMedium,
                ui.EspFontSizeLarge);
        }

        #region DX Init Handling

        private void Overlay_DeviceInitFailed(Exception ex)
        {
            _dxInitFailed = true;
            DebugLogger.LogDebug($"ESP DX init failed: {ex}");

            Dispatcher.BeginInvoke(new Action(() =>
            {
                RenderRoot.Children.Clear();
                RenderRoot.Children.Add(new TextBlock
                {
                    Text = "DX overlay init failed. See log for details.",
                    Foreground = System.Windows.Media.Brushes.White,
                    Background = System.Windows.Media.Brushes.Black,
                    Margin = new Thickness(12)
                });
            }), DispatcherPriority.Send);
        }

        #endregion

        #region WorldToScreen Conversion

        /// <summary>
        /// Resets ESP state when a raid ends (ensures clean slate next raid).
        /// </summary>
        public void OnRaidStopped()
        {
            _lastInRaidState = false;
            _espGroupPaints.Clear();
            CameraManagerNew.Reset();
            RefreshESP();
            DebugLogger.LogInfo("ESP: RaidStopped -> state reset");
        }

        private bool WorldToScreen2(in Vector3 world, out SKPoint scr, float screenWidth, float screenHeight)
        {
            return CameraManagerNew.WorldToScreen(in world, out scr, true, true);
        }

        private bool WorldToScreen2WithScale(in Vector3 world, out SKPoint scr, out float scale, float screenWidth, float screenHeight)
        {
            scr = default;
            scale = 1f;

            // Get projection (PR #6 version - no depth output)
            if (!CameraManagerNew.WorldToScreen(in world, out var screen, true, true))
            {
                return false;
            }

            scr = screen;

            // ? Calculate scale based on distance from PLAYER (not camera) - matches Aimview behavior
            var playerPos = LocalPlayer?.Position ?? CameraManagerNew.CameraPosition;
            float dist = Vector3.Distance(playerPos, world);
            
            // Perspective-based scaling - markers get SMALLER at greater distances (natural view)
            // At close range (5m): scale ~2.0x (larger, more visible)
            // At medium range (10m): scale ~1.0x (normal size)  
            // At far range (30m+): scale ~0.33x (smaller, less obtrusive)
            const float referenceDistance = 10f; // Reference distance for 1.0x scale
            scale = Math.Clamp(referenceDistance / Math.Max(dist, 1f), 0.3f, 3f);
            
            return true;
        }

        private bool TryProject(in Vector3 world, float w, float h, out SKPoint screen)
        {
            screen = default;
            if (world == Vector3.Zero)
                return false;
            if (!WorldToScreen2(world, out screen, w, h))
                return false;
            if (float.IsNaN(screen.X) || float.IsInfinity(screen.X) ||
                float.IsNaN(screen.Y) || float.IsInfinity(screen.Y))
                return false;

            const float margin = 200f; 
            if (screen.X < -margin || screen.X > w + margin ||
                screen.Y < -margin || screen.Y > h + margin)
                return false;

            return true;
        }

        #endregion

        #region Window Management

        private void GlControl_MouseDown(object sender, WinForms.MouseEventArgs e)
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                try { this.DragMove(); } catch { /* ignore dragging errors */ }
            }
        }

        private void GlControl_DoubleClick(object sender, EventArgs e)
        {
            ToggleFullscreen();
        }

        private void GlControl_KeyDown(object sender, WinForms.KeyEventArgs e)
        {
            if (e.KeyCode == WinForms.Keys.F12)
            {
                ForceReleaseCursorAndHide();
                return;
            }

            if (e.KeyCode == WinForms.Keys.Escape && this.WindowState == WindowState.Maximized)
            {
                ToggleFullscreen();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Allow dragging the window
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _isClosing = true;
            try
            {
                _highFrequencyTimer?.Dispose();
                _dxOverlay?.Dispose();
                _skeletonPaint.Dispose();
                _boxPaint.Dispose();
                _crosshairPaint.Dispose();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ESP: OnClosed cleanup error: {ex}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        // Method to force refresh
        public void RefreshESP()
        {
            if (_isClosing)
                return;

            try
            {
                _dxOverlay?.Render();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ESP Refresh error: {ex}")
;            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _renderPending, 0);
            }
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ToggleFullscreen();
        }

        // Handler for keys (ESC to exit fullscreen)
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F12)
            {
                ForceReleaseCursorAndHide();
                return;
            }

            if (e.Key == Key.Escape && this.WindowState == WindowState.Maximized)
            {
                ToggleFullscreen();
            }
        }

        // Simple fullscreen toggle
        public void ToggleFullscreen()
        {
            if (_isFullscreen)
            {
                this.WindowState = WindowState.Normal;
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.Topmost = false;
                this.ResizeMode = ResizeMode.CanResize;
                this.Width = 400;
                this.Height = 300;
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                _isFullscreen = false;
            }
            else
            {
                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;
                this.Topmost = true;
                this.WindowState = WindowState.Normal;

                var monitor = GetTargetMonitor();
                var (width, height) = GetConfiguredResolution(monitor);

                this.Left = monitor?.Left ?? 0;
                this.Top = monitor?.Top ?? 0;

                this.Width = width;
                this.Height = height;
                _isFullscreen = true;
            }

            this.RefreshESP();
        }

        public void ApplyResolutionOverride()
        {
            if (!_isFullscreen)
                return;

            var monitor = GetTargetMonitor();
            var (width, height) = GetConfiguredResolution(monitor);
            this.Left = monitor?.Left ?? 0;
            this.Top = monitor?.Top ?? 0;
            this.Width = width;
            this.Height = height;
            this.RefreshESP();
        }

        private (double width, double height) GetConfiguredResolution(MonitorInfo monitor)
        {
            double width = App.Config.UI.EspScreenWidth > 0
                ? App.Config.UI.EspScreenWidth
                : monitor?.Width ?? SystemParameters.PrimaryScreenWidth;
            double height = App.Config.UI.EspScreenHeight > 0
                ? App.Config.UI.EspScreenHeight
                : monitor?.Height ?? SystemParameters.PrimaryScreenHeight;
            return (width, height);
        }

        private void ApplyResolutionOverrideIfNeeded()
        {
            if (!_isFullscreen)
                return;

            if (App.Config.UI.EspScreenWidth <= 0 && App.Config.UI.EspScreenHeight <= 0)
                return;

            var monitor = GetTargetMonitor();
            var target = GetConfiguredResolution(monitor);
            if (Math.Abs(Width - target.width) > 0.5 || Math.Abs(Height - target.height) > 0.5)
            {
                Width = target.width;
                Height = target.height;
                Left = monitor?.Left ?? 0;
                Top = monitor?.Top ?? 0;
            }
        }

        private MonitorInfo GetTargetMonitor()
        {
            return MonitorInfo.GetMonitor(App.Config.UI.EspTargetScreen);
        }

        public void ApplyFontConfig()
        {
            ApplyDxFontConfig();
            RefreshESP();
 }

        /// <summary>
        /// Emergency escape hatch if the overlay ever captures the cursor:
        /// releases capture, resets cursors, hides the ESP, and drops Topmost.
        /// Bound to F12 on both WPF and WinForms handlers.
        /// </summary>
        private void ForceReleaseCursorAndHide()
        {
            try
            {
                Mouse.Capture(null);
                WinForms.Cursor.Current = WinForms.Cursors.Default;
                this.Cursor = System.Windows.Input.Cursors.Arrow;
                Mouse.OverrideCursor = null;
                if (_dxOverlay != null)
                {
                    _dxOverlay.Capture = false;
                }
                this.Topmost = false;
                ShowESP = false;
                Hide();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ESP: ForceReleaseCursor failed: {ex}");
            }
        }

        #endregion
    }
}