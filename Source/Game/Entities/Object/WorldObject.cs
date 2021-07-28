/*
 * Copyright (C) 2012-2020 CypherCore <http://github.com/CypherCore>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using Framework.Constants;
using Framework.Dynamic;
using Framework.GameMath;
using Game.AI;
using Game.BattleFields;
using Game.Maps;
using Game.Networking;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;
using Game.DataStorage;
using System.Collections;
using Framework.IO;
using System.Runtime.InteropServices;
using Framework.Collections;

namespace Game.Entities
{
    public abstract class WorldObject : WorldLocation, IDisposable
    {
        public WorldObject(bool isWorldObject)
        {
            _name = "";
            m_isWorldObject = isWorldObject;

            m_serverSideVisibility.SetValue(ServerSideVisibilityType.Ghost, GhostVisibilityType.Alive | GhostVisibilityType.Ghost);
            m_serverSideVisibilityDetect.SetValue(ServerSideVisibilityType.Ghost, GhostVisibilityType.Alive);

            ObjectTypeId = TypeId.Object;
            ObjectTypeMask = TypeMask.Object;

            m_movementInfo = new MovementInfo();
            m_updateFlag.Clear();

            m_staticFloorZ = MapConst.VMAPInvalidHeightValue;
        }

        public virtual void Dispose()
        {
            // this may happen because there are many !create/delete
            if (IsWorldObject() && _currMap)
            {
                if (IsTypeId(TypeId.Corpse))
                {
                    Log.outFatal(LogFilter.Misc, "WorldObject.Dispose() Corpse Type: {0} ({1}) deleted but still in map!!", ToCorpse().GetCorpseType(), GetGUID().ToString());
                    Cypher.Assert(false);
                }
                ResetMap();
            }

            if (IsInWorld)
            {
                Log.outFatal(LogFilter.Misc, "WorldObject.Dispose() {0} deleted but still in world!!", GetGUID().ToString());
                if (IsTypeMask(TypeMask.Item))
                    Log.outFatal(LogFilter.Misc, "Item slot {0}", ((Item)this).GetSlot());
                Cypher.Assert(false);
            }

            if (m_objectUpdated)
            {
                Log.outFatal(LogFilter.Misc, "WorldObject.Dispose() {0} deleted but still in update list!!", GetGUID().ToString());
                Cypher.Assert(false);
            }
        }

        public void _Create(ObjectGuid guid)
        {
            if (m_updateValues == null)
                InitializeUpdateFieldValues();

            m_objectUpdated = false;
            m_guid = guid;

            SetUpdateField<ObjectGuid>(ObjectFields.Guid, guid);
        }

        private void InitializeUpdateFieldValues()
        {
            m_updateValues = new UpdateValues[(int)ValuesCount];
            m_changesMask = new((int)ValuesCount);

            if (m_dynamicValuesCount != 0)
            {
                m_dynamicValues = new uint[m_dynamicValuesCount][];
                m_dynamicChangesArrayMask = new BitArray[m_dynamicValuesCount];

                for (var i = 0; i < m_dynamicValuesCount; ++i)
                {
                    m_dynamicValues[i] = new uint[0];
                    m_dynamicChangesArrayMask[i] = new(0);
                    m_dynamicChangesMask[i] = DynamicFieldChangeType.Unchanged;
                }
            }
        }

        public virtual void AddToWorld()
        {
            if (IsInWorld)
                return;

            IsInWorld = true;

            if (GetMap() != null)
                GetMap().GetZoneAndAreaId(_phaseShift, out m_zoneId, out m_areaId, GetPositionX(), GetPositionY(), GetPositionZ());
        }

        public virtual void RemoveFromWorld()
        {
            if (!IsInWorld)
                return;

            if (!ObjectTypeMask.HasAnyFlag(TypeMask.Item | TypeMask.Container))
                DestroyForNearbyPlayers();

            IsInWorld = false;
        }

        public void UpdatePositionData()
        {
            PositionFullTerrainStatus data = new();
            GetMap().GetFullTerrainStatusForPosition(_phaseShift, GetPositionX(), GetPositionY(), GetPositionZ(), data, LiquidHeaderTypeFlags.AllLiquids);
            ProcessPositionDataChanged(data);
        }

        public virtual void ProcessPositionDataChanged(PositionFullTerrainStatus data)
        {
            m_zoneId = m_areaId = data.AreaId;

            var area = CliDB.AreaTableStorage.LookupByKey(m_areaId);
            if (area != null)
                if (area.ParentAreaID != 0)
                    m_zoneId = area.ParentAreaID;

            m_staticFloorZ = data.FloorZ;
        }

        public virtual void BuildCreateUpdateBlockForPlayer(UpdateData data, Player target)
        {
            if (!target)
                return;

            UpdateType updateType = UpdateType.CreateObject;
            TypeId tempObjectType = ObjectTypeId;
            CreateObjectBits flags = m_updateFlag;

            if (target == this)
            {
                flags.ThisIsYou = true;
                flags.ActivePlayer = true;
                tempObjectType = TypeId.ActivePlayer;
            }

            switch (GetGUID().GetHigh())
            {
                case HighGuid.Player:
                case HighGuid.Pet:
                case HighGuid.Corpse:
                case HighGuid.DynamicObject:
                case HighGuid.AreaTrigger:
                case HighGuid.Conversation:
                    updateType = UpdateType.CreateObject2;
                    break;
                case HighGuid.Creature:
                case HighGuid.Vehicle:
                    TempSummon summon = ToUnit().ToTempSummon();
                    if (summon)
                        if (summon.GetSummonerGUID().IsPlayer())
                            updateType = UpdateType.CreateObject2;
                    break;
                case HighGuid.GameObject:
                    if (ToGameObject().GetOwnerGUID().IsPlayer())
                        updateType = UpdateType.CreateObject2;
                    break;
            }

            if (!flags.MovementUpdate && !m_movementInfo.transport.guid.IsEmpty())
                flags.MovementTransport = true;

            if (GetAIAnimKitId() != 0 || GetMovementAnimKitId() != 0 || GetMeleeAnimKitId() != 0)
                flags.AnimKit = true;

            if (flags.Stationary)
            {
                // UPDATETYPE_CREATE_OBJECT2 for some gameobject types...
                if (IsTypeMask(TypeMask.GameObject))
                {
                    switch (ToGameObject().GetGoType())
                    {
                        case GameObjectTypes.Trap:
                        case GameObjectTypes.DuelArbiter:
                        case GameObjectTypes.FlagStand:
                        case GameObjectTypes.FlagDrop:
                            updateType = UpdateType.CreateObject2;
                            break;
                        default:
                            break;
                    }
                }
            }
            Unit unit = ToUnit();
            if (unit)
                if (unit.GetVictim())
                    flags.CombatVictim = true;

            WorldPacket buffer = new();
            buffer.WriteUInt8((byte)updateType);
            buffer.WritePackedGuid(GetGUID());
            buffer.WriteUInt8((byte)tempObjectType);

            BuildMovementUpdate(buffer, flags);

            BuildValuesUpdate(updateType, buffer, target);
            BuildDynamicValuesUpdate(updateType, buffer, target);

            data.AddUpdateBlock(buffer);
        }

        public void SendUpdateToPlayer(Player player)
        {
            // send create update to player
            UpdateData upd = new(player.GetMapId());

            if (player.HaveAtClient(this))
                BuildValuesUpdateBlockForPlayer(upd, player);
            else
                BuildCreateUpdateBlockForPlayer(upd, player);

            upd.BuildPacket(out UpdateObject packet);
            player.SendPacket(packet);
        }

        public void BuildValuesUpdateBlockForPlayer(UpdateData data, Player target)
        {
            WorldPacket buffer = new();
            buffer.WriteUInt8((byte)UpdateType.Values);
            buffer.WritePackedGuid(GetGUID());

            BuildValuesUpdate(UpdateType.Values, buffer, target);
            BuildDynamicValuesUpdate(UpdateType.Values, buffer, target);

            data.AddUpdateBlock(buffer);
        }

        void BuildDestroyUpdateBlock(UpdateData data)
        {
            data.AddDestroyObject(GetGUID());
        }

        public void BuildOutOfRangeUpdateBlock(UpdateData data)
        {
            data.AddOutOfRangeGUID(GetGUID());
        }

        public virtual void DestroyForPlayer(Player target)
        {
            UpdateData updateData = new(target.GetMapId());
            BuildDestroyUpdateBlock(updateData);
            updateData.BuildPacket(out UpdateObject packet);
            target.SendPacket(packet);
        }

        public void BuildMovementUpdate(WorldPacket data, CreateObjectBits flags)
        {
            int PauseTimesCount = 0;

            GameObject go = ToGameObject();
            if (go)
            {
                if (go.GetGoType() == GameObjectTypes.Transport)
                    PauseTimesCount = go.GetGoValue().Transport.StopFrames.Count;
            }

            data.WriteBit(flags.NoBirthAnim);
            data.WriteBit(flags.EnablePortals);
            data.WriteBit(flags.PlayHoverAnim);
            data.WriteBit(flags.MovementUpdate);
            data.WriteBit(flags.MovementTransport);
            data.WriteBit(flags.Stationary);
            data.WriteBit(flags.CombatVictim);
            data.WriteBit(flags.ServerTime);
            data.WriteBit(flags.Vehicle);
            data.WriteBit(flags.AnimKit);
            data.WriteBit(flags.Rotation);
            data.WriteBit(flags.AreaTrigger);
            data.WriteBit(flags.GameObject);
            data.WriteBit(flags.SmoothPhasing);
            data.WriteBit(flags.ThisIsYou);
            data.WriteBit(flags.SceneObject);
            data.WriteBit(flags.ActivePlayer);
            data.WriteBit(flags.Conversation);
            data.FlushBits();

            if (flags.MovementUpdate)
            {
                Unit unit = ToUnit();
                bool HasFallDirection = unit.HasUnitMovementFlag(MovementFlag.Falling);
                bool HasFall = HasFallDirection || unit.m_movementInfo.jump.fallTime != 0;
                bool HasSpline = unit.IsSplineEnabled();

                data.WritePackedGuid(GetGUID());                                         // MoverGUID

                data.WriteUInt32(unit.m_movementInfo.Time);                     // MoveTime
                data.WriteFloat(unit.GetPositionX());
                data.WriteFloat(unit.GetPositionY());
                data.WriteFloat(unit.GetPositionZ());
                data.WriteFloat(unit.GetOrientation());

                data.WriteFloat(unit.m_movementInfo.Pitch);                     // Pitch
                data.WriteFloat(unit.m_movementInfo.SplineElevation);           // StepUpStartElevation

                data.WriteUInt32(0);                                             // RemoveForcesIDs.size()
                data.WriteUInt32(0);                                             // MoveIndex

                //for (public uint i = 0; i < RemoveForcesIDs.Count; ++i)
                //    *data << ObjectGuid(RemoveForcesIDs);

                data.WriteBits((uint)unit.GetUnitMovementFlags(), 30);
                data.WriteBits((uint)unit.GetUnitMovementFlags2(), 18);
                data.WriteBit(!unit.m_movementInfo.transport.guid.IsEmpty());  // HasTransport
                data.WriteBit(HasFall);                                        // HasFall
                data.WriteBit(HasSpline);                                      // HasSpline - marks that the unit uses spline movement
                data.WriteBit(false);                                          // HeightChangeFailed
                data.WriteBit(false);                                          // RemoteTimeValid

                if (!unit.m_movementInfo.transport.guid.IsEmpty())
                    MovementExtensions.WriteTransportInfo(data, unit.m_movementInfo.transport);

                if (HasFall)
                {
                    data.WriteUInt32(unit.m_movementInfo.jump.fallTime);              // Time
                    data.WriteFloat(unit.m_movementInfo.jump.zspeed);                 // JumpVelocity

                    if (data.WriteBit(HasFallDirection))
                    {
                        data.WriteFloat(unit.m_movementInfo.jump.sinAngle);           // Direction
                        data.WriteFloat(unit.m_movementInfo.jump.cosAngle);
                        data.WriteFloat(unit.m_movementInfo.jump.xyspeed);            // Speed
                    }
                }

                data.WriteFloat(unit.GetSpeed(UnitMoveType.Walk));
                data.WriteFloat(unit.GetSpeed(UnitMoveType.Run));
                data.WriteFloat(unit.GetSpeed(UnitMoveType.RunBack));
                data.WriteFloat(unit.GetSpeed(UnitMoveType.Swim));
                data.WriteFloat(unit.GetSpeed(UnitMoveType.SwimBack));
                data.WriteFloat(unit.GetSpeed(UnitMoveType.Flight));
                data.WriteFloat(unit.GetSpeed(UnitMoveType.FlightBack));
                data.WriteFloat(unit.GetSpeed(UnitMoveType.TurnRate));
                data.WriteFloat(unit.GetSpeed(UnitMoveType.PitchRate));

                MovementForces movementForces = unit.GetMovementForces();
                if (movementForces != null)
                {
                    data.WriteInt32(movementForces.GetForces().Count);
                    data.WriteFloat(movementForces.GetModMagnitude());          // MovementForcesModMagnitude
                }
                else
                {
                    data.WriteUInt32(0);
                    data.WriteFloat(1.0f);                                       // MovementForcesModMagnitude
                }

                data.WriteBit(HasSpline);
                data.FlushBits();

                if (movementForces != null)
                    foreach (MovementForce force in movementForces.GetForces())
                        MovementExtensions.WriteMovementForceWithDirection(force, data, unit);

                // HasMovementSpline - marks that spline data is present in packet
                if (HasSpline)
                    MovementExtensions.WriteCreateObjectSplineDataBlock(unit.MoveSpline, data);
            }

            data.WriteInt32(PauseTimesCount);

            if (flags.Stationary)
            {
                WorldObject self = this;
                data.WriteFloat(self.GetStationaryX());
                data.WriteFloat(self.GetStationaryY());
                data.WriteFloat(self.GetStationaryZ());
                data.WriteFloat(self.GetStationaryO());
            }

            if (flags.CombatVictim)
                data.WritePackedGuid(ToUnit().GetVictim().GetGUID());                      // CombatVictim

            if (flags.ServerTime)
            {
                GameObject go1 = ToGameObject();
                /** @TODO Use IsTransport() to also handle type 11 (TRANSPORT)
                    Currently grid objects are not updated if there are no nearby players,
                    this causes clients to receive different PathProgress
                    resulting in players seeing the object in a different position
                */
                if (go1 && go1.ToTransport())                                    // ServerTime
                    data.WriteUInt32(go1.GetGoValue().Transport.PathProgress);
                else
                    data.WriteUInt32(GameTime.GetGameTimeMS());
            }

            if (flags.Vehicle)
            {
                Unit unit = ToUnit();
                data.WriteUInt32(unit.GetVehicleKit().GetVehicleInfo().Id); // RecID
                data.WriteFloat(unit.GetOrientation());                         // InitialRawFacing
            }

            if (flags.AnimKit)
            {
                data.WriteUInt16(GetAIAnimKitId());                        // AiID
                data.WriteUInt16(GetMovementAnimKitId());                  // MovementID
                data.WriteUInt16(GetMeleeAnimKitId());                     // MeleeID
            }

            if (flags.Rotation)
                data.WriteInt64(ToGameObject().GetPackedWorldRotation());                 // Rotation

            if (go)
            {
                for (int i = 0; i < PauseTimesCount; ++i)
                    data.WriteUInt32(go.GetGoValue().Transport.StopFrames[i]);
            }

            if (flags.MovementTransport)
            {
                WorldObject self = this;
                MovementExtensions.WriteTransportInfo(data, self.m_movementInfo.transport);
            }

            if (flags.AreaTrigger)
            {
                AreaTrigger areaTrigger = ToAreaTrigger();
                AreaTriggerMiscTemplate areaTriggerMiscTemplate = areaTrigger.GetMiscTemplate();
                AreaTriggerTemplate areaTriggerTemplate = areaTrigger.GetTemplate();

                data.WriteUInt32(areaTrigger.GetTimeSinceCreated());

                data.WriteVector3(areaTrigger.GetRollPitchYaw());

                bool hasAbsoluteOrientation = areaTriggerTemplate.HasFlag(AreaTriggerFlags.HasAbsoluteOrientation);
                bool hasDynamicShape = areaTriggerTemplate.HasFlag(AreaTriggerFlags.HasDynamicShape);
                bool hasAttached = areaTriggerTemplate.HasFlag(AreaTriggerFlags.HasAttached);
                bool hasFaceMovementDir = areaTriggerTemplate.HasFlag(AreaTriggerFlags.HasFaceMovementDir);
                bool hasFollowsTerrain = areaTriggerTemplate.HasFlag(AreaTriggerFlags.HasFollowsTerrain);
                bool hasUnk1 = areaTriggerTemplate.HasFlag(AreaTriggerFlags.Unk1);
                bool hasTargetRollPitchYaw = areaTriggerTemplate.HasFlag(AreaTriggerFlags.HasTargetRollPitchYaw);
                bool hasScaleCurveID = areaTriggerMiscTemplate.ScaleCurveId != 0;
                bool hasMorphCurveID = areaTriggerMiscTemplate.MorphCurveId != 0;
                bool hasFacingCurveID = areaTriggerMiscTemplate.FacingCurveId != 0;
                bool hasMoveCurveID = areaTriggerMiscTemplate.MoveCurveId != 0;
                bool hasAnimation = areaTriggerTemplate.HasFlag(AreaTriggerFlags.HasAnimID);
                bool hasUnk3 = areaTriggerTemplate.HasFlag(AreaTriggerFlags.Unk3);
                bool hasAnimKitID = areaTriggerTemplate.HasFlag(AreaTriggerFlags.HasAnimKitID);
                bool hasAnimProgress = false;
                bool hasAreaTriggerSphere = areaTriggerTemplate.IsSphere();
                bool hasAreaTriggerBox = areaTriggerTemplate.IsBox();
                bool hasAreaTriggerPolygon = areaTriggerTemplate.IsPolygon();
                bool hasAreaTriggerCylinder = areaTriggerTemplate.IsCylinder();
                bool hasAreaTriggerSpline = areaTrigger.HasSplines();
                bool hasOrbit = areaTrigger.HasOrbit();
                bool hasMovementScript = false;

                data.WriteBit(hasAbsoluteOrientation);
                data.WriteBit(hasDynamicShape);
                data.WriteBit(hasAttached);
                data.WriteBit(hasFaceMovementDir);
                data.WriteBit(hasFollowsTerrain);
                data.WriteBit(hasUnk1);
                data.WriteBit(hasTargetRollPitchYaw);
                data.WriteBit(hasScaleCurveID);
                data.WriteBit(hasMorphCurveID);
                data.WriteBit(hasFacingCurveID);
                data.WriteBit(hasMoveCurveID);
                data.WriteBit(hasAnimation);
                data.WriteBit(hasAnimKitID);
                data.WriteBit(hasUnk3);
                data.WriteBit(hasAnimProgress);
                data.WriteBit(hasAreaTriggerSphere);
                data.WriteBit(hasAreaTriggerBox);
                data.WriteBit(hasAreaTriggerPolygon);
                data.WriteBit(hasAreaTriggerCylinder);
                data.WriteBit(hasAreaTriggerSpline);
                data.WriteBit(hasOrbit);
                data.WriteBit(hasMovementScript);

                if (hasUnk3)
                    data.WriteBit(false);

                data.FlushBits();

                if (hasAreaTriggerSpline)
                {
                    data.WriteUInt32(areaTrigger.GetTimeToTarget());
                    data.WriteUInt32(areaTrigger.GetElapsedTimeForMovement());

                    MovementExtensions.WriteCreateObjectAreaTriggerSpline(areaTrigger.GetSpline(), data);
                }

                if (hasTargetRollPitchYaw)
                    data.WriteVector3(areaTrigger.GetTargetRollPitchYaw());

                if (hasScaleCurveID)
                    data.WriteUInt32(areaTriggerMiscTemplate.ScaleCurveId);

                if (hasMorphCurveID)
                    data.WriteUInt32(areaTriggerMiscTemplate.MorphCurveId);

                if (hasFacingCurveID)
                    data.WriteUInt32(areaTriggerMiscTemplate.FacingCurveId);

                if (hasMoveCurveID)
                    data.WriteUInt32(areaTriggerMiscTemplate.MoveCurveId);

                if (hasAnimation)
                    data.WriteUInt32(areaTriggerMiscTemplate.AnimId);

                if (hasAnimKitID)
                    data.WriteUInt32(areaTriggerMiscTemplate.AnimKitId);

                if (hasAnimProgress)
                    data.WriteUInt32(0);

                if (hasAreaTriggerSphere)
                {
                    data.WriteFloat(areaTriggerTemplate.SphereDatas.Radius);
                    data.WriteFloat(areaTriggerTemplate.SphereDatas.RadiusTarget);
                }

                if (hasAreaTriggerBox)
                {
                    unsafe
                    {
                        data.WriteFloat(areaTriggerTemplate.BoxDatas.Extents[0]);
                        data.WriteFloat(areaTriggerTemplate.BoxDatas.Extents[1]);
                        data.WriteFloat(areaTriggerTemplate.BoxDatas.Extents[2]);

                        data.WriteFloat(areaTriggerTemplate.BoxDatas.ExtentsTarget[0]);
                        data.WriteFloat(areaTriggerTemplate.BoxDatas.ExtentsTarget[1]);
                        data.WriteFloat(areaTriggerTemplate.BoxDatas.ExtentsTarget[2]);
                    }
                }

                if (hasAreaTriggerPolygon)
                {
                    data.WriteInt32(areaTriggerTemplate.PolygonVertices.Count);
                    data.WriteInt32(areaTriggerTemplate.PolygonVerticesTarget.Count);
                    data.WriteFloat(areaTriggerTemplate.PolygonDatas.Height);
                    data.WriteFloat(areaTriggerTemplate.PolygonDatas.HeightTarget);

                    foreach (var vertice in areaTriggerTemplate.PolygonVertices)
                        data.WriteVector2(vertice);

                    foreach (var vertice in areaTriggerTemplate.PolygonVerticesTarget)
                        data.WriteVector2(vertice);
                }

                if (hasAreaTriggerCylinder)
                {
                    data.WriteFloat(areaTriggerTemplate.CylinderDatas.Radius);
                    data.WriteFloat(areaTriggerTemplate.CylinderDatas.RadiusTarget);
                    data.WriteFloat(areaTriggerTemplate.CylinderDatas.Height);
                    data.WriteFloat(areaTriggerTemplate.CylinderDatas.HeightTarget);
                    data.WriteFloat(areaTriggerTemplate.CylinderDatas.LocationZOffset);
                    data.WriteFloat(areaTriggerTemplate.CylinderDatas.LocationZOffsetTarget);
                }

                //if (hasMovementScript)
                //    *data << *areaTrigger->GetMovementScript(); // AreaTriggerMovementScriptInfo

                if (hasOrbit)
                    areaTrigger.GetCircularMovementInfo().Value.Write(data);
            }

            if (flags.GameObject)
            {
                bool bit8 = false;
                uint Int1 = 0;

                GameObject gameObject = ToGameObject();

                data.WriteUInt32(gameObject.GetWorldEffectID());

                data.WriteBit(bit8);
                data.FlushBits();
                if (bit8)
                    data.WriteUInt32(Int1);
            }

            //if (flags.SmoothPhasing)
            //{
            //    data.WriteBit(ReplaceActive);
            //    data.WriteBit(StopAnimKits);
            //    data.WriteBit(HasReplaceObjectt);
            //    data.FlushBits();
            //    if (HasReplaceObject)
            //        *data << ObjectGuid(ReplaceObject);
            //}

            //if (flags.SceneObject)
            //{
            //    data.WriteBit(HasLocalScriptData);
            //    data.WriteBit(HasPetBattleFullUpdate);
            //    data.FlushBits();

            //    if (HasLocalScriptData)
            //    {
            //        data.WriteBits(Data.length(), 7);
            //        data.FlushBits();
            //        data.WriteString(Data);
            //    }

            //    if (HasPetBattleFullUpdate)
            //    {
            //        for (std::size_t i = 0; i < 2; ++i)
            //        {
            //            *data << ObjectGuid(Players[i].CharacterID);
            //            *data << int32(Players[i].TrapAbilityID);
            //            *data << int32(Players[i].TrapStatus);
            //            *data << uint16(Players[i].RoundTimeSecs);
            //            *data << int8(Players[i].FrontPet);
            //            *data << uint8(Players[i].InputFlags);

            //            data.WriteBits(Players[i].Pets.size(), 2);
            //            data.FlushBits();
            //            for (std::size_t j = 0; j < Players[i].Pets.size(); ++j)
            //            {
            //                *data << ObjectGuid(Players[i].Pets[j].BattlePetGUID);
            //                *data << int32(Players[i].Pets[j].SpeciesID);
            //                *data << int32(Players[i].Pets[j].DisplayID);
            //                *data << int32(Players[i].Pets[j].CollarID);
            //                *data << int16(Players[i].Pets[j].Level);
            //                *data << int16(Players[i].Pets[j].Xp);
            //                *data << int32(Players[i].Pets[j].CurHealth);
            //                *data << int32(Players[i].Pets[j].MaxHealth);
            //                *data << int32(Players[i].Pets[j].Power);
            //                *data << int32(Players[i].Pets[j].Speed);
            //                *data << int32(Players[i].Pets[j].NpcTeamMemberID);
            //                *data << uint16(Players[i].Pets[j].BreedQuality);
            //                *data << uint16(Players[i].Pets[j].StatusFlags);
            //                *data << int8(Players[i].Pets[j].Slot);

            //                *data << uint32(Players[i].Pets[j].Abilities.size());
            //                *data << uint32(Players[i].Pets[j].Auras.size());
            //                *data << uint32(Players[i].Pets[j].States.size());
            //                for (std::size_t k = 0; k < Players[i].Pets[j].Abilities.size(); ++k)
            //                {
            //                    *data << int32(Players[i].Pets[j].Abilities[k].AbilityID);
            //                    *data << int16(Players[i].Pets[j].Abilities[k].CooldownRemaining);
            //                    *data << int16(Players[i].Pets[j].Abilities[k].LockdownRemaining);
            //                    *data << int8(Players[i].Pets[j].Abilities[k].AbilityIndex);
            //                    *data << uint8(Players[i].Pets[j].Abilities[k].Pboid);
            //                }

            //                for (std::size_t k = 0; k < Players[i].Pets[j].Auras.size(); ++k)
            //                {
            //                    *data << int32(Players[i].Pets[j].Auras[k].AbilityID);
            //                    *data << uint32(Players[i].Pets[j].Auras[k].InstanceID);
            //                    *data << int32(Players[i].Pets[j].Auras[k].RoundsRemaining);
            //                    *data << int32(Players[i].Pets[j].Auras[k].CurrentRound);
            //                    *data << uint8(Players[i].Pets[j].Auras[k].CasterPBOID);
            //                }

            //                for (std::size_t k = 0; k < Players[i].Pets[j].States.size(); ++k)
            //                {
            //                    *data << uint32(Players[i].Pets[j].States[k].StateID);
            //                    *data << int32(Players[i].Pets[j].States[k].StateValue);
            //                }

            //                data.WriteBits(Players[i].Pets[j].CustomName.length(), 7);
            //                data.FlushBits();
            //                data.WriteString(Players[i].Pets[j].CustomName);
            //            }
            //        }

            //        for (std::size_t i = 0; i < 3; ++i)
            //        {
            //            *data << uint32(Enviros[j].Auras.size());
            //            *data << uint32(Enviros[j].States.size());
            //            for (std::size_t j = 0; j < Enviros[j].Auras.size(); ++j)
            //            {
            //                *data << int32(Enviros[j].Auras[j].AbilityID);
            //                *data << uint32(Enviros[j].Auras[j].InstanceID);
            //                *data << int32(Enviros[j].Auras[j].RoundsRemaining);
            //                *data << int32(Enviros[j].Auras[j].CurrentRound);
            //                *data << uint8(Enviros[j].Auras[j].CasterPBOID);
            //            }

            //            for (std::size_t j = 0; j < Enviros[j].States.size(); ++j)
            //            {
            //                *data << uint32(Enviros[i].States[j].StateID);
            //                *data << int32(Enviros[i].States[j].StateValue);
            //            }
            //        }

            //        *data << uint16(WaitingForFrontPetsMaxSecs);
            //        *data << uint16(PvpMaxRoundTime);
            //        *data << int32(CurRound);
            //        *data << uint32(NpcCreatureID);
            //        *data << uint32(NpcDisplayID);
            //        *data << int8(CurPetBattleState);
            //        *data << uint8(ForfeitPenalty);
            //        *data << ObjectGuid(InitialWildPetGUID);
            //        data.WriteBit(IsPVP);
            //        data.WriteBit(CanAwardXP);
            //        data.FlushBits();
            //    }
            //}

            if (flags.ActivePlayer)
            {
                bool hasSceneInstanceIDs = false;
                bool hasRuneState = false;
                bool hasUnkBCC = false;

                data.WriteBit(hasSceneInstanceIDs);
                data.WriteBit(hasRuneState);
                data.WriteBit(hasUnkBCC);
                data.FlushBits();
                //if (HasSceneInstanceIDs)
                //{
                //    *data << uint32(SceneInstanceIDs.size());
                //    for (std::size_t i = 0; i < SceneInstanceIDs.size(); ++i)
                //        *data << uint32(SceneInstanceIDs[i]);
                //}
                //if (hasRuneState)
                //{
                //    *data << uint8((1 << maxRunes) - 1);
                //    *data << uint8(player->GetRunesState());
                //    *data << uint32(maxRunes);
                //    for (std::size_t i = 0; i < maxRunes; ++i)
                //        *data << uint8((baseCd - float(player->GetruneCooldown(i))) / baseCd * 255);
                //}
                //if(hasUnkBCC)
                //{
                //    for (std::size_t i = 0; i < 132; ++i)
                //        *data << int32(0);
                //}
            }

            if (flags.Conversation)
            {
                Conversation self = ToConversation();
                if (data.WriteBit(self.GetTextureKitId() != 0))
                    data.WriteUInt32(self.GetTextureKitId());
                data.FlushBits();
            }
        }

        #region UpdateFields Get/Set Methods
        public void SetUpdateField<T>(object index, T value, byte offset = 0, bool update = false) where T : new()
        {
            if (value is sbyte || value is byte)
            {
                if (offset > 3)
                {
                    Log.outError(LogFilter.Server, $"SetUpdateField<UInt8>: Wrong offset: {offset}");
                    return;
                }

                if (update || ((byte)(GetUpdateField<uint>(index) >> (offset * 8)) != Convert.ToByte(value)))
                {
                    if (value is byte)
                    {
                        m_updateValues[(int)index].UnsignedValue &= ~(uint)(0xFF << (offset * 8));
                        m_updateValues[(int)index].UnsignedValue |= Convert.ToUInt32(value) << (offset * 8);
                    }
                    else
                    {
                        m_updateValues[(int)index].SignedValue &= ~(0xFF << (offset * 8));
                        m_updateValues[(int)index].SignedValue |= Convert.ToInt32(value) << (offset * 8);
                    }

                    m_changesMask.Set((int)index, true);
                }
            }
            else if (value is short || value is ushort)
            {
                if (offset > 2)
                {
                    Log.outError(LogFilter.Server, $"SetUpdateField<UInt16>: Wrong offset: {offset}");
                    return;
                }

                if (update || ((ushort)(GetUpdateField<uint>(index) >> (offset * 16)) != Convert.ToUInt16(value)))
                {
                    if (value is ushort)
                    {
                        m_updateValues[(int)index].UnsignedValue &= ~((uint)0xFFFF << (offset * 16));
                        m_updateValues[(int)index].UnsignedValue |= Convert.ToUInt32(value) << (offset * 16);
                    }
                    else
                    {
                        m_updateValues[(int)index].SignedValue &= ~(0xFFFF << (offset * 16));
                        m_updateValues[(int)index].SignedValue |= Convert.ToInt32(value) << (offset * 16);
                    }

                    m_changesMask.Set((int)index, true);
                }
            }
            else if (value is int || value is uint || value is float)
            {
                if (update || (value is uint && GetUpdateField<uint>(index) != Convert.ToUInt32(value)))
                {
                    m_updateValues[(int)index].UnsignedValue = Convert.ToUInt32(value);
                    m_changesMask.Set((int)index, true);
                }
                else if (update || (value is int && GetUpdateField<int>(index) != Convert.ToInt32(value)))
                {
                    m_updateValues[(int)index].SignedValue = Convert.ToInt32(value);
                    m_changesMask.Set((int)index, true);
                }
                else if (update || (value is float && GetUpdateField<float>(index) != Convert.ToSingle(value)))
                {
                    m_updateValues[(int)index].FloatValue = Convert.ToSingle(value);
                    m_changesMask.Set((int)index, true);
                }
            }
            else if (value is long || value is ulong)
            {
                if (update || (value is long && GetUpdateField<long>(index) != Convert.ToInt64(value)))
                {
                    m_updateValues[(int)index].SignedValue = (int)MathFunctions.Pair64_LoPart(Convert.ToUInt64(value));
                    m_updateValues[(int)index + 1].SignedValue = (int)MathFunctions.Pair64_HiPart(Convert.ToUInt64(value));
                    m_changesMask.Set((int)index, true);
                    m_changesMask.Set((int)index + 1, true);
                }
                else if (update || (value is ulong && GetUpdateField<ulong>(index) != Convert.ToUInt64(value)))
                {
                    m_updateValues[(int)index].UnsignedValue = MathFunctions.Pair64_LoPart(Convert.ToUInt64(value));
                    m_updateValues[(int)index + 1].UnsignedValue = MathFunctions.Pair64_HiPart(Convert.ToUInt64(value));
                    m_changesMask.Set((int)index, true);
                    m_changesMask.Set((int)index + 1, true);
                }
            }
            else if (value is ObjectGuid)
            {
                var objectValue = (ObjectGuid)Convert.ChangeType(value, typeof(ObjectGuid));
                if (GetUpdateField<ObjectGuid>(index) != objectValue)
                {
                    SetUpdateField<ulong>(index, objectValue.GetLowValue());
                    SetUpdateField<ulong>((int)index + 2, objectValue.GetHighValue());
                }
            }

            if (!update)
                AddToObjectUpdateIfNeeded();
        }

        public T GetUpdateField<T>(object index, byte offset = 0) where T : new() =>
            default(T) switch
            {
                sbyte => (T)Convert.ChangeType(Convert.ToSByte(m_updateValues[(int)index].SignedValue >> (offset * 8)) & 0xFF, typeof(T)),
                byte => (T)Convert.ChangeType(Convert.ToByte(m_updateValues[(int)index].UnsignedValue >> (offset * 8)) & 0xFF, typeof(T)),
                short => (T)Convert.ChangeType(Convert.ToInt16(m_updateValues[(int)index].SignedValue >> (offset * 16)) & 0xFF, typeof(T)),
                ushort => (T)Convert.ChangeType(Convert.ToUInt16(m_updateValues[(int)index].UnsignedValue >> (offset * 16)) & 0xFF, typeof(T)),
                int => (T)Convert.ChangeType(Convert.ToInt32(m_updateValues[(int)index].SignedValue), typeof(T)),
                uint => (T)Convert.ChangeType(Convert.ToUInt32(m_updateValues[(int)index].UnsignedValue), typeof(T)),
                float => (T)Convert.ChangeType(Convert.ToSingle(m_updateValues[(int)index].FloatValue), typeof(T)),
                long => (T)Convert.ChangeType(Convert.ToInt64(m_updateValues[(int)index + 1].SignedValue) << 32 | (uint)m_updateValues[(int)index].SignedValue, typeof(T)),
                ulong => (T)Convert.ChangeType(Convert.ToUInt64(m_updateValues[(int)index + 1].UnsignedValue) << 32 | m_updateValues[(int)index].UnsignedValue, typeof(T)),
                ObjectGuid => (T)Convert.ChangeType(new ObjectGuid(GetUpdateField<ulong>((int)index + 2), GetUpdateField<ulong>(index)), typeof(T)),
                _ => throw new Exception($"{typeof(T)} is not implemented in GetUpdateField<T>"),
            };


        public void _LoadIntoDataField(string data, uint startOffset, uint count)
        {
            if (string.IsNullOrEmpty(data))
                return;

            var lines = new StringArray(data, ' ');
            if (lines.Length != count)
                return;

            for (var index = 0; index < count; ++index)
            {
                if (uint.TryParse(lines[index], out uint value))
                {
                    m_updateValues[(int)startOffset + index].UnsignedValue = value;
                    m_changesMask.Set((int)(startOffset + index), true);
                }
            }
        }
        public bool HasFlag(object index, object flag)
        {
            if ((int)index >= ValuesCount)
                return false;

            return (GetUpdateField<uint>(index) & (uint)flag) != 0;
        }

        public void AddFlag(object index, object newFlag)
        {
            var oldValue = m_updateValues[(int)index].UnsignedValue;
            var newValue = oldValue | Convert.ToUInt32(newFlag);

            if (oldValue != newValue)
                SetUpdateField<uint>(index, newValue);
        }

        public void RemoveFlag(object index, object newFlag)
        {
            var oldValue = m_updateValues[(int)index].UnsignedValue;
            var newValue = oldValue & ~Convert.ToUInt32(newFlag);

            if (oldValue != newValue)
            {
                SetUpdateField<uint>(index, newValue);
            }
        }

        public void ApplyFlag<T>(object index, T flag, bool apply)
        {
            if (apply)
                AddFlag(index, flag);
            else
                RemoveFlag(index, flag);
        }

        public void AddFlag64(object index, object newFlag)
        {
            var oldValue = GetUpdateField<ulong>(index);
            var newValue = oldValue | Convert.ToUInt64(newFlag);

            if (oldValue != newValue)
                SetUpdateField<ulong>(index, newValue);
        }

        public void RemoveFlag64(object index, object newFlag)
        {
            var oldValue = GetUpdateField<ulong>(index);
            var newValue = oldValue & ~Convert.ToUInt64(newFlag);

            if (oldValue != newValue)
                SetUpdateField<ulong>(index, newValue);
        }

        public void ApplyFlag64<T>(object index, T flag, bool apply)
        {
            if (apply)
                AddFlag(index, flag);
            else
                RemoveFlag(index, flag);
        }

        public void AddByteFlag(object index, byte offset, object newFlag)
        {
            if (offset > 4)
            {
                Log.outError(LogFilter.Server, $"Object.SetByteFlag: Wrong offset {offset}");
                return;
            }

            if (((byte)m_updateValues[(int)index].UnsignedValue >> (offset * 8) & (int)newFlag) == 0)
            {
                m_updateValues[(int)index].UnsignedValue |= (uint)newFlag << (offset * 8);
                m_changesMask.Set((int)index, true);

                AddToObjectUpdateIfNeeded();
            }
        }

        public void RemoveByteFlag(object index, byte offset, object oldFlag)
        {
            if (offset > 4)
            {
                Log.outError(LogFilter.Server, $"Object.RemoveByteFlag: Wrong offset {offset}");
                return;
            }

            if (((byte)m_updateValues[(int)index].UnsignedValue >> (offset * 8) & (int)oldFlag) != 0)
            {
                m_updateValues[(int)index].UnsignedValue &= ~((uint)oldFlag << (offset * 8));
                m_changesMask.Set((int)index, true);

                AddToObjectUpdateIfNeeded();
            }
        }

        public virtual void BuildValuesUpdate(UpdateType updateType, ByteBuffer data, Player target)
        {
            if (!target)
                return;

            var fieldBuffer = new ByteBuffer();
            var updateMask = new UpdateMask(ValuesCount);

            var visibleFlag = GetUpdateFieldData(target, out var flags);
            for (var index = 0; index < ValuesCount; ++index)
            {
                if (m_fieldNotifyFlags.HasAnyFlag(flags[index]) ||
                    ((updateType == UpdateType.Values ? m_changesMask.Get(index) : m_updateValues[index].UnsignedValue != 0) && flags[index].HasAnyFlag(visibleFlag)))
                {
                    updateMask.SetBit(index);
                    fieldBuffer.WriteUInt32(m_updateValues[index].UnsignedValue);
                }
            }

            updateMask.AppendToPacket(data);
            data.WriteBytes(fieldBuffer);
        }

        public virtual void BuildDynamicValuesUpdate(UpdateType updateType, WorldPacket data, Player target)
        {
            if (!target)
                return;

            var valueCount = m_dynamicValuesCount;
            if (target != this && !IsPlayer())
                valueCount = (uint)PlayerDynamicFields.End;

            var fieldBuffer = new ByteBuffer();
            var updateMask = new UpdateMask(valueCount);

            var visibleFlag = GetDynamicUpdateFieldData(target, out var flags);
            for (var index = 0; index < valueCount; ++index)
            {
                var valueBuffer = new ByteBuffer();
                var values = m_dynamicValues[index];
                if (m_fieldNotifyFlags.HasAnyFlag(flags[index]) ||
                    ((updateType == UpdateType.Values ? m_dynamicChangesMask[index] != DynamicFieldChangeType.Unchanged : !values.Empty()) && flags[index].HasAnyFlag(visibleFlag)))
                {
                    updateMask.SetBit(index);

                    var arrayMask = new DynamicUpdateMask((uint)values.Length);
                    arrayMask.EncodeDynamicFieldChangeType(m_dynamicChangesMask[index], updateType);
                    if (updateType == UpdateType.Values && m_dynamicChangesMask[index] == DynamicFieldChangeType.ValueAndSizeChanged)
                        arrayMask.SetCount(values.Length);

                    for (var v = 0; v < values.Length; ++v)
                    {
                        if (updateType != UpdateType.Values || m_dynamicChangesArrayMask[index].Get(v))
                        {
                            arrayMask.SetBit(v);
                            valueBuffer.WriteUInt32(values[v]);
                        }
                    }

                    arrayMask.AppendToPacket(fieldBuffer);
                    fieldBuffer.WriteBytes(valueBuffer);
                }
            }

            updateMask.AppendToPacket(data);
            data.WriteBytes(fieldBuffer);
        }

        public void ApplyModUpdateField<T>(object index, T val, bool apply, byte offset = 0) where T : new()
        {
            if (val is int || val is uint)
            {
                var cur = GetUpdateField<int>(index) + (apply ? Convert.ToInt32(val) : -Convert.ToInt32(val));
                if (cur < 0)
                    cur = 0;
                SetUpdateField<int>(index, cur);
            }
            else if (val is float)
            {
                var cur = GetUpdateField<float>(index) + (apply ? Convert.ToSingle(val) : -Convert.ToSingle(val));
                SetUpdateField<float>(index, cur);
            }
            else if (val is short || val is ushort)
            {
                var cur = (short)(GetUpdateField<short>(index, offset) + (apply ? Convert.ToInt16(val) : -Convert.ToInt16(val)));
                if (cur < 0)
                    cur = 0;
                SetUpdateField<short>(index, cur, offset);
            }
        }

        public void SetStatUpdateField<T>(object index, T val) where T : new()
        {
            if (val is int intVal)
            {
                if (intVal < 0)
                    intVal = 0;
                val = (T)Convert.ChangeType(intVal, typeof(T));
            }
            else if (val is float floatVal)
            {
                if (floatVal < 0.0f)
                    floatVal = 0.0f;
                val = (T)Convert.ChangeType(floatVal, typeof(T));
            }
            else
                throw new Exception($"Object::SetStatUpdateField<T>: Unknown type: {typeof(T)}");

            SetUpdateField<T>(index, val);
        }

        public void ApplyPercentModUpdateField(object index, float val, bool apply)
        {
            float value = GetUpdateField<float>(index);
            MathFunctions.ApplyPercentModFloatVar(ref value, val, apply);
            SetUpdateField<float>(index, value);
        }

        public uint[] GetDynamicValues(object index) => m_dynamicValues[(int)index];

        public uint GetDynamicValue(object index, ushort offset)
        {
            if (offset >= m_dynamicValues[(int)index].Length)
                return 0;

            return m_dynamicValues[(int)index][offset];
        }

        public bool HasDynamicValue(object index, uint value)
        {
            var values = m_dynamicValues[(int)index];
            for (var i = 0; i < values.Length; ++i)
                if (values[i] == value)
                    return true;

            return false;
        }

        public void AddDynamicValue(object index, uint value) =>
            SetDynamicValue(index, (byte)m_dynamicValues[(int)index].Length, value);

        public void SetDynamicValue(object index, ushort offset, uint value)
        {
            var changeType = DynamicFieldChangeType.ValueChanged;
            if (m_dynamicValues[(int)index].Length <= offset)
            {
                Array.Resize(ref m_dynamicValues[(int)index], offset + 1);
                changeType = DynamicFieldChangeType.ValueAndSizeChanged;
            }

            if (m_dynamicChangesArrayMask[(int)index].Count <= offset)
                m_dynamicChangesArrayMask[(int)index].Length = offset + 1;

            if (m_dynamicValues[(int)index][offset] != value || changeType == DynamicFieldChangeType.ValueAndSizeChanged)
            {
                m_dynamicValues[(int)index][offset] = value;
                m_dynamicChangesMask[(int)index] = changeType;
                m_dynamicChangesArrayMask[(int)index].Set(offset, true);

                AddToObjectUpdateIfNeeded();
            }
        }

        public void RemoveDynamicValue(object index, uint value)
        {
            for (var i = 0; i < m_dynamicValues[(int)index].Length; ++i)
            {
                if (m_dynamicValues[(int)index][i] == value)
                {
                    m_dynamicValues[(int)index][i] = 0;
                    m_dynamicChangesMask[(int)index] = DynamicFieldChangeType.ValueChanged;
                    m_dynamicChangesArrayMask[(int)index].Set(i, true);

                    AddToObjectUpdateIfNeeded();
                }
            }
        }

        public void ClearDynamicValue(object index)
        {
            if (!m_dynamicValues[(int)index].Empty())
            {
                m_dynamicValues[(int)index] = new uint[0];
                m_dynamicChangesMask[(int)index] = DynamicFieldChangeType.ValueAndSizeChanged;
                m_dynamicChangesArrayMask[(int)index].SetAll(false);

                AddToObjectUpdateIfNeeded();
            }
        }

        public List<T> GetDynamicStructuredValues<T>(object index)
        {
            var values = m_dynamicValues[(int)index];
            return values.DeserializeObjects<T>();
        }

        public T GetDynamicStructuredValue<T>(object index, ushort offset) =>
            GetDynamicStructuredValues<T>(index)[offset];

        public void SetDynamicStructuredValue<T>(object index, ushort offset, T value)
        {
            var blockCount = Marshal.SizeOf<T>() / sizeof(uint);
            SetDynamicValue(index, (ushort)((offset + 1) * blockCount - 1), 0); // reserve space

            for (var i = 0; i < blockCount; ++i)
                SetDynamicValue(index, (ushort)(offset * blockCount + i), value.SerializeObject()[i]);
        }

        public void AddDynamicStructuredValue<T>(object index, T value)
        {
            var blockCount = Marshal.SizeOf<T>() / sizeof(uint);
            var offset = (ushort)(m_dynamicValues[(int)index].Length / blockCount);
            SetDynamicValue(index, (ushort)((offset + 1) * blockCount - 1), 0); // reserve space
            for (var i = 0; i < blockCount; ++i)
                SetDynamicValue(index, (ushort)(offset * blockCount + i), value.SerializeObject()[i]);
        }

        public void DoWithSuppressingObjectUpdates(Action action)
        {
            bool wasUpdatedBeforeAction = m_objectUpdated;
            action();
            if (m_objectUpdated && !wasUpdatedBeforeAction)
            {
                RemoveFromObjectUpdate();
                m_objectUpdated = false;
            }
        }

        public void ClearUpdateMask(bool remove)
        {
            m_changesMask.SetAll(false);
            for (int i = 0; i < m_dynamicValuesCount; ++i)
            {
                m_dynamicChangesMask[i] = DynamicFieldChangeType.Unchanged;
                m_dynamicChangesArrayMask[i].SetAll(false);
            }

            if (m_objectUpdated)
            {
                if (remove)
                    RemoveFromObjectUpdate();

                m_objectUpdated = false;
            }
        }

        public void ForceValuesUpdateAtIndex(object index)
        {
            m_changesMask.Set((int)index, true);
            AddToObjectUpdateIfNeeded();
        }

        public void AddToObjectUpdateIfNeeded()
        {
            if (IsInWorld && !m_objectUpdated)
            {
                AddToObjectUpdate();
                m_objectUpdated = true;
            }
        }

        public void BuildFieldsUpdate(Player player, Dictionary<Player, UpdateData> data_map)
        {
            if (!data_map.ContainsKey(player))
                data_map.Add(player, new UpdateData(player.GetMapId()));

            BuildValuesUpdateBlockForPlayer(data_map[player], player);
        }

        public void ForceUpdateFieldChange() => AddToObjectUpdateIfNeeded();

        MirrorFlags GetUpdateFieldData(Player target, out MirrorFlags[] flags)
        {
            var visibleFlag = MirrorFlags.All;
            flags = null;

            if (target == this)
                visibleFlag |= MirrorFlags.Self;

            switch (GetTypeId())
            {
                case TypeId.Item:
                case TypeId.Container:
                    flags = UpdateFieldFlags.ContainerUpdateFieldFlags;
                    if (((Item)this).GetOwnerGUID() == target.GetGUID())
                        visibleFlag |= MirrorFlags.Owner | MirrorFlags.ItemOwner;
                    break;
                case TypeId.Unit:
                case TypeId.Player:
                {
                    Player plr = ToUnit().GetCharmerOrOwnerPlayerOrPlayerItself();
                    flags = UpdateFieldFlags.UnitUpdateFieldFlags;
                    if (ToUnit().GetOwnerGUID() == target.GetGUID())
                        visibleFlag |= MirrorFlags.Owner;

                    if (HasDynamicFlag(UnitDynFlags.SpecialInfo))
                        if (ToUnit().HasAuraTypeWithCaster(AuraType.Empathy, target.GetGUID()))
                            visibleFlag |= MirrorFlags.Empath;

                    if (plr != null && plr.IsInSameGroupWith(target))
                        visibleFlag |= MirrorFlags.Party;
                    break;
                }
                case TypeId.GameObject:
                    flags = UpdateFieldFlags.GameObjectUpdateFieldFlags;
                    if (ToGameObject().GetOwnerGUID() == target.GetGUID())
                        visibleFlag |= MirrorFlags.Owner;
                    break;
                case TypeId.DynamicObject:
                    flags = UpdateFieldFlags.DynamicObjectUpdateFieldFlags;
                    if (ToDynamicObject().GetCasterGUID() == target.GetGUID())
                        visibleFlag |= MirrorFlags.Owner;
                    break;
                case TypeId.Corpse:
                    flags = UpdateFieldFlags.CorpseUpdateFieldFlags;
                    if (ToCorpse().GetOwnerGUID() == target.GetGUID())
                        visibleFlag |= MirrorFlags.Owner;
                    break;
                case TypeId.AreaTrigger:
                    flags = UpdateFieldFlags.AreaTriggerUpdateFieldFlags;
                    break;
                case TypeId.SceneObject:
                    flags = UpdateFieldFlags.SceneObjectUpdateFieldFlags;
                    break;
                case TypeId.Conversation:
                    flags = UpdateFieldFlags.ConversationUpdateFieldFlags;
                    break;
                case TypeId.Object:
                case TypeId.ActivePlayer:
                    Cypher.Assert(true);
                    break;
            }
            return visibleFlag;
        }

        public MirrorFlags GetDynamicUpdateFieldData(Player target, out MirrorFlags[] flags)
        {
            var visibleFlag = MirrorFlags.All;

            if (target == this)
                visibleFlag |= MirrorFlags.Self;

            switch (GetTypeId())
            {
                case TypeId.Item:
                case TypeId.Container:
                    flags = UpdateFieldFlags.ItemDynamicUpdateFieldFlags;
                    if (((Item)this).GetOwnerGUID() == target.GetGUID())
                        visibleFlag |= MirrorFlags.Owner | MirrorFlags.ItemOwner;
                    break;
                case TypeId.Unit:
                case TypeId.Player:
                {
                    Player plr = ToUnit().GetCharmerOrOwnerPlayerOrPlayerItself();
                    flags = UpdateFieldFlags.UnitDynamicUpdateFieldFlags;
                    if (ToUnit().GetOwnerGUID() == target.GetGUID())
                        visibleFlag |= MirrorFlags.Owner;

                    if (HasDynamicFlag(UnitDynFlags.SpecialInfo))
                        if (ToUnit().HasAuraTypeWithCaster(AuraType.Empathy, target.GetGUID()))
                            visibleFlag |= MirrorFlags.Empath;

                    if (plr && plr.IsInSameRaidWith(target))
                        visibleFlag |= MirrorFlags.Party;
                    break;
                }
                case TypeId.GameObject:
                    flags = UpdateFieldFlags.GameObjectDynamicUpdateFieldFlags;
                    break;
                case TypeId.Conversation:
                    flags = UpdateFieldFlags.ConversationDynamicUpdateFieldFlags;

                    if (ToConversation().GetCreatorGuid() == target.GetGUID())
                        visibleFlag |= MirrorFlags.Unk0x100;
                    break;
                default:
                    flags = null;
                    break;
            }

            return visibleFlag;
        }

        public void AddFieldNotifyFlag(MirrorFlags flags) => m_fieldNotifyFlags |= flags;
        public void RemoveFieldNotifyFlag(MirrorFlags flags) => m_fieldNotifyFlags &= ~flags;

        #endregion

        public bool IsWorldObject()
        {
            if (m_isWorldObject)
                return true;

            if (IsTypeId(TypeId.Unit) && ToCreature().m_isTempWorldObject)
                return true;

            return false;
        }
        public void SetWorldObject(bool on)
        {
            if (!IsInWorld)
                return;

            GetMap().AddObjectToSwitchList(this, on);
        }
        public void SetActive(bool on)
        {
            if (m_isActive == on)
                return;

            if (IsTypeId(TypeId.Player))
                return;

            m_isActive = on;

            if (!IsInWorld)
                return;

            Map map = GetMap();
            if (map == null)
                return;

            if (on)
            {
                if (IsTypeId(TypeId.Unit))
                    map.AddToActive(ToCreature());
                else if (IsTypeId(TypeId.DynamicObject))
                    map.AddToActive(ToDynamicObject());
            }
            else
            {
                if (IsTypeId(TypeId.Unit))
                    map.RemoveFromActive(ToCreature());
                else if (IsTypeId(TypeId.DynamicObject))
                    map.RemoveFromActive(ToDynamicObject());
            }
        }

        bool IsVisibilityOverridden() => m_visibilityDistanceOverride.HasValue;

        public void SetVisibilityDistanceOverride(VisibilityDistanceType type)
        {
            Cypher.Assert(type < VisibilityDistanceType.Max);
            if (GetTypeId() == TypeId.Player)
                return;

            m_visibilityDistanceOverride.Set(SharedConst.VisibilityDistances[(int)type]);
        }

        public virtual void CleanupsBeforeDelete(bool finalCleanup = true)
        {
            if (IsInWorld)
                RemoveFromWorld();

            Transport transport = GetTransport();
            if (transport)
                transport.RemovePassenger(this);
        }

        public uint GetZoneId() => m_zoneId;
        public uint GetAreaId() => m_areaId;

        public void GetZoneAndAreaId(out uint zoneid, out uint areaid) { zoneid = m_zoneId; areaid = m_areaId; }

        public bool IsInWorldPvpZone()
        {
            switch (GetZoneId())
            {
                case 4197: // Wintergrasp
                case 5095: // Tol Barad
                case 6941: // Ashran
                    return true;
                default:
                    return false;
            }
        }

        public InstanceScript GetInstanceScript()
        {
            Map map = GetMap();
            return map.IsDungeon() ? ((InstanceMap)map).GetInstanceScript() : null;
        }

        public float GetGridActivationRange()
        {
            if (IsActiveObject())
            {
                if (GetTypeId() == TypeId.Player && ToPlayer().GetCinematicMgr().IsOnCinematic())
                    return Math.Max(SharedConst.DefaultVisibilityInstance, GetMap().GetVisibilityRange());

                return GetMap().GetVisibilityRange();
            }
            Creature thisCreature = ToCreature();
            if (thisCreature != null)
                return thisCreature.m_SightDistance;

            return 0.0f;
        }

        public float GetVisibilityRange()
        {
            if (IsVisibilityOverridden() && !IsTypeId(TypeId.Player))
                return m_visibilityDistanceOverride.Value;
            else if (IsActiveObject() && !IsTypeId(TypeId.Player))
                return SharedConst.MaxVisibilityDistance;
            else
                return GetMap().GetVisibilityRange();
        }

        public float GetSightRange(WorldObject target = null)
        {
            if (IsTypeId(TypeId.Player) || IsTypeId(TypeId.Unit))
            {
                if (IsTypeId(TypeId.Player))
                {
                    if (target != null && target.IsVisibilityOverridden() && !target.IsTypeId(TypeId.Player))
                        return target.m_visibilityDistanceOverride.Value;
                    else if (target != null && target.IsActiveObject() && !target.IsTypeId(TypeId.Player))
                        return SharedConst.MaxVisibilityDistance;
                    else if (ToPlayer().GetCinematicMgr().IsOnCinematic())
                        return SharedConst.DefaultVisibilityInstance;
                    else
                        return GetMap().GetVisibilityRange();
                }
                else if (IsTypeId(TypeId.Unit))
                    return ToCreature().m_SightDistance;
                else
                    return SharedConst.SightRangeUnit;
            }

            if (IsTypeId(TypeId.DynamicObject) && IsActiveObject())
            {
                return GetMap().GetVisibilityRange();
            }

            return 0.0f;
        }

        public bool CheckPrivateObjectOwnerVisibility(WorldObject seer)
        {
            if (!IsPrivateObject())
                return true;

            // Owner of this private object
            if (_privateObjectOwner == seer.GetGUID())
                return true;

            // Another private object of the same owner
            if (_privateObjectOwner == seer.GetPrivateObjectOwner())
                return true;

            Player playerSeer = seer.ToPlayer();
            if (playerSeer != null)
                if (playerSeer.IsInGroup(_privateObjectOwner))
                    return true;

            return false;
        }

        public bool CanSeeOrDetect(WorldObject obj, bool ignoreStealth = false, bool distanceCheck = false, bool checkAlert = false)
        {
            if (this == obj)
                return true;

            if (obj.IsNeverVisibleFor(this) || CanNeverSee(obj))
                return false;

            if (obj.IsAlwaysVisibleFor(this) || CanAlwaysSee(obj))
                return true;

            if (!obj.CheckPrivateObjectOwnerVisibility(this))
                return false;

            bool corpseVisibility = false;
            if (distanceCheck)
            {
                bool corpseCheck = false;
                Player thisPlayer = ToPlayer();
                if (thisPlayer != null)
                {
                    if (thisPlayer.IsDead() && thisPlayer.GetHealth() > 0 && // Cheap way to check for ghost state
                    !Convert.ToBoolean(obj.m_serverSideVisibility.GetValue(ServerSideVisibilityType.Ghost) & m_serverSideVisibility.GetValue(ServerSideVisibilityType.Ghost) & (uint)GhostVisibilityType.Ghost))
                    {
                        Corpse corpse = thisPlayer.GetCorpse();
                        if (corpse != null)
                        {
                            corpseCheck = true;
                            if (corpse.IsWithinDist(thisPlayer, GetSightRange(obj), false))
                                if (corpse.IsWithinDist(obj, GetSightRange(obj), false))
                                    corpseVisibility = true;
                        }
                    }

                    Unit target = obj.ToUnit();
                    if (target)
                    {
                        // Don't allow to detect vehicle accessories if you can't see vehicle
                        Unit vehicle = target.GetVehicleBase();
                        if (vehicle)
                            if (!thisPlayer.HaveAtClient(vehicle))
                                return false;
                    }
                }

                WorldObject viewpoint = this;
                Player player = ToPlayer();
                if (player != null)
                    viewpoint = player.GetViewpoint();

                if (viewpoint == null)
                    viewpoint = this;

                if (!corpseCheck && !viewpoint.IsWithinDist(obj, GetSightRange(obj), false))
                    return false;
            }

            // GM visibility off or hidden NPC
            if (obj.m_serverSideVisibility.GetValue(ServerSideVisibilityType.GM) == 0)
            {
                // Stop checking other things for GMs
                if (m_serverSideVisibilityDetect.GetValue(ServerSideVisibilityType.GM) != 0)
                    return true;
            }
            else
                return m_serverSideVisibilityDetect.GetValue(ServerSideVisibilityType.GM) >= obj.m_serverSideVisibility.GetValue(ServerSideVisibilityType.GM);

            // Ghost players, Spirit Healers, and some other NPCs
            if (!corpseVisibility && !Convert.ToBoolean(obj.m_serverSideVisibility.GetValue(ServerSideVisibilityType.Ghost) & m_serverSideVisibilityDetect.GetValue(ServerSideVisibilityType.Ghost)))
            {
                // Alive players can see dead players in some cases, but other objects can't do that
                Player thisPlayer = ToPlayer();
                if (thisPlayer != null)
                {
                    Player objPlayer = obj.ToPlayer();
                    if (objPlayer != null)
                    {
                        if (thisPlayer.GetTeam() != objPlayer.GetTeam() || !thisPlayer.IsGroupVisibleFor(objPlayer))
                            return false;
                    }
                    else
                        return false;
                }
                else
                    return false;
            }

            if (obj.IsInvisibleDueToDespawn())
                return false;

            if (!CanDetect(obj, ignoreStealth, checkAlert))
                return false;

            return true;
        }

        bool CanNeverSee(WorldObject obj) => GetMap() != obj.GetMap() || !IsInPhase(obj);

        public virtual bool CanAlwaysSee(WorldObject obj) => false;

        bool CanDetect(WorldObject obj, bool ignoreStealth, bool checkAlert = false)
        {
            WorldObject seer = this;

            // Pets don't have detection, they use the detection of their masters
            Unit thisUnit = ToUnit();
            if (thisUnit != null)
            {
                Unit controller = thisUnit.GetCharmerOrOwner();
                if (controller != null)
                    seer = controller;
            }

            if (obj.IsAlwaysDetectableFor(seer))
                return true;

            if (!ignoreStealth && !seer.CanDetectInvisibilityOf(obj))
                return false;

            if (!ignoreStealth && !seer.CanDetectStealthOf(obj, checkAlert))
                return false;

            return true;
        }

        bool CanDetectInvisibilityOf(WorldObject obj)
        {
            uint mask = obj.m_invisibility.GetFlags() & m_invisibilityDetect.GetFlags();

            // Check for not detected types
            if (mask != obj.m_invisibility.GetFlags())
                return false;

            for (int i = 0; i < (int)InvisibilityType.Max; ++i)
            {
                if (!Convert.ToBoolean(mask & (1 << i)))
                    continue;

                int objInvisibilityValue = obj.m_invisibility.GetValue((InvisibilityType)i);
                int ownInvisibilityDetectValue = m_invisibilityDetect.GetValue((InvisibilityType)i);

                // Too low value to detect
                if (ownInvisibilityDetectValue < objInvisibilityValue)
                    return false;
            }

            return true;
        }

        bool CanDetectStealthOf(WorldObject obj, bool checkAlert = false)
        {
            // Combat reach is the minimal distance (both in front and behind),
            //   and it is also used in the range calculation.
            // One stealth point increases the visibility range by 0.3 yard.

            if (obj.m_stealth.GetFlags() == 0)
                return true;

            float distance = GetExactDist(obj);
            float combatReach = 0.0f;

            Unit unit = ToUnit();
            if (unit != null)
                combatReach = unit.GetCombatReach();

            if (distance < combatReach)
                return true;

            if (!HasInArc(MathFunctions.PI, obj))
                return false;

            GameObject go = obj.ToGameObject();
            for (int i = 0; i < (int)StealthType.Max; ++i)
            {
                if (!Convert.ToBoolean(obj.m_stealth.GetFlags() & (1 << i)))
                    continue;

                if (unit != null && unit.HasAuraTypeWithMiscvalue(AuraType.DetectStealth, i))
                    return true;

                // Starting points
                int detectionValue = 30;

                // Level difference: 5 point / level, starting from level 1.
                // There may be spells for this and the starting points too, but
                // not in the DBCs of the client.
                detectionValue += (int)(GetLevelForTarget(obj) - 1) * 5;

                // Apply modifiers
                detectionValue += m_stealthDetect.GetValue((StealthType)i);
                if (go != null)
                {
                    Unit owner = go.GetOwner();
                    if (owner != null)
                        detectionValue -= (int)(owner.GetLevelForTarget(this) - 1) * 5;
                }

                detectionValue -= obj.m_stealth.GetValue((StealthType)i);

                // Calculate max distance
                float visibilityRange = detectionValue * 0.3f + combatReach;

                // If this unit is an NPC then player detect range doesn't apply
                if (unit && unit.IsTypeId(TypeId.Player) && visibilityRange > SharedConst.MaxPlayerStealthDetectRange)
                    visibilityRange = SharedConst.MaxPlayerStealthDetectRange;

                // When checking for alert state, look 8% further, and then 1.5 yards more than that.
                if (checkAlert)
                    visibilityRange += (visibilityRange * 0.08f) + 1.5f;

                // If checking for alert, and creature's visibility range is greater than aggro distance, No alert
                Unit tunit = obj.ToUnit();
                if (checkAlert && unit && unit.ToCreature() && visibilityRange >= unit.ToCreature().GetAttackDistance(tunit) + unit.ToCreature().m_CombatDistance)
                    return false;

                if (distance > visibilityRange)
                    return false;
            }

            return true;
        }

        public virtual void SendMessageToSet(ServerPacket packet, bool self)
        {
            if (IsInWorld)
                SendMessageToSetInRange(packet, GetVisibilityRange(), self);
        }

        public virtual void SendMessageToSetInRange(ServerPacket data, float dist, bool self)
        {
            PacketSenderRef sender = new(data);
            MessageDistDeliverer<PacketSenderRef> notifier = new(this, sender, dist);
            Cell.VisitWorldObjects(this, notifier, dist);
        }

        public virtual void SendMessageToSet(ServerPacket data, Player skip)
        {
            PacketSenderRef sender = new(data);
            var notifier = new MessageDistDeliverer<PacketSenderRef>(this, sender, GetVisibilityRange(), false, skip);
            Cell.VisitWorldObjects(this, notifier, GetVisibilityRange());
        }

        public virtual void SetMap(Map map)
        {
            Cypher.Assert(map != null);
            Cypher.Assert(!IsInWorld);

            if (_currMap == map)
                return;

            _currMap = map;
            SetMapId(map.GetId());
            instanceId = map.GetInstanceId();
            if (IsWorldObject())
                _currMap.AddWorldObject(this);
        }

        public virtual void ResetMap()
        {
            if (_currMap == null)
                return;

            Cypher.Assert(_currMap != null);
            Cypher.Assert(!IsInWorld);
            if (IsWorldObject())
                _currMap.RemoveWorldObject(this);
            _currMap = null;
        }

        public Map GetMap() => _currMap;

        public void AddObjectToRemoveList()
        {
            Map map = GetMap();
            if (map == null)
            {
                Log.outError(LogFilter.Server, "Object (TypeId: {0} Entry: {1} GUID: {2}) at attempt add to move list not have valid map (Id: {3}).", GetTypeId(), GetEntry(), GetGUID().ToString(), GetMapId());
                return;
            }

            map.AddObjectToRemoveList(this);
        }

        public void SetZoneScript()
        {
            Map map = GetMap();
            if (map != null)
            {
                if (map.IsDungeon())
                    m_zoneScript = ((InstanceMap)map).GetInstanceScript();
                else if (!map.IsBattlegroundOrArena())
                {
                    BattleField bf = Global.BattleFieldMgr.GetBattlefieldToZoneId(GetZoneId());
                    if (bf != null)
                        m_zoneScript = bf;
                    else
                        m_zoneScript = Global.OutdoorPvPMgr.GetZoneScript(GetZoneId());
                }
            }
        }

        public TempSummon SummonCreature(uint entry, float x, float y, float z, float o = 0, TempSummonType despawnType = TempSummonType.ManualDespawn, uint despawnTime = 0, ObjectGuid privateObjectOwner = default)
        {
            if (x == 0.0f && y == 0.0f && z == 0.0f)
                GetClosePoint(out x, out y, out z, GetCombatReach());

            if (o == 0.0f)
                o = GetOrientation();

            return SummonCreature(entry, new Position(x, y, z, o), despawnType, despawnTime, 0, privateObjectOwner);
        }

        public TempSummon SummonCreature(uint entry, Position pos, TempSummonType despawnType = TempSummonType.ManualDespawn, uint despawnTime = 0, uint vehId = 0, ObjectGuid privateObjectOwner = default)
        {
            Map map = GetMap();
            if (map != null)
            {
                TempSummon summon = map.SummonCreature(entry, pos, null, despawnTime, ToUnit(), 0, vehId, privateObjectOwner);
                if (summon != null)
                {
                    summon.SetTempSummonType(despawnType);
                    return summon;
                }
            }

            return null;
        }

        public GameObject SummonGameObject(uint entry, float x, float y, float z, float ang, Quaternion rotation, uint respawnTime, GameObjectSummonType summonType = GameObjectSummonType.TimedOrCorpseDespawn)
        {
            if (x == 0 && y == 0 && z == 0)
            {
                GetClosePoint(out x, out y, out z, GetCombatReach());
                ang = GetOrientation();
            }

            Position pos = new(x, y, z, ang);
            return SummonGameObject(entry, pos, rotation, respawnTime, summonType);
        }

        public GameObject SummonGameObject(uint entry, Position pos, Quaternion rotation, uint respawnTime, GameObjectSummonType summonType = GameObjectSummonType.TimedOrCorpseDespawn)
        {
            if (!IsInWorld)
                return null;

            GameObjectTemplate goinfo = Global.ObjectMgr.GetGameObjectTemplate(entry);
            if (goinfo == null)
            {
                Log.outError(LogFilter.Sql, "Gameobject template {0} not found in database!", entry);
                return null;
            }

            Map map = GetMap();
            GameObject go = GameObject.CreateGameObject(entry, map, pos, rotation, 255, GameObjectState.Ready);
            if (!go)
                return null;

            PhasingHandler.InheritPhaseShift(go, this);

            go.SetRespawnTime((int)respawnTime);
            if (IsPlayer() || (IsCreature() && summonType == GameObjectSummonType.TimedOrCorpseDespawn)) //not sure how to handle this
                ToUnit().AddGameObject(go);
            else
                go.SetSpawnedByDefault(false);

            map.AddToMap(go);
            return go;
        }

        public Creature SummonTrigger(float x, float y, float z, float ang, uint duration, CreatureAI AI = null)
        {
            TempSummonType summonType = (duration == 0) ? TempSummonType.DeadDespawn : TempSummonType.TimedDespawn;
            Creature summon = SummonCreature(SharedConst.WorldTrigger, x, y, z, ang, summonType, duration);
            if (summon == null)
                return null;

            if (IsTypeId(TypeId.Player) || IsTypeId(TypeId.Unit))
            {
                summon.SetFaction(ToUnit().GetFaction());
                summon.SetLevel(ToUnit().GetLevel());
            }

            if (AI != null)
                summon.InitializeAI(new CreatureAI(summon));
            return summon;
        }

        public void SummonCreatureGroup(byte group) => SummonCreatureGroup(group, out _);

        public void SummonCreatureGroup(byte group, out List<TempSummon> list)
        {
            Cypher.Assert((IsTypeId(TypeId.GameObject) || IsTypeId(TypeId.Unit)), "Only GOs and creatures can summon npc groups!");
            list = new List<TempSummon>();
            var data = Global.ObjectMgr.GetSummonGroup(GetEntry(), IsTypeId(TypeId.GameObject) ? SummonerType.GameObject : SummonerType.Creature, group);
            if (data.Empty())
            {
                Log.outWarn(LogFilter.Scripts, "{0} ({1}) tried to summon non-existing summon group {2}.", GetName(), GetGUID().ToString(), group);
                return;
            }

            foreach (var tempSummonData in data)
            {
                TempSummon summon = SummonCreature(tempSummonData.entry, tempSummonData.pos, tempSummonData.type, tempSummonData.time);
                if (summon)
                    list.Add(summon);
            }
        }

        public Creature FindNearestCreature(uint entry, float range, bool alive = true)
        {
            var checker = new NearestCreatureEntryWithLiveStateInObjectRangeCheck(this, entry, alive, range);
            var searcher = new CreatureLastSearcher(this, checker);

            Cell.VisitAllObjects(this, searcher, range);
            return searcher.GetTarget();
        }

        public GameObject FindNearestGameObject(uint entry, float range)
        {
            var checker = new NearestGameObjectEntryInObjectRangeCheck(this, entry, range);
            var searcher = new GameObjectLastSearcher(this, checker);

            Cell.VisitGridObjects(this, searcher, range);
            return searcher.GetTarget();
        }

        public GameObject FindNearestGameObjectOfType(GameObjectTypes type, float range)
        {
            var checker = new NearestGameObjectTypeInObjectRangeCheck(this, type, range);
            var searcher = new GameObjectLastSearcher(this, checker);

            Cell.VisitGridObjects(this, searcher, range);
            return searcher.GetTarget();
        }

        public Player SelectNearestPlayer(float distance)
        {
            var checker = new NearestPlayerInObjectRangeCheck(this, distance);
            var searcher = new PlayerLastSearcher(this, checker);
            Cell.VisitAllObjects(this, searcher, distance);
            return searcher.GetTarget();
        }

        public void GetGameObjectListWithEntryInGrid(List<GameObject> gameobjectList, uint entry = 0, float maxSearchRange = 250.0f)
        {
            var check = new AllGameObjectsWithEntryInRange(this, entry, maxSearchRange);
            var searcher = new GameObjectListSearcher(this, gameobjectList, check);

            Cell.VisitGridObjects(this, searcher, maxSearchRange);
        }

        public void GetCreatureListWithEntryInGrid(List<Creature> creatureList, uint entry = 0, float maxSearchRange = 250.0f)
        {
            var check = new AllCreaturesOfEntryInRange(this, entry, maxSearchRange);
            var searcher = new CreatureListSearcher(this, creatureList, check);

            Cell.VisitGridObjects(this, searcher, maxSearchRange);
        }

        public List<Unit> GetPlayerListInGrid(float maxSearchRange)
        {
            List<Unit> playerList = new();
            var checker = new AnyPlayerInObjectRangeCheck(this, maxSearchRange);
            var searcher = new PlayerListSearcher(this, playerList, checker);

            Cell.VisitWorldObjects(this, searcher, maxSearchRange);
            return playerList;
        }

        public bool IsInPhase(WorldObject obj)
        => GetPhaseShift().CanSee(obj.GetPhaseShift());

        public static bool InSamePhase(WorldObject a, WorldObject b)
        {
            return a != null && b != null && a.IsInPhase(b);
        }

        public virtual float GetCombatReach() => 0.0f;  // overridden (only) in Unit
        public PhaseShift GetPhaseShift() => _phaseShift;
        public void SetPhaseShift(PhaseShift phaseShift) => _phaseShift = new PhaseShift(phaseShift);
        public PhaseShift GetSuppressedPhaseShift() => _suppressedPhaseShift;
        public void SetSuppressedPhaseShift(PhaseShift phaseShift) => _suppressedPhaseShift = new PhaseShift(phaseShift);
        public int GetDBPhase() => _dbPhase;

        // if negative it is used as PhaseGroupId
        public void SetDBPhase(int p) => _dbPhase = p;

        public void PlayDistanceSound(uint soundId, Player target = null)
        {
            PlaySpeakerBoxSound playSpeakerBoxSound = new(GetGUID(), soundId);
            if (target != null)
                target.SendPacket(playSpeakerBoxSound);
            else
                SendMessageToSet(playSpeakerBoxSound, true);
        }

        public void PlayDirectSound(uint soundId, Player target = null, uint broadcastTextId = 0)
        {
            PlaySound sound = new(GetGUID(), soundId, broadcastTextId);
            if (target)
                target.SendPacket(sound);
            else
                SendMessageToSet(sound, true);
        }

        public void PlayDirectMusic(uint musicId, Player target = null)
        {
            if (target)
                target.SendPacket(new PlayMusic(musicId));
            else
                SendMessageToSet(new PlayMusic(musicId), true);
        }

        public void DestroyForNearbyPlayers()
        {
            if (!IsInWorld)
                return;

            List<Unit> targets = new();
            var check = new AnyPlayerInObjectRangeCheck(this, GetVisibilityRange(), false);
            var searcher = new PlayerListSearcher(this, targets, check);

            Cell.VisitWorldObjects(this, searcher, GetVisibilityRange());
            foreach (Player player in targets)
            {
                if (player == this)
                    continue;

                if (!player.HaveAtClient(this))
                    continue;

                if (IsTypeMask(TypeMask.Unit) && (ToUnit().GetCharmerGUID() == player.GetGUID()))// @todo this is for puppet
                    continue;

                DestroyForPlayer(player);
                player.m_clientGUIDs.Remove(GetGUID());
            }
        }

        public virtual void UpdateObjectVisibility(bool force = true)
        {
            //updates object's visibility for nearby players
            var notifier = new VisibleChangesNotifier(this);
            Cell.VisitWorldObjects(this, notifier, GetVisibilityRange());
        }

        public virtual void UpdateObjectVisibilityOnCreate() => UpdateObjectVisibility(true);

        public virtual void BuildUpdate(Dictionary<Player, UpdateData> data)
        {
            var notifier = new WorldObjectChangeAccumulator(this, data);
            Cell.VisitWorldObjects(this, notifier, GetVisibilityRange());
        }

        public virtual void AddToObjectUpdate() => GetMap().AddUpdateObject(this);

        public virtual void RemoveFromObjectUpdate() => GetMap().RemoveUpdateObject(this);

        public uint GetInstanceId() => instanceId;

        public virtual ushort GetAIAnimKitId() => 0;
        public virtual ushort GetMovementAnimKitId() => 0;
        public virtual ushort GetMeleeAnimKitId() => 0;

        // Watcher
        public bool IsPrivateObject() => !_privateObjectOwner.IsEmpty();
        public ObjectGuid GetPrivateObjectOwner() => _privateObjectOwner;
        public void SetPrivateObjectOwner(ObjectGuid owner) => _privateObjectOwner = owner;

        public virtual string GetName(Locale locale = Locale.enUS) => _name;
        public void SetName(string name) => _name = name;

        public ObjectGuid GetGUID() => m_guid;
        public uint GetEntry() => GetUpdateField<uint>(ObjectFields.EntryID);
        public void SetEntry(uint entry) => SetUpdateField<uint>(ObjectFields.EntryID, entry);

        public float GetObjectScale() => GetUpdateField<float>(ObjectFields.Scale);
        public virtual void SetObjectScale(float scale) => SetUpdateField<float>(ObjectFields.Scale, scale);

        public UnitDynFlags GetDynamicFlags() => (UnitDynFlags)GetUpdateField<uint>(ObjectFields.DynamicFlags);
        public bool HasDynamicFlag(UnitDynFlags flag) => GetDynamicFlags().HasAnyFlag(flag);
        public void AddDynamicFlag(UnitDynFlags flag) => AddFlag(ObjectFields.DynamicFlags, (uint)flag);
        public void RemoveDynamicFlag(UnitDynFlags flag) => RemoveFlag(ObjectFields.DynamicFlags, (uint)flag);
        public void SetDynamicFlags(UnitDynFlags flag) => SetUpdateField<uint>(ObjectFields.DynamicFlags, (uint)flag);

        public TypeId GetTypeId() => ObjectTypeId;
        public bool IsTypeId(TypeId typeId) => GetTypeId() == typeId;
        public bool IsTypeMask(TypeMask mask) => Convert.ToBoolean(mask & ObjectTypeMask);

        public virtual bool HasQuest(uint questId) => false;
        public virtual bool HasInvolvedQuest(uint questId) => false;
        public void SetIsNewObject(bool enable) => _isNewObject = enable;

        public bool IsCreature() => GetTypeId() == TypeId.Unit;
        public bool IsPlayer() => GetTypeId() == TypeId.Player;
        public bool IsGameObject() => GetTypeId() == TypeId.GameObject;
        public bool IsUnit() => IsTypeMask(TypeMask.Unit);
        public bool IsCorpse() => GetTypeId() == TypeId.Corpse;
        public bool IsDynObject() => GetTypeId() == TypeId.DynamicObject;
        public bool IsAreaTrigger() => GetTypeId() == TypeId.AreaTrigger;
        public bool IsConversation() => GetTypeId() == TypeId.Conversation;

        public Creature ToCreature() => IsCreature() ? (this as Creature) : null;
        public Player ToPlayer() => IsPlayer() ? (this as Player) : null;
        public GameObject ToGameObject() => IsGameObject() ? (this as GameObject) : null;
        public Unit ToUnit() => IsUnit() ? (this as Unit) : null;
        public Corpse ToCorpse() => IsCorpse() ? (this as Corpse) : null;
        public DynamicObject ToDynamicObject() => IsDynObject() ? (this as DynamicObject) : null;
        public AreaTrigger ToAreaTrigger() => IsAreaTrigger() ? (this as AreaTrigger) : null;
        public Conversation ToConversation() => IsConversation() ? (this as Conversation) : null;

        public virtual void Update(uint diff) { }

        public virtual uint GetLevelForTarget(WorldObject target) => 1;

        public virtual void SaveRespawnTime(uint forceDelay = 0, bool saveToDB = true) { }

        public ZoneScript GetZoneScript() => m_zoneScript;

        public void AddToNotify(NotifyFlags f) => m_notifyflags |= f;
        public bool IsNeedNotify(NotifyFlags f) => Convert.ToBoolean(m_notifyflags & f);
        NotifyFlags GetNotifyFlags() { return m_notifyflags; }
        public void ResetAllNotifies() { m_notifyflags = 0; }

        public bool IsActiveObject() => m_isActive;
        public bool IsPermanentWorldObject() => m_isWorldObject;

        public Transport GetTransport() => m_transport;
        public float GetTransOffsetX() => m_movementInfo.transport.pos.GetPositionX();
        public float GetTransOffsetY() => m_movementInfo.transport.pos.GetPositionY();
        public float GetTransOffsetZ() => m_movementInfo.transport.pos.GetPositionZ();
        public float GetTransOffsetO() => m_movementInfo.transport.pos.GetOrientation();
        Position GetTransOffset() { return m_movementInfo.transport.pos; }
        public uint GetTransTime() => m_movementInfo.transport.time;
        public sbyte GetTransSeat() => m_movementInfo.transport.seat;
        public virtual ObjectGuid GetTransGUID()
        {
            if (GetTransport())
                return GetTransport().GetGUID();

            return ObjectGuid.Empty;
        }
        public void SetTransport(Transport t) => m_transport = t;

        public virtual float GetStationaryX() => GetPositionX();
        public virtual float GetStationaryY() => GetPositionY();
        public virtual float GetStationaryZ() => GetPositionZ();
        public virtual float GetStationaryO() => GetOrientation();

        public virtual float GetCollisionHeight() => 0.0f;
        public float GetMidsectionHeight() => GetCollisionHeight() / 2.0f;

        public virtual bool IsNeverVisibleFor(WorldObject seer) => !IsInWorld;
        public virtual bool IsAlwaysVisibleFor(WorldObject seer) => false;
        public virtual bool IsInvisibleDueToDespawn() => false;
        public virtual bool IsAlwaysDetectableFor(WorldObject seer) => false;

        public virtual bool LoadFromDB(ulong spawnId, Map map, bool addToMap, bool allowDuplicate) => true;

        //Position

        public float GetDistanceZ(WorldObject obj)
        {
            float dz = Math.Abs(GetPositionZ() - obj.GetPositionZ());
            float sizefactor = GetCombatReach() + obj.GetCombatReach();
            float dist = dz - sizefactor;
            return (dist > 0 ? dist : 0);
        }

        public virtual bool _IsWithinDist(WorldObject obj, float dist2compare, bool is3D, bool incOwnRadius = true, bool incTargetRadius = true)
        {
            float sizefactor = 0;
            sizefactor += incOwnRadius ? GetCombatReach() : 0.0f;
            sizefactor += incTargetRadius ? obj.GetCombatReach() : 0.0f;
            float maxdist = dist2compare + sizefactor;

            Position thisOrTransport = this;
            Position objOrObjTransport = obj;

            if (GetTransport() && obj.GetTransport() != null && obj.GetTransport().GetGUID() == GetTransport().GetGUID())
            {
                thisOrTransport = m_movementInfo.transport.pos;
                objOrObjTransport = obj.m_movementInfo.transport.pos;
            }


            if (is3D)
                return thisOrTransport.IsInDist(objOrObjTransport, maxdist);
            else
                return thisOrTransport.IsInDist2d(objOrObjTransport, maxdist);
        }

        public float GetDistance(WorldObject obj)
        {
            float d = GetExactDist(obj.GetPosition()) - GetCombatReach() - obj.GetCombatReach();
            return d > 0.0f ? d : 0.0f;
        }

        public float GetDistance(Position pos)
        {
            float d = GetExactDist(pos) - GetCombatReach();
            return d > 0.0f ? d : 0.0f;
        }

        public float GetDistance(float x, float y, float z)
        {
            float d = GetExactDist(x, y, z) - GetCombatReach();
            return d > 0.0f ? d : 0.0f;
        }

        public float GetDistance2d(WorldObject obj)
        {
            float d = GetExactDist2d(obj.GetPosition()) - GetCombatReach() - obj.GetCombatReach();
            return d > 0.0f ? d : 0.0f;
        }

        public float GetDistance2d(float x, float y)
        {
            float d = GetExactDist2d(x, y) - GetCombatReach();
            return d > 0.0f ? d : 0.0f;
        }

        public bool IsSelfOrInSameMap(WorldObject obj)
        {
            if (this == obj)
                return true;
            return IsInMap(obj);
        }

        public bool IsInMap(WorldObject obj)
        {
            if (obj != null)
                return IsInWorld && obj.IsInWorld && GetMap().GetId() == obj.GetMap().GetId();

            return false;
        }

        public bool IsWithinDist3d(float x, float y, float z, float dist) => IsInDist(x, y, z, dist + GetCombatReach());
        public bool IsWithinDist3d(Position pos, float dist) => IsInDist(pos, dist + GetCombatReach());
        public bool IsWithinDist2d(float x, float y, float dist) => IsInDist2d(x, y, dist + GetCombatReach());
        public bool IsWithinDist2d(Position pos, float dist) => IsInDist2d(pos, dist + GetCombatReach());
        public bool IsWithinDist(WorldObject obj, float dist2compare, bool is3D = true) => obj != null && _IsWithinDist(obj, dist2compare, is3D);
        public bool IsWithinDistInMap(WorldObject obj, float dist2compare, bool is3D = true, bool incOwnRadius = true, bool incTargetRadius = true) =>
            obj && IsInMap(obj) && IsInPhase(obj) && _IsWithinDist(obj, dist2compare, is3D, incOwnRadius, incTargetRadius);

        public bool IsWithinLOS(float ox, float oy, float oz, LineOfSightChecks checks = LineOfSightChecks.All, ModelIgnoreFlags ignoreFlags = ModelIgnoreFlags.Nothing)
        {
            if (IsInWorld)
            {
                oz += GetCollisionHeight();
                float x, y, z;
                if (IsTypeId(TypeId.Player))
                {
                    GetPosition(out x, out y, out z);
                    z += GetCollisionHeight();
                }
                else
                    GetHitSpherePointFor(new Position(ox, oy, oz), out x, out y, out z);

                return GetMap().IsInLineOfSight(GetPhaseShift(), x, y, z, ox, oy, oz, checks, ignoreFlags);
            }

            return true;
        }

        public bool IsWithinLOSInMap(WorldObject obj, LineOfSightChecks checks = LineOfSightChecks.All, ModelIgnoreFlags ignoreFlags = ModelIgnoreFlags.Nothing)
        {
            if (!IsInMap(obj))
                return false;

            float ox, oy, oz;
            if (obj.IsTypeId(TypeId.Player))
            {
                obj.GetPosition(out ox, out oy, out oz);
                oz += GetCollisionHeight();
            }
            else
                obj.GetHitSpherePointFor(new(GetPositionX(), GetPositionY(), GetPositionZ() + GetCollisionHeight()), out ox, out oy, out oz);

            float x, y, z;
            if (IsPlayer())
            {
                GetPosition(out x, out y, out z);
                z += GetCollisionHeight();
            }
            else
                GetHitSpherePointFor(new(obj.GetPositionX(), obj.GetPositionY(), obj.GetPositionZ() + obj.GetCollisionHeight()), out x, out y, out z);

            return GetMap().IsInLineOfSight(GetPhaseShift(), x, y, z, ox, oy, oz, checks, ignoreFlags);
        }

        Position GetHitSpherePointFor(Position dest)
        {
            Vector3 vThis = new(GetPositionX(), GetPositionY(), GetPositionZ() + GetCollisionHeight());
            Vector3 vObj = new(dest.GetPositionX(), dest.GetPositionY(), dest.GetPositionZ());
            Vector3 contactPoint = vThis + (vObj - vThis).directionOrZero() * Math.Min(dest.GetExactDist(GetPosition()), GetCombatReach());

            return new Position(contactPoint.X, contactPoint.Y, contactPoint.Z, GetAngle(contactPoint.X, contactPoint.Y));
        }

        void GetHitSpherePointFor(Position dest, out float x, out float y, out float z)
        {
            Position pos = GetHitSpherePointFor(dest);
            x = pos.GetPositionX();
            y = pos.GetPositionY();
            z = pos.GetPositionZ();
        }

        public bool GetDistanceOrder(WorldObject obj1, WorldObject obj2, bool is3D = true)
        {
            float dx1 = GetPositionX() - obj1.GetPositionX();
            float dy1 = GetPositionY() - obj1.GetPositionY();
            float distsq1 = dx1 * dx1 + dy1 * dy1;
            if (is3D)
            {
                float dz1 = GetPositionZ() - obj1.GetPositionZ();
                distsq1 += dz1 * dz1;
            }

            float dx2 = GetPositionX() - obj2.GetPositionX();
            float dy2 = GetPositionY() - obj2.GetPositionY();
            float distsq2 = dx2 * dx2 + dy2 * dy2;
            if (is3D)
            {
                float dz2 = GetPositionZ() - obj2.GetPositionZ();
                distsq2 += dz2 * dz2;
            }

            return distsq1 < distsq2;
        }

        public bool IsInRange(WorldObject obj, float minRange, float maxRange, bool is3D = true)
        {
            float dx = GetPositionX() - obj.GetPositionX();
            float dy = GetPositionY() - obj.GetPositionY();
            float distsq = dx * dx + dy * dy;
            if (is3D)
            {
                float dz = GetPositionZ() - obj.GetPositionZ();
                distsq += dz * dz;
            }

            float sizefactor = GetCombatReach() + obj.GetCombatReach();

            // check only for real range
            if (minRange > 0.0f)
            {
                float mindist = minRange + sizefactor;
                if (distsq < mindist * mindist)
                    return false;
            }

            float maxdist = maxRange + sizefactor;
            return distsq < maxdist * maxdist;
        }

        public bool IsInBetween(WorldObject obj1, WorldObject obj2, float size = 0) => obj1 && obj2 && IsInBetween(obj1.GetPosition(), obj2.GetPosition(), size);
        bool IsInBetween(Position pos1, Position pos2, float size)
        {
            float dist = GetExactDist2d(pos1);

            // not using sqrt() for performance
            if ((dist * dist) >= pos1.GetExactDist2dSq(pos2))
                return false;

            if (size == 0)
                size = GetCombatReach() / 2;

            float angle = pos1.GetAngle(pos2);

            // not using sqrt() for performance
            return (size * size) >= GetExactDist2dSq(pos1.GetPositionX() + (float)Math.Cos(angle) * dist, pos1.GetPositionY() + (float)Math.Sin(angle) * dist);
        }

        public bool IsInFront(WorldObject target, float arc = MathFunctions.PI) => HasInArc(arc, target);
        public bool IsInBack(WorldObject target, float arc = MathFunctions.PI) => !HasInArc(2 * MathFunctions.PI - arc, target);

        public void GetRandomPoint(Position pos, float distance, out float rand_x, out float rand_y, out float rand_z)
        {
            if (distance == 0)
            {
                pos.GetPosition(out rand_x, out rand_y, out rand_z);
                return;
            }

            // angle to face `obj` to `this`
            float angle = (float)RandomHelper.NextDouble() * (2 * MathFunctions.PI);
            float new_dist = (float)RandomHelper.NextDouble() + (float)RandomHelper.NextDouble();
            new_dist = distance * (new_dist > 1 ? new_dist - 2 : new_dist);

            rand_x = (float)(pos.posX + new_dist * Math.Cos(angle));
            rand_y = (float)(pos.posY + new_dist * Math.Sin(angle));
            rand_z = pos.posZ;

            GridDefines.NormalizeMapCoord(ref rand_x);
            GridDefines.NormalizeMapCoord(ref rand_y);
            UpdateGroundPositionZ(rand_x, rand_y, ref rand_z);            // update to LOS height if available
        }

        public Position GetRandomPoint(Position srcPos, float distance)
        {
            GetRandomPoint(srcPos, distance, out float x, out float y, out float z);
            return new Position(x, y, z, GetOrientation());
        }

        public void UpdateGroundPositionZ(float x, float y, ref float z) => z = GetMapHeight(x, y, z);

        public void UpdateAllowedPositionZ(float x, float y, ref float z)
        {
            // TODO: Allow transports to be part of dynamic vmap tree
            if (GetTransport())
                return;

            switch (GetTypeId())
            {
                case TypeId.Unit:
                {
                    // non fly unit don't must be in air
                    // non swim unit must be at ground (mostly speedup, because it don't must be in water and water level check less fast
                    if (!ToCreature().CanFly())
                    {
                        bool canSwim = ToCreature().CanSwim();
                        float ground_z = z;
                        float max_z = canSwim
                            ? GetMapWaterOrGroundLevel(x, y, z, ref ground_z)
                            : (ground_z = GetMapHeight(x, y, z));
                        if (max_z > MapConst.InvalidHeight)
                        {
                            if (z > max_z)
                                z = max_z;
                            else if (z < ground_z)
                                z = ground_z;
                        }
                    }
                    else
                    {
                        float ground_z = GetMapHeight(x, y, z);
                        if (z < ground_z)
                            z = ground_z;
                    }
                    break;
                }
                case TypeId.Player:
                {
                    // for server controlled moves playr work same as creature (but it can always swim)
                    if (!ToPlayer().CanFly())
                    {
                        float ground_z = z;
                        float max_z = GetMapWaterOrGroundLevel(x, y, z, ref ground_z);
                        if (max_z > MapConst.InvalidHeight)
                        {
                            if (z > max_z)
                                z = max_z;
                            else if (z < ground_z)
                                z = ground_z;
                        }
                    }
                    else
                    {
                        float ground_z = GetMapHeight(x, y, z);
                        if (z < ground_z)
                            z = ground_z;
                    }
                    break;
                }
                default:
                {
                    float ground_z = GetMapHeight(x, y, z);
                    if (ground_z > MapConst.InvalidHeight)
                        z = ground_z;
                    break;
                }
            }
        }

        public void GetNearPoint2D(out float x, out float y, float distance2d, float absAngle)
        {
            x = (float)(GetPositionX() + (GetCombatReach() + distance2d) * Math.Cos(absAngle));
            y = (float)(GetPositionY() + (GetCombatReach() + distance2d) * Math.Sin(absAngle));

            GridDefines.NormalizeMapCoord(ref x);
            GridDefines.NormalizeMapCoord(ref y);
        }

        public void GetNearPoint(WorldObject searcher, out float x, out float y, out float z, float searcher_size, float distance2d, float absAngle)
        {
            GetNearPoint2D(out x, out y, distance2d + searcher_size, absAngle);
            z = GetPositionZ();
            UpdateAllowedPositionZ(x, y, ref z);

            // if detection disabled, return first point
            if (!WorldConfig.GetBoolValue(WorldCfg.DetectPosCollision))
                return;

            // return if the point is already in LoS
            if (IsWithinLOS(x, y, z))
                return;

            // remember first point
            float first_x = x;
            float first_y = y;
            float first_z = z;

            // loop in a circle to look for a point in LoS using small steps
            for (float angle = MathFunctions.PI / 8; angle < Math.PI * 2; angle += MathFunctions.PI / 8)
            {
                GetNearPoint2D(out x, out y, distance2d + searcher_size, absAngle + angle);
                z = GetPositionZ();
                UpdateAllowedPositionZ(x, y, ref z);
                if (IsWithinLOS(x, y, z))
                    return;
            }

            // still not in LoS, give up and return first position found
            x = first_x;
            y = first_y;
            z = first_z;
        }

        public void GetClosePoint(out float x, out float y, out float z, float size, float distance2d = 0, float angle = 0) =>
            GetNearPoint(null, out x, out y, out z, size, distance2d, GetOrientation() + angle);

        public Position GetNearPosition(float dist, float angle)
        {
            var pos = GetPosition();
            MovePosition(pos, dist, angle);
            return pos;
        }

        public Position GetFirstCollisionPosition(float dist, float angle)
        {
            var pos = new Position(GetPosition());
            MovePositionToFirstCollision(pos, dist, angle);
            return pos;
        }

        public Position GetRandomNearPosition(float radius)
        {
            var pos = GetPosition();
            MovePosition(pos, radius * (float)RandomHelper.NextDouble(), (float)RandomHelper.NextDouble() * MathFunctions.PI * 2);
            return pos;
        }

        public void GetContactPoint(WorldObject obj, out float x, out float y, out float z, float distance2d = 0.5f) =>
            GetNearPoint(obj, out x, out y, out z, obj.GetCombatReach(), distance2d, GetAngle(obj));

        public void MovePosition(Position pos, float dist, float angle)
        {
            angle += GetOrientation();
            float destx = pos.posX + dist * (float)Math.Cos(angle);
            float desty = pos.posY + dist * (float)Math.Sin(angle);

            // Prevent invalid coordinates here, position is unchanged
            if (!GridDefines.IsValidMapCoord(destx, desty, pos.posZ))
            {
                Log.outError(LogFilter.Server, "WorldObject.MovePosition invalid coordinates X: {0} and Y: {1} were passed!", destx, desty);
                return;
            }

            float ground = GetMapHeight(destx, desty, MapConst.MaxHeight);
            float floor = GetMapHeight(destx, desty, pos.posZ);
            float destz = Math.Abs(ground - pos.posZ) <= Math.Abs(floor - pos.posZ) ? ground : floor;

            float step = dist / 10.0f;

            for (byte j = 0; j < 10; ++j)
            {
                // do not allow too big z changes
                if (Math.Abs(pos.posZ - destz) > 6)
                {
                    destx -= step * (float)Math.Cos(angle);
                    desty -= step * (float)Math.Sin(angle);
                    ground = GetMap().GetHeight(GetPhaseShift(), destx, desty, MapConst.MaxHeight, true);
                    floor = GetMap().GetHeight(GetPhaseShift(), destx, desty, pos.posZ, true);
                    destz = Math.Abs(ground - pos.posZ) <= Math.Abs(floor - pos.posZ) ? ground : floor;
                }
                // we have correct destz now
                else
                {
                    pos.Relocate(destx, desty, destz);
                    break;
                }
            }

            GridDefines.NormalizeMapCoord(ref pos.posX);
            GridDefines.NormalizeMapCoord(ref pos.posY);
            UpdateGroundPositionZ(pos.posX, pos.posY, ref pos.posZ);
            pos.SetOrientation(GetOrientation());
        }

        public void MovePositionToFirstCollision(Position pos, float dist, float angle)
        {
            angle += GetOrientation();
            float destx = pos.posX + dist * (float)Math.Cos(angle);
            float desty = pos.posY + dist * (float)Math.Sin(angle);
            float destz = pos.posZ;

            // Prevent invalid coordinates here, position is unchanged
            if (!GridDefines.IsValidMapCoord(destx, desty))
            {
                Log.outError(LogFilter.Server, "WorldObject.MovePositionToFirstCollision invalid coordinates X: {0} and Y: {1} were passed!", destx, desty);
                return;
            }

            UpdateAllowedPositionZ(destx, desty, ref destz);
            bool col = Global.VMapMgr.GetObjectHitPos(PhasingHandler.GetTerrainMapId(GetPhaseShift(), GetMap(), pos.posX, pos.posY), pos.posX, pos.posY, pos.posZ, destx, desty, destz, out destx, out desty, out destz, -0.5f);

            // collision occured
            if (col)
            {
                // move back a bit
                destx -= SharedConst.ContactDistance * (float)Math.Cos(angle);
                desty -= SharedConst.ContactDistance * (float)Math.Sin(angle);
                dist = (float)Math.Sqrt((pos.posX - destx) * (pos.posX - destx) + (pos.posY - desty) * (pos.posY - desty));
            }

            // check dynamic collision
            col = GetMap().GetObjectHitPos(GetPhaseShift(), pos.posX, pos.posY, pos.posZ, destx, desty, destz, out destx, out desty, out destz, -0.5f);

            // Collided with a gameobject
            if (col)
            {
                destx -= SharedConst.ContactDistance * (float)Math.Cos(angle);
                desty -= SharedConst.ContactDistance * (float)Math.Sin(angle);
                dist = (float)Math.Sqrt((pos.posX - destx) * (pos.posX - destx) + (pos.posY - desty) * (pos.posY - desty));
            }

            float step = dist / 10.0f;

            for (byte j = 0; j < 10; ++j)
            {
                // do not allow too big z changes
                if (Math.Abs(pos.posZ - destz) > 6f)
                {
                    destx -= step * (float)Math.Cos(angle);
                    desty -= step * (float)Math.Sin(angle);
                    UpdateAllowedPositionZ(destx, desty, ref destz);
                }
                // we have correct destz now
                else
                {
                    pos.Relocate(destx, desty, destz);
                    break;
                }
            }

            GridDefines.NormalizeMapCoord(ref pos.posX);
            GridDefines.NormalizeMapCoord(ref pos.posY);
            UpdateAllowedPositionZ(destx, desty, ref pos.posZ);
            pos.SetOrientation(GetOrientation());
        }

        public float GetFloorZ()
        {
            if (!IsInWorld)
                return m_staticFloorZ;
            return Math.Max(m_staticFloorZ, GetMap().GetGameObjectFloor(GetPhaseShift(), GetPositionX(), GetPositionY(), GetPositionZ() + GetCollisionHeight()));
        }

        float GetMapWaterOrGroundLevel(float x, float y, float z, ref float ground)
        {
            Unit unit = ToUnit();
            if (unit != null)
                return GetMap().GetWaterOrGroundLevel(GetPhaseShift(), x, y, z, ref ground, !unit.HasAuraType(AuraType.WaterWalk), GetCollisionHeight());

            return z;
        }

        public float GetMapHeight(float x, float y, float z, bool vmap = true, float distanceToSearch = MapConst.DefaultHeightSearch)
        {
            if (z != MapConst.MaxHeight)
                z += GetCollisionHeight();

            return GetMap().GetHeight(GetPhaseShift(), x, y, z, vmap, distanceToSearch);
        }

        public void SetLocationInstanceId(uint _instanceId) => instanceId = _instanceId;

        #region Fields
        public TypeMask ObjectTypeMask { get; set; }
        protected TypeId ObjectTypeId { get; set; }
        protected CreateObjectBits m_updateFlag;
        ObjectGuid m_guid;
        bool _isNewObject;

        protected BitArray m_changesMask;
        protected UpdateValues[] m_updateValues;

        protected uint[][] m_dynamicValues;
        protected Dictionary<int, DynamicFieldChangeType> m_dynamicChangesMask = new Dictionary<int, DynamicFieldChangeType>();
        protected BitArray[] m_dynamicChangesArrayMask;

        public uint ValuesCount;
        protected uint m_dynamicValuesCount;

        public uint LastUsedScriptID;

        protected MirrorFlags m_fieldNotifyFlags { get; set; }
        bool m_objectUpdated;

        uint m_zoneId;
        uint m_areaId;
        float m_staticFloorZ;

        public MovementInfo m_movementInfo;
        string _name;
        protected bool m_isActive;
        Optional<float> m_visibilityDistanceOverride;
        bool m_isWorldObject;
        public ZoneScript m_zoneScript;

        Transport m_transport;
        Map _currMap;
        uint instanceId;
        PhaseShift _phaseShift = new();
        PhaseShift _suppressedPhaseShift = new();                   // contains phases for current area but not applied due to conditions
        int _dbPhase;
        public bool IsInWorld { get; set; }

        NotifyFlags m_notifyflags;

        ObjectGuid _privateObjectOwner;

        public FlaggedArray<StealthType> m_stealth = new(2);
        public FlaggedArray<StealthType> m_stealthDetect = new(2);

        public FlaggedArray<InvisibilityType> m_invisibility = new((int)InvisibilityType.Max);
        public FlaggedArray<InvisibilityType> m_invisibilityDetect = new((int)InvisibilityType.Max);

        public FlaggedArray<ServerSideVisibilityType> m_serverSideVisibility = new(2);
        public FlaggedArray<ServerSideVisibilityType> m_serverSideVisibilityDetect = new(2);
        #endregion

        public static implicit operator bool(WorldObject obj) => obj != null;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct UpdateValues
    {
        [FieldOffset(0)]
        public uint UnsignedValue;

        [FieldOffset(0)]
        public int SignedValue;

        [FieldOffset(0)]
        public float FloatValue;
    }

    public class MovementInfo
    {
        public ObjectGuid Guid { get; set; }
        MovementFlag flags;
        MovementFlag2 flags2;
        public Position Pos { get; set; }
        public uint Time { get; set; }
        public TransportInfo transport;
        public float Pitch { get; set; }
        public JumpInfo jump;
        public float SplineElevation { get; set; }

        public MovementInfo()
        {
            Guid = ObjectGuid.Empty;
            flags = MovementFlag.None;
            flags2 = MovementFlag2.None;
            Time = 0;
            Pitch = 0.0f;

            Pos = new Position();
            transport.Reset();
            jump.Reset();
        }

        public MovementFlag GetMovementFlags() => flags;
        public void SetMovementFlags(MovementFlag f) => flags = f;
        public void AddMovementFlag(MovementFlag f) => flags |= f;
        public void RemoveMovementFlag(MovementFlag f) => flags &= ~f;
        public bool HasMovementFlag(MovementFlag f) => Convert.ToBoolean(flags & f);

        public MovementFlag2 GetMovementFlags2() => flags2;
        public void SetMovementFlags2(MovementFlag2 f) => flags2 = f;
        public void AddMovementFlag2(MovementFlag2 f) => flags2 |= f;
        public void RemoveMovementFlag2(MovementFlag2 f) => flags2 &= ~f;
        public bool HasMovementFlag2(MovementFlag2 f) => Convert.ToBoolean(flags2 & f);

        public void SetFallTime(uint time) => jump.fallTime = time;

        public void ResetTransport() => transport.Reset();

        public void ResetJump() => jump.Reset();

        public struct TransportInfo
        {
            public void Reset()
            {
                guid = ObjectGuid.Empty;
                pos = new Position();
                seat = -1;
                time = 0;
                prevTime = 0;
                vehicleId = 0;
            }

            public ObjectGuid guid;
            public Position pos;
            public sbyte seat;
            public uint time;
            public uint prevTime;
            public uint vehicleId;
        }

        public struct JumpInfo
        {
            public void Reset()
            {
                fallTime = 0;
                zspeed = sinAngle = cosAngle = xyspeed = 0.0f;
            }

            public uint fallTime;
            public float zspeed;
            public float sinAngle;
            public float cosAngle;
            public float xyspeed;
        }
    }

    public class MovementForce
    {
        public ObjectGuid ID;
        public Vector3 Origin;
        public Vector3 Direction;
        public uint TransportID;
        public float Magnitude;
        public byte Type;

        public void Read(WorldPacket data)
        {
            ID = data.ReadPackedGuid();
            Origin = data.ReadVector3();
            Direction = data.ReadVector3();
            TransportID = data.ReadUInt32();
            Magnitude = data.ReadFloat();
            Type = data.ReadBits<byte>(2);

        }

        public void Write(WorldPacket data)
        {
            MovementExtensions.WriteMovementForceWithDirection(this, data);
        }
    }

    public class MovementForces
    {
        List<MovementForce> _forces = new();
        float _modMagnitude = 1.0f;

        public List<MovementForce> GetForces() => _forces;

        public bool Add(MovementForce newForce)
        {
            var movementForce = FindMovementForce(newForce.ID);
            if (movementForce == null)
            {
                _forces.Add(newForce);
                return true;
            }

            return false;
        }

        public bool Remove(ObjectGuid id)
        {
            var movementForce = FindMovementForce(id);
            if (movementForce != null)
            {
                _forces.Remove(movementForce);
                return true;
            }

            return false;
        }

        public float GetModMagnitude() => _modMagnitude;
        public void SetModMagnitude(float modMagnitude) => _modMagnitude = modMagnitude;

        public bool IsEmpty() => _forces.Empty() && _modMagnitude == 1.0f;

        MovementForce FindMovementForce(ObjectGuid id) => _forces.Find(force => force.ID == id);
    }

    public struct CreateObjectBits
    {
        public bool NoBirthAnim;
        public bool EnablePortals;
        public bool PlayHoverAnim;
        public bool MovementUpdate;
        public bool MovementTransport;
        public bool Stationary;
        public bool CombatVictim;
        public bool ServerTime;
        public bool Vehicle;
        public bool AnimKit;
        public bool Rotation;
        public bool AreaTrigger;
        public bool GameObject;
        public bool SmoothPhasing;
        public bool ThisIsYou;
        public bool SceneObject;
        public bool ActivePlayer;
        public bool Conversation;

        public void Clear()
        {
            NoBirthAnim = false;
            EnablePortals = false;
            PlayHoverAnim = false;
            MovementUpdate = false;
            MovementTransport = false;
            Stationary = false;
            CombatVictim = false;
            ServerTime = false;
            Vehicle = false;
            AnimKit = false;
            Rotation = false;
            AreaTrigger = false;
            GameObject = false;
            SmoothPhasing = false;
            ThisIsYou = false;
            SceneObject = false;
            ActivePlayer = false;
            Conversation = false;
        }
    }
}