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

using Framework.Collections;
using Framework.Constants;
using Framework.Dynamic;
using Game.AI;
using Game.Combat;
using Game.DataStorage;
using Game.Maps;
using Game.Movement;
using Game.Networking;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;

namespace Game.Entities
{
    public partial class Unit
    {
        //AI
        protected UnitAI i_AI;
        protected UnitAI i_disabledAI;
        public bool IsAIEnabled { get; set; }
        public bool NeedChangeAI { get; set; }

        //Movement
        protected float[] m_speed_rate = new float[(int)UnitMoveType.Max];
        RefManager<Unit, ITargetedMovementGeneratorBase> m_FollowingRefManager;
        public MoveSpline MoveSpline { get; set; }
        MotionMaster i_motionMaster;
        public uint m_movementCounter;       //< Incrementing counter used in movement packets
        TimeTrackerSmall movesplineTimer;
        public Player m_playerMovingMe;
        MovementForces _movementForces;

        //Combat
        protected List<Unit> attackerList = new();
        Dictionary<ReactiveType, uint> m_reactiveTimer = new();
        protected float[][] m_weaponDamage = new float[(int)WeaponAttackType.Max][];

        uint[] m_baseAttackSpeed = new uint[(int)WeaponAttackType.Max];
        float[] m_modAttackSpeedPct = new float[(int)WeaponAttackType.Max];
        protected uint[] m_attackTimer = new uint[(int)WeaponAttackType.Max];

        CombatManager m_combatManager;
        ThreatManager m_threatManager;

        protected Unit attacking;

        public float ModMeleeHitChance { get; set; }
        public float ModRangedHitChance { get; set; }
        public float ModSpellHitChance { get; set; }
        public bool m_canDualWield;
        public int BaseSpellCritChance { get; set; }
        public uint RegenTimer { get; set; }
        uint combatTimer;
        public uint ExtraAttacks { get; set; }

        //Charm
        public List<Unit> m_Controlled = new();
        List<Player> m_sharedVision = new();
        CharmInfo m_charmInfo;
        protected bool m_ControlledByPlayer;
        public ObjectGuid LastCharmerGUID { get; set; }

        uint _oldFactionId;         // faction before charm
        bool _isWalkingBeforeCharm; // Are we walking before we were charmed?

        //Spells 
        protected Dictionary<CurrentSpellTypes, Spell> m_currentSpells = new((int)CurrentSpellTypes.Max);
        MultiMap<uint, uint>[] m_spellImmune = new MultiMap<uint, uint>[(int)SpellImmunity.Max];
        SpellAuraInterruptFlags m_interruptMask;
        SpellAuraInterruptFlags2 m_interruptMask2;
        protected int m_procDeep;
        bool m_AutoRepeatFirstCast;
        SpellHistory _spellHistory;

        //Auras
        MultiMap<AuraType, AuraEffect> m_modAuras = new();
        List<Aura> m_removedAuras = new();
        List<AuraApplication> m_interruptableAuras = new();             // auras which have interrupt mask applied on unit
        MultiMap<AuraStateType, AuraApplication> m_auraStateAuras = new();        // Used for improve performance of aura state checks on aura apply/remove
        SortedSet<AuraApplication> m_visibleAuras = new(new VisibleAuraSlotCompare());
        SortedSet<AuraApplication> m_visibleAurasToUpdate = new(new VisibleAuraSlotCompare());
        MultiMap<uint, AuraApplication> m_appliedAuras = new();
        MultiMap<uint, Aura> m_ownedAuras = new();
        List<Aura> m_scAuras = new();
        protected float[][] m_auraFlatModifiersGroup = new float[(int)UnitMods.End][];
        protected float[][] m_auraPctModifiersGroup = new float[(int)UnitMods.End][];
        uint m_removedAurasCount;

        //General  
        public UnitData m_unitData;

