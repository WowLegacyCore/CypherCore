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
using Game.Entities;
using System.Collections.Generic;

namespace Game.AI
{
    public class CombatAI : CreatureAI
    {
        public List<uint> Spells = new();

        public CombatAI(Creature c) : base(c) { }

        public override void InitializeAI()
        {
            for (var i = 0; i < SharedConst.MaxCreatureSpells; ++i)
                if (me.m_spells[i] != 0 && Global.SpellMgr.HasSpellInfo(me.m_spells[i], me.GetMap().GetDifficultyID()))
                    Spells.Add(me.m_spells[i]);

            base.InitializeAI();
        }

        public override void Reset()
        {
            _events.Reset();
        }

        public override void JustDied(Unit killer)
        {
            foreach (var id in Spells)
            {
                AISpellInfoType info = GetAISpellInfo(id, me.GetMap().GetDifficultyID());
                if (info != null && info.condition == AICondition.Die)
                    me.CastSpell(killer, id, true);
            }
        }

        public override void JustEngagedWith(Unit victim)
        {
            foreach (var id in Spells)
            {
                AISpellInfoType info = GetAISpellInfo(id, me.GetMap().GetDifficultyID());
                if (info != null)
                {
                    if (info.condition == AICondition.Aggro)
                        me.CastSpell(victim, id, false);
                    else if (info.condition == AICondition.Combat)
                        _events.ScheduleEvent(id, info.cooldown + RandomHelper.Rand32() % info.cooldown);
                }
            }
        }

        public override void UpdateAI(uint diff)
        {
            if (!UpdateVictim())
                return;

            _events.Update(diff);

            if (me.HasUnitState(UnitState.Casting))
                return;

            uint spellId = _events.ExecuteEvent();
            if (spellId != 0)
            {
                DoCast(spellId);
                AISpellInfoType info = GetAISpellInfo(spellId, me.GetMap().GetDifficultyID());
                if (info != null)
                    _events.ScheduleEvent(spellId, info.cooldown + RandomHelper.Rand32() % info.cooldown);
            }
            else
                DoMeleeAttackIfReady();
        }

        public override void SpellInterrupted(uint spellId, uint unTimeMs)
        {
            _events.RescheduleEvent(spellId, unTimeMs);
        }
    }

    public class AggressorAI : CreatureAI
    {
        public AggressorAI(Creature c) : base(c) { }

        public override void UpdateAI(uint diff)
        {
            if (!UpdateVictim())
                return;

            DoMeleeAttackIfReady();
        }
    }

    public class CasterAI : CombatAI
    {
        float _attackDist;

        public CasterAI(Creature c)
            : base(c)
        {
            _attackDist = SharedConst.MeleeRange;
        }

        public override void InitializeAI()
        {
            base.InitializeAI();

            _attackDist = 30.0f;
            foreach (var id in Spells)
            {
                AISpellInfoType info = GetAISpellInfo(id, me.GetMap().GetDifficultyID());
                if (info != null && info.condition == AICondition.Combat && _attackDist > info.maxRange)
                    _attackDist = info.maxRange;
            }

            if (_attackDist == 30.0f)
                _attackDist = SharedConst.MeleeRange;
        }

        public override void AttackStart(Unit victim)
        {
            AttackStartCaster(victim, _attackDist);
        }

        public override void JustEngagedWith(Unit victim)
        {
            if (Spells.Empty())
                return;

            int spell = (int)(RandomHelper.Rand32() % Spells.Count);
            uint count = 0;
            foreach (var id in Spells)
            {
                AISpellInfoType info = GetAISpellInfo(id, me.GetMap().GetDifficultyID());
                if (info != null)
                {
                    if (info.condition == AICondition.Aggro)
                        me.CastSpell(victim, id, false);
                    else if (info.condition == AICondition.Combat)
                    {
                        uint cooldown = info.realCooldown;
                        if (count == spell)
                        {
                            DoCast(Spells[spell]);
                            cooldown += (uint)me.GetCurrentSpellCastTime(id);
                        }
                        _events.ScheduleEvent(id, cooldown);
                    }
                }
            }
        }

        public override void UpdateAI(uint diff)
        {
            if (!UpdateVictim())
                return;

            _events.Update(diff);

            if (me.GetVictim().HasBreakableByDamageCrowdControlAura(me))
            {
                me.InterruptNonMeleeSpells(false);
                return;
            }

            if (me.HasUnitState(UnitState.Casting))
                return;

            uint spellId = _events.ExecuteEvent();
            if (spellId != 0)
            {
                DoCast(spellId);
                uint casttime = (uint)me.GetCurrentSpellCastTime(spellId);
                AISpellInfoType info = GetAISpellInfo(spellId, me.GetMap().GetDifficultyID());
                if (info != null)
                    _events.ScheduleEvent(spellId, (casttime != 0 ? casttime : 500) + info.realCooldown);
            }
        }
    }

    public class ArcherAI : CreatureAI
    {
        float _minRange;

