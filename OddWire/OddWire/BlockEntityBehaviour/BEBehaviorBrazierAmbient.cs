using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable disable

namespace OddWire.GameContent
{
    public class BEBehaviorBrazierAmbient : BlockEntityBehavior
    {
        const string ambientString = "sounds/environment/fireplace.ogg";
        ILoadedSound ambientSound;

        public BEBehaviorBrazierAmbient(BlockEntity blockentity) : base(blockentity) {}
        
        ~BEBehaviorBrazierAmbient() => ambientSound?.Dispose();
        
        public override void OnBlockRemoved()
        {
            ambientSound?.Stop();
            ambientSound?.Dispose();
        }

        public override void OnBlockUnloaded() => EnableAmbientSounds(false);

        
        public void EnableAmbientSounds(bool enabled)
        {
            if (Api.Side != EnumAppSide.Client)
                return;

            if (!enabled)
            {
                ambientSound?.Stop();
                ambientSound?.Dispose();
                ambientSound = null;
                return;
            }

            if (ambientSound?.IsPlaying == true)
                return;
            
            ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams()
                {Location = new AssetLocation(ambientString)
                ,ShouldLoop = true
                ,Position = Blockentity.Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f)
                ,DisposeOnFinish = false
                ,Volume = 0.66f
                });

            if (ambientSound == null)
                return;
            
            ambientSound.Start();
            ambientSound.PlaybackPosition = ambientSound.SoundLengthSeconds * (float)Api.World.Rand.NextDouble();
        }
    }
}