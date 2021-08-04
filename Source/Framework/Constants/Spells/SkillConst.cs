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
 */﻿

namespace Framework.Constants
{
    public struct SkillConst
    {
        public const int MaxPlayerSkills = 256;
        public const uint MaxSkillStep = 15;
    }

    public enum SkillType
    {
        None                    = 0,
        Frost                   = 6,
        Fire                    = 8,
        Arms                    = 26,
        Combat                  = 38,
        Subtlety                = 39,
        Poisons                 = 40,
        Swords                  = 43,
        Axes                    = 44,
        Bows                    = 45,
        Guns                    = 46,
        BeastMastery            = 50,
        Survival                = 51,
        Maces                   = 54,
        TwoHandedSwords         = 55,
        Holy                    = 56,
        ShadowMagic             = 78,
        Defense                 = 95,
        LanguageCommon          = 98,
        RacialDwarf             = 101,
        LanguageOrcish          = 109,
        LanguageDwarven         = 111,
        LanguageDarnassian      = 113,
        LanguageTaurahe         = 115,
        DualWield               = 118,
        RacialTauren            = 124,
        RacialOrc               = 125,
        RacialNightElf          = 126,
        FirstAid                = 129,
        FeralCombat             = 134,
        Staves                  = 136,
        LanguageThalassian      = 137,
        LanguageDraconic        = 138,
        LanguageDemonTongue     = 139,
        LanguageTitan           = 140,
        LanguageOldTongue       = 141,
        Survival_2              = 142,
        HorseRiding             = 148,
        WolfRiding              = 149,
        TigerRiding             = 150,
        RamRiding               = 152,
        Swimming                = 155,
        TwoHandedMaces          = 160,
        Unarmed                 = 162,
        Marksmanship            = 163,
        Blacksmithing           = 164,
        Leatherworking          = 165,
        Alchemy                 = 171,
        TwoHandedAxes           = 172,
        Daggers                 = 173,
        Thrown                  = 176,
        Herbalism               = 182,
        GenericDnd              = 183,
        Retribution             = 184,
        Cooking                 = 185,
        Mining                  = 186,
        PetImp                  = 188,
        PetFelhunter            = 189,
        Tailoring               = 197,
        Engineering             = 202,
        PetSpider               = 203,
        PetVoidwalker           = 204,
        PetSuccubus             = 205,
        PetInfernal             = 206,
        PetDoomguard            = 207,
        PetWolf                 = 208,
        PetCat                  = 209,
        PetBear                 = 210,
        PetBoar                 = 211,
        PetCrocolisk            = 212,
        PetCarrionBird          = 213,
        PetCrab                 = 214,
        PetGorilla              = 215,
        PetRaptor               = 217,
        PetTallstrider          = 218,
        RacialUndead            = 220,
        Crossbows               = 226,
        Wands                   = 228,
        Polearms                = 229,
        PetScorpid              = 236,
        Arcane                  = 237,
        PetTurtle               = 251,
        Assassination           = 253,
        Fury                    = 256,
        Protection              = 257,
        BeastTraining           = 261,
        Protection_2            = 267,
        PetGenericHunter        = 270,
        PlateMail               = 293,
        LanguageGnomish         = 313,
        LanguageTroll           = 315,
        Enchanting              = 333,
        Demonology              = 354,
        Affliction              = 255,
        Fishing                 = 356,
        Enhancement             = 373,
        Restoration             = 374,
        ElementalCombat         = 375,
        Skinning                = 393,
        Mail                    = 413,
        Leather                 = 414,
        Cloth                   = 415,
        Shield                  = 433,
        FistWeapons             = 473,
        RaptorRiding            = 533,
        MechanostriderPiloting  = 553,
        UndeadHorsemanship      = 554,
        Restoration_2           = 573,
        Balance                 = 574,
        Destruction             = 593,
        Holy_2                  = 594,
        Discipline              = 613,
        Lockpicking             = 633,
        PetBat                  = 653,
        PetHyena                = 654,
        PetOwl                  = 655,
        PetWindSerpent          = 656,
        LanguageGutterspeak     = 673,
        KodoRiding              = 713,
        RacialTroll             = 733,
        RacialGnome             = 753,
        RacialHuman             = 754,
        Jewelcrafting           = 755,
        RacialBloodElf          = 756,
        PetEventRemoteControl   = 758,
        LanguageDraenei         = 759,
        RacialDraenei           = 760,
        PetFelguard             = 761,
        Riding                  = 762,
        PetDragonhawk           = 763,
        PetNetherRay            = 764,
        PetSporebat             = 765,
        PetWarpStalker          = 766,
        PetRavager              = 767,
        PetSerpent              = 768,
        Internal                = 769,
    }

    public enum SkillState
    {
        Unchanged = 0,
        Changed = 1,
        New = 2,
        Deleted = 3
    }

    public enum SkillCategory : sbyte
    {
        Unk = 0,
        Attributes = 5,
        Weapon = 6,
        Class = 7,
        Armor = 8,
        Secondary = 9,
        Languages = 10,
        Profession = 11,
        Generic = 12
    }

    public enum SkillRangeType
    {
        Language,                                   // 300..300
        Level,                                      // 1..max skill for level
        Mono,                                       // 1..1, grey monolite bar
        Rank,                                       // 1..skill for known rank
        None                                        // 0..0 always
    }
}
