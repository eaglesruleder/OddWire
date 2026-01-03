using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable disable

namespace OddWire.GameContent
{
    public class BEBehaviorBrazierMusic : BlockEntityBehavior
    {
        IBrazier beBrazier;
        
        const string trackString = "music/safety-of-a-warm-fire.ogg";
        MusicTrack track;
        
        static bool MusicActive;
        static double MusicLastPlayedTotalHr = -99;
        
        int playerCheckCounter;
        bool fadingOut;

        
        public BEBehaviorBrazierMusic(BlockEntity blockentity) : base(blockentity)
        {
            beBrazier = blockentity as IBrazier;
        }
        
        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (Blockentity.Api.Side == EnumAppSide.Client
            &&  beBrazier != null
                )
                Blockentity.RegisterGameTickListener(OnMusicTick, 3000);
        }
        
        public override void OnBlockRemoved() => StopMusic();
        public override void OnBlockUnloaded() => StopMusic();
        
        bool IsNight => Api.World.Calendar.GetDayLightStrength(Blockentity.Pos.X, Blockentity.Pos.Z) < 0.4;

        bool HasNearbySittingPlayer
        { get {
            var player = (Api as ICoreClientAPI).World.Player.Entity;
            return
                player.Controls.FloorSitting
            &&  player.Pos.DistanceTo(Blockentity.Pos.ToVec3d().Add(0.5, 0.5, 0.5)) < 4;
        } }
        
        private void OnMusicTick(float dt)
        {
            if (MusicActive)
            {
                if(!fadingOut
                &&  track?.IsActive == true
                && (!beBrazier.IsBurning || !HasNearbySittingPlayer || !IsNight))
                {
                    fadingOut = true;
                    track.FadeOut(4, () => StopMusic());
                }
                return;
            }

            double nowHours = Api.World.Calendar.TotalHours;
            if(!IsNight
            || !beBrazier.IsBurning
            ||  nowHours - MusicLastPlayedTotalHr < 6
                ) return;

            if (!HasNearbySittingPlayer)
            {
                playerCheckCounter = 0;
                return;
            }
            
            playerCheckCounter++;
            if (playerCheckCounter < 4)
                return;
            
            MusicActive = true;
            MusicLastPlayedTotalHr = nowHours;
            startLoadingMs = Api.World.ElapsedMilliseconds;
            track = (Api as ICoreClientAPI)?.StartTrack(new AssetLocation(trackString), 120f, EnumSoundType.Music, onTrackLoaded);
        }
        
        
        long startLoadingMs;
        long handlerId;
        bool wasStopped;

        private void onTrackLoaded(ILoadedSound sound)
        {
            if (sound == null
            ||  track == null
                )
            {
                sound?.Dispose();
                return;
            }

            track.Sound = sound;

            // Needed so that the music engine does not dispose the sound
            Api.Event.EnqueueMainThreadTask(() => track.loading = true, "settrackloading");

            long longMsPassed = Api.World.ElapsedMilliseconds - startLoadingMs;
            handlerId = Blockentity.RegisterDelayedCallback((dt) =>
            {
                if (sound.IsDisposed)
                    Api.World.Logger.Notification("brazier track is diposed? o.O");

                if (!wasStopped)
                    sound.Start();

                track.loading = false;

            }, (int)Math.Max(0, 500 - longMsPassed));
        }

        void StopMusic()
        {
            if (Api?.Side != EnumAppSide.Client)
                return;

            if (track?.IsActive == true)
                MusicActive = false;

            track?.Stop();
            track = null;
            Api.Event.UnregisterCallback(handlerId);
            wasStopped = true;
            fadingOut = false;
        }
    }
}