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
using Game.BattleFields;
using Game.BattleGrounds;
using Game.Combat;
using Game.DataStorage;
using Game.Loots;
using Game.Maps;
using Game.Networking.Packets;
using Game.PvP;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entities
{
    public partial class Unit
    {
        public virtual void AtEnterCombat()
        {
            foreach (var pair in GetAppliedAuras())
                pair.Value.GetBase().CallScriptEnterLeaveCombatHandlers(pair.Value, true);

            Spell spell = GetCurrentSpell(CurrentSpellTypes.Generic);
            if (spell != null)
                if (spell.GetState() == SpellState.Preparing
                    && spell.m_spellInfo.HasAttribute(SpellAttr0.CantUsedInCombat)
                    && spell.m_spellInfo.InterruptFlags.HasFlag(SpellInterruptFlags.Combat))
                    InterruptNonMeleeSpells(false);

            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.EnteringCombat);
            ProcSkillsAndAuras(this, null, ProcFlags.EnterCombat, ProcFlags.None, ProcFlagsSpellType.MaskAll, ProcFlagsSpellPhase.None, ProcFlagsHit.None, null, null, null);
        }

        public virtual void AtExitCombat()
        {
            foreach (var pair in GetAppliedAuras())
                pair.Value.GetBase().CallScriptEnterLeaveCombatHandlers(pair.Value, false);

            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.LeavingCombat);
        }

        public void CombatStop(bool includingCast = false, bool mutualPvP = true)
        {
            if (includingCast && IsNonMeleeSpellCast(false))
                InterruptNonMeleeSpells(false);

            AttackStop();
            RemoveAllAttackers();
            if (IsTypeId(TypeId.Player))
                ToPlayer().SendAttackSwingCancelAttack();     // melee and ranged forced attack cancel

            if (mutualPvP)
                ClearInCombat();
            else
            { // vanish and brethren are weird
                m_combatManager.EndAllPvECombat();
                m_combatManager.SuppressPvPCombat();
            }
        }

        public void CombatStopWithPets(bool includingCast = false)
        {
            CombatStop(includingCast);

            foreach (var minion in m_Controlled)
                minion.CombatStop(includingCast);
        }

        public bool IsInCombat() => HasUnitFlag(UnitFlags.InCombat);

        public bool IsInCombatWith(Unit who) => who != null && m_combatManager.IsInCombatWith(who);

        public bool IsPetInCombat() => HasUnitFlag(UnitFlags.PetInCombat);

        public void SetInCombatWith(Unit enemy)
        {
            if (enemy != null)
                m_combatManager.SetInCombatWith(enemy);
        }

        public void EngageWithTarget(Unit enemy)
        {
            if (enemy == null)
                return;

            if (IsEngagedBy(enemy))
                return;

            if (CanHaveThreatList())
                m_threatManager.AddThreat(enemy, 0.0f, null, true, true);
            else
                SetInCombatWith(enemy);

            Creature creature = ToCreature();
            if (creature != null)
            {
                CreatureGroup formation = creature.GetFormation();
                if (formation != null)
                    formation.MemberEngagingTarget(creature, enemy);
            }
        }

        public void ClearInCombat() => m_combatManager.EndAllCombat();

        public void ClearInPetCombat()
        {
            RemoveUnitFlag(UnitFlags.PetInCombat);
            Unit owner = GetOwner();
            if (owner != null)
                owner.RemoveUnitFlag(UnitFlags.PetInCombat);
        }

        public void RemoveAllAttackers()
        {
            while (!attackerList.Empty())
            {
                var iter = attackerList.First();
                if (!iter.AttackStop())
                {
                    Log.outError(LogFilter.Unit, "WORLD: Unit has an attacker that isn't attacking it!");
                    attackerList.Remove(iter);
                }
            }
        }

        public virtual void OnCombatExit()
        {
            foreach (var pair in GetAppliedAuras())
            {
                AuraApplication aurApp = pair.Value;
                aurApp.GetBase().CallScriptEnterLeaveCombatHandlers(aurApp, false);
            }
        }

        public bool CanHaveThreatList() => m_threatManager.CanHaveThreatList();

        void SendThreatListUpdate() => m_threatManager.SendThreatListToClients();

        // For NPCs with threat list: Whether there are any enemies on our threat list
        // For other units: Whether we're in combat
        // This value is different from IsInCombat when a projectile spell is midair (combat on launch - threat+aggro on impact)
        public bool IsEngaged() => CanHaveThreatList() ? m_threatManager.IsEngaged() : IsInCombat();

        public bool IsEngagedBy(Unit who) => CanHaveThreatList() ? IsThreatenedBy(who) : IsInCombatWith(who);

        public bool IsThreatenedBy(Unit who) => who != null && m_threatManager.IsThreatenedBy(who, true);

        public bool IsTargetableForAttack(bool checkFakeDeath = true)
        {
            if (!IsAlive())
                return false;

            if (HasUnitFlag(UnitFlags.NonAttackable | UnitFlags.NotSelectable))
                return false;

            if (IsTypeId(TypeId.Player) && ToPlayer().IsGameMaster())
                return false;

            return !HasUnitState(UnitState.Unattackable) && (!checkFakeDeath || !HasUnitState(UnitState.Died));
        }

        public void ValidateAttackersAndOwnTarget()
        {
            // iterate attackers
            List<Unit> toRemove = new();
            foreach (Unit attacker in GetAttackers())
                if (!attacker.IsValidAttackTarget(this))
                    toRemove.Add(attacker);

            foreach (Unit attacker in toRemove)
                attacker.AttackStop();

            // remove our own victim
            Unit victim = GetVictim();
            if (victim != null)
                if (!IsValidAttackTarget(victim))
                    AttackStop();
        }

        public void StopAttackFaction(uint factionId)
        {
            Unit victim = GetVictim();
            if (victim != null)
            {
                if (victim.GetFactionTemplateEntry().Faction == factionId)
                {
                    AttackStop();
                    if (IsNonMeleeSpellCast(false))
                        InterruptNonMeleeSpells(false);

                    // melee and ranged forced attack cancel
                    if (IsTypeId(TypeId.Player))
                        ToPlayer().SendAttackSwingCancelAttack();
                }
            }

            var attackers = GetAttackers();
            for (var i = 0; i < attackers.Count;)
            {
                var unit = attackers[i];
                if (unit.GetFactionTemplateEntry().Faction == factionId)
                {
                    unit.AttackStop();
                    i = 0;
                }
                else
                    ++i;
            }

            List<CombatReference> refsToEnd = new();
            foreach (var pair in m_combatManager.GetPvECombatRefs())
                if (pair.Value.GetOther(this).GetFactionTemplateEntry().Faction == factionId)
                    refsToEnd.Add(pair.Value);

            foreach (CombatReference refe in refsToEnd)
                refe.EndCombat();

            foreach (var minion in m_Controlled)
                minion.StopAttackFaction(factionId);
        }


        public void HandleProcExtraAttackFor(Unit victim)
        {
            while (ExtraAttacks != 0)
            {
                AttackerStateUpdate(victim, WeaponAttackType.BaseAttack, true);
                --ExtraAttacks;
            }
        }

        public bool Attack(Unit victim, bool meleeAttack)
        {
            if (victim == null || victim.GetGUID() == GetGUID())
                return false;

            // dead units can neither attack nor be attacked
            if (!IsAlive() || !victim.IsInWorld || !victim.IsAlive())
                return false;

            // player cannot attack in mount state
            if (IsTypeId(TypeId.Player) && IsMounted())
                return false;

            Creature creature = ToCreature();
            // creatures cannot attack while evading
            if (creature != null && creature.IsInEvadeMode())
                return false;

            if (HasUnitFlag(UnitFlags.Pacified))
                return false;

            if (HasAuraType(AuraType.DisableAttackingExceptAbilities))
                return false;

            // nobody can attack GM in GM-mode
            if (victim.IsTypeId(TypeId.Player))
            {
                if (victim.ToPlayer().IsGameMaster())
                    return false;
            }
            else
            {
                if (victim.ToCreature().IsEvadingAttacks())
                    return false;
            }

            // remove SPELL_AURA_MOD_UNATTACKABLE at attack (in case non-interruptible spells stun aura applied also that not let attack)
            if (HasAuraType(AuraType.ModUnattackable))
                RemoveAurasByType(AuraType.ModUnattackable);

            if (attacking != null)
            {
                if (attacking == victim)
                {
                    // switch to melee attack from ranged/magic
                    if (meleeAttack)
                    {
                        if (!HasUnitState(UnitState.MeleeAttacking))
                        {
                            AddUnitState(UnitState.MeleeAttacking);
                            SendMeleeAttackStart(victim);
                            return true;
                        }
                    }
                    else if (HasUnitState(UnitState.MeleeAttacking))
                    {
                        ClearUnitState(UnitState.MeleeAttacking);
                        SendMeleeAttackStop(victim);
                        return true;
                    }
                    return false;
                }

                // switch target
                InterruptSpell(CurrentSpellTypes.Melee);
                if (!meleeAttack)
                    ClearUnitState(UnitState.MeleeAttacking);
            }

            if (attacking != null)
                attacking._removeAttacker(this);

            attacking = victim;
            attacking._addAttacker(this);

            // Set our target
            SetTarget(victim.GetGUID());

            if (meleeAttack)
                AddUnitState(UnitState.MeleeAttacking);

            if (creature != null && !IsPet())
            {
                EngageWithTarget(victim); // ensure that anything we're attacking has threat

                creature.SendAIReaction(AiReaction.Hostile);
                creature.CallAssistance();

                // Remove emote state - will be restored on creature reset
                SetEmoteState(Emote.OneshotNone);
            }

            // delay offhand weapon attack by 50% of the base attack time
            if (HaveOffhandWeapon() && GetTypeId() != TypeId.Player)
                SetAttackTimer(WeaponAttackType.OffAttack, Math.Max(GetAttackTimer(WeaponAttackType.OffAttack), GetAttackTimer(WeaponAttackType.BaseAttack) + MathFunctions.CalculatePct(GetBaseAttackTime(WeaponAttackType.BaseAttack), 50)));

            if (meleeAttack)
                SendMeleeAttackStart(victim);

            // Let the pet know we've started attacking someting. Handles melee attacks only
            // Spells such as auto-shot and others handled in WorldSession.HandleCastSpellOpcode
            if (IsTypeId(TypeId.Player))
            {
                foreach (Unit controlled in m_Controlled)
                {
                    Creature cControlled = controlled.ToCreature();
                    if (cControlled != null)
                        if (cControlled.IsAIEnabled)
                            cControlled.GetAI().OwnerAttacked(victim);
                }
            }
            return true;
        }

        public void SendMeleeAttackStart(Unit victim)
        {
            AttackStart packet = new();
            packet.Attacker = GetGUID();
            packet.Victim = victim.GetGUID();
            SendMessageToSet(packet, true);
        }

        public void SendMeleeAttackStop(Unit victim = null)
        {
            SendMessageToSet(new SAttackStop(this, victim), true);

            if (victim)
                Log.outInfo(LogFilter.Unit, "{0} {1} stopped attacking {2} {3}", (IsTypeId(TypeId.Player) ? "Player" : "Creature"), GetGUID().ToString(),
                    (victim.IsTypeId(TypeId.Player) ? "player" : "creature"), victim.GetGUID().ToString());
            else
                Log.outInfo(LogFilter.Unit, "{0} {1} stopped attacking", (IsTypeId(TypeId.Player) ? "Player" : "Creature"), GetGUID().ToString());
        }

        public ObjectGuid GetTarget() => GetUpdateField<ObjectGuid>(UnitFields.Target);

        public virtual void SetTarget(ObjectGuid guid) { }

        public bool AttackStop()
        {
            if (attacking == null)
                return false;

            Unit victim = attacking;

            attacking._removeAttacker(this);
            attacking = null;

            // Clear our target
            SetTarget(ObjectGuid.Empty);

            ClearUnitState(UnitState.MeleeAttacking);

            InterruptSpell(CurrentSpellTypes.Melee);

            // reset only at real combat stop
            Creature creature = ToCreature();
            if (creature != null)
            {
                creature.SetNoCallAssistance(false);

                if (creature.HasSearchedAssistance())
                {
                    creature.SetNoSearchAssistance(false);
                    UpdateSpeed(UnitMoveType.Run);
                }
            }

            SendMeleeAttackStop(victim);
            return true;
        }

        void _addAttacker(Unit pAttacker) => attackerList.Add(pAttacker);
        void _removeAttacker(Unit pAttacker) => attackerList.Remove(pAttacker);

        public Unit GetVictim() => attacking;

        public Unit GetAttackerForHelper()
        {
            if (!IsEngaged())
                return null;

            Unit victim = GetVictim();
            if (victim != null)
                if ((!IsPet() && GetPlayerMovingMe() == null) || IsInCombatWith(victim))
                    return victim;

            CombatManager mgr = GetCombatManager();
            // pick arbitrary targets; our pvp combat > owner's pvp combat > our pve combat > owner's pve combat
            Unit owner = GetCharmerOrOwner();
            if (mgr.HasPvPCombat())
                return mgr.GetPvPCombatRefs().First().Value.GetOther(this);

            if (owner && (owner.GetCombatManager().HasPvPCombat()))
                return owner.GetCombatManager().GetPvPCombatRefs().First().Value.GetOther(owner);

            if (mgr.HasPvECombat())
                return mgr.GetPvECombatRefs().First().Value.GetOther(this);

            if (owner && (owner.GetCombatManager().HasPvECombat()))
                return owner.GetCombatManager().GetPvECombatRefs().First().Value.GetOther(owner);

            return null;
        }

        public List<Unit> GetAttackers() => attackerList;

        public override float GetCombatReach() => GetUpdateField<float>(UnitFields.CombatReach);

        public void SetCombatReach(float combatReach) => SetUpdateField<float>(UnitFields.CombatReach, combatReach);

        public float GetBoundingRadius() => GetUpdateField<float>(UnitFields.BoundingRadius);

        public void SetBoundingRadius(float boundingRadius) => SetUpdateField<float>(UnitFields.BoundingRadius, boundingRadius);

        public void ResetAttackTimer(WeaponAttackType type = WeaponAttackType.BaseAttack) => m_attackTimer[(int)type] = (uint)(GetBaseAttackTime(type) * m_modAttackSpeedPct[(int)type]);

        public void SetAttackTimer(WeaponAttackType type, uint time) => m_attackTimer[(int)type] = time;

        public uint GetAttackTimer(WeaponAttackType type) => m_attackTimer[(int)type];

        public bool IsAttackReady(WeaponAttackType type = WeaponAttackType.BaseAttack) => m_attackTimer[(int)type] == 0;

        public uint GetBaseAttackTime(WeaponAttackType att) => m_baseAttackSpeed[(int)att];

        public void AttackerStateUpdate(Unit victim, WeaponAttackType attType = WeaponAttackType.BaseAttack, bool extra = false)
        {
            if (HasUnitState(UnitState.CannotAutoattack) || HasUnitFlag(UnitFlags.Pacified))
                return;

            if (!victim.IsAlive())
                return;

            if ((attType == WeaponAttackType.BaseAttack || attType == WeaponAttackType.OffAttack) && !IsWithinLOSInMap(victim))
                return;

            AttackedTarget(victim, true);
            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.Attacking);

            // ignore ranged case
            if (attType != WeaponAttackType.BaseAttack && attType != WeaponAttackType.OffAttack)
                return;

            if (IsTypeId(TypeId.Unit) && !HasUnitFlag(UnitFlags.PlayerControlled) && !HasUnitFlag2(UnitFlags2.DisableTurn))
                SetFacingToObject(victim, false); // update client side facing to face the target (prevents visual glitches when casting untargeted spells)

            // melee attack spell casted at main hand attack only - no normal melee dmg dealt
            if (attType == WeaponAttackType.BaseAttack && GetCurrentSpell(CurrentSpellTypes.Melee) != null && !extra)
                m_currentSpells[CurrentSpellTypes.Melee].Cast();
            else
            {
                // attack can be redirected to another target
                victim = GetMeleeHitRedirectTarget(victim);

                var meleeAttackOverrides = GetAuraEffectsByType(AuraType.OverrideAutoattackWithMeleeSpell);
                AuraEffect meleeAttackAuraEffect = null;
                uint meleeAttackSpellId = 0;
                if (attType == WeaponAttackType.BaseAttack)
                {
                    if (!meleeAttackOverrides.Empty())
                    {
                        meleeAttackAuraEffect = meleeAttackOverrides.First();
                        meleeAttackSpellId = meleeAttackAuraEffect.GetSpellEffectInfo().TriggerSpell;
                    }
                }
                else
                {
                    var auraEffect = meleeAttackOverrides.Find(aurEff =>
                    {
                        return aurEff.GetSpellEffectInfo().MiscValue != 0;
                    });

                    if (auraEffect != null)
                    {
                        meleeAttackAuraEffect = auraEffect;
                        meleeAttackSpellId = (uint)meleeAttackAuraEffect.GetSpellEffectInfo().MiscValue;
                    }
                }

                if (meleeAttackAuraEffect == null)
                {
                    CalculateMeleeDamage(victim, out CalcDamageInfo damageInfo, attType);
                    // Send log damage message to client
                    DealDamageMods(damageInfo.Attacker, victim, ref damageInfo.Damage, ref damageInfo.Absorb);
                    SendAttackStateUpdate(damageInfo);

                    DealMeleeDamage(damageInfo, true);

                    DamageInfo dmgInfo = new(damageInfo);
                    ProcSkillsAndAuras(damageInfo.Attacker, damageInfo.Target, damageInfo.ProcAttacker, damageInfo.ProcVictim, ProcFlagsSpellType.None, ProcFlagsSpellPhase.None, dmgInfo.GetHitMask(), null, dmgInfo, null);
                    Log.outDebug(LogFilter.Unit, "AttackerStateUpdate: {0} attacked {1} for {2} dmg, absorbed {3}, blocked {4}, resisted {5}.",
                        GetGUID().ToString(), victim.GetGUID().ToString(), damageInfo.Damage, damageInfo.Absorb, damageInfo.Blocked, damageInfo.Resist);
                }
                else
                {
                    CastSpellExtraArgs args = new(TriggerCastFlags.FullMask);
                    args.TriggeringAura = meleeAttackAuraEffect;
                    CastSpell(victim, meleeAttackSpellId, args);

                    HitInfo hitInfo = HitInfo.AffectsVictim | HitInfo.NoAnimation;
                    if (attType == WeaponAttackType.OffAttack)
                        hitInfo |= HitInfo.OffHand;

                    SendAttackStateUpdate(hitInfo, victim, GetMeleeDamageSchoolMask(), 0, 0, 0, VictimState.Hit, 0);
                }
            }
        }

        public void FakeAttackerStateUpdate(Unit victim, WeaponAttackType attType = WeaponAttackType.BaseAttack)
        {
            if (HasUnitState(UnitState.CannotAutoattack) || HasUnitFlag(UnitFlags.Pacified))
                return;

            if (!victim.IsAlive())
                return;

            if ((attType == WeaponAttackType.BaseAttack || attType == WeaponAttackType.OffAttack) && !IsWithinLOSInMap(victim))
                return;

            AttackedTarget(victim, true);
            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.Attacking);

            if (attType != WeaponAttackType.BaseAttack && attType != WeaponAttackType.OffAttack)
                return;                                             // ignore ranged case

            if (IsTypeId(TypeId.Unit) && !HasUnitFlag(UnitFlags.PlayerControlled) && !HasUnitFlag2(UnitFlags2.DisableTurn))
                SetFacingToObject(victim, false); // update client side facing to face the target (prevents visual glitches when casting untargeted spells)

            CalcDamageInfo damageInfo = new();
            damageInfo.Attacker = this;
            damageInfo.Target = victim;

            damageInfo.DamageSchoolMask = (uint)GetMeleeDamageSchoolMask();
            damageInfo.OriginalDamage = 0;
            damageInfo.Damage = 0;
            damageInfo.Absorb = 0;
            damageInfo.Resist = 0;

            damageInfo.AttackType = attType;
            damageInfo.CleanDamage = 0;
            damageInfo.Blocked = 0;

            damageInfo.TargetState = VictimState.Hit;
            damageInfo.HitInfo = HitInfo.AffectsVictim | HitInfo.NormalSwing | HitInfo.FakeDamage;
            if (attType == WeaponAttackType.OffAttack)
                damageInfo.HitInfo |= HitInfo.OffHand;

            damageInfo.ProcAttacker = ProcFlags.None;
            damageInfo.ProcVictim = ProcFlags.None;
            damageInfo.HitOutCome = MeleeHitOutcome.Normal;

            SendAttackStateUpdate(damageInfo);
        }

        public void SetBaseWeaponDamage(WeaponAttackType attType, WeaponDamageRange damageRange, float value) => m_weaponDamage[(int)attType][(int)damageRange] = value;

        public Unit GetMagicHitRedirectTarget(Unit victim, SpellInfo spellInfo)
        {
            // Patch 1.2 notes: Spell Reflection no longer reflects abilities
            if (spellInfo.HasAttribute(SpellAttr0.Ability) || spellInfo.HasAttribute(SpellAttr1.CantBeRedirected) || spellInfo.HasAttribute(SpellAttr0.UnaffectedByInvulnerability))
                return victim;

            var magnetAuras = victim.GetAuraEffectsByType(AuraType.SpellMagnet);
            foreach (var eff in magnetAuras)
            {
                Unit magnet = eff.GetBase().GetCaster();
                if (magnet != null)
                {
                    if (spellInfo.CheckExplicitTarget(this, magnet) == SpellCastResult.SpellCastOk && _IsValidAttackTarget(magnet, spellInfo))
                    {
                        // @todo handle this charge drop by proc in cast phase on explicit target
                        if (spellInfo.HasHitDelay())
                        {
                            // Set up missile speed based delay
                            float hitDelay = spellInfo.LaunchDelay;
                            if (spellInfo.HasAttribute(SpellAttr9.SpecialDelayCalculation))
                                hitDelay += spellInfo.Speed;
                            else if (spellInfo.Speed > 0.0f)
                                hitDelay += Math.Max(victim.GetDistance(this), 5.0f) / spellInfo.Speed;

                            uint delay = (uint)Math.Floor(hitDelay * 1000.0f);
                            // Schedule charge drop
                            eff.GetBase().DropChargeDelayed(delay, AuraRemoveMode.Expire);
                        }
                        else
                            eff.GetBase().DropCharge(AuraRemoveMode.Expire);
                        return magnet;
                    }
                }
            }
            return victim;
        }

        public Unit GetMeleeHitRedirectTarget(Unit victim, SpellInfo spellInfo = null)
        {
            var interceptAuras = victim.GetAuraEffectsByType(AuraType.InterceptMeleeRangedAttacks);
            foreach (var i in interceptAuras)
            {
                Unit magnet = i.GetCaster();
                if (magnet != null)
                    if (_IsValidAttackTarget(magnet, spellInfo) && magnet.IsWithinLOSInMap(this)
                       && (spellInfo == null || (spellInfo.CheckExplicitTarget(this, magnet) == SpellCastResult.SpellCastOk
                       && spellInfo.CheckTarget(this, magnet, false) == SpellCastResult.SpellCastOk)))
                    {
                        i.GetBase().DropCharge(AuraRemoveMode.Expire);
                        return magnet;
                    }
            }
            return victim;
        }

        public bool IsValidAttackTarget(Unit target) => _IsValidAttackTarget(target, null);

        public void SendAttackStateUpdate(HitInfo HitInfo, Unit target, SpellSchoolMask damageSchoolMask, uint Damage, uint AbsorbDamage, uint Resist, VictimState TargetState, uint BlockedAmount)
        {
            CalcDamageInfo dmgInfo = new();
            dmgInfo.HitInfo = HitInfo;
            dmgInfo.Attacker = this;
            dmgInfo.Target = target;
            dmgInfo.Damage = Damage - AbsorbDamage - Resist - BlockedAmount;
            dmgInfo.OriginalDamage = Damage;
            dmgInfo.DamageSchoolMask = (uint)damageSchoolMask;
            dmgInfo.Absorb = AbsorbDamage;
            dmgInfo.Resist = Resist;
            dmgInfo.TargetState = TargetState;
            dmgInfo.Blocked = BlockedAmount;
            SendAttackStateUpdate(dmgInfo);
        }

        public void SendAttackStateUpdate(CalcDamageInfo damageInfo)
        {
            AttackerStateUpdate packet = new();
            packet.hitInfo = damageInfo.HitInfo;
            packet.AttackerGUID = damageInfo.Attacker.GetGUID();
            packet.VictimGUID = damageInfo.Target.GetGUID();
            packet.Damage = (int)damageInfo.Damage;
            packet.OriginalDamage = (int)damageInfo.OriginalDamage;
            int overkill = (int)(damageInfo.Damage - damageInfo.Target.GetHealth());
            packet.OverDamage = (overkill < 0 ? -1 : overkill);

            SubDamage subDmg = new();
            subDmg.SchoolMask = (int)damageInfo.DamageSchoolMask;   // School of sub damage
            subDmg.FDamage = damageInfo.Damage;                // sub damage
            subDmg.Damage = (int)damageInfo.Damage;                 // Sub Damage
            subDmg.Absorbed = (int)damageInfo.Absorb;
            subDmg.Resisted = (int)damageInfo.Resist;
            packet.SubDmg.Set(subDmg);

            packet.VictimState = (byte)damageInfo.TargetState;
            packet.BlockAmount = (int)damageInfo.Blocked;
            packet.LogData.Initialize(damageInfo.Attacker);

            ContentTuningParams contentTuningParams = new();
            if (contentTuningParams.GenerateDataForUnits(damageInfo.Attacker, damageInfo.Target))
                packet.ContentTuning = contentTuningParams;

            SendCombatLogMessage(packet);
        }

        public void AttackedTarget(Unit target, bool canInitialAggro = true)
        {
            if (!target.IsEngaged() && !canInitialAggro)
                return;

            target.EngageWithTarget(this);

            Unit targetOwner = target.GetCharmerOrOwner();
            if (targetOwner != null)
                targetOwner.EngageWithTarget(this);

            Player myPlayerOwner = GetCharmerOrOwnerPlayerOrPlayerItself();
            Player targetPlayerOwner = target.GetCharmerOrOwnerPlayerOrPlayerItself();
            if (myPlayerOwner && targetPlayerOwner && !(myPlayerOwner.duel != null && myPlayerOwner.duel.opponent == targetPlayerOwner))
            {
                myPlayerOwner.UpdatePvP(true);
                myPlayerOwner.SetContestedPvP(targetPlayerOwner);
                myPlayerOwner.RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.PvPActive);
            }
        }

        internal void SendCombatLogMessage(CombatLogServerPacket combatLog)
        {
            CombatLogSender combatLogSender = new(combatLog);

            if (IsPlayer())
                combatLogSender.Invoke(ToPlayer());

            var notifier = new MessageDistDeliverer<CombatLogSender>(this, combatLogSender, GetVisibilityRange());
            Cell.VisitWorldObjects(this, notifier, GetVisibilityRange());
        }

        bool IsThreatened() => !m_threatManager.IsThreatListEmpty();

        public static void Kill(Unit attacker, Unit victim, bool durabilityLoss = true, bool skipSettingDeathState = false)
        {
            // Prevent killing unit twice (and giving reward from kill twice)
            if (victim.GetHealth() == 0)
                return;

            if (attacker != null && !attacker.IsInMap(victim))
                attacker = null;

            // find player: owner of controlled `this` or `this` itself maybe
            Player player = null;
            if (attacker != null)
                player = attacker.GetCharmerOrOwnerPlayerOrPlayerItself();

            Creature creature = victim.ToCreature();

            bool isRewardAllowed = true;
            if (creature != null)
            {
                isRewardAllowed = creature.IsDamageEnoughForLootingAndReward();
                if (!isRewardAllowed)
                    creature.SetLootRecipient(null);
            }

            if (isRewardAllowed && creature != null && creature.GetLootRecipient() != null)
                player = creature.GetLootRecipient();

            // Exploit fix
            if (creature && creature.IsPet() && creature.GetOwnerGUID().IsPlayer())
                isRewardAllowed = false;

            // Reward player, his pets, and group/raid members
            // call kill spell proc event (before real die and combat stop to triggering auras removed at death/combat stop)
            if (isRewardAllowed && player != null && player != victim)
            {
                PartyKillLog partyKillLog = new();
                partyKillLog.Player = player.GetGUID();
                partyKillLog.Victim = victim.GetGUID();

                Player looter = player;
                var group = player.GetGroup();
                bool hasLooterGuid = false;
                if (group)
                {
                    group.BroadcastPacket(partyKillLog, group.GetMemberGroup(player.GetGUID()) != 0);

                    if (creature)
                    {
                        group.UpdateLooterGuid(creature, true);
                        if (!group.GetLooterGuid().IsEmpty())
                        {
                            looter = Global.ObjAccessor.FindPlayer(group.GetLooterGuid());
                            if (looter)
                            {
                                hasLooterGuid = true;
                                creature.SetLootRecipient(looter);   // update creature loot recipient to the allowed looter.
                            }
                        }
                    }
                }
                else
                {
                    player.SendPacket(partyKillLog);

                    if (creature != null)
                    {
                        LootList lootList = new();
                        lootList.Owner = creature.GetGUID();
                        lootList.LootObj = creature.loot.GetGUID();
                        player.SendMessageToSet(lootList, true);
                    }
                }

                if (creature)
                {
                    Loot loot = creature.loot;

                    loot.Clear();
                    uint lootid = creature.GetCreatureTemplate().LootId;
                    if (lootid != 0)
                        loot.FillLoot(lootid, LootStorage.Creature, looter, false, false, creature.GetLootMode(), creature.GetMap().GetDifficultyLootItemContext());

                    if (creature.GetLootMode() > 0)
                        loot.GenerateMoneyLoot(creature.GetCreatureTemplate().MinGold, creature.GetCreatureTemplate().MaxGold);

                    if (group)
                    {
                        if (hasLooterGuid)
                            group.SendLooter(creature, looter);
                        else
                            group.SendLooter(creature, null);

                        // Update round robin looter only if the creature had loot
                        if (!loot.Empty())
                            group.UpdateLooterGuid(creature);
                    }
                }

                player.RewardPlayerAndGroupAtKill(victim, false);
            }

            // Do KILL and KILLED procs. KILL proc is called only for the unit who landed the killing blow (and its owner - for pets and totems) regardless of who tapped the victim
            if (attacker != null && (attacker.IsPet() || attacker.IsTotem()))
            {
                // proc only once for victim
                Unit owner = attacker.GetOwner();
                if (owner != null)
                    ProcSkillsAndAuras(owner, victim, ProcFlags.Kill, ProcFlags.None, ProcFlagsSpellType.MaskAll, ProcFlagsSpellPhase.None, ProcFlagsHit.None, null, null, null);
            }

            if (!victim.IsCritter())
                ProcSkillsAndAuras(attacker, victim, ProcFlags.Kill, ProcFlags.Killed, ProcFlagsSpellType.MaskAll, ProcFlagsSpellPhase.None, ProcFlagsHit.None, null, null, null);

            // Proc auras on death - must be before aura/combat remove
            ProcSkillsAndAuras(victim, victim, ProcFlags.None, ProcFlags.Death, ProcFlagsSpellType.MaskAll, ProcFlagsSpellPhase.None, ProcFlagsHit.None, null, null, null);

            if (!skipSettingDeathState)
            {
                Log.outDebug(LogFilter.Unit, "SET JUST_DIED");
                victim.SetDeathState(DeathState.JustDied);
            }

            // Inform pets (if any) when player kills target)
            // MUST come after victim.setDeathState(JUST_DIED); or pet next target
            // selection will get stuck on same target and break pet react state
            if (player != null)
            {
                Pet pet = player.GetPet();
                if (pet != null && pet.IsAlive() && pet.IsControlled())
                    pet.GetAI().KilledUnit(victim);
            }

            // 10% durability loss on death
            Player plrVictim = victim.ToPlayer();
            if (plrVictim != null)
            {
                // remember victim PvP death for corpse type and corpse reclaim delay
                // at original death (not at SpiritOfRedemtionTalent timeout)
                plrVictim.SetPvPDeath(player != null);

                // only if not player and not controlled by player pet. And not at BG
                if ((durabilityLoss && player == null && !victim.ToPlayer().InBattleground()) || (player != null && WorldConfig.GetBoolValue(WorldCfg.DurabilityLossInPvp)))
                {
                    double baseLoss = WorldConfig.GetFloatValue(WorldCfg.RateDurabilityLossOnDeath);
                    uint loss = (uint)(baseLoss - (baseLoss * plrVictim.GetTotalAuraMultiplier(AuraType.ModDurabilityLoss)));
                    Log.outDebug(LogFilter.Unit, "We are dead, losing {0} percent durability", loss);
                    // Durability loss is calculated more accurately again for each item in Player.DurabilityLoss
                    plrVictim.DurabilityLossAll(baseLoss, false);
                    // durability lost message
                    plrVictim.SendDurabilityLoss(plrVictim, loss);
                }
                // Call KilledUnit for creatures
                if (attacker != null && attacker.IsCreature() && attacker.IsAIEnabled)
                    attacker.ToCreature().GetAI().KilledUnit(victim);

                // last damage from non duel opponent or opponent controlled creature
                if (plrVictim.duel != null)
                {
                    plrVictim.duel.opponent.CombatStopWithPets(true);
                    plrVictim.CombatStopWithPets(true);
                    plrVictim.DuelComplete(DuelCompleteType.Interrupted);
                }
            }
            else                                                // creature died
            {
                Log.outDebug(LogFilter.Unit, "DealDamageNotPlayer");

                if (!creature.IsPet())
                {
                    creature.GetThreatManager().ClearAllThreat();

                    // must be after setDeathState which resets dynamic flags
                    if (!creature.loot.IsLooted())
                        creature.AddDynamicFlag(UnitDynFlags.Lootable);
                    else
                        creature.AllLootRemovedFromCorpse();
                }

                // Call KilledUnit for creatures, this needs to be called after the lootable flag is set
                if (attacker != null && attacker.IsCreature() && attacker.IsAIEnabled)
                    attacker.ToCreature().GetAI().KilledUnit(victim);

                // Call creature just died function
                if (creature.IsAIEnabled)
                    creature.GetAI().JustDied(attacker);

                TempSummon summon = creature.ToTempSummon();
                if (summon != null)
                {
                    Unit summoner = summon.GetSummoner();
                    if (summoner != null)
                        if (summoner.IsTypeId(TypeId.Unit) && summoner.IsAIEnabled)
                            summoner.ToCreature().GetAI().SummonedCreatureDies(creature, attacker);
                }

                // Dungeon specific stuff, only applies to players killing creatures
                if (creature.GetInstanceId() != 0)
                {
                    Map instanceMap = creature.GetMap();

                    /// @todo do instance binding anyway if the charmer/owner is offline
                    if (instanceMap.IsDungeon() && ((attacker != null && attacker.GetCharmerOrOwnerPlayerOrPlayerItself() != null) || attacker == victim))
                    {
                        if (instanceMap.IsRaidOrHeroicDungeon())
                        {
                            if (creature.GetCreatureTemplate().FlagsExtra.HasAnyFlag(CreatureFlagsExtra.InstanceBind))
                                instanceMap.ToInstanceMap().PermBindAllPlayers();
                        }
                        else
                        {
                            // the reset time is set but not added to the scheduler
                            // until the players leave the instance
                            long resettime = GameTime.GetGameTime() + 2 * Time.Hour;
                            InstanceSave save = Global.InstanceSaveMgr.GetInstanceSave(creature.GetInstanceId());
                            if (save != null)
                                if (save.GetResetTime() < resettime)
                                    save.SetResetTime(resettime);
                        }
                    }
                }
            }

            // outdoor pvp things, do these after setting the death state, else the player activity notify won't work... doh...
            // handle player kill only if not suicide (spirit of redemption for example)
            if (player != null && attacker != victim)
            {
                OutdoorPvP pvp = player.GetOutdoorPvP();
                if (pvp != null)
                    pvp.HandleKill(player, victim);

                BattleField bf = Global.BattleFieldMgr.GetBattlefieldToZoneId(player.GetZoneId());
                if (bf != null)
                    bf.HandleKill(player, victim);
            }

            // Battlegroundthings (do this at the end, so the death state flag will be properly set to handle in the bg.handlekill)
            if (player != null && player.InBattleground())
            {
                Battleground bg = player.GetBattleground();
                if (bg)
                {
                    Player playerVictim = victim.ToPlayer();
                    if (playerVictim)
                        bg.HandleKillPlayer(playerVictim, player);
                    else
                        bg.HandleKillUnit(victim.ToCreature(), player);
                }
            }

            // Hook for OnPVPKill Event
            if (attacker != null)
            {
                Player killerPlr = attacker.ToPlayer();
                if (killerPlr != null)
                {
                    Player killedPlr = victim.ToPlayer();
                    if (killedPlr != null)
                        Global.ScriptMgr.OnPVPKill(killerPlr, killedPlr);
                    else
                    {
                        Creature killedCre = victim.ToCreature();
                        if (killedCre != null)
                            Global.ScriptMgr.OnCreatureKill(killerPlr, killedCre);
                    }
                }
                else
                {
                    Creature killerCre = attacker.ToCreature();
                    if (killerCre != null)
                    {
                        Player killed = victim.ToPlayer();
                        if (killed != null)
                            Global.ScriptMgr.OnPlayerKilledByCreature(killerCre, killed);
                    }
                }
            }
        }

        public void KillSelf(bool durabilityLoss = true, bool skipSettingDeathState = false) => Kill(this, this, durabilityLoss, skipSettingDeathState);

        public virtual bool CanUseAttackType(WeaponAttackType attacktype)
        {
            return attacktype switch
            {
                WeaponAttackType.BaseAttack => !HasUnitFlag(UnitFlags.Disarmed),
                WeaponAttackType.OffAttack => !HasUnitFlag2(UnitFlags2.DisarmOffhand),
                WeaponAttackType.RangedAttack => !HasUnitFlag2(UnitFlags2.DisarmRanged),
                _ => true,
            };
        }

        // TODO for melee need create structure as in
        void CalculateMeleeDamage(Unit victim, out CalcDamageInfo damageInfo, WeaponAttackType attackType)
        {
            damageInfo = new CalcDamageInfo();

            damageInfo.Attacker = this;
            damageInfo.Target = victim;

            damageInfo.DamageSchoolMask = (uint)SpellSchoolMask.Normal;
            damageInfo.Damage = 0;
            damageInfo.OriginalDamage = 0;
            damageInfo.Absorb = 0;
            damageInfo.Resist = 0;

            damageInfo.Blocked = 0;
            damageInfo.HitInfo = 0;
            damageInfo.TargetState = 0;

            damageInfo.AttackType = attackType;
            damageInfo.ProcAttacker = ProcFlags.None;
            damageInfo.ProcVictim = ProcFlags.None;
            damageInfo.CleanDamage = 0;
            damageInfo.HitOutCome = MeleeHitOutcome.Evade;

            if (victim == null)
                return;

            if (!IsAlive() || !victim.IsAlive())
                return;

            // Select HitInfo/procAttacker/procVictim flag based on attack type
            switch (attackType)
            {
                case WeaponAttackType.BaseAttack:
                    damageInfo.ProcAttacker = ProcFlags.DoneMeleeAutoAttack | ProcFlags.DoneMainHandAttack;
                    damageInfo.ProcVictim = ProcFlags.TakenMeleeAutoAttack;
                    break;
                case WeaponAttackType.OffAttack:
                    damageInfo.ProcAttacker = ProcFlags.DoneMeleeAutoAttack | ProcFlags.DoneOffHandAttack;
                    damageInfo.ProcVictim = ProcFlags.TakenMeleeAutoAttack;
                    damageInfo.HitInfo = HitInfo.OffHand;
                    break;
                default:
                    return;
            }

            // Physical Immune check
            if (damageInfo.Target.IsImmunedToDamage((SpellSchoolMask)damageInfo.DamageSchoolMask))
            {
                damageInfo.HitInfo |= HitInfo.NormalSwing;
                damageInfo.TargetState = VictimState.Immune;

                damageInfo.Damage = 0;
                damageInfo.CleanDamage = 0;
                return;
            }

            uint damage = 0;
            damage += CalculateDamage(damageInfo.AttackType, false, true);
            // Add melee damage bonus
            damage = MeleeDamageBonusDone(damageInfo.Target, damage, damageInfo.AttackType, DamageEffectType.Direct, null, (SpellSchoolMask)damageInfo.DamageSchoolMask);
            damage = damageInfo.Target.MeleeDamageBonusTaken(this, damage, damageInfo.AttackType, DamageEffectType.Direct, null, (SpellSchoolMask)damageInfo.DamageSchoolMask);

            // Script Hook For CalculateMeleeDamage -- Allow scripts to change the Damage pre class mitigation calculations
            Global.ScriptMgr.ModifyMeleeDamage(damageInfo.Target, damageInfo.Attacker, ref damage);

            // Calculate armor reduction
            if (IsDamageReducedByArmor((SpellSchoolMask)damageInfo.DamageSchoolMask))
            {
                damageInfo.Damage = CalcArmorReducedDamage(damageInfo.Attacker, damageInfo.Target, damage, null, damageInfo.AttackType);
                damageInfo.CleanDamage += damage - damageInfo.Damage;
            }
            else
                damageInfo.Damage = damage;

            damageInfo.HitOutCome = RollMeleeOutcomeAgainst(damageInfo.Target, damageInfo.AttackType);

            switch (damageInfo.HitOutCome)
            {
                case MeleeHitOutcome.Evade:
                    damageInfo.HitInfo |= HitInfo.Miss | HitInfo.SwingNoHitSound;
                    damageInfo.TargetState = VictimState.Evades;
                    damageInfo.OriginalDamage = damageInfo.Damage;

                    damageInfo.Damage = 0;
                    damageInfo.CleanDamage = 0;
                    return;
                case MeleeHitOutcome.Miss:
                    damageInfo.HitInfo |= HitInfo.Miss;
                    damageInfo.TargetState = VictimState.Intact;
                    damageInfo.OriginalDamage = damageInfo.Damage;

                    damageInfo.Damage = 0;
                    damageInfo.CleanDamage = 0;
                    break;
                case MeleeHitOutcome.Normal:
                    damageInfo.TargetState = VictimState.Hit;
                    damageInfo.OriginalDamage = damageInfo.Damage;
                    break;
                case MeleeHitOutcome.Crit:
                    damageInfo.HitInfo |= HitInfo.CriticalHit;
                    damageInfo.TargetState = VictimState.Hit;
                    // Crit bonus calc
                    damageInfo.Damage *= 2;

                    // Increase crit damage from SPELL_AURA_MOD_CRIT_DAMAGE_BONUS
                    float mod = (GetTotalAuraMultiplierByMiscMask(AuraType.ModCritDamageBonus, damageInfo.DamageSchoolMask) - 1.0f) * 100;

                    if (mod != 0)
                        MathFunctions.AddPct(ref damageInfo.Damage, mod);

                    damageInfo.OriginalDamage = damageInfo.Damage;
                    break;
                case MeleeHitOutcome.Parry:
                    damageInfo.TargetState = VictimState.Parry;
                    damageInfo.CleanDamage += damageInfo.Damage;

                    damageInfo.OriginalDamage = damageInfo.Damage;
                    damageInfo.Damage = 0;
                    break;
                case MeleeHitOutcome.Dodge:
                    damageInfo.TargetState = VictimState.Dodge;
                    damageInfo.CleanDamage += damageInfo.Damage;

                    damageInfo.OriginalDamage = damageInfo.Damage;
                    damageInfo.Damage = 0;
                    break;
                case MeleeHitOutcome.Block:
                    damageInfo.TargetState = VictimState.Hit;
                    damageInfo.HitInfo |= HitInfo.Block;
                    // 30% damage blocked, double blocked amount if block is critical
                    damageInfo.Blocked = MathFunctions.CalculatePct(damageInfo.Damage, damageInfo.Target.GetBlockPercent(GetLevel()));
                    if (damageInfo.Target.IsBlockCritical())
                        damageInfo.Blocked *= 2;

                    damageInfo.OriginalDamage = damageInfo.Damage;
                    damageInfo.Damage -= damageInfo.Blocked;
                    damageInfo.CleanDamage += damageInfo.Blocked;
                    break;
                case MeleeHitOutcome.Glancing:
                    damageInfo.HitInfo |= HitInfo.Glancing;
                    damageInfo.TargetState = VictimState.Hit;
                    int leveldif = (int)victim.GetLevel() - (int)GetLevel();
                    if (leveldif > 3)
                        leveldif = 3;

                    damageInfo.OriginalDamage = damageInfo.Damage;
                    float reducePercent = 1.0f - leveldif * 0.1f;
                    damageInfo.CleanDamage += damageInfo.Damage - (uint)(reducePercent * damageInfo.Damage);
                    damageInfo.Damage = (uint)(reducePercent * damageInfo.Damage);
                    break;
                case MeleeHitOutcome.Crushing:
                    damageInfo.HitInfo |= HitInfo.Crushing;
                    damageInfo.TargetState = VictimState.Hit;
                    // 150% normal damage
                    damageInfo.Damage += (damageInfo.Damage / 2);
                    damageInfo.OriginalDamage = damageInfo.Damage;
                    break;

                default:
                    break;
            }

            // Always apply HITINFO_AFFECTS_VICTIM in case its not a miss
            if (!damageInfo.HitInfo.HasAnyFlag(HitInfo.Miss))
                damageInfo.HitInfo |= HitInfo.AffectsVictim;

            int resilienceReduction = (int)damageInfo.Damage;
            if (CanApplyResilience())
                ApplyResilience(victim, ref resilienceReduction);

            resilienceReduction = (int)damageInfo.Damage - resilienceReduction;
            damageInfo.Damage -= (uint)resilienceReduction;
            damageInfo.CleanDamage += (uint)resilienceReduction;

            // Calculate absorb resist
            if (damageInfo.Damage > 0)
            {
                damageInfo.ProcVictim |= ProcFlags.TakenDamage;
                // Calculate absorb & resists
                DamageInfo dmgInfo = new(damageInfo);
                CalcAbsorbResist(dmgInfo);
                damageInfo.Absorb = dmgInfo.GetAbsorb();
                damageInfo.Resist = dmgInfo.GetResist();

                if (damageInfo.Absorb != 0)
                    damageInfo.HitInfo |= (damageInfo.Damage - damageInfo.Absorb == 0 ? HitInfo.FullAbsorb : HitInfo.PartialAbsorb);

                if (damageInfo.Resist != 0)
                    damageInfo.HitInfo |= (damageInfo.Damage - damageInfo.Resist == 0 ? HitInfo.FullResist : HitInfo.PartialResist);

                damageInfo.Damage = dmgInfo.GetDamage();
            }
            else // Impossible get negative result but....
                damageInfo.Damage = 0;
        }

        MeleeHitOutcome RollMeleeOutcomeAgainst(Unit victim, WeaponAttackType attType)
        {
            if (victim.IsTypeId(TypeId.Unit) && victim.ToCreature().IsEvadingAttacks())
                return MeleeHitOutcome.Evade;

            // Miss chance based on melee
            int miss_chance = (int)(MeleeSpellMissChance(victim, attType, null) * 100.0f);

            // Critical hit chance
            int crit_chance = (int)((GetUnitCriticalChanceAgainst(attType, victim) + GetTotalAuraModifier(AuraType.ModAutoAttackCritChance)) * 100.0f);

            int dodge_chance = (int)(GetUnitDodgeChance(attType, victim) * 100.0f);
            int block_chance = (int)(GetUnitBlockChance(attType, victim) * 100.0f);
            int parry_chance = (int)(GetUnitParryChance(attType, victim) * 100.0f);

            // melee attack table implementation
            // outcome priority:
            //   1. >    2. >    3. >       4. >    5. >   6. >       7. >  8.
            // MISS > DODGE > PARRY > GLANCING > BLOCK > CRIT > CRUSHING > HIT

            int sum = 0;
            int roll = RandomHelper.IRand(0, 9999);

            uint attackerLevel = GetLevelForTarget(victim);
            uint victimLevel = GetLevelForTarget(this);

            // check if attack comes from behind, nobody can parry or block if attacker is behind
            bool canParryOrBlock = victim.HasInArc((float)Math.PI, this) || victim.HasAuraType(AuraType.IgnoreHitDirection);

            // only creatures can dodge if attacker is behind
            bool canDodge = !victim.IsTypeId(TypeId.Player) || canParryOrBlock;

            // if victim is casting or cc'd it can't avoid attacks
            if (victim.IsNonMeleeSpellCast(false) || victim.HasUnitState(UnitState.Controlled))
            {
                canDodge = false;
                canParryOrBlock = false;
            }

            // 1. MISS
            int tmp = miss_chance;
            if (tmp > 0 && roll < (sum += tmp))
                return MeleeHitOutcome.Miss;

            // always crit against a sitting target (except 0 crit chance)
            if (victim.IsTypeId(TypeId.Player) && crit_chance > 0 && !victim.IsStandState())
                return MeleeHitOutcome.Crit;

            // 2. DODGE
            if (canDodge)
            {
                tmp = dodge_chance;
                if (tmp > 0                                         // check if unit _can_ dodge
                    && roll < (sum += tmp))
                    return MeleeHitOutcome.Dodge;
            }

            // 3. PARRY
            if (canParryOrBlock)
            {
                tmp = parry_chance;
                if (tmp > 0                                         // check if unit _can_ parry
                    && roll < (sum += tmp))
                    return MeleeHitOutcome.Parry;
            }

            // 4. GLANCING
            // Max 40% chance to score a glancing blow against mobs that are higher level (can do only players and pets and not with ranged weapon)
            if ((IsTypeId(TypeId.Player) || IsPet()) &&
                !victim.IsTypeId(TypeId.Player) && !victim.IsPet() &&
                attackerLevel + 3 < victimLevel)
            {
                // cap possible value (with bonuses > max skill)
                tmp = (int)(10 + 10 * (victimLevel - attackerLevel)) * 100;
                if (tmp > 0 && roll < (sum += tmp))
                    return MeleeHitOutcome.Glancing;
            }

            // 5. BLOCK
            if (canParryOrBlock)
            {
                tmp = block_chance;
                if (tmp > 0                                          // check if unit _can_ block
                    && roll < (sum += tmp))
                    return MeleeHitOutcome.Block;
            }

            // 6.CRIT
            tmp = crit_chance;
            if (tmp > 0 && roll < (sum += tmp))
                return MeleeHitOutcome.Crit;

            // 7. CRUSHING
            // mobs can score crushing blows if they're 4 or more levels above victim
            if (attackerLevel >= victimLevel + 4 &&
                // can be from by creature (if can) or from controlled player that considered as creature
                !IsControlledByPlayer() &&
                !(GetTypeId() == TypeId.Unit && ToCreature().GetCreatureTemplate().FlagsExtra.HasAnyFlag(CreatureFlagsExtra.NoCrush)))
            {
                // add 2% chance per level, min. is 15%
                tmp = (int)(attackerLevel - victimLevel * 1000 - 1500);
                if (roll < (sum += tmp))
                {
                    Log.outDebug(LogFilter.Unit, "RollMeleeOutcomeAgainst: CRUSHING <{0}, {1})", sum - tmp, sum);
                    return MeleeHitOutcome.Crushing;
                }
            }

            // 8. HIT
            return MeleeHitOutcome.Normal;
        }

        public uint CalculateDamage(WeaponAttackType attType, bool normalized, bool addTotalPct)
        {
            float minDamage;
            float maxDamage;

            if (normalized || !addTotalPct)
            {
                CalculateMinMaxDamage(attType, normalized, addTotalPct, out minDamage, out maxDamage);
                if (IsInFeralForm() && attType == WeaponAttackType.BaseAttack)
                {
                    CalculateMinMaxDamage(WeaponAttackType.OffAttack, normalized, addTotalPct, out float minOffhandDamage, out float maxOffhandDamage);
                    minDamage += minOffhandDamage;
                    maxDamage += maxOffhandDamage;
                }
            }
            else
            {
                switch (attType)
                {
                    case WeaponAttackType.RangedAttack:
                        minDamage = GetUpdateField<float>(UnitFields.MinRangedDamage);
                        maxDamage = GetUpdateField<float>(UnitFields.MaxRangedDamage);
                        break;
                    case WeaponAttackType.BaseAttack:
                        minDamage = GetUpdateField<float>(UnitFields.MinDamage);
                        maxDamage = GetUpdateField<float>(UnitFields.MaxDamage);
                        if (IsInFeralForm())
                        {
                            minDamage += GetUpdateField<float>(UnitFields.MinOffHandDamage);
                            maxDamage += GetUpdateField<float>(UnitFields.MaxOffHandDamage);
                        }
                        break;
                    case WeaponAttackType.OffAttack:
                        minDamage = GetUpdateField<float>(UnitFields.MinOffHandDamage);
                        maxDamage = GetUpdateField<float>(UnitFields.MaxOffHandDamage);
                        break;
                    // Just for good manner
                    default:
                        minDamage = 0.0f;
                        maxDamage = 0.0f;
                        break;
                }
            }
            minDamage = Math.Max(0.0f, minDamage);
            maxDamage = Math.Max(0.0f, maxDamage);

            if (minDamage > maxDamage)
                Extensions.Swap(ref minDamage, ref maxDamage);

            return RandomHelper.URand(minDamage, maxDamage);
        }

        public float GetWeaponDamageRange(WeaponAttackType attType, WeaponDamageRange type)
        {
            if (attType == WeaponAttackType.OffAttack && !HaveOffhandWeapon())
                return 0.0f;

            return m_weaponDamage[(int)attType][(int)type];
        }

        public float GetAPMultiplier(WeaponAttackType attType, bool normalized)
        {
            if (!IsTypeId(TypeId.Player) || (IsInFeralForm() && !normalized))
                return GetBaseAttackTime(attType) / 1000.0f;

            Item weapon = ToPlayer().GetWeaponForAttack(attType, true);
            if (!weapon)
                return 2.0f;

            if (!normalized)
                return weapon.GetTemplate().GetDelay() / 1000.0f;

            switch ((ItemSubClassWeapon)weapon.GetTemplate().GetSubClass())
            {
                case ItemSubClassWeapon.Axe2:
                case ItemSubClassWeapon.Mace2:
                case ItemSubClassWeapon.Polearm:
                case ItemSubClassWeapon.Sword2:
                case ItemSubClassWeapon.Staff:
                case ItemSubClassWeapon.FishingPole:
                    return 3.3f;
                case ItemSubClassWeapon.Axe:
                case ItemSubClassWeapon.Mace:
                case ItemSubClassWeapon.Sword:
                case ItemSubClassWeapon.Warglaives:
                case ItemSubClassWeapon.Exotic:
                case ItemSubClassWeapon.Exotic2:
                case ItemSubClassWeapon.Fist:
                    return 2.4f;
                case ItemSubClassWeapon.Dagger:
                    return 1.7f;
                case ItemSubClassWeapon.Thrown:
                    return 2.0f;
                default:
                    return weapon.GetTemplate().GetDelay() / 1000.0f;
            }
        }

        public float GetTotalAttackPowerValue(WeaponAttackType attType, bool includeWeapon = true)
        {
            if (attType == WeaponAttackType.RangedAttack)
            {
                float ap = GetUpdateField<float>(UnitFields.RangedAttackPower) + GetUpdateField<float>(UnitFields.RangedAttackPowerModPos) + GetUpdateField<float>(UnitFields.RangedAttackPowerModNeg);
                if (ap < 0)
                    return 0.0f;
                return ap * (1.0f + GetUpdateField<float>(UnitFields.RangedAttackPowerMultiplier));
            }
            else
            {
                float ap = GetUpdateField<float>(UnitFields.AttackPower) + GetUpdateField<float>(UnitFields.AttackPowerModPos) + GetUpdateField<float>(UnitFields.AttackPowerModNeg);
                if (ap < 0)
                    return 0.0f;
                return ap * (1.0f + GetUpdateField<float>(UnitFields.AttackPowerMultiplier));
            }
        }

        public bool IsWithinMeleeRange(Unit obj)
        {
            if (!obj || !IsInMap(obj) || !IsInPhase(obj))
                return false;

            float dx = GetPositionX() - obj.GetPositionX();
            float dy = GetPositionY() - obj.GetPositionY();
            float dz = GetPositionZMinusOffset() - obj.GetPositionZMinusOffset();
            float distsq = (dx * dx) + (dy * dy) + (dz * dz);

            float maxdist = GetMeleeRange(obj) + GetTotalAuraModifier(AuraType.ModAutoAttackRange);

            return distsq <= maxdist * maxdist;
        }

        public float GetMeleeRange(Unit target)
        {
            float range = GetCombatReach() + target.GetCombatReach() + 4.0f / 3.0f;
            return Math.Max(range, SharedConst.NominalMeleeRange);
        }

        public void SetBaseAttackTime(WeaponAttackType att, uint val)
        {
            m_baseAttackSpeed[(int)att] = val;
            UpdateAttackTimeField(att);
        }

        void UpdateAttackTimeField(WeaponAttackType att)
        {
            switch (att)
            {
                case WeaponAttackType.BaseAttack:
                case WeaponAttackType.OffAttack:
                    SetUpdateField<uint>(UnitFields.AttackRoundBaseTime + (int)att, (uint)(m_baseAttackSpeed[(int)att] * m_modAttackSpeedPct[(int)att]));
                    break;
                case WeaponAttackType.RangedAttack:
                    SetUpdateField<uint>(UnitFields.RangedAttackRoundBaseTime, (uint)(m_baseAttackSpeed[(int)att] * m_modAttackSpeedPct[(int)att]));
                    break;
                default:
                    break; ;
            }
        }

        // function based on function Unit.CanAttack from 13850 client
        public bool _IsValidAttackTarget(Unit target, SpellInfo bySpell, WorldObject obj = null)
        {
            Cypher.Assert(target != null);

            // can't attack self
            if (this == target)
                return false;

            // can't attack unattackable units or GMs
            if (target.HasUnitState(UnitState.Unattackable)
                || (target.IsTypeId(TypeId.Player) && target.ToPlayer().IsGameMaster()))
                return false;

            // visibility checks
            // skip visibility check for GO casts, needs removal when go cast is implemented. Also ignore for gameobject and dynauras
            if (GetEntry() != SharedConst.WorldTrigger && (!obj || !obj.IsTypeMask(TypeMask.GameObject | TypeMask.DynamicObject)))
            {
                // can't attack invisible
                if (bySpell == null || !bySpell.HasAttribute(SpellAttr6.CanTargetInvisible))
                {
                    if (obj && !obj.CanSeeOrDetect(target, bySpell != null && bySpell.IsAffectingArea()))
                        return false;
                    else if (!obj)
                    {
                        // ignore stealth for aoe spells. Ignore stealth if target is player and unit in combat with same player
                        bool ignoreStealthCheck = (bySpell != null && bySpell.IsAffectingArea()) ||
                            (target.GetTypeId() == TypeId.Player && target.HasStealthAura() && IsInCombatWith(target));

                        if (!CanSeeOrDetect(target, ignoreStealthCheck))
                            return false;
                    }
                }
            }

            // can't attack dead
            if ((bySpell == null || !bySpell.IsAllowingDeadTarget()) && !target.IsAlive())
                return false;

            // can't attack untargetable
            if ((bySpell == null || !bySpell.HasAttribute(SpellAttr6.CanTargetUntargetable))
                && target.HasUnitFlag(UnitFlags.NotSelectable))
                return false;

            Player playerAttacker = ToPlayer();
            if (playerAttacker != null)
            {
                if (playerAttacker.HasPlayerFlag(PlayerFlags.Commentator2))
                    return false;
            }

            // check flags
            if (target.HasUnitFlag(UnitFlags.NonAttackable | UnitFlags.TaxiFlight | UnitFlags.NotAttackable1 | UnitFlags.Unk16)
                || (!HasUnitFlag(UnitFlags.PvpAttackable) && target.IsImmuneToNPC())
                || (!target.HasUnitFlag(UnitFlags.PvpAttackable) && IsImmuneToNPC()))
                return false;

            if ((bySpell == null || !bySpell.HasAttribute(SpellAttr8.AttackIgnoreImmuneToPCFlag))
                && (HasUnitFlag(UnitFlags.PvpAttackable) && target.IsImmuneToPC())
                // check if this is a world trigger cast - GOs are using world triggers to cast their spells, so we need to ignore their immunity flag here, this is a temp workaround, needs removal when go cast is implemented properly
                && GetEntry() != SharedConst.WorldTrigger)
                return false;

            // CvC case - can attack each other only when one of them is hostile
            if (!HasUnitFlag(UnitFlags.PvpAttackable) && !target.HasUnitFlag(UnitFlags.PvpAttackable))
                return GetReactionTo(target) <= ReputationRank.Hostile || target.GetReactionTo(this) <= ReputationRank.Hostile;

            // PvP, PvC, CvP case
            // can't attack friendly targets
            if (GetReactionTo(target) > ReputationRank.Neutral || target.GetReactionTo(this) > ReputationRank.Neutral)
                return false;

            Player playerAffectingAttacker = HasUnitFlag(UnitFlags.PvpAttackable) ? GetAffectingPlayer() : null;
            Player playerAffectingTarget = target.HasUnitFlag(UnitFlags.PvpAttackable) ? target.GetAffectingPlayer() : null;

            // Not all neutral creatures can be attacked (even some unfriendly faction does not react aggresive to you, like Sporaggar)
            if ((playerAffectingAttacker && !playerAffectingTarget) || (!playerAffectingAttacker && playerAffectingTarget))
            {
                if (!(target.IsTypeId(TypeId.Player) && IsTypeId(TypeId.Player)) &&
                    !(target.IsTypeId(TypeId.Unit) && IsTypeId(TypeId.Unit)))
                {
                    Player player = playerAffectingAttacker ? playerAffectingAttacker : playerAffectingTarget;
                    Unit creature = playerAffectingAttacker ? target : this;

                    if (creature.IsContestedGuard() && player.HasPlayerFlag(PlayerFlags.ContestedPVP))
                        return true;

                    var factionTemplate = creature.GetFactionTemplateEntry();
                    if (factionTemplate != null)
                    {
                        if (player.GetReputationMgr().GetForcedRankIfAny(factionTemplate) == ReputationRank.None)
                        {
                            var factionEntry = CliDB.FactionStorage.LookupByKey(factionTemplate.Faction);
                            if (factionEntry != null)
                            {
                                var repState = player.GetReputationMgr().GetState(factionEntry);
                                if (repState != null)
                                    if (!repState.Flags.HasFlag(ReputationFlags.AtWar))
                                        return false;
                            }
                        }

                    }
                }
            }

            Creature creatureAttacker = ToCreature();
            if (creatureAttacker != null && Convert.ToBoolean(creatureAttacker.GetCreatureTemplate().TypeFlags & CreatureTypeFlags.TreatAsRaidUnit))
                return false;

            // check duel - before sanctuary checks
            if (playerAffectingAttacker != null && playerAffectingTarget != null)
                if (playerAffectingAttacker.duel != null && playerAffectingAttacker.duel.opponent == playerAffectingTarget && playerAffectingAttacker.duel.startTime != 0)
                    return true;

            // PvP case - can't attack when attacker or target are in sanctuary
            // however, 13850 client doesn't allow to attack when one of the unit's has sanctuary flag and is pvp
            if (target.HasUnitFlag(UnitFlags.PvpAttackable) && HasUnitFlag(UnitFlags.PvpAttackable) && (target.IsInSanctuary() || IsInSanctuary()))
                return false;

            // additional checks - only PvP case
            if (playerAffectingAttacker != null && playerAffectingTarget != null)
            {
                if (target.IsPvP())
                    return true;

                if (IsFFAPvP() && target.IsFFAPvP())
                    return true;

                return HasPvpFlag(UnitPVPStateFlags.Unk1) || target.HasPvpFlag(UnitPVPStateFlags.Unk1);
            }
            return true;
        }

        public bool IsValidAssistTarget(Unit target) => _IsValidAssistTarget(target, null);

        // function based on function Unit.CanAssist from 13850 client
        public bool _IsValidAssistTarget(Unit target, SpellInfo bySpell)
        {
            Cypher.Assert(target != null);

            // can assist to self
            if (this == target)
                return true;

            // can't assist unattackable units or GMs
            if (target.HasUnitState(UnitState.Unattackable)
                || (target.IsTypeId(TypeId.Player) && target.ToPlayer().IsGameMaster()))
                return false;

            // can't assist own vehicle or passenger
            if (m_vehicle != null)
                if (IsOnVehicle(target) || m_vehicle.GetBase().IsOnVehicle(target))
                    return false;

            // can't assist invisible
            if ((bySpell == null || !bySpell.HasAttribute(SpellAttr6.CanTargetInvisible)) && !CanSeeOrDetect(target, bySpell != null && bySpell.IsAffectingArea()))
                return false;

            // can't assist dead
            if ((bySpell == null || !bySpell.IsAllowingDeadTarget()) && !target.IsAlive())
                return false;

            // can't assist untargetable
            if ((bySpell == null || !bySpell.HasAttribute(SpellAttr6.CanTargetUntargetable))
                && target.HasUnitFlag(UnitFlags.NotSelectable))
                return false;

            if (bySpell == null || !bySpell.HasAttribute(SpellAttr6.AssistIgnoreImmuneFlag))
            {
                if (HasUnitFlag(UnitFlags.PvpAttackable))
                {
                    if (target.IsImmuneToPC())
                        return false;
                }
                else
                {
                    if (target.IsImmuneToNPC())
                        return false;
                }
            }

            // can't assist non-friendly targets
            if (GetReactionTo(target) <= ReputationRank.Neutral
                && target.GetReactionTo(this) <= ReputationRank.Neutral
                && (!IsTypeId(TypeId.Unit) || !Convert.ToBoolean(ToCreature().GetCreatureTemplate().TypeFlags & CreatureTypeFlags.TreatAsRaidUnit)))
                return false;

            // Controlled player case, we can assist creatures (reaction already checked above, our faction == charmer faction)
            if (IsTypeId(TypeId.Player) && IsCharmed() && GetCharmerGUID().IsCreature())
                return true;

            // PvP case
            else if (target.HasUnitFlag(UnitFlags.PvpAttackable))
            {
                Player targetPlayerOwner = target.GetAffectingPlayer();
                if (HasUnitFlag(UnitFlags.PvpAttackable))
                {
                    Player selfPlayerOwner = GetAffectingPlayer();
                    if (selfPlayerOwner != null && targetPlayerOwner != null)
                    {
                        // can't assist player which is dueling someone
                        if (selfPlayerOwner != targetPlayerOwner && targetPlayerOwner.duel != null)
                            return false;
                    }
                    // can't assist player in ffa_pvp zone from outside
                    if (target.HasPvpFlag(UnitPVPStateFlags.FFAPvp) && !HasPvpFlag(UnitPVPStateFlags.FFAPvp))
                        return false;
                    // can't assist player out of sanctuary from sanctuary if has pvp enabled
                    if (target.HasPvpFlag(UnitPVPStateFlags.PvP))
                        if (IsInSanctuary() && !target.IsInSanctuary())
                            return false;
                }
            }
            // PvC case - player can assist creature only if has specific type flags
            else if (HasUnitFlag(UnitFlags.PvpAttackable)
                && (bySpell == null || !bySpell.HasAttribute(SpellAttr6.AssistIgnoreImmuneFlag))
                && !target.HasPvpFlag(UnitPVPStateFlags.PvP))
            {
                Creature creatureTarget = target.ToCreature();
                if (creatureTarget != null)
                    return Convert.ToBoolean(creatureTarget.GetCreatureTemplate().TypeFlags & CreatureTypeFlags.TreatAsRaidUnit)
                        || Convert.ToBoolean(creatureTarget.GetCreatureTemplate().TypeFlags & CreatureTypeFlags.CanAssist);
            }
            return true;
        }

        public virtual bool CheckAttackFitToAuraRequirement(WeaponAttackType attackType, AuraEffect aurEff) => true;

        public void ApplyAttackTimePercentMod(WeaponAttackType att, float val, bool apply)
        {
            float remainingTimePct = m_attackTimer[(int)att] / (m_baseAttackSpeed[(int)att] * m_modAttackSpeedPct[(int)att]);
            if (val > 0.0f)
            {
                MathFunctions.ApplyPercentModFloatVar(ref m_modAttackSpeedPct[(int)att], val, !apply);

                if (att == WeaponAttackType.BaseAttack)
                    ApplyModUpdateField<float>(UnitFields.ModHaste, val, !apply);
                else if (att == WeaponAttackType.RangedAttack)
                    ApplyModUpdateField<float>(UnitFields.ModRangedHaste, val, !apply);
            }
            else
            {
                MathFunctions.ApplyPercentModFloatVar(ref m_modAttackSpeedPct[(int)att], -val, apply);

                if (att == WeaponAttackType.BaseAttack)
                    ApplyModUpdateField<float>(UnitFields.ModHaste, -val, apply);
                else if (att == WeaponAttackType.RangedAttack)
                    ApplyModUpdateField<float>(UnitFields.ModRangedHaste, -val, apply);
            }

            UpdateAttackTimeField(att);
            m_attackTimer[(int)att] = (uint)(m_baseAttackSpeed[(int)att] * m_modAttackSpeedPct[(int)att] * remainingTimePct);
        }

        public float GetModCastingSpeed() => GetModCastingSpeed();
    }
}
