﻿/*
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
using Game.BattleGrounds;
using Game.Chat;
using Game.Combat;
using Game.DataStorage;
using Game.Groups;
using Game.Maps;
using Game.Movement;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entities
{
    public partial class Unit : WorldObject
    {
        public Unit(bool isWorldObject) : base(isWorldObject)
        {
            MoveSpline = new MoveSpline();
            i_motionMaster = new MotionMaster(this);
            m_combatManager = new CombatManager(this);
            m_threatManager = new ThreatManager(this);
            _spellHistory = new SpellHistory(this);
            m_FollowingRefManager = new RefManager<Unit, ITargetedMovementGeneratorBase>();

            ObjectTypeId = TypeId.Unit;
            ObjectTypeMask |= TypeMask.Unit;
            m_updateFlag.MovementUpdate = true;

            m_modAttackSpeedPct = new float[] { 1.0f, 1.0f, 1.0f };
            m_deathState = DeathState.Alive;

            for (byte i = 0; i < (int)SpellImmunity.Max; ++i)
                m_spellImmune[i] = new MultiMap<uint, uint>();

            for (byte i = 0; i < (int)UnitMods.End; ++i)
            {
                m_auraFlatModifiersGroup[i] = new float[(int)UnitModifierFlatType.End];
                m_auraFlatModifiersGroup[i][(int)UnitModifierFlatType.Base] = 0.0f;
                m_auraFlatModifiersGroup[i][(int)UnitModifierFlatType.BasePCTExcludeCreate] = 100.0f;
                m_auraFlatModifiersGroup[i][(int)UnitModifierFlatType.Total] = 0.0f;

                m_auraPctModifiersGroup[i] = new float[(int)UnitModifierPctType.End];
                m_auraPctModifiersGroup[i][(int)UnitModifierPctType.Base] = 1.0f;
                m_auraPctModifiersGroup[i][(int)UnitModifierPctType.Total] = 1.0f;
            }

            m_auraPctModifiersGroup[(int)UnitMods.DamageOffHand][(int)UnitModifierPctType.Total] = 0.5f;

            foreach (AuraType auraType in Enum.GetValues(typeof(AuraType)))
                m_modAuras[auraType] = new List<AuraEffect>();

            for (byte i = 0; i < (int)WeaponAttackType.Max; ++i)
                m_weaponDamage[i] = new float[] { 1.0f, 2.0f };

            if (IsTypeId(TypeId.Player))
            {
                ModMeleeHitChance = 7.5f;
                ModRangedHitChance = 7.5f;
                ModSpellHitChance = 15.0f;
            }
            BaseSpellCritChance = 5;

            for (byte i = 0; i < (int)UnitMoveType.Max; ++i)
                m_speed_rate[i] = 1.0f;

            m_serverSideVisibility.SetValue(ServerSideVisibilityType.Ghost, GhostVisibilityType.Alive);

            movesplineTimer = new TimeTrackerSmall();

            ValuesCount = (int)UnitFields.End;
            m_dynamicValuesCount = (int)UnitDynamicFields.End;
        }

        public override void Dispose()
        {
            // set current spells as deletable
            for (CurrentSpellTypes i = 0; i < CurrentSpellTypes.Max; ++i)
            {
                if (m_currentSpells.ContainsKey(i))
                {
                    if (m_currentSpells[i] != null)
                    {
                        m_currentSpells[i].SetReferencedFromCurrent(false);
                        m_currentSpells[i] = null;
                    }
                }
            }

            m_Events.KillAllEvents(true);

            _DeleteRemovedAuras();

            //i_motionMaster = null;
            m_charmInfo = null;
            MoveSpline = null;
            _spellHistory = null;

            /*ASSERT(!m_duringRemoveFromWorld);
            ASSERT(!m_attacking);
            ASSERT(m_attackers.empty());
            ASSERT(m_sharedVision.empty());
            ASSERT(m_Controlled.empty());
            ASSERT(m_appliedAuras.empty());
            ASSERT(m_ownedAuras.empty());
            ASSERT(m_removedAuras.empty());
            ASSERT(m_gameObj.empty());
            ASSERT(m_dynObj.empty());*/

            base.Dispose();
        }

        public override void Update(uint diff)
        {
            // WARNING! Order of execution here is important, do not change.
            // Spells must be processed with event system BEFORE they go to _UpdateSpells.
            m_Events.Update(diff);

            if (!IsInWorld)
                return;

            _UpdateSpells(diff);

            // If this is set during update SetCantProc(false) call is missing somewhere in the code
            // Having this would prevent spells from being proced, so let's crash
            Cypher.Assert(m_procDeep == 0);

            m_combatManager.Update(diff);

            uint att;
            // not implemented before 3.0.2
            if ((att = GetAttackTimer(WeaponAttackType.BaseAttack)) != 0)
                SetAttackTimer(WeaponAttackType.BaseAttack, (diff >= att ? 0 : att - diff));
            if ((att = GetAttackTimer(WeaponAttackType.RangedAttack)) != 0)
                SetAttackTimer(WeaponAttackType.RangedAttack, (diff >= att ? 0 : att - diff));
            if ((att = GetAttackTimer(WeaponAttackType.OffAttack)) != 0)
                SetAttackTimer(WeaponAttackType.OffAttack, (diff >= att ? 0 : att - diff));

            // update abilities available only for fraction of time
            UpdateReactives(diff);

            if (IsAlive())
            {
                ModifyAuraState(AuraStateType.Wounded20Percent, HealthBelowPct(20));
                ModifyAuraState(AuraStateType.Wounded25Percent, HealthBelowPct(25));
                ModifyAuraState(AuraStateType.Wounded35Percent, HealthBelowPct(35));
                ModifyAuraState(AuraStateType.WoundHealth20_80, HealthBelowPct(20) || HealthAbovePct(80));
                ModifyAuraState(AuraStateType.Healthy75Percent, HealthAbovePct(75));
                ModifyAuraState(AuraStateType.WoundHealth35_80, HealthBelowPct(35) || HealthAbovePct(80));
            }

            UpdateSplineMovement(diff);
            GetMotionMaster().UpdateMotion(diff);
        }

        void _UpdateSpells(uint diff)
        {
            if (GetCurrentSpell(CurrentSpellTypes.AutoRepeat) != null)
                _UpdateAutoRepeatSpell();

            for (CurrentSpellTypes i = 0; i < CurrentSpellTypes.Max; ++i)
            {
                if (GetCurrentSpell(i) != null && m_currentSpells[i].GetState() == SpellState.Finished)
                {
                    m_currentSpells[i].SetReferencedFromCurrent(false);
                    m_currentSpells[i] = null;
                }
            }

            foreach (var app in GetOwnedAuras())
            {
                Aura i_aura = app.Value;
                if (i_aura == null)
                    continue;

                i_aura.UpdateOwner(diff, this);
            }

            // remove expired auras - do that after updates(used in scripts?)
            foreach (var pair in GetOwnedAuras())
            {
                if (pair.Value != null && pair.Value.IsExpired())
                    RemoveOwnedAura(pair, AuraRemoveMode.Expire);
            }

            foreach (var aura in m_visibleAurasToUpdate)
                aura.ClientUpdate();

            m_visibleAurasToUpdate.Clear();

            _DeleteRemovedAuras();

            if (!m_gameObj.Empty())
            {
                for (var i = 0; i < m_gameObj.Count; ++i)
                {
                    GameObject go = m_gameObj[i];
                    if (!go.IsSpawned())
                    {
                        go.SetOwnerGUID(ObjectGuid.Empty);
                        go.SetRespawnTime(0);
                        go.Delete();
                        m_gameObj.Remove(go);
                    }
                }
            }

            _spellHistory.Update();
        }

        public void HandleEmoteCommand(Emote animId, uint[] spellVisualKitIds = null)
        {
            EmoteMessage packet = new();
            packet.Guid = GetGUID();
            packet.EmoteID = (uint)animId;

            var emotesEntry = CliDB.EmotesStorage.LookupByKey(animId);
            if (emotesEntry != null && spellVisualKitIds != null)
                if (emotesEntry.AnimID == (uint)Anim.MountSpecial || emotesEntry.AnimID == (uint)Anim.MountSelfSpecial)
                    packet.SpellVisualKitIDs.AddRange(spellVisualKitIds);

            SendMessageToSet(packet, true);
        }

        public void SendDurabilityLoss(Player receiver, uint percent)
        {
            DurabilityDamageDeath packet = new();
            packet.Percent = percent;
            receiver.SendPacket(packet);
        }

        public bool IsInDisallowedMountForm() => IsDisallowedMountForm(GetTransForm(), GetShapeshiftForm(), GetDisplayId());

        public bool IsDisallowedMountForm(uint spellId, ShapeShiftForm form, uint displayId)
        {
            SpellInfo transformSpellInfo = Global.SpellMgr.GetSpellInfo(spellId, GetMap().GetDifficultyID());
            if (transformSpellInfo != null)
                if (transformSpellInfo.HasAttribute(SpellAttr0.CastableWhileMounted))
                    return false;

            if (form != 0)
            {
                SpellShapeshiftFormRecord shapeshift = CliDB.SpellShapeshiftFormStorage.LookupByKey(form);
                if (shapeshift == null)
                    return true;

                if (!shapeshift.Flags.HasAnyFlag(SpellShapeshiftFormFlags.Stance))
                    return true;
            }
            if (displayId == GetNativeDisplayId())
                return false;

            CreatureDisplayInfoRecord display = CliDB.CreatureDisplayInfoStorage.LookupByKey(displayId);
            if (display == null)
                return true;

            CreatureDisplayInfoExtraRecord displayExtra = CliDB.CreatureDisplayInfoExtraStorage.LookupByKey(display.ExtendedDisplayInfoID);
            if (displayExtra == null)
                return true;

            CreatureModelDataRecord model = CliDB.CreatureModelDataStorage.LookupByKey(display.ModelID);
            ChrRacesRecord race = CliDB.ChrRacesStorage.LookupByKey(displayExtra.DisplayRaceID);

            if (model != null && !Convert.ToBoolean(model.Flags & 0x80))
                if (race != null && !race.Flags.HasAnyFlag(ChrRacesFlag.Unk0x04))
                    return true;

            return false;
        }

        public void SendClearTarget()
        {
            BreakTarget breakTarget = new();
            breakTarget.UnitGUID = GetGUID();
            SendMessageToSet(breakTarget, false);
        }
        public virtual bool IsLoading() => false;
        public bool IsDuringRemoveFromWorld() => m_duringRemoveFromWorld;

        //SharedVision
        public bool HasSharedVision() => !m_sharedVision.Empty();
        public List<Player> GetSharedVisionList() => m_sharedVision;

        public void AddPlayerToVision(Player player)
        {
            if (m_sharedVision.Empty())
            {
                SetActive(true);
                SetWorldObject(true);
            }
            m_sharedVision.Add(player);
        }

        // only called in Player.SetSeer
        public void RemovePlayerFromVision(Player player)
        {
            m_sharedVision.Remove(player);
            if (m_sharedVision.Empty())
            {
                SetActive(false);
                SetWorldObject(false);
            }
        }

        public virtual void Talk(string text, ChatMsg msgType, Language language, float textRange, WorldObject target)
        {
            var builder = new CustomChatTextBuilder(this, msgType, text, language, target);
            var localizer = new LocalizedDo(builder);
            var worker = new PlayerDistWorker(this, textRange, localizer);
            Cell.VisitWorldObjects(this, worker, textRange);
        }

        public virtual void Say(string text, Language language, WorldObject target = null) =>
            Talk(text, ChatMsg.MonsterSay, language, WorldConfig.GetFloatValue(WorldCfg.ListenRangeSay), target);

        public virtual void Yell(string text, Language language, WorldObject target = null) =>
            Talk(text, ChatMsg.MonsterYell, language, WorldConfig.GetFloatValue(WorldCfg.ListenRangeYell), target);

        public virtual void TextEmote(string text, WorldObject target = null, bool isBossEmote = false) =>
            Talk(text, isBossEmote ? ChatMsg.RaidBossEmote : ChatMsg.MonsterEmote, Language.Universal, WorldConfig.GetFloatValue(WorldCfg.ListenRangeTextemote), target);

        public virtual void Whisper(string text, Language language, Player target, bool isBossWhisper = false)
        {
            if (!target)
                return;

            Locale locale = target.GetSession().GetSessionDbLocaleIndex();
            ChatPkt data = new();
            data.Initialize(isBossWhisper ? ChatMsg.RaidBossWhisper : ChatMsg.MonsterWhisper, Language.Universal, this, target, text, 0, "", locale);
            target.SendPacket(data);
        }

        public void Talk(uint textId, ChatMsg msgType, float textRange, WorldObject target)
        {
            if (!CliDB.BroadcastTextStorage.ContainsKey(textId))
            {
                Log.outError(LogFilter.Unit, "Unit.Talk: `broadcast_text` (Id: {0}) was not found", textId);
                return;
            }

            var builder = new BroadcastTextBuilder(this, msgType, textId, GetGender(), target);
            var localizer = new LocalizedDo(builder);
            var worker = new PlayerDistWorker(this, textRange, localizer);
            Cell.VisitWorldObjects(this, worker, textRange);
        }

        public virtual void Say(uint textId, WorldObject target = null) =>
            Talk(textId, ChatMsg.MonsterSay, WorldConfig.GetFloatValue(WorldCfg.ListenRangeSay), target);

        public virtual void Yell(uint textId, WorldObject target = null) =>
            Talk(textId, ChatMsg.MonsterYell, WorldConfig.GetFloatValue(WorldCfg.ListenRangeYell), target);

        public virtual void TextEmote(uint textId, WorldObject target = null, bool isBossEmote = false) =>
            Talk(textId, isBossEmote ? ChatMsg.RaidBossEmote : ChatMsg.MonsterEmote, WorldConfig.GetFloatValue(WorldCfg.ListenRangeTextemote), target);

        public virtual void Whisper(uint textId, Player target, bool isBossWhisper = false)
        {
            if (!target)
                return;

            BroadcastTextRecord bct = CliDB.BroadcastTextStorage.LookupByKey(textId);
            if (bct == null)
            {
                Log.outError(LogFilter.Unit, "Unit.Whisper: `broadcast_text` was not {0} found", textId);
                return;
            }

            Locale locale = target.GetSession().GetSessionDbLocaleIndex();
            ChatPkt data = new();
            data.Initialize(isBossWhisper ? ChatMsg.RaidBossWhisper : ChatMsg.MonsterWhisper, Language.Universal, this, target, Global.DB2Mgr.GetBroadcastTextValue(bct, locale, GetGender()), 0, "", locale);
            target.SendPacket(data);
        }

        public override void UpdateObjectVisibility(bool forced = true)
        {
            if (!forced)
                AddToNotify(NotifyFlags.VisibilityChanged);
            else
            {
                m_threatManager.UpdateOnlineStates(true, true);
                base.UpdateObjectVisibility(true);
                // call MoveInLineOfSight for nearby creatures
                AIRelocationNotifier notifier = new(this);
                Cell.VisitAllObjects(this, notifier, GetVisibilityRange());
            }
        }

        public override void AddToWorld()
        {
            base.AddToWorld();

            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.EnterWorld);
        }

        public override void RemoveFromWorld()
        {
            // cleanup

            if (IsInWorld)
            {
                m_duringRemoveFromWorld = true;
                if (IsVehicle())
                    RemoveVehicleKit(true);

                RemoveCharmAuras();
                RemoveAurasByType(AuraType.BindSight);
                RemoveNotOwnSingleTargetAuras();
                RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.LeaveWorld);

                RemoveAllGameObjects();
                RemoveAllDynObjects();
                RemoveAllAreaTriggers();

                ExitVehicle();  // Remove applied auras with SPELL_AURA_CONTROL_VEHICLE
                UnsummonAllTotems();
                RemoveAllControlled();

                RemoveAreaAurasDueToLeaveWorld();

                if (!GetCharmerGUID().IsEmpty())
                {
                    Log.outFatal(LogFilter.Unit, "Unit {0} has charmer guid when removed from world", GetEntry());
                }
                Unit owner = GetOwner();
                if (owner != null)
                {
                    if (owner.m_Controlled.Contains(this))
                    {
                        Log.outFatal(LogFilter.Unit, "Unit {0} is in controlled list of {1} when removed from world", GetEntry(), owner.GetEntry());
                    }
                }

                base.RemoveFromWorld();
                m_duringRemoveFromWorld = false;
            }
        }

        public void CleanupBeforeRemoveFromMap(bool finalCleanup)
        {
            // This needs to be before RemoveFromWorld to make GetCaster() return a valid for aura removal
            InterruptNonMeleeSpells(true);

            if (IsInWorld)
                RemoveFromWorld();

            // A unit may be in removelist and not in world, but it is still in grid
            // and may have some references during delete
            RemoveAllAuras();
            RemoveAllGameObjects();

            if (finalCleanup)
                m_cleanupDone = true;

            m_Events.KillAllEvents(false);                      // non-delatable (currently casted spells) will not deleted now but it will deleted at call in Map.RemoveAllObjectsInRemoveList
            CombatStop();
        }
        public override void CleanupsBeforeDelete(bool finalCleanup = true)
        {
            CleanupBeforeRemoveFromMap(finalCleanup);

            base.CleanupsBeforeDelete(finalCleanup);
        }

        public void SetTransForm(uint spellid) => m_transform = spellid;
        public uint GetTransForm() => m_transform;

        public Vehicle GetVehicleKit() => VehicleKit;
        public Vehicle GetVehicle() => m_vehicle;
        public void SetVehicle(Vehicle vehicle) => m_vehicle = vehicle;
        public Unit GetVehicleBase() => m_vehicle != null ? m_vehicle.GetBase() : null;
        public Creature GetVehicleCreatureBase()
        {
            Unit veh = GetVehicleBase();
            if (veh != null)
            {
                Creature c = veh.ToCreature();
                if (c != null)
                    return c;
            }
            return null;
        }
        public ITransport GetDirectTransport()
        {
            Vehicle veh = GetVehicle();
            if (veh != null)
                return veh;
            return GetTransport();
        }

        public void _RegisterDynObject(DynamicObject dynObj)
        {
            m_dynObj.Add(dynObj);
            if (IsTypeId(TypeId.Unit) && IsAIEnabled)
                ToCreature().GetAI().JustRegisteredDynObject(dynObj);
        }

        public void _UnregisterDynObject(DynamicObject dynObj)
        {
            m_dynObj.Remove(dynObj);
            if (IsTypeId(TypeId.Unit) && IsAIEnabled)
                ToCreature().GetAI().JustUnregisteredDynObject(dynObj);
        }

        public DynamicObject GetDynObject(uint spellId) => GetDynObjects(spellId).FirstOrDefault();

        List<DynamicObject> GetDynObjects(uint spellId)
        {
            List<DynamicObject> dynamicobjects = new();
            foreach (var obj in m_dynObj)
                if (obj.GetSpellId() == spellId)
                    dynamicobjects.Add(obj);

            return dynamicobjects;
        }

        public void RemoveDynObject(uint spellId)
        {
            for (var i = 0; i < m_dynObj.Count; ++i)
            {
                var dynObj = m_dynObj[i];
                if (dynObj.GetSpellId() == spellId)
                    dynObj.Remove();
            }
        }

        public void RemoveAllDynObjects()
        {
            while (!m_dynObj.Empty())
                m_dynObj.First().Remove();
        }

        public GameObject GetGameObject(uint spellId) => GetGameObjects(spellId).FirstOrDefault();

        List<GameObject> GetGameObjects(uint spellId)
        {
            List<GameObject> gameobjects = new();
            foreach (var obj in m_gameObj)
                if (obj.GetSpellId() == spellId)
                    gameobjects.Add(obj);

            return gameobjects;
        }

        public void AddGameObject(GameObject gameObj)
        {
            if (gameObj == null || !gameObj.GetOwnerGUID().IsEmpty())
                return;

            m_gameObj.Add(gameObj);
            gameObj.SetOwnerGUID(GetGUID());

            if (gameObj.GetSpellId() != 0)
            {
                SpellInfo createBySpell = Global.SpellMgr.GetSpellInfo(gameObj.GetSpellId(), GetMap().GetDifficultyID());
                // Need disable spell use for owner
                if (createBySpell != null && createBySpell.HasAttribute(SpellAttr0.DisabledWhileActive))
                    // note: item based cooldowns and cooldown spell mods with charges ignored (unknown existing cases)
                    GetSpellHistory().StartCooldown(createBySpell, 0, null, true);
            }

            if (IsTypeId(TypeId.Unit) && ToCreature().IsAIEnabled)
                ToCreature().GetAI().JustSummonedGameobject(gameObj);
        }

        public void RemoveGameObject(GameObject gameObj, bool del)
        {
            if (gameObj == null || gameObj.GetOwnerGUID() != GetGUID())
                return;

            gameObj.SetOwnerGUID(ObjectGuid.Empty);

            for (byte i = 0; i < SharedConst.MaxGameObjectSlot; ++i)
            {
                if (m_ObjectSlot[i] == gameObj.GetGUID())
                {
                    m_ObjectSlot[i].Clear();
                    break;
                }
            }

            // GO created by some spell
            uint spellid = gameObj.GetSpellId();
            if (spellid != 0)
            {
                RemoveAurasDueToSpell(spellid);

                SpellInfo createBySpell = Global.SpellMgr.GetSpellInfo(spellid, GetMap().GetDifficultyID());
                // Need activate spell use for owner
                if (createBySpell != null && createBySpell.IsCooldownStartedOnEvent())
                    // note: item based cooldowns and cooldown spell mods with charges ignored (unknown existing cases)
                    GetSpellHistory().SendCooldownEvent(createBySpell);
            }

            m_gameObj.Remove(gameObj);

            if (IsTypeId(TypeId.Unit) && ToCreature().IsAIEnabled)
                ToCreature().GetAI().SummonedGameobjectDespawn(gameObj);

            if (del)
            {
                gameObj.SetRespawnTime(0);
                gameObj.Delete();
            }
        }

        public void RemoveGameObject(uint spellid, bool del)
        {
            if (m_gameObj.Empty())
                return;

            for (var i =0; i < m_gameObj.Count; ++i)
            {
                var obj = m_gameObj[i];
                if (spellid == 0 || obj.GetSpellId() == spellid)
                {
                    obj.SetOwnerGUID(ObjectGuid.Empty);
                    if (del)
                    {
                        obj.SetRespawnTime(0);
                        obj.Delete();
                    }

                    m_gameObj.Remove(obj);
                }
            }
        }

        public void RemoveAllGameObjects()
        {
            // remove references to unit
            while (!m_gameObj.Empty())
            {
                var obj = m_gameObj.First();
                obj.SetOwnerGUID(ObjectGuid.Empty);
                obj.SetRespawnTime(0);
                obj.Delete();
                m_gameObj.Remove(obj);
            }
        }

        public void _RegisterAreaTrigger(AreaTrigger areaTrigger)
        {
            m_areaTrigger.Add(areaTrigger);
            if (IsTypeId(TypeId.Unit) && IsAIEnabled)
                ToCreature().GetAI().JustRegisteredAreaTrigger(areaTrigger);
        }

        public void _UnregisterAreaTrigger(AreaTrigger areaTrigger)
        {
            m_areaTrigger.Remove(areaTrigger);
            if (IsTypeId(TypeId.Unit) && IsAIEnabled)
                ToCreature().GetAI().JustUnregisteredAreaTrigger(areaTrigger);
        }

        AreaTrigger GetAreaTrigger(uint spellId)
        {
            List<AreaTrigger> areaTriggers = GetAreaTriggers(spellId);
            return areaTriggers.Empty() ? null : areaTriggers[0];
        }

        public List<AreaTrigger> GetAreaTriggers(uint spellId) => m_areaTrigger.Where(trigger => trigger.GetSpellId() == spellId).ToList();

        public void RemoveAreaTrigger(uint spellId)
        {
            if (m_areaTrigger.Empty())
                return;

            for (var i = 0; i < m_areaTrigger.Count; ++i)
            {
                AreaTrigger areaTrigger = m_areaTrigger[i];
                if (areaTrigger.GetSpellId() == spellId)
                    areaTrigger.Remove();
            }
        }

        public void RemoveAreaTrigger(AuraEffect aurEff)
        {
            if (m_areaTrigger.Empty())
                return;

            foreach (AreaTrigger areaTrigger in m_areaTrigger)
            {
                if (areaTrigger.GetAuraEffect() == aurEff)
                {
                    areaTrigger.Remove();
                    break; // There can only be one AreaTrigger per AuraEffect
                }
            }
        }

        public void RemoveAllAreaTriggers()
        {
            while (!m_areaTrigger.Empty())
                m_areaTrigger[0].Remove();
        }

        public bool HasNpcFlag(NPCFlags flags) => ((NPCFlags)GetUpdateField<uint>(UnitFields.NpcFlags)).HasAnyFlag(flags);
        public void AddNpcFlag(NPCFlags flags) => AddFlag(UnitFields.NpcFlags, flags);
        public void RemoveNpcFlag(NPCFlags flags) => RemoveFlag(UnitFields.NpcFlags, flags);
        public void SetNpcFlags(NPCFlags flags) => SetUpdateField<uint>(UnitFields.NpcFlags, (uint)flags);
        public bool HasNpcFlag2(NPCFlags2 flags) => ((NPCFlags2)GetUpdateField<uint>(UnitFields.NpcFlags + 1)).HasAnyFlag(flags);
        public void AddNpcFlag2(NPCFlags2 flags) => AddFlag(UnitFields.NpcFlags + 1, flags);
        public void RemoveNpcFlag2(NPCFlags2 flags) => RemoveFlag(UnitFields.NpcFlags + 1, flags);
        public void SetNpcFlags2(NPCFlags2 flags) => SetUpdateField<uint>(UnitFields.NpcFlags + 1, (uint)flags);

        public bool IsVendor() => HasNpcFlag(NPCFlags.Vendor);
        public bool IsTrainer() => HasNpcFlag(NPCFlags.Trainer);
        public bool IsQuestGiver() => HasNpcFlag(NPCFlags.QuestGiver);
        public bool IsGossip() => HasNpcFlag(NPCFlags.Gossip);
        public bool IsTaxi() => HasNpcFlag(NPCFlags.FlightMaster);
        public bool IsGuildMaster() => HasNpcFlag(NPCFlags.Petitioner);
        public bool IsBattleMaster() => HasNpcFlag(NPCFlags.BattleMaster);
        public bool IsBanker() => HasNpcFlag(NPCFlags.Banker);
        public bool IsInnkeeper() => HasNpcFlag(NPCFlags.Innkeeper);
        public bool IsSpiritHealer() => HasNpcFlag(NPCFlags.SpiritHealer);
        public bool IsSpiritGuide() => HasNpcFlag(NPCFlags.SpiritGuide);
        public bool IsTabardDesigner() => HasNpcFlag(NPCFlags.TabardDesigner);
        public bool IsAuctioner() => HasNpcFlag(NPCFlags.Auctioneer);
        public bool IsArmorer() => HasNpcFlag(NPCFlags.Repair);
        public bool IsServiceProvider() => HasNpcFlag(NPCFlags.Vendor | NPCFlags.Trainer | NPCFlags.FlightMaster |
            NPCFlags.Petitioner | NPCFlags.BattleMaster | NPCFlags.Banker | NPCFlags.Innkeeper |
            NPCFlags.SpiritHealer | NPCFlags.SpiritGuide | NPCFlags.TabardDesigner | NPCFlags.Auctioneer);
        public bool IsSpiritService() => HasNpcFlag(NPCFlags.SpiritHealer | NPCFlags.SpiritGuide);
        public bool IsCritter() => GetCreatureType() == CreatureType.Critter;
        public bool IsInFlight() => HasUnitState(UnitState.InFlight);

        public bool IsContestedGuard()
        {
            var entry = GetFactionTemplateEntry();
            if (entry != null)
                return entry.IsContestedGuardFaction();

            return false;
        }

        public float GetHoverHeight() => GetUpdateField<float>(UnitFields.HoverHeight);
        public void SetHoverHeight(float hoverHeight) => SetUpdateField<float>(UnitFields.HoverHeight, hoverHeight);

        public override float GetCollisionHeight()
        {
            float scaleMod = GetObjectScale(); // 99% sure about this

            if (IsMounted())
            {
                var mountDisplayInfo = CliDB.CreatureDisplayInfoStorage.LookupByKey(GetMountDisplayId());
                if (mountDisplayInfo != null)
                {
                    var mountModelData = CliDB.CreatureModelDataStorage.LookupByKey(mountDisplayInfo.ModelID);
                    if (mountModelData != null)
                    {
                        var displayInfo = CliDB.CreatureDisplayInfoStorage.LookupByKey(GetNativeDisplayId());
                        var modelData = CliDB.CreatureModelDataStorage.LookupByKey(displayInfo.ModelID);
                        float collisionHeight = scaleMod * (mountModelData.MountHeight + modelData.CollisionHeight * modelData.ModelScale * displayInfo.CreatureModelScale * 0.5f);
                        return collisionHeight == 0.0f ? MapConst.DefaultCollesionHeight : collisionHeight;
                    }
                }
            }

            //! Dismounting case - use basic default model data
            var defaultDisplayInfo = CliDB.CreatureDisplayInfoStorage.LookupByKey(GetNativeDisplayId());
            var defaultModelData = CliDB.CreatureModelDataStorage.LookupByKey(defaultDisplayInfo.ModelID);

            float collisionHeight1 = scaleMod * defaultModelData.CollisionHeight * defaultModelData.ModelScale * defaultDisplayInfo.CreatureModelScale;
            return collisionHeight1 == 0.0f ? MapConst.DefaultCollesionHeight : collisionHeight1;
        }

        public Guardian GetGuardianPet()
        {
            ObjectGuid pet_guid = GetPetGUID();
            if (!pet_guid.IsEmpty())
            {
                Creature pet = ObjectAccessor.GetCreatureOrPetOrVehicle(this, pet_guid);
                if (pet != null)
                    if (pet.HasUnitTypeMask(UnitTypeMask.Guardian))
                        return (Guardian)pet;

                Log.outFatal(LogFilter.Unit, "Unit:GetGuardianPet: Guardian {0} not exist.", pet_guid);
                SetPetGUID(ObjectGuid.Empty);
            }

            return null;
        }

        public Unit SelectNearbyTarget(Unit exclude = null, float dist = SharedConst.NominalMeleeRange)
        {
            List<Unit> targets = new();
            var u_check = new AnyUnfriendlyUnitInObjectRangeCheck(this, this, dist);
            var searcher = new UnitListSearcher(this, targets, u_check);
            Cell.VisitAllObjects(this, searcher, dist);

            // remove current target
            if (GetVictim())
                targets.Remove(GetVictim());

            if (exclude)
                targets.Remove(exclude);

            // remove not LoS targets
            foreach (var unit in targets)
            {
                if (!IsWithinLOSInMap(unit) || unit.IsTotem() || unit.IsSpiritService() || unit.IsCritter())
                    targets.Remove(unit);
            }

            // no appropriate targets
            if (targets.Empty())
                return null;

            // select random
            return targets.SelectRandom();
        }

        public void EnterVehicle(Unit baseUnit, sbyte seatId = -1)
        {
            CastSpellExtraArgs args = new(TriggerCastFlags.IgnoreCasterMountedOrOnVehicle);
            args.AddSpellMod(SpellValueMod.BasePoint0, seatId + 1);
            CastSpell(baseUnit, SharedConst.VehicleSpellRideHardcoded, args);
        }

        public void _EnterVehicle(Vehicle vehicle, sbyte seatId, AuraApplication aurApp)
        {
            // Must be called only from aura handler
            Cypher.Assert(aurApp != null);

            if (!IsAlive() || GetVehicleKit() == vehicle || vehicle.GetBase().IsOnVehicle(this))
                return;

            if (m_vehicle != null)
            {
                if (m_vehicle != vehicle)
                {
                    Log.outDebug(LogFilter.Vehicle, "EnterVehicle: {0} exit {1} and enter {2}.", GetEntry(), m_vehicle.GetBase().GetEntry(), vehicle.GetBase().GetEntry());
                    ExitVehicle();
                }
                else if (seatId >= 0 && seatId == GetTransSeat())
                    return;
            }

            if (aurApp.HasRemoveMode())
                return;

            Player player = ToPlayer();
            if (player != null)
            {
                if (vehicle.GetBase().IsTypeId(TypeId.Player) && player.IsInCombat())
                {
                    vehicle.GetBase().RemoveAura(aurApp);
                    return;
                }
            }

            Cypher.Assert(!m_vehicle);
            vehicle.AddPassenger(this, seatId);
        }

        public void ChangeSeat(sbyte seatId, bool next = true)
        {
            if (m_vehicle == null)
                return;

            // Don't change if current and new seat are identical
            if (seatId == GetTransSeat())
                return;

            var seat = (seatId < 0 ? m_vehicle.GetNextEmptySeat(GetTransSeat(), next) : m_vehicle.Seats.LookupByKey(seatId));
            // The second part of the check will only return true if seatId >= 0. @Vehicle.GetNextEmptySeat makes sure of that.
            if (seat == null || !seat.IsEmpty())
                return;

            AuraEffect rideVehicleEffect = null;
            var vehicleAuras = m_vehicle.GetBase().GetAuraEffectsByType(AuraType.ControlVehicle);
            foreach (var eff in vehicleAuras)
            {
                if (eff.GetCasterGUID() != GetGUID())
                    continue;

                // Make sure there is only one ride vehicle aura on target cast by the unit changing seat
                Cypher.Assert(rideVehicleEffect == null);
                rideVehicleEffect = eff;
            }

            // Unit riding a vehicle must always have control vehicle aura on target
            Cypher.Assert(rideVehicleEffect != null);

            rideVehicleEffect.ChangeAmount((seatId < 0 ? GetTransSeat() : seatId) + 1);
        }

        public void ExitVehicle(Position exitPosition = null)
        {
            //! This function can be called at upper level code to initialize an exit from the passenger's side.
            if (m_vehicle == null)
                return;

            GetVehicleBase().RemoveAurasByType(AuraType.ControlVehicle, GetGUID());
            //! The following call would not even be executed successfully as the
            //! SPELL_AURA_CONTROL_VEHICLE unapply handler already calls _ExitVehicle without
            //! specifying an exitposition. The subsequent call below would return on if (!m_vehicle).

            //! To do:
            //! We need to allow SPELL_AURA_CONTROL_VEHICLE unapply handlers in spellscripts
            //! to specify exit coordinates and either store those per passenger, or we need to
            //! init spline movement based on those coordinates in unapply handlers, and
            //! relocate exiting passengers based on Unit.moveSpline data. Either way,
            //! Coming Soon(TM)
        }

        public void _ExitVehicle(Position exitPosition = null)
        {
            // It's possible m_vehicle is NULL, when this function is called indirectly from @VehicleJoinEvent.Abort.
            // In that case it was not possible to add the passenger to the vehicle. The vehicle aura has already been removed
            // from the target in the aforementioned function and we don't need to do anything else at this point.
            if (m_vehicle == null)
                return;

            // This should be done before dismiss, because there may be some aura removal
            Vehicle vehicle = m_vehicle.RemovePassenger(this);

            Player player = ToPlayer();

            // If the player is on mounted duel and exits the mount, he should immediatly lose the duel
            if (player && player.duel != null && player.duel.isMounted)
                player.DuelComplete(DuelCompleteType.Fled);

            SetControlled(false, UnitState.Root);      // SMSG_MOVE_FORCE_UNROOT, ~MOVEMENTFLAG_ROOT

            Position pos;
            if (exitPosition == null)                          // Exit position not specified
                pos = vehicle.GetBase().GetPosition();  // This should use passenger's current position, leaving it as it is now
            // because we calculate positions incorrect (sometimes under map)
            else
                pos = exitPosition;

            AddUnitState(UnitState.Move);

            if (player != null)
                player.SetFallInformation(0, GetPositionZ());

            float height = pos.GetPositionZ() + vehicle.GetBase().GetCollisionHeight();

            MoveSplineInit init = new(this);

            // Creatures without inhabit type air should begin falling after exiting the vehicle
            if (IsTypeId(TypeId.Unit) && !ToCreature().CanFly() && height > GetMap().GetWaterOrGroundLevel(GetPhaseShift(), pos.GetPositionX(), pos.GetPositionY(), pos.GetPositionZ() + vehicle.GetBase().GetCollisionHeight(), ref height))
                init.SetFall();

            init.MoveTo(pos.GetPositionX(), pos.GetPositionY(), height, false);
            init.SetFacing(GetOrientation());
            init.SetTransportExit();
            init.Launch();

            if (player != null)
                player.ResummonPetTemporaryUnSummonedIfAny();

            if (vehicle.GetBase().HasUnitTypeMask(UnitTypeMask.Minion) && vehicle.GetBase().IsTypeId(TypeId.Unit))
                if (((Minion)vehicle.GetBase()).GetOwner() == this)
                    vehicle.GetBase().ToCreature().DespawnOrUnsummon(vehicle.GetDespawnDelay());

            if (HasUnitTypeMask(UnitTypeMask.Accessory))
            {
                // Vehicle just died, we die too
                if (vehicle.GetBase().GetDeathState() == DeathState.JustDied)
                    SetDeathState(DeathState.JustDied);
                // If for other reason we as minion are exiting the vehicle (ejected, master dismounted) - unsummon
                else
                    ToTempSummon().UnSummon(2000); // Approximation
            }
        }

        void SendCancelOrphanSpellVisual(uint id)
        {
            CancelOrphanSpellVisual cancelOrphanSpellVisual = new();
            cancelOrphanSpellVisual.SpellVisualID = id;
            SendMessageToSet(cancelOrphanSpellVisual, true);
        }

        void SendPlayOrphanSpellVisual(ObjectGuid target, uint spellVisualId, float travelSpeed, bool speedAsTime = false, bool withSourceOrientation = false)
        {
            PlayOrphanSpellVisual playOrphanSpellVisual = new();
            playOrphanSpellVisual.SourceLocation = GetPosition();
            if (withSourceOrientation)
                playOrphanSpellVisual.SourceRotation = new Vector3(0.0f, 0.0f, GetOrientation());
            playOrphanSpellVisual.Target = target; // exclusive with TargetLocation
            playOrphanSpellVisual.SpellVisualID = spellVisualId;
            playOrphanSpellVisual.TravelSpeed = travelSpeed;
            playOrphanSpellVisual.SpeedAsTime = speedAsTime;
            playOrphanSpellVisual.LaunchDelay = 0.0f;
            SendMessageToSet(playOrphanSpellVisual, true);
        }

        void SendPlayOrphanSpellVisual(Vector3 targetLocation, uint spellVisualId, float travelSpeed, bool speedAsTime = false, bool withSourceOrientation = false)
        {
            PlayOrphanSpellVisual playOrphanSpellVisual = new();
            playOrphanSpellVisual.SourceLocation = GetPosition();
            if (withSourceOrientation)
                playOrphanSpellVisual.SourceRotation = new Vector3(0.0f, 0.0f, GetOrientation());
            playOrphanSpellVisual.TargetLocation = targetLocation; // exclusive with Target
            playOrphanSpellVisual.SpellVisualID = spellVisualId;
            playOrphanSpellVisual.TravelSpeed = travelSpeed;
            playOrphanSpellVisual.SpeedAsTime = speedAsTime;
            playOrphanSpellVisual.LaunchDelay = 0.0f;
            SendMessageToSet(playOrphanSpellVisual, true);
        }

        void SendCancelSpellVisual(uint id)
        {
            CancelSpellVisual cancelSpellVisual = new();
            cancelSpellVisual.Source = GetGUID();
            cancelSpellVisual.SpellVisualID = id;
            SendMessageToSet(cancelSpellVisual, true);
        }

        public void SendPlaySpellVisual(ObjectGuid targetGuid, uint spellVisualId, uint missReason, uint reflectStatus, float travelSpeed, bool speedAsTime = false)
        {
            PlaySpellVisual playSpellVisual = new();
            playSpellVisual.Source = GetGUID();
            playSpellVisual.Target = targetGuid; // exclusive with TargetPosition
            playSpellVisual.SpellVisualID = spellVisualId;
            playSpellVisual.TravelSpeed = travelSpeed;
            playSpellVisual.MissReason = (ushort)missReason;
            playSpellVisual.ReflectStatus = (ushort)reflectStatus;
            playSpellVisual.SpeedAsTime = speedAsTime;
            SendMessageToSet(playSpellVisual, true);
        }

        public void SendPlaySpellVisual(Vector3 targetPosition, float launchDelay, uint spellVisualId, uint missReason, uint reflectStatus, float travelSpeed, bool speedAsTime = false)
        {
            PlaySpellVisual playSpellVisual = new();
            playSpellVisual.Source = GetGUID();
            playSpellVisual.TargetPosition = targetPosition; // exclusive with Target
            playSpellVisual.LaunchDelay = launchDelay;
            playSpellVisual.SpellVisualID = spellVisualId;
            playSpellVisual.TravelSpeed = travelSpeed;
            playSpellVisual.MissReason = (ushort)missReason;
            playSpellVisual.ReflectStatus = (ushort)reflectStatus;
            playSpellVisual.SpeedAsTime = speedAsTime;
            SendMessageToSet(playSpellVisual, true);
        }

        void SendCancelSpellVisualKit(uint id)
        {
            CancelSpellVisualKit cancelSpellVisualKit = new();
            cancelSpellVisualKit.Source = GetGUID();
            cancelSpellVisualKit.SpellVisualKitID = id;
            SendMessageToSet(cancelSpellVisualKit, true);
        }

        public void SendPlaySpellVisualKit(uint id, uint type, uint duration)
        {
            PlaySpellVisualKit playSpellVisualKit = new();
            playSpellVisualKit.Unit = GetGUID();
            playSpellVisualKit.KitRecID = id;
            playSpellVisualKit.KitType = type;
            playSpellVisualKit.Duration = duration;
            SendMessageToSet(playSpellVisualKit, true);
        }

        void CancelSpellMissiles(uint spellId, bool reverseMissile = false)
        {
            bool hasMissile = false;
            foreach (var pair in m_Events.GetEvents())
            {
                Spell spell = Spell.ExtractSpellFromEvent(pair.Value);
                if (spell != null)
                {
                    if (spell.GetSpellInfo().Id == spellId)
                    {
                        pair.Value.ScheduleAbort();
                        hasMissile = true;
                    }
                }
            }

            if (hasMissile)
            {
                MissileCancel packet = new();
                packet.OwnerGUID = GetGUID();
                packet.SpellID = spellId;
                packet.Reverse = reverseMissile;
                SendMessageToSet(packet, false);
            }
        }

        public void UnsummonAllTotems()
        {
            for (byte i = 0; i < SharedConst.MaxSummonSlot; ++i)
            {
                if (m_SummonSlot[i].IsEmpty())
                    continue;

                Creature OldTotem = GetMap().GetCreature(m_SummonSlot[i]);
                if (OldTotem != null)
                    if (OldTotem.IsSummon())
                        OldTotem.ToTempSummon().UnSummon();
            }
        }

        public bool IsOnVehicle(Unit vehicle) => m_vehicle != null && m_vehicle == vehicle.GetVehicleKit();

        public virtual UnitAI GetAI() => i_AI;
        public void SetAI(UnitAI newAI) => i_AI = newAI;

        public bool IsPossessing()
        {
            Unit u = GetCharm();
            if (u != null)
                return u.IsPossessed();
            else
                return false;
        }
        public Unit GetCharm()
        {
            ObjectGuid charm_guid = GetCharmGUID();
            if (!charm_guid.IsEmpty())
            {
                Unit pet = Global.ObjAccessor.GetUnit(this, charm_guid);
                if (pet != null)
                    return pet;

                Log.outError(LogFilter.Unit, "Unit.GetCharm: Charmed creature {0} not exist.", charm_guid);
                SetCharmGUID(ObjectGuid.Empty);
            }

            return null;
        }
        public bool IsCharmed() => !GetCharmerGUID().IsEmpty();
        public bool IsPossessed() => HasUnitState(UnitState.Possessed);

        public void OnPhaseChange() { }

        public uint GetModelForForm(ShapeShiftForm form, uint spellId)
        {
            // Hardcoded cases
            switch (spellId)
            {
                case 7090: // Bear Form
                    return 29414;
                case 35200: // Roc Form
                    return 4877;
                default:
                    break;
            }

            return 0;
        }

        public Totem ToTotem() => IsTotem() ? (this as Totem) : null;
        public TempSummon ToTempSummon() => IsSummon() ? (this as TempSummon) : null;
        public virtual void SetDeathState(DeathState s)
        {
            // Death state needs to be updated before RemoveAllAurasOnDeath() is called, to prevent entering combat
            m_deathState = s;

            if (s != DeathState.Alive && s != DeathState.JustRespawned)
            {
                CombatStop();
                GetThreatManager().ClearAllThreat();

                if (IsNonMeleeSpellCast(false))
                    InterruptNonMeleeSpells(false);

                ExitVehicle();                                      // Exit vehicle before calling RemoveAllControlled
                // vehicles use special type of charm that is not removed by the next function
                // triggering an assert
                UnsummonAllTotems();
                RemoveAllControlled();
                RemoveAllAurasOnDeath();
            }

            if (s == DeathState.JustDied)
            {
                // remove aurastates allowing special moves
                ClearAllReactives();
                m_Diminishing.Clear();
                if (IsInWorld)
                {
                    // Only clear MotionMaster for entities that exists in world
                    // Avoids crashes in the following conditions :
                    //  * Using 'call pet' on dead pets
                    //  * Using 'call stabled pet'
                    //  * Logging in with dead pets
                    GetMotionMaster().Clear(false);
                    GetMotionMaster().MoveIdle();
                }
                StopMoving();
                DisableSpline();
                // without this when removing IncreaseMaxHealth aura player may stuck with 1 hp
                // do not why since in IncreaseMaxHealth currenthealth is checked
                SetHealth(0);
                SetPower(GetPowerType(), 0);
                SetEmoteState(Emote.OneshotNone);

                // players in instance don't have ZoneScript, but they have InstanceScript
                ZoneScript zoneScript = GetZoneScript() != null ? GetZoneScript() : GetInstanceScript();
                if (zoneScript != null)
                    zoneScript.OnUnitDeath(this);
            }
            else if (s == DeathState.JustRespawned)
                RemoveUnitFlag(UnitFlags.Skinnable); // clear skinnable for creature and player (at Battleground)
        }

        public bool IsVisible() => m_serverSideVisibility.GetValue(ServerSideVisibilityType.GM) <= (uint)AccountTypes.Player;

        public void SetVisible(bool val)
        {
            if (!val)
                m_serverSideVisibility.SetValue(ServerSideVisibilityType.GM, AccountTypes.GameMaster);
            else
                m_serverSideVisibility.SetValue(ServerSideVisibilityType.GM, AccountTypes.Player);

            UpdateObjectVisibility();
        }

        public bool IsMagnet()
        {
            // Grounding Totem
            if (GetUpdateField<uint>(UnitFields.CreatedBySpell) == 8177) /// @todo: find a more generic solution
                return true;

            return false;
        }

        public void SetShapeshiftForm(ShapeShiftForm form) => SetUpdateField<byte>(UnitFields.Bytes4, (byte)form, 3);

        public int CalcSpellDuration(SpellInfo spellProto)
        {
            sbyte comboPoints = (sbyte)(m_playerMovingMe != null ? m_playerMovingMe.GetComboPoints() : 0);

            int minduration = spellProto.GetDuration();
            int maxduration = spellProto.GetMaxDuration();

            int duration;

            if (comboPoints != 0 && minduration != -1 && minduration != maxduration)
                duration = minduration + (maxduration - minduration) * comboPoints / 5;
            else
                duration = minduration;

            return duration;
        }

        public int ModSpellDuration(SpellInfo spellProto, Unit target, int duration, bool positive, uint effectMask)
        {
            // don't mod permanent auras duration
            if (duration < 0)
                return duration;

            // some auras are not affected by duration modifiers
            if (spellProto.HasAttribute(SpellAttr7.IgnoreDurationMods))
                return duration;

            // cut duration only of negative effects
            if (!positive)
            {
                uint mechanic = spellProto.GetSpellMechanicMaskByEffectMask(effectMask);

                int durationMod;
                int durationMod_always = 0;
                int durationMod_not_stack = 0;

                for (byte i = 1; i <= (int)Mechanics.Enraged; ++i)
                {
                    if (!Convert.ToBoolean(mechanic & 1 << i))
                        continue;
                    // Find total mod value (negative bonus)
                    int new_durationMod_always = target.GetTotalAuraModifierByMiscValue(AuraType.MechanicDurationMod, i);
                    // Find max mod (negative bonus)
                    int new_durationMod_not_stack = target.GetMaxNegativeAuraModifierByMiscValue(AuraType.MechanicDurationModNotStack, i);
                    // Check if mods applied before were weaker
                    if (new_durationMod_always < durationMod_always)
                        durationMod_always = new_durationMod_always;
                    if (new_durationMod_not_stack < durationMod_not_stack)
                        durationMod_not_stack = new_durationMod_not_stack;
                }

                // Select strongest negative mod
                if (durationMod_always > durationMod_not_stack)
                    durationMod = durationMod_not_stack;
                else
                    durationMod = durationMod_always;

                if (durationMod != 0)
                    MathFunctions.AddPct(ref duration, durationMod);

                // there are only negative mods currently
                durationMod_always = target.GetTotalAuraModifierByMiscValue(AuraType.ModAuraDurationByDispel, (int)spellProto.Dispel);
                durationMod_not_stack = target.GetMaxNegativeAuraModifierByMiscValue(AuraType.ModAuraDurationByDispelNotStack, (int)spellProto.Dispel);

                durationMod = 0;
                if (durationMod_always > durationMod_not_stack)
                    durationMod += durationMod_not_stack;
                else
                    durationMod += durationMod_always;

                if (durationMod != 0)
                    MathFunctions.AddPct(ref duration, durationMod);
            }
            else
            {
                // else positive mods here, there are no currently
                // when there will be, change GetTotalAuraModifierByMiscValue to GetTotalPositiveAuraModifierByMiscValue

                // Mixology - duration boost
                if (target.IsTypeId(TypeId.Player))
                {
                    if (spellProto.SpellFamilyName == SpellFamilyNames.Potion && (
                       Global.SpellMgr.IsSpellMemberOfSpellGroup(spellProto.Id, SpellGroup.ElixirBattle) ||
                       Global.SpellMgr.IsSpellMemberOfSpellGroup(spellProto.Id, SpellGroup.ElixirGuardian)))
                    {
                        SpellEffectInfo effect = spellProto.GetEffect(0);
                        if (target.HasAura(53042) && effect != null && target.HasSpell(effect.TriggerSpell))
                            duration *= 2;
                    }
                }
            }

            return Math.Max(duration, 0);
        }

        // creates aura application instance and registers it in lists
        // aura application effects are handled separately to prevent aura list corruption
        public AuraApplication _CreateAuraApplication(Aura aura, uint effMask)
        {
            // can't apply aura on unit which is going to be deleted - to not create a memory leak
            Cypher.Assert(!m_cleanupDone);
            // aura musn't be removed
            Cypher.Assert(!aura.IsRemoved());

            // aura mustn't be already applied on target
            Cypher.Assert(!aura.IsAppliedOnTarget(GetGUID()), "Unit._CreateAuraApplication: aura musn't be applied on target");

            SpellInfo aurSpellInfo = aura.GetSpellInfo();
            uint aurId = aurSpellInfo.Id;

            // ghost spell check, allow apply any auras at player loading in ghost mode (will be cleanup after load)
            if (!IsAlive() && !aurSpellInfo.IsDeathPersistent() &&
                (!IsTypeId(TypeId.Player) || !ToPlayer().GetSession().PlayerLoading()))
                return null;

            Unit caster = aura.GetCaster();

            AuraApplication aurApp = new(this, caster, aura, effMask);
            m_appliedAuras.Add(aurId, aurApp);

            if (aurSpellInfo.HasAnyAuraInterruptFlag())
            {
                m_interruptableAuras.Add(aurApp);
                AddInterruptMask(aurSpellInfo.AuraInterruptFlags, aurSpellInfo.AuraInterruptFlags2);
            }

            AuraStateType aState = aura.GetSpellInfo().GetAuraState();
            if (aState != 0)
                m_auraStateAuras.Add(aState, aurApp);

            aura._ApplyForTarget(this, caster, aurApp);
            return aurApp;
        }

        bool HasInterruptFlag(SpellAuraInterruptFlags flags) => m_interruptMask.HasFlag(flags);
        bool HasInterruptFlag(SpellAuraInterruptFlags2 flags) => m_interruptMask2.HasFlag(flags);

        public void AddInterruptMask(SpellAuraInterruptFlags flags, SpellAuraInterruptFlags2 flags2)
        {
            m_interruptMask |= flags;
            m_interruptMask2 |= flags2;
        }

        void _UpdateAutoRepeatSpell()
        {
            SpellInfo autoRepeatSpellInfo = m_currentSpells[CurrentSpellTypes.AutoRepeat].m_spellInfo;

            // check "realtime" interrupts
            // don't cancel spells which are affected by a SPELL_AURA_CAST_WHILE_WALKING effect
            if (((IsTypeId(TypeId.Player) && ToPlayer().IsMoving()) || IsNonMeleeSpellCast(false, false, true, autoRepeatSpellInfo.Id == 75)) &&
                !HasAuraTypeWithAffectMask(AuraType.CastWhileWalking, autoRepeatSpellInfo))
            {
                // cancel wand shoot
                if (autoRepeatSpellInfo.Id != 75)
                    InterruptSpell(CurrentSpellTypes.AutoRepeat);
                m_AutoRepeatFirstCast = true;
                return;
            }

            // apply delay (Auto Shot (spellID 75) not affected)
            if (m_AutoRepeatFirstCast && GetAttackTimer(WeaponAttackType.RangedAttack) < 500 && autoRepeatSpellInfo.Id != 75)
                SetAttackTimer(WeaponAttackType.RangedAttack, 500);
            m_AutoRepeatFirstCast = false;

            // castroutine
            if (IsAttackReady(WeaponAttackType.RangedAttack))
            {
                // Check if able to cast
                SpellCastResult result = m_currentSpells[CurrentSpellTypes.AutoRepeat].CheckCast(true);
                if (result != SpellCastResult.SpellCastOk)
                {
                    if (autoRepeatSpellInfo.Id != 75)
                        InterruptSpell(CurrentSpellTypes.AutoRepeat);
                    else if (GetTypeId() == TypeId.Player)
                        Spell.SendCastResult(ToPlayer(), autoRepeatSpellInfo, m_currentSpells[CurrentSpellTypes.AutoRepeat].m_spellXSpellVisualId, m_currentSpells[CurrentSpellTypes.AutoRepeat].m_castId, result);

                    return;
                }

                // we want to shoot
                Spell spell = new(this, autoRepeatSpellInfo, TriggerCastFlags.FullMask);
                spell.Prepare(m_currentSpells[CurrentSpellTypes.AutoRepeat].m_targets);

                // all went good, reset attack
                ResetAttackTimer(WeaponAttackType.RangedAttack);
            }
        }

        public void UpdateDisplayPower()
        {
            PowerType displayPower = PowerType.Mana;
            switch (GetShapeshiftForm())
            {
                case ShapeShiftForm.Ghoul:
                case ShapeShiftForm.CatForm:
                    displayPower = PowerType.Energy;
                    break;
                case ShapeShiftForm.BearForm:
                    displayPower = PowerType.Rage;
                    break;
                case ShapeShiftForm.TravelForm:
                case ShapeShiftForm.GhostWolf:
                    displayPower = PowerType.Mana;
                    break;
                default:
                    {
                        var powerTypeAuras = GetAuraEffectsByType(AuraType.ModPowerDisplay);
                        if (!powerTypeAuras.Empty())
                        {
                            AuraEffect powerTypeAura = powerTypeAuras.First();
                            displayPower = (PowerType)powerTypeAura.GetMiscValue();
                        }
                        else if (GetTypeId() == TypeId.Player)
                        {
                            ChrClassesRecord cEntry = CliDB.ChrClassesStorage.LookupByKey(GetClass());
                            if (cEntry != null && cEntry.DisplayPower < PowerType.Max)
                                displayPower = cEntry.DisplayPower;
                        }
                        else if (GetTypeId() == TypeId.Unit)
                        {
                            Vehicle vehicle = GetVehicleKit();
                            if (vehicle)
                            {
                                PowerDisplayRecord powerDisplay = CliDB.PowerDisplayStorage.LookupByKey(vehicle.GetVehicleInfo().PowerDisplayID[0]);
                                if (powerDisplay != null)
                                    displayPower = (PowerType)powerDisplay.ActualType;
                                else if (GetClass() == Class.Rogue)
                                    displayPower = PowerType.Energy;
                            }
                            else
                            {
                                Pet pet = ToPet();
                                if (pet)
                                {
                                    if (pet.GetPetType() == PetType.Hunter) // Hunter pets have focus
                                        displayPower = PowerType.Focus;
                                    else if (pet.IsPetGhoul() || pet.IsPetAbomination()) // DK pets have energy
                                        displayPower = PowerType.Energy;
                                }
                            }
                        }
                        break;
                    }
            }

            SetPowerType(displayPower);
        }

        public void SetSheath(SheathState sheathed)
        {
            SetUpdateField<byte>(UnitFields.Bytes4, (byte)sheathed);
            if (sheathed == SheathState.Unarmed)
                RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.Sheathing);
        }

        public FactionTemplateRecord GetFactionTemplateEntry()
        {
            FactionTemplateRecord entry = CliDB.FactionTemplateStorage.LookupByKey(GetFaction());
            if (entry == null)
            {
                Player player = ToPlayer();
                if (player != null)
                    Log.outError(LogFilter.Unit, "Player {0} has invalid faction (faction template id) #{1}", player.GetName(), GetFaction());
                else
                {
                    Creature creature = ToCreature();
                    if (creature != null)
                        Log.outError(LogFilter.Unit, "Creature (template id: {0}) has invalid faction (faction template id) #{1}", creature.GetCreatureTemplate().Entry, GetFaction());
                    else
                        Log.outError(LogFilter.Unit, "Unit (name={0}, type={1}) has invalid faction (faction template id) #{2}", GetName(), GetTypeId(), GetFaction());
                }
            }
            return entry;
        }

        public bool IsInFeralForm()
        {
            ShapeShiftForm form = GetShapeshiftForm();
            return form == ShapeShiftForm.CatForm || form == ShapeShiftForm.BearForm || form == ShapeShiftForm.DireBearForm || form == ShapeShiftForm.GhostWolf;
        }
        public bool IsControlledByPlayer() => m_ControlledByPlayer;

        public bool IsCharmedOwnedByPlayerOrPlayer() => GetCharmerOrOwnerOrOwnGUID().IsPlayer();

        public void AddFollower(FollowerReference pRef) => m_FollowingRefManager.InsertFirst(pRef);
        public void RemoveFollower(FollowerReference pRef) { } //nothing to do yet

        public uint GetCreatureTypeMask()
        {
            uint creatureType = (uint)GetCreatureType();
            return (uint)(creatureType >= 1 ? (1 << (int)(creatureType - 1)) : 0);
        }

        public Pet ToPet() => IsPet() ? (this as Pet) : null;
        public MotionMaster GetMotionMaster() => i_motionMaster;

        public void PlayOneShotAnimKitId(ushort animKitId)
        {
            if (!CliDB.AnimKitStorage.ContainsKey(animKitId))
            {
                Log.outError(LogFilter.Unit, "Unit.PlayOneShotAnimKitId using invalid AnimKit ID: {0}", animKitId);
                return;
            }

            PlayOneShotAnimKit packet = new();
            packet.Unit = GetGUID();
            packet.AnimKitID = animKitId;
            SendMessageToSet(packet, true);
        }

        public void SetAIAnimKitId(ushort animKitId)
        {
            if (_aiAnimKitId == animKitId)
                return;

            if (animKitId != 0 && !CliDB.AnimKitStorage.ContainsKey(animKitId))
                return;

            _aiAnimKitId = animKitId;

            SetAIAnimKit data = new();
            data.Unit = GetGUID();
            data.AnimKitID = animKitId;
            SendMessageToSet(data, true);
        }

        public override ushort GetAIAnimKitId() => _aiAnimKitId;

        public void SetMovementAnimKitId(ushort animKitId)
        {
            if (_movementAnimKitId == animKitId)
                return;

            if (animKitId != 0 && !CliDB.AnimKitStorage.ContainsKey(animKitId))
                return;

            _movementAnimKitId = animKitId;

            SetMovementAnimKit data = new();
            data.Unit = GetGUID();
            data.AnimKitID = animKitId;
            SendMessageToSet(data, true);
        }

        public override ushort GetMovementAnimKitId() => _movementAnimKitId;

        public void SetMeleeAnimKitId(ushort animKitId)
        {
            if (_meleeAnimKitId == animKitId)
                return;

            if (animKitId != 0 && !CliDB.AnimKitStorage.ContainsKey(animKitId))
                return;

            _meleeAnimKitId = animKitId;

            SetMeleeAnimKit data = new();
            data.Unit = GetGUID();
            data.AnimKitID = animKitId;
            SendMessageToSet(data, true);
        }

        public override ushort GetMeleeAnimKitId() => _meleeAnimKitId;

        public uint GetVirtualItemId(int slot)
        {
            if (slot >= SharedConst.MaxEquipmentItems)
                return 0;

            return GetUpdateField<uint>(UnitFields.VirtualItems + slot * 2);
        }

        public ushort GetVirtualItemAppearanceMod(int slot)
        {
            if (slot >= SharedConst.MaxEquipmentItems)
                return 0;

            return GetUpdateField<ushort>(UnitFields.VirtualItems + slot * 2 + 1, 0);
        }

        public void SetVirtualItem(int slot, uint itemId, uint appearanceModId = 0, ushort itemVisual = 0)
        {
            if (slot >= SharedConst.MaxEquipmentItems)
                return;

            SetUpdateField<uint>(UnitFields.VirtualItems + slot * 2, itemId);
            SetUpdateField<ushort>(UnitFields.VirtualItems + slot * 2 + 1, (ushort)appearanceModId, 0);
            SetUpdateField<ushort>(UnitFields.VirtualItems + slot * 2 + 1, itemVisual, 1);
        }

        //Unit
        public void SetLevel(uint lvl)
        {
            SetUpdateField<uint>(UnitFields.Level, lvl);

            Player player = ToPlayer();
            if (player != null)
            {
                if (player.GetGroup())
                    player.SetGroupUpdateFlag(GroupUpdateFlags.Level);

                Global.CharacterCacheStorage.UpdateCharacterLevel(ToPlayer().GetGUID(), (byte)lvl);
            }
        }
        public uint GetLevel() => GetUpdateField<uint>(UnitFields.Level);
        public override uint GetLevelForTarget(WorldObject target) { return GetLevel(); }

        public Race GetRace() => (Race)GetUpdateField<byte>(UnitFields.Bytes1, (byte)UnitBytes1Offset.Race);
        public void SetRace(Race race) => SetUpdateField<byte>(UnitFields.Bytes1, (byte)race, (byte)UnitBytes1Offset.Race);
        public Class GetClass() => (Class)GetUpdateField<byte>(UnitFields.Bytes1, (byte)UnitBytes1Offset.Class);
        public void SetClass(Class classId) => SetUpdateField<byte>(UnitFields.Bytes1, (byte)classId, (byte)UnitBytes1Offset.Class);
        public uint GetClassMask() => (uint)(1 << ((int)GetClass() - 1));
        public Gender GetGender() => (Gender)GetUpdateField<byte>(UnitFields.Bytes1, (byte)UnitBytes1Offset.Sex);
        public void SetGender(Gender sex) => SetUpdateField<byte>(UnitFields.Bytes1, (byte)sex, (byte)UnitBytes1Offset.Sex);

        public uint GetDisplayId() => GetUpdateField<uint>(UnitFields.DisplayID);
        public virtual void SetDisplayId(uint modelId, float displayScale = 1f)
        {
            SetUpdateField<uint>(UnitFields.DisplayID, modelId);
            SetUpdateField<float>(UnitFields.DisplayScale, displayScale);

            // Set Gender by modelId
            CreatureModelInfo minfo = Global.ObjectMgr.GetCreatureModelInfo(modelId);
            if (minfo != null)
                SetGender((Gender)minfo.gender);
        }
        public void RestoreDisplayId(bool ignorePositiveAurasPreventingMounting = false)
        {
            AuraEffect handledAura = null;
            // try to receive model from transform auras
            var transforms = GetAuraEffectsByType(AuraType.Transform);
            if (!transforms.Empty())
            {
                // iterate over already applied transform auras - from newest to oldest
                foreach (var eff in transforms)
                {
                    AuraApplication aurApp = eff.GetBase().GetApplicationOfTarget(GetGUID());
                    if (aurApp != null)
                    {
                        if (handledAura == null)
                        {
                            if (!ignorePositiveAurasPreventingMounting)
                                handledAura = eff;
                            else
                            {
                                CreatureTemplate ci = Global.ObjectMgr.GetCreatureTemplate((uint)eff.GetMiscValue());
                                if (ci != null)
                                    if (!IsDisallowedMountForm(eff.GetId(), ShapeShiftForm.None, ObjectManager.ChooseDisplayId(ci).CreatureDisplayID))
                                        handledAura = eff;
                            }
                        }

                        // prefer negative auras
                        if (!aurApp.IsPositive())
                        {
                            handledAura = eff;
                            break;
                        }
                    }
                }
            }

            var shapeshiftAura = GetAuraEffectsByType(AuraType.ModShapeshift);

            // transform aura was found
            if (handledAura != null)
            {
                handledAura.HandleEffect(this, AuraEffectHandleModes.SendForClient, true);
                return;
            }
            // we've found shapeshift
            else if (!shapeshiftAura.Empty()) // we've found shapeshift
            {
                // only one such aura possible at a time
                uint modelId = GetModelForForm(GetShapeshiftForm(), shapeshiftAura[0].GetId());
                if (modelId != 0)
                {
                    if (!ignorePositiveAurasPreventingMounting || !IsDisallowedMountForm(0, GetShapeshiftForm(), modelId))
                        SetDisplayId(modelId);
                    else
                        SetDisplayId(GetNativeDisplayId());
                    return;
                }
            }
            // no auras found - set modelid to default
            SetDisplayId(GetNativeDisplayId());
        }
        public uint GetNativeDisplayId() => GetUpdateField<uint>(UnitFields.NativeDisplayID);
        public void SetNativeDisplayId(uint displayId, float displayScale = 1f)
        {
            SetUpdateField<uint>(UnitFields.NativeDisplayID, displayId);
            SetUpdateField<float>(UnitFields.NativeXDisplayScale, displayScale);
        }
        public float GetNativeDisplayScale() => GetUpdateField<float>(UnitFields.NativeXDisplayScale);

        public bool IsMounted() => HasUnitFlag(UnitFlags.Mount);
        public uint GetMountDisplayId() => GetUpdateField<uint>(UnitFields.MountDisplayID);
        public void SetMountDisplayId(uint mountDisplayId) => SetUpdateField<uint>(UnitFields.MountDisplayID, mountDisplayId);

        public virtual Unit GetOwner()
        {
            ObjectGuid ownerid = GetOwnerGUID();
            if (!ownerid.IsEmpty())
                return Global.ObjAccessor.GetUnit(this, ownerid);

            return null;
        }
        public virtual float GetFollowAngle() => MathFunctions.PiOver2;

        public ObjectGuid GetOwnerGUID() => GetUpdateField<ObjectGuid>(UnitFields.SummonedBy);
        public void SetOwnerGUID(ObjectGuid owner)
        {
            if (GetOwnerGUID() == owner)
                return;

            SetUpdateField<ObjectGuid>(UnitFields.SummonedBy, owner);
            if (owner.IsEmpty())
                return;

            // Update owner dependent fields
            Player player = Global.ObjAccessor.GetPlayer(this, owner);
            if (player == null || !player.HaveAtClient(this)) // if player cannot see this unit yet, he will receive needed data with create object
                return;

            UpdateData udata = new(GetMapId());
            BuildValuesUpdateBlockForPlayer(udata, player);
            udata.BuildPacket(out UpdateObject packet);
            player.SendPacket(packet);
        }
        public ObjectGuid GetCreatorGUID() => GetUpdateField<ObjectGuid>(UnitFields.CreatedBy);
        public void SetCreatorGUID(ObjectGuid creator) => SetUpdateField<ObjectGuid>(UnitFields.CreatedBy, creator);
        public ObjectGuid GetMinionGUID() => GetUpdateField<ObjectGuid>(UnitFields.Summon);
        public void SetMinionGUID(ObjectGuid guid) => SetUpdateField<ObjectGuid>(UnitFields.Summon, guid);
        public ObjectGuid GetCharmerGUID() => GetUpdateField<ObjectGuid>(UnitFields.CharmedBy);
        public void SetCharmerGUID(ObjectGuid owner) => SetUpdateField<ObjectGuid>(UnitFields.CharmedBy, owner);
        public ObjectGuid GetCharmGUID() => GetUpdateField<ObjectGuid>(UnitFields.Charm);
        public void SetCharmGUID(ObjectGuid charm) => SetUpdateField<ObjectGuid>(UnitFields.Charm, charm);
        public ObjectGuid GetPetGUID() => m_SummonSlot[0];
        public void SetPetGUID(ObjectGuid guid) => m_SummonSlot[0] = guid;
        public ObjectGuid GetCritterGUID() => GetUpdateField<ObjectGuid>(UnitFields.Critter);
        public void SetCritterGUID(ObjectGuid guid) => SetUpdateField<ObjectGuid>(UnitFields.Critter, guid);
        public ObjectGuid GetBattlePetCompanionGUID() => GetUpdateField<ObjectGuid>(UnitFields.BattlePetCompanionGUID);
        public void SetBattlePetCompanionGUID(ObjectGuid guid) => SetUpdateField<ObjectGuid>(UnitFields.BattlePetCompanionGUID, guid);
        public ObjectGuid GetCharmerOrOwnerGUID() => !GetCharmerGUID().IsEmpty() ? GetCharmerGUID() : GetOwnerGUID();
        public ObjectGuid GetCharmerOrOwnerOrOwnGUID()
        {
            ObjectGuid guid = GetCharmerOrOwnerGUID();
            if (!guid.IsEmpty())
                return guid;

            return GetGUID();
        }
        public Unit GetCharmer()
        {
            ObjectGuid charmerid = GetCharmerGUID();
            if (!charmerid.IsEmpty())
                return Global.ObjAccessor.GetUnit(this, charmerid);
            return null;
        }
        public Unit GetCharmerOrOwnerOrSelf()
        {
            Unit u = GetCharmerOrOwner();
            if (u != null)
                return u;

            return this;
        }
        public Player GetCharmerOrOwnerPlayerOrPlayerItself()
        {
            ObjectGuid guid = GetCharmerOrOwnerGUID();
            if (guid.IsPlayer())
                return Global.ObjAccessor.FindPlayer(guid);

            return IsTypeId(TypeId.Player) ? ToPlayer() : null;
        }
        public Unit GetCharmerOrOwner()
        => !GetCharmerGUID().IsEmpty() ? GetCharmer() : GetOwner();

        public bool HasUnitFlag(UnitFlags flags) => ((UnitFlags)GetUpdateField<uint>(UnitFields.Flags)).HasAnyFlag(flags);
        public void AddUnitFlag(UnitFlags flags) => AddFlag(UnitFields.Flags, flags);
        public void RemoveUnitFlag(UnitFlags flags) => RemoveFlag(UnitFields.Flags, flags);
        public void SetUnitFlags(UnitFlags flags) => SetUpdateField<uint>(UnitFields.Flags, (uint)flags);
        public bool HasUnitFlag2(UnitFlags2 flags) => ((UnitFlags2)GetUpdateField<uint>(UnitFields.Flags2)).HasAnyFlag(flags);
        public void AddUnitFlag2(UnitFlags2 flags) => AddFlag(UnitFields.Flags2, flags);
        public void RemoveUnitFlag2(UnitFlags2 flags) => RemoveFlag(UnitFields.Flags2, flags);
        public void SetUnitFlags2(UnitFlags2 flags) => SetUpdateField<uint>(UnitFields.Flags2, (uint)flags);
        public bool HasUnitFlag3(UnitFlags3 flags) => ((UnitFlags3)GetUpdateField<uint>(UnitFields.Flags3)).HasAnyFlag(flags);
        public void AddUnitFlag3(UnitFlags3 flags) => AddFlag(UnitFields.Flags3, flags);
        public void RemoveUnitFlag3(UnitFlags3 flags) => RemoveFlag(UnitFields.Flags3, flags);
        public void SetUnitFlags3(UnitFlags3 flags) => SetUpdateField<uint>(UnitFields.Flags3, (uint)flags);

        public void SetCreatedBySpell(uint spellId) => SetUpdateField<uint>(UnitFields.CreatedBySpell, spellId);

        public Emote GetEmoteState() => (Emote)GetUpdateField<int>(UnitFields.EmoteState);
        public void SetEmoteState(Emote emote) => SetUpdateField<int>(UnitFields.EmoteState, (int)emote);

        public SheathState GetSheath() => (SheathState)GetUpdateField<byte>(UnitFields.Bytes4);

        public uint GetCombatTimer() => combatTimer;
        public UnitPVPStateFlags GetPvpFlags() => (UnitPVPStateFlags)GetUpdateField<byte>(UnitFields.Bytes4, 1);
        public bool HasPvpFlag(UnitPVPStateFlags flags) => GetPvpFlags().HasAnyFlag(flags);
        public void AddPvpFlag(UnitPVPStateFlags flags) => AddByteFlag(UnitFields.Bytes4, 1, flags);
        public void RemovePvpFlag(UnitPVPStateFlags flags) => RemoveByteFlag(UnitFields.Bytes4, 1, flags);
        public void SetPvpFlags(UnitPVPStateFlags flags) => SetUpdateField<byte>(UnitFields.Bytes4, (byte)flags, 1);
        public bool IsInSanctuary() => HasPvpFlag(UnitPVPStateFlags.Sanctuary);
        public bool IsPvP() => HasPvpFlag(UnitPVPStateFlags.PvP);
        public bool IsFFAPvP() => HasPvpFlag(UnitPVPStateFlags.FFAPvp);

        public UnitPetFlags GetPetFlags() => (UnitPetFlags)GetUpdateField<byte>(UnitFields.Bytes4, 2);
        public bool HasPetFlag(UnitPetFlags flags) => GetPetFlags().HasAnyFlag(flags);
        public void AddPetFlag(UnitPetFlags flags) => AddByteFlag(UnitFields.Bytes4, 2, flags);
        public void RemovePetFlag(UnitPetFlags flags) => RemoveByteFlag(UnitFields.Bytes4, 2, flags);
        public void SetPetFlags(UnitPetFlags flags) => SetUpdateField<byte>(UnitFields.Bytes4, (byte)flags, 2);

        public void SetPetNumberForClient(uint petNumber) => SetUpdateField<uint>(UnitFields.PetNumber, petNumber);
        public void SetPetNameTimestamp(uint timestamp) => SetUpdateField<uint>(UnitFields.PetNameTimestamp, timestamp);

        public ShapeShiftForm GetShapeshiftForm() => (ShapeShiftForm)GetUpdateField<byte>(UnitFields.Bytes4, 3);
        public CreatureType GetCreatureType()
        {
            if (IsTypeId(TypeId.Player))
            {
                ShapeShiftForm form = GetShapeshiftForm();
                var ssEntry = CliDB.SpellShapeshiftFormStorage.LookupByKey((uint)form);
                if (ssEntry != null && ssEntry.CreatureType > 0)
                    return (CreatureType)ssEntry.CreatureType;
                else
                    return CreatureType.Humanoid;
            }
            else
                return ToCreature().GetCreatureTemplate().CreatureType;
        }
        public Player GetAffectingPlayer()
        {
            if (GetCharmerOrOwnerGUID().IsEmpty())
                return IsTypeId(TypeId.Player) ? ToPlayer() : null;

            Unit owner = GetCharmerOrOwner();
            if (owner != null)
                return owner.GetCharmerOrOwnerPlayerOrPlayerItself();
            return null;
        }

        public void DeMorph() => SetDisplayId(GetNativeDisplayId());

        public bool HasUnitTypeMask(UnitTypeMask mask) => Convert.ToBoolean(mask & UnitTypeMask);
        public void AddUnitTypeMask(UnitTypeMask mask) => UnitTypeMask |= mask;

        public bool IsAlive() => m_deathState == DeathState.Alive;
        public bool IsDying() => m_deathState == DeathState.JustDied;
        public bool IsDead() => (m_deathState == DeathState.Dead || m_deathState == DeathState.Corpse);
        public bool IsSummon() => UnitTypeMask.HasAnyFlag(UnitTypeMask.Summon);
        public bool IsGuardian() => UnitTypeMask.HasAnyFlag(UnitTypeMask.Guardian);
        public bool IsPet() => UnitTypeMask.HasAnyFlag(UnitTypeMask.Pet);
        public bool IsHunterPet() => UnitTypeMask.HasAnyFlag(UnitTypeMask.HunterPet);
        public bool IsTotem() => UnitTypeMask.HasAnyFlag(UnitTypeMask.Totem);
        public bool IsVehicle() => UnitTypeMask.HasAnyFlag(UnitTypeMask.Vehicle);

        public void AddUnitState(UnitState f) => m_state |= f;
        public bool HasUnitState(UnitState f) => m_state.HasFlag(f);
        public void ClearUnitState(UnitState f) => m_state &= ~f;

        public override bool IsAlwaysVisibleFor(WorldObject seer)
        {
            if (base.IsAlwaysVisibleFor(seer))
                return true;

            // Always seen by owner
            ObjectGuid guid = GetCharmerOrOwnerGUID();
            if (!guid.IsEmpty())
                if (seer.GetGUID() == guid)
                    return true;

            Player seerPlayer = seer.ToPlayer();
            if (seerPlayer != null)
            {
                Unit owner = GetOwner();
                if (owner != null)
                {
                    Player ownerPlayer = owner.ToPlayer();
                    if (ownerPlayer)
                        if (ownerPlayer.IsGroupVisibleFor(seerPlayer))
                            return true;
                }
            }

            return false;
        }

        //Faction
        public bool IsNeutralToAll()
        {
            var my_faction = GetFactionTemplateEntry();
            if (my_faction == null || my_faction.Faction == 0)
                return true;

            var raw_faction = CliDB.FactionStorage.LookupByKey(my_faction.Faction);
            if (raw_faction != null && raw_faction.ReputationIndex >= 0)
                return false;

            return my_faction.IsNeutralToAll();
        }
        public bool IsHostileTo(Unit unit) => GetReactionTo(unit) <= ReputationRank.Hostile;
        public bool IsFriendlyTo(Unit unit) => GetReactionTo(unit) >= ReputationRank.Friendly;
        public ReputationRank GetReactionTo(Unit target)
        {
            // always friendly to self
            if (this == target)
                return ReputationRank.Friendly;

            // always friendly to charmer or owner
            if (GetCharmerOrOwnerOrSelf() == target.GetCharmerOrOwnerOrSelf())
                return ReputationRank.Friendly;

            if (HasUnitFlag(UnitFlags.PvpAttackable))
            {
                if (target.HasUnitFlag(UnitFlags.PvpAttackable))
                {
                    Player selfPlayerOwner = GetAffectingPlayer();
                    Player targetPlayerOwner = target.GetAffectingPlayer();

                    if (selfPlayerOwner != null && targetPlayerOwner != null)
                    {
                        // always friendly to other unit controlled by player, or to the player himself
                        if (selfPlayerOwner == targetPlayerOwner)
                            return ReputationRank.Friendly;

                        // duel - always hostile to opponent
                        if (selfPlayerOwner.duel != null && selfPlayerOwner.duel.opponent == targetPlayerOwner && selfPlayerOwner.duel.startTime != 0)
                            return ReputationRank.Hostile;

                        // same group - checks dependant only on our faction - skip FFA_PVP for example
                        if (selfPlayerOwner.IsInRaidWith(targetPlayerOwner))
                            return ReputationRank.Friendly; // return true to allow config option AllowTwoSide.Interaction.Group to work
                    }

                    // check FFA_PVP
                    if (IsFFAPvP() && target.IsFFAPvP())
                        return ReputationRank.Hostile;

                    if (selfPlayerOwner != null)
                    {
                        var targetFactionTemplateEntry = target.GetFactionTemplateEntry();
                        if (targetFactionTemplateEntry != null)
                        {
                            if (!selfPlayerOwner.HasUnitFlag2(UnitFlags2.IgnoreReputation))
                            {
                                var targetFactionEntry = CliDB.FactionStorage.LookupByKey(targetFactionTemplateEntry.Faction);
                                if (targetFactionEntry != null)
                                {
                                    if (targetFactionEntry.CanHaveReputation())
                                    {
                                        // check contested flags
                                        if (Convert.ToBoolean(targetFactionTemplateEntry.Flags & (uint)FactionTemplateFlags.ContestedGuard)
                                            && selfPlayerOwner.HasPlayerFlag(PlayerFlags.ContestedPVP))
                                            return ReputationRank.Hostile;

                                        // if faction has reputation, hostile state depends only from AtWar state
                                        if (selfPlayerOwner.GetReputationMgr().IsAtWar(targetFactionEntry))
                                            return ReputationRank.Hostile;
                                        return ReputationRank.Friendly;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // do checks dependant only on our faction
            return GetFactionReactionTo(GetFactionTemplateEntry(), target);
        }
        ReputationRank GetFactionReactionTo(FactionTemplateRecord factionTemplateEntry, Unit target)
        {
            // always neutral when no template entry found
            if (factionTemplateEntry == null)
                return ReputationRank.Neutral;

            var targetFactionTemplateEntry = target.GetFactionTemplateEntry();
            if (targetFactionTemplateEntry == null)
                return ReputationRank.Neutral;

            Player targetPlayerOwner = target.GetAffectingPlayer();
            if (targetPlayerOwner != null)
            {
                // check contested flags
                if (Convert.ToBoolean(factionTemplateEntry.Flags & (uint)FactionTemplateFlags.ContestedGuard)
                    && targetPlayerOwner.HasPlayerFlag(PlayerFlags.ContestedPVP))
                    return ReputationRank.Hostile;
                ReputationRank repRank = targetPlayerOwner.GetReputationMgr().GetForcedRankIfAny(factionTemplateEntry);
                if (repRank != ReputationRank.None)
                    return repRank;
                if (!target.HasUnitFlag2(UnitFlags2.IgnoreReputation))
                {
                    var factionEntry = CliDB.FactionStorage.LookupByKey(factionTemplateEntry.Faction);
                    if (factionEntry != null)
                    {
                        if (factionEntry.CanHaveReputation())
                        {
                            // CvP case - check reputation, don't allow state higher than neutral when at war
                            repRank = targetPlayerOwner.GetReputationMgr().GetRank(factionEntry);
                            if (targetPlayerOwner.GetReputationMgr().IsAtWar(factionEntry))
                                repRank = (ReputationRank)Math.Min((int)ReputationRank.Neutral, (int)repRank);
                            return repRank;
                        }
                    }
                }
            }

            // common faction based check
            if (factionTemplateEntry.IsHostileTo(targetFactionTemplateEntry))
                return ReputationRank.Hostile;
            if (factionTemplateEntry.IsFriendlyTo(targetFactionTemplateEntry))
                return ReputationRank.Friendly;
            if (targetFactionTemplateEntry.IsFriendlyTo(factionTemplateEntry))
                return ReputationRank.Friendly;
            if (Convert.ToBoolean(factionTemplateEntry.Flags & (uint)FactionTemplateFlags.HostileByDefault))
                return ReputationRank.Hostile;
            // neutral by default
            return ReputationRank.Neutral;
        }

        public uint GetFaction() => GetUpdateField<uint>(UnitFields.FactionTemplate);
        public void SetFaction(uint faction) => SetUpdateField<uint>(UnitFields.FactionTemplate, faction);

        public void RestoreFaction()
        {
            if (IsTypeId(TypeId.Player))
                ToPlayer().SetFactionForRace(GetRace());
            else
            {
                if (HasUnitTypeMask(UnitTypeMask.Minion))
                {
                    Unit owner = GetOwner();
                    if (owner)
                    {
                        SetFaction(owner.GetFaction());
                        return;
                    }
                }
                CreatureTemplate cinfo = ToCreature().GetCreatureTemplate();
                if (cinfo != null)  // normal creature
                    SetFaction(cinfo.Faction);
            }
        }

        public bool IsInPartyWith(Unit unit)
        {
            if (this == unit)
                return true;

            Unit u1 = GetCharmerOrOwnerOrSelf();
            Unit u2 = unit.GetCharmerOrOwnerOrSelf();
            if (u1 == u2)
                return true;

            if (u1.IsTypeId(TypeId.Player) && u2.IsTypeId(TypeId.Player))
                return u1.ToPlayer().IsInSameGroupWith(u2.ToPlayer());
            else if ((u2.IsTypeId(TypeId.Player) && u1.IsTypeId(TypeId.Unit) && u1.ToCreature().GetCreatureTemplate().TypeFlags.HasAnyFlag(CreatureTypeFlags.TreatAsRaidUnit)) ||
                (u1.IsTypeId(TypeId.Player) && u2.IsTypeId(TypeId.Unit) && u2.ToCreature().GetCreatureTemplate().TypeFlags.HasAnyFlag(CreatureTypeFlags.TreatAsRaidUnit)))
                return true;

            return u1.GetTypeId() == TypeId.Unit && u2.GetTypeId() == TypeId.Unit && u1.GetFaction() == u2.GetFaction();
        }

        public bool IsInRaidWith(Unit unit)
        {
            if (this == unit)
                return true;

            Unit u1 = GetCharmerOrOwnerOrSelf();
            Unit u2 = unit.GetCharmerOrOwnerOrSelf();
            if (u1 == u2)
                return true;

            if (u1.IsTypeId(TypeId.Player) && u2.IsTypeId(TypeId.Player))
                return u1.ToPlayer().IsInSameRaidWith(u2.ToPlayer());
            else if ((u2.IsTypeId(TypeId.Player) && u1.IsTypeId(TypeId.Unit) && u1.ToCreature().GetCreatureTemplate().TypeFlags.HasAnyFlag(CreatureTypeFlags.TreatAsRaidUnit)) ||
                    (u1.IsTypeId(TypeId.Player) && u2.IsTypeId(TypeId.Unit) && u2.ToCreature().GetCreatureTemplate().TypeFlags.HasAnyFlag(CreatureTypeFlags.TreatAsRaidUnit)))
                return true;

            // else u1.GetTypeId() == u2.GetTypeId() == TYPEID_UNIT
            return u1.GetFaction() == u2.GetFaction();
        }

        public UnitStandStateType GetStandState() => (UnitStandStateType)GetUpdateField<byte>(UnitFields.Bytes2);
        public void AddVisFlags(UnitVisFlags flags) => AddByteFlag(UnitFields.Bytes2, 2, flags);
        public void RemoveVisFlags(UnitVisFlags flags) => RemoveByteFlag(UnitFields.Bytes2, 2, flags);
        public void SetVisFlags(UnitVisFlags flags) => SetUpdateField<byte>(UnitFields.Bytes2, (byte)flags, 2);

        public bool IsSitState()
        {
            UnitStandStateType s = GetStandState();
            return
                s == UnitStandStateType.SitChair || s == UnitStandStateType.SitLowChair ||
                s == UnitStandStateType.SitMediumChair || s == UnitStandStateType.SitHighChair ||
                s == UnitStandStateType.Sit;
        }

        public bool IsStandState()
        {
            UnitStandStateType s = GetStandState();
            return !IsSitState() && s != UnitStandStateType.Sleep && s != UnitStandStateType.Kneel;
        }

        public void SetStandState(UnitStandStateType state, uint animKitId = 0)
        {
            SetUpdateField<byte>(UnitFields.Bytes2, (byte)state);

            if (IsStandState())
                RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.Standing);

            if (IsTypeId(TypeId.Player))
            {
                StandStateUpdate packet = new(state, animKitId);
                ToPlayer().SendPacket(packet);
            }
        }

        public void SetAnimTier(UnitBytes1Flags animTier, bool notifyClient)
        {
            SetUpdateField<byte>(UnitFields.Bytes2, (byte)animTier, 3);

            if (notifyClient)
            {
                SetAnimTier setAnimTier = new();
                setAnimTier.Unit = GetGUID();
                setAnimTier.Tier = (int)animTier;
                SendMessageToSet(setAnimTier, true);
            }
        }

        public uint GetChannelSpellId() => GetUpdateField<uint>(UnitFields.ChannelData);
        public void SetChannelSpellId(uint channelSpellId) => SetUpdateField<uint>(UnitFields.ChannelData, channelSpellId);
        public uint GetChannelSpellXSpellVisualId() => GetUpdateField<uint>(UnitFields.ChannelData + 1);
        public void SetChannelSpellXSpellVisualId(uint spellXSpellVisualId) => SetUpdateField<uint>(UnitFields.ChannelData + 1, spellXSpellVisualId);
        public void AddChannelObject(ObjectGuid guid) => AddDynamicStructuredValue<ObjectGuid>(UnitDynamicFields.ChannelObjects, guid);
        public void SetChannelObject(byte slot, ObjectGuid guid) => SetDynamicStructuredValue<ObjectGuid>(UnitDynamicFields.ChannelObjects, slot, guid);
        public void ClearChannelObjects() => ClearDynamicValue(UnitDynamicFields.ChannelObjects);

        public void RemoveChannelObject(ObjectGuid guid)
        {
            // RemoveDynamicUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ChannelObjects), index);
        }

        public static bool IsDamageReducedByArmor(SpellSchoolMask schoolMask, SpellInfo spellInfo = null, sbyte effIndex = -1)
        {
            // only physical spells damage gets reduced by armor
            if ((schoolMask & SpellSchoolMask.Normal) == 0)
                return false;

            if (spellInfo != null)
            {
                // there are spells with no specific attribute but they have "ignores armor" in tooltip
                if (spellInfo.HasAttribute(SpellCustomAttributes.IgnoreArmor))
                    return false;

                if (effIndex != -1)
                {
                    // bleeding effects are not reduced by armor
                    SpellEffectInfo effect = spellInfo.GetEffect((uint)effIndex);
                    if (effect != null)
                    {
                        if (effect.ApplyAuraName == AuraType.PeriodicDamage || effect.Effect == SpellEffectName.SchoolDamage)
                            if (spellInfo.GetEffectMechanicMask((byte)effIndex).HasAnyFlag((1u << (int)Mechanics.Bleed)))
                                return false;
                    }
                }
            }

            return true;
        }

        public override void DestroyForPlayer(Player target)
        {
            Battleground bg = target.GetBattleground();
            if (bg != null)
            {
                if (bg.IsArena())
                {
                    DestroyArenaUnit destroyArenaUnit = new();
                    destroyArenaUnit.Guid = GetGUID();
                    target.SendPacket(destroyArenaUnit);
                }
            }

            base.DestroyForPlayer(target);
        }

        public bool CanDualWield() => m_canDualWield;

        public virtual void SetCanDualWield(bool value) => m_canDualWield = value;

        public DeathState GetDeathState() => m_deathState;

        public bool HaveOffhandWeapon()
        {
            if (IsTypeId(TypeId.Player))
                return ToPlayer().GetWeaponForAttack(WeaponAttackType.OffAttack, true) != null;
            else
                return m_canDualWield;
        }

        void StartReactiveTimer(ReactiveType reactive) => m_reactiveTimer[reactive] = 4000;

        public static void DealDamageMods(Unit attacker, Unit victim, ref uint damage)
        {
            if (victim == null || !victim.IsAlive() || victim.HasUnitState(UnitState.InFlight)
                || (victim.IsTypeId(TypeId.Unit) && victim.ToCreature().IsInEvadeMode()))
            {
                damage = 0;
            }
        }

        public static void DealDamageMods(Unit attacker, Unit victim, ref uint damage, ref uint absorb)
        {
            if (victim == null || !victim.IsAlive() || victim.HasUnitState(UnitState.InFlight)
                || (victim.IsTypeId(TypeId.Unit) && victim.ToCreature().IsEvadingAttacks()))
            {
                absorb += damage;
                damage = 0;
                return;
            }
        }

        public static uint DealDamage(Unit attacker, Unit victim, uint damage, CleanDamage cleanDamage = null, DamageEffectType damagetype = DamageEffectType.Direct, SpellSchoolMask damageSchoolMask = SpellSchoolMask.Normal, SpellInfo spellProto = null, bool durabilityLoss = true)
        {
            if (victim.IsAIEnabled)
                victim.GetAI().DamageTaken(attacker, ref damage);

            if (attacker != null && attacker.IsAIEnabled)
                attacker.GetAI().DamageDealt(victim, ref damage, damagetype);

            // Hook for OnDamage Event
            Global.ScriptMgr.OnDamage(attacker, victim, ref damage);

            if (victim.IsTypeId(TypeId.Player) && attacker != victim)
            {
                // Signal to pets that their owner was attacked - except when DOT.
                if (damagetype != DamageEffectType.DOT)
                {
                    foreach (Unit controlled in victim.m_Controlled)
                    {
                        Creature cControlled = controlled.ToCreature();
                        if (cControlled != null)
                            if (cControlled.IsAIEnabled)
                                cControlled.GetAI().OwnerAttackedBy(attacker);
                    }
                }

                if (victim.ToPlayer().GetCommandStatus(PlayerCommandStates.God))
                    return 0;
            }

            if (damagetype != DamageEffectType.NoDamage)
            {
                // interrupting auras with SpellAuraInterruptFlags.Damage before checking !damage (absorbed damage breaks that type of auras)
                if (spellProto != null)
                {
                    if (!spellProto.HasAttribute(SpellAttr4.DamageDoesntBreakAuras))
                        victim.RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.Damage, spellProto.Id);
                }
                else
                    victim.RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.Damage, 0);

                if (damage == 0 && damagetype != DamageEffectType.DOT && cleanDamage != null && cleanDamage.absorbed_damage != 0)
                {
                    if (victim != attacker && victim.IsPlayer())
                    {
                        Spell spell = victim.GetCurrentSpell(CurrentSpellTypes.Generic);
                        if (spell != null)
                            if (spell.GetState() == SpellState.Preparing && spell.m_spellInfo.InterruptFlags.HasAnyFlag(SpellInterruptFlags.DamageAbsorb))
                                victim.InterruptNonMeleeSpells(false);
                    }
                }

                // We're going to call functions which can modify content of the list during iteration over it's elements
                // Let's copy the list so we can prevent iterator invalidation
                var vCopyDamageCopy = victim.GetAuraEffectsByType(AuraType.ShareDamagePct);
                // copy damage to casters of this aura
                foreach (var aura in vCopyDamageCopy)
                {
                    // Check if aura was removed during iteration - we don't need to work on such auras
                    if (!aura.GetBase().IsAppliedOnTarget(victim.GetGUID()))
                        continue;

                    // check damage school mask
                    if ((aura.GetMiscValue() & (int)damageSchoolMask) == 0)
                        continue;

                    Unit shareDamageTarget = aura.GetCaster();
                    if (shareDamageTarget == null)
                        continue;

                    SpellInfo spell = aura.GetSpellInfo();

                    uint share = MathFunctions.CalculatePct(damage, aura.GetAmount());

                    // @todo check packets if damage is done by victim, or by attacker of victim
                    DealDamageMods(attacker, shareDamageTarget, ref share);
                    DealDamage(attacker, shareDamageTarget, share, null, DamageEffectType.NoDamage, spell.GetSchoolMask(), spell, false);
                }
            }

            // Rage from Damage made (only from direct weapon damage)
            if (attacker != null && cleanDamage != null && (cleanDamage.attackType == WeaponAttackType.BaseAttack || cleanDamage.attackType == WeaponAttackType.OffAttack) && damagetype == DamageEffectType.Direct && attacker != victim && attacker.GetPowerType() == PowerType.Rage)
            {
                uint rage = (uint)(attacker.GetBaseAttackTime(cleanDamage.attackType) / 1000.0f * 1.75f);
                if (cleanDamage.attackType == WeaponAttackType.OffAttack)
                    rage /= 2;

                attacker.RewardRage(rage);
            }

            if (damage == 0)
                return 0;

            uint health = (uint)victim.GetHealth();

            // duel ends when player has 1 or less hp
            bool duel_hasEnded = false;
            bool duel_wasMounted = false;
            if (victim.IsPlayer() && victim.ToPlayer().duel != null && damage >= (health - 1))
            {
                if (!attacker)
                    return 0;

                // prevent kill only if killed in duel and killed by opponent or opponent controlled creature
                if (victim.ToPlayer().duel.opponent == attacker || victim.ToPlayer().duel.opponent.GetGUID() == attacker.GetOwnerGUID())
                    damage = health - 1;

                duel_hasEnded = true;
            }
            else if (victim.IsVehicle() && damage >= (health - 1) && victim.GetCharmer() != null && victim.GetCharmer().IsTypeId(TypeId.Player))
            {
                Player victimRider = victim.GetCharmer().ToPlayer();
                if (victimRider != null && victimRider.duel != null && victimRider.duel.isMounted)
                {
                    if (!attacker)
                        return 0;

                    // prevent kill only if killed in duel and killed by opponent or opponent controlled creature
                    if (victimRider.duel.opponent == attacker || victimRider.duel.opponent.GetGUID() == attacker.GetCharmerGUID())
                        damage = health - 1;

                    duel_wasMounted = true;
                    duel_hasEnded = true;
                }
            }

            if (attacker != null && attacker != victim)
            {
                Player killer = attacker.ToPlayer();
                if (killer != null)
                {
                    // in bg, count dmg if victim is also a player
                    if (victim.IsPlayer())
                    {
                        Battleground bg = killer.GetBattleground();
                        if (bg != null)
                            bg.UpdatePlayerScore(killer, ScoreType.DamageDone, damage);
                    }
                }
            }

            if (victim.GetTypeId() != TypeId.Player && (!victim.IsControlledByPlayer() || victim.IsVehicle()))
            {
                if (!victim.ToCreature().HasLootRecipient())
                    victim.ToCreature().SetLootRecipient(attacker);

                if (attacker == null || attacker.IsControlledByPlayer())
                    victim.ToCreature().LowerPlayerDamageReq(health < damage ? health : damage);
            }

            bool killed = false;
            bool skipSettingDeathState = false;

            if (health <= damage)
            {
                killed = true;

                if (damagetype != DamageEffectType.NoDamage && damagetype != DamageEffectType.Self && victim.HasAuraType(AuraType.SchoolAbsorbOverkill))
                {
                    var vAbsorbOverkill = victim.GetAuraEffectsByType(AuraType.SchoolAbsorbOverkill);
                    DamageInfo damageInfo = new(attacker, victim, damage, spellProto, damageSchoolMask, damagetype, cleanDamage != null ? cleanDamage.attackType : WeaponAttackType.BaseAttack);

                    foreach (var absorbAurEff in vAbsorbOverkill)
                    {
                        Aura baseAura = absorbAurEff.GetBase();
                        AuraApplication aurApp = baseAura.GetApplicationOfTarget(victim.GetGUID());
                        if (aurApp == null)
                            continue;

                        if ((absorbAurEff.GetMiscValue() & (int)damageInfo.GetSchoolMask()) == 0)
                            continue;

                        // cannot absorb over limit
                        if (damage >= victim.CountPctFromMaxHealth(100 + absorbAurEff.GetMiscValueB()))
                            continue;

                        // get amount which can be still absorbed by the aura
                        int currentAbsorb = absorbAurEff.GetAmount();
                        // aura with infinite absorb amount - let the scripts handle absorbtion amount, set here to 0 for safety
                        if (currentAbsorb < 0)
                            currentAbsorb = 0;

                        uint tempAbsorb = (uint)currentAbsorb;

                        // This aura type is used both by Spirit of Redemption (death not really prevented, must grant all credit immediately) and Cheat Death (death prevented)
                        // repurpose PreventDefaultAction for this
                        bool deathFullyPrevented = false;

                        absorbAurEff.GetBase().CallScriptEffectAbsorbHandlers(absorbAurEff, aurApp, damageInfo, ref tempAbsorb, ref deathFullyPrevented);
                        currentAbsorb = (int)tempAbsorb;

                        // absorb must be smaller than the damage itself
                        currentAbsorb = MathFunctions.RoundToInterval(ref currentAbsorb, 0, (int)damageInfo.GetDamage());
                        damageInfo.AbsorbDamage((uint)currentAbsorb);

                        if (deathFullyPrevented)
                            killed = false;

                        skipSettingDeathState = true;

                        if (currentAbsorb != 0)
                        {
                            SpellAbsorbLog absorbLog = new();
                            absorbLog.Attacker = attacker != null ? attacker.GetGUID() : ObjectGuid.Empty;
                            absorbLog.Victim = victim.GetGUID();
                            absorbLog.Caster = baseAura.GetCasterGUID();
                            absorbLog.AbsorbedSpellID = spellProto != null ? spellProto.Id : 0;
                            absorbLog.AbsorbSpellID = baseAura.GetId();
                            absorbLog.Absorbed = currentAbsorb;
                            absorbLog.OriginalDamage = damageInfo.GetOriginalDamage();
                            absorbLog.LogData.Initialize(victim);
                            victim.SendCombatLogMessage(absorbLog);
                        }
                    }

                    damage = damageInfo.GetDamage();
                }
            }

            if (killed)
                Kill(attacker, victim, durabilityLoss, skipSettingDeathState);
            else
            {
                victim.ModifyHealth(-(int)damage);

                if (damagetype == DamageEffectType.Direct || damagetype == DamageEffectType.SpellDirect)
                    victim.RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.NonPeriodicDamage, spellProto != null ? spellProto.Id : 0);

                if (!victim.IsTypeId(TypeId.Player))
                {
                    // Part of Evade mechanics. DoT's and Thorns / Retribution Aura do not contribute to this
                    if (damagetype != DamageEffectType.DOT && damage > 0 && !victim.GetOwnerGUID().IsPlayer() && (spellProto == null || !spellProto.HasAura(AuraType.DamageShield)))
                        victim.ToCreature().SetLastDamagedTime(GameTime.GetGameTime() + SharedConst.MaxAggroResetTime);

                    if (attacker != null)
                        victim.GetThreatManager().AddThreat(attacker, damage, spellProto);
                }
                else                                                // victim is a player
                {
                    // random durability for items (HIT TAKEN)
                    if (WorldConfig.GetFloatValue(WorldCfg.RateDurabilityLossDamage) > RandomHelper.randChance())
                    {
                        byte slot = (byte)RandomHelper.IRand(0, EquipmentSlot.End - 1);
                        victim.ToPlayer().DurabilityPointLossForEquipSlot(slot);
                    }
                }

                if (attacker != null && attacker.IsPlayer())
                {
                    // random durability for items (HIT DONE)
                    if (RandomHelper.randChance(WorldConfig.GetFloatValue(WorldCfg.RateDurabilityLossDamage)))
                    {
                        byte slot = (byte)RandomHelper.IRand(0, EquipmentSlot.End - 1);
                        attacker.ToPlayer().DurabilityPointLossForEquipSlot(slot);
                    }
                }

                if (damagetype != DamageEffectType.NoDamage)
                {
                    if (victim != attacker && (spellProto == null || !(spellProto.HasAttribute(SpellAttr7.NoPushbackOnDamage) || spellProto.HasAttribute(SpellAttr3.TreatAsPeriodic))))
                    {
                        if (damagetype != DamageEffectType.DOT)
                        {
                            Spell spell = victim.GetCurrentSpell(CurrentSpellTypes.Generic);
                            if (spell != null)
                            {
                                if (spell.GetState() == SpellState.Preparing)
                                {
                                    bool isCastInterrupted()
                                    {
                                        if (damage == 0)
                                            return spell.m_spellInfo.InterruptFlags.HasAnyFlag(SpellInterruptFlags.ZeroDamageCancels);

                                        if (victim.IsPlayer() && spell.m_spellInfo.InterruptFlags.HasAnyFlag(SpellInterruptFlags.DamageCancelsPlayerOnly))
                                            return true;

                                        if (spell.m_spellInfo.InterruptFlags.HasAnyFlag(SpellInterruptFlags.DamageCancels))
                                            return true;

                                        return false;
                                    };

                                    bool isCastDelayed()
                                    {
                                        if (damage == 0)
                                            return false;

                                        if (victim.IsPlayer() && spell.m_spellInfo.InterruptFlags.HasAnyFlag(SpellInterruptFlags.DamagePushbackPlayerOnly))
                                            return true;

                                        if (spell.m_spellInfo.InterruptFlags.HasAnyFlag(SpellInterruptFlags.DamagePushback))
                                            return true;

                                        return false;
                                    }

                                    if (isCastInterrupted())
                                        victim.InterruptNonMeleeSpells(false);
                                    else if (isCastDelayed())
                                        spell.Delayed();
                                }
                            }

                            if (damage != 0 && victim.IsPlayer())
                            {
                                Spell spell1 = victim.GetCurrentSpell(CurrentSpellTypes.Channeled);
                                if (spell1 != null)
                                    if (spell1.GetState() == SpellState.Casting && spell1.m_spellInfo.HasChannelInterruptFlag(SpellAuraInterruptFlags.DamageChannelDuration))
                                        spell1.DelayedChannel();
                            }
                        }
                    }
                }

                // last damage from duel opponent
                if (duel_hasEnded)
                {
                    Player he = duel_wasMounted ? victim.GetCharmer().ToPlayer() : victim.ToPlayer();

                    Cypher.Assert(he && he.duel != null);

                    if (duel_wasMounted) // In this case victim==mount
                        victim.SetHealth(1);
                    else
                        he.SetHealth(1);

                    he.duel.opponent.CombatStopWithPets(true);
                    he.CombatStopWithPets(true);

                    he.CastSpell(he, 7267, true);                  // beg
                    he.DuelComplete(DuelCompleteType.Won);
                }
            }

            return damage;
        }

        void DealMeleeDamage(CalcDamageInfo damageInfo, bool durabilityLoss)
        {
            Unit victim = damageInfo.Target;

            if (!victim.IsAlive() || victim.HasUnitState(UnitState.InFlight) || (victim.IsTypeId(TypeId.Unit) && victim.ToCreature().IsEvadingAttacks()))
                return;

            // Hmmmm dont like this emotes client must by self do all animations
            if (damageInfo.HitInfo.HasAnyFlag(HitInfo.CriticalHit))
                victim.HandleEmoteCommand(Emote.OneshotWoundCritical);
            if (damageInfo.Blocked != 0 && damageInfo.TargetState != VictimState.Blocks)
                victim.HandleEmoteCommand(Emote.OneshotParryShield);

            if (damageInfo.TargetState == VictimState.Parry &&
                (!IsTypeId(TypeId.Unit) || !ToCreature().GetCreatureTemplate().FlagsExtra.HasAnyFlag(CreatureFlagsExtra.NoParryHasten)))
            {
                // Get attack timers
                float offtime = victim.GetAttackTimer(WeaponAttackType.OffAttack);
                float basetime = victim.GetAttackTimer(WeaponAttackType.BaseAttack);
                // Reduce attack time
                if (victim.HaveOffhandWeapon() && offtime < basetime)
                {
                    float percent20 = victim.GetBaseAttackTime(WeaponAttackType.OffAttack) * 0.20f;
                    float percent60 = 3.0f * percent20;
                    if (offtime > percent20 && offtime <= percent60)
                        victim.SetAttackTimer(WeaponAttackType.OffAttack, (uint)percent20);
                    else if (offtime > percent60)
                    {
                        offtime -= 2.0f * percent20;
                        victim.SetAttackTimer(WeaponAttackType.OffAttack, (uint)offtime);
                    }
                }
                else
                {
                    float percent20 = victim.GetBaseAttackTime(WeaponAttackType.BaseAttack) * 0.20f;
                    float percent60 = 3.0f * percent20;
                    if (basetime > percent20 && basetime <= percent60)
                        victim.SetAttackTimer(WeaponAttackType.BaseAttack, (uint)percent20);
                    else if (basetime > percent60)
                    {
                        basetime -= 2.0f * percent20;
                        victim.SetAttackTimer(WeaponAttackType.BaseAttack, (uint)basetime);
                    }
                }
            }

            // Call default DealDamage
            CleanDamage cleanDamage = new(damageInfo.CleanDamage, damageInfo.Absorb, damageInfo.AttackType, damageInfo.HitOutCome);
            DealDamage(this, victim, damageInfo.Damage, cleanDamage, DamageEffectType.Direct, (SpellSchoolMask)damageInfo.DamageSchoolMask, null, durabilityLoss);

            // If this is a creature and it attacks from behind it has a probability to daze it's victim
            if ((damageInfo.HitOutCome == MeleeHitOutcome.Crit || damageInfo.HitOutCome == MeleeHitOutcome.Crushing || damageInfo.HitOutCome == MeleeHitOutcome.Normal || damageInfo.HitOutCome == MeleeHitOutcome.Glancing) &&
                !IsTypeId(TypeId.Player) && !ToCreature().IsControlledByPlayer() && !victim.HasInArc(MathFunctions.PI, this)
                && (victim.IsTypeId(TypeId.Player) || !victim.ToCreature().IsWorldBoss()) && !victim.IsVehicle())
            {
                // 20% base chance
                float chance = 20.0f;

                // there is a newbie protection, at level 10 just 7% base chance; assuming linear function
                if (victim.GetLevel() < 30)
                    chance = 0.65f * victim.GetLevelForTarget(this) + 0.5f;

                uint victimDefense = victim.GetMaxSkillValueForLevel(this);
                uint attackerMeleeSkill = GetMaxSkillValueForLevel();

                chance *= attackerMeleeSkill / (float)victimDefense * 0.16f;

                // -probability is between 0% and 40%
                MathFunctions.RoundToInterval(ref chance, 0.0f, 40.0f);

                if (RandomHelper.randChance(chance))
                    CastSpell(victim, 1604, true);
            }

            if (IsTypeId(TypeId.Player))
            {
                DamageInfo dmgInfo = new(damageInfo);
                ToPlayer().CastItemCombatSpell(dmgInfo);
            }

            // Do effect if any damage done to target
            if (damageInfo.Damage != 0)
            {
                // We're going to call functions which can modify content of the list during iteration over it's elements
                // Let's copy the list so we can prevent iterator invalidation
                var vDamageShieldsCopy = victim.GetAuraEffectsByType(AuraType.DamageShield);
                foreach (var dmgShield in vDamageShieldsCopy)
                {
                    SpellInfo spellInfo = dmgShield.GetSpellInfo();

                    // Damage shield can be resisted...
                    var missInfo = victim.SpellHitResult(this, spellInfo, false);
                    if (missInfo != SpellMissInfo.None)
                    {
                        victim.SendSpellMiss(this, spellInfo.Id, missInfo);
                        continue;
                    }

                    // ...or immuned
                    if (IsImmunedToDamage(spellInfo))
                    {
                        victim.SendSpellDamageImmune(this, spellInfo.Id, false);
                        continue;
                    }

                    uint damage = (uint)dmgShield.GetAmount();
                    Unit caster = dmgShield.GetCaster();
                    if (caster)
                    {
                        damage = caster.SpellDamageBonusDone(this, spellInfo, damage, DamageEffectType.SpellDirect, dmgShield.GetSpellEffectInfo());
                        damage = SpellDamageBonusTaken(caster, spellInfo, damage, DamageEffectType.SpellDirect);
                    }

                    DamageInfo damageInfo1 = new(this, victim, damage, spellInfo, spellInfo.GetSchoolMask(), DamageEffectType.SpellDirect, WeaponAttackType.BaseAttack);
                    CalcAbsorbResist(damageInfo1);
                    damage = damageInfo1.GetDamage();

                    DealDamageMods(victim, this, ref damage);

                    SpellDamageShield damageShield = new();
                    damageShield.Attacker = victim.GetGUID();
                    damageShield.Defender = GetGUID();
                    damageShield.SpellID = spellInfo.Id;
                    damageShield.TotalDamage = damage;
                    damageShield.OriginalDamage = (int)damageInfo.OriginalDamage;
                    damageShield.OverKill = (uint)Math.Max(damage - GetHealth(), 0);
                    damageShield.SchoolMask = (uint)spellInfo.SchoolMask;
                    damageShield.LogAbsorbed = damageInfo1.GetAbsorb();

                    DealDamage(victim, this, damage, null, DamageEffectType.SpellDirect, spellInfo.GetSchoolMask(), spellInfo, true);
                    damageShield.LogData.Initialize(this);

                    victim.SendCombatLogMessage(damageShield);
                }
            }
        }


        public long ModifyHealth(long dVal)
        {
            long gain = 0;

            if (dVal == 0)
                return 0;

            long curHealth = (long)GetHealth();

            long val = dVal + curHealth;
            if (val <= 0)
            {
                SetHealth(0);
                return -curHealth;
            }

            long maxHealth = (long)GetMaxHealth();
            if (val < maxHealth)
            {
                SetHealth((uint)val);
                gain = val - curHealth;
            }
            else if (curHealth != maxHealth)
            {
                SetHealth((uint)maxHealth);
                gain = maxHealth - curHealth;
            }

            if (dVal < 0)
            {
                HealthUpdate packet = new();
                packet.Guid = GetGUID();
                packet.Health = (long)GetHealth();

                Player player = GetCharmerOrOwnerPlayerOrPlayerItself();
                if (player)
                    player.SendPacket(packet);
            }

            return gain;
        }
        public long GetHealthGain(long dVal)
        {
            long gain = 0;

            if (dVal == 0)
                return 0;

            long curHealth = (long)GetHealth();

            long val = dVal + curHealth;
            if (val <= 0)
            {
                return -curHealth;
            }

            long maxHealth = (long)GetMaxHealth();

            if (val < maxHealth)
                gain = dVal;
            else if (curHealth != maxHealth)
                gain = maxHealth - curHealth;

            return gain;
        }

        public bool IsImmuneToAll() => IsImmuneToPC() && IsImmuneToNPC();

        public void SetImmuneToAll(bool apply, bool keepCombat)
        {
            if (apply)
            {
                AddUnitFlag(UnitFlags.ImmuneToPc | UnitFlags.ImmuneToNpc);
                ValidateAttackersAndOwnTarget();
                if (keepCombat)
                    m_threatManager.UpdateOnlineStates(true, true);
                else
                    m_combatManager.EndAllCombat();
            }
            else
            {
                RemoveUnitFlag(UnitFlags.ImmuneToPc | UnitFlags.ImmuneToNpc);
                m_threatManager.UpdateOnlineStates(true, true);
            }
        }

        public virtual void SetImmuneToAll(bool apply) => SetImmuneToAll(apply, false);

        public bool IsImmuneToPC() => HasUnitFlag(UnitFlags.ImmuneToPc);

        public void SetImmuneToPC(bool apply, bool keepCombat)
        {
            if (apply)
            {
                AddUnitFlag(UnitFlags.ImmuneToPc);
                ValidateAttackersAndOwnTarget();
                if (keepCombat)
                    m_threatManager.UpdateOnlineStates(true, true);
                else
                {
                    List<CombatReference> toEnd = new();
                    foreach (var pair in m_combatManager.GetPvECombatRefs())
                        if (pair.Value.GetOther(this).HasUnitFlag(UnitFlags.PvpAttackable))
                            toEnd.Add(pair.Value);

                    foreach (var pair in m_combatManager.GetPvPCombatRefs())
                        if (pair.Value.GetOther(this).HasUnitFlag(UnitFlags.PvpAttackable))
                            toEnd.Add(pair.Value);

                    foreach (CombatReference refe in toEnd)
                        refe.EndCombat();
                }
            }
            else
            {
                RemoveUnitFlag(UnitFlags.ImmuneToPc);
                m_threatManager.UpdateOnlineStates(true, true);
            }
        }

        public virtual void SetImmuneToPC(bool apply) => SetImmuneToPC(apply, false);

        public bool IsImmuneToNPC() => HasUnitFlag(UnitFlags.ImmuneToNpc);

        public void SetImmuneToNPC(bool apply, bool keepCombat)
        {
            if (apply)
            {
                AddUnitFlag(UnitFlags.ImmuneToNpc);
                ValidateAttackersAndOwnTarget();
                if (keepCombat)
                    m_threatManager.UpdateOnlineStates(true, true);
                else
                {
                    List<CombatReference> toEnd = new();
                    foreach (var pair in m_combatManager.GetPvECombatRefs())
                        if (!pair.Value.GetOther(this).HasUnitFlag(UnitFlags.PvpAttackable))
                            toEnd.Add(pair.Value);

                    foreach (var pair in m_combatManager.GetPvPCombatRefs())
                        if (!pair.Value.GetOther(this).HasUnitFlag(UnitFlags.PvpAttackable))
                            toEnd.Add(pair.Value);

                    foreach (CombatReference refe in toEnd)
                        refe.EndCombat();
                }
            }
            else
            {
                RemoveUnitFlag(UnitFlags.ImmuneToNpc);
                m_threatManager.UpdateOnlineStates(true, true);
            }
        }

        public virtual void SetImmuneToNPC(bool apply) => SetImmuneToNPC(apply, false);

        public virtual float GetBlockPercent(uint attackerLevel) => 30.0f;

        void UpdateReactives(uint p_time)
        {
            for (ReactiveType reactive = 0; reactive < ReactiveType.Max; ++reactive)
            {
                if (!m_reactiveTimer.ContainsKey(reactive))
                    continue;

                if (m_reactiveTimer[reactive] <= p_time)
                {
                    m_reactiveTimer[reactive] = 0;

                    switch (reactive)
                    {
                        case ReactiveType.Defense:
                            if (HasAuraState(AuraStateType.Defensive))
                                ModifyAuraState(AuraStateType.Defensive, false);
                            break;
                        case ReactiveType.Defense2:
                            if (HasAuraState(AuraStateType.Defensive2))
                                ModifyAuraState(AuraStateType.Defensive2, false);
                            break;
                    }
                }
                else
                {
                    m_reactiveTimer[reactive] -= p_time;
                }
            }
        }

        public void RewardRage(uint baseRage)
        {
            float addRage = baseRage;

            // talent who gave more rage on attack
            MathFunctions.AddPct(ref addRage, GetTotalAuraModifier(AuraType.ModRageFromDamageDealt));

            addRage *= WorldConfig.GetFloatValue(WorldCfg.RatePowerRageIncome);

            ModifyPower(PowerType.Rage, (int)(addRage * 10));
        }

        public float GetPPMProcChance(uint WeaponSpeed, float PPM, SpellInfo spellProto)
        {
            // proc per minute chance calculation
            if (PPM <= 0)
                return 0.0f;

            // Apply chance modifer aura
            if (spellProto != null)
            {
                Player modOwner = GetSpellModOwner();
                if (modOwner != null)
                    modOwner.ApplySpellMod(spellProto, SpellModOp.ProcFrequency, ref PPM);
            }

            return (float)Math.Floor((WeaponSpeed * PPM) / 600.0f);   // result is chance in percents (probability = Speed_in_sec * (PPM / 60))
        }

        public Unit GetNextRandomRaidMemberOrPet(float radius)
        {
            Player player = null;
            if (IsTypeId(TypeId.Player))
                player = ToPlayer();
            // Should we enable this also for charmed units?
            else if (IsTypeId(TypeId.Unit) && IsPet())
                player = GetOwner().ToPlayer();

            if (player == null)
                return null;
            Group group = player.GetGroup();
            // When there is no group check pet presence
            if (!group)
            {
                // We are pet now, return owner
                if (player != this)
                    return IsWithinDistInMap(player, radius) ? player : null;
                Unit pet = GetGuardianPet();
                // No pet, no group, nothing to return
                if (pet == null)
                    return null;
                // We are owner now, return pet
                return IsWithinDistInMap(pet, radius) ? pet : null;
            }

            List<Unit> nearMembers = new();
            // reserve place for players and pets because resizing vector every unit push is unefficient (vector is reallocated then)

            for (GroupReference refe = group.GetFirstMember(); refe != null; refe = refe.Next())
            {
                Player target = refe.GetSource();
                if (target)
                {
                    // IsHostileTo check duel and controlled by enemy
                    if (target != this && IsWithinDistInMap(target, radius) && target.IsAlive() && !IsHostileTo(target))
                        nearMembers.Add(target);

                    // Push player's pet to vector
                    Unit pet = target.GetGuardianPet();
                    if (pet)
                        if (pet != this && IsWithinDistInMap(pet, radius) && pet.IsAlive() && !IsHostileTo(pet))
                            nearMembers.Add(pet);
                }
            }

            if (nearMembers.Empty())
                return null;

            int randTarget = RandomHelper.IRand(0, nearMembers.Count - 1);
            return nearMembers[randTarget];
        }

        public void ClearAllReactives()
        {
            for (ReactiveType i = 0; i < ReactiveType.Max; ++i)
                m_reactiveTimer[i] = 0;

            if (HasAuraState(AuraStateType.Defensive))
                ModifyAuraState(AuraStateType.Defensive, false);
            if (HasAuraState(AuraStateType.Defensive2))
                ModifyAuraState(AuraStateType.Defensive2, false);
        }

        public virtual void SetPvP(bool state)
        {
            if (state)
                AddPvpFlag(UnitPVPStateFlags.PvP);
            else
                RemovePvpFlag(UnitPVPStateFlags.PvP);
        }

        static uint CalcSpellResistedDamage(Unit attacker, Unit victim, uint damage, SpellSchoolMask schoolMask, SpellInfo spellInfo)
        {
            // Magic damage, check for resists
            if (!Convert.ToBoolean(schoolMask & SpellSchoolMask.Magic))
                return 0;

            // Npcs can have holy resistance
            if (schoolMask.HasAnyFlag(SpellSchoolMask.Holy) && victim.GetTypeId() != TypeId.Unit)
                return 0;

            // Ignore spells that can't be resisted
            if (spellInfo != null)
            {
                if (spellInfo.HasAttribute(SpellAttr4.IgnoreResistances))
                    return 0;

                // Binary spells can't have damage part resisted
                if (spellInfo.HasAttribute(SpellCustomAttributes.BinarySpell))
                    return 0;
            }

            float averageResist = CalculateAverageResistReduction(attacker, schoolMask, victim, spellInfo);

            float[] discreteResistProbability = new float[11];
            if (averageResist <= 0.1f)
            {
                discreteResistProbability[0] = 1.0f - 7.5f * averageResist;
                discreteResistProbability[1] = 5.0f * averageResist;
                discreteResistProbability[2] = 2.5f * averageResist;
            }
            else
            {
                for (uint i = 0; i < 11; ++i)
                    discreteResistProbability[i] = Math.Max(0.5f - 2.5f * Math.Abs(0.1f * i - averageResist), 0.0f);
            }

            float roll = (float)RandomHelper.NextDouble();
            float probabilitySum = 0.0f;

            uint resistance = 0;
            for (; resistance < 11; ++resistance)
                if (roll < (probabilitySum += discreteResistProbability[resistance]))
                    break;

            float damageResisted = damage * resistance / 10f;
            if (damageResisted > 0.0f) // if any damage was resisted
            {
                int ignoredResistance = 0;

                if (attacker != null)
                    ignoredResistance += attacker.GetTotalAuraModifierByMiscMask(AuraType.ModIgnoreTargetResist, (int)schoolMask);

                ignoredResistance = Math.Min(ignoredResistance, 100);
                MathFunctions.ApplyPct(ref damageResisted, 100 - ignoredResistance);

                // Spells with melee and magic school mask, decide whether resistance or armor absorb is higher
                if (spellInfo != null && spellInfo.HasAttribute(SpellCustomAttributes.SchoolmaskNormalWithMagic))
                {
                    uint damageAfterArmor = CalcArmorReducedDamage(attacker, victim, damage, spellInfo, spellInfo.GetAttackType());
                    float armorReduction = damage - damageAfterArmor;

                    // pick the lower one, the weakest resistance counts
                    damageResisted = Math.Min(damageResisted, armorReduction);
                }
            }

            damageResisted = Math.Max(damageResisted, 0.0f);
            return (uint)damageResisted;
        }

        static float CalculateAverageResistReduction(Unit attacker, SpellSchoolMask schoolMask, Unit victim, SpellInfo spellInfo = null)
        {
            float victimResistance = victim.GetResistance(schoolMask);

            if (attacker != null)
            {
                // pets inherit 100% of masters penetration
                // excluding traps
                Player player = attacker.GetSpellModOwner();
                if (player != null && attacker.GetEntry() != SharedConst.WorldTrigger)
                {
                    victimResistance += player.GetTotalAuraModifierByMiscMask(AuraType.ModTargetResistance, (int)schoolMask);
                    victimResistance -= player.GetSpellPenetrationItemMod();
                }
                else
                    victimResistance += attacker.GetTotalAuraModifierByMiscMask(AuraType.ModTargetResistance, (int)schoolMask);
            }

            // holy resistance exists in pve and comes from level difference, ignore template values
            if (schoolMask.HasAnyFlag(SpellSchoolMask.Holy))
                victimResistance = 0.0f;

            // Chaos Bolt exception, ignore all target resistances (unknown attribute?)
            if (spellInfo != null && spellInfo.SpellFamilyName == SpellFamilyNames.Warlock && spellInfo.Id == 116858)
                victimResistance = 0.0f;

            victimResistance = Math.Max(victimResistance, 0.0f);

            // level-based resistance does not apply to binary spells, and cannot be overcome by spell penetration
            if (attacker != null && (spellInfo == null || !spellInfo.HasAttribute(SpellCustomAttributes.BinarySpell)))
                victimResistance += Math.Max((victim.GetLevelForTarget(attacker) - (float)attacker.GetLevelForTarget(victim)) * 5.0f, 0.0f);

            uint bossLevel = 83;
            float bossResistanceConstant = 510.0f;
            uint level = attacker != null ? victim.GetLevelForTarget(attacker) : attacker.GetLevel();
            float resistanceConstant;

            if (level == bossLevel)
                resistanceConstant = bossResistanceConstant;
            else
                resistanceConstant = level * 5.0f;

            return victimResistance / (victimResistance + resistanceConstant);
        }

        public static void CalcAbsorbResist(DamageInfo damageInfo)
        {
            if (!damageInfo.GetVictim() || !damageInfo.GetVictim().IsAlive() || damageInfo.GetDamage() == 0)
                return;

            uint resistedDamage = CalcSpellResistedDamage(damageInfo.GetAttacker(), damageInfo.GetVictim(), damageInfo.GetDamage(), damageInfo.GetSchoolMask(), damageInfo.GetSpellInfo());
            damageInfo.ResistDamage(resistedDamage);

            // Ignore Absorption Auras
            float auraAbsorbMod = 0f;

            Unit attacker = damageInfo.GetAttacker();
            if (attacker != null)
                auraAbsorbMod = attacker.GetMaxPositiveAuraModifierByMiscMask(AuraType.ModTargetAbsorbSchool, (uint)damageInfo.GetSchoolMask());

            MathFunctions.RoundToInterval(ref auraAbsorbMod, 0.0f, 100.0f);

            int absorbIgnoringDamage = (int)MathFunctions.CalculatePct(damageInfo.GetDamage(), auraAbsorbMod);
            damageInfo.ModifyDamage(-absorbIgnoringDamage);

            // We're going to call functions which can modify content of the list during iteration over it's elements
            // Let's copy the list so we can prevent iterator invalidation
            var vSchoolAbsorbCopy = damageInfo.GetVictim().GetAuraEffectsByType(AuraType.SchoolAbsorb);
            vSchoolAbsorbCopy.Sort(new AbsorbAuraOrderPred());

            // absorb without mana cost
            for (var i = 0; i < vSchoolAbsorbCopy.Count; ++i)
            {
                var absorbAurEff = vSchoolAbsorbCopy[i];
                if (damageInfo.GetDamage() == 0)
                    break;

                // Check if aura was removed during iteration - we don't need to work on such auras
                AuraApplication aurApp = absorbAurEff.GetBase().GetApplicationOfTarget(damageInfo.GetVictim().GetGUID());
                if (aurApp == null)
                    continue;
                if (!Convert.ToBoolean(absorbAurEff.GetMiscValue() & (int)damageInfo.GetSchoolMask()))
                    continue;

                // get amount which can be still absorbed by the aura
                int currentAbsorb = absorbAurEff.GetAmount();
                // aura with infinite absorb amount - let the scripts handle absorbtion amount, set here to 0 for safety
                if (currentAbsorb < 0)
                    currentAbsorb = 0;

                uint tempAbsorb = (uint)currentAbsorb;

                bool defaultPrevented = false;

                absorbAurEff.GetBase().CallScriptEffectAbsorbHandlers(absorbAurEff, aurApp, damageInfo, ref tempAbsorb, ref defaultPrevented);
                currentAbsorb = (int)tempAbsorb;

                if (!defaultPrevented)
                {

                    // absorb must be smaller than the damage itself
                    currentAbsorb = MathFunctions.RoundToInterval(ref currentAbsorb, 0, damageInfo.GetDamage());

                    damageInfo.AbsorbDamage((uint)currentAbsorb);

                    tempAbsorb = (uint)currentAbsorb;
                    absorbAurEff.GetBase().CallScriptEffectAfterAbsorbHandlers(absorbAurEff, aurApp, damageInfo, ref tempAbsorb);

                    // Check if our aura is using amount to count damage
                    if (absorbAurEff.GetAmount() >= 0)
                    {
                        // Reduce shield amount
                        absorbAurEff.ChangeAmount(absorbAurEff.GetAmount() - currentAbsorb);
                        // Aura cannot absorb anything more - remove it
                        if (absorbAurEff.GetAmount() <= 0)
                            absorbAurEff.GetBase().Remove(AuraRemoveMode.EnemySpell);
                    }
                }

                if (currentAbsorb != 0)
                {
                    SpellAbsorbLog absorbLog = new();
                    absorbLog.Attacker = damageInfo.GetAttacker() != null ? damageInfo.GetAttacker().GetGUID() : ObjectGuid.Empty;
                    absorbLog.Victim = damageInfo.GetVictim().GetGUID();
                    absorbLog.Caster = absorbAurEff.GetBase().GetCasterGUID();
                    absorbLog.AbsorbedSpellID = damageInfo.GetSpellInfo() != null ? damageInfo.GetSpellInfo().Id : 0;
                    absorbLog.AbsorbSpellID = absorbAurEff.GetId();
                    absorbLog.Absorbed = currentAbsorb;
                    absorbLog.OriginalDamage = damageInfo.GetOriginalDamage();
                    absorbLog.LogData.Initialize(damageInfo.GetVictim());
                    damageInfo.GetVictim().SendCombatLogMessage(absorbLog);
                }
            }

            // absorb by mana cost
            var vManaShieldCopy = damageInfo.GetVictim().GetAuraEffectsByType(AuraType.ManaShield);
            foreach (var absorbAurEff in vManaShieldCopy)
            {
                if (damageInfo.GetDamage() == 0)
                    break;

                // Check if aura was removed during iteration - we don't need to work on such auras
                AuraApplication aurApp = absorbAurEff.GetBase().GetApplicationOfTarget(damageInfo.GetVictim().GetGUID());
                if (aurApp == null)
                    continue;
                // check damage school mask
                if (!Convert.ToBoolean(absorbAurEff.GetMiscValue() & (int)damageInfo.GetSchoolMask()))
                    continue;

                // get amount which can be still absorbed by the aura
                int currentAbsorb = absorbAurEff.GetAmount();
                // aura with infinite absorb amount - let the scripts handle absorbtion amount, set here to 0 for safety
                if (currentAbsorb < 0)
                    currentAbsorb = 0;

                uint tempAbsorb = (uint)currentAbsorb;

                bool defaultPrevented = false;

                absorbAurEff.GetBase().CallScriptEffectManaShieldHandlers(absorbAurEff, aurApp, damageInfo, ref tempAbsorb, ref defaultPrevented);
                currentAbsorb = (int)tempAbsorb;

                if (!defaultPrevented)
                {
                    // absorb must be smaller than the damage itself
                    currentAbsorb = MathFunctions.RoundToInterval(ref currentAbsorb, 0, damageInfo.GetDamage());

                    int manaReduction = currentAbsorb;

                    // lower absorb amount by talents
                    float manaMultiplier = absorbAurEff.GetSpellEffectInfo().CalcValueMultiplier(absorbAurEff.GetCaster());
                    if (manaMultiplier != 0)
                        manaReduction = (int)(manaReduction * manaMultiplier);

                    int manaTaken = -damageInfo.GetVictim().ModifyPower(PowerType.Mana, -manaReduction);

                    // take case when mana has ended up into account
                    currentAbsorb = currentAbsorb != 0 ? (currentAbsorb * (manaTaken / manaReduction)) : 0;

                    damageInfo.AbsorbDamage((uint)currentAbsorb);

                    tempAbsorb = (uint)currentAbsorb;
                    absorbAurEff.GetBase().CallScriptEffectAfterManaShieldHandlers(absorbAurEff, aurApp, damageInfo, ref tempAbsorb);

                    // Check if our aura is using amount to count damage
                    if (absorbAurEff.GetAmount() >= 0)
                    {
                        absorbAurEff.ChangeAmount(absorbAurEff.GetAmount() - currentAbsorb);
                        if ((absorbAurEff.GetAmount() <= 0))
                            absorbAurEff.GetBase().Remove(AuraRemoveMode.EnemySpell);
                    }
                }

                if (currentAbsorb != 0)
                {
                    SpellAbsorbLog absorbLog = new();
                    absorbLog.Attacker = damageInfo.GetAttacker() != null ? damageInfo.GetAttacker().GetGUID() : ObjectGuid.Empty;
                    absorbLog.Victim = damageInfo.GetVictim().GetGUID();
                    absorbLog.Caster = absorbAurEff.GetBase().GetCasterGUID();
                    absorbLog.AbsorbedSpellID = damageInfo.GetSpellInfo() != null ? damageInfo.GetSpellInfo().Id : 0;
                    absorbLog.AbsorbSpellID = absorbAurEff.GetId();
                    absorbLog.Absorbed = currentAbsorb;
                    absorbLog.OriginalDamage = damageInfo.GetOriginalDamage();
                    absorbLog.LogData.Initialize(damageInfo.GetVictim());
                    damageInfo.GetVictim().SendCombatLogMessage(absorbLog);
                }
            }

            damageInfo.ModifyDamage(absorbIgnoringDamage);

            // split damage auras - only when not damaging self
            if (damageInfo.GetVictim() != damageInfo.GetAttacker())
            {
                // We're going to call functions which can modify content of the list during iteration over it's elements
                // Let's copy the list so we can prevent iterator invalidation
                var vSplitDamagePctCopy = damageInfo.GetVictim().GetAuraEffectsByType(AuraType.SplitDamagePct);
                foreach (var itr in vSplitDamagePctCopy)
                {
                    if (damageInfo.GetDamage() == 0)
                        break;

                    // Check if aura was removed during iteration - we don't need to work on such auras
                    AuraApplication aurApp = itr.GetBase().GetApplicationOfTarget(damageInfo.GetVictim().GetGUID());
                    if (aurApp == null)
                        continue;

                    // check damage school mask
                    if (!Convert.ToBoolean(itr.GetMiscValue() & (int)damageInfo.GetSchoolMask()))
                        continue;

                    // Damage can be splitted only if aura has an alive caster
                    Unit caster = itr.GetCaster();
                    if (!caster || (caster == damageInfo.GetVictim()) || !caster.IsInWorld || !caster.IsAlive())
                        continue;

                    uint splitDamage = MathFunctions.CalculatePct(damageInfo.GetDamage(), itr.GetAmount());

                    itr.GetBase().CallScriptEffectSplitHandlers(itr, aurApp, damageInfo, splitDamage);

                    // absorb must be smaller than the damage itself
                    splitDamage = MathFunctions.RoundToInterval(ref splitDamage, 0, damageInfo.GetDamage());

                    damageInfo.AbsorbDamage(splitDamage);

                    // check if caster is immune to damage
                    if (caster.IsImmunedToDamage(damageInfo.GetSchoolMask()))
                    {
                        damageInfo.GetVictim().SendSpellMiss(caster, itr.GetSpellInfo().Id, SpellMissInfo.Immune);
                        continue;
                    }

                    uint split_absorb = 0;
                    DealDamageMods(damageInfo.GetAttacker(), caster, ref splitDamage, ref split_absorb);

                    SpellNonMeleeDamage log = new(damageInfo.GetAttacker(), caster, itr.GetSpellInfo(), itr.GetBase().GetSpellXSpellVisualID(), damageInfo.GetSchoolMask(), itr.GetBase().GetCastGUID());
                    CleanDamage cleanDamage = new(splitDamage, 0, WeaponAttackType.BaseAttack, MeleeHitOutcome.Normal);
                    DealDamage(damageInfo.GetAttacker(), caster, splitDamage, cleanDamage, DamageEffectType.Direct, damageInfo.GetSchoolMask(), itr.GetSpellInfo(), false);
                    log.damage = splitDamage;
                    log.originalDamage = splitDamage;
                    log.absorb = split_absorb;
                    caster.SendSpellNonMeleeDamageLog(log);

                    // break 'Fear' and similar auras
                    ProcSkillsAndAuras(damageInfo.GetAttacker(), caster, ProcFlags.None, ProcFlags.TakenSpellMagicDmgClassNeg, ProcFlagsSpellType.Damage, ProcFlagsSpellPhase.Hit, ProcFlagsHit.None, null, damageInfo, null);
                }
            }
        }

        public static void CalcHealAbsorb(HealInfo healInfo)
        {
            if (healInfo.GetHeal() == 0)
                return;

            // Need remove expired auras after
            bool existExpired = false;

            // absorb without mana cost
            var vHealAbsorb = healInfo.GetTarget().GetAuraEffectsByType(AuraType.SchoolHealAbsorb);
            for (var i = 0; i < vHealAbsorb.Count; ++i)
            {
                var eff = vHealAbsorb[i];
                if (healInfo.GetHeal() <= 0)
                    break;

                if (!Convert.ToBoolean(eff.GetMiscValue() & (int)healInfo.GetSpellInfo().SchoolMask))
                    continue;

                // Max Amount can be absorbed by this aura
                int currentAbsorb = eff.GetAmount();

                // Found empty aura (impossible but..)
                if (currentAbsorb <= 0)
                {
                    existExpired = true;
                    continue;
                }

                // currentAbsorb - damage can be absorbed by shield
                // If need absorb less damage
                currentAbsorb = (int)Math.Min(healInfo.GetHeal(), currentAbsorb);

                healInfo.AbsorbHeal((uint)currentAbsorb);

                // Reduce shield amount
                eff.ChangeAmount(eff.GetAmount() - currentAbsorb);
                // Need remove it later
                if (eff.GetAmount() <= 0)
                    existExpired = true;
            }

            // Remove all expired absorb auras
            if (existExpired)
            {
                for (var i = 0; i < vHealAbsorb.Count;)
                {
                    AuraEffect auraEff = vHealAbsorb[i];
                    ++i;
                    if (auraEff.GetAmount() <= 0)
                    {
                        uint removedAuras = healInfo.GetTarget().m_removedAurasCount;
                        auraEff.GetBase().Remove(AuraRemoveMode.EnemySpell);
                        if (removedAuras + 1 < healInfo.GetTarget().m_removedAurasCount)
                            i = 0;
                    }
                }
            }
        }

        public static uint CalcArmorReducedDamage(Unit attacker, Unit victim, uint damage, SpellInfo spellInfo, WeaponAttackType attackType = WeaponAttackType.Max, uint attackerLevel = 0)
        {
            float armor = victim.GetArmor();

            if (attacker != null)
            {
                armor *= victim.GetTotalAuraModifierByMiscMask(AuraType.ModTargetResistance, (int)SpellSchoolMask.Normal);

                // bypass enemy armor by SPELL_AURA_BYPASS_ARMOR_FOR_CASTER
                int armorBypassPct = 0;
                var reductionAuras = victim.GetAuraEffectsByType(AuraType.BypassArmorForCaster);
                foreach (var eff in reductionAuras)
                    if (eff.GetCasterGUID() == attacker.GetGUID())
                        armorBypassPct += eff.GetAmount();

                armor = MathFunctions.CalculatePct(armor, 100 - Math.Min(armorBypassPct, 100));

                // Ignore enemy armor by SPELL_AURA_MOD_TARGET_RESISTANCE aura
                armor += attacker.GetTotalAuraModifierByMiscMask(AuraType.ModTargetResistance, (int)SpellSchoolMask.Normal);

                if (spellInfo != null)
                {
                    Player modOwner = attacker.GetSpellModOwner();
                    if (modOwner != null)
                        modOwner.ApplySpellMod(spellInfo, SpellModOp.TargetResistance, ref armor);
                }

                var resIgnoreAuras = attacker.GetAuraEffectsByType(AuraType.ModIgnoreTargetResist);
                foreach (var eff in resIgnoreAuras)
                {
                    if (eff.GetMiscValue().HasAnyFlag((int)SpellSchoolMask.Normal) && eff.IsAffectingSpell(spellInfo))
                        armor = (float)Math.Floor(MathFunctions.AddPct(ref armor, -eff.GetAmount()));
                }

                // Apply Player CR_ARMOR_PENETRATION rating
                if (attacker.IsPlayer())
                {
                    float arpPct = attacker.ToPlayer().GetRatingBonusValue(CombatRating.ArmorPenetration);

                    // no more than 100%
                    MathFunctions.RoundToInterval(ref arpPct, 0.0f, 100.0f);

                    float maxArmorPen;
                    if (victim.GetLevelForTarget(attacker) < 60)
                        maxArmorPen = 400 + 85 * victim.GetLevelForTarget(attacker);
                    else
                        maxArmorPen = 400 + 85 * victim.GetLevelForTarget(attacker) + 4.5f * 85 * (victim.GetLevelForTarget(attacker) - 59);

                    // Cap armor penetration to this number
                    maxArmorPen = Math.Min((armor + maxArmorPen) / 3.0f, armor);
                    // Figure out how much armor do we ignore
                    armor -= MathFunctions.CalculatePct(maxArmorPen, arpPct);
                }
            }

            if (MathFunctions.fuzzyLe(armor, 0.0f))
                return damage;

            if (attacker != null)
                attackerLevel = attacker.GetLevelForTarget(victim);

            float levelModifier = attacker != null ? attacker.GetLevel() : attackerLevel;
            if (levelModifier > 59.0f)
                levelModifier = levelModifier + 4.5f * (levelModifier - 59.0f);

            float damageReduction = 0.1f * armor / (8.5f * levelModifier + 40.0f);
            damageReduction /= (1.0f + damageReduction);

            return Math.Max((uint)(damage * (1.0f - damageReduction)), 0);
        }

        public uint MeleeDamageBonusDone(Unit victim, uint damage, WeaponAttackType attType, DamageEffectType damagetype, SpellInfo spellProto = null, SpellSchoolMask damageSchoolMask = SpellSchoolMask.Normal)
        {
            if (victim == null || damage == 0)
                return 0;

            uint creatureTypeMask = victim.GetCreatureTypeMask();

            // Done fixed damage bonus auras
            int DoneFlatBenefit = 0;

            // ..done
            DoneFlatBenefit += GetTotalAuraModifierByMiscMask(AuraType.ModDamageDoneCreature, (int)creatureTypeMask);

            // ..done
            // SPELL_AURA_MOD_DAMAGE_DONE included in weapon damage

            // ..done (base at attack power for marked target and base at attack power for creature type)
            int APbonus = 0;

            if (attType == WeaponAttackType.RangedAttack)
            {
                APbonus += victim.GetTotalAuraModifier(AuraType.RangedAttackPowerAttackerBonus);

                // ..done (base at attack power and creature type)
                APbonus += GetTotalAuraModifierByMiscMask(AuraType.ModRangedAttackPowerVersus, (int)creatureTypeMask);
            }
            else
            {
                APbonus += victim.GetTotalAuraModifier(AuraType.MeleeAttackPowerAttackerBonus);

                // ..done (base at attack power and creature type)
                APbonus += GetTotalAuraModifierByMiscMask(AuraType.ModMeleeAttackPowerVersus, (int)creatureTypeMask);
            }

            if (APbonus != 0)                                       // Can be negative
            {
                bool normalized = spellProto != null && spellProto.HasEffect(SpellEffectName.NormalizedWeaponDmg);
                DoneFlatBenefit += (int)(APbonus / 3.5f * GetAPMultiplier(attType, normalized));
            }

            // Done total percent damage auras
            float DoneTotalMod = 1.0f;

            SpellSchoolMask schoolMask = spellProto != null ? spellProto.GetSchoolMask() : damageSchoolMask;

            if ((schoolMask & SpellSchoolMask.Normal) == 0)
            {
                // Some spells don't benefit from pct done mods
                // mods for SPELL_SCHOOL_MASK_NORMAL are already factored in base melee damage calculation
                if (spellProto == null || !spellProto.HasAttribute(SpellAttr6.IgnoreCasterDamageModifiers))
                {
                    float maxModDamagePercentSchool = 0.0f;
                    Player thisPlayer = ToPlayer();
                    if (thisPlayer != null)
                    {
                        for (var i = SpellSchools.Holy; i < SpellSchools.Max; ++i)
                        {
                            if (Convert.ToBoolean((int)schoolMask & (1 << (int)i)))
                                maxModDamagePercentSchool = Math.Max(maxModDamagePercentSchool, thisPlayer.GetUpdateField<float>(ActivePlayerFields.ModDamageDoneNeg + (int)i));
                        }
                    }
                    else
                        maxModDamagePercentSchool = GetTotalAuraMultiplierByMiscMask(AuraType.ModDamagePercentDone, (uint)schoolMask);

                    DoneTotalMod *= maxModDamagePercentSchool;
                }
            }

            if (spellProto == null)
            {
                // melee attack
                foreach (AuraEffect autoAttackDamage in GetAuraEffectsByType(AuraType.ModAutoAttackDamage))
                    MathFunctions.AddPct(ref DoneTotalMod, autoAttackDamage.GetAmount());
            }

            DoneTotalMod *= GetTotalAuraMultiplierByMiscMask(AuraType.ModDamageDoneVersus, creatureTypeMask);

            // bonus against aurastate
            DoneTotalMod *= GetTotalAuraMultiplier(AuraType.ModDamageDoneVersusAurastate, aurEff =>
            {
                if (victim.HasAuraState((AuraStateType)aurEff.GetMiscValue()))
                    return true;
                return false;
            });

            // Add SPELL_AURA_MOD_DAMAGE_DONE_FOR_MECHANIC percent bonus
            if (spellProto != null)
                MathFunctions.AddPct(ref DoneTotalMod, GetTotalAuraModifierByMiscValue(AuraType.ModDamageDoneForMechanic, (int)spellProto.Mechanic));

            float damageF = damage;

            // apply spellmod to Done damage
            if (spellProto != null)
            {
                Player modOwner = GetSpellModOwner();
                if (modOwner != null)
                    modOwner.ApplySpellMod(spellProto, damagetype == DamageEffectType.DOT ? SpellModOp.PeriodicHealingAndDamage : SpellModOp.HealingAndDamage, ref damageF);
            }

            damageF = (damageF + DoneFlatBenefit) * DoneTotalMod;

            // bonus result can be negative
            return (uint)Math.Max(damageF, 0.0f);
        }

        public uint MeleeDamageBonusTaken(Unit attacker, uint pdamage, WeaponAttackType attType, DamageEffectType damagetype, SpellInfo spellProto = null, SpellSchoolMask damageSchoolMask = SpellSchoolMask.Normal)
        {
            if (pdamage == 0)
                return 0;

            int TakenFlatBenefit = 0;

            // ..taken
            TakenFlatBenefit += GetTotalAuraModifierByMiscMask(AuraType.ModDamageTaken, (int)attacker.GetMeleeDamageSchoolMask());

            if (attType != WeaponAttackType.RangedAttack)
                TakenFlatBenefit += GetTotalAuraModifier(AuraType.ModMeleeDamageTaken);
            else
                TakenFlatBenefit += GetTotalAuraModifier(AuraType.ModRangedDamageTaken);

            // Taken total percent damage auras
            float TakenTotalMod = 1.0f;

            // ..taken
            TakenTotalMod *= GetTotalAuraMultiplierByMiscMask(AuraType.ModDamagePercentTaken, (uint)attacker.GetMeleeDamageSchoolMask());

            // .. taken pct (special attacks)
            if (spellProto != null)
            {
                // From caster spells
                TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModSchoolMaskDamageFromCaster, aurEff =>
                {
                    return aurEff.GetCasterGUID() == attacker.GetGUID() && (aurEff.GetMiscValue() & (int)spellProto.GetSchoolMask()) != 0;
                });

                TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModSpellDamageFromCaster, aurEff =>
                {
                    return aurEff.GetCasterGUID() == attacker.GetGUID() && aurEff.IsAffectingSpell(spellProto);
                });

                // Mod damage from spell mechanic
                uint mechanicMask = spellProto.GetAllEffectsMechanicMask();

                // Shred, Maul - "Effects which increase Bleed damage also increase Shred damage"
                if (spellProto.SpellFamilyName == SpellFamilyNames.Druid && spellProto.SpellFamilyFlags[0].HasAnyFlag(0x00008800u))
                    mechanicMask |= (1 << (int)Mechanics.Bleed);

                if (mechanicMask != 0)
                {
                    TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModMechanicDamageTakenPercent, aurEff =>
                    {
                        if ((mechanicMask & (1 << (aurEff.GetMiscValue()))) != 0)
                            return true;
                        return false;
                    });
                }

                if (damagetype == DamageEffectType.DOT)
                    TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModPeriodicDamageTaken, aurEff => (aurEff.GetMiscValue() & (uint)spellProto.GetSchoolMask()) != 0);
            }

            AuraEffect cheatDeath = GetAuraEffect(45182, 0);
            if (cheatDeath != null)
                MathFunctions.AddPct(ref TakenTotalMod, cheatDeath.GetAmount());

            if (attType != WeaponAttackType.RangedAttack)
                TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModMeleeDamageTakenPct);
            else
                TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModRangedDamageTakenPct);

            // Versatility
            Player modOwner = GetSpellModOwner();
            if (modOwner)
            {
                // only 50% of SPELL_AURA_MOD_VERSATILITY for damage reduction
                float versaBonus = modOwner.GetTotalAuraModifier(AuraType.ModVersatility) / 2.0f;
                MathFunctions.AddPct(ref TakenTotalMod, -(modOwner.GetRatingBonusValue(CombatRating.VersatilityDamageTaken) + versaBonus));
            }

            // Sanctified Wrath (bypass damage reduction)
            if (attacker != null && TakenTotalMod < 1.0f)
            {
                SpellSchoolMask attackSchoolMask = spellProto != null ? spellProto.GetSchoolMask() : damageSchoolMask;

                float damageReduction = 1.0f - TakenTotalMod;
                var casterIgnoreResist = attacker.GetAuraEffectsByType(AuraType.ModIgnoreTargetResist);
                foreach (AuraEffect aurEff in casterIgnoreResist)
                {
                    if ((aurEff.GetMiscValue() & (int)attackSchoolMask) == 0)
                        continue;

                    MathFunctions.AddPct(ref damageReduction, -aurEff.GetAmount());
                }

                TakenTotalMod = 1.0f - damageReduction;
            }

            float tmpDamage = (pdamage + TakenFlatBenefit) * TakenTotalMod;
            return (uint)Math.Max(tmpDamage, 0.0f);
        }

        bool IsBlockCritical()
        {
            if (RandomHelper.randChance(GetTotalAuraModifier(AuraType.ModBlockCritChance)))
                return true;
            return false;
        }

        public virtual SpellSchoolMask GetMeleeDamageSchoolMask(WeaponAttackType attackType = WeaponAttackType.BaseAttack) => SpellSchoolMask.None;

        float CalculateDefaultCoefficient(SpellInfo spellInfo, DamageEffectType damagetype)
        {
            // Damage over Time spells bonus calculation
            float DotFactor = 1.0f;
            if (damagetype == DamageEffectType.DOT)
            {

                int DotDuration = spellInfo.GetDuration();
                if (!spellInfo.IsChanneled() && DotDuration > 0)
                    DotFactor = DotDuration / 15000.0f;

                uint DotTicks = spellInfo.GetMaxTicks();
                if (DotTicks != 0)
                    DotFactor /= DotTicks;
            }

            uint CastingTime = (uint)(spellInfo.IsChanneled() ? spellInfo.GetDuration() : spellInfo.CalcCastTime());
            // Distribute Damage over multiple effects, reduce by AoE
            CastingTime = GetCastingTimeForBonus(spellInfo, damagetype, CastingTime);

            // As wowwiki says: C = (Cast Time / 3.5)
            return (CastingTime / 3500.0f) * DotFactor;
        }

        public virtual void UpdateDamageDoneMods(WeaponAttackType attackType)
        {
            UnitMods unitMod = attackType switch
            {
                WeaponAttackType.BaseAttack => UnitMods.DamageMainHand,
                WeaponAttackType.OffAttack => UnitMods.DamageOffHand,
                WeaponAttackType.RangedAttack => UnitMods.DamageRanged,
                _ => throw new NotImplementedException(),
            };

            float amount = GetTotalAuraModifier(AuraType.ModDamageDone, aurEff =>
            {
                if ((aurEff.GetMiscValue() & (int)SpellSchoolMask.Normal) == 0)
                    return false;

                return CheckAttackFitToAuraRequirement(attackType, aurEff);
            });

            SetStatFlatModifier(unitMod, UnitModifierFlatType.Total, amount);
        }

        public void UpdateAllDamageDoneMods()
        {
            for (var attackType = WeaponAttackType.BaseAttack; attackType < WeaponAttackType.Max; ++attackType)
                UpdateDamageDoneMods(attackType);
        }

        public void UpdateDamagePctDoneMods(WeaponAttackType attackType)
        {
            (UnitMods unitMod, float factor) = attackType switch
            {
                WeaponAttackType.BaseAttack => (UnitMods.DamageMainHand, 1.0f),
                WeaponAttackType.OffAttack => (UnitMods.DamageOffHand, 0.5f),
                WeaponAttackType.RangedAttack => (UnitMods.DamageRanged, 1.0f),
                _ => throw new NotImplementedException(),
            };

            factor *= GetTotalAuraMultiplier(AuraType.ModDamagePercentDone, aurEff =>
            {
                if (!aurEff.GetMiscValue().HasAnyFlag((int)SpellSchoolMask.Normal))
                    return false;

                return CheckAttackFitToAuraRequirement(attackType, aurEff);
            });

            if (attackType == WeaponAttackType.OffAttack)
                factor *= GetTotalAuraMultiplier(AuraType.ModOffhandDamagePct, auraEffect => CheckAttackFitToAuraRequirement(attackType, auraEffect));

            SetStatPctModifier(unitMod, UnitModifierPctType.Total, factor);
        }

        public void UpdateAllDamagePctDoneMods()
        {
            for (var attackType = WeaponAttackType.BaseAttack; attackType < WeaponAttackType.Max; ++attackType)
                UpdateDamagePctDoneMods(attackType);
        }

        public CombatManager GetCombatManager() => m_combatManager;

        // Exposes the threat manager directly - be careful when interfacing with this
        // As a general rule of thumb, any unit pointer MUST be null checked BEFORE passing it to threatmanager methods
        // threatmanager will NOT null check your pointers for you - misuse = crash
        public ThreatManager GetThreatManager() => m_threatManager;
    }
}
