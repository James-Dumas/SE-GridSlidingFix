using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using Sandbox.Common.ObjectBuilders;
using VRage.Game.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using Draygo.API;
using static Draygo.API.HudAPIv2;

namespace JamacSpaceGameMod
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class GridSlidingFix : MySessionComponentBase
    {
        
        private HashSet<IMyEntity> characterEntities = new HashSet<IMyEntity>();
        private HudAPIv2 TextAPI;
        private HUDMessage message;
        private long lastFrameTime;
    
        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            // Init Text HUD API
            TextAPI = new HudAPIv2();
        }

        public override void BeforeStart()
        {
            // Run for all existing characters
            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);
            foreach(IMyEntity entity in entities)
            {
                OnEntityAdd(entity);
            }
            
            // Register callback for not-yet-existing characters
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;

            lastFrameTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            MyLog.Default.WriteLineAndConsole("GridSlidingFix: Setup complete");
        }

        public override void UpdateBeforeSimulation()
        {
            foreach(IMyEntity entity in characterEntities)
            {
                if(entity != null && entity.Physics != null)
                {
                    try
                    {
                        long frameTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                        float dt = (frameTime - lastFrameTime) / 1000f;
                        lastFrameTime = frameTime;

                        IMyCharacter character = (IMyCharacter) entity;

                        float v = entity.Physics.LinearVelocity.Length();
                        float w = entity.Physics.AngularVelocity.Length();

                        float lateralSlideSpeed = 0.745f * w;
                        float outwardSlideSpeed = 0.0035f * v;
                        Vector3D lateralSlideVelocity = dt * lateralSlideSpeed * Vector3.Normalize(Vector3.Cross(entity.Physics.SupportNormal, entity.Physics.AngularVelocity)) * Math.Min(1f, AngleBetween(entity.Physics.SupportNormal, entity.Physics.AngularVelocity) / (((float) Math.PI) / 2f - 0.45f));
                        Vector3D outwardSlideVelocity = dt * outwardSlideSpeed * Vector3.Normalize(Vector3.Cross(entity.Physics.LinearVelocity, entity.Physics.AngularVelocity)) * (1f - (AngleBetween(entity.Physics.SupportNormal, entity.Physics.AngularVelocity) / ((float) Math.PI) * 2f));
                        if(Double.IsNaN(lateralSlideVelocity.X))
                        {
                            lateralSlideVelocity = Vector3D.Zero;
                        }
                        if(Double.IsNaN(outwardSlideVelocity.X))
                        {
                            outwardSlideVelocity = Vector3D.Zero;
                        }

                        if(v > 0.1f && character.CurrentMovementState != MyCharacterMovementEnum.Jump && character.CurrentMovementState != MyCharacterMovementEnum.Falling && character.CurrentMovementState != MyCharacterMovementEnum.Flying)
                        {
                            entity.PositionComp.SetPosition(entity.PositionComp.GetPosition() - lateralSlideVelocity - outwardSlideVelocity);
                        }
                    }
                    catch(Exception e)
                    {
                        MyLog.Default.WriteLineAndConsole($"Error in GridSlidingFix:\n{e}");
                    }
                }
            }
        }

        public override void Draw()
        {
            foreach(IMyEntity entity in characterEntities)
            {
                try
                {
                    if(entity.Physics != null && TextAPI.Heartbeat)
                    {
                        if(message != null)
                        {
                            message.DeleteMessage();
                        }

                        message = new HUDMessage(new StringBuilder($"Angular Velocity: {entity.Physics.AngularVelocity}\n   Linear Velocity: {entity.Physics.LinearVelocity}\n\nAngular Speed: {entity.Physics.AngularVelocity.Length()}\n   Linear Speed: {entity.Physics.LinearVelocity.Length()}\n\nSupport Normal: {entity.Physics.SupportNormal}\n\nAngle: {AngleBetween(entity.Physics.AngularVelocity, entity.Physics.SupportNormal) / Math.PI * 180}\n\nState: {((IMyCharacter) entity).CurrentMovementState}"), new Vector2D(0.1f, 0.1f));
                    }
                }
                catch(Exception e)
                {
                    MyLog.Default.WriteLineAndConsole($"Error in GridSlidingFix:\n{e}");
                }
            }
        }

        public void OnEntityAdd(IMyEntity entity)
        {
            if(entity != null && entity is IMyCharacter)
            {
                characterEntities.Add(entity);
            }
        }

        public void OnEntityRemove(IMyEntity entity)
        {
            if(entity != null && characterEntities.Contains(entity))
            {
                characterEntities.Remove(entity);
            }
        }

        private static float AngleBetween(Vector3 a, Vector3 b)
        {
            float angle = (float) Math.Min(Math.Acos(Vector3.Dot(a, b) / (a.Length() * b.Length())), Math.Acos(Vector3.Dot(a, -b) / (a.Length() * b.Length())));
            if(Double.IsNaN(angle))
            {
                return 0f;
            }
            
            return angle;
        }
    }
}