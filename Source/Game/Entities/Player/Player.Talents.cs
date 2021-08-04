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
using Game.Spells;
using System.Collections.Generic;

namespace Game.Entities
{
    public partial class Player
    {
        public void InitTalentForLevel()
        {
            uint level = GetLevel();
            // talents base at level diff (talents = level - 9 but some can be used already)
            if (level < PlayerConst.MinSpecializationLevel)
            {
                // Remove all talent points
                if (specializationInfo.UsedTalentCount > 0)
                {
                    ResetTalents(true);
                    SetFreeTalentPoints(0);
                }
            }
            else
            {
                if (level < WorldConfig.GetIntValue(WorldCfg.MinDualspecLevel) || specializationInfo.SpecCount == 0)
                {
                    specializationInfo.SpecCount = 1;
                    specializationInfo.ActiveSpec = 0;
                }

                uint talentPointsForLevel = CalculateTalentsPoints();

                // If used more that have then reset
                if (specializationInfo.UsedTalentCount > talentPointsForLevel)
                {
                    if (!GetSession().HasPermission(RBACPermissions.SkipCheckMoreTalentsThanAllowed))
                        ResetTalents(true);
                    else
                        SetFreeTalentPoints(0);
                }
                // else update amount of free points
                else
                    SetFreeTalentPoints(talentPointsForLevel - specializationInfo.UsedTalentCount);
            }

            if (!GetSession().PlayerLoading())
                SendTalentsInfoData();   // update at client
        }

        public void SetFreeTalentPoints(uint points)
        {
            Global.ScriptMgr.OnPlayerFreeTalentPointsChanged(this, points);
            SetUpdateField<uint>(ActivePlayerFields.CharacterPoints, points);
        }

        public uint CalculateTalentsPoints() => GetLevel() < 10 ? 0 : GetLevel() - 9;

        public bool AddTalent(uint spellId, byte spec, bool learning)
        {
            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
            if (spellInfo == null)
            {
                // Do character spell book cleanup (all characters)
                if (!IsInWorld && !learning)
                {
                    Log.outError(LogFilter.Spells, $"Player::AddTalent: Spell (ID: {spellId} does not exist, Deleting for all characters in `character_spell` and `character_talent`.");
                    DeleteSpellFromAllPlayers(spellId);
                }
                else
                    Log.outError(LogFilter.Spells, $"Player::AddTalent: Spell (ID: {spellId} does not exist");

                return false;
            }

            if (!Global.SpellMgr.IsSpellValid(spellInfo, this, false))
            {
                // Do character spell book cleanup (all characters)
                if (!IsInWorld && !learning)
                {
                    Log.outError(LogFilter.Spells, $"Player::AddTalent: Spell (ID: {spellId} is invalid, Deleting for all characters in `character_spell` and `character_talent`.");
                    DeleteSpellFromAllPlayers(spellId);
                }
                else
                    Log.outError(LogFilter.Spells, $"Player::AddTalent: Spell (ID: {spellId} is invalid");

                return false;
            }

            var talentMap = GetTalentMap(GetActiveTalentGroup());
            if (talentMap.ContainsKey(spellId))
                talentMap[spellId] = PlayerSpellState.Unchanged;

            TalentSpellPos talentPos = Global.DB2Mgr.GetTalentSpellPos(spellId);
            if (talentPos != null)
            {
                TalentRecord talentInfo = CliDB.TalentStorage.LookupByKey(talentPos.TalentID);
                if (talentInfo != null)
                {
                    for (byte rank = 0; rank < PlayerConst.MaxTalentRank; ++rank)
                    {
                        // Skip learning spell and no rank spell case
                        uint rankSpellId = talentInfo.SpellRank[rank];
                        if (rankSpellId == 0 || rankSpellId == spellId)
                            continue;

                        if (talentMap.ContainsKey(rankSpellId))
                            talentMap[rankSpellId] = PlayerSpellState.Removed;
                    }
                }

                if (GetTalentMap(GetActiveTalentGroup()).ContainsKey(spellId))
                    GetTalentMap(GetActiveTalentGroup())[spellId] = PlayerSpellState.Unchanged;
                else
                    GetTalentMap(GetActiveTalentGroup())[spellId] = learning ? PlayerSpellState.New : PlayerSpellState.Unchanged;
            }

            return true;
        }

