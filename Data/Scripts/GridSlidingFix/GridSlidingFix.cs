using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace JamacSpaceGameMod
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class GridSlidingFix : MySessionComponentBase
    {
        
        private HashSet<IMyEntity> characterEntities = new HashSet<IMyEntity>();
        private Vector3D lastSupportNormal;
        private bool supported = false;

        const float dt = 0.01666667f;

        public override void BeforeStart()
        {
            if(MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer)
                return;

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

            Vector3D lastSupportNormal = Vector3D.Zero;

            MyLog.Default.WriteLineAndConsole("GridSlidingFix: Setup complete");
        }

        protected override void UnloadData()
        {
            // Unregister callback on world close
            foreach(IMyEntity entity in characterEntities)
            {
                OnEntityRemove(entity);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            foreach(IMyEntity entity in characterEntities)
            {
                if(entity != null && entity.Physics != null)
                {
                    try
                    {
                        IMyCharacter character = (IMyCharacter) entity;

                        float v = entity.Physics.LinearVelocity.Length();
                        float w = entity.Physics.AngularVelocity.Length();

                        float lateralSlideSpeed = 0.85f * w;
                        float outwardSlideSpeed = 0.003f * v;
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

                        // check if current movement state is one where player *could* be standing on a surface
                        if(!(character.CurrentMovementState == MyCharacterMovementEnum.Flying
                        || character.CurrentMovementState == MyCharacterMovementEnum.Jump
                        || character.CurrentMovementState == MyCharacterMovementEnum.Falling
                        || character.CurrentMovementState == MyCharacterMovementEnum.Sitting
                        || character.CurrentMovementState == MyCharacterMovementEnum.Died
                        || character.CurrentMovementState == MyCharacterMovementEnum.Ladder
                        || character.CurrentMovementState == MyCharacterMovementEnum.LadderUp
                        || character.CurrentMovementState == MyCharacterMovementEnum.LadderDown
                        || character.CurrentMovementState == MyCharacterMovementEnum.LadderOut
                        ))
                        {
                            if(!supported && (entity.Physics.SupportNormal - lastSupportNormal).Length() > 0.000000000001)
                            {
                                supported = true;
                            }
                        }
                        else
                        {
                            supported = false;
                        }

                        if(v > 0.01f && w > 0.01f && supported)
                        {
                            // correct sliding
                            entity.PositionComp.SetPosition(entity.PositionComp.GetPosition() - lateralSlideVelocity - outwardSlideVelocity);
                        }

                        lastSupportNormal = entity.Physics.SupportNormal;
                    }
                    catch(Exception e)
                    {
                        MyLog.Default.WriteLineAndConsole($"Error in GridSlidingFix:\n{e}");
                    }
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
            if(characterEntities.Contains(entity))
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