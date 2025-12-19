/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using SkiaSharp;
using CameraManagerNew = LoneEftDmaRadar.Tarkov.GameWorld.Camera.CameraManager;

namespace LoneEftDmaRadar.UI.Skia.Rendering
{
    /// <summary>
    /// Handles rendering of player skeletons in the Aimview widget.
    /// </summary>
    public sealed class AimviewPlayerRenderer
    {
        private readonly SKBitmap _bitmap;
        private readonly SKCanvas _canvas;

        private static readonly (Bones From, Bones To)[] BoneConnections = new[]
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

        public AimviewPlayerRenderer(SKBitmap bitmap, SKCanvas canvas)
        {
            _bitmap = bitmap;
            _canvas = canvas;
        }

        public void DrawPlayersAndAIAsSkeletons(LocalPlayer localPlayer, IReadOnlyCollection<AbstractPlayer> allPlayers)
        {
            if (!App.Config.AimviewWidget.ShowAI && !App.Config.AimviewWidget.ShowEnemyPlayers)
                return;

            var players = allPlayers?
                .Where(p => p.IsActive && p.IsAlive && p is not LocalPlayer);

            if (players is null)
                return;

            bool drawHeadCircles = App.Config.AimviewWidget.ShowHeadCircle;

            foreach (var player in players)
            {
                if (!ShouldDrawPlayer(player))
                    continue;

                float distance = Vector3.Distance(localPlayer.Position, player.Position);
                if (App.Config.UI.MaxDistance > 0 && distance > App.Config.UI.MaxDistance)
                    continue;

                if (player.Skeleton?.BoneTransforms == null)
                    continue;

                var paint = GetPlayerPaint(player);
                DrawSkeleton(player, paint, distance);

                if (drawHeadCircles)
                {
                    DrawHeadCircle(player, paint);
                }
            }
        }

        private static bool ShouldDrawPlayer(AbstractPlayer player)
        {
            bool isAI = player.IsAI;
            bool isEnemyPlayer = !isAI && player.IsHostile;

            if (isAI && !App.Config.AimviewWidget.ShowAI)
                return false;
            if (isEnemyPlayer && !App.Config.AimviewWidget.ShowEnemyPlayers)
                return false;

            return true;
        }

        private void DrawSkeleton(AbstractPlayer player, SKPaint paint, float distance)
        {
            float distanceScale = Math.Clamp(
                SKConstants.AimviewSkeletonDistanceBase / Math.Max(distance, SKConstants.AimviewMinSkeletonDistance),
                SKConstants.AimviewSkeletonScaleMin,
                SKConstants.AimviewSkeletonScaleMax);

            foreach (var (from, to) in BoneConnections)
            {
                if (!player.Skeleton.BoneTransforms.TryGetValue(from, out var bone1) ||
                    !player.Skeleton.BoneTransforms.TryGetValue(to, out var bone2))
                    continue;

                var p1 = bone1.Position;
                var p2 = bone2.Position;

                if (p1 == Vector3.Zero || p2 == Vector3.Zero)
                    continue;

                if (TryProject(p1, out var s1) && TryProject(p2, out var s2))
                {
                    float t = Math.Max(SKConstants.AimviewSkeletonLineThicknessMin,
                        SKConstants.AimviewSkeletonLineThicknessBase * distanceScale);
                    paint.StrokeWidth = t;
                    _canvas.DrawLine(s1.X, s1.Y, s2.X, s2.Y, paint);
                }
            }
        }

        private void DrawHeadCircle(AbstractPlayer player, SKPaint paint)
        {
            if (!player.Skeleton.BoneTransforms.TryGetValue(Bones.HumanHead, out var headBone))
                return;

            var head = headBone.Position;
            if (head == Vector3.Zero || float.IsNaN(head.X) || float.IsInfinity(head.X))
                return;

            var headTop = head;
            headTop.Y += SKConstants.AimviewHeadOffset;

            if (TryProject(head, out var headScreen) && TryProject(headTop, out var headTopScreen))
            {
                var dy = MathF.Abs(headTopScreen.Y - headScreen.Y);
                float radius = dy * SKConstants.AimviewHeadRadiusMultiplier;
                radius = Math.Clamp(radius, SKConstants.AimviewHeadRadiusMin, SKConstants.AimviewHeadRadiusMax);

                paint.Style = SKPaintStyle.Stroke;
                _canvas.DrawCircle(headScreen.X, headScreen.Y, radius, paint);
                paint.Style = SKPaintStyle.Fill;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static SKPaint GetPlayerPaint(AbstractPlayer player)
        {
            if (player.IsFocused)
                return SKPaints.PaintAimviewWidgetFocused;
            if (player is LocalPlayer)
                return SKPaints.PaintAimviewWidgetLocalPlayer;

            return player.Type switch
            {
                PlayerType.Teammate => SKPaints.PaintAimviewWidgetTeammate,
                PlayerType.PMC => SKPaints.PaintAimviewWidgetPMC,
                PlayerType.AIScav => SKPaints.PaintAimviewWidgetScav,
                PlayerType.AIRaider => SKPaints.PaintAimviewWidgetRaider,
                PlayerType.AIBoss => SKPaints.PaintAimviewWidgetBoss,
                PlayerType.PScav => SKPaints.PaintAimviewWidgetPScav,
                PlayerType.SpecialPlayer => SKPaints.PaintAimviewWidgetWatchlist,
                PlayerType.Streamer => SKPaints.PaintAimviewWidgetStreamer,
                _ => SKPaints.PaintAimviewWidgetPMC
            };
        }

        private bool TryProject(in Vector3 world, out SKPoint scr)
        {
            scr = default;

            if (world == Vector3.Zero)
                return false;

            if (!CameraManagerNew.WorldToScreen(in world, out var espScreen, false, false))
                return false;

            var viewport = CameraManagerNew.Viewport;
            if (viewport.Width <= 0 || viewport.Height <= 0)
                return false;

            float relX = espScreen.X / viewport.Width;
            float relY = espScreen.Y / viewport.Height;

            scr = new SKPoint(relX * _bitmap.Width, relY * _bitmap.Height);

            if (scr.X < -SKConstants.AimviewEdgeTolerance || scr.X > _bitmap.Width + SKConstants.AimviewEdgeTolerance ||
                scr.Y < -SKConstants.AimviewEdgeTolerance || scr.Y > _bitmap.Height + SKConstants.AimviewEdgeTolerance)
            {
                return false;
            }

            return true;
        }
    }
}