        DiminishingReturn[] m_Diminishing = new DiminishingReturn[(int)DiminishingGroup.Max];
        protected List<GameObject> m_gameObj = new();
        List<AreaTrigger> m_areaTrigger = new();
        protected List<DynamicObject> m_dynObj = new();
        protected float[] CreateStats = new float[(int)Stats.Max];
        float[] m_floatStatPosBuff = new float[(int)Stats.Max];
        float[] m_floatStatNegBuff = new float[(int)Stats.Max];
        public ObjectGuid[] m_SummonSlot = new ObjectGuid[7];
        public ObjectGuid[] m_ObjectSlot = new ObjectGuid[4];
        public EventSystem m_Events = new();
        public UnitTypeMask UnitTypeMask { get; set; }
        UnitState m_state;
        protected LiquidTypeRecord _lastLiquid;
        protected DeathState m_deathState;
        public Vehicle m_vehicle { get; set; }
        public Vehicle VehicleKit { get; set; }
        bool canModifyStats;
        public uint LastSanctuaryTime { get; set; }
        uint m_transform;
        bool m_cleanupDone; // lock made to not add stuff after cleanup before delete
        bool m_duringRemoveFromWorld; // lock made to not add stuff after begining removing from world
        bool _instantCast;

        ushort _aiAnimKitId;
        ushort _movementAnimKitId;
        ushort _meleeAnimKitId;
    }

    public struct DiminishingReturn
    {
        public DiminishingReturn(uint hitTime, DiminishingLevels hitCount)
        {
            Stack = 0;
            HitTime = hitTime;
            HitCount = hitCount;
        }

        public void Clear()
        {
            Stack = 0;
            HitTime = 0;
            HitCount = DiminishingLevels.Level1;
        }

        public uint Stack;
        public uint HitTime;
        public DiminishingLevels HitCount;
    }

    public class ProcEventInfo
    {
        public ProcEventInfo(Unit actor, Unit actionTarget, Unit procTarget, ProcFlags typeMask, ProcFlagsSpellType spellTypeMask,
            ProcFlagsSpellPhase spellPhaseMask, ProcFlagsHit hitMask, Spell spell, DamageInfo damageInfo, HealInfo healInfo)
        {
            _actor = actor;
            _actionTarget = actionTarget;
            _procTarget = procTarget;
            _typeMask = typeMask;
            _spellTypeMask = spellTypeMask;
            _spellPhaseMask = spellPhaseMask;
            _hitMask = hitMask;
            _spell = spell;
            _damageInfo = damageInfo;
            _healInfo = healInfo;
        }

        public Unit GetActor() { return _actor; }
        public Unit GetActionTarget() { return _actionTarget; }
        public Unit GetProcTarget() { return _procTarget; }

        public ProcFlags GetTypeMask() { return _typeMask; }
        public ProcFlagsSpellType GetSpellTypeMask() { return _spellTypeMask; }
        public ProcFlagsSpellPhase GetSpellPhaseMask() { return _spellPhaseMask; }
        public ProcFlagsHit GetHitMask() { return _hitMask; }

        public SpellInfo GetSpellInfo()
        {
            if (_spell)
                return _spell.GetSpellInfo();
            if (_damageInfo != null)
                return _damageInfo.GetSpellInfo();
            if (_healInfo != null)
                return _healInfo.GetSpellInfo();

            return null;
        }
        public SpellSchoolMask GetSchoolMask()
        {
            if (_spell)
                return _spell.GetSpellInfo().GetSchoolMask();
            if (_damageInfo != null)
                return _damageInfo.GetSchoolMask();
            if (_healInfo != null)
                return _healInfo.GetSchoolMask();

            return SpellSchoolMask.None;
        }

        public DamageInfo GetDamageInfo() { return _damageInfo; }
        public HealInfo GetHealInfo() { return _healInfo; }

        public Spell GetProcSpell() { return _spell; }

        Unit _actor;
        Unit _actionTarget;
        Unit _procTarget;
        ProcFlags _typeMask;
        ProcFlagsSpellType _spellTypeMask;
        ProcFlagsSpellPhase _spellPhaseMask;
        ProcFlagsHit _hitMask;
        Spell _spell;
        DamageInfo _damageInfo;
        HealInfo _healInfo;
    }

