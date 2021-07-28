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
using Framework.Database;
using Game.DataStorage;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entities
{
    public class CollectionMgr
    {
        static Dictionary<uint, uint> FactionSpecificMounts = new();

        WorldSession _owner;
        Dictionary<uint, HeirloomData> _heirlooms = new();
        Dictionary<uint, MountStatusFlags> _mounts = new();

        public static void LoadMountDefinitions()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT spellId, otherFactionSpellId FROM mount_definitions");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 mount definitions. DB table `mount_definitions` is empty.");
                return;
            }

            do
            {
                uint spellId = result.Read<uint>(0);
                uint otherFactionSpellId = result.Read<uint>(1);

                if (Global.DB2Mgr.GetMount(spellId) == null)
                {
                    Log.outError(LogFilter.Sql, "Mount spell {0} defined in `mount_definitions` does not exist in Mount.db2, skipped", spellId);
                    continue;
                }

                if (otherFactionSpellId != 0 && Global.DB2Mgr.GetMount(otherFactionSpellId) == null)
                {
                    Log.outError(LogFilter.Sql, "otherFactionSpellId {0} defined in `mount_definitions` for spell {1} does not exist in Mount.db2, skipped", otherFactionSpellId, spellId);
                    continue;
                }

                FactionSpecificMounts[spellId] = otherFactionSpellId;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} mount definitions in {1} ms", FactionSpecificMounts.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        public CollectionMgr(WorldSession owner)
        {
            _owner = owner;
        }

        public void OnItemAdded(Item item)
        {
            if (Global.DB2Mgr.GetHeirloomByItemId(item.GetEntry()) != null)
                AddHeirloom(item.GetEntry(), 0);
        }

        public void LoadAccountHeirlooms(SQLResult result)
        {
            if (result.IsEmpty())
                return;

            do
            {
                uint itemId = result.Read<uint>(0);
                HeirloomPlayerFlags flags = (HeirloomPlayerFlags)result.Read<uint>(1);

                HeirloomRecord heirloom = Global.DB2Mgr.GetHeirloomByItemId(itemId);
                if (heirloom == null)
                    continue;

                uint bonusId = 0;

                if (flags.HasAnyFlag(HeirloomPlayerFlags.BonusLevel120))
                    bonusId = heirloom.UpgradeItemBonusListID[3];
                else if (flags.HasAnyFlag(HeirloomPlayerFlags.BonusLevel110))
                    bonusId = heirloom.UpgradeItemBonusListID[2];
                else if (flags.HasAnyFlag(HeirloomPlayerFlags.BonusLevel100))
                    bonusId = heirloom.UpgradeItemBonusListID[1];
                else if (flags.HasAnyFlag(HeirloomPlayerFlags.BonusLevel90))
                    bonusId = heirloom.UpgradeItemBonusListID[0];

                _heirlooms[itemId] = new HeirloomData(flags, bonusId);
            } while (result.NextRow());
        }

        public void SaveAccountHeirlooms(SQLTransaction trans)
        {
            PreparedStatement stmt;
            foreach (var heirloom in _heirlooms)
            {
                stmt = DB.Login.GetPreparedStatement(LoginStatements.REP_ACCOUNT_HEIRLOOMS);
                stmt.AddValue(0, _owner.GetBattlenetAccountId());
                stmt.AddValue(1, heirloom.Key);
                stmt.AddValue(2, (uint)heirloom.Value.flags);
                trans.Append(stmt);
            }
        }

        bool UpdateAccountHeirlooms(uint itemId, HeirloomPlayerFlags flags)
        {
            if (_heirlooms.ContainsKey(itemId))
                return false;

            _heirlooms.Add(itemId, new HeirloomData(flags, 0));
            return true;
        }

        public uint GetHeirloomBonus(uint itemId)
        {
            var data = _heirlooms.LookupByKey(itemId);
            if (data != null)
                return data.bonusId;

            return 0;
        }

        public void LoadHeirlooms()
        {
            // foreach (var item in _heirlooms)
            //     _owner.GetPlayer().AddHeirloom(item.Key, (uint)item.Value.flags);
        }

        public void AddHeirloom(uint itemId, HeirloomPlayerFlags flags)
        {
            // if (UpdateAccountHeirlooms(itemId, flags))
            //     _owner.GetPlayer().AddHeirloom(itemId, (uint)flags);
        }

        public void UpgradeHeirloom(uint itemId, uint castItem)
        {
            Player player = _owner.GetPlayer();
            if (!player)
                return;

            HeirloomRecord heirloom = Global.DB2Mgr.GetHeirloomByItemId(itemId);
            if (heirloom == null)
                return;

            var data = _heirlooms.LookupByKey(itemId);
            if (data == null)
                return;

            HeirloomPlayerFlags flags = data.flags;
            uint bonusId = 0;

            if (heirloom.UpgradeItemID[0] == castItem)
            {
                flags |= HeirloomPlayerFlags.BonusLevel90;
                bonusId = heirloom.UpgradeItemBonusListID[0];
            }
            if (heirloom.UpgradeItemID[1] == castItem)
            {
                flags |= HeirloomPlayerFlags.BonusLevel100;
                bonusId = heirloom.UpgradeItemBonusListID[1];
            }
            if (heirloom.UpgradeItemID[2] == castItem)
            {
                flags |= HeirloomPlayerFlags.BonusLevel110;
                bonusId = heirloom.UpgradeItemBonusListID[2];
            }
            if (heirloom.UpgradeItemID[3] == castItem)
            {
                flags |= HeirloomPlayerFlags.BonusLevel120;
                bonusId = heirloom.UpgradeItemBonusListID[3];
            }

            foreach (Item item in player.GetItemListByEntry(itemId, true))
                item.AddBonuses(bonusId);

            // Get heirloom offset to update only one part of dynamic field
            // List<uint> heirlooms = player.m_activePlayerData.Heirlooms;
            // int offset = heirlooms.IndexOf(itemId);
            //
            // player.SetHeirloomFlags(offset, (uint)flags);
            // data.flags = flags;
            // data.bonusId = bonusId;
        }

        public void CheckHeirloomUpgrades(Item item)
        {
            Player player = _owner.GetPlayer();
            if (!player)
                return;

            // Check already owned heirloom for upgrade kits
            HeirloomRecord heirloom = Global.DB2Mgr.GetHeirloomByItemId(item.GetEntry());
            if (heirloom != null)
            {
                var data = _heirlooms.LookupByKey(item.GetEntry());
                if (data == null)
                    return;

                // Check for heirloom pairs (normal - heroic, heroic - mythic)
                uint heirloomItemId = heirloom.StaticUpgradedItemID;
                uint newItemId = 0;
                HeirloomRecord heirloomDiff;
                while ((heirloomDiff = Global.DB2Mgr.GetHeirloomByItemId(heirloomItemId)) != null)
                {
                    if (player.GetItemByEntry(heirloomDiff.ItemID))
                        newItemId = heirloomDiff.ItemID;

                    HeirloomRecord heirloomSub = Global.DB2Mgr.GetHeirloomByItemId(heirloomDiff.StaticUpgradedItemID);
                    if (heirloomSub != null)
                    {
                        heirloomItemId = heirloomSub.ItemID;
                        continue;
                    }

                    break;
                }

                if (newItemId != 0)
                {
                    // List<uint> heirlooms = player.m_activePlayerData.Heirlooms;
                    // int offset = heirlooms.IndexOf(item.GetEntry());
                    //
                    // player.SetHeirloom(offset, newItemId);
                    // player.SetHeirloomFlags(offset, 0);

                    _heirlooms.Remove(item.GetEntry());
                    _heirlooms[newItemId] = null;

                    return;
                }

                uint[] bonusListIDs = item.GetDynamicValues(ItemDynamicFields.BonusListIDs);
                foreach (uint bonusId in bonusListIDs)
                {
                    if (bonusId != data.bonusId)
                    {
                        item.ClearBonuses();
                        break;
                    }
                }

                if (!bonusListIDs.Contains(data.bonusId))
                    item.AddBonuses(data.bonusId);
            }
        }

        public bool CanApplyHeirloomXpBonus(uint itemId, uint level)
        {
            if (Global.DB2Mgr.GetHeirloomByItemId(itemId) == null)
                return false;

            var data = _heirlooms.LookupByKey(itemId);
            if (data == null)
                return false;

            if (data.flags.HasAnyFlag(HeirloomPlayerFlags.BonusLevel120))
                return level <= 120;
            if (data.flags.HasAnyFlag(HeirloomPlayerFlags.BonusLevel110))
                return level <= 110;
            if (data.flags.HasAnyFlag(HeirloomPlayerFlags.BonusLevel100))
                return level <= 100;
            if (data.flags.HasAnyFlag(HeirloomPlayerFlags.BonusLevel90))
                return level <= 90;

            return level <= 60;
        }

        public void LoadMounts()
        {
            foreach (var m in _mounts.ToList())
                AddMount(m.Key, m.Value, false, false);
        }

        public void LoadAccountMounts(SQLResult result)
        {
            if (result.IsEmpty())
                return;

            do
            {
                uint mountSpellId = result.Read<uint>(0);
                MountStatusFlags flags = (MountStatusFlags)result.Read<byte>(1);

                if (Global.DB2Mgr.GetMount(mountSpellId) == null)
                    continue;

                _mounts[mountSpellId] = flags;
            } while (result.NextRow());
        }

        public void SaveAccountMounts(SQLTransaction trans)
        {
            foreach (var mount in _mounts)
            {
                PreparedStatement stmt = DB.Login.GetPreparedStatement(LoginStatements.REP_ACCOUNT_MOUNTS);
                stmt.AddValue(0, _owner.GetBattlenetAccountId());
                stmt.AddValue(1, mount.Key);
                stmt.AddValue(2, (byte)mount.Value);
                trans.Append(stmt);
            }
        }

        public bool AddMount(uint spellId, MountStatusFlags flags, bool factionMount = false, bool learned = false)
        {
            Player player = _owner.GetPlayer();
            if (!player)
                return false;

            MountRecord mount = Global.DB2Mgr.GetMount(spellId);
            if (mount == null)
                return false;

            var value = FactionSpecificMounts.LookupByKey(spellId);
            if (value != 0 && !factionMount)
                AddMount(value, flags, true, learned);

            _mounts[spellId] = flags;

            // Mount condition only applies to using it, should still learn it.
            if (mount.PlayerConditionID != 0)
            {
                PlayerConditionRecord playerCondition = CliDB.PlayerConditionStorage.LookupByKey(mount.PlayerConditionID);
                if (playerCondition != null && !ConditionManager.IsPlayerMeetingCondition(player, playerCondition))
                    return false;
            }

            if (!learned)
            {
                if (!player.HasSpell(spellId))
                    player.LearnSpell(spellId, true);
            }

            return true;
        }

        public Dictionary<uint, HeirloomData> GetAccountHeirlooms() => _heirlooms;
        public Dictionary<uint, MountStatusFlags> GetAccountMounts() => _mounts;
    }

    public class HeirloomData
    {
        public HeirloomPlayerFlags flags;
        public uint bonusId;

        public HeirloomData(HeirloomPlayerFlags _flags = 0, uint _bonusId = 0)
        {
            flags = _flags;
            bonusId = _bonusId;
        }
    }
}
