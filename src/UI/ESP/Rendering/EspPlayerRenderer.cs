/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Skia;
using SharpDX.Mathematics.Interop;
using SkiaSharp;
using System.Drawing;
using DxColor = SharpDX.Mathematics.Interop.RawColorBGRA;

namespace LoneEftDmaRadar.UI.ESP.Rendering
{
    /// <summary>
    /// Handles rendering of players (skeleton, boxes, labels) on the ESP overlay.
    /// </summary>
    internal sealed class EspPlayerRenderer
    {
        #region Bone Connections

        private static readonly (Bones From, Bones To)[] _boneConnections = new[]
        {
            (Bones.HumanHead, Bones.HumanNeck),
            (Bones.HumanNeck, Bones.HumanSpine3),
            (Bones.HumanSpine3, Bones.HumanSpine2),
            (Bones.HumanSpine2, Bones.HumanSpine1),
            (Bones.HumanSpine1, Bones.HumanPelvis),
            
            // Left Arm
            (Bones.HumanNeck, Bones.HumanLUpperarm),
            (Bones.HumanLUpperarm, Bones.HumanLForearm1),
            (Bones.HumanLForearm1, Bones.HumanLForearm2),
            (Bones.HumanLForearm2, Bones.HumanLPalm),
            
            // Right Arm
            (Bones.HumanNeck, Bones.HumanRUpperarm),
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

        /// <summary>
        /// Group color palette for ESP.
        /// </summary>
        private static readonly SKColor[] _groupPalette = new SKColor[]
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

        private static readonly ConcurrentDictionary<int, DxColor> _groupColorCache = new();

        #endregion

        #region Public Methods

        /// <summary>
        /// Renders all players on the ESP overlay.
        /// </summary>
        public void Draw(EspRenderContext context, IReadOnlyCollection<AbstractPlayer> allPlayers)
        {
            foreach (var player in allPlayers)
            {
                DrawPlayer(context, player);
            }
        }

        /// <summary>
        /// Clears cached group colors (call on raid end).
        /// </summary>
        public static void ClearCache()
        {
            _groupColorCache.Clear();
        }

        #endregion

        #region Private Methods

        private void DrawPlayer(EspRenderContext context, AbstractPlayer player)
        {
            if (player == null || player == context.LocalPlayer || !player.IsAlive || !player.IsActive)
                return;

            // Validate player position
            var playerPos = player.Position;
            if (!IsValidPosition(playerPos))
                return;

            // Check player type and distance limits
            bool isAI = player.Type is PlayerType.AIScav or PlayerType.AIRaider or PlayerType.AIBoss or PlayerType.PScav;
            float distance = context.DistanceTo(playerPos);
            float maxDistance = isAI ? App.Config.UI.EspAIMaxDistance : App.Config.UI.EspPlayerMaxDistance;

            if (maxDistance > 0 && distance > maxDistance)
                return;

            // Get player color
            var color = GetPlayerColor(player);
            bool isDeviceAimbotLocked = MemDMA.DeviceAimbot?.LockedTarget == player;
            if (isDeviceAimbotLocked)
            {
                color = EspColorHelper.DeviceAimbotLockedColor;
            }

            // Determine what to draw based on config
            bool drawSkeleton = isAI ? App.Config.UI.EspAISkeletons : App.Config.UI.EspPlayerSkeletons;
            bool drawBox = isAI ? App.Config.UI.EspAIBoxes : App.Config.UI.EspPlayerBoxes;
            bool drawName = isAI ? App.Config.UI.EspAINames : App.Config.UI.EspPlayerNames;
            bool drawHealth = isAI ? App.Config.UI.EspAIHealth : App.Config.UI.EspPlayerHealth;
            bool drawDistance = isAI ? App.Config.UI.EspAIDistance : App.Config.UI.EspPlayerDistance;
            bool drawGroupId = isAI ? App.Config.UI.EspAIGroupIds : App.Config.UI.EspGroupIds;
            bool drawLabel = drawName || drawDistance || drawHealth || drawGroupId;
            bool drawHeadCircle = isAI ? App.Config.UI.EspHeadCircleAI : App.Config.UI.EspHeadCirclePlayers;

            // Draw skeleton
            if (drawSkeleton && !player.IsError)
            {
                DrawSkeleton(context, player, color);
            }

            // Get bounding box if needed
            RectangleF bbox = default;
            bool hasBox = false;
            if (drawBox || drawLabel)
            {
                hasBox = TryGetBoundingBox(context, player, out bbox);
            }

            // Draw box
            if (drawBox && hasBox)
            {
                context.Ctx.DrawRect(bbox, color, ESPConstants.BoxStrokeWidth);
            }

            // Draw head circle
            if (drawHeadCircle)
            {
                DrawHeadCircle(context, player, color);
            }

            // Draw label
            if (drawLabel)
            {
                DrawPlayerLabel(context, player, distance, color, hasBox ? bbox : (RectangleF?)null, isAI, drawName, drawDistance, drawHealth, drawGroupId);
            }
        }

        private void DrawSkeleton(EspRenderContext context, AbstractPlayer player, DxColor color)
        {
            if (player.Skeleton?.BoneTransforms == null)
                return;

            // Calculate distance-based thickness scaling
            float thickness = ESPConstants.SkeletonStrokeWidth;
            float distance = context.DistanceTo(player.Position);
            float distanceScale = Math.Clamp(
                ESPConstants.SkeletonScaleReferenceDistance / Math.Max(distance, ESPConstants.MinSkeletonScaleDistance),
                ESPConstants.MinSkeletonScaleFactor,
                ESPConstants.MaxSkeletonScaleFactor);
            thickness *= distanceScale;

            foreach (var (from, to) in _boneConnections)
            {
                if (!player.Skeleton.BoneTransforms.TryGetValue(from, out var bone1) ||
                    !player.Skeleton.BoneTransforms.TryGetValue(to, out var bone2))
                    continue;

                var p1 = bone1.Position;
                var p2 = bone2.Position;

                if (p1 == Vector3.Zero || p2 == Vector3.Zero)
                    continue;

                if (context.TryProject(p1, out var s1) && context.TryProject(p2, out var s2))
                {
                    context.Ctx.DrawLine(ToRaw(s1), ToRaw(s2), color, thickness);
                }
            }
        }

        private bool TryGetBoundingBox(EspRenderContext context, AbstractPlayer player, out RectangleF rect)
        {
            rect = default;

            var playerPos = player.Position;
            if (!IsValidPosition(playerPos))
                return false;

            var projectedPoints = new List<SKPoint>();
            var mins = playerPos + new Vector3(-ESPConstants.BoundingBoxExtentXZ, 0, -ESPConstants.BoundingBoxExtentXZ);
            var maxs = playerPos + new Vector3(ESPConstants.BoundingBoxExtentXZ, ESPConstants.BoundingBoxHeight, ESPConstants.BoundingBoxExtentXZ);

            var corners = new[]
            {
                new Vector3(mins.X, mins.Y, mins.Z),
                new Vector3(mins.X, maxs.Y, mins.Z),
                new Vector3(maxs.X, maxs.Y, mins.Z),
                new Vector3(maxs.X, mins.Y, mins.Z),
                new Vector3(maxs.X, maxs.Y, maxs.Z),
                new Vector3(mins.X, maxs.Y, maxs.Z),
                new Vector3(mins.X, mins.Y, maxs.Z),
                new Vector3(maxs.X, mins.Y, maxs.Z)
            };

            foreach (var corner in corners)
            {
                if (context.TryProject(corner, out var s))
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

            if (boxWidth < ESPConstants.MinBoundingBoxDimension || 
                boxHeight < ESPConstants.MinBoundingBoxDimension ||
                boxWidth > context.ScreenWidth * 2f || 
                boxHeight > context.ScreenHeight * 2f)
                return false;

            minX = Math.Clamp(minX, -ESPConstants.BoundingBoxScreenMargin, context.ScreenWidth + ESPConstants.BoundingBoxScreenMargin);
            maxX = Math.Clamp(maxX, -ESPConstants.BoundingBoxScreenMargin, context.ScreenWidth + ESPConstants.BoundingBoxScreenMargin);
            minY = Math.Clamp(minY, -ESPConstants.BoundingBoxScreenMargin, context.ScreenHeight + ESPConstants.BoundingBoxScreenMargin);
            maxY = Math.Clamp(maxY, -ESPConstants.BoundingBoxScreenMargin, context.ScreenHeight + ESPConstants.BoundingBoxScreenMargin);

            rect = new RectangleF(
                minX - ESPConstants.BoundingBoxPadding,
                minY - ESPConstants.BoundingBoxPadding,
                (maxX - minX) + ESPConstants.BoundingBoxPadding * 2f,
                (maxY - minY) + ESPConstants.BoundingBoxPadding * 2f);

            return true;
        }

        private void DrawHeadCircle(EspRenderContext context, AbstractPlayer player, DxColor color)
        {
            if (player.Skeleton?.BoneTransforms == null)
                return;

            if (!player.Skeleton.BoneTransforms.TryGetValue(Bones.HumanHead, out var headBone))
                return;

            var head = headBone.Position;
            if (!IsValidPosition(head))
                return;

            if (!context.TryProject(head, out var headScreen))
                return;

            var headTop = head;
            headTop.Y += ESPConstants.HeadCircleOffset;

            if (!context.TryProject(headTop, out var headTopScreen))
                return;

            var dy = MathF.Abs(headTopScreen.Y - headScreen.Y);
            float radius = Math.Clamp(
                dy * ESPConstants.HeadCircleRadiusMultiplier,
                ESPConstants.MinHeadCircleRadius,
                ESPConstants.MaxHeadCircleRadius);

            context.Ctx.DrawCircle(ToRaw(headScreen), radius, color, filled: false);
        }

        private void DrawPlayerLabel(
            EspRenderContext context,
            AbstractPlayer player,
            float distance,
            DxColor color,
            RectangleF? bbox,
            bool isAI,
            bool showName,
            bool showDistance,
            bool showHealth,
            bool showGroup)
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

            // Build label text
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

            float drawX, drawY;
            var bounds = context.Ctx.MeasureText(text, DxTextSize.Medium);
            int textHeight = Math.Max(1, bounds.Bottom - bounds.Top);

            var labelPos = isAI ? App.Config.UI.EspLabelPositionAI : App.Config.UI.EspLabelPosition;

            if (bbox.HasValue)
            {
                var box = bbox.Value;
                drawX = box.Left + (box.Width / 2f);
                drawY = labelPos == EspLabelPosition.Top
                    ? box.Top - textHeight - ESPConstants.TextPadding
                    : box.Bottom + ESPConstants.TextPadding;
            }
            else if (context.TryProject(player.GetBonePos(Bones.HumanHead), out var headScreen))
            {
                drawX = headScreen.X;
                drawY = labelPos == EspLabelPosition.Top
                    ? headScreen.Y - textHeight - ESPConstants.TextPadding
                    : headScreen.Y + ESPConstants.TextPadding;
            }
            else
            {
                return;
            }

            context.Ctx.DrawText(text, drawX, drawY, color, DxTextSize.Medium, centerX: true);
        }

        #endregion

        #region Player Color Logic

        private DxColor GetPlayerColor(AbstractPlayer player)
        {
            var cfg = App.Config.UI;

            // Special cases (focused, watchlist, streamer, teammate)
            if (player.IsFocused)
                return EspColorHelper.ToColor(SKPaints.PaintAimviewWidgetFocused);

            if (player.Type is PlayerType.SpecialPlayer or PlayerType.Streamer or PlayerType.Teammate)
            {
                return player.Type switch
                {
                    PlayerType.Teammate => EspColorHelper.ToColor(SKPaints.PaintAimviewWidgetTeammate),
                    PlayerType.SpecialPlayer => EspColorHelper.ToColor(SKPaints.PaintAimviewWidgetWatchlist),
                    PlayerType.Streamer => EspColorHelper.ToColor(SKPaints.PaintAimviewWidgetStreamer),
                    _ => EspColorHelper.White
                };
            }

            // Group colors for hostile PMCs
            if (!player.IsAI && cfg.EspGroupColors && player.GroupID >= 0)
            {
                return _groupColorCache.GetOrAdd(player.GroupID, id =>
                {
                    var skColor = _groupPalette[Math.Abs(id) % _groupPalette.Length];
                    return EspColorHelper.ToColor(skColor);
                });
            }

            // Faction colors for PMCs
            if (!player.IsAI && cfg.EspFactionColors && player.IsPmc)
            {
                return player.PlayerSide switch
                {
                    Enums.EPlayerSide.Bear => EspColorHelper.GetBearColor(),
                    Enums.EPlayerSide.Usec => EspColorHelper.GetUsecColor(),
                    _ => EspColorHelper.GetPlayerColor()
                };
            }

            // AI colors
            if (player.IsAI)
            {
                return player.Type switch
                {
                    PlayerType.AIBoss => EspColorHelper.GetBossColor(),
                    PlayerType.AIRaider => EspColorHelper.GetRaiderColor(),
                    _ => EspColorHelper.GetAIColor()
                };
            }

            // Player Scavs
            if (player.Type == PlayerType.PScav)
            {
                return EspColorHelper.GetPlayerScavColor();
            }

            // Default player color
            return EspColorHelper.GetPlayerColor();
        }

        #endregion

        #region Utility

        private static bool IsValidPosition(Vector3 pos)
        {
            return pos != Vector3.Zero &&
                   !float.IsNaN(pos.X) && !float.IsNaN(pos.Y) && !float.IsNaN(pos.Z) &&
                   !float.IsInfinity(pos.X) && !float.IsInfinity(pos.Y) && !float.IsInfinity(pos.Z);
        }

        private static RawVector2 ToRaw(SKPoint point) => new(point.X, point.Y);

        #endregion
    }
}