    public class DamageInfo
    {
        public DamageInfo(Unit attacker, Unit victim, uint damage, SpellInfo spellInfo, SpellSchoolMask schoolMask, DamageEffectType damageType, WeaponAttackType attackType)
        {
            m_attacker = attacker;
            m_victim = victim;
            m_damage = damage;
            m_originalDamage = damage;
            m_spellInfo = spellInfo;
            m_schoolMask = schoolMask;
            m_damageType = damageType;
            m_attackType = attackType;
        }

        public DamageInfo(CalcDamageInfo dmgInfo)
        {
            m_attacker = dmgInfo.Attacker;
            m_victim = dmgInfo.Target;
            m_damage = dmgInfo.Damage;
            m_originalDamage = dmgInfo.Damage;
            m_spellInfo = null;
            m_schoolMask = (SpellSchoolMask)dmgInfo.DamageSchoolMask;
            m_damageType = DamageEffectType.Direct;
            m_attackType = dmgInfo.AttackType;
            m_absorb = dmgInfo.Absorb;
            m_resist = dmgInfo.Resist;
            m_block = dmgInfo.Blocked;

            switch (dmgInfo.TargetState)
            {
                case VictimState.Immune:
                    m_hitMask |= ProcFlagsHit.Immune;
                    break;
                case VictimState.Blocks:
                    m_hitMask |= ProcFlagsHit.FullBlock;
                    break;
            }

            if (dmgInfo.HitInfo.HasAnyFlag(HitInfo.PartialAbsorb | HitInfo.FullAbsorb))
                m_hitMask |= ProcFlagsHit.Absorb;

            if (dmgInfo.HitInfo.HasAnyFlag(HitInfo.FullResist))
                m_hitMask |= ProcFlagsHit.FullResist;

            if (m_block != 0)
                m_hitMask |= ProcFlagsHit.Block;

            bool damageNullified = dmgInfo.HitInfo.HasAnyFlag(HitInfo.FullAbsorb | HitInfo.FullResist) || m_hitMask.HasAnyFlag(ProcFlagsHit.Immune | ProcFlagsHit.FullBlock);
            switch (dmgInfo.HitOutCome)
            {
                case MeleeHitOutcome.Miss:
                    m_hitMask |= ProcFlagsHit.Miss;
                    break;
                case MeleeHitOutcome.Dodge:
                    m_hitMask |= ProcFlagsHit.Dodge;
                    break;
                case MeleeHitOutcome.Parry:
                    m_hitMask |= ProcFlagsHit.Parry;
                    break;
                case MeleeHitOutcome.Evade:
                    m_hitMask |= ProcFlagsHit.Evade;
                    break;
                case MeleeHitOutcome.Block:
                case MeleeHitOutcome.Crushing:
                case MeleeHitOutcome.Glancing:
                case MeleeHitOutcome.Normal:
                    if (!damageNullified)
                        m_hitMask |= ProcFlagsHit.Normal;
                    break;
                case MeleeHitOutcome.Crit:
                    if (!damageNullified)
                        m_hitMask |= ProcFlagsHit.Critical;
                    break;
            }
        }

        public DamageInfo(SpellNonMeleeDamage spellNonMeleeDamage, DamageEffectType damageType, WeaponAttackType attackType, ProcFlagsHit hitMask)
        {
            m_attacker = spellNonMeleeDamage.attacker;
            m_victim = spellNonMeleeDamage.target;
            m_damage = spellNonMeleeDamage.damage;
            m_spellInfo = spellNonMeleeDamage.Spell;
            m_schoolMask = spellNonMeleeDamage.schoolMask;
            m_damageType = damageType;
            m_attackType = attackType;
            m_absorb = spellNonMeleeDamage.absorb;
            m_resist = spellNonMeleeDamage.resist;
            m_block = spellNonMeleeDamage.blocked;
            m_hitMask = hitMask;

            if (spellNonMeleeDamage.blocked != 0)
                m_hitMask |= ProcFlagsHit.Block;
            if (spellNonMeleeDamage.absorb != 0)
                m_hitMask |= ProcFlagsHit.Absorb;
        }

