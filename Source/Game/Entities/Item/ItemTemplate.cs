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
using Game.DataStorage;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Game.Entities
{
    public class ItemTemplate
    {
        public ItemTemplate(ItemRecord item, ItemSparseRecord sparse)
        {
            BasicData = item;
            ExtendedData = sparse;

            Specializations[0] = new BitSet((int)Class.Max * PlayerConst.MaxTalentSpecs);
            Specializations[1] = new BitSet((int)Class.Max * PlayerConst.MaxTalentSpecs);
            Specializations[2] = new BitSet((int)Class.Max * PlayerConst.MaxTalentSpecs);
        }

        public string GetName(Locale locale = SharedConst.DefaultLocale)
        => ExtendedData.Display[locale];

        public bool CanChangeEquipStateInCombat()
        {
            switch (GetInventoryType())
            {
                case InventoryType.Relic:
                case InventoryType.Shield:
                case InventoryType.Holdable:
                    return true;
                default:
                    break;
            }

            switch (GetClass())
            {
                case ItemClass.Weapon:
                case ItemClass.Projectile:
                    return true;
            }

            return false;
        }

        public SkillType GetSkill()
        {
            SkillType[] item_weapon_skills =
            {
                SkillType.Axes,             SkillType.TwoHandedAxes,    SkillType.Bows,     SkillType.Guns,             SkillType.Maces,
                SkillType.TwoHandedMaces,   SkillType.Polearms,         SkillType.Swords,   SkillType.TwoHandedSwords,  0,
                SkillType.Staves,           0,                          0,                  SkillType.FistWeapons,      0,
                SkillType.Daggers,          0,                          0,                  SkillType.Crossbows,        SkillType.Wands,
                SkillType.Fishing
            };

            SkillType[] item_armor_skills =
            {
                0, SkillType.Cloth, SkillType.Leather, SkillType.Mail, SkillType.PlateMail, 0, SkillType.Shield, 0, 0, 0, 0, 0
            };


            switch (GetClass())
            {
                case ItemClass.Weapon:
                    if (GetSubClass() >= (int)ItemSubClassWeapon.Max)
                        return 0;
                    else
                        return item_weapon_skills[GetSubClass()];

                case ItemClass.Armor:
                    if (GetSubClass() >= (int)ItemSubClassArmor.Max)
                        return 0;
                    else
                        return item_armor_skills[GetSubClass()];

                default:
                    return 0;
            }
        }

        public float GetDPS(uint itemLevel)
        {
            ItemQuality quality = GetQuality() != ItemQuality.Heirloom ? GetQuality() : ItemQuality.Rare;
            if (GetClass() != ItemClass.Weapon || quality > ItemQuality.Artifact)
                return 0.0f;

            float dps = 0.0f;
            switch (GetInventoryType())
            {
                case InventoryType.Ammo:
                    dps = CliDB.ItemDamageAmmoStorage.LookupByKey(itemLevel).Quality[(int)quality];
                    break;
                case InventoryType.Weapon2Hand:
                    if (GetFlags2().HasAnyFlag(ItemFlags2.CasterWeapon))
                        dps = CliDB.ItemDamageTwoHandCasterStorage.LookupByKey(itemLevel).Quality[(int)quality];
                    else
                        dps = CliDB.ItemDamageTwoHandStorage.LookupByKey(itemLevel).Quality[(int)quality];
                    break;
                case InventoryType.Ranged:
                case InventoryType.Thrown:
                case InventoryType.RangedRight:
                    switch ((ItemSubClassWeapon)GetSubClass())
                    {
                        case ItemSubClassWeapon.Wand:
                            dps = CliDB.ItemDamageOneHandCasterStorage.LookupByKey(itemLevel).Quality[(int)quality];
                            break;
                        case ItemSubClassWeapon.Bow:
                        case ItemSubClassWeapon.Gun:
                        case ItemSubClassWeapon.Crossbow:
                            if (GetFlags2().HasAnyFlag(ItemFlags2.CasterWeapon))
                                dps = CliDB.ItemDamageTwoHandCasterStorage.LookupByKey(itemLevel).Quality[(int)quality];
                            else
                                dps = CliDB.ItemDamageTwoHandStorage.LookupByKey(itemLevel).Quality[(int)quality];
                            break;
                        default:
                            break;
                    }
                    break;
                case InventoryType.Weapon:
                case InventoryType.WeaponMainhand:
                case InventoryType.WeaponOffhand:
                    if (GetFlags2().HasAnyFlag(ItemFlags2.CasterWeapon))
                        dps = CliDB.ItemDamageOneHandCasterStorage.LookupByKey(itemLevel).Quality[(int)quality];
                    else
                        dps = CliDB.ItemDamageOneHandStorage.LookupByKey(itemLevel).Quality[(int)quality];
                    break;
                default:
                    break;
            }

            return dps;
        }

        public void GetDamage(uint itemLevel, out float minDamage, out float maxDamage)
        {
            minDamage = maxDamage = 0.0f;
            float dps = GetDPS(itemLevel);
            if (dps > 0.0f)
            {
                float avgDamage = dps * GetDelay() * 0.001f;
                minDamage = (GetDmgVariance() * -0.5f + 1.0f) * avgDamage;
                maxDamage = (float)Math.Floor(avgDamage * (GetDmgVariance() * 0.5f + 1.0f) + 0.5f);
            }
        }

        public bool IsUsableByLootSpecialization(Player player, bool alwaysAllowBoundToAccount)
        {
            if (GetFlags().HasAnyFlag(ItemFlags.IsBoundToAccount) && alwaysAllowBoundToAccount)
                return true;

            uint spec = player.GetLootSpecId();
            if (spec == 0)
                spec = player.GetPrimarySpecialization();

            ChrSpecializationRecord chrSpecialization = CliDB.ChrSpecializationStorage.LookupByKey(spec);
            if (chrSpecialization == null)
                return false;

            int levelIndex = 0;
            if (player.GetLevel() >= 110)
                levelIndex = 2;
            else if (player.GetLevel() > 40)
                levelIndex = 1;

            return Specializations[levelIndex].Get(CalculateItemSpecBit(chrSpecialization));
        }

        public static int CalculateItemSpecBit(ChrSpecializationRecord spec)
        {
            return (spec.ClassID - 1) * PlayerConst.MaxTalentSpecs + spec.OrderIndex;
        }

        public uint GetId() => BasicData.Id;
        public ItemClass GetClass() => BasicData.ClassID;
        public uint GetSubClass() => BasicData.SubclassID;
        public ItemQuality GetQuality() => (ItemQuality)ExtendedData.OverallQualityID;
        public ItemFlags GetFlags() => (ItemFlags)ExtendedData.Flags[0];
        public ItemFlags2 GetFlags2() => (ItemFlags2)ExtendedData.Flags[1];
        public ItemFlags3 GetFlags3() => (ItemFlags3)ExtendedData.Flags[2];
        public ItemFlags4 GetFlags4() => (ItemFlags4)ExtendedData.Flags[3];
        public float GetPriceRandomValue() => ExtendedData.PriceRandomValue;
        public float GetPriceVariance() => ExtendedData.PriceVariance;
        public uint GetBuyCount() => Math.Max(ExtendedData.VendorStackCount, 1u);
        public uint GetBuyPrice() => ExtendedData.BuyPrice;
        public uint GetSellPrice() => ExtendedData.SellPrice;
        public InventoryType GetInventoryType() => ExtendedData.InventoryType;
        public int GetAllowableClass() => ExtendedData.AllowableClass;
        public long GetAllowableRace() => ExtendedData.AllowableRace;
        public uint GetBaseItemLevel() => ExtendedData.ItemLevel;
        public int GetBaseRequiredLevel() => ExtendedData.RequiredLevel;
        public uint GetRequiredSkill() => ExtendedData.RequiredSkill;
        public uint GetRequiredSkillRank() => ExtendedData.RequiredSkillRank;
        public uint GetRequiredSpell() => ExtendedData.RequiredAbility;
        public uint GetRequiredReputationFaction() => ExtendedData.MinFactionID;
        public uint GetRequiredReputationRank() => ExtendedData.MinReputation;
        public uint GetMaxCount() => ExtendedData.MaxCount;
        public uint GetContainerSlots() => ExtendedData.ContainerSlots;
        public int GetStatModifierBonusStat(uint index) { Cypher.Assert(index < ItemConst.MaxStats); return ExtendedData.StatModifierBonusStat[index]; }
        public int GetStatPercentEditor(uint index) { Cypher.Assert(index < ItemConst.MaxStats); return ExtendedData.StatPercentEditor[index]; }
        public float GetStatPercentageOfSocket(uint index) { Cypher.Assert(index < ItemConst.MaxStats); return ExtendedData.StatPercentageOfSocket[index]; }
        public uint GetScalingStatDistribution() => ExtendedData.ScalingStatDistributionID;
        public uint GetDamageType() => ExtendedData.DamageType;
        public uint GetDelay() => ExtendedData.ItemDelay;
        public float GetRangedModRange() => ExtendedData.ItemRange;
        public ItemBondingType GetBonding() => (ItemBondingType)ExtendedData.Bonding;
        public uint GetPageText() => ExtendedData.PageID;
        public uint GetStartQuest() => ExtendedData.StartQuestID;
        public uint GetLockID() => ExtendedData.LockID;
        public uint GetItemSet() => ExtendedData.ItemSet;
        public uint GetArea(int index) => ExtendedData.ZoneBound[index];
        public uint GetMap() => ExtendedData.InstanceBound;
        public BagFamilyMask GetBagFamily() => (BagFamilyMask)ExtendedData.BagFamily;
        public uint GetTotemCategory() => ExtendedData.TotemCategoryID;
        public SocketColor GetSocketColor(uint index)
        {
            Cypher.Assert(index < ItemConst.MaxGemSockets);
            return (SocketColor)ExtendedData.SocketType[index];
        }
        public uint GetSocketBonus() => ExtendedData.SocketMatchenchantmentID;
        public uint GetGemProperties() => ExtendedData.GemProperties;
        public float GetQualityModifier() => ExtendedData.QualityModifier;
        public uint GetDuration() => ExtendedData.DurationInInventory;
        public uint GetItemLimitCategory() => ExtendedData.LimitCategory;
        public HolidayIds GetHolidayID() => (HolidayIds)ExtendedData.RequiredHoliday;
        public float GetDmgVariance() => ExtendedData.DmgVariance;
        public byte GetArtifactID() => ExtendedData.ArtifactID;
        public byte GetRequiredExpansion() => ExtendedData.ExpansionID;

        public bool IsCurrencyToken() => (GetBagFamily() & BagFamilyMask.CurrencyTokens) != 0;

        public uint GetMaxStackSize() => (ExtendedData.Stackable == 2147483647 || ExtendedData.Stackable <= 0) ? (0x7FFFFFFF - 1) : ExtendedData.Stackable;

        public bool IsPotion() => GetClass() == ItemClass.Consumable && GetSubClass() == (uint)ItemSubClassConsumable.Potion;
        public bool IsVellum() => GetFlags3().HasAnyFlag(ItemFlags3.CanStoreEnchants);
        public bool IsConjuredConsumable() => GetClass() == ItemClass.Consumable && GetFlags().HasAnyFlag(ItemFlags.Conjured);
        public bool IsCraftingReagent() => GetFlags2().HasAnyFlag(ItemFlags2.UsedInATradeskill);

        public bool IsWeapon() => GetClass() == ItemClass.Weapon;

        public bool IsRangedWeapon() => GetClass() == ItemClass.Weapon || GetSubClass() == (uint)ItemSubClassWeapon.Bow ||
            GetSubClass() == (uint)ItemSubClassWeapon.Gun || GetSubClass() == (uint)ItemSubClassWeapon.Crossbow;

        public uint MaxDurability;
        public List<ItemEffectRecord> Effects = new();

        // extra fields, not part of db2 files
        public uint ScriptId;
        public uint FoodType;
        public uint MinMoneyLoot;
        public uint MaxMoneyLoot;
        public ItemFlagsCustom FlagsCu;
        public float SpellPPMRate;
        public uint RandomBonusListTemplateId;
        public BitSet[] Specializations = new BitSet[3];
        public uint ItemSpecClassMask;

        protected ItemRecord BasicData;
        protected ItemSparseRecord ExtendedData;
    }
}
