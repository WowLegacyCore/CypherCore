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
using Game.AI;
using Game.Entities;
using Game.Scripting;
using Game.Spells;

namespace Scripts.Pets
{
    namespace Hunter
    {
        struct SpellIds
        {
            public const uint CripplingPoison = 30981;   // Viper
            public const uint DeadlyPoisonPassive = 34657;   // Venomous Snake
            public const uint MindNumbingPoison = 25810;    // Viper

        }

        struct CreatureIds
        {
            public const int Viper = 19921;
        }

        [Script]
        class npc_pet_hunter_snake_trap : ScriptedAI
        {
            public npc_pet_hunter_snake_trap(Creature creature) : base(creature) { }

            public override void JustEngagedWith(Unit who) { }

            public override void Reset()
            {
                _spellTimer = 0;

                CreatureTemplate Info = me.GetCreatureTemplate();

                _isViper = Info.Entry == CreatureIds.Viper ? true : false;

                me.SetMaxHealth((uint)(107 * (me.GetLevel() - 40) * 0.025f));
                // Add delta to make them not all hit the same time
                uint delta = (RandomHelper.Rand32() % 7) * 100;
                me.SetBaseAttackTime(WeaponAttackType.BaseAttack, Info.BaseAttackTime + delta);
                //me.SetStatFloatValue(UnitFields.RangedAttackPower, (float)Info.AttackPower);

                // Start attacking attacker of owner on first ai update after spawn - move in line of sight may choose better target
                if (!me.GetVictim() && me.IsSummon())
                {
                    Unit owner = me.ToTempSummon().GetSummoner();
                    if (owner)
                        if (owner.GetAttackerForHelper())
                            AttackStart(owner.GetAttackerForHelper());
                }

                if (!_isViper)
                    DoCast(me, SpellIds.DeadlyPoisonPassive, new CastSpellExtraArgs(true));
            }

            // Redefined for random target selection:
            public override void MoveInLineOfSight(Unit who)
            {
                if (!me.GetVictim() && me.CanCreatureAttack(who))
                {
                    if (me.GetDistanceZ(who) > SharedConst.CreatureAttackRangeZ)
                        return;

                    float attackRadius = me.GetAttackDistance(who);
                    if (me.IsWithinDistInMap(who, attackRadius) && me.IsWithinLOSInMap(who))
                    {
                        if ((RandomHelper.Rand32() % 5) == 0)
                        {
                            me.SetAttackTimer(WeaponAttackType.BaseAttack, (RandomHelper.Rand32() % 10) * 100);
                            _spellTimer = (RandomHelper.Rand32() % 10) * 100;
                            AttackStart(who);
                        }
                    }
                }
            }

            public override void UpdateAI(uint diff)
            {
                if (!UpdateVictim() || !me.GetVictim())
                    return;

                if (me.GetVictim().HasBreakableByDamageCrowdControlAura(me))
                {
                    me.InterruptNonMeleeSpells(false);
                    return;
                }

                //Viper
                if (_isViper)
                {
                    if (_spellTimer <= diff)
                    {
                        if (RandomHelper.IRand(0, 2) == 0) //33% chance to cast
                            DoCastVictim(RandomHelper.RAND(SpellIds.MindNumbingPoison, SpellIds.CripplingPoison));

                        _spellTimer = 3000;
                    }
                    else
                        _spellTimer -= diff;
                }

                DoMeleeAttackIfReady();
            }

            bool _isViper;
            uint _spellTimer;
        }
    }
}
