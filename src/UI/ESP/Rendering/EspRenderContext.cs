/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Camera;
using LoneEftDmaRadar.Tarkov.GameWorld.Exits;
using LoneEftDmaRadar.Tarkov.GameWorld.Explosives;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Quests;
using SkiaSharp;
using DxColor = SharpDX.Mathematics.Interop.RawColorBGRA;

namespace LoneEftDmaRadar.UI.ESP.Rendering
{
    /// <summary>
    /// Shared context for ESP rendering operations.
    /// Contains common data and utilities used by all ESP renderers.
    /// Supports dependency injection via IGameStateProvider for testability.
    /// </summary>
    internal sealed class EspRenderContext
    {
        #region Properties

        /// <summary>
        /// DirectX rendering context.
        /// </summary>
        public Dx9RenderContext Ctx { get; }

        /// <summary>
        /// Screen width in pixels.
        /// </summary>
        public float ScreenWidth { get; }

        /// <summary>
        /// Screen height in pixels.
        /// </summary>
        public float ScreenHeight { get; }

        /// <summary>
        /// The local player (radar user).
        /// </summary>
        public LocalPlayer LocalPlayer { get; }

        /// <summary>
        /// UI scale factor from config.
        /// </summary>
        public float UIScale { get; }

        /// <summary>
        /// Game state provider for accessing game data.
        /// </summary>
        public IGameStateProvider GameState { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new render context with explicit game state provider.
        /// Use this constructor for testability.
        /// </summary>
        public EspRenderContext(
            Dx9RenderContext ctx,
            float screenWidth,
            float screenHeight,
            LocalPlayer localPlayer,
            IGameStateProvider gameState)
        {
            Ctx = ctx;
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            LocalPlayer = localPlayer;
            GameState = gameState ?? MemoryGameStateProvider.Instance;
            UIScale = App.Config.UI.UIScale;
        }

        /// <summary>
        /// Creates a new render context using the default MemoryGameStateProvider.
        /// </summary>
        public EspRenderContext(Dx9RenderContext ctx, float screenWidth, float screenHeight, LocalPlayer localPlayer)
            : this(ctx, screenWidth, screenHeight, localPlayer, MemoryGameStateProvider.Instance)
        {
        }

        #endregion

        #region Game State Accessors

        /// <summary>
        /// All players in the current raid.
        /// </summary>
        public IReadOnlyCollection<AbstractPlayer> AllPlayers => GameState.Players;

        /// <summary>
        /// All exit points in the current raid.
        /// </summary>
        public IReadOnlyCollection<IExitPoint> Exits => GameState.Exits;

        /// <summary>
        /// All explosives (grenades, tripwires) in the current raid.
        /// </summary>
        public IReadOnlyCollection<IExplosiveItem> Explosives => GameState.Explosives;

        /// <summary>
        /// Loot manager for the current raid.
        /// </summary>
        public LootManager Loot => GameState.Loot;

        /// <summary>
        /// Quest manager for the current raid.
        /// </summary>
        public QuestManager QuestManager => GameState.QuestManager;

        /// <summary>
        /// True if currently in a raid.
        /// </summary>
        public bool InRaid => GameState.InRaid;

        #endregion

        #region WorldToScreen Methods

        /// <summary>
        /// Projects a world position to screen coordinates.
        /// </summary>
        /// <param name="world">World position.</param>
        /// <param name="screen">Output screen position.</param>
        /// <returns>True if the position is visible on screen.</returns>
        public bool WorldToScreen(in Vector3 world, out SKPoint screen)
        {
            return CameraManager.WorldToScreen(in world, out screen, true, true);
        }

        /// <summary>
        /// Projects a world position to screen coordinates with perspective scale.
        /// </summary>
        /// <param name="world">World position.</param>
        /// <param name="screen">Output screen position.</param>
        /// <param name="scale">Output perspective scale factor.</param>
        /// <returns>True if the position is visible on screen.</returns>
        public bool WorldToScreenWithScale(in Vector3 world, out SKPoint screen, out float scale)
        {
            screen = default;
            scale = 1f;

            if (!CameraManager.WorldToScreen(in world, out var screenPos, true, true))
                return false;

            screen = screenPos;

            // Calculate scale based on distance from player (matches Aimview behavior)
            var playerPos = LocalPlayer?.Position ?? CameraManager.CameraPosition;
            float dist = Vector3.Distance(playerPos, world);

            // Perspective-based scaling - markers get smaller at greater distances
            scale = Math.Clamp(
                ESPConstants.ScaleReferenceDistance / Math.Max(dist, 1f),
                ESPConstants.MinScaleFactor,
                ESPConstants.MaxScaleFactor);

            return true;
        }

        /// <summary>
        /// Tries to project a world position to valid screen coordinates.
        /// Validates the result for NaN, Infinity, and screen bounds.
        /// </summary>
        /// <param name="world">World position.</param>
        /// <param name="screen">Output screen position.</param>
        /// <returns>True if the position is valid and on screen.</returns>
        public bool TryProject(in Vector3 world, out SKPoint screen)
        {
            screen = default;

            if (world == Vector3.Zero)
                return false;

            if (!WorldToScreen(world, out screen))
                return false;

            if (float.IsNaN(screen.X) || float.IsInfinity(screen.X) ||
                float.IsNaN(screen.Y) || float.IsInfinity(screen.Y))
                return false;

            if (screen.X < -ESPConstants.ScreenMargin || screen.X > ScreenWidth + ESPConstants.ScreenMargin ||
                screen.Y < -ESPConstants.ScreenMargin || screen.Y > ScreenHeight + ESPConstants.ScreenMargin)
                return false;

            return true;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Calculates the distance from local player to a world position.
        /// </summary>
        public float DistanceTo(Vector3 worldPos)
        {
            if (LocalPlayer == null)
                return float.MaxValue;

            return Vector3.Distance(LocalPlayer.Position, worldPos);
        }

        /// <summary>
        /// Calculates the scaled marker radius based on perspective scale and UI scale.
        /// </summary>
        public float GetScaledRadius(float scale)
        {
            return Math.Clamp(
                ESPConstants.BaseMarkerRadius * UIScale * scale,
                ESPConstants.MinMarkerRadius,
                ESPConstants.MaxMarkerRadius);
        }

        /// <summary>
        /// Gets the appropriate text size based on perspective scale.
        /// </summary>
        public DxTextSize GetTextSize(float scale)
        {
            return scale > ESPConstants.MediumTextScaleThreshold
                ? DxTextSize.Medium
                : DxTextSize.Small;
        }

        /// <summary>
        /// Checks if a position is within the specified max distance (0 = unlimited).
        /// </summary>
        public bool IsWithinDistance(Vector3 worldPos, float maxDistance)
        {
            if (maxDistance <= 0f)
                return true; // Unlimited

            return DistanceTo(worldPos) <= maxDistance;
        }

        #endregion
    }
}