        public void RemoveTalent(uint spellId)
        {
            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
            if (spellInfo == null)
                return;

            RemoveSpell(spellId, true);

            // search for spells that the talent teaches and unlearn them
            foreach (SpellEffectInfo effect in spellInfo.GetEffects())
                if (effect != null && effect.TriggerSpell > 0 && effect.Effect == SpellEffectName.LearnSpell)
                    RemoveSpell(effect.TriggerSpell, true);

            var talentMap = GetTalentMap(GetActiveTalentGroup());
            // if this talent rank can be found in the PlayerTalentMap, mark the talent as removed so it gets deleted
            if (talentMap.ContainsKey(spellId))
                talentMap[spellId] = PlayerSpellState.Removed;
        }

        public uint GetFreeTalentPoints() => GetUpdateField<uint>(ActivePlayerFields.CharacterPoints);

        public void LearnTalent(uint talentId, uint talentRank)
        {
            uint currentTalentPoints = GetFreeTalentPoints();
            if (currentTalentPoints == 0)
                return;

            if (talentRank >= PlayerConst.MaxTalentRank)
                return;

            TalentRecord talent = CliDB.TalentStorage.LookupByKey(talentId);
            if (talent == null)
                return;

            TalentTabRecord talentTabl = CliDB.TalentTabStorage.LookupByKey(talent.TabID);
            if (talentTabl == null)
                return;

            // Prevent learning talent for different class
            if ((int)GetClass() != talent.ClassID)
                return;

            // Find current max talent rank (0-9)
            int currentTalentAtMaxRank = 0;
            for (int rank = PlayerConst.MaxTalentRank - 1; rank >= 0; rank--)
            {
                if (talent.SpellRank[rank] != 0 && HasSpell(talent.SpellRank[rank]))
                {
                    currentTalentAtMaxRank = (rank + 1);
                    break;
                }
            }

            // We already have same or higher talent rank learned
            if (currentTalentAtMaxRank >= (talentRank + 1))
                return;

            // Check if we have enough talent points
            if (currentTalentPoints < (talentRank - currentTalentAtMaxRank + 1))
                return;

            // Check if it requires another talent
            if (talent.PrereqTalent[0] > 0)
            {
                TalentRecord prereqTalent = CliDB.TalentStorage.LookupByKey(talent.PrereqTalent[0]);
                if (prereqTalent != null)
                {
                    bool hasEnoughRank = false;
                    for (int rank = talent.PrereqRank[0]; rank < PlayerConst.MaxTalentRank; rank++)
                    {
                        if (prereqTalent.SpellRank[rank] != 0)
                            if (HasSpell(prereqTalent.SpellRank[rank]))
                                hasEnoughRank = true;
                    }
                    if (!hasEnoughRank)
                        return;
                }
            }

            // Find out how many points we have in this field
            int spentPoints = 0;

            uint tab = talent.TabID;
            if (talent.TierID > 0)
            {
                foreach (var tempTalent in CliDB.TalentStorage.Values)
                {
                    if (tempTalent.TabID != tab)
                        continue;

                    for (int rank = 0; rank < PlayerConst.MaxTalentRank; rank++)
                    {
                        if (tempTalent.SpellRank[rank] == 0)
                            continue;

                        if (HasSpell(tempTalent.SpellRank[rank]))
                            spentPoints += (rank + 1);
                    }
                }
            }

            // We do not have required min points spent in the talent tree
            if (spentPoints < (talent.TierID * PlayerConst.MaxTalentRank))
                return;

            // Spell not set in Talent.db2
            uint spellId = talent.SpellRank[talentRank];
            if (spellId == 0)
            {
                Log.outError(LogFilter.Player, $"Player::LearnTalent: Talent.db2 has no spellinfo for Talent: {talentId} (spell id = 0)");
                return;
            }

            // Already known
            if (HasSpell(spellId))
                return;

            // Learn! (Other talent ranks will unlearned at learning
            LearnSpell(spellId, false);
            AddTalent(spellId, specializationInfo.ActiveSpec, true);

            Log.outDebug(LogFilter.Misc, $"Player::LearnTalent: TalentID: {talentId} Spell: {spellId} Group: {specializationInfo.ActiveSpec}");

            // Update free talent points
            SetFreeTalentPoints((uint)(currentTalentPoints - (talentRank - currentTalentAtMaxRank + 1)));
        }