        public ArcherAI(Creature c) : base(c)
        {
            if (me.m_spells[0] == 0)
                Log.outError(LogFilter.ScriptsAi, "ArcherAI set for creature (entry = {0}) with spell1=0. AI will do nothing", me.GetEntry());

            var spellInfo = Global.SpellMgr.GetSpellInfo(me.m_spells[0], me.GetMap().GetDifficultyID());
            _minRange = spellInfo != null ? spellInfo.GetMinRange(false) : 0;

            if (_minRange == 0)
                _minRange = SharedConst.MeleeRange;
            me.m_CombatDistance = spellInfo != null ? spellInfo.GetMaxRange(false) : 0;
            me.m_SightDistance = me.m_CombatDistance;
        }

        public override void AttackStart(Unit who)
        {
            if (who == null)
                return;

            if (me.IsWithinCombatRange(who, _minRange))
            {
                if (me.Attack(who, true) && !who.IsFlying())
                    me.GetMotionMaster().MoveChase(who);
            }
            else
            {
                if (me.Attack(who, false) && !who.IsFlying())
                    me.GetMotionMaster().MoveChase(who, me.m_CombatDistance);
            }

            if (who.IsFlying())
                me.GetMotionMaster().MoveIdle();
        }

        public override void UpdateAI(uint diff)
        {
            if (!UpdateVictim())
                return;

            if (!me.IsWithinCombatRange(me.GetVictim(), _minRange))
                DoSpellAttackIfReady(me.m_spells[0]);
            else
                DoMeleeAttackIfReady();
        }
    }

    public class TurretAI : CreatureAI
    {
        float _minRange;

        public TurretAI(Creature c)
            : base(c)
        {
            if (me.m_spells[0] == 0)
                Log.outError(LogFilter.Server, "TurretAI set for creature (entry = {0}) with spell1=0. AI will do nothing", me.GetEntry());

            var spellInfo = Global.SpellMgr.GetSpellInfo(me.m_spells[0], me.GetMap().GetDifficultyID());
            _minRange = spellInfo != null ? spellInfo.GetMinRange(false) : 0;
            me.m_CombatDistance = spellInfo != null ? spellInfo.GetMaxRange(false) : 0;
            me.m_SightDistance = me.m_CombatDistance;
        }

        public override bool CanAIAttack(Unit victim)
        {
            // todo use one function to replace it
            if (!me.IsWithinCombatRange(victim, me.m_CombatDistance)
                || (_minRange != 0 && me.IsWithinCombatRange(victim, _minRange)))
                return false;
            return true;
        }

        public override void AttackStart(Unit victim)
        {
            if (victim != null)
                me.Attack(victim, false);
        }

        public override void UpdateAI(uint diff)
        {
            if (!UpdateVictim())
                return;

            DoSpellAttackIfReady(me.m_spells[0]);
        }
    }

    public class VehicleAI : CreatureAI
    {
        const int VEHICLE_CONDITION_CHECK_TIME = 1000;
        const int VEHICLE_DISMISS_TIME = 5000;

        bool _hasConditions;
        uint _conditionsTimer;
        bool _doDismiss;
        uint _dismissTimer;

        public VehicleAI(Creature creature) : base(creature)
        {
            _conditionsTimer = VEHICLE_CONDITION_CHECK_TIME;
            LoadConditions();
            _doDismiss = false;
            _dismissTimer = VEHICLE_DISMISS_TIME;
        }

        public override void UpdateAI(uint diff)
        {
            CheckConditions(diff);

            if (_doDismiss)
            {
                if (_dismissTimer < diff)
                {
                    _doDismiss = false;
                    me.DespawnOrUnsummon();
                }
                else
                    _dismissTimer -= diff;
            }
        }

        public override void MoveInLineOfSight(Unit who) { }

        public override void AttackStart(Unit victim) { }

        public override void OnCharmed(bool apply)
        {
            if (!me.GetVehicleKit().IsVehicleInUse() && !apply && _hasConditions)//was used and has conditions
                _doDismiss = true;//needs reset
            else if (apply)
                _doDismiss = false;//in use again

            _dismissTimer = VEHICLE_DISMISS_TIME;//reset timer
        }

        void LoadConditions()
        {
            _hasConditions = Global.ConditionMgr.HasConditionsForNotGroupedEntry(ConditionSourceType.CreatureTemplateVehicle, me.GetEntry());
        }

        void CheckConditions(uint diff)
        {
            if (!_hasConditions)
                return;

            if (_conditionsTimer <= diff)
            {
                Vehicle vehicleKit = me.GetVehicleKit();
                if (vehicleKit)
                {
                    foreach (var pair in vehicleKit.Seats)
                    {
                        Unit passenger = Global.ObjAccessor.GetUnit(me, pair.Value.Passenger.Guid);
                        if (passenger)
                        {
                            Player player = passenger.ToPlayer();
                            if (player)
                            {
                                if (!Global.ConditionMgr.IsObjectMeetingNotGroupedConditions(ConditionSourceType.CreatureTemplateVehicle, me.GetEntry(), player, me))
                                {
                                    player.ExitVehicle();
                                    return;//check other pessanger in next tick
                                }
                            }
                        }
                    }
                }

                _conditionsTimer = VEHICLE_CONDITION_CHECK_TIME;
            }
            else
                _conditionsTimer -= diff;
        }
    }

    public class ReactorAI : CreatureAI
    {
        public ReactorAI(Creature c) : base(c) { }

        public override void MoveInLineOfSight(Unit who) { }

        public override void UpdateAI(uint diff)
        {
            if (!UpdateVictim())
                return;

            DoMeleeAttackIfReady();
        }
    }
}
