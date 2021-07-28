/*
 * Copyright (C) 2012-2021 CypherCore <http://github.com/CypherCore>
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

namespace Framework.Constants
{
    public enum ObjectFields
    {
        Guid                                                              = 0x0000,      // Size: 4, Flags: (All)
        EntryID                                                           = 0x0004,      // Size: 1, Flags: (ViewerDependent)
        DynamicFlags                                                      = 0x0005,      // Size: 1, Flags: (ViewerDependent, Urgent)
        Scale                                                             = 0x0006,      // Size: 1, Flags: (All)
        End                                                               = 0x0007,
    }

    public enum ObjectDynamicFields
    {
        End                                                               = 0x0000,
    }

    public enum ItemFields
    {
        Owner                                                             = ObjectFields.End + 0x0000,      // Size: 4, Flags: (All)
        ContainedIn                                                       = ObjectFields.End + 0x0004,      // Size: 4, Flags: (All)
        Creator                                                           = ObjectFields.End + 0x0008,      // Size: 4, Flags: (All)
        GiftCreator                                                       = ObjectFields.End + 0x000C,      // Size: 4, Flags: (All)
        StackCount                                                        = ObjectFields.End + 0x0010,      // Size: 1, Flags: (Owner)
        Expiration                                                        = ObjectFields.End + 0x0011,      // Size: 1, Flags: (Owner)
        SpellCharges                                                      = ObjectFields.End + 0x0012,      // Size: 5, Flags: (Owner)
        DynamicFlags                                                      = ObjectFields.End + 0x0017,      // Size: 1, Flags: (All)
        Enchantment                                                       = ObjectFields.End + 0x0018,      // Size: 39, Flags: (All)
        PropertySeed                                                      = ObjectFields.End + 0x003F,      // Size: 1, Flags: (All)
        RandomPropertiesID                                                = ObjectFields.End + 0x0040,      // Size: 1, Flags: (All)
        Durability                                                        = ObjectFields.End + 0x0041,      // Size: 1, Flags: (Owner)
        MaxDurability                                                     = ObjectFields.End + 0x0042,      // Size: 1, Flags: (Owner)
        CreatePlayedTime                                                  = ObjectFields.End + 0x0043,      // Size: 1, Flags: (All)
        ModifiersMask                                                     = ObjectFields.End + 0x0044,      // Size: 1, Flags: (Owner)
        Context                                                           = ObjectFields.End + 0x0045,      // Size: 1, Flags: (All)
        ArtifactXP                                                        = ObjectFields.End + 0x0046,      // Size: 2, Flags: (Owner)
        ItemAppearanceModID                                               = ObjectFields.End + 0x0048,      // Size: 1, Flags: (Owner)
        End                                                               = ObjectFields.End + 0x0049,
    }

    public enum ItemDynamicFields
    {
        Modifiers                                                         = ObjectDynamicFields.End + 0x0000,      // Flags: (Owner)
        BonusListIDs                                                      = ObjectDynamicFields.End + 0x0001,      // Flags: (Owner, Unk0x100)
        ArtifactPowers                                                    = ObjectDynamicFields.End + 0x0002,      // Flags: (Owner)
        Gems                                                              = ObjectDynamicFields.End + 0x0003,      // Flags: (Owner)
        End                                                               = ObjectDynamicFields.End + 0x0004,
    }

    public enum ContainerFields
    {
        Slots                                                             = ItemFields.End + 0x0000,      // Size: 144, Flags: (All)
        NumSlots                                                          = ItemFields.End + 0x0090,      // Size: 1, Flags: (All)
        End                                                               = ItemFields.End + 0x0091,
    }

    public enum ContainerDynamicFields
    {
        End                                                               = ItemDynamicFields.End + 0x0000,
    }

    public enum UnitFields
    {
        Charm                                                             = ObjectFields.End + 0x0000,      // Size: 4, Flags: (All)
        Summon                                                            = ObjectFields.End + 0x0004,      // Size: 4, Flags: (All)
        Critter                                                           = ObjectFields.End + 0x0008,      // Size: 4, Flags: (Self)
        CharmedBy                                                         = ObjectFields.End + 0x000C,      // Size: 4, Flags: (All)
        SummonedBy                                                        = ObjectFields.End + 0x0010,      // Size: 4, Flags: (All)
        CreatedBy                                                         = ObjectFields.End + 0x0014,      // Size: 4, Flags: (All)
        DemonCreator                                                      = ObjectFields.End + 0x0018,      // Size: 4, Flags: (All)
        LookAtControllerTarget                                            = ObjectFields.End + 0x001C,      // Size: 4, Flags: (All)
        Target                                                            = ObjectFields.End + 0x0020,      // Size: 4, Flags: (All)
        BattlePetCompanionGUID                                            = ObjectFields.End + 0x0024,      // Size: 4, Flags: (All)
        BattlePetDBID                                                     = ObjectFields.End + 0x0028,      // Size: 2, Flags: (All)
        ChannelData                                                       = ObjectFields.End + 0x002A,      // Size: 2, Flags: (All, Urgent)
        SummonedByHomeRealm                                               = ObjectFields.End + 0x002C,      // Size: 1, Flags: (All)
        Bytes1                                                            = ObjectFields.End + 0x002D,      // Size: 1, Flags: (All) Nested: (Race, ClassId, PlayerClassId, Sex)
        DisplayPower                                                      = ObjectFields.End + 0x002E,      // Size: 1, Flags: (All)
        OverrideDisplayPowerID                                            = ObjectFields.End + 0x002F,      // Size: 1, Flags: (All)
        Health                                                            = ObjectFields.End + 0x0030,      // Size: 2, Flags: (ViewerDependent)
        Power                                                             = ObjectFields.End + 0x0032,      // Size: 6, Flags: (All, UrgentSelfOnly)
        MaxHealth                                                         = ObjectFields.End + 0x0038,      // Size: 2, Flags: (ViewerDependent)
        MaxPower                                                          = ObjectFields.End + 0x003A,      // Size: 6, Flags: (All)
        ModPowerRegen                                                     = ObjectFields.End + 0x0040,      // Size: 6, Flags: (Self, Owner, UnitAll)
        Level                                                             = ObjectFields.End + 0x0046,      // Size: 1, Flags: (All)
        EffectiveLevel                                                    = ObjectFields.End + 0x0047,      // Size: 1, Flags: (All)
        ContentTuningID                                                   = ObjectFields.End + 0x0048,      // Size: 1, Flags: (All)
        ScalingLevelMin                                                   = ObjectFields.End + 0x0049,      // Size: 1, Flags: (All)
        ScalingLevelMax                                                   = ObjectFields.End + 0x004A,      // Size: 1, Flags: (All)
        ScalingLevelDelta                                                 = ObjectFields.End + 0x004B,      // Size: 1, Flags: (All)
        ScalingFactionGroup                                               = ObjectFields.End + 0x004C,      // Size: 1, Flags: (All)
        ScalingHealthItemLevelCurveID                                     = ObjectFields.End + 0x004D,      // Size: 1, Flags: (All)
        ScalingDamageItemLevelCurveID                                     = ObjectFields.End + 0x004E,      // Size: 1, Flags: (All)
        FactionTemplate                                                   = ObjectFields.End + 0x004F,      // Size: 1, Flags: (All)
        VirtualItems                                                      = ObjectFields.End + 0x0050,      // Size: 6, Flags: (All)
        Flags                                                             = ObjectFields.End + 0x0056,      // Size: 1, Flags: (All, Urgent)
        Flags2                                                            = ObjectFields.End + 0x0057,      // Size: 1, Flags: (All, Urgent)
        Flags3                                                            = ObjectFields.End + 0x0058,      // Size: 1, Flags: (All, Urgent)
        AuraState                                                         = ObjectFields.End + 0x0059,      // Size: 1, Flags: (All)
        AttackRoundBaseTime                                               = ObjectFields.End + 0x005A,      // Size: 2, Flags: (All)
        RangedAttackRoundBaseTime                                         = ObjectFields.End + 0x005C,      // Size: 1, Flags: (Self)
        BoundingRadius                                                    = ObjectFields.End + 0x005D,      // Size: 1, Flags: (All)
        CombatReach                                                       = ObjectFields.End + 0x005E,      // Size: 1, Flags: (All)
        DisplayID                                                         = ObjectFields.End + 0x005F,      // Size: 1, Flags: (ViewerDependent, Urgent)
        DisplayScale                                                      = ObjectFields.End + 0x0060,      // Size: 1, Flags: (ViewerDependent, Urgent)
        NativeDisplayID                                                   = ObjectFields.End + 0x0061,      // Size: 1, Flags: (All, Urgent)
        NativeXDisplayScale                                               = ObjectFields.End + 0x0062,      // Size: 1, Flags: (All, Urgent)
        MountDisplayID                                                    = ObjectFields.End + 0x0063,      // Size: 1, Flags: (All, Urgent)
        MinDamage                                                         = ObjectFields.End + 0x0064,      // Size: 1, Flags: (Self, Owner, Empath)
        MaxDamage                                                         = ObjectFields.End + 0x0065,      // Size: 1, Flags: (Self, Owner, Empath)
        MinOffHandDamage                                                  = ObjectFields.End + 0x0066,      // Size: 1, Flags: (Self, Owner, Empath)
        MaxOffHandDamage                                                  = ObjectFields.End + 0x0067,      // Size: 1, Flags: (Self, Owner, Empath)
        Bytes2                                                            = ObjectFields.End + 0x0068,      // Size: 1, Flags: (All) Nested: (StandState, PetLoyaltyIndex, VisFlags, AnimTier)
        PetNumber                                                         = ObjectFields.End + 0x0069,      // Size: 1, Flags: (All)
        PetNameTimestamp                                                  = ObjectFields.End + 0x006A,      // Size: 1, Flags: (All)
        PetExperience                                                     = ObjectFields.End + 0x006B,      // Size: 1, Flags: (Owner)
        PetNextLevelExperience                                            = ObjectFields.End + 0x006C,      // Size: 1, Flags: (Owner)
        ModCastingSpeed                                                   = ObjectFields.End + 0x006D,      // Size: 1, Flags: (All)
        ModSpellHaste                                                     = ObjectFields.End + 0x006E,      // Size: 1, Flags: (All)
        ModHaste                                                          = ObjectFields.End + 0x006F,      // Size: 1, Flags: (All)
        ModRangedHaste                                                    = ObjectFields.End + 0x0070,      // Size: 1, Flags: (All)
        ModHasteRegen                                                     = ObjectFields.End + 0x0071,      // Size: 1, Flags: (All)
        ModTimeRate                                                       = ObjectFields.End + 0x0072,      // Size: 1, Flags: (All)
        CreatedBySpell                                                    = ObjectFields.End + 0x0073,      // Size: 1, Flags: (All)
        NpcFlags                                                          = ObjectFields.End + 0x0074,      // Size: 2, Flags: (All, ViewerDependent)
        EmoteState                                                        = ObjectFields.End + 0x0076,      // Size: 1, Flags: (All)
        Bytes3                                                            = ObjectFields.End + 0x0077,      // Size: 1, Flags: (Owner) Nested: (TrainingPointsUsed, TrainingPointsTotal)
        Stats                                                             = ObjectFields.End + 0x0078,      // Size: 5, Flags: (Self, Owner)
        StatPosBuff                                                       = ObjectFields.End + 0x007D,      // Size: 5, Flags: (Self, Owner)
        StatNegBuff                                                       = ObjectFields.End + 0x0082,      // Size: 5, Flags: (Self, Owner)
        Resistances                                                       = ObjectFields.End + 0x0087,      // Size: 7, Flags: (Self, Owner, Empath)
        ResistanceBuffModsPositive                                        = ObjectFields.End + 0x008E,      // Size: 7, Flags: (Self, Owner)
        ResistanceBuffModsNegative                                        = ObjectFields.End + 0x0095,      // Size: 7, Flags: (Self, Owner)
        BaseMana                                                          = ObjectFields.End + 0x009C,      // Size: 1, Flags: (All)
        BaseHealth                                                        = ObjectFields.End + 0x009D,      // Size: 1, Flags: (Self, Owner)
        Bytes4                                                            = ObjectFields.End + 0x009E,      // Size: 1, Flags: (All) Nested: (SheatheState, PvpFlags, PetFlags, ShapeshiftForm)
        AttackPower                                                       = ObjectFields.End + 0x009F,      // Size: 1, Flags: (Self, Owner)
        AttackPowerModPos                                                 = ObjectFields.End + 0x00A0,      // Size: 1, Flags: (Self, Owner)
        AttackPowerModNeg                                                 = ObjectFields.End + 0x00A1,      // Size: 1, Flags: (Self, Owner)
        AttackPowerMultiplier                                             = ObjectFields.End + 0x00A2,      // Size: 1, Flags: (Self, Owner)
        RangedAttackPower                                                 = ObjectFields.End + 0x00A3,      // Size: 1, Flags: (Self, Owner)
        RangedAttackPowerModPos                                           = ObjectFields.End + 0x00A4,      // Size: 1, Flags: (Self, Owner)
        RangedAttackPowerModNeg                                           = ObjectFields.End + 0x00A5,      // Size: 1, Flags: (Self, Owner)
        RangedAttackPowerMultiplier                                       = ObjectFields.End + 0x00A6,      // Size: 1, Flags: (Self, Owner)
        SetAttackSpeedAura                                                = ObjectFields.End + 0x00A7,      // Size: 1, Flags: (Self, Owner)
        Lifesteal                                                         = ObjectFields.End + 0x00A8,      // Size: 1, Flags: (Self, Owner)
        MinRangedDamage                                                   = ObjectFields.End + 0x00A9,      // Size: 1, Flags: (Self, Owner)
        MaxRangedDamage                                                   = ObjectFields.End + 0x00AA,      // Size: 1, Flags: (Self, Owner)
        PowerCostModifier                                                 = ObjectFields.End + 0x00AB,      // Size: 7, Flags: (Self, Owner)
        PowerCostMultiplier                                               = ObjectFields.End + 0x00B2,      // Size: 7, Flags: (Self, Owner)
        MaxHealthModifier                                                 = ObjectFields.End + 0x00B9,      // Size: 1, Flags: (Self, Owner)
        HoverHeight                                                       = ObjectFields.End + 0x00BA,      // Size: 1, Flags: (All)
        MinItemLevelCutoff                                                = ObjectFields.End + 0x00BB,      // Size: 1, Flags: (All)
        MinItemLevel                                                      = ObjectFields.End + 0x00BC,      // Size: 1, Flags: (All)
        MaxItemLevel                                                      = ObjectFields.End + 0x00BD,      // Size: 1, Flags: (All)
        WildBattlePetLevel                                                = ObjectFields.End + 0x00BE,      // Size: 1, Flags: (All)
        BattlePetCompanionNameTimestamp                                   = ObjectFields.End + 0x00BF,      // Size: 1, Flags: (All)
        InteractSpellID                                                   = ObjectFields.End + 0x00C0,      // Size: 1, Flags: (All)
        StateSpellVisualID                                                = ObjectFields.End + 0x00C1,      // Size: 1, Flags: (ViewerDependent, Urgent)
        StateAnimID                                                       = ObjectFields.End + 0x00C2,      // Size: 1, Flags: (ViewerDependent, Urgent)
        StateAnimKitID                                                    = ObjectFields.End + 0x00C3,      // Size: 1, Flags: (ViewerDependent, Urgent)
        StateWorldEffectID                                                = ObjectFields.End + 0x00C4,      // Size: 4, Flags: (ViewerDependent, Urgent)
        ScaleDuration                                                     = ObjectFields.End + 0x00C8,      // Size: 1, Flags: (All)
        LooksLikeMountID                                                  = ObjectFields.End + 0x00C9,      // Size: 1, Flags: (All)
        LooksLikeCreatureID                                               = ObjectFields.End + 0x00CA,      // Size: 1, Flags: (All)
        LookAtControllerID                                                = ObjectFields.End + 0x00CB,      // Size: 1, Flags: (All)
        GuildGUID                                                         = ObjectFields.End + 0x00CC,      // Size: 4, Flags: (All)
        End                                                               = ObjectFields.End + 0x00D0,
    }

    public enum UnitDynamicFields
    {
        PassiveSpells                                                     = ObjectDynamicFields.End + 0x0000,      // Flags: (All, Urgent)
        WorldEffects                                                      = ObjectDynamicFields.End + 0x0001,      // Flags: (All, Urgent)
        ChannelObjects                                                    = ObjectDynamicFields.End + 0x0002,      // Flags: (All, Urgent)
        End                                                               = ObjectDynamicFields.End + 0x0003,
    }

    public enum PlayerFields
    {
        DuelArbiter                                                       = UnitFields.End + 0x0000,      // Size: 4, Flags: (All)
        WowAccount                                                        = UnitFields.End + 0x0004,      // Size: 4, Flags: (All)
        LootTargetGUID                                                    = UnitFields.End + 0x0008,      // Size: 4, Flags: (All)
        PlayerFlags                                                       = UnitFields.End + 0x000C,      // Size: 1, Flags: (All)
        PlayerFlagsEx                                                     = UnitFields.End + 0x000D,      // Size: 1, Flags: (All)
        GuildRankID                                                       = UnitFields.End + 0x000E,      // Size: 1, Flags: (All)
        GuildDeleteDate                                                   = UnitFields.End + 0x000F,      // Size: 1, Flags: (All)
        GuildLevel                                                        = UnitFields.End + 0x0010,      // Size: 1, Flags: (All)
        Bytes1                                                            = UnitFields.End + 0x0011,      // Size: 1, Flags: (All) Nested: (PartyType, NumBankSlots, NativeSex, Inebriation)
        Bytes2                                                            = UnitFields.End + 0x0012,      // Size: 1, Flags: (All) Nested: (PvpTitle, ArenaFaction, PvpRank)
        DuelTeam                                                          = UnitFields.End + 0x0013,      // Size: 1, Flags: (All)
        GuildTimeStamp                                                    = UnitFields.End + 0x0014,      // Size: 1, Flags: (All)
        QuestLog                                                          = UnitFields.End + 0x0015,      // Size: 400, Flags: (Party)
        VisibleItems                                                      = UnitFields.End + 0x01A5,      // Size: 38, Flags: (All)
        PlayerTitle                                                       = UnitFields.End + 0x01CB,      // Size: 1, Flags: (All)
        FakeInebriation                                                   = UnitFields.End + 0x01CC,      // Size: 1, Flags: (All)
        VirtualPlayerRealm                                                = UnitFields.End + 0x01CD,      // Size: 1, Flags: (All)
        CurrentSpecID                                                     = UnitFields.End + 0x01CE,      // Size: 1, Flags: (All)
        TaxiMountAnimKitID                                                = UnitFields.End + 0x01CF,      // Size: 1, Flags: (All)
        AvgItemLevel                                                      = UnitFields.End + 0x01D0,      // Size: 4, Flags: (All)
        CurrentBattlePetBreedQuality                                      = UnitFields.End + 0x01D4,      // Size: 1, Flags: (All)
        HonorLevel                                                        = UnitFields.End + 0x01D5,      // Size: 1, Flags: (All)
        CustomizationChoices                                              = UnitFields.End + 0x01D6,      // Size: 72, Flags: (All)
        End                                                               = UnitFields.End + 0x021E,
    }

    public enum PlayerDynamicFields
    {
        ArenaCooldowns                                                    = UnitDynamicFields.End + 0x0000,      // Flags: (All)
        End                                                               = UnitDynamicFields.End + 0x0001,
    }

    public enum ActivePlayerFields
    {
        InvSlots                                                          = PlayerFields.End + 0x0000,      // Size: 516, Flags: (All)
        FarsightObject                                                    = PlayerFields.End + 0x0204,      // Size: 4, Flags: (All)
        ComboTarget                                                       = PlayerFields.End + 0x0208,      // Size: 4, Flags: (All)
        SummonedBattlePetGUID                                             = PlayerFields.End + 0x020C,      // Size: 4, Flags: (All)
        KnownTitles                                                       = PlayerFields.End + 0x0210,      // Size: 12, Flags: (All)
        Coinage                                                           = PlayerFields.End + 0x021C,      // Size: 2, Flags: (All)
        XP                                                                = PlayerFields.End + 0x021E,      // Size: 1, Flags: (All)
        NextLevelXP                                                       = PlayerFields.End + 0x021F,      // Size: 1, Flags: (All)
        TrialXP                                                           = PlayerFields.End + 0x0220,      // Size: 1, Flags: (All)
        Skill                                                             = PlayerFields.End + 0x0221,      // Size: 896, Flags: (All)
        CharacterPoints                                                   = PlayerFields.End + 0x05A1,      // Size: 1, Flags: (All)
        MaxTalentTiers                                                    = PlayerFields.End + 0x05A2,      // Size: 1, Flags: (All)
        TrackCreatureMask                                                 = PlayerFields.End + 0x05A3,      // Size: 1, Flags: (All)
        TrackResourceMask                                                 = PlayerFields.End + 0x05A4,      // Size: 2, Flags: (All)
        MainhandExpertise                                                 = PlayerFields.End + 0x05A6,      // Size: 1, Flags: (All)
        OffhandExpertise                                                  = PlayerFields.End + 0x05A7,      // Size: 1, Flags: (All)
        RangedExpertise                                                   = PlayerFields.End + 0x05A8,      // Size: 1, Flags: (All)
        CombatRatingExpertise                                             = PlayerFields.End + 0x05A9,      // Size: 1, Flags: (All)
        BlockPercentage                                                   = PlayerFields.End + 0x05AA,      // Size: 1, Flags: (All)
        DodgePercentage                                                   = PlayerFields.End + 0x05AB,      // Size: 1, Flags: (All)
        DodgePercentageFromAttribute                                      = PlayerFields.End + 0x05AC,      // Size: 1, Flags: (All)
        ParryPercentage                                                   = PlayerFields.End + 0x05AD,      // Size: 1, Flags: (All)
        ParryPercentageFromAttribute                                      = PlayerFields.End + 0x05AE,      // Size: 1, Flags: (All)
        CritPercentage                                                    = PlayerFields.End + 0x05AF,      // Size: 1, Flags: (All)
        RangedCritPercentage                                              = PlayerFields.End + 0x05B0,      // Size: 1, Flags: (All)
        OffhandCritPercentage                                             = PlayerFields.End + 0x05B1,      // Size: 1, Flags: (All)
        SpellCritPercentage                                               = PlayerFields.End + 0x05B2,      // Size: 7, Flags: (All)
        ShieldBlock                                                       = PlayerFields.End + 0x05B9,      // Size: 1, Flags: (All)
        Mastery                                                           = PlayerFields.End + 0x05BA,      // Size: 1, Flags: (All)
        Speed                                                             = PlayerFields.End + 0x05BB,      // Size: 1, Flags: (All)
        Avoidance                                                         = PlayerFields.End + 0x05BC,      // Size: 1, Flags: (All)
        Sturdiness                                                        = PlayerFields.End + 0x05BD,      // Size: 1, Flags: (All)
        Versatility                                                       = PlayerFields.End + 0x05BE,      // Size: 1, Flags: (All)
        VersatilityBonus                                                  = PlayerFields.End + 0x05BF,      // Size: 1, Flags: (All)
        PvpPowerDamage                                                    = PlayerFields.End + 0x05C0,      // Size: 1, Flags: (All)
        PvpPowerHealing                                                   = PlayerFields.End + 0x05C1,      // Size: 1, Flags: (All)
        ExploredZones                                                     = PlayerFields.End + 0x05C2,      // Size: 384, Flags: (All)
        RestInfo                                                          = PlayerFields.End + 0x0742,      // Size: 4, Flags: (All)
        ModDamageDonePos                                                  = PlayerFields.End + 0x0746,      // Size: 7, Flags: (All)
        ModDamageDoneNeg                                                  = PlayerFields.End + 0x074D,      // Size: 7, Flags: (All)
        ModDamageDonePercent                                              = PlayerFields.End + 0x0754,      // Size: 7, Flags: (All)
        ModHealingDonePos                                                 = PlayerFields.End + 0x075B,      // Size: 1, Flags: (All)
        ModHealingPercent                                                 = PlayerFields.End + 0x075C,      // Size: 1, Flags: (All)
        ModHealingDonePercent                                             = PlayerFields.End + 0x075D,      // Size: 1, Flags: (All)
        ModPeriodicHealingDonePercent                                     = PlayerFields.End + 0x075E,      // Size: 1, Flags: (All)
        WeaponDmgMultipliers                                              = PlayerFields.End + 0x075F,      // Size: 3, Flags: (All)
        WeaponAtkSpeedMultipliers                                         = PlayerFields.End + 0x0762,      // Size: 3, Flags: (All)
        ModSpellPowerPercent                                              = PlayerFields.End + 0x0765,      // Size: 1, Flags: (All)
        ModResiliencePercent                                              = PlayerFields.End + 0x0766,      // Size: 1, Flags: (All)
        OverrideSpellPowerByAPPercent                                     = PlayerFields.End + 0x0767,      // Size: 1, Flags: (All)
        OverrideAPBySpellPowerPercent                                     = PlayerFields.End + 0x0768,      // Size: 1, Flags: (All)
        ModTargetResistance                                               = PlayerFields.End + 0x0769,      // Size: 1, Flags: (All)
        ModTargetPhysicalResistance                                       = PlayerFields.End + 0x076A,      // Size: 1, Flags: (All)
        LocalFlags                                                        = PlayerFields.End + 0x076B,      // Size: 1, Flags: (All)
        Bytes1                                                            = PlayerFields.End + 0x076C,      // Size: 1, Flags: (All) Nested: (GrantableLevels, MultiActionBars, LifetimeMaxRank, NumRespecs)
        AmmoID                                                            = PlayerFields.End + 0x076D,      // Size: 1, Flags: (All)
        PvpMedals                                                         = PlayerFields.End + 0x076E,      // Size: 1, Flags: (All)
        BuybackPrice                                                      = PlayerFields.End + 0x076F,      // Size: 12, Flags: (All)
        BuybackTimestamp                                                  = PlayerFields.End + 0x077B,      // Size: 12, Flags: (All)
        Bytes2                                                            = PlayerFields.End + 0x0787,      // Size: 1, Flags: (All) Nested: (TodayHonorableKills, YesterdayHonorableKills)
        Bytes3                                                            = PlayerFields.End + 0x0788,      // Size: 1, Flags: (All) Nested: (LastWeekHonorableKills, ThisWeekHonorableKills)
        ThisWeekContribution                                              = PlayerFields.End + 0x0789,      // Size: 1, Flags: (All)
        LifetimeHonorableKills                                            = PlayerFields.End + 0x078A,      // Size: 1, Flags: (All)
        YesterdayContribution                                             = PlayerFields.End + 0x078B,      // Size: 1, Flags: (All)
        LastWeekContribution                                              = PlayerFields.End + 0x078C,      // Size: 1, Flags: (All)
        LastWeekRank                                                      = PlayerFields.End + 0x078D,      // Size: 1, Flags: (All)
        WatchedFactionIndex                                               = PlayerFields.End + 0x078E,      // Size: 1, Flags: (All)
        CombatRatings                                                     = PlayerFields.End + 0x078F,      // Size: 32, Flags: (All)
        PvpInfo                                                           = PlayerFields.End + 0x07AF,      // Size: 72, Flags: (All)
        MaxLevel                                                          = PlayerFields.End + 0x07F7,      // Size: 1, Flags: (All)
        ScalingPlayerLevelDelta                                           = PlayerFields.End + 0x07F8,      // Size: 1, Flags: (All)
        MaxCreatureScalingLevel                                           = PlayerFields.End + 0x07F9,      // Size: 1, Flags: (All)
        NoReagentCostMask                                                 = PlayerFields.End + 0x07FA,      // Size: 4, Flags: (All)
        PetSpellPower                                                     = PlayerFields.End + 0x07FE,      // Size: 1, Flags: (All)
        ProfessionSkillLine                                               = PlayerFields.End + 0x07FF,      // Size: 2, Flags: (All)
        UiHitModifier                                                     = PlayerFields.End + 0x0801,      // Size: 1, Flags: (All)
        UiSpellHitModifier                                                = PlayerFields.End + 0x0802,      // Size: 1, Flags: (All)
        HomeRealmTimeOffset                                               = PlayerFields.End + 0x0803,      // Size: 1, Flags: (All)
        ModPetHaste                                                       = PlayerFields.End + 0x0804,      // Size: 1, Flags: (All)
        Bytes4                                                            = PlayerFields.End + 0x0805,      // Size: 1, Flags: (All) Nested: (LocalRegenFlags, AuraVision, NumBackpackSlots)
        OverrideSpellsID                                                  = PlayerFields.End + 0x0806,      // Size: 1, Flags: (All, UrgentSelfOnly)
        LfgBonusFactionID                                                 = PlayerFields.End + 0x0807,      // Size: 1, Flags: (All)
        LootSpecID                                                        = PlayerFields.End + 0x0808,      // Size: 1, Flags: (All)
        OverrideZonePVPType                                               = PlayerFields.End + 0x0809,      // Size: 1, Flags: (All, UrgentSelfOnly)
        BagSlotFlags                                                      = PlayerFields.End + 0x080A,      // Size: 4, Flags: (All)
        BankBagSlotFlags                                                  = PlayerFields.End + 0x080E,      // Size: 7, Flags: (All)
        QuestCompleted                                                    = PlayerFields.End + 0x0815,      // Size: 1750, Flags: (All)
        Honor                                                             = PlayerFields.End + 0x0EEB,      // Size: 1, Flags: (All)
        HonorNextLevel                                                    = PlayerFields.End + 0x0EEC,      // Size: 1, Flags: (All)
        PvpTierMaxFromWins                                                = PlayerFields.End + 0x0EED,      // Size: 1, Flags: (All)
        PvpLastWeeksTierMaxFromWins                                       = PlayerFields.End + 0x0EEE,      // Size: 1, Flags: (All)
        Bytes5                                                            = PlayerFields.End + 0x0EEF,      // Size: 1, Flags: (All) Nested: (InsertItemsLeftToRight, PvpRankProgress)
        End                                                               = PlayerFields.End + 0x0EF0,
    }

    public enum ActivePlayerDynamicFields
    {
        Research                                                          = PlayerDynamicFields.End + 0x0000,      // Flags: (All)
        ResearchSites                                                     = PlayerDynamicFields.End + 0x0001,      // Flags: (All)
        ResearchSiteProgress                                              = PlayerDynamicFields.End + 0x0002,      // Flags: (All)
        DailyQuestsCompleted                                              = PlayerDynamicFields.End + 0x0003,      // Flags: (All)
        AvailableQuestLineXQuestIDs                                       = PlayerDynamicFields.End + 0x0004,      // Flags: (All)
        Heirlooms                                                         = PlayerDynamicFields.End + 0x0005,      // Flags: (All)
        HeirloomFlags                                                     = PlayerDynamicFields.End + 0x0006,      // Flags: (All)
        Toys                                                              = PlayerDynamicFields.End + 0x0007,      // Flags: (All)
        Transmog                                                          = PlayerDynamicFields.End + 0x0008,      // Flags: (All)
        ConditionalTransmog                                               = PlayerDynamicFields.End + 0x0009,      // Flags: (All)
        SelfResSpells                                                     = PlayerDynamicFields.End + 0x000A,      // Flags: (All)
        CharacterRestrictions                                             = PlayerDynamicFields.End + 0x000B,      // Flags: (All)
        SpellFlatModByLabel                                               = PlayerDynamicFields.End + 0x000C,      // Flags: (All)
        SpellPctModByLabel                                                = PlayerDynamicFields.End + 0x000D,      // Flags: (All)
        End                                                               = PlayerDynamicFields.End + 0x000E,
    }

    public enum GameObjectFields
    {
        CreatedBy                                                         = ObjectFields.End + 0x0000,      // Size: 4, Flags: (All)
        GuildGUID                                                         = ObjectFields.End + 0x0004,      // Size: 4, Flags: (All)
        DisplayID                                                         = ObjectFields.End + 0x0008,      // Size: 1, Flags: (ViewerDependent, Urgent)
        Flags                                                             = ObjectFields.End + 0x0009,      // Size: 1, Flags: (All, Urgent)
        ParentRotation                                                    = ObjectFields.End + 0x000A,      // Size: 4, Flags: (All)
        FactionTemplate                                                   = ObjectFields.End + 0x000E,      // Size: 1, Flags: (All)
        Level                                                             = ObjectFields.End + 0x000F,      // Size: 1, Flags: (All)
        Bytes1                                                            = ObjectFields.End + 0x0010,      // Size: 1, Flags: (All, Urgent) Nested: (State, TypeID, ArtKit, PercentHealth)
        SpellVisualID                                                     = ObjectFields.End + 0x0011,      // Size: 1, Flags: (All, ViewerDependent, Urgent)
        StateSpellVisualID                                                = ObjectFields.End + 0x0012,      // Size: 1, Flags: (ViewerDependent, Urgent)
        SpawnTrackingStateAnimID                                          = ObjectFields.End + 0x0013,      // Size: 1, Flags: (ViewerDependent, Urgent)
        SpawnTrackingStateAnimKitID                                       = ObjectFields.End + 0x0014,      // Size: 1, Flags: (ViewerDependent, Urgent)
        StateWorldEffectID                                                = ObjectFields.End + 0x0015,      // Size: 4, Flags: (ViewerDependent, Urgent)
        CustomParam                                                       = ObjectFields.End + 0x0019,      // Size: 1, Flags: (All, Urgent)
        End                                                               = ObjectFields.End + 0x001A,
    }

    public enum GameObjectDynamicFields
    {
        EnableDoodadSets                                                  = ObjectDynamicFields.End + 0x0000,      // Flags: (All)
        End                                                               = ObjectDynamicFields.End + 0x0001,
    }

    public enum DynamicObjectFields
    {
        Caster                                                            = ObjectFields.End + 0x0000,      // Size: 4, Flags: (All)
        Type                                                              = ObjectFields.End + 0x0004,      // Size: 1, Flags: (All)
        SpellXSpellVisualID                                               = ObjectFields.End + 0x0005,      // Size: 1, Flags: (All)
        SpellID                                                           = ObjectFields.End + 0x0006,      // Size: 1, Flags: (All)
        Radius                                                            = ObjectFields.End + 0x0007,      // Size: 1, Flags: (All)
        CastTime                                                          = ObjectFields.End + 0x0008,      // Size: 1, Flags: (All)
        End                                                               = ObjectFields.End + 0x0009,
    }

    public enum DynamicObjectDynamicFields
    {
        End                                                               = ObjectDynamicFields.End + 0x0000,
    }

    public enum CorpseFields
    {
        Owner                                                             = ObjectFields.End + 0x0000,      // Size: 4, Flags: (All)
        PartyGUID                                                         = ObjectFields.End + 0x0004,      // Size: 4, Flags: (All)
        GuildGUID                                                         = ObjectFields.End + 0x0008,      // Size: 4, Flags: (All)
        DisplayID                                                         = ObjectFields.End + 0x000C,      // Size: 1, Flags: (All)
        Items                                                             = ObjectFields.End + 0x000D,      // Size: 19, Flags: (All)
        Bytes1                                                            = ObjectFields.End + 0x0020,      // Size: 1, Flags: (All) Nested: (RaceID, Sex, ClassID, Padding)
        Flags                                                             = ObjectFields.End + 0x0021,      // Size: 1, Flags: (All)
        DynamicFlags                                                      = ObjectFields.End + 0x0022,      // Size: 1, Flags: (ViewerDependent)
        FactionTemplate                                                   = ObjectFields.End + 0x0023,      // Size: 1, Flags: (All)
        CustomizationChoices                                              = ObjectFields.End + 0x0024,      // Size: 72, Flags: (All)
        End                                                               = ObjectFields.End + 0x006C,
    }

    public enum CorpseDynamicFields
    {
        End                                                               = ObjectDynamicFields.End + 0x0000,
    }

    public enum AreaTriggerFields
    {
        OverrideScaleCurve                                                = ObjectFields.End + 0x0000,      // Size: 7, Flags: (All, Urgent)
        ExtraScaleCurve                                                   = ObjectFields.End + 0x0007,      // Size: 7, Flags: (All, Urgent)
        Caster                                                            = ObjectFields.End + 0x000E,      // Size: 4, Flags: (All)
        Duration                                                          = ObjectFields.End + 0x0012,      // Size: 1, Flags: (All)
        TimeToTarget                                                      = ObjectFields.End + 0x0013,      // Size: 1, Flags: (All, Urgent)
        TimeToTargetScale                                                 = ObjectFields.End + 0x0014,      // Size: 1, Flags: (All, Urgent)
        TimeToTargetExtraScale                                            = ObjectFields.End + 0x0015,      // Size: 1, Flags: (All, Urgent)
        SpellID                                                           = ObjectFields.End + 0x0016,      // Size: 1, Flags: (All)
        SpellForVisuals                                                   = ObjectFields.End + 0x0017,      // Size: 1, Flags: (All)
        SpellXSpellVisualID                                               = ObjectFields.End + 0x0018,      // Size: 1, Flags: (All)
        BoundsRadius2D                                                    = ObjectFields.End + 0x0019,      // Size: 1, Flags: (ViewerDependent, Urgent)
        DecalPropertiesID                                                 = ObjectFields.End + 0x001A,      // Size: 1, Flags: (All)
        CreatingEffectGUID                                                = ObjectFields.End + 0x001B,      // Size: 4, Flags: (All)
        End                                                               = ObjectFields.End + 0x001F,
    }

    public enum AreaTriggerDynamicFields
    {
        End                                                               = ObjectDynamicFields.End + 0x0000,
    }

    public enum SceneObjectFields
    {
        ScriptPackageID                                                   = ObjectFields.End + 0x0000,      // Size: 1, Flags: (All)
        RndSeedVal                                                        = ObjectFields.End + 0x0001,      // Size: 1, Flags: (All)
        CreatedBy                                                         = ObjectFields.End + 0x0002,      // Size: 4, Flags: (All)
        SceneType                                                         = ObjectFields.End + 0x0006,      // Size: 1, Flags: (All)
        End                                                               = ObjectFields.End + 0x0007,
    }

    public enum SceneObjectDynamicFields
    {
        End                                                               = ObjectDynamicFields.End + 0x0000,
    }

    public enum ConversationFields
    {
        LastLineEndTime                                                   = ObjectFields.End + 0x0000,      // Size: 1, Flags: (ViewerDependent)
        End                                                               = ObjectFields.End + 0x0001,
    }

    public enum ConversationDynamicFields
    {
        Actors                                                            = ObjectDynamicFields.End + 0x0000,      // Flags: (All)
        Lines                                                             = ObjectDynamicFields.End + 0x0001,      // Flags: (Unk0x100)
        End                                                               = ObjectDynamicFields.End + 0x0002,
    }
}
