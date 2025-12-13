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

using LoneEftDmaRadar.Tarkov.Unity.Collections;
using LoneEftDmaRadar.Tarkov.Unity.Structures;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Player
{
    public class ClientPlayer : AbstractPlayer
    {
        /// <summary>
        /// EFT.Profile Address
        /// </summary>
        public ulong Profile { get; }
        /// <summary>
        /// PlayerInfo Address (GClass1044)
        /// </summary>
        public ulong Info { get; }
        /// <summary>
        /// Player name.
        /// </summary>
        public override string Name { get; set; }
        /// <summary>
        /// Account UUID for Human Controlled Players.
        /// </summary>
        public override string AccountID { get; }
        /// <summary>
        /// Group that the player belongs to.
        /// </summary>
        public override int GroupID { get; protected set; } = -1;
        /// <summary>
        /// Player's Faction.
        /// </summary>
        public override Enums.EPlayerSide PlayerSide { get; protected set; }
        /// <summary>
        /// Player is Human-Controlled.
        /// </summary>
        public override bool IsHuman { get; }
        /// <summary>
        /// MovementContext / StateContext
        /// </summary>
        public override ulong MovementContext { get; }
        /// <summary>
        /// Corpse field address..
        /// </summary>
        public override ulong CorpseAddr { get; }
        /// <summary>
        /// Player Rotation Field Address (view angles).
        /// </summary>
        public override ulong RotationAddress { get; }

        internal ClientPlayer(ulong playerBase) : base(playerBase)
        {
            Profile = Memory.ReadPtr(this + Offsets.Player.Profile);
            Info = Memory.ReadPtr(Profile + Offsets.Profile.Info);
            CorpseAddr = this + Offsets.Player.Corpse;
            PlayerSide = (Enums.EPlayerSide)Memory.ReadValue<int>(Info + Offsets.PlayerInfo.Side);
            if (!Enum.IsDefined<Enums.EPlayerSide>(PlayerSide))
                throw new ArgumentOutOfRangeException(nameof(PlayerSide));

            GroupID = GetGroupNumber();
            MovementContext = GetMovementContext();
            RotationAddress = ValidateRotationAddr(MovementContext + Offsets.MovementContext._rotation);
            /// Setup Transform
            var ti = Memory.ReadPtrChain(this, false, _transformInternalChain);
            SkeletonRoot = new UnityTransform(ti);
            var initialPos = SkeletonRoot.UpdatePosition();
            SetupBones();
            // Initialize cached position for fallback (in case skeleton updates fail later)
            _cachedPosition = initialPos;
        }

        public int GetPoseLevel()
        {
             return Memory.ReadValue<int>(MovementContext + 0xD0); // 0xD0 = PoseLevel in MovementContext
        }

        public float GetFov()
        {
            try
            {
                var hands = Memory.ReadPtr(Base + Offsets.Player._handsController);
                if (hands == 0) return 0f;
                
                var anim = Memory.ReadPtr(hands + Offsets.FirearmController.WeaponAnimation);
                if (anim == 0) return 0f;

                return Memory.ReadValue<float>(anim + Offsets.ProceduralWeaponAnimation._fieldOfView);
            }
            catch { return 0f; }
        }

        public ulong PWA
        {
            get
            {
                try
                {
                    return Memory.ReadPtr(Base + Offsets.Player.ProceduralWeaponAnimation);
                }
                catch
                {
                    return 0;
                }
            }
        }

        public bool IsAiming
        {
            get
            {
                try
                {
                    var weaponAnim = PWA;
                    if (weaponAnim == 0)
                    {
                        return false;
                    }

                    bool isAiming = Memory.ReadValue<bool>(weaponAnim + Offsets.ProceduralWeaponAnimation.IsAiming);
                    return isAiming;
                }
                catch
                {
                    return false;
                }
            }
        }

        public int GetCurrentOpticZoom()
        {
            try
            {
                // This is a placeholder for getting the current optic zoom
                // You would need to find the actual offset for the current optic zoom level
                // For now, we can return a default value or try to find the offset
                return 1;
            }
            catch { return 1; }
        }

        private void SetupBones()
        {
            var bonesToRegister = new[]
            {
                Bones.HumanHead,
                Bones.HumanNeck,
                Bones.HumanSpine3,
                Bones.HumanSpine2,
                Bones.HumanSpine1,
                Bones.HumanPelvis,
                Bones.HumanLUpperarm,
                Bones.HumanLForearm1,
                Bones.HumanLForearm2,
                Bones.HumanLPalm,
                Bones.HumanRUpperarm,
                Bones.HumanRForearm1,
                Bones.HumanRForearm2,
                Bones.HumanRPalm,
                Bones.HumanLThigh1,
                Bones.HumanLThigh2,
                Bones.HumanLCalf,
                Bones.HumanLFoot,
                Bones.HumanRThigh1,
                Bones.HumanRThigh2,
                Bones.HumanRCalf,
                Bones.HumanRFoot
            };

            foreach (var bone in bonesToRegister)
            {
                try
                {
                    var chain = _transformInternalChain.ToArray();
                    chain[chain.Length - 2] = UnityList<byte>.ArrStartOffset + (uint)bone * 0x8;
                    
                    var ti = Memory.ReadPtrChain(this, false, chain);
                    var transform = new UnityTransform(ti);
                    PlayerBones.TryAdd(bone, transform);
                }
                catch { }
            }
            
            if (PlayerBones.Count > 0)
            {
                 _verticesCount = PlayerBones.Values.Max(x => x.Count);
                 _verticesCount = Math.Max(_verticesCount, SkeletonRoot.Count);
            }
            Skeleton = new PlayerSkeleton(SkeletonRoot, PlayerBones);
        }

        /// <summary>
        /// Gets player's Group Number.
        /// </summary>
        private int GetGroupNumber()
        {
            try
            {
                var groupIdPtr = Memory.ReadPtr(Info + Offsets.PlayerInfo.GroupId);
                string groupId = Memory.ReadUnicodeString(groupIdPtr);
                return _groups.GetOrAdd(
                    groupId,
                    _ => Interlocked.Increment(ref _lastGroupNumber));
            }
            catch { return -1; } // will return null if Solo / Don't have a team
        }

        /// <summary>
        /// Get Movement Context Instance.
        /// </summary>
        private ulong GetMovementContext()
        {
            var movementContext = Memory.ReadPtr(this + Offsets.Player.MovementContext);
            var player = Memory.ReadPtr(movementContext + Offsets.MovementContext.Player, false);
            if (player != this)
                throw new ArgumentOutOfRangeException(nameof(movementContext));
            return movementContext;
        }

        private static readonly uint[] _transformInternalChain =
        [
            Offsets.Player._playerBody,
            Offsets.PlayerBody.SkeletonRootJoint,
            Offsets.DizSkinningSkeleton._values,
            UnityList<byte>.ArrOffset,
            UnityList<byte>.ArrStartOffset + (uint)Bones.HumanBase * 0x8,
            0x10
        ];
    }
}
