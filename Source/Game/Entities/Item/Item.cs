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

using Framework.Collections;
using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Loots;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Game.Entities
{
    public class Item : WorldObject
    {
        public Item() : base(false)
        {
            ObjectTypeMask |= TypeMask.Item;
            ObjectTypeId = TypeId.Item;

            ValuesCount = (int)ItemFields.End;
            m_dynamicValuesCount = (int)ItemDynamicFields.End;

            uState = ItemUpdateState.New;
            uQueuePos = -1;
            m_lastPlayedTimeUpdate = GameTime.GetGameTime();

            loot = new Loot();
        }

        public virtual bool Create(ulong guidlow, uint itemId, ItemContext context, Player owner)
        {
            _Create(ObjectGuid.Create(HighGuid.Item, guidlow));

            SetEntry(itemId);
            SetObjectScale(1.0f);

            if (owner)
            {
                SetOwnerGUID(owner.GetGUID());
                SetContainedIn(owner.GetGUID());
            }

            ItemTemplate itemProto = Global.ObjectMgr.GetItemTemplate(itemId);
            if (itemProto == null)
                return false;

            _bonusData = new BonusData(itemProto);
            SetCount(1);
            SetUpdateField<uint>(ItemFields.MaxDurability, itemProto.MaxDurability);
            SetDurability(itemProto.MaxDurability);

            for (int i = 0; i < itemProto.Effects.Count; ++i)
                if (itemProto.Effects[i].LegacySlotIndex < 5)
                    SetSpellCharges(itemProto.Effects[i].LegacySlotIndex, itemProto.Effects[i].Charges);

            SetExpiration(itemProto.GetDuration());
            SetCreatePlayedTime(0);
            SetContext(context);

            return true;
        }

        public override string GetName(Locale locale = Locale.enUS)
        {
            ItemTemplate itemTemplate = GetTemplate();
            var suffix = CliDB.ItemNameDescriptionStorage.LookupByKey(_bonusData.Suffix);
            if (suffix != null)
                return $"{itemTemplate.GetName(locale)} {suffix.Description[locale]}";

            return itemTemplate.GetName(locale);
        }

        public bool IsNotEmptyBag()
        {
            Bag bag = ToBag();
            if (bag != null)
                return !bag.IsEmpty();

            return false;
        }

        public void UpdateDuration(Player owner, uint diff)
        {
            uint duration = GetUpdateField<uint>(ItemFields.Expiration);
            if (duration == 0)
                return;

            Log.outDebug(LogFilter.Player, "Item.UpdateDuration Item (Entry: {0} Duration {1} Diff {2})", GetEntry(), duration, diff);

            if (duration <= diff)
            {
                Global.ScriptMgr.OnItemExpire(owner, GetTemplate());
                owner.DestroyItem(GetBagSlot(), GetSlot(), true);
                return;
            }

            SetExpiration(duration - diff);
            SetState(ItemUpdateState.Changed, owner);                          // save new time in database
        }

        public virtual void SaveToDB(SQLTransaction trans)
        {
            PreparedStatement stmt;
            switch (uState)
            {
                case ItemUpdateState.New:
                case ItemUpdateState.Changed:
                {
                    byte index = 0;
                    stmt = DB.Characters.GetPreparedStatement(uState == ItemUpdateState.New ? CharStatements.REP_ITEM_INSTANCE : CharStatements.UPD_ITEM_INSTANCE);
                    stmt.AddValue(index, GetEntry());
                    stmt.AddValue(++index, GetOwnerGUID().GetCounter());
                    stmt.AddValue(++index, GetCreator().GetCounter());
                    stmt.AddValue(++index, GetGiftCreator().GetCounter());
                    stmt.AddValue(++index, GetCount());
                    stmt.AddValue(++index, GetUpdateField<uint>(ItemFields.Expiration));

                    StringBuilder ss = new();
                    for (byte i = 0; i < ItemConst.MaxSpells && i < _bonusData.EffectCount; ++i)
                        ss.AppendFormat("{0} ", GetSpellCharges(i));

                    stmt.AddValue(++index, ss.ToString());
                    stmt.AddValue(++index, GetUpdateField<uint>(ItemFields.DynamicFlags));

                    ss.Clear();
                    for (EnchantmentSlot slot = 0; slot < EnchantmentSlot.Max; ++slot)
                        ss.AppendFormat("{0} {1} {2} ", GetEnchantmentId(slot), GetEnchantmentDuration(slot), GetEnchantmentCharges(slot));

                    stmt.AddValue(++index, ss.ToString());
                    stmt.AddValue(++index, m_randomBonusListId);
                    stmt.AddValue(++index, GetUpdateField<uint>(ItemFields.Durability));
                    stmt.AddValue(++index, GetUpdateField<uint>(ItemFields.CreatePlayedTime));
                    stmt.AddValue(++index, m_text);
                    stmt.AddValue(++index, GetModifier(ItemModifier.BattlePetSpeciesId));
                    stmt.AddValue(++index, GetModifier(ItemModifier.BattlePetBreedData));
                    stmt.AddValue(++index, GetModifier(ItemModifier.BattlePetLevel));
                    stmt.AddValue(++index, GetModifier(ItemModifier.BattlePetDisplayId));
                    stmt.AddValue(++index, (byte)GetUpdateField<uint>(ItemFields.Context));

                    ss.Clear();

                    foreach (int bonusListID in GetDynamicValues(ItemDynamicFields.BonusListIDs))
                        ss.Append($"{bonusListID} ");

                    stmt.AddValue(++index, ss.ToString());
                    stmt.AddValue(++index, GetGUID().GetCounter());

                    DB.Characters.Execute(stmt);

                    if ((uState == ItemUpdateState.Changed) && HasItemFlag(ItemFieldFlags.Wrapped))
                    {
                        stmt = DB.Characters.GetPreparedStatement(CharStatements.UPD_GIFT_OWNER);
                        stmt.AddValue(0, GetOwnerGUID().GetCounter());
                        stmt.AddValue(1, GetGUID().GetCounter());
                        DB.Characters.Execute(stmt);
                    }

                    stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_GEMS);
                    stmt.AddValue(0, GetGUID().GetCounter());
                    trans.Append(stmt);

                    if (GetGems().Count != 0)
                    {
                        stmt = DB.Characters.GetPreparedStatement(CharStatements.INS_ITEM_INSTANCE_GEMS);
                        stmt.AddValue(0, GetGUID().GetCounter());
                        int i = 0;
                        int gemFields = 4;

                        foreach (var gemData in GetGems())
                        {
                            if (gemData.ItemId != 0)
                            {
                                stmt.AddValue(1 + i * gemFields, gemData.ItemId);
                                StringBuilder gemBonusListIDs = new();
                                foreach (ushort bonusListID in gemData.BonusListIDs)
                                {
                                    if (bonusListID != 0)
                                        gemBonusListIDs.AppendFormat("{0} ", bonusListID);
                                }

                                stmt.AddValue(2 + i * gemFields, gemBonusListIDs.ToString());
                                stmt.AddValue(3 + i * gemFields, gemData.Context);
                                stmt.AddValue(4 + i * gemFields, m_gemScalingLevels[i]);
                            }
                            else
                            {
                                stmt.AddValue(1 + i * gemFields, 0);
                                stmt.AddValue(2 + i * gemFields, "");
                                stmt.AddValue(3 + i * gemFields, 0);
                                stmt.AddValue(4 + i * gemFields, 0);
                            }
                            ++i;
                        }

                        for (; i < ItemConst.MaxGemSockets; ++i)
                        {
                            stmt.AddValue(1 + i * gemFields, 0);
                            stmt.AddValue(2 + i * gemFields, "");
                            stmt.AddValue(3 + i * gemFields, 0);
                            stmt.AddValue(4 + i * gemFields, 0);
                        }
                        trans.Append(stmt);
                    }

                    ItemModifier[] modifiersTable =
                    {
                            ItemModifier.TimewalkerLevel,
                            ItemModifier.ArtifactKnowledgeLevel
                        };

                    stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_MODIFIERS);
                    stmt.AddValue(0, GetGUID().GetCounter());
                    trans.Append(stmt);

                    if (modifiersTable.Any(modifier => GetModifier(modifier) != 0))
                    {
                        stmt = DB.Characters.GetPreparedStatement(CharStatements.INS_ITEM_INSTANCE_MODIFIERS);
                        stmt.AddValue(0, GetGUID().GetCounter());
                        stmt.AddValue(1, GetModifier(ItemModifier.TimewalkerLevel));
                        stmt.AddValue(2, GetModifier(ItemModifier.ArtifactKnowledgeLevel));
                        trans.Append(stmt);
                    }
                    break;
                }
                case ItemUpdateState.Removed:
                {
                    stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE);
                    stmt.AddValue(0, GetGUID().GetCounter());
                    trans.Append(stmt);

                    stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_GEMS);
                    stmt.AddValue(0, GetGUID().GetCounter());
                    trans.Append(stmt);

                    stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_MODIFIERS);
                    stmt.AddValue(0, GetGUID().GetCounter());
                    trans.Append(stmt);

                    if (HasItemFlag(ItemFieldFlags.Wrapped))
                    {
                        stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_GIFT);
                        stmt.AddValue(0, GetGUID().GetCounter());
                        trans.Append(stmt);
                    }

                    // Delete the items if this is a container
                    if (!loot.IsLooted())
                        Global.LootItemStorage.RemoveStoredLootForContainer(GetGUID().GetCounter());

                    Dispose();
                    return;
                }
                case ItemUpdateState.Unchanged:
                    break;
            }

            SetState(ItemUpdateState.Unchanged);
        }

        public virtual bool LoadFromDB(ulong guid, ObjectGuid ownerGuid, SQLFields fields, uint entry)
        {
            // create item before any checks for store correct guid
            // and allow use "FSetState(ITEM_REMOVED); SaveToDB();" for deleting item from DB
            _Create(ObjectGuid.Create(HighGuid.Item, guid));

            SetEntry(entry);
            SetObjectScale(1.0f);

            ItemTemplate proto = GetTemplate();
            if (proto == null)
                return false;

            _bonusData = new BonusData(proto);

            // set owner (not if item is only loaded for gbank/auction/mail
            if (!ownerGuid.IsEmpty())
                SetOwnerGUID(ownerGuid);

            uint itemFlags = fields.Read<uint>(7);
            bool need_save = false;
            ulong creator = fields.Read<ulong>(2);
            if (creator != 0)
            {
                if (!Convert.ToBoolean(itemFlags & (int)ItemFieldFlags.Child))
                    SetCreator(ObjectGuid.Create(HighGuid.Player, creator));
                else
                    SetCreator(ObjectGuid.Create(HighGuid.Item, creator));
            }

            ulong giftCreator = fields.Read<ulong>(3);
            if (giftCreator != 0)
                SetGiftCreator(ObjectGuid.Create(HighGuid.Player, giftCreator));

            SetCount(fields.Read<uint>(4));

            uint duration = fields.Read<uint>(5);
            SetExpiration(duration);
            // update duration if need, and remove if not need
            if (proto.GetDuration() != duration)
            {
                SetExpiration(proto.GetDuration());
                need_save = true;
            }

            SetItemFlags((ItemFieldFlags)itemFlags);

            uint durability = fields.Read<uint>(10);
            SetDurability(durability);
            // update max durability (and durability) if need
            SetUpdateField<uint>(ItemFields.MaxDurability, proto.MaxDurability);

            // do not overwrite durability for wrapped items
            if (durability > proto.MaxDurability && !HasItemFlag(ItemFieldFlags.Wrapped))
            {
                SetDurability(proto.MaxDurability);
                need_save = true;
            }

            SetCreatePlayedTime(fields.Read<uint>(11));
            SetText(fields.Read<string>(12));

            SetModifier(ItemModifier.BattlePetSpeciesId, fields.Read<uint>(13));
            SetModifier(ItemModifier.BattlePetBreedData, fields.Read<uint>(14));
            SetModifier(ItemModifier.BattlePetLevel, fields.Read<ushort>(15));
            SetModifier(ItemModifier.BattlePetDisplayId, fields.Read<uint>(16));

            SetContext((ItemContext)fields.Read<byte>(17));

            var bonusListString = new StringArray(fields.Read<string>(18), ' ');
            List<uint> bonusListIDs = new();
            for (var i = 0; i < bonusListString.Length; ++i)
            {
                if (uint.TryParse(bonusListString[i], out uint bonusListID))
                    bonusListIDs.Add(bonusListID);
            }
            SetBonuses(bonusListIDs);

            // load charges after bonuses, they can add more item effects
            var tokens = new StringArray(fields.Read<string>(6), ' ');
            for (byte i = 0; i < ItemConst.MaxSpells && i < _bonusData.EffectCount && i < tokens.Length; ++i)
            {
                if (int.TryParse(tokens[i], out int value))
                    SetSpellCharges(i, value);
            }

            int gemFields = 4;
            ItemDynamicFieldGems[] gemData = new ItemDynamicFieldGems[ItemConst.MaxGemSockets];
            for (int i = 0; i < ItemConst.MaxGemSockets; ++i)
            {
                gemData[i] = new ItemDynamicFieldGems();
                gemData[i].ItemId = fields.Read<uint>(19 + i * gemFields);
                var gemBonusListIDs = new StringArray(fields.Read<string>(20 + i * gemFields), ' ');
                if (!gemBonusListIDs.IsEmpty())
                {
                    uint b = 0;
                    foreach (string token in gemBonusListIDs)
                    {
                        if (uint.TryParse(token, out uint bonusListID) && bonusListID != 0)
                            gemData[i].BonusListIDs[b++] = (ushort)bonusListID;
                    }
                }

                gemData[i].Context = fields.Read<byte>(21 + i * gemFields);
                if (gemData[i].ItemId != 0)
                    SetGem((ushort)i, gemData[i], fields.Read<uint>(22 + i * gemFields));
            }

            SetModifier(ItemModifier.TimewalkerLevel, fields.Read<uint>(31));
            SetModifier(ItemModifier.ArtifactKnowledgeLevel, fields.Read<uint>(32));

            // Enchants must be loaded after all other bonus/scaling data
            _LoadIntoDataField(fields.Read<string>(8), (uint)ItemFields.Enchantment, (uint)EnchantmentSlot.Max * (uint)EnchantmentOffset.Max);

            m_randomBonusListId = fields.Read<uint>(9);

            // Remove bind flag for items vs NO_BIND set
            if (IsSoulBound() && GetBonding() == ItemBondingType.None)
            {
                RemoveItemFlag(ItemFieldFlags.Soulbound);
                need_save = true;
            }

            if (need_save)                                           // normal item changed state set not work at loading
            {
                byte index = 0;
                PreparedStatement stmt = DB.Characters.GetPreparedStatement(CharStatements.UPD_ITEM_INSTANCE_ON_LOAD);
                stmt.AddValue(index++, GetUpdateField<uint>(ItemFields.Expiration));
                stmt.AddValue(index++, GetUpdateField<uint>(ItemFields.DynamicFlags));
                stmt.AddValue(index++, GetUpdateField<uint>(ItemFields.Durability));
                stmt.AddValue(index++, guid);
                DB.Characters.Execute(stmt);
            }
            return true;
        }

        public static void DeleteFromDB(SQLTransaction trans, ulong itemGuid)
        {
            PreparedStatement stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE);
            stmt.AddValue(0, itemGuid);
            DB.Characters.ExecuteOrAppend(trans, stmt);

            stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_GEMS);
            stmt.AddValue(0, itemGuid);
            DB.Characters.ExecuteOrAppend(trans, stmt);


            stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_MODIFIERS);
            stmt.AddValue(0, itemGuid);
            DB.Characters.ExecuteOrAppend(trans, stmt);

            stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_GIFT);
            stmt.AddValue(0, itemGuid);
            DB.Characters.ExecuteOrAppend(trans, stmt);
        }

        public virtual void DeleteFromDB(SQLTransaction trans)
        {
            DeleteFromDB(trans, GetGUID().GetCounter());

            // Delete the items if this is a container
            if (!loot.IsLooted())
                Global.LootItemStorage.RemoveStoredLootForContainer(GetGUID().GetCounter());
        }

        public static void DeleteFromInventoryDB(SQLTransaction trans, ulong itemGuid)
        {
            PreparedStatement stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_CHAR_INVENTORY_BY_ITEM);
            stmt.AddValue(0, itemGuid);
            trans.Append(stmt);
        }

        public void DeleteFromInventoryDB(SQLTransaction trans)
        {
            DeleteFromInventoryDB(trans, GetGUID().GetCounter());
        }

        public ItemTemplate GetTemplate()
        => Global.ObjectMgr.GetItemTemplate(GetEntry());

        public Player GetOwner()
        => Global.ObjAccessor.FindPlayer(GetOwnerGUID());

        public SkillType GetSkill()
        {
            ItemTemplate proto = GetTemplate();
            return proto.GetSkill();
        }

        public void SetItemRandomBonusList(uint bonusListId)
        {
            if (bonusListId == 0)
                return;

            AddBonuses(bonusListId);
        }

        public void SetState(ItemUpdateState state, Player forplayer = null)
        {
            if (uState == ItemUpdateState.New && state == ItemUpdateState.Removed)
            {
                // pretend the item never existed
                if (forplayer)
                {
                    RemoveItemFromUpdateQueueOf(this, forplayer);
                    forplayer.DeleteRefundReference(GetGUID());
                }
                return;
            }
            if (state != ItemUpdateState.Unchanged)
            {
                // new items must stay in new state until saved
                if (uState != ItemUpdateState.New)
                    uState = state;

                if (forplayer)
                    AddItemToUpdateQueueOf(this, forplayer);
            }
            else
            {
                // unset in queue
                // the item must be removed from the queue manually
                uQueuePos = -1;
                uState = ItemUpdateState.Unchanged;
            }
        }

        static void AddItemToUpdateQueueOf(Item item, Player player)
        {
            if (item.IsInUpdateQueue())
                return;

            Cypher.Assert(player != null);

            if (player.GetGUID() != item.GetOwnerGUID())
            {
                Log.outError(LogFilter.Player, "Item.AddToUpdateQueueOf - Owner's guid ({0}) and player's guid ({1}) don't match!", item.GetOwnerGUID(), player.GetGUID().ToString());
                return;
            }

            if (player.m_itemUpdateQueueBlocked)
                return;

            player.ItemUpdateQueue.Add(item);
            item.uQueuePos = player.ItemUpdateQueue.Count - 1;
        }

        public static void RemoveItemFromUpdateQueueOf(Item item, Player player)
        {
            if (!item.IsInUpdateQueue())
                return;

            Cypher.Assert(player != null);

            if (player.GetGUID() != item.GetOwnerGUID())
            {
                Log.outError(LogFilter.Player, "Item.RemoveFromUpdateQueueOf - Owner's guid ({0}) and player's guid ({1}) don't match!", item.GetOwnerGUID().ToString(), player.GetGUID().ToString());
                return;
            }

            if (player.m_itemUpdateQueueBlocked)
                return;

            player.ItemUpdateQueue[item.uQueuePos] = null;
            item.uQueuePos = -1;
        }

        public byte GetBagSlot() => m_container != null ? m_container.GetSlot() : InventorySlots.Bag0;
        public bool IsEquipped() => !IsInBag() && m_slot < EquipmentSlot.End;

        public bool CanBeTraded(bool mail = false, bool trade = false)
        {
            if (m_lootGenerated)
                return false;

            if ((!mail || !IsBoundAccountWide()) && (IsSoulBound() && (!HasItemFlag(ItemFieldFlags.BopTradeable) || !trade)))
                return false;

            if (IsBag() && (Player.IsBagPos(GetPos()) || !ToBag().IsEmpty()))
                return false;

            Player owner = GetOwner();
            if (owner != null)
            {
                if (owner.CanUnequipItem(GetPos(), false) != InventoryResult.Ok)
                    return false;
                if (owner.GetLootGUID() == GetGUID())
                    return false;
            }

            if (IsBoundByEnchant())
                return false;

            return true;
        }

        public void SetCount(uint value)
        {
            SetUpdateField<uint>(ItemFields.StackCount, value);

            Player player = GetOwner();
            if (player)
            {
                TradeData tradeData = player.GetTradeData();
                if (tradeData != null)
                {
                    TradeSlots slot = tradeData.GetTradeSlotForItem(GetGUID());

                    if (slot != TradeSlots.Invalid)
                        tradeData.SetItem(slot, this, true);
                }
            }
        }

        bool HasEnchantRequiredSkill(Player player)
        {
            // Check all enchants for required skill
            for (var enchant_slot = EnchantmentSlot.Perm; enchant_slot < EnchantmentSlot.Max; ++enchant_slot)
            {
                uint enchant_id = GetEnchantmentId(enchant_slot);
                if (enchant_id != 0)
                {
                    SpellItemEnchantmentRecord enchantEntry = CliDB.SpellItemEnchantmentStorage.LookupByKey(enchant_id);
                    if (enchantEntry != null)
                        if (enchantEntry.RequiredSkillID != 0 && player.GetSkillValue((SkillType)enchantEntry.RequiredSkillID) < enchantEntry.RequiredSkillRank)
                            return false;
                }
            }

            return true;
        }

        uint GetEnchantRequiredLevel()
        {
            uint level = 0;

            // Check all enchants for required level
            for (var enchant_slot = EnchantmentSlot.Perm; enchant_slot < EnchantmentSlot.Max; ++enchant_slot)
            {
                uint enchant_id = GetEnchantmentId(enchant_slot);
                if (enchant_id != 0)
                {
                    var enchantEntry = CliDB.SpellItemEnchantmentStorage.LookupByKey(enchant_id);
                    if (enchantEntry != null)
                        if (enchantEntry.MinLevel > level)
                            level = enchantEntry.MinLevel;
                }
            }

            return level;
        }

        bool IsBoundByEnchant()
        {
            // Check all enchants for soulbound
            for (var enchant_slot = EnchantmentSlot.Perm; enchant_slot < EnchantmentSlot.Max; ++enchant_slot)
            {
                uint enchant_id = GetEnchantmentId(enchant_slot);
                if (enchant_id != 0)
                {
                    var enchantEntry = CliDB.SpellItemEnchantmentStorage.LookupByKey(enchant_id);
                    if (enchantEntry != null)
                        if (enchantEntry.Flags.HasAnyFlag(EnchantmentSlotMask.CanSouldBound))
                            return true;
                }
            }

            return false;
        }

        public InventoryResult CanBeMergedPartlyWith(ItemTemplate proto)
        {
            // not allow merge looting currently items
            if (m_lootGenerated)
                return InventoryResult.LootGone;

            // check item type
            if (GetEntry() != proto.GetId())
                return InventoryResult.CantStack;

            // check free space (full stacks can't be target of merge
            if (GetCount() >= proto.GetMaxStackSize())
                return InventoryResult.CantStack;

            return InventoryResult.Ok;
        }

        public bool IsFitToSpellRequirements(SpellInfo spellInfo)
        {
            ItemTemplate proto = GetTemplate();

            bool isEnchantSpell = spellInfo.HasEffect(SpellEffectName.EnchantItem) || spellInfo.HasEffect(SpellEffectName.EnchantItemTemporary) || spellInfo.HasEffect(SpellEffectName.EnchantItemPrismatic);
            if ((int)spellInfo.EquippedItemClass != -1)                 // -1 == any item class
            {
                if (isEnchantSpell && proto.GetFlags3().HasAnyFlag(ItemFlags3.CanStoreEnchants))
                    return true;

                if (spellInfo.EquippedItemClass != proto.GetClass())
                    return false;                                   //  wrong item class

                if (spellInfo.EquippedItemSubClassMask != 0)        // 0 == any subclass
                {
                    if ((spellInfo.EquippedItemSubClassMask & (1 << (int)proto.GetSubClass())) == 0)
                        return false;                               // subclass not present in mask
                }
            }

            if (isEnchantSpell && spellInfo.EquippedItemInventoryTypeMask != 0)       // 0 == any inventory type
            {
                // Special case - accept weapon type for main and offhand requirements
                if (proto.GetInventoryType() == InventoryType.Weapon &&
                    Convert.ToBoolean(spellInfo.EquippedItemInventoryTypeMask & (1 << (int)InventoryType.WeaponMainhand)) ||
                     Convert.ToBoolean(spellInfo.EquippedItemInventoryTypeMask & (1 << (int)InventoryType.WeaponOffhand)))
                    return true;
                else if ((spellInfo.EquippedItemInventoryTypeMask & (1 << (int)proto.GetInventoryType())) == 0)
                    return false;                                   // inventory type not present in mask
            }

            return true;
        }

        public void SetEnchantment(EnchantmentSlot slot, uint id, uint duration, uint charges, ObjectGuid caster = default)
        {
            // Better lost small time at check in comparison lost time at item save to DB.
            if ((GetEnchantmentId(slot) == id) && (GetEnchantmentDuration(slot) == duration) && (GetEnchantmentCharges(slot) == charges))
                return;

            Player owner = GetOwner();
            if (slot < EnchantmentSlot.MaxInspected)
            {
                uint oldEnchant = GetEnchantmentId(slot);
                if (oldEnchant != 0)
                    owner.GetSession().SendEnchantmentLog(GetOwnerGUID(), ObjectGuid.Empty, GetGUID(), GetEntry(), oldEnchant, (uint)slot);

                if (id != 0)
                    owner.GetSession().SendEnchantmentLog(GetOwnerGUID(), caster, GetGUID(), GetEntry(), id, (uint)slot);
            }

            SetUpdateField<uint>(ItemFields.Enchantment + (int)slot * (int)EnchantmentOffset.Max + (int)EnchantmentOffset.Id, id);
            SetUpdateField<uint>(ItemFields.Enchantment + (int)slot * (int)EnchantmentOffset.Max + (int)EnchantmentOffset.Duration, duration);
            SetUpdateField<uint>(ItemFields.Enchantment + (int)slot * (int)EnchantmentOffset.Max + (int)EnchantmentOffset.Charges, charges);
            SetState(ItemUpdateState.Changed, owner);
        }

        public void SetEnchantmentDuration(EnchantmentSlot slot, uint duration, Player owner)
        {
            if (GetEnchantmentDuration(slot) == duration)
                return;

            SetUpdateField<uint>(ItemFields.Enchantment + (int)slot * (int)EnchantmentOffset.Max + (int)EnchantmentOffset.Duration, duration);
            SetState(ItemUpdateState.Changed, owner);
            // Cannot use GetOwner() here, has to be passed as an argument to avoid freeze due to hashtable locking
        }

        public void SetEnchantmentCharges(EnchantmentSlot slot, uint charges)
        {
            if (GetEnchantmentCharges(slot) == charges)
                return;

            SetUpdateField<uint>(ItemFields.Enchantment + (int)slot * (int)EnchantmentOffset.Max + (int)EnchantmentOffset.Charges, charges);
            SetState(ItemUpdateState.Changed, GetOwner());
        }

        public void ClearEnchantment(EnchantmentSlot slot)
        {
            if (GetEnchantmentId(slot) == 0)
                return;

            for (var i = 0; i < ItemConst.MaxItemEnchantmentEffects; ++i)
                SetUpdateField<uint>(ItemFields.Enchantment + (int)slot * (int)EnchantmentOffset.Max + i, 0);
            SetState(ItemUpdateState.Changed, GetOwner());
        }

        public List<ItemDynamicFieldGems> GetGems() =>
            GetDynamicStructuredValues<ItemDynamicFieldGems>(ItemDynamicFields.Gems);

        public ItemDynamicFieldGems GetGem(ushort slot) =>
            GetDynamicStructuredValue<ItemDynamicFieldGems>(ItemDynamicFields.Gems, slot);

        public void SetGem(ushort slot, ItemDynamicFieldGems gem, uint gemScalingLevel)
        {
            //ASSERT(slot < MAX_GEM_SOCKETS);
            m_gemScalingLevels[slot] = gemScalingLevel;
            _bonusData.GemItemLevelBonus[slot] = 0;
            ItemTemplate gemTemplate = Global.ObjectMgr.GetItemTemplate(gem.ItemId);
            if (gemTemplate != null)
            {
                GemPropertiesRecord gemProperties = CliDB.GemPropertiesStorage.LookupByKey(gemTemplate.GetGemProperties());
                if (gemProperties != null)
                {
                    SpellItemEnchantmentRecord gemEnchant = CliDB.SpellItemEnchantmentStorage.LookupByKey(gemProperties.EnchantID);
                    if (gemEnchant != null)
                    {
                        BonusData gemBonus = new(gemTemplate);
                        foreach (var bonusListId in gem.BonusListIDs)
                            gemBonus.AddBonusList(bonusListId);

                        uint gemBaseItemLevel = gemTemplate.GetBaseItemLevel();
                        ScalingStatDistributionRecord ssd = CliDB.ScalingStatDistributionStorage.LookupByKey(gemBonus.ScalingStatDistribution);
                        if (ssd != null)
                        {
                            uint scaledIlvl = (uint)Global.DB2Mgr.GetCurveValueAt(ssd.PlayerLevelToItemLevelCurveID, gemScalingLevel);
                            if (scaledIlvl != 0)
                                gemBaseItemLevel = scaledIlvl;
                        }

                        _bonusData.GemRelicType[slot] = gemBonus.RelicType;

                        for (uint i = 0; i < ItemConst.MaxItemEnchantmentEffects; ++i)
                        {
                            switch (gemEnchant.Effect[i])
                            {
                                case ItemEnchantmentType.BonusListID:
                                {
                                    var bonusesEffect = Global.DB2Mgr.GetItemBonusList(gemEnchant.EffectArg[i]);
                                    if (bonusesEffect != null)
                                    {
                                        foreach (ItemBonusRecord itemBonus in bonusesEffect)
                                            if (itemBonus.Type == ItemBonusType.ItemLevel)
                                                _bonusData.GemItemLevelBonus[slot] += (uint)itemBonus.Value[0];
                                    }
                                    break;
                                }
                                case ItemEnchantmentType.BonusListCurve:
                                {
                                    uint artifactrBonusListId = Global.DB2Mgr.GetItemBonusListForItemLevelDelta((short)Global.DB2Mgr.GetCurveValueAt((uint)Curves.ArtifactRelicItemLevelBonus, gemBaseItemLevel + gemBonus.ItemLevelBonus));
                                    if (artifactrBonusListId != 0)
                                    {
                                        var bonusesEffect = Global.DB2Mgr.GetItemBonusList(artifactrBonusListId);
                                        if (bonusesEffect != null)
                                            foreach (ItemBonusRecord itemBonus in bonusesEffect)
                                                if (itemBonus.Type == ItemBonusType.ItemLevel)
                                                    _bonusData.GemItemLevelBonus[slot] += (uint)itemBonus.Value[0];
                                    }
                                    break;
                                }
                                default:
                                    break;
                            }
                        }
                    }
                }
            }

            SetDynamicStructuredValue(ItemDynamicFields.Gems, slot, gem);
        }

        public bool GemsFitSockets()
        {
            uint gemSlot = 0;
            foreach (ItemDynamicFieldGems gemData in GetGems())
            {
                SocketColor SocketColor = GetTemplate().GetSocketColor(gemSlot);
                if (SocketColor == 0) // no socket slot
                    continue;

                SocketColor GemColor = 0;

                ItemTemplate gemProto = Global.ObjectMgr.GetItemTemplate(gemData.ItemId);
                if (gemProto != null)
                {
                    GemPropertiesRecord gemProperty = CliDB.GemPropertiesStorage.LookupByKey(gemProto.GetGemProperties());
                    if (gemProperty != null)
                        GemColor = gemProperty.Type;
                }

                if (!GemColor.HasAnyFlag(ItemConst.SocketColorToGemTypeMask[(int)SocketColor])) // bad gem color on this socket
                    return false;
            }
            return true;
        }

        public byte GetGemCountWithID(uint gemID) => (byte)GetGems().Count(gemData => gemData.ItemId == gemID);

        public byte GetGemCountWithLimitCategory(uint limitCategory)
        => (byte)GetGems().Count(gemData => { ItemTemplate gemProto = Global.ObjectMgr.GetItemTemplate(gemData.ItemId); if (gemProto == null) return false; return gemProto.GetItemLimitCategory() == limitCategory; });

        public bool IsLimitedToAnotherMapOrZone(uint cur_mapId, uint cur_zoneId)
        {
            ItemTemplate proto = GetTemplate();
            return proto != null && ((proto.GetMap() != 0 && proto.GetMap() != cur_mapId) ||
                ((proto.GetArea(0) != 0 && proto.GetArea(0) != cur_zoneId) && (proto.GetArea(1) != 0 && proto.GetArea(1) != cur_zoneId)));
        }

        public void SendUpdateSockets()
        {
            SocketGemsSuccess socketGems = new();
            socketGems.Item = GetGUID();

            GetOwner().SendPacket(socketGems);
        }

        public void SendTimeUpdate(Player owner)
        {
            uint duration = GetUpdateField<uint>(ItemFields.Expiration);
            if (duration == 0)
                return;

            ItemTimeUpdate itemTimeUpdate = new();
            itemTimeUpdate.ItemGuid = GetGUID();
            itemTimeUpdate.DurationLeft = duration;
            owner.SendPacket(itemTimeUpdate);
        }

        public static Item CreateItem(uint item, uint count, ItemContext context, Player player = null)
        {
            if (count < 1)
                return null;                                        //don't create item at zero count

            var pProto = Global.ObjectMgr.GetItemTemplate(item);
            if (pProto != null)
            {
                if (count > pProto.GetMaxStackSize())
                    count = pProto.GetMaxStackSize();

                Item pItem = Bag.NewItemOrBag(pProto);
                if (pItem.Create(Global.ObjectMgr.GetGenerator(HighGuid.Item).Generate(), item, context, player))
                {
                    pItem.SetCount(count);
                    return pItem;
                }
            }

            return null;
        }

        public Item CloneItem(uint count, Player player = null)
        {
            Item newItem = CreateItem(GetEntry(), count, GetContext(), player);
            if (newItem == null)
                return null;

            newItem.SetCreator(GetCreator());
            newItem.SetGiftCreator(GetGiftCreator());
            newItem.SetItemFlags((ItemFieldFlags)(GetUpdateField<uint>(ItemFields.DynamicFlags) & ~(uint)(ItemFieldFlags.Refundable | ItemFieldFlags.BopTradeable)));
            newItem.SetExpiration(GetUpdateField<uint>(ItemFields.Expiration));
            // player CAN be NULL in which case we must not update random properties because that accesses player's item update queue
            if (player != null)
                newItem.SetItemRandomBonusList(m_randomBonusListId);
            return newItem;
        }

        public bool IsBindedNotWith(Player player)
        {
            // not binded item
            if (!IsSoulBound())
                return false;

            // own item
            if (GetOwnerGUID() == player.GetGUID())
                return false;

            if (HasItemFlag(ItemFieldFlags.BopTradeable))
                if (allowedGUIDs.Contains(player.GetGUID()))
                    return false;

            // BOA item case
            if (IsBoundAccountWide())
                return false;

            return true;
        }

        public override void BuildUpdate(Dictionary<Player, UpdateData> data)
        {
            Player owner = GetOwner();
            if (owner != null)
                BuildFieldsUpdate(owner, data);
            ClearUpdateMask(false);
        }

        public override void AddToObjectUpdate()
        {
            Player owner = GetOwner();
            if (owner)
                owner.GetMap().AddUpdateObject(this);
        }

        public override void RemoveFromObjectUpdate()
        {
            Player owner = GetOwner();
            if (owner)
                owner.GetMap().RemoveUpdateObject(this);
        }

        public void SaveRefundDataToDB()
        {
            DeleteRefundDataFromDB();

            PreparedStatement stmt = DB.Characters.GetPreparedStatement(CharStatements.INS_ITEM_REFUND_INSTANCE);
            stmt.AddValue(0, GetGUID().GetCounter());
            stmt.AddValue(1, GetRefundRecipient().GetCounter());
            stmt.AddValue(2, GetPaidMoney());
            stmt.AddValue(3, (ushort)GetPaidExtendedCost());
            DB.Characters.Execute(stmt);
        }

        public void DeleteRefundDataFromDB(SQLTransaction trans = null)
        {
            PreparedStatement stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_ITEM_REFUND_INSTANCE);
            stmt.AddValue(0, GetGUID().GetCounter());
            if (trans != null)
                trans.Append(stmt);
            else
                DB.Characters.Execute(stmt);
        }

        public void SetNotRefundable(Player owner, bool changestate = true, SQLTransaction trans = null)
        {
            if (!HasItemFlag(ItemFieldFlags.Refundable))
                return;

            ItemExpirePurchaseRefund itemExpirePurchaseRefund = new();
            itemExpirePurchaseRefund.ItemGUID = GetGUID();
            owner.SendPacket(itemExpirePurchaseRefund);

            RemoveItemFlag(ItemFieldFlags.Refundable);
            // Following is not applicable in the trading procedure
            if (changestate)
                SetState(ItemUpdateState.Changed, owner);

            SetRefundRecipient(ObjectGuid.Empty);
            SetPaidMoney(0);
            SetPaidExtendedCost(0);
            DeleteRefundDataFromDB(trans);

            owner.DeleteRefundReference(GetGUID());
        }

        public void UpdatePlayedTime(Player owner)
        {
            // Get current played time
            uint current_playtime = GetUpdateField<uint>(ItemFields.CreatePlayedTime);
            // Calculate time elapsed since last played time update
            long curtime = GameTime.GetGameTime();
            uint elapsed = (uint)(curtime - m_lastPlayedTimeUpdate);
            uint new_playtime = current_playtime + elapsed;
            // Check if the refund timer has expired yet
            if (new_playtime <= 2 * Time.Hour)
            {
                // No? Proceed.
                // Update the data field
                SetCreatePlayedTime(new_playtime);
                // Flag as changed to get saved to DB
                SetState(ItemUpdateState.Changed, owner);
                // Speaks for itself
                m_lastPlayedTimeUpdate = curtime;
                return;
            }
            // Yes
            SetNotRefundable(owner);
        }

        public uint GetPlayedTime()
        {
            long curtime = GameTime.GetGameTime();
            uint elapsed = (uint)(curtime - m_lastPlayedTimeUpdate);
            return GetUpdateField<uint>(ItemFields.CreatePlayedTime) + elapsed;
        }

        public bool IsRefundExpired() => (GetPlayedTime() > 2 * Time.Hour);

        public void SetSoulboundTradeable(List<ObjectGuid> allowedLooters)
        {
            AddItemFlag(ItemFieldFlags.BopTradeable);
            allowedGUIDs = allowedLooters;
        }

        public void ClearSoulboundTradeable(Player currentOwner)
        {
            RemoveItemFlag(ItemFieldFlags.BopTradeable);
            if (allowedGUIDs.Empty())
                return;

            allowedGUIDs.Clear();
            SetState(ItemUpdateState.Changed, currentOwner);
            PreparedStatement stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_ITEM_BOP_TRADE);
            stmt.AddValue(0, GetGUID().GetCounter());
            DB.Characters.Execute(stmt);
        }

        public bool CheckSoulboundTradeExpire()
        {
            // called from owner's update - GetOwner() MUST be valid
            if (GetUpdateField<uint>(ItemFields.CreatePlayedTime) + 2 * Time.Hour < GetOwner().GetTotalPlayedTime())
            {
                ClearSoulboundTradeable(GetOwner());
                return true; // remove from tradeable list
            }

            return false;
        }
        bool HasStats()
        {
            ItemTemplate proto = GetTemplate();
            Player owner = GetOwner();
            for (byte i = 0; i < ItemConst.MaxStats; ++i)
            {
                if ((owner ? GetItemStatValue(i, owner) : proto.GetStatPercentEditor(i)) != 0)
                    return true;
            }

            return false;
        }

        static bool HasStats(ItemInstance itemInstance, BonusData bonus)
        {
            for (byte i = 0; i < ItemConst.MaxStats; ++i)
            {
                if (bonus.StatPercentEditor[i] != 0)
                    return true;
            }

            return false;
        }

        uint GetBuyPrice(Player owner, out bool standardPrice) => GetBuyPrice(GetTemplate(), (uint)GetQuality(), GetItemLevel(owner), out standardPrice);

        static uint GetBuyPrice(ItemTemplate proto, uint quality, uint itemLevel, out bool standardPrice)
        {
            standardPrice = true;

            if (proto.GetFlags2().HasAnyFlag(ItemFlags2.OverrideGoldCost))
                return proto.GetBuyPrice();

            var qualityPrice = CliDB.ImportPriceQualityStorage.LookupByKey(quality + 1);
            if (qualityPrice == null)
                return 0;

            var basePrice = CliDB.ItemPriceBaseStorage.LookupByKey(proto.GetBaseItemLevel());
            if (basePrice == null)
                return 0;

            float qualityFactor = qualityPrice.Data;
            float baseFactor;

            var inventoryType = proto.GetInventoryType();

            if (inventoryType == InventoryType.Weapon ||
                inventoryType == InventoryType.Weapon2Hand ||
                inventoryType == InventoryType.WeaponMainhand ||
                inventoryType == InventoryType.WeaponOffhand ||
                inventoryType == InventoryType.Ranged ||
                inventoryType == InventoryType.Thrown ||
                inventoryType == InventoryType.RangedRight)
                baseFactor = basePrice.Weapon;
            else
                baseFactor = basePrice.Armor;

            if (inventoryType == InventoryType.Robe)
                inventoryType = InventoryType.Chest;

            if (proto.GetClass() == ItemClass.Gem && (ItemSubClassGem)proto.GetSubClass() == ItemSubClassGem.ArtifactRelic)
            {
                inventoryType = InventoryType.Weapon;
                baseFactor = basePrice.Weapon / 3.0f;
            }


            float typeFactor = 0.0f;
            sbyte weapType = -1;

            switch (inventoryType)
            {
                case InventoryType.Head:
                case InventoryType.Neck:
                case InventoryType.Shoulders:
                case InventoryType.Chest:
                case InventoryType.Waist:
                case InventoryType.Legs:
                case InventoryType.Feet:
                case InventoryType.Wrists:
                case InventoryType.Hands:
                case InventoryType.Finger:
                case InventoryType.Trinket:
                case InventoryType.Cloak:
                case InventoryType.Holdable:
                {
                    var armorPrice = CliDB.ImportPriceArmorStorage.LookupByKey(inventoryType);
                    if (armorPrice == null)
                        return 0;

                    switch ((ItemSubClassArmor)proto.GetSubClass())
                    {
                        case ItemSubClassArmor.Miscellaneous:
                        case ItemSubClassArmor.Cloth:
                            typeFactor = armorPrice.ClothModifier;
                            break;
                        case ItemSubClassArmor.Leather:
                            typeFactor = armorPrice.LeatherModifier;
                            break;
                        case ItemSubClassArmor.Mail:
                            typeFactor = armorPrice.ChainModifier;
                            break;
                        case ItemSubClassArmor.Plate:
                            typeFactor = armorPrice.PlateModifier;
                            break;
                        default:
                            typeFactor = 1.0f;
                            break;
                    }

                    break;
                }
                case InventoryType.Shield:
                {
                    var shieldPrice = CliDB.ImportPriceShieldStorage.LookupByKey(2); // it only has two rows, it's unclear which is the one used
                    if (shieldPrice == null)
                        return 0;

                    typeFactor = shieldPrice.Data;
                    break;
                }
                case InventoryType.WeaponMainhand:
                    weapType = 0;
                    break;
                case InventoryType.WeaponOffhand:
                    weapType = 1;
                    break;
                case InventoryType.Weapon:
                    weapType = 2;
                    break;
                case InventoryType.Weapon2Hand:
                    weapType = 3;
                    break;
                case InventoryType.Ranged:
                case InventoryType.RangedRight:
                case InventoryType.Relic:
                    weapType = 4;
                    break;
                default:
                    return proto.GetBuyPrice();
            }

            if (weapType != -1)
            {
                var weaponPrice = CliDB.ImportPriceWeaponStorage.LookupByKey(weapType + 1);
                if (weaponPrice == null)
                    return 0;

                typeFactor = weaponPrice.Data;
            }

            standardPrice = false;
            return (uint)(proto.GetPriceVariance() * typeFactor * baseFactor * qualityFactor * proto.GetPriceRandomValue());
        }

        public uint GetSellPrice(Player owner) => GetSellPrice(GetTemplate(), (uint)GetQuality(), GetItemLevel(owner));

        public static uint GetSellPrice(ItemTemplate proto, uint quality, uint itemLevel)
        {
            if (proto.GetFlags2().HasAnyFlag(ItemFlags2.OverrideGoldCost))
                return proto.GetSellPrice();

            uint cost = GetBuyPrice(proto, quality, itemLevel, out bool standardPrice);

            if (standardPrice)
            {
                ItemClassRecord classEntry = Global.DB2Mgr.GetItemClassByOldEnum(proto.GetClass());
                if (classEntry != null)
                {
                    uint buyCount = Math.Max(proto.GetBuyCount(), 1u);
                    return (uint)(cost * classEntry.PriceModifier / buyCount);
                }

                return 0;
            }
            else
                return proto.GetSellPrice();
        }

        public uint GetItemLevel(Player owner)
        {
            ItemTemplate itemTemplate = GetTemplate();
            uint minItemLevel = owner.GetUpdateField<uint>(UnitFields.MinItemLevel);
            uint minItemLevelCutoff = owner.GetUpdateField<uint>(UnitFields.MinItemLevelCutoff);
            uint maxItemLevel = itemTemplate.GetFlags3().HasAnyFlag(ItemFlags3.IgnoreItemLevelCapInPvp) ? 0u : owner.GetUpdateField<uint>(UnitFields.MaxItemLevel);

            return GetItemLevel(itemTemplate, _bonusData, owner.GetLevel(), GetModifier(ItemModifier.TimewalkerLevel),
                minItemLevel, minItemLevelCutoff, maxItemLevel);
        }

        public static uint GetItemLevel(ItemTemplate itemTemplate, BonusData bonusData, uint level, uint fixedLevel, uint minItemLevel, uint minItemLevelCutoff, uint maxItemLevel)
        {
            if (itemTemplate == null)
                return 1;

            uint itemLevel = itemTemplate.GetBaseItemLevel();

            ScalingStatDistributionRecord ssd = CliDB.ScalingStatDistributionStorage.LookupByKey(bonusData.ScalingStatDistribution);
            if (ssd != null)
            {
                if (fixedLevel != 0)
                    level = fixedLevel;
                else
                    level = (uint)Math.Min(Math.Max((ushort)level, ssd.MinLevel), ssd.MaxLevel);

                ContentTuningRecord contentTuning = CliDB.ContentTuningStorage.LookupByKey(bonusData.ContentTuningId);
                if (contentTuning != null)
                    if ((contentTuning.Flags.HasAnyFlag(ContentTuningFlag.Unk0x02) || contentTuning.MinLevel != 0 || contentTuning.MaxLevel != 0) && !contentTuning.Flags.HasAnyFlag(ContentTuningFlag.DisabledForItem))
                        level = (uint)Math.Min(Math.Max(level, contentTuning.MinLevel), contentTuning.MaxLevel);

                uint heirloomIlvl = (uint)Global.DB2Mgr.GetCurveValueAt(ssd.PlayerLevelToItemLevelCurveID, level);
                if (heirloomIlvl != 0)
                    itemLevel = heirloomIlvl;
            }

            itemLevel += (uint)bonusData.ItemLevelBonus;

            for (uint i = 0; i < ItemConst.MaxGemSockets; ++i)
                itemLevel += bonusData.GemItemLevelBonus[i];

            uint itemLevelBeforeUpgrades = itemLevel;

            if (itemTemplate.GetInventoryType() != InventoryType.NonEquip)
            {
                if (minItemLevel != 0 && (minItemLevelCutoff == 0 || itemLevelBeforeUpgrades >= minItemLevelCutoff) && itemLevel < minItemLevel)
                    itemLevel = minItemLevel;

                if (maxItemLevel != 0 && itemLevel > maxItemLevel)
                    itemLevel = maxItemLevel;
            }

            return Math.Min(Math.Max(itemLevel, 1), 1300);
        }

        public float GetItemStatValue(uint index, Player owner)
        {
            Cypher.Assert(index < ItemConst.MaxStats);
            switch ((ItemModType)GetItemStatType(index))
            {
                case ItemModType.Corruption:
                case ItemModType.CorruptionResistance:
                    return _bonusData.StatPercentEditor[index];
                default:
                    break;
            }

            uint itemLevel = GetItemLevel(owner);
            float randomPropPoints = ItemEnchantmentManager.GetRandomPropertyPoints(itemLevel, GetQuality(), GetTemplate().GetInventoryType(), GetTemplate().GetSubClass());
            if (randomPropPoints != 0)
            {
                float statValue = _bonusData.StatPercentEditor[index] * randomPropPoints * 0.0001f;
                GtItemSocketCostPerLevelRecord gtCost = CliDB.ItemSocketCostPerLevelGameTable.GetRow(itemLevel);
                if (gtCost != null)
                    statValue -= _bonusData.ItemStatSocketCostMultiplier[index] * gtCost.SocketCost;

                return statValue;
            }

            return 0f;
        }

        public ItemDisenchantLootRecord GetDisenchantLoot(Player owner)
        {
            if (!_bonusData.CanDisenchant)
                return null;

            return GetDisenchantLoot(GetTemplate(), (uint)GetQuality(), GetItemLevel(owner));
        }

        public static ItemDisenchantLootRecord GetDisenchantLoot(ItemTemplate itemTemplate, uint quality, uint itemLevel)
        {
            if (itemTemplate.GetFlags().HasAnyFlag(ItemFlags.Conjured | ItemFlags.NoDisenchant) || itemTemplate.GetBonding() == ItemBondingType.Quest)
                return null;

            if (itemTemplate.GetArea(0) != 0 || itemTemplate.GetArea(1) != 0 || itemTemplate.GetMap() != 0 || itemTemplate.GetMaxStackSize() > 1)
                return null;

            if (GetSellPrice(itemTemplate, quality, itemLevel) == 0 && !Global.DB2Mgr.HasItemCurrencyCost(itemTemplate.GetId()))
                return null;

            byte itemClass = (byte)itemTemplate.GetClass();
            uint itemSubClass = itemTemplate.GetSubClass();
            byte expansion = itemTemplate.GetRequiredExpansion();
            foreach (ItemDisenchantLootRecord disenchant in CliDB.ItemDisenchantLootStorage.Values)
            {
                if (disenchant.Class != itemClass)
                    continue;

                if (disenchant.Subclass >= 0 && itemSubClass != 0)
                    continue;

                if (disenchant.Quality != quality)
                    continue;

                if (disenchant.MinLevel > itemLevel || disenchant.MaxLevel < itemLevel)
                    continue;

                if (disenchant.ExpansionID != -2 && disenchant.ExpansionID != expansion)
                    continue;

                return disenchant;
            }

            return null;
        }

        public uint GetDisplayId(Player owner)
        {
            uint itemModifiedAppearanceId = GetModifier(ItemConst.AppearanceModifierSlotBySpec[owner.GetActiveTalentGroup()]);
            if (itemModifiedAppearanceId == 0)
                itemModifiedAppearanceId = GetModifier(ItemModifier.TransmogAppearanceAllSpecs);

            ItemModifiedAppearanceRecord transmog = CliDB.ItemModifiedAppearanceStorage.LookupByKey(itemModifiedAppearanceId);
            if (transmog != null)
            {
                ItemAppearanceRecord itemAppearance = CliDB.ItemAppearanceStorage.LookupByKey(transmog.ItemAppearanceID);
                if (itemAppearance != null)
                    return itemAppearance.ItemDisplayInfoID;
            }

            return Global.DB2Mgr.GetItemDisplayId(GetEntry(), GetAppearanceModId());
        }

        public ItemModifiedAppearanceRecord GetItemModifiedAppearance() => Global.DB2Mgr.GetItemModifiedAppearance(GetEntry(), _bonusData.AppearanceModID);

        public uint GetModifier(ItemModifier modifier) => GetDynamicValue(ItemDynamicFields.Modifiers, (ushort)modifier);

        public void SetModifier(ItemModifier modifier, uint value)
        {
            ApplyFlag(ItemFields.ModifiersMask, 1 << (int)modifier, value != 0);
            // SetDynamicValue(ItemDynamicFields.Modifiers, (ushort)modifier, value);
        }

        public uint GetVisibleEntry(Player owner)
        {
            uint itemModifiedAppearanceId = GetModifier(ItemConst.AppearanceModifierSlotBySpec[owner.GetActiveTalentGroup()]);
            if (itemModifiedAppearanceId == 0)
                itemModifiedAppearanceId = GetModifier(ItemModifier.TransmogAppearanceAllSpecs);

            ItemModifiedAppearanceRecord transmog = CliDB.ItemModifiedAppearanceStorage.LookupByKey(itemModifiedAppearanceId);
            if (transmog != null)
                return transmog.ItemID;

            return GetEntry();
        }

        public uint GetVisibleAppearanceModId(Player owner)
        {
            uint itemModifiedAppearanceId = GetModifier(ItemConst.AppearanceModifierSlotBySpec[owner.GetActiveTalentGroup()]);
            if (itemModifiedAppearanceId == 0)
                itemModifiedAppearanceId = GetModifier(ItemModifier.TransmogAppearanceAllSpecs);

            ItemModifiedAppearanceRecord transmog = CliDB.ItemModifiedAppearanceStorage.LookupByKey(itemModifiedAppearanceId);
            if (transmog != null)
                return transmog.ItemAppearanceModifierID;

            return (ushort)GetAppearanceModId();
        }

        public uint GetVisibleEnchantmentId(Player owner)
        {
            uint enchantmentId = GetModifier(ItemConst.IllusionModifierSlotBySpec[owner.GetActiveTalentGroup()]);
            if (enchantmentId == 0)
                enchantmentId = GetModifier(ItemModifier.EnchantIllusionAllSpecs);

            if (enchantmentId == 0)
                enchantmentId = GetEnchantmentId(EnchantmentSlot.Perm);

            return enchantmentId;
        }

        public ushort GetVisibleItemVisual(Player owner)
        {
            SpellItemEnchantmentRecord enchant = CliDB.SpellItemEnchantmentStorage.LookupByKey(GetVisibleEnchantmentId(owner));
            if (enchant != null)
                return enchant.ItemVisual;

            return 0;
        }

        public void AddBonuses(uint bonusListID)
        {
            if (HasDynamicValue(ItemDynamicFields.BonusListIDs, bonusListID))
                return;

            var bonuses = Global.DB2Mgr.GetItemBonusList(bonusListID);
            if (bonuses != null)
            {
                AddDynamicValue(ItemDynamicFields.BonusListIDs, bonusListID);
                foreach (ItemBonusRecord bonus in bonuses)
                    _bonusData.AddBonus(bonus.Type, bonus.Value);

                SetUpdateField<uint>(ItemFields.ItemAppearanceModID, _bonusData.AppearanceModID);
            }
        }

        public void SetBonuses(List<uint> bonusListIDs)
        {
            if (bonusListIDs == null)
                bonusListIDs = new List<uint>();

            ClearDynamicValue(ItemDynamicFields.BonusListIDs);
            foreach (uint bonusListID in bonusListIDs)
            {
                _bonusData.AddBonusList(bonusListID);
                AddDynamicValue(ItemDynamicFields.BonusListIDs, bonusListID);
            }

            SetUpdateField<uint>(ItemFields.ItemAppearanceModID, _bonusData.AppearanceModID);
        }

        public void ClearBonuses()
        {
            ClearDynamicValue(ItemDynamicFields.BonusListIDs);
            _bonusData = new BonusData(GetTemplate());
            SetUpdateField<uint>(ItemFields.ItemAppearanceModID, _bonusData.AppearanceModID);
        }

        public ItemContext GetContext() => (ItemContext)GetUpdateField<int>(ItemFields.Context);
        public void SetContext(ItemContext context) => SetUpdateField<int>(ItemFields.Context, (int)context);

        public void SetPetitionId(uint petitionId)
        {
            // ItemEnchantment enchantmentField = m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Enchantment, 0);
            // SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.ID), petitionId);
        }
        public void SetPetitionNumSignatures(uint signatures)
        {
            // ItemEnchantment enchantmentField = m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Enchantment, 0);
            // SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.Duration), signatures);
        }

        public void SetFixedLevel(uint level)
        {
            if (!_bonusData.HasFixedLevel || GetModifier(ItemModifier.TimewalkerLevel) != 0)
                return;

            ScalingStatDistributionRecord ssd = CliDB.ScalingStatDistributionStorage.LookupByKey(_bonusData.ScalingStatDistribution);
            if (ssd != null)
            {
                level = (uint)Math.Min(Math.Max(level, ssd.MinLevel), ssd.MaxLevel);

                ContentTuningRecord contentTuning = CliDB.ContentTuningStorage.LookupByKey(_bonusData.ContentTuningId);
                if (contentTuning != null)
                    if ((contentTuning.Flags.HasAnyFlag(ContentTuningFlag.Unk0x02) || contentTuning.MinLevel != 0 || contentTuning.MaxLevel != 0) && !contentTuning.Flags.HasAnyFlag(ContentTuningFlag.DisabledForItem))
                        level = (uint)Math.Min(Math.Max(level, contentTuning.MinLevel), contentTuning.MaxLevel);

                SetModifier(ItemModifier.TimewalkerLevel, level);
            }
        }

        public int GetRequiredLevel()
        {
            if (_bonusData.RequiredLevelOverride != 0)
                return _bonusData.RequiredLevelOverride;
            else if (_bonusData.HasFixedLevel)
                return (int)GetModifier(ItemModifier.TimewalkerLevel);
            else
                return _bonusData.RequiredLevel;
        }

        public static Item NewItemOrBag(ItemTemplate proto)
        {
            if (proto.GetInventoryType() == InventoryType.Bag)
                return new Bag();

            return new Item();
        }

        public static void AddItemsSetItem(Player player, Item item)
        {
            ItemTemplate proto = item.GetTemplate();
            uint setid = proto.GetItemSet();

            ItemSetRecord set = CliDB.ItemSetStorage.LookupByKey(setid);
            if (set == null)
            {
                Log.outError(LogFilter.Sql, "Item set {0} for item (id {1}) not found, mods not applied.", setid, proto.GetId());
                return;
            }

            if (set.RequiredSkill != 0 && player.GetSkillValue((SkillType)set.RequiredSkill) < set.RequiredSkillRank)
                return;

            if (set.SetFlags.HasAnyFlag(ItemSetFlags.LegacyInactive))
                return;

            ItemSetEffect eff = null;
            for (int x = 0; x < player.ItemSetEff.Count; ++x)
            {
                if (player.ItemSetEff[x]?.ItemSetID == setid)
                {
                    eff = player.ItemSetEff[x];
                    break;
                }
            }

            if (eff == null)
            {
                eff = new ItemSetEffect();
                eff.ItemSetID = setid;

                int x = 0;
                for (; x < player.ItemSetEff.Count; ++x)
                    if (player.ItemSetEff[x] == null)
                        break;

                if (x < player.ItemSetEff.Count)
                    player.ItemSetEff[x] = eff;
                else
                    player.ItemSetEff.Add(eff);
            }

            ++eff.EquippedItemCount;

            List<ItemSetSpellRecord> itemSetSpells = Global.DB2Mgr.GetItemSetSpells(setid);
            foreach (var itemSetSpell in itemSetSpells)
            {
                //not enough for  spell
                if (itemSetSpell.Threshold > eff.EquippedItemCount)
                    continue;

                if (eff.SetBonuses.Contains(itemSetSpell))
                    continue;

                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(itemSetSpell.SpellID, Difficulty.None);
                if (spellInfo == null)
                {
                    Log.outError(LogFilter.Player, "WORLD: unknown spell id {0} in items set {1} effects", itemSetSpell.SpellID, setid);
                    continue;
                }

                eff.SetBonuses.Add(itemSetSpell);
                // spell cast only if fit form requirement, in other case will cast at form change
                if (itemSetSpell.ChrSpecID == 0 || itemSetSpell.ChrSpecID == player.GetPrimarySpecialization())
                    player.ApplyEquipSpell(spellInfo, null, true);
            }
        }

        public static void RemoveItemsSetItem(Player player, ItemTemplate proto)
        {
            uint setid = proto.GetItemSet();

            ItemSetRecord set = CliDB.ItemSetStorage.LookupByKey(setid);
            if (set == null)
            {
                Log.outError(LogFilter.Sql, "Item set {0} for item {1} not found, mods not removed.", setid, proto.GetId());
                return;
            }

            ItemSetEffect eff = null;
            int setindex = 0;
            for (; setindex < player.ItemSetEff.Count; setindex++)
            {
                if (player.ItemSetEff[setindex] != null && player.ItemSetEff[setindex].ItemSetID == setid)
                {
                    eff = player.ItemSetEff[setindex];
                    break;
                }
            }

            // can be in case now enough skill requirement for set appling but set has been appliend when skill requirement not enough
            if (eff == null)
                return;

            --eff.EquippedItemCount;

            List<ItemSetSpellRecord> itemSetSpells = Global.DB2Mgr.GetItemSetSpells(setid);
            foreach (ItemSetSpellRecord itemSetSpell in itemSetSpells)
            {
                // enough for spell
                if (itemSetSpell.Threshold <= eff.EquippedItemCount)
                    continue;

                if (!eff.SetBonuses.Contains(itemSetSpell))
                    continue;

                player.ApplyEquipSpell(Global.SpellMgr.GetSpellInfo(itemSetSpell.SpellID, Difficulty.None), null, false);
                eff.SetBonuses.Remove(itemSetSpell);
            }

            if (eff.EquippedItemCount == 0)                                    //all items of a set were removed
            {
                Cypher.Assert(eff == player.ItemSetEff[setindex]);
                player.ItemSetEff[setindex] = null;
            }
        }

        public BonusData GetBonus() => _bonusData;

        public ObjectGuid GetOwnerGUID() => GetUpdateField<ObjectGuid>(ItemFields.Owner);
        public void SetOwnerGUID(ObjectGuid guid) => SetUpdateField<ObjectGuid>(ItemFields.Owner, guid);
        public ObjectGuid GetContainedIn() => GetUpdateField<ObjectGuid>(ItemFields.ContainedIn);
        public void SetContainedIn(ObjectGuid guid) => SetUpdateField<ObjectGuid>(ItemFields.ContainedIn, guid);
        public ObjectGuid GetCreator() => GetUpdateField<ObjectGuid>(ItemFields.Creator);
        public void SetCreator(ObjectGuid guid) => SetUpdateField<ObjectGuid>(ItemFields.Creator, guid);
        public ObjectGuid GetGiftCreator() => GetUpdateField<ObjectGuid>(ItemFields.GiftCreator);
        public void SetGiftCreator(ObjectGuid guid) => SetUpdateField<ObjectGuid>(ItemFields.GiftCreator, guid);

        void SetExpiration(uint expiration) => SetUpdateField<uint>(ItemFields.Expiration, expiration);

        public ItemBondingType GetBonding() => _bonusData.Bonding;
        public void SetBinding(bool val)
        {
            if (val)
                AddItemFlag(ItemFieldFlags.Soulbound);
            else
                RemoveItemFlag(ItemFieldFlags.Soulbound);
        }

        public bool IsSoulBound() => HasItemFlag(ItemFieldFlags.Soulbound);
        public bool IsBoundAccountWide() => GetTemplate().GetFlags().HasAnyFlag(ItemFlags.IsBoundToAccount);
        public bool IsBattlenetAccountBound() => GetTemplate().GetFlags2().HasAnyFlag(ItemFlags2.BnetAccountTradeOk);

        public bool HasItemFlag(ItemFieldFlags flag) => (GetUpdateField<uint>(ItemFields.DynamicFlags) & (uint)flag) != 0;
        public void AddItemFlag(ItemFieldFlags flags) => AddFlag(ItemFields.DynamicFlags, flags);
        public void RemoveItemFlag(ItemFieldFlags flags) => RemoveFlag(ItemFields.DynamicFlags, flags);
        public void SetItemFlags(ItemFieldFlags flags) => SetUpdateField<uint>(ItemFields.DynamicFlags, (uint)flags);

        public Bag ToBag() => IsBag() ? this as Bag : null;

        public bool IsLocked() => !HasItemFlag(ItemFieldFlags.Unlocked);
        public bool IsBag() => GetTemplate().GetInventoryType() == InventoryType.Bag;
        public bool IsCurrencyToken() => GetTemplate().IsCurrencyToken();
        public bool IsBroken() => GetUpdateField<uint>(ItemFields.MaxDurability) > 0 && GetUpdateField<uint>(ItemFields.Durability) == 0;
        public void SetDurability(uint durability) => SetUpdateField<uint>(ItemFields.Durability, durability);
        public uint GetDurability() => GetUpdateField<uint>(ItemFields.Durability);
        public void SetMaxDurability(uint maxDurability) => SetUpdateField<uint>(ItemFields.MaxDurability, maxDurability);
        public uint GetMaxDurability() => GetUpdateField<uint>(ItemFields.MaxDurability);
        public void SetInTrade(bool isInTrade = true) => m_isInTrade = isInTrade;
        public bool IsInTrade() => m_isInTrade;

        public uint GetCount() => GetUpdateField<uint>(ItemFields.StackCount);
        public uint GetMaxStackCount() => GetTemplate().GetMaxStackSize();

        public byte GetSlot() => m_slot;
        public Bag GetContainer() => m_container;
        public void SetSlot(byte slot) => m_slot = slot;
        public ushort GetPos() => (ushort)(GetBagSlot() << 8 | GetSlot());
        public void SetContainer(Bag container) => m_container = container;

        bool IsInBag() => m_container != null;

        public uint GetItemRandomBonusListId() => m_randomBonusListId;
        public uint GetEnchantmentId(EnchantmentSlot slot) => GetUpdateField<uint>(ItemFields.Enchantment + (int)slot * (int)EnchantmentOffset.Max + (int)EnchantmentOffset.Id);
        public uint GetEnchantmentDuration(EnchantmentSlot slot) => GetUpdateField<uint>(ItemFields.Enchantment + (int)slot * (int)EnchantmentOffset.Max + (int)EnchantmentOffset.Duration);
        public int GetEnchantmentCharges(EnchantmentSlot slot) => GetUpdateField<int>(ItemFields.Enchantment + (int)slot * (int)EnchantmentOffset.Max + (int)EnchantmentOffset.Charges);

        public void SetCreatePlayedTime(uint createPlayedTime) => SetUpdateField<uint>(ItemFields.CreatePlayedTime, createPlayedTime);

        public string GetText() => m_text;
        public void SetText(string text) => m_text = text;

        public int GetSpellCharges(int index = 0) => GetUpdateField<int>(ItemFields.SpellCharges + index);
        public void SetSpellCharges(int index, int value) => SetUpdateField<int>(ItemFields.SpellCharges + index, value);

        public ItemUpdateState GetState() => uState;

        public bool IsInUpdateQueue() => uQueuePos != -1;
        public int GetQueuePos() => uQueuePos;
        public void FSetState(ItemUpdateState state) => uState = state;

        public override bool HasQuest(uint quest_id) => GetTemplate().GetStartQuest() == quest_id;
        public override bool HasInvolvedQuest(uint quest_id) => false;
        public bool IsPotion() => GetTemplate().IsPotion();
        public bool IsVellum() => GetTemplate().IsVellum();
        public bool IsConjuredConsumable() => GetTemplate().IsConjuredConsumable();
        public bool IsRangedWeapon() => GetTemplate().IsRangedWeapon();
        public ItemQuality GetQuality() => _bonusData.Quality;
        public int GetItemStatType(uint index)
        {
            Cypher.Assert(index < ItemConst.MaxStats);
            return _bonusData.ItemStatType[index];
        }
        public SocketColor GetSocketColor(uint index)
        {
            Cypher.Assert(index < ItemConst.MaxGemSockets);
            return _bonusData.socketColor[index];
        }
        public uint GetAppearanceModId() => GetUpdateField<uint>(ItemFields.ItemAppearanceModID);
        public void SetAppearanceModId(uint appearanceModId) => SetUpdateField<uint>(ItemFields.ItemAppearanceModID, appearanceModId);
        public float GetRepairCostMultiplier() => _bonusData.RepairCostMultiplier;
        public uint GetScalingStatDistribution() => _bonusData.ScalingStatDistribution;

        public void SetRefundRecipient(ObjectGuid guid) => m_refundRecipient = guid;
        public void SetPaidMoney(ulong money) => m_paidMoney = money;
        public void SetPaidExtendedCost(uint iece) => m_paidExtendedCost = iece;

        public ObjectGuid GetRefundRecipient() => m_refundRecipient;
        public ulong GetPaidMoney() => m_paidMoney;
        public uint GetPaidExtendedCost() => m_paidExtendedCost;

        public uint GetScriptId() => GetTemplate().ScriptId;

        public ObjectGuid GetChildItem() => m_childItem;
        public void SetChildItem(ObjectGuid childItem) => m_childItem = childItem;

        public ItemEffectRecord[] GetEffects() => _bonusData.Effects[0.._bonusData.EffectCount];
        public ItemEffectRecord GetEffect(int i)
        {
            Cypher.Assert(i < _bonusData.EffectCount, $"Attempted to get effect at index {i} but item has only {_bonusData.EffectCount} effects!");
            return _bonusData.Effects[i];
        }

        //Static
        public static bool ItemCanGoIntoBag(ItemTemplate pProto, ItemTemplate pBagProto)
        {
            if (pProto == null || pBagProto == null)
                return false;

            switch (pBagProto.GetClass())
            {
                case ItemClass.Container:
                    switch ((ItemSubClassContainer)pBagProto.GetSubClass())
                    {
                        case ItemSubClassContainer.Container:
                            return true;
                        case ItemSubClassContainer.SoulContainer:
                            if (!Convert.ToBoolean(pProto.GetBagFamily() & BagFamilyMask.SoulShards))
                                return false;
                            return true;
                        case ItemSubClassContainer.HerbContainer:
                            if (!Convert.ToBoolean(pProto.GetBagFamily() & BagFamilyMask.Herbs))
                                return false;
                            return true;
                        case ItemSubClassContainer.EnchantingContainer:
                            if (!Convert.ToBoolean(pProto.GetBagFamily() & BagFamilyMask.EnchantingSupp))
                                return false;
                            return true;
                        case ItemSubClassContainer.MiningContainer:
                            if (!Convert.ToBoolean(pProto.GetBagFamily() & BagFamilyMask.MiningSupp))
                                return false;
                            return true;
                        case ItemSubClassContainer.EngineeringContainer:
                            if (!Convert.ToBoolean(pProto.GetBagFamily() & BagFamilyMask.EngineeringSupp))
                                return false;
                            return true;
                        case ItemSubClassContainer.GemContainer:
                            if (!Convert.ToBoolean(pProto.GetBagFamily() & BagFamilyMask.Gems))
                                return false;
                            return true;
                        case ItemSubClassContainer.LeatherworkingContainer:
                            if (!Convert.ToBoolean(pProto.GetBagFamily() & BagFamilyMask.LeatherworkingSupp))
                                return false;
                            return true;
                        case ItemSubClassContainer.InscriptionContainer:
                            if (!Convert.ToBoolean(pProto.GetBagFamily() & BagFamilyMask.InscriptionSupp))
                                return false;
                            return true;
                        case ItemSubClassContainer.TackleContainer:
                            if (!Convert.ToBoolean(pProto.GetBagFamily() & BagFamilyMask.FishingSupp))
                                return false;
                            return true;
                        case ItemSubClassContainer.CookingContainer:
                            if (!pProto.GetBagFamily().HasAnyFlag(BagFamilyMask.CookingSupp))
                                return false;
                            return true;
                        default:
                            return false;
                    }
                //can remove?
                case ItemClass.Quiver:
                    switch ((ItemSubClassQuiver)pBagProto.GetSubClass())
                    {
                        case ItemSubClassQuiver.Quiver:
                            if (!Convert.ToBoolean(pProto.GetBagFamily() & BagFamilyMask.Arrows))
                                return false;
                            return true;
                        case ItemSubClassQuiver.AmmoPouch:
                            if (!Convert.ToBoolean(pProto.GetBagFamily() & BagFamilyMask.Bullets))
                                return false;
                            return true;
                        default:
                            return false;
                    }
            }
            return false;
        }

        public static uint ItemSubClassToDurabilityMultiplierId(ItemClass ItemClass, uint ItemSubClass)
        {
            switch (ItemClass)
            {
                case ItemClass.Weapon: return ItemSubClass;
                case ItemClass.Armor: return ItemSubClass + 21;
            }
            return 0;
        }

        #region Fields
        public bool m_lootGenerated;
        public Loot loot;
        internal BonusData _bonusData;

        ItemUpdateState uState;
        uint m_paidExtendedCost;
        ulong m_paidMoney;
        ObjectGuid m_refundRecipient;
        byte m_slot;
        Bag m_container;
        int uQueuePos;
        string m_text;
        bool m_isInTrade;
        long m_lastPlayedTimeUpdate;
        List<ObjectGuid> allowedGUIDs = new();
        uint m_randomBonusListId;        // store separately to easily find which bonus list is the one randomly given for stat rerolling
        ObjectGuid m_childItem;
        Dictionary<uint, ushort> m_artifactPowerIdToIndex = new();
        Array<uint> m_gemScalingLevels = new(ItemConst.MaxGemSockets);
        #endregion
    }

    public class ItemPosCount
    {
        public ItemPosCount(ushort _pos, uint _count)
        {
            pos = _pos;
            count = _count;
        }

        public bool IsContainedIn(List<ItemPosCount> vec)
        {
            foreach (var posCount in vec)
                if (posCount.pos == pos)
                    return true;
            return false;
        }

        public ushort pos;
        public uint count;
    }

    public class ItemSetEffect
    {
        public uint ItemSetID;
        public uint EquippedItemCount;
        public List<ItemSetSpellRecord> SetBonuses = new();
    }

    public class BonusData
    {
        public BonusData(ItemTemplate proto)
        {
            if (proto == null)
                return;

            Quality = proto.GetQuality();
            ItemLevelBonus = 0;
            RequiredLevel = proto.GetBaseRequiredLevel();
            for (uint i = 0; i < ItemConst.MaxStats; ++i)
                ItemStatType[i] = proto.GetStatModifierBonusStat(i);

            for (uint i = 0; i < ItemConst.MaxStats; ++i)
                StatPercentEditor[i] = proto.GetStatPercentEditor(i);

            for (uint i = 0; i < ItemConst.MaxStats; ++i)
                ItemStatSocketCostMultiplier[i] = proto.GetStatPercentageOfSocket(i);

            for (uint i = 0; i < ItemConst.MaxGemSockets; ++i)
            {
                socketColor[i] = proto.GetSocketColor(i);
                GemItemLevelBonus[i] = 0;
                GemRelicType[i] = -1;
                GemRelicRankBonus[i] = 0;
            }

            Bonding = proto.GetBonding();

            AppearanceModID = 0;
            RepairCostMultiplier = 1.0f;
            ScalingStatDistribution = proto.GetScalingStatDistribution();
            RelicType = -1;
            HasFixedLevel = false;
            RequiredLevelOverride = 0;

            EffectCount = 0;
            foreach (ItemEffectRecord itemEffect in proto.Effects)
                Effects[EffectCount++] = itemEffect;

            for (int i = EffectCount; i < Effects.Length; ++i)
                Effects[i] = null;

            CanDisenchant = !proto.GetFlags().HasAnyFlag(ItemFlags.NoDisenchant);
            CanScrap = proto.GetFlags4().HasAnyFlag(ItemFlags4.Scrapable);

            _state.SuffixPriority = int.MaxValue;
            _state.AppearanceModPriority = int.MaxValue;
            _state.ScalingStatDistributionPriority = int.MaxValue;
            _state.AzeriteTierUnlockSetPriority = int.MaxValue;
            _state.RequiredLevelCurvePriority = int.MaxValue;
            _state.HasQualityBonus = false;
        }

        public BonusData(ItemInstance itemInstance) : this(Global.ObjectMgr.GetItemTemplate(itemInstance.ItemID))
        {
            if (itemInstance.ItemBonus.HasValue)
            {
                foreach (uint bonusListID in itemInstance.ItemBonus.Value.BonusListIDs)
                    AddBonusList(bonusListID);
            }
        }

        public void AddBonusList(uint bonusListId)
        {
            var bonuses = Global.DB2Mgr.GetItemBonusList(bonusListId);
            if (bonuses != null)
                foreach (ItemBonusRecord bonus in bonuses)
                    AddBonus(bonus.Type, bonus.Value);
        }

        public void AddBonus(ItemBonusType type, int[] values)
        {
            switch (type)
            {
                case ItemBonusType.ItemLevel:
                    ItemLevelBonus += values[0];
                    break;
                case ItemBonusType.Stat:
                {
                    uint statIndex;
                    for (statIndex = 0; statIndex < ItemConst.MaxStats; ++statIndex)
                        if (ItemStatType[statIndex] == values[0] || ItemStatType[statIndex] == -1)
                            break;

                    if (statIndex < ItemConst.MaxStats)
                    {
                        ItemStatType[statIndex] = values[0];
                        StatPercentEditor[statIndex] += values[1];
                    }
                    break;
                }
                case ItemBonusType.Quality:
                    if (!_state.HasQualityBonus)
                    {
                        Quality = (ItemQuality)values[0];
                        _state.HasQualityBonus = true;
                    }
                    else if ((uint)Quality < values[0])
                        Quality = (ItemQuality)values[0];
                    break;
                case ItemBonusType.Suffix:
                    if (values[1] < _state.SuffixPriority)
                    {
                        Suffix = (uint)values[0];
                        _state.SuffixPriority = values[1];
                    }
                    break;
                case ItemBonusType.Socket:
                {
                    uint socketCount = (uint)values[0];
                    for (uint i = 0; i < ItemConst.MaxGemSockets && socketCount != 0; ++i)
                    {
                        if (socketColor[i] == 0)
                        {
                            socketColor[i] = (SocketColor)values[1];
                            --socketCount;
                        }
                    }
                    break;
                }
                case ItemBonusType.Appearance:
                    if (values[1] < _state.AppearanceModPriority)
                    {
                        AppearanceModID = Convert.ToUInt32(values[0]);
                        _state.AppearanceModPriority = values[1];
                    }
                    break;
                case ItemBonusType.RequiredLevel:
                    RequiredLevel += values[0];
                    break;
                case ItemBonusType.RepairCostMuliplier:
                    RepairCostMultiplier *= Convert.ToSingle(values[0]) * 0.01f;
                    break;
                case ItemBonusType.ScalingStatDistribution:
                case ItemBonusType.ScalingStatDistributionFixed:
                    if (values[1] < _state.ScalingStatDistributionPriority)
                    {
                        ScalingStatDistribution = (uint)values[0];
                        ContentTuningId = (uint)values[2];
                        _state.ScalingStatDistributionPriority = values[1];
                        HasFixedLevel = type == ItemBonusType.ScalingStatDistributionFixed;
                    }
                    break;
                case ItemBonusType.Bounding:
                    Bonding = (ItemBondingType)values[0];
                    break;
                case ItemBonusType.RelicType:
                    RelicType = values[0];
                    break;
                case ItemBonusType.OverrideRequiredLevel:
                    RequiredLevelOverride = values[0];
                    break;
                case ItemBonusType.OverrideCanDisenchant:
                    CanDisenchant = values[0] != 0;
                    break;
                case ItemBonusType.OverrideCanScrap:
                    CanScrap = values[0] != 0;
                    break;
                case ItemBonusType.ItemEffectId:
                    ItemEffectRecord itemEffect = CliDB.ItemEffectStorage.LookupByKey(values[0]);
                    if (itemEffect != null)
                        Effects[EffectCount++] = itemEffect;
                    break;
                case ItemBonusType.RequiredLevelCurve:
                    if (values[2] < _state.RequiredLevelCurvePriority)
                    {
                        RequiredLevelCurve = (uint)values[0];
                        _state.RequiredLevelCurvePriority = values[2];
                        if (values[1] != 0)
                            ContentTuningId = (uint)values[1];
                    }
                    break;
            }
        }

        public ItemQuality Quality;
        public int ItemLevelBonus;
        public int RequiredLevel;
        public int[] ItemStatType = new int[ItemConst.MaxStats];
        public int[] StatPercentEditor = new int[ItemConst.MaxStats];
        public float[] ItemStatSocketCostMultiplier = new float[ItemConst.MaxStats];
        public SocketColor[] socketColor = new SocketColor[ItemConst.MaxGemSockets];
        public ItemBondingType Bonding;
        public uint AppearanceModID;
        public float RepairCostMultiplier;
        public uint ScalingStatDistribution;
        public uint ContentTuningId;
        public uint DisenchantLootId;
        public uint[] GemItemLevelBonus = new uint[ItemConst.MaxGemSockets];
        public int[] GemRelicType = new int[ItemConst.MaxGemSockets];
        public ushort[] GemRelicRankBonus = new ushort[ItemConst.MaxGemSockets];
        public int RelicType;
        public int RequiredLevelOverride;
        public uint Suffix;
        public uint RequiredLevelCurve;
        public ItemEffectRecord[] Effects = new ItemEffectRecord[13];
        public int EffectCount;
        public bool CanDisenchant;
        public bool CanScrap;
        public bool HasFixedLevel;
        State _state;

        struct State
        {
            public int SuffixPriority;
            public int AppearanceModPriority;
            public int ScalingStatDistributionPriority;
            public int AzeriteTierUnlockSetPriority;
            public int RequiredLevelCurvePriority;
            public bool HasQualityBonus;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class ItemDynamicFieldGems
    {
        public uint ItemId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public ushort[] BonusListIDs = new ushort[16];
        public byte Context;
    }
}