        public void ModifyDamage(int amount)
        {
            amount = Math.Max(amount, -((int)GetDamage()));
            m_damage += (uint)amount;
        }
        public void AbsorbDamage(uint amount)
        {
            amount = Math.Min(amount, GetDamage());
            m_absorb += amount;
            m_damage -= amount;
            m_hitMask |= ProcFlagsHit.Absorb;
        }
        public void ResistDamage(uint amount)
        {
            amount = Math.Min(amount, GetDamage());
            m_resist += amount;
            m_damage -= amount;
            if (m_damage == 0)
            { 
                m_hitMask |= ProcFlagsHit.FullResist;
                m_hitMask &= ~(ProcFlagsHit.Normal | ProcFlagsHit.Critical);
            }
        }
        void BlockDamage(uint amount)
        {
            amount = Math.Min(amount, GetDamage());
            m_block += amount;
            m_damage -= amount;
            m_hitMask |= ProcFlagsHit.Block;
            if (m_damage == 0)
            { 
                m_hitMask |= ProcFlagsHit.FullBlock;
                m_hitMask &= ~(ProcFlagsHit.Normal | ProcFlagsHit.Critical);
            }
        }

        public Unit GetAttacker() { return m_attacker; }
        public Unit GetVictim() { return m_victim; }
        public SpellInfo GetSpellInfo() { return m_spellInfo; }
        public SpellSchoolMask GetSchoolMask() { return m_schoolMask; }
        DamageEffectType GetDamageType() { return m_damageType; }
        public WeaponAttackType GetAttackType() { return m_attackType; }
        public uint GetDamage() { return m_damage; }
        public uint GetOriginalDamage() { return m_originalDamage; }
        public uint GetAbsorb() { return m_absorb; }
        public uint GetResist() { return m_resist; }
        uint GetBlock() { return m_block; }
        public ProcFlagsHit GetHitMask() { return m_hitMask; }

        Unit m_attacker;
        Unit m_victim;
        uint m_damage;
        uint m_originalDamage;
        SpellInfo m_spellInfo;
        SpellSchoolMask m_schoolMask;
        DamageEffectType m_damageType;
        WeaponAttackType m_attackType;
        uint m_absorb;
        uint m_resist;
        uint m_block;
        ProcFlagsHit m_hitMask;
    }

    public class HealInfo
    {
        public HealInfo(Unit healer, Unit target, uint heal, SpellInfo spellInfo, SpellSchoolMask schoolMask)
        {
            _healer = healer;
            _target = target;
            _heal = heal;
            _originalHeal = heal;
            _spellInfo = spellInfo;
            _schoolMask = schoolMask;
        }

        public void AbsorbHeal(uint amount)
        {
            amount = Math.Min(amount, GetHeal());
            _absorb += amount;
            _heal -= amount;
            amount = Math.Min(amount, GetEffectiveHeal());
            _effectiveHeal -= amount;
            _hitMask |= ProcFlagsHit.Absorb;
        }
        public void SetEffectiveHeal(uint amount) { _effectiveHeal = amount; }

        public Unit GetHealer() { return _healer; }
        public Unit GetTarget() { return _target; }
        public uint GetHeal() { return _heal; }
        public uint GetOriginalHeal() { return _originalHeal; }
        public uint GetEffectiveHeal() { return _effectiveHeal; }
        public uint GetAbsorb() { return _absorb; }
        public SpellInfo GetSpellInfo() { return _spellInfo; }
        public SpellSchoolMask GetSchoolMask() { return _schoolMask; }
        ProcFlagsHit GetHitMask() { return _hitMask; }

        Unit _healer;
        Unit _target;
        uint _heal;
        uint _originalHeal;
        uint _effectiveHeal;
        uint _absorb;
        SpellInfo _spellInfo;
        SpellSchoolMask _schoolMask;
        ProcFlagsHit _hitMask;
    }

    public class CalcDamageInfo
    {
        public Unit Attacker { get; set; }             // Attacker
        public Unit Target { get; set; }               // Target for damage
        public uint DamageSchoolMask { get; set; }
        public uint Damage;
        public uint OriginalDamage { get; set; }
        public uint Absorb;
        public uint Resist { get; set; }
        public uint Blocked { get; set; }
        public HitInfo HitInfo { get; set; }
        public VictimState TargetState { get; set; }