        bool HasTalent(uint spellId, byte group) => GetTalentMap(group).ContainsKey(spellId) && GetTalentMap(group)[spellId] != PlayerSpellState.Removed;

        uint GetTalentResetCost() => specializationInfo.ResetTalentsCost;
        void SetTalentResetCost(uint cost) => specializationInfo.ResetTalentsCost = cost;
        long GetTalentResetTime() => specializationInfo.ResetTalentsTime;
        void SetTalentResetTime(long time_) => specializationInfo.ResetTalentsTime = time_;
        public uint GetPrimarySpecialization() => GetUpdateField<uint>(PlayerFields.CurrentSpecID);
        void SetPrimarySpecialization(uint spec) => SetUpdateField<uint>(PlayerFields.CurrentSpecID, spec);
        public byte GetActiveTalentGroup() => specializationInfo.ActiveSpec;
        void SetActiveTalentGroup(byte group) => specializationInfo.ActiveSpec = group;

        // Loot Spec
        public void SetLootSpecId(uint id) => SetUpdateField<uint>(ActivePlayerFields.LootSpecID, id);
        public uint GetLootSpecId() => GetUpdateField<uint>(ActivePlayerFields.LootSpecID);

        public Dictionary<uint, PlayerSpellState> GetTalentMap(uint spec) => specializationInfo.Talents[spec];

        public uint ResetTalentsCost()
        {
            // The first time reset costs 1 gold
            if (GetTalentResetCost() < 1 * MoneyConstants.Gold)
                return 1 * MoneyConstants.Gold;
            // then 5 gold
            else if (GetTalentResetCost() < 5 * MoneyConstants.Gold)
                return 5 * MoneyConstants.Gold;
            // After that it increases in increments of 5 gold
            else if (GetTalentResetCost() < 10 * MoneyConstants.Gold)
                return 10 * MoneyConstants.Gold;
            else
            {
                ulong months = (ulong)(GameTime.GetGameTime() - GetTalentResetTime()) / Time.Month;
                if (months > 0)
                {
                    // This cost will be reduced by a rate of 5 gold per month
                    uint new_cost = (uint)(GetTalentResetCost() - 5 * MoneyConstants.Gold * months);
                    // to a minimum of 10 gold.
                    return new_cost < 10 * MoneyConstants.Gold ? 10 * MoneyConstants.Gold : new_cost;
                }
                else
                {
                    // After that it increases in increments of 5 gold
                    uint new_cost = GetTalentResetCost() + 5 * MoneyConstants.Gold;
                    // until it hits a cap of 50 gold.
                    if (new_cost > 50 * MoneyConstants.Gold)
                        new_cost = 50 * MoneyConstants.Gold;
                    return new_cost;
                }
            }
        }

        public bool ResetTalents(bool noCost = false)
        {
            Global.ScriptMgr.OnPlayerTalentsReset(this, noCost);

            // not need after this call
            if (HasAtLoginFlag(AtLoginFlags.ResetTalents))
                RemoveAtLoginFlag(AtLoginFlags.ResetTalents, true);

            uint talentPointsForLevel = CalculateTalentsPoints();

            if (specializationInfo.UsedTalentCount == 0)
            {
                SetFreeTalentPoints(talentPointsForLevel);
                return false;
            }

            uint cost = 0;
            if (!noCost && !WorldConfig.GetBoolValue(WorldCfg.NoResetTalentCost))
            {
                cost = ResetTalentsCost();

                if (!HasEnoughMoney(cost))
                {
                    SendBuyError(BuyResult.NotEnoughtMoney, null, 0);
                    return false;
                }
            }

            RemovePet(null, PetSaveMode.NotInSlot, true);

            foreach (var talentInfo in CliDB.TalentStorage.Values)
            {
                // unlearn only talents for character class
                // some spell learned by one class as normal spells or know at creation but another class learn it as talent,
                // to prevent unexpected lost normal learned spell skip another class talents
                if (talentInfo.ClassID != (uint)GetClass())
                    continue;

                for (int rank = PlayerConst.MaxTalentRank - 1; rank >= 0; -- rank)
                {
                    // Skip non-existing talent ranks
                    if (talentInfo.SpellRank[rank] == 0)
                        continue;

                    RemoveTalent(talentInfo.SpellRank[rank]);
                }
            }

            SQLTransaction trans = new();
            _SaveTalents(trans);
            _SaveSpells(trans);
            DB.Characters.CommitTransaction(trans);

            if (!noCost)
            {
                ModifyMoney(-cost);

                SetTalentResetCost(cost);
                SetTalentResetTime(GameTime.GetGameTime());
            }

            return true;
        }

