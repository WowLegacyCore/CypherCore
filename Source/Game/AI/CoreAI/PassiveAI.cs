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
using Game.Spells;
using System.Collections.Generic;

namespace Game.AI
{
    public class PassiveAI : CreatureAI
    {
        public PassiveAI(Creature c) : base(c)
        {
            me.SetReactState(ReactStates.Passive);
        }

        public override void UpdateAI(uint diff)
        {
            if (me.IsInCombat() && me.GetAttackers().Empty())
                EnterEvadeMode(EvadeReason.NoHostiles);
        }

        public override void AttackStart(Unit victim) { }

        public override void MoveInLineOfSight(Unit who) { }
    }

    public class PossessedAI : CreatureAI
    {
        public PossessedAI(Creature c) : base(c)
        {
            me.SetReactState(ReactStates.Passive);
        }

        public override void AttackStart(Unit target)
        {
            me.Attack(target, true);
        }

        public override void UpdateAI(uint diff)
        {
            if (me.GetVictim() != null)
            {
                if (!me.IsValidAttackTarget(me.GetVictim()))
                    me.AttackStop();
                else
                    DoMeleeAttackIfReady();
            }
        }

        public override void JustDied(Unit unit)
        {
            // We died while possessed, disable our loot
            me.RemoveDynamicFlag(UnitDynFlags.Lootable);
        }

        public override void KilledUnit(Unit victim)
        {
            // We killed a creature, disable victim's loot
            if (victim.IsTypeId(TypeId.Unit))
                me.RemoveDynamicFlag(UnitDynFlags.Lootable);
        }

        public override void OnCharmed(bool apply)
        {
            me.NeedChangeAI = true;
            me.IsAIEnabled = false;
        }

        public override void MoveInLineOfSight(Unit who) { }

        public override void EnterEvadeMode(EvadeReason why) { }
    }

    public class NullCreatureAI : CreatureAI
    {
        public NullCreatureAI(Creature creature) : base(creature)
        {
            me.SetReactState(ReactStates.Passive);
        }

        public override void MoveInLineOfSight(Unit unit) { }
        public override void AttackStart(Unit unit) { }
        public override void UpdateAI(uint diff) { }
        public override void EnterEvadeMode(EvadeReason why) { }
        public override void OnCharmed(bool apply) { }
    }

    public class CritterAI : PassiveAI
    {
        public CritterAI(Creature c) : base(c)
        {
            me.SetReactState(ReactStates.Passive);
        }

        public override void DamageTaken(Unit done_by, ref uint damage)
        {
            if (!me.HasUnitState(UnitState.Fleeing))
                me.SetControlled(true, UnitState.Fleeing);
        }

        public override void EnterEvadeMode(EvadeReason why)
        {
            if (me.HasUnitState(UnitState.Fleeing))
                me.SetControlled(false, UnitState.Fleeing);
            base.EnterEvadeMode(why);
        }
    }

    public class TriggerAI : NullCreatureAI
    {
        public TriggerAI(Creature c) : base(c) { }

        public override void IsSummonedBy(Unit summoner)
        {
            if (me.m_spells[0] != 0)
            {
                CastSpellExtraArgs extra = new();
                extra.OriginalCaster = summoner.GetGUID();
                me.CastSpell(me, me.m_spells[0], extra);
            }
        }
    }
}