        // Helper
        public WeaponAttackType AttackType { get; set; }
        public ProcFlags ProcAttacker { get; set; }
        public ProcFlags ProcVictim { get; set; }
        public uint CleanDamage { get; set; }        // Used only for rage calculation
        public MeleeHitOutcome HitOutCome { get; set; }  // TODO: remove this field (need use TargetState)
    }

    public class SpellNonMeleeDamage
    {
        public SpellNonMeleeDamage(Unit _attacker, Unit _target, SpellInfo _spellInfo, SpellCastVisual spellVisual, SpellSchoolMask _schoolMask, ObjectGuid _castId = default)
        {
            target = _target;
            attacker = _attacker;
            Spell = _spellInfo;
            SpellVisual = spellVisual;
            schoolMask = _schoolMask;
            castId = _castId;
            preHitHealth = (uint)_target.GetHealth();
        }

        public Unit target;
        public Unit attacker;
        public ObjectGuid castId;
        public SpellInfo Spell;
        public SpellCastVisual SpellVisual;
        public uint damage;
        public uint originalDamage;
        public SpellSchoolMask schoolMask;
        public uint absorb;
        public uint resist;
        public bool periodicLog;
        public uint blocked;
        public HitInfo HitInfo;
        // Used for help
        public uint cleanDamage;
        public bool fullBlock;
        public uint preHitHealth;
    }

    public class CleanDamage
    {
        public CleanDamage(uint mitigated, uint absorbed, WeaponAttackType _attackType, MeleeHitOutcome _hitOutCome)
        {
            absorbed_damage = absorbed;
            mitigated_damage = mitigated;
            attackType = _attackType;
            hitOutCome = _hitOutCome;
        }

        public uint absorbed_damage { get; }
        public uint mitigated_damage { get; set; }

        public WeaponAttackType attackType { get; }
        public MeleeHitOutcome hitOutCome { get; }
    }

    public class DispelInfo
    {
        public DispelInfo(Unit dispeller, uint dispellerSpellId, byte chargesRemoved)
        {
            _dispellerUnit = dispeller;
            _dispellerSpell = dispellerSpellId;
            _chargesRemoved = chargesRemoved;
        }

        public Unit GetDispeller() { return _dispellerUnit; }
        uint GetDispellerSpellId() { return _dispellerSpell; }
        public byte GetRemovedCharges() { return _chargesRemoved; }
        public void SetRemovedCharges(byte amount)
        {
            _chargesRemoved = amount;
        }

        Unit _dispellerUnit;
        uint _dispellerSpell;
        byte _chargesRemoved;
    }

    public class SpellPeriodicAuraLogInfo
    {
        public SpellPeriodicAuraLogInfo(AuraEffect _auraEff, uint _damage, uint _originalDamage, uint _overDamage, uint _absorb, uint _resist, float _multiplier, bool _critical)
        {
            auraEff = _auraEff;
            damage = _damage;
            originalDamage = _originalDamage;
            overDamage = _overDamage;
            absorb = _absorb;
            resist = _resist;
            multiplier = _multiplier;
            critical = _critical;
        }

        public AuraEffect auraEff;
        public uint damage;
        public uint originalDamage;
        public uint overDamage;                                      // overkill/overheal
        public uint absorb;
        public uint resist;
        public float multiplier;
        public bool critical;
    }

    class VisibleAuraSlotCompare : IComparer<AuraApplication>
    {
        public int Compare(AuraApplication x, AuraApplication y)
        {
            return x.GetSlot().CompareTo(y.GetSlot());
        }
    }

    public class DeclinedName
    {
        public StringArray name = new(SharedConst.MaxDeclinedNameCases);
    }

    class CombatLogSender : IDoWork<Player>
    {
        CombatLogServerPacket i_message;

        public CombatLogSender(CombatLogServerPacket msg)
        {
            i_message = msg;
        }

        public void Invoke(Player player)
        {
            i_message.Clear();
            i_message.SetAdvancedCombatLogging(player.IsAdvancedCombatLoggingEnabled());

            player.SendPacket(i_message);
        }
    }
}