        public void SendTalentsInfoData()
        {
            //UpdateTalentData packet = new();
            //packet.Info.PrimarySpecialization = GetPrimarySpecialization();

            //for (byte i = 0; i < PlayerConst.MaxSpecializations; ++i)
            //{
            //    ChrSpecializationRecord spec = Global.DB2Mgr.GetChrSpecializationByIndex(GetClass(), i);
            //    if (spec == null)
            //        continue;

            //    var talents = GetTalentMap(i);
            //    var pvpTalents = GetPvpTalentMap(i);

            //    UpdateTalentData.TalentGroupInfo groupInfoPkt = new();
            //    groupInfoPkt.SpecID = spec.Id;

            //    foreach (var pair in talents)
            //    {
            //        if (pair.Value == PlayerSpellState.Removed)
            //            continue;

            //        TalentRecord talentInfo = CliDB.TalentStorage.LookupByKey(pair.Key);
            //        if (talentInfo == null)
            //        {
            //            Log.outError(LogFilter.Player, "Player {0} has unknown talent id: {1}", GetName(), pair.Key);
            //            continue;
            //        }

            //        SpellInfo spellEntry = Global.SpellMgr.GetSpellInfo(talentInfo.SpellID, Difficulty.None);
            //        if (spellEntry == null)
            //        {
            //            Log.outError(LogFilter.Player, "Player {0} has unknown talent spell: {1}", GetName(), talentInfo.SpellID);
            //            continue;
            //        }

            //        groupInfoPkt.TalentIDs.Add((ushort)pair.Key);
            //    }

            //    for (byte slot = 0; slot < PlayerConst.MaxPvpTalentSlots; ++slot)
            //    {
            //        if (pvpTalents[slot] == 0)
            //            continue;

            //        PvpTalentRecord talentInfo = CliDB.PvpTalentStorage.LookupByKey(pvpTalents[slot]);
            //        if (talentInfo == null)
            //        {
            //            Log.outError(LogFilter.Player, $"Player.SendTalentsInfoData: Player '{GetName()}' ({GetGUID()}) has unknown pvp talent id: {pvpTalents[slot]}");
            //            continue;
            //        }

            //        SpellInfo spellEntry = Global.SpellMgr.GetSpellInfo(talentInfo.SpellID, Difficulty.None);
            //        if (spellEntry == null)
            //        {
            //            Log.outError(LogFilter.Player, $"Player.SendTalentsInfoData: Player '{GetName()}' ({GetGUID()}) has unknown pvp talent spell: {talentInfo.SpellID}");
            //            continue;
            //        }

            //        PvPTalent pvpTalent = new();
            //        pvpTalent.PvPTalentID = (ushort)pvpTalents[slot];
            //        pvpTalent.Slot = slot;
            //        groupInfoPkt.PvPTalents.Add(pvpTalent);
            //    }

            //    if (i == GetActiveTalentGroup())
            //        packet.Info.ActiveGroup = (byte)packet.Info.TalentGroups.Count;

            //    if (!groupInfoPkt.TalentIDs.Empty() || !groupInfoPkt.PvPTalents.Empty() || i == GetActiveTalentGroup())
            //        packet.Info.TalentGroups.Add(groupInfoPkt);
            //}

            //SendPacket(packet);
        }

        public void SendRespecWipeConfirm(ObjectGuid guid, uint cost)
        {
            RespecWipeConfirm respecWipeConfirm = new();
            respecWipeConfirm.RespecMaster = guid;
            respecWipeConfirm.Cost = cost;
            respecWipeConfirm.RespecType = SpecResetType.Talents;
            SendPacket(respecWipeConfirm);
        }
    }
}
