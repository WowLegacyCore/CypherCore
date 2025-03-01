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
using Framework.Database;
using Game.Arenas;
using Game.BattleFields;
using Game.BattleGrounds;
using Game.DataStorage;
using Game.Entities;
using Game.Garrisons;
using Game.Maps;
using Game.Networking;
using Game.Networking.Packets;
using Game.Scenarios;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Game.Achievements
{
    public class CriteriaHandler
    {
        protected Dictionary<uint, CriteriaProgress> _criteriaProgress = new();
        Dictionary<uint, uint /*ms time left*/> _timeCriteriaTrees = new();

        public virtual void Reset()
        {
            foreach (var iter in _criteriaProgress)
                SendCriteriaProgressRemoved(iter.Key);

            _criteriaProgress.Clear();
        }

        /// <summary>
        /// this function will be called whenever the user might have done a criteria relevant action
        /// </summary>
        /// <param name="type"></param>
        /// <param name="miscValue1"></param>
        /// <param name="miscValue2"></param>
        /// <param name="miscValue3"></param>
        /// <param name="unit"></param>
        /// <param name="referencePlayer"></param>
        public void UpdateCriteria(CriteriaTypes type, ulong miscValue1 = 0, ulong miscValue2 = 0, ulong miscValue3 = 0, Unit unit = null, Player referencePlayer = null)
        {
            if (type >= CriteriaTypes.TotalTypes)
            {
                Log.outDebug(LogFilter.Achievement, "UpdateCriteria: Wrong criteria type {0}", type);
                return;
            }

            if (!referencePlayer)
            {
                Log.outDebug(LogFilter.Achievement, "UpdateCriteria: Player is NULL! Cant update criteria");
                return;
            }

            // disable for gamemasters with GM-mode enabled
            if (referencePlayer.IsGameMaster())
            {
                Log.outDebug(LogFilter.Achievement, "UpdateCriteria: [Player {0} GM mode on] {1}, {2} ({3}), {4}, {5}, {6}", referencePlayer.GetName(), GetOwnerInfo(), type, type, miscValue1, miscValue2, miscValue3);
                return;
            }

            Log.outDebug(LogFilter.Achievement, "UpdateCriteria({0}, {1}, {2}, {3}) {4}", type, type, miscValue1, miscValue2, miscValue3, GetOwnerInfo());

            List<Criteria> criteriaList = GetCriteriaByType(type, (uint)miscValue1);
            foreach (Criteria criteria in criteriaList)
            {
                List<CriteriaTree> trees = Global.CriteriaMgr.GetCriteriaTreesByCriteria(criteria.Id);
                if (!CanUpdateCriteria(criteria, trees, miscValue1, miscValue2, miscValue3, unit, referencePlayer))
                    continue;

                // requirements not found in the dbc
                CriteriaDataSet data = Global.CriteriaMgr.GetCriteriaDataSet(criteria);
                if (data != null)
                    if (!data.Meets(referencePlayer, unit, (uint)miscValue1, (uint)miscValue2))
                        continue;

                switch (type)
                {
                    // std. case: increment at 1
                    case CriteriaTypes.WinBg:
                    case CriteriaTypes.NumberOfTalentResets:
                    case CriteriaTypes.LoseDuel:
                    case CriteriaTypes.CreateAuction:
                    case CriteriaTypes.WonAuctions:    //FIXME: for online player only currently
                    case CriteriaTypes.RollNeed:
                    case CriteriaTypes.RollGreed:
                    case CriteriaTypes.QuestAbandoned:
                    case CriteriaTypes.FlightPathsTaken:
                    case CriteriaTypes.AcceptedSummonings:
                    case CriteriaTypes.LootEpicItem:
                    case CriteriaTypes.ReceiveEpicItem:
                    case CriteriaTypes.Death:
                    case CriteriaTypes.CompleteDailyQuest:
                    case CriteriaTypes.CompleteBattleground:
                    case CriteriaTypes.DeathAtMap:
                    case CriteriaTypes.DeathInDungeon:
                    case CriteriaTypes.KilledByCreature:
                    case CriteriaTypes.KilledByPlayer:
                    case CriteriaTypes.DeathsFrom:
                    case CriteriaTypes.BeSpellTarget:
                    case CriteriaTypes.BeSpellTarget2:
                    case CriteriaTypes.CastSpell:
                    case CriteriaTypes.CastSpell2:
                    case CriteriaTypes.WinRatedArena:
                    case CriteriaTypes.UseItem:
                    case CriteriaTypes.RollNeedOnLoot:
                    case CriteriaTypes.RollGreedOnLoot:
                    case CriteriaTypes.DoEmote:
                    case CriteriaTypes.UseGameobject:
                    case CriteriaTypes.FishInGameobject:
                    case CriteriaTypes.WinDuel:
                    case CriteriaTypes.HkClass:
                    case CriteriaTypes.HkRace:
                    case CriteriaTypes.BgObjectiveCapture:
                    case CriteriaTypes.HonorableKill:
                    case CriteriaTypes.SpecialPvpKill:
                    case CriteriaTypes.GetKillingBlows:
                    case CriteriaTypes.HonorableKillAtArea:
                    case CriteriaTypes.WinArena: // This also behaves like ACHIEVEMENT_CRITERIA_TYPE_WIN_RATED_ARENA
                    case CriteriaTypes.OnLogin:
                    case CriteriaTypes.PlaceGarrisonBuilding:
                    case CriteriaTypes.UpgradeGarrisonBuilding:
                    case CriteriaTypes.OwnBattlePetCount:
                    case CriteriaTypes.HonorLevelReached:
                    case CriteriaTypes.PrestigeReached:
                    case CriteriaTypes.CompleteQuestAccumulate:
                    case CriteriaTypes.BoughtItemFromVendor:
                    case CriteriaTypes.SoldItemToVendor:
                    case CriteriaTypes.TravelledToArea:
                        SetCriteriaProgress(criteria, 1, referencePlayer, ProgressType.Accumulate);
                        break;
                    // std case: increment at miscValue1
                    case CriteriaTypes.MoneyFromVendors:
                    case CriteriaTypes.GoldSpentForTalents:
                    case CriteriaTypes.MoneyFromQuestReward:
                    case CriteriaTypes.GoldSpentForTravelling:
                    case CriteriaTypes.GoldSpentAtBarber:
                    case CriteriaTypes.GoldSpentForMail:
                    case CriteriaTypes.LootMoney:
                    case CriteriaTypes.GoldEarnedByAuctions: //FIXME: for online player only currently
                    case CriteriaTypes.TotalDamageReceived:
                    case CriteriaTypes.TotalHealingReceived:
                    case CriteriaTypes.UseLfdToGroupWithPlayers:
                    case CriteriaTypes.DamageDone:
                    case CriteriaTypes.HealingDone:
                    case CriteriaTypes.HeartOfAzerothArtifactPowerEarned:
                        SetCriteriaProgress(criteria, miscValue1, referencePlayer, ProgressType.Accumulate);
                        break;
                    case CriteriaTypes.KillCreature:
                    case CriteriaTypes.KillCreatureType:
                    case CriteriaTypes.LootType:
                    case CriteriaTypes.OwnItem:
                    case CriteriaTypes.LootItem:
                    case CriteriaTypes.Currency:
                        SetCriteriaProgress(criteria, miscValue2, referencePlayer, ProgressType.Accumulate);
                        break;
                    // std case: high value at miscValue1
                    case CriteriaTypes.HighestAuctionBid:
                    case CriteriaTypes.HighestAuctionSold: //FIXME: for online player only currently
                    case CriteriaTypes.HighestHitDealt:
                    case CriteriaTypes.HighestHitReceived:
                    case CriteriaTypes.HighestHealCasted:
                    case CriteriaTypes.HighestHealingReceived:
                    case CriteriaTypes.HeartOfAzerothLevelReached:
                        SetCriteriaProgress(criteria, miscValue1, referencePlayer, ProgressType.Highest);
                        break;
                    case CriteriaTypes.ReachLevel:
                        SetCriteriaProgress(criteria, referencePlayer.GetLevel(), referencePlayer);
                        break;
                    case CriteriaTypes.ReachSkillLevel:
                        uint skillvalue = referencePlayer.GetBaseSkillValue((SkillType)criteria.Entry.Asset);
                        if (skillvalue != 0)
                            SetCriteriaProgress(criteria, skillvalue, referencePlayer);
                        break;
                    case CriteriaTypes.LearnSkillLevel:
                        uint maxSkillvalue = referencePlayer.GetPureMaxSkillValue((SkillType)criteria.Entry.Asset);
                        if (maxSkillvalue != 0)
                            SetCriteriaProgress(criteria, maxSkillvalue, referencePlayer);
                        break;
                    case CriteriaTypes.CompleteQuestCount:
                        SetCriteriaProgress(criteria, (uint)referencePlayer.GetRewardedQuestCount(), referencePlayer);
                        break;
                    case CriteriaTypes.CompleteDailyQuestDaily:
                        {
                            long nextDailyResetTime = Global.WorldMgr.GetNextDailyQuestsResetTime();
                            CriteriaProgress progress = GetCriteriaProgress(criteria);

                            if (miscValue1 == 0) // Login case.
                            {
                                // reset if player missed one day.
                                if (progress != null && progress.Date < (nextDailyResetTime - 2 * Time.Day))
                                    SetCriteriaProgress(criteria, 0, referencePlayer);
                                continue;
                            }

                            ProgressType progressType;
                            if (progress == null)
                                // 1st time. Start count.
                                progressType = ProgressType.Set;
                            else if (progress.Date < (nextDailyResetTime - 2 * Time.Day))
                                // last progress is older than 2 days. Player missed 1 day => Restart count.
                                progressType = ProgressType.Set;
                            else if (progress.Date < (nextDailyResetTime - Time.Day))
                                // last progress is between 1 and 2 days. => 1st time of the day.
                                progressType = ProgressType.Accumulate;
                            else
                                // last progress is within the day before the reset => Already counted today.
                                continue;

                            SetCriteriaProgress(criteria, 1, referencePlayer, progressType);
                            break;
                        }
                    case CriteriaTypes.CompleteQuestsInZone:
                        {
                            if (miscValue1 != 0)
                            {
                                SetCriteriaProgress(criteria, 1, referencePlayer, ProgressType.Accumulate);
                            }
                            else // login case
                            {
                                uint counter = 0;

                                var rewQuests = referencePlayer.GetRewardedQuests();
                                foreach (var id in rewQuests)
                                {
                                    Quest quest = Global.ObjectMgr.GetQuestTemplate(id);
                                    if (quest != null && quest.QuestSortID >= 0 && quest.QuestSortID == criteria.Entry.Asset)
                                        ++counter;
                                }
                                SetCriteriaProgress(criteria, counter, referencePlayer);
                            }
                            break;
                        }
                    case CriteriaTypes.FallWithoutDying:
                        // miscValue1 is the ingame fallheight*100 as stored in dbc
                        SetCriteriaProgress(criteria, miscValue1, referencePlayer);
                        break;
                    case CriteriaTypes.CompleteQuest:
                    case CriteriaTypes.LearnSpell:
                    case CriteriaTypes.ExploreArea:
                    case CriteriaTypes.VisitBarberShop:
                    case CriteriaTypes.EquipEpicItem:
                    case CriteriaTypes.EquipItem:
                    case CriteriaTypes.CompleteAchievement:
                    case CriteriaTypes.RecruitGarrisonFollower:
                    case CriteriaTypes.OwnBattlePet:
                        SetCriteriaProgress(criteria, 1, referencePlayer);
                        break;
                    case CriteriaTypes.BuyBankSlot:
                        SetCriteriaProgress(criteria, referencePlayer.GetBankBagSlotCount(), referencePlayer);
                        break;
                    case CriteriaTypes.GainReputation:
                        {
                            int reputation = referencePlayer.GetReputationMgr().GetReputation(criteria.Entry.Asset);
                            if (reputation > 0)
                                SetCriteriaProgress(criteria, (uint)reputation, referencePlayer);
                            break;
                        }
                    case CriteriaTypes.GainExaltedReputation:
                        SetCriteriaProgress(criteria, referencePlayer.GetReputationMgr().GetExaltedFactionCount(), referencePlayer);
                        break;
                    case CriteriaTypes.LearnSkilllineSpells:
                    case CriteriaTypes.LearnSkillLine:
                        {
                            uint spellCount = 0;
                            foreach (var spell in referencePlayer.GetSpellMap())
                            {
                                var bounds = Global.SpellMgr.GetSkillLineAbilityMapBounds(spell.Key);
                                foreach (var skill in bounds)
                                {
                                    if (skill.SkillLine == criteria.Entry.Asset)
                                    {
                                        // do not add couter twice if by any chance skill is listed twice in dbc (eg. skill 777 and spell 22717)
                                        ++spellCount;
                                        break;
                                    }
                                }
                            }
                            SetCriteriaProgress(criteria, spellCount, referencePlayer);
                            break;
                        }
                    case CriteriaTypes.GainReveredReputation:
                        SetCriteriaProgress(criteria, referencePlayer.GetReputationMgr().GetReveredFactionCount(), referencePlayer);
                        break;
                    case CriteriaTypes.GainHonoredReputation:
                        SetCriteriaProgress(criteria, referencePlayer.GetReputationMgr().GetHonoredFactionCount(), referencePlayer);
                        break;
                    case CriteriaTypes.KnownFactions:
                        SetCriteriaProgress(criteria, referencePlayer.GetReputationMgr().GetVisibleFactionCount(), referencePlayer);
                        break;
                    case CriteriaTypes.EarnHonorableKill:
                        SetCriteriaProgress(criteria, referencePlayer.m_activePlayerData.LifetimeHonorableKills, referencePlayer);
                        break;
                    case CriteriaTypes.HighestGoldValueOwned:
                        SetCriteriaProgress(criteria, referencePlayer.GetMoney(), referencePlayer, ProgressType.Highest);
                        break;
                    case CriteriaTypes.EarnAchievementPoints:
                        if (miscValue1 == 0)
                            continue;
                        SetCriteriaProgress(criteria, miscValue1, referencePlayer, ProgressType.Accumulate);
                        break;
                    case CriteriaTypes.HighestPersonalRating:
                        {
                            uint reqTeamType = criteria.Entry.Asset;

                            if (miscValue1 != 0)
                            {
                                if (miscValue2 != reqTeamType)
                                    continue;

                                SetCriteriaProgress(criteria, miscValue1, referencePlayer, ProgressType.Highest);
                            }
                            else // login case
                            {

                                for (byte arena_slot = 0; arena_slot < SharedConst.MaxArenaSlot; ++arena_slot)
                                {
                                    uint teamId = referencePlayer.GetArenaTeamId(arena_slot);
                                    if (teamId == 0)
                                        continue;

                                    ArenaTeam team = Global.ArenaTeamMgr.GetArenaTeamById(teamId);
                                    if (team == null || team.GetArenaType() != reqTeamType)
                                        continue;

                                    ArenaTeamMember member = team.GetMember(referencePlayer.GetGUID());
                                    if (member != null)
                                    {
                                        SetCriteriaProgress(criteria, member.PersonalRating, referencePlayer, ProgressType.Highest);
                                        break;
                                    }
                                }
                            }
                            break;
                        }
                    case CriteriaTypes.ReachGuildLevel:
                        SetCriteriaProgress(criteria, miscValue1, referencePlayer);
                        break;
                    case CriteriaTypes.TransmogSetUnlocked:
                        if (miscValue1 != criteria.Entry.Asset)
                            continue;
                        SetCriteriaProgress(criteria, 1, referencePlayer, ProgressType.Accumulate);
                        break;
                    case CriteriaTypes.AppearanceUnlockedBySlot:
                        if (miscValue2 == 0 /*login case*/ || miscValue1 != criteria.Entry.Asset)
                            continue;
                        SetCriteriaProgress(criteria, 1, referencePlayer, ProgressType.Accumulate);
                        break;
                    // FIXME: not triggered in code as result, need to implement
                    case CriteriaTypes.CompleteRaid:
                    case CriteriaTypes.PlayArena:
                    case CriteriaTypes.HighestTeamRating:
                    case CriteriaTypes.OwnRank:
                    case CriteriaTypes.SpentGoldGuildRepairs:
                    case CriteriaTypes.CraftItemsGuild:
                    case CriteriaTypes.CatchFromPool:
                    case CriteriaTypes.BuyGuildBankSlots:
                    case CriteriaTypes.EarnGuildAchievementPoints:
                    case CriteriaTypes.WinRatedBattleground:
                    case CriteriaTypes.ReachBgRating:
                    case CriteriaTypes.BuyGuildTabard:
                    case CriteriaTypes.CompleteQuestsGuild:
                    case CriteriaTypes.HonorableKillsGuild:
                    case CriteriaTypes.KillCreatureTypeGuild:
                    case CriteriaTypes.CompleteArchaeologyProjects:
                    case CriteriaTypes.CompleteGuildChallengeType:
                    case CriteriaTypes.CompleteGuildChallenge:
                    case CriteriaTypes.LfrDungeonsCompleted:
                    case CriteriaTypes.LfrLeaves:
                    case CriteriaTypes.LfrVoteKicksInitiatedByPlayer:
                    case CriteriaTypes.LfrVoteKicksNotInitByPlayer:
                    case CriteriaTypes.BeKickedFromLfr:
                    case CriteriaTypes.CountOfLfrQueueBoostsByTank:
                    case CriteriaTypes.CompleteScenarioCount:
                    case CriteriaTypes.CompleteScenario:
                    case CriteriaTypes.CaptureBattlePet:
                    case CriteriaTypes.WinPetBattle:
                    case CriteriaTypes.LevelBattlePet:
                    case CriteriaTypes.CaptureBattlePetCredit:
                    case CriteriaTypes.LevelBattlePetCredit:
                    case CriteriaTypes.EnterArea:
                    case CriteriaTypes.LeaveArea:
                    case CriteriaTypes.CompleteDungeonEncounter:
                    case CriteriaTypes.ConstructGarrisonBuilding:
                    case CriteriaTypes.UpgradeGarrison:
                    case CriteriaTypes.StartGarrisonMission:
                    case CriteriaTypes.CompleteGarrisonMissionCount:
                    case CriteriaTypes.CompleteGarrisonMission:
                    case CriteriaTypes.RecruitGarrisonFollowerCount:
                    case CriteriaTypes.LearnGarrisonBlueprintCount:
                    case CriteriaTypes.CompleteGarrisonShipment:
                    case CriteriaTypes.RaiseGarrisonFollowerItemLevel:
                    case CriteriaTypes.RaiseGarrisonFollowerLevel:
                    case CriteriaTypes.OwnToy:
                    case CriteriaTypes.OwnToyCount:
                    case CriteriaTypes.OwnHeirlooms:
                    case CriteriaTypes.SurveyGameobject:
                    case CriteriaTypes.ClearDigsite:
                    case CriteriaTypes.ManualCompleteCriteria:
                    case CriteriaTypes.CompleteChallengeModeGuild:
                    case CriteriaTypes.DefeatCreatureGroup:
                    case CriteriaTypes.CompleteChallengeMode:
                    case CriteriaTypes.SendEvent:
                    case CriteriaTypes.CookRecipesGuild:
                    case CriteriaTypes.EarnPetBattleAchievementPoints:
                    case CriteriaTypes.SendEventScenario:
                    case CriteriaTypes.ReleaseSpirit:
                    case CriteriaTypes.OwnPet:
                    case CriteriaTypes.GarrisonCompleteDungeonEncounter:
                    case CriteriaTypes.CompleteLfgDungeon:
                    case CriteriaTypes.LfgVoteKicksInitiatedByPlayer:
                    case CriteriaTypes.LfgVoteKicksNotInitByPlayer:
                    case CriteriaTypes.BeKickedFromLfg:
                    case CriteriaTypes.LfgLeaves:
                    case CriteriaTypes.CountOfLfgQueueBoostsByTank:
                    case CriteriaTypes.ReachAreatriggerWithActionset:
                    case CriteriaTypes.StartOrderHallMission:
                    case CriteriaTypes.RecruitGarrisonFollowerWithQuality:
                    case CriteriaTypes.ArtifactPowerEarned:
                    case CriteriaTypes.ArtifactTraitsUnlocked:
                    case CriteriaTypes.OrderHallTalentLearned:
                    case CriteriaTypes.OrderHallRecruitTroop:
                    case CriteriaTypes.CompleteWorldQuest:
                    case CriteriaTypes.GainParagonReputation:
                    case CriteriaTypes.EarnHonorXp:
                    case CriteriaTypes.RelicTalentUnlocked:
                    case CriteriaTypes.ReachAccountHonorLevel:
                    case CriteriaTypes.MythicKeystoneCompleted:
                    case CriteriaTypes.ApplyConduit:
                    case CriteriaTypes.ConvertItemsToCurrency:
                        break;                                   // Not implemented yet :(
                }

                foreach (CriteriaTree tree in trees)
                {
                    if (IsCompletedCriteriaTree(tree))
                        CompletedCriteriaTree(tree, referencePlayer);

                    AfterCriteriaTreeUpdate(tree, referencePlayer);
                }
            }
        }

        public void UpdateTimedCriteria(uint timeDiff)
        {
            if (!_timeCriteriaTrees.Empty())
            {
                foreach (var key in _timeCriteriaTrees.Keys.ToList())
                {
                    var value = _timeCriteriaTrees[key];
                    // Time is up, remove timer and reset progress
                    if (value <= timeDiff)
                    {
                        CriteriaTree criteriaTree = Global.CriteriaMgr.GetCriteriaTree(key);
                        if (criteriaTree.Criteria != null)
                            RemoveCriteriaProgress(criteriaTree.Criteria);

                        _timeCriteriaTrees.Remove(key);
                    }
                    else
                    {
                        _timeCriteriaTrees[key] -= timeDiff;
                    }
                }
            }
        }

        public void StartCriteriaTimer(CriteriaStartEvent startEvent, uint entry, uint timeLost = 0)
        {
            List<Criteria> criteriaList = Global.CriteriaMgr.GetTimedCriteriaByType(startEvent);
            foreach (Criteria criteria in criteriaList)
            {
                if (criteria.Entry.StartAsset != entry)
                    continue;

                List<CriteriaTree> trees = Global.CriteriaMgr.GetCriteriaTreesByCriteria(criteria.Id);
                bool canStart = false;
                foreach (CriteriaTree tree in trees)
                {
                    if (!_timeCriteriaTrees.ContainsKey(tree.Id) && !IsCompletedCriteriaTree(tree))
                    {
                        // Start the timer
                        if (criteria.Entry.StartTimer * Time.InMilliseconds > timeLost)
                        {
                            _timeCriteriaTrees[tree.Id] = (uint)(criteria.Entry.StartTimer * Time.InMilliseconds - timeLost);
                            canStart = true;
                        }
                    }
                }

                if (!canStart)
                    continue;

                // and at client too
                SetCriteriaProgress(criteria, 0, null, ProgressType.Set);
            }
        }

        public void RemoveCriteriaTimer(CriteriaStartEvent startEvent, uint entry)
        {
            List<Criteria> criteriaList = Global.CriteriaMgr.GetTimedCriteriaByType(startEvent);
            foreach (Criteria criteria in criteriaList)
            {
                if (criteria.Entry.StartAsset != entry)
                    continue;

                List<CriteriaTree> trees = Global.CriteriaMgr.GetCriteriaTreesByCriteria(criteria.Id);
                // Remove the timer from all trees
                foreach (CriteriaTree tree in trees)
                    _timeCriteriaTrees.Remove(tree.Id);

                // remove progress
                RemoveCriteriaProgress(criteria);
            }
        }

        public CriteriaProgress GetCriteriaProgress(Criteria entry)
        {
            return _criteriaProgress.LookupByKey(entry.Id);
        }

        public void SetCriteriaProgress(Criteria criteria, ulong changeValue, Player referencePlayer, ProgressType progressType = ProgressType.Set)
        {
            // Don't allow to cheat - doing timed criteria without timer active
            List<CriteriaTree> trees = null;
            if (criteria.Entry.StartTimer != 0)
            {
                trees = Global.CriteriaMgr.GetCriteriaTreesByCriteria(criteria.Id);
                if (trees.Empty())
                    return;

                bool hasTreeForTimed = false;
                foreach (CriteriaTree tree in trees)
                {
                    var timedIter = _timeCriteriaTrees.LookupByKey(tree.Id);
                    if (timedIter != 0)
                    {
                        hasTreeForTimed = true;
                        break;
                    }
                }

                if (!hasTreeForTimed)
                    return;
            }

            Log.outDebug(LogFilter.Achievement, "SetCriteriaProgress({0}, {1}) for {2}", criteria.Id, changeValue, GetOwnerInfo());

            CriteriaProgress progress = GetCriteriaProgress(criteria);
            if (progress == null)
            {
                // not create record for 0 counter but allow it for timed criteria
                // we will need to send 0 progress to client to start the timer
                if (changeValue == 0 && criteria.Entry.StartTimer == 0)
                    return;

                progress = new CriteriaProgress();
                progress.Counter = changeValue;

            }
            else
            {
                ulong newValue = 0;
                switch (progressType)
                {
                    case ProgressType.Set:
                        newValue = changeValue;
                        break;
                    case ProgressType.Accumulate:
                        {
                            // avoid overflow
                            ulong max_value = ulong.MaxValue;
                            newValue = max_value - progress.Counter > changeValue ? progress.Counter + changeValue : max_value;
                            break;
                        }
                    case ProgressType.Highest:
                        newValue = progress.Counter < changeValue ? changeValue : progress.Counter;
                        break;
                }

                // not update (not mark as changed) if counter will have same value
                if (progress.Counter == newValue && criteria.Entry.StartTimer == 0)
                    return;

                progress.Counter = newValue;
            }

            progress.Changed = true;
            progress.Date = GameTime.GetGameTime(); // set the date to the latest update.
            progress.PlayerGUID = referencePlayer ? referencePlayer.GetGUID() : ObjectGuid.Empty;
            _criteriaProgress[criteria.Id] = progress;

            TimeSpan timeElapsed = TimeSpan.Zero;
            if (criteria.Entry.StartTimer != 0)
            {
                Cypher.Assert(trees != null);

                foreach (CriteriaTree tree in trees)
                {
                    var timed = _timeCriteriaTrees.LookupByKey(tree.Id);
                    if (timed != 0)
                    {
                        // Client expects this in packet
                        timeElapsed = TimeSpan.FromSeconds(criteria.Entry.StartTimer - (timed / Time.InMilliseconds));

                        // Remove the timer, we wont need it anymore
                        if (IsCompletedCriteriaTree(tree))
                            _timeCriteriaTrees.Remove(tree.Id);
                    }
                }
            }

            SendCriteriaUpdate(criteria, progress, timeElapsed, true);
        }

        public void RemoveCriteriaProgress(Criteria criteria)
        {
            if (criteria == null)
                return;

            if (!_criteriaProgress.ContainsKey(criteria.Id))
                return;

            SendCriteriaProgressRemoved(criteria.Id);

            _criteriaProgress.Remove(criteria.Id);
        }

        public bool IsCompletedCriteriaTree(CriteriaTree tree)
        {
            if (!CanCompleteCriteriaTree(tree))
                return false;

            ulong requiredCount = tree.Entry.Amount;
            switch ((CriteriaTreeOperator)tree.Entry.Operator)
            {
                case CriteriaTreeOperator.Complete:
                    return tree.Criteria != null && IsCompletedCriteria(tree.Criteria, requiredCount);
                case CriteriaTreeOperator.NotComplete:
                    return tree.Criteria == null || !IsCompletedCriteria(tree.Criteria, requiredCount);
                case CriteriaTreeOperator.CompleteAll:
                    foreach (CriteriaTree node in tree.Children)
                        if (!IsCompletedCriteriaTree(node))
                            return false;
                    return true;
                case CriteriaTreeOperator.Sum:
                    {
                        ulong progress = 0;
                        CriteriaManager.WalkCriteriaTree(tree, criteriaTree =>
                        {
                            if (criteriaTree.Criteria != null)
                            {
                                CriteriaProgress criteriaProgress = GetCriteriaProgress(criteriaTree.Criteria);
                                if (criteriaProgress != null)
                                    progress += criteriaProgress.Counter;
                            }
                        });
                        return progress >= requiredCount;
                    }
                case CriteriaTreeOperator.Highest:
                    {
                        ulong progress = 0;
                        CriteriaManager.WalkCriteriaTree(tree, criteriaTree =>
                        {
                            if (criteriaTree.Criteria != null)
                            {
                                CriteriaProgress criteriaProgress = GetCriteriaProgress(criteriaTree.Criteria);
                                if (criteriaProgress != null)
                                    if (criteriaProgress.Counter > progress)
                                        progress = criteriaProgress.Counter;
                            }
                        });
                        return progress >= requiredCount;
                    }
                case CriteriaTreeOperator.StartedAtLeast:
                    {
                        ulong progress = 0;
                        foreach (CriteriaTree node in tree.Children)
                        {
                            if (node.Criteria != null)
                            {
                                CriteriaProgress criteriaProgress = GetCriteriaProgress(node.Criteria);
                                if (criteriaProgress != null)
                                    if (criteriaProgress.Counter >= 1)
                                        if (++progress >= requiredCount)
                                            return true;
                            }
                        }

                        return false;
                    }
                case CriteriaTreeOperator.CompleteAtLeast:
                    {
                        ulong progress = 0;
                        foreach (CriteriaTree node in tree.Children)
                            if (IsCompletedCriteriaTree(node))
                                if (++progress >= requiredCount)
                                    return true;

                        return false;
                    }
                case CriteriaTreeOperator.ProgressBar:
                    {
                        ulong progress = 0;
                        CriteriaManager.WalkCriteriaTree(tree, criteriaTree =>
                        {
                            if (criteriaTree.Criteria != null)
                            {
                                CriteriaProgress criteriaProgress = GetCriteriaProgress(criteriaTree.Criteria);
                                if (criteriaProgress != null)
                                    progress += criteriaProgress.Counter * criteriaTree.Entry.Amount;
                            }
                        });
                        return progress >= requiredCount;
                    }
                default:
                    break;
            }

            return false;
        }

        public virtual bool CanUpdateCriteriaTree(Criteria criteria, CriteriaTree tree, Player referencePlayer)
        {
            if ((tree.Entry.Flags.HasAnyFlag(CriteriaTreeFlags.HordeOnly) && referencePlayer.GetTeam() != Team.Horde) ||
                (tree.Entry.Flags.HasAnyFlag(CriteriaTreeFlags.AllianceOnly) && referencePlayer.GetTeam() != Team.Alliance))
            {
                Log.outTrace(LogFilter.Achievement, "CriteriaHandler.CanUpdateCriteriaTree: (Id: {0} Type {1} CriteriaTree {2}) Wrong faction",
                    criteria.Id, criteria.Entry.Type, tree.Entry.Id);
                return false;
            }

            return true;
        }

        public virtual bool CanCompleteCriteriaTree(CriteriaTree tree)
        {
            return true;
        }

        bool IsCompletedCriteria(Criteria criteria, ulong requiredAmount)
        {
            CriteriaProgress progress = GetCriteriaProgress(criteria);
            if (progress == null)
                return false;

            switch (criteria.Entry.Type)
            {
                case CriteriaTypes.WinBg:
                case CriteriaTypes.KillCreature:
                case CriteriaTypes.ReachLevel:
                case CriteriaTypes.ReachGuildLevel:
                case CriteriaTypes.ReachSkillLevel:
                case CriteriaTypes.CompleteQuestCount:
                case CriteriaTypes.CompleteDailyQuestDaily:
                case CriteriaTypes.CompleteQuestsInZone:
                case CriteriaTypes.DamageDone:
                case CriteriaTypes.HealingDone:
                case CriteriaTypes.CompleteDailyQuest:
                case CriteriaTypes.FallWithoutDying:
                case CriteriaTypes.BeSpellTarget:
                case CriteriaTypes.BeSpellTarget2:
                case CriteriaTypes.CastSpell:
                case CriteriaTypes.CastSpell2:
                case CriteriaTypes.BgObjectiveCapture:
                case CriteriaTypes.HonorableKillAtArea:
                case CriteriaTypes.HonorableKill:
                case CriteriaTypes.EarnHonorableKill:
                case CriteriaTypes.OwnItem:
                case CriteriaTypes.WinRatedArena:
                case CriteriaTypes.HighestPersonalRating:
                case CriteriaTypes.UseItem:
                case CriteriaTypes.LootItem:
                case CriteriaTypes.BuyBankSlot:
                case CriteriaTypes.GainReputation:
                case CriteriaTypes.GainExaltedReputation:
                case CriteriaTypes.VisitBarberShop:
                case CriteriaTypes.EquipEpicItem:
                case CriteriaTypes.RollNeedOnLoot:
                case CriteriaTypes.RollGreedOnLoot:
                case CriteriaTypes.HkClass:
                case CriteriaTypes.HkRace:
                case CriteriaTypes.DoEmote:
                case CriteriaTypes.EquipItem:
                case CriteriaTypes.MoneyFromQuestReward:
                case CriteriaTypes.LootMoney:
                case CriteriaTypes.UseGameobject:
                case CriteriaTypes.SpecialPvpKill:
                case CriteriaTypes.FishInGameobject:
                case CriteriaTypes.LearnSkilllineSpells:
                case CriteriaTypes.LearnSkillLine:
                case CriteriaTypes.WinDuel:
                case CriteriaTypes.LootType:
                case CriteriaTypes.UseLfdToGroupWithPlayers:
                case CriteriaTypes.GetKillingBlows:
                case CriteriaTypes.Currency:
                case CriteriaTypes.PlaceGarrisonBuilding:
                case CriteriaTypes.OwnBattlePetCount:
                case CriteriaTypes.AppearanceUnlockedBySlot:
                case CriteriaTypes.GainParagonReputation:
                case CriteriaTypes.EarnHonorXp:
                case CriteriaTypes.RelicTalentUnlocked:
                case CriteriaTypes.ReachAccountHonorLevel:
                case CriteriaTypes.HeartOfAzerothArtifactPowerEarned:
                case CriteriaTypes.HeartOfAzerothLevelReached:
                case CriteriaTypes.CompleteQuestAccumulate:
                case CriteriaTypes.BoughtItemFromVendor:
                case CriteriaTypes.SoldItemToVendor:
                case CriteriaTypes.TravelledToArea:
                    return progress.Counter >= requiredAmount;
                case CriteriaTypes.CompleteAchievement:
                case CriteriaTypes.CompleteQuest:
                case CriteriaTypes.LearnSpell:
                case CriteriaTypes.ExploreArea:
                case CriteriaTypes.RecruitGarrisonFollower:
                case CriteriaTypes.OwnBattlePet:
                case CriteriaTypes.HonorLevelReached:
                case CriteriaTypes.PrestigeReached:
                case CriteriaTypes.TransmogSetUnlocked:
                    return progress.Counter >= 1;
                case CriteriaTypes.LearnSkillLevel:
                    return progress.Counter >= (requiredAmount * 75);
                case CriteriaTypes.EarnAchievementPoints:
                    return progress.Counter >= 9000;
                case CriteriaTypes.WinArena:
                    return requiredAmount != 0 && progress.Counter >= requiredAmount;
                case CriteriaTypes.OnLogin:
                    return true;
                // handle all statistic-only criteria here
                default:
                    break;
            }

            return false;
        }

        bool CanUpdateCriteria(Criteria criteria, List<CriteriaTree> trees, ulong miscValue1, ulong miscValue2, ulong miscValue3, Unit unit, Player referencePlayer)
        {
            if (Global.DisableMgr.IsDisabledFor(DisableType.Criteria, criteria.Id, null))
            {
                Log.outError(LogFilter.Achievement, "CanUpdateCriteria: (Id: {0} Type {1}) Disabled", criteria.Id, criteria.Entry.Type);
                return false;
            }

            bool treeRequirementPassed = false;
            foreach (CriteriaTree tree in trees)
            {
                if (!CanUpdateCriteriaTree(criteria, tree, referencePlayer))
                    continue;

                treeRequirementPassed = true;
                break;
            }

            if (!treeRequirementPassed)
                return false;

            if (!RequirementsSatisfied(criteria, miscValue1, miscValue2, miscValue3, unit, referencePlayer))
            {
                Log.outTrace(LogFilter.Achievement, "CanUpdateCriteria: (Id: {0} Type {1}) Requirements not satisfied", criteria.Id, criteria.Entry.Type);
                return false;
            }

            if (criteria.Modifier != null && !ModifierTreeSatisfied(criteria.Modifier, miscValue1, miscValue2, unit, referencePlayer))
            {
                Log.outTrace(LogFilter.Achievement, "CanUpdateCriteria: (Id: {0} Type {1}) Requirements have not been satisfied", criteria.Id, criteria.Entry.Type);
                return false;
            }

            if (!ConditionsSatisfied(criteria, referencePlayer))
            {
                Log.outTrace(LogFilter.Achievement, "CanUpdateCriteria: (Id: {0} Type {1}) Conditions have not been satisfied", criteria.Id, criteria.Entry.Type);
                return false;
            }

            return true;
        }

        bool ConditionsSatisfied(Criteria criteria, Player referencePlayer)
        {
            if (criteria.Entry.FailEvent == 0)
                return true;

            switch ((CriteriaFailEvent)criteria.Entry.FailEvent)
            {
                case CriteriaFailEvent.LeaveBattleground:
                    if (!referencePlayer.InBattleground())
                        return false;
                    break;
                case CriteriaFailEvent.ModifyPartyStatus:
                    if (referencePlayer.GetGroup())
                        return false;
                    break;
                default:
                    break;
            }

            return true;
        }

        bool RequirementsSatisfied(Criteria criteria, ulong miscValue1, ulong miscValue2, ulong miscValue3, Unit unit, Player referencePlayer)
        {
            switch (criteria.Entry.Type)
            {
                case CriteriaTypes.AcceptedSummonings:
                case CriteriaTypes.CompleteDailyQuest:
                case CriteriaTypes.CreateAuction:
                case CriteriaTypes.FallWithoutDying:
                case CriteriaTypes.FlightPathsTaken:
                case CriteriaTypes.GetKillingBlows:
                case CriteriaTypes.GoldEarnedByAuctions:
                case CriteriaTypes.GoldSpentAtBarber:
                case CriteriaTypes.GoldSpentForMail:
                case CriteriaTypes.GoldSpentForTalents:
                case CriteriaTypes.GoldSpentForTravelling:
                case CriteriaTypes.HighestAuctionBid:
                case CriteriaTypes.HighestAuctionSold:
                case CriteriaTypes.HighestHealingReceived:
                case CriteriaTypes.HighestHealCasted:
                case CriteriaTypes.HighestHitDealt:
                case CriteriaTypes.HighestHitReceived:
                case CriteriaTypes.HonorableKill:
                case CriteriaTypes.LootMoney:
                case CriteriaTypes.LoseDuel:
                case CriteriaTypes.MoneyFromQuestReward:
                case CriteriaTypes.MoneyFromVendors:
                case CriteriaTypes.NumberOfTalentResets:
                case CriteriaTypes.QuestAbandoned:
                case CriteriaTypes.ReachGuildLevel:
                case CriteriaTypes.RollGreed:
                case CriteriaTypes.RollNeed:
                case CriteriaTypes.SpecialPvpKill:
                case CriteriaTypes.TotalDamageReceived:
                case CriteriaTypes.TotalHealingReceived:
                case CriteriaTypes.UseLfdToGroupWithPlayers:
                case CriteriaTypes.VisitBarberShop:
                case CriteriaTypes.WinDuel:
                case CriteriaTypes.WinRatedArena:
                case CriteriaTypes.WonAuctions:
                case CriteriaTypes.CompleteQuestAccumulate:
                case CriteriaTypes.BoughtItemFromVendor:
                case CriteriaTypes.SoldItemToVendor:
                    if (miscValue1 == 0)
                        return false;
                    break;
                case CriteriaTypes.BuyBankSlot:
                case CriteriaTypes.CompleteDailyQuestDaily:
                case CriteriaTypes.CompleteQuestCount:
                case CriteriaTypes.EarnAchievementPoints:
                case CriteriaTypes.GainExaltedReputation:
                case CriteriaTypes.GainHonoredReputation:
                case CriteriaTypes.GainReveredReputation:
                case CriteriaTypes.HighestGoldValueOwned:
                case CriteriaTypes.HighestPersonalRating:
                case CriteriaTypes.KnownFactions:
                case CriteriaTypes.ReachLevel:
                case CriteriaTypes.OnLogin:
                    break;
                case CriteriaTypes.CompleteAchievement:
                    if (!RequiredAchievementSatisfied(criteria.Entry.Asset))
                        return false;
                    break;
                case CriteriaTypes.WinBg:
                case CriteriaTypes.CompleteBattleground:
                case CriteriaTypes.DeathAtMap:
                    if (miscValue1 == 0 || criteria.Entry.Asset != referencePlayer.GetMapId())
                        return false;
                    break;
                case CriteriaTypes.KillCreature:
                case CriteriaTypes.KilledByCreature:
                    if (miscValue1 == 0 || criteria.Entry.Asset != miscValue1)
                        return false;
                    break;
                case CriteriaTypes.ReachSkillLevel:
                case CriteriaTypes.LearnSkillLevel:
                    // update at loading or specific skill update
                    if (miscValue1 != 0 && miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.CompleteQuestsInZone:
                    if (miscValue1 != 0)
                    {
                        Quest quest = Global.ObjectMgr.GetQuestTemplate((uint)miscValue1);
                        if (quest == null || quest.QuestSortID != criteria.Entry.Asset)
                            return false;
                    }
                    break;
                case CriteriaTypes.Death:
                    {
                        if (miscValue1 == 0)
                            return false;
                        break;
                    }
                case CriteriaTypes.DeathInDungeon:
                    {
                        if (miscValue1 == 0)
                            return false;

                        Map map = referencePlayer.IsInWorld ? referencePlayer.GetMap() : Global.MapMgr.FindMap(referencePlayer.GetMapId(), referencePlayer.GetInstanceId());
                        if (!map || !map.IsDungeon())
                            return false;

                        //FIXME: work only for instances where max == min for players
                        if (map.ToInstanceMap().GetMaxPlayers() != criteria.Entry.Asset)
                            return false;
                        break;
                    }
                case CriteriaTypes.KilledByPlayer:
                    if (miscValue1 == 0 || !unit || !unit.IsTypeId(TypeId.Player))
                        return false;
                    break;
                case CriteriaTypes.DeathsFrom:
                    if (miscValue1 == 0 || miscValue2 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.CompleteQuest:
                    {
                        // if miscValues != 0, it contains the questID.
                        if (miscValue1 != 0)
                        {
                            if (miscValue1 != criteria.Entry.Asset)
                                return false;
                        }
                        else
                        {
                            // login case.
                            if (!referencePlayer.GetQuestRewardStatus(criteria.Entry.Asset))
                                return false;
                        }
                        CriteriaDataSet data = Global.CriteriaMgr.GetCriteriaDataSet(criteria);
                        if (data != null)
                            if (!data.Meets(referencePlayer, unit))
                                return false;
                        break;
                    }
                case CriteriaTypes.BeSpellTarget:
                case CriteriaTypes.BeSpellTarget2:
                case CriteriaTypes.CastSpell:
                case CriteriaTypes.CastSpell2:
                    if (miscValue1 == 0 || miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.LearnSpell:
                    if (miscValue1 != 0 && miscValue1 != criteria.Entry.Asset)
                        return false;

                    if (!referencePlayer.HasSpell(criteria.Entry.Asset))
                        return false;
                    break;
                case CriteriaTypes.LootType:
                    // miscValue1 = itemId - miscValue2 = count of item loot
                    // miscValue3 = loot_type (note: 0 = LOOT_CORPSE and then it ignored)
                    if (miscValue1 == 0 || miscValue2 == 0 || miscValue3 == 0 || miscValue3 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.OwnItem:
                    if (miscValue1 != 0 && criteria.Entry.Asset != miscValue1)
                        return false;
                    break;
                case CriteriaTypes.UseItem:
                case CriteriaTypes.LootItem:
                case CriteriaTypes.EquipItem:
                    if (miscValue1 == 0 || criteria.Entry.Asset != miscValue1)
                        return false;
                    break;
                case CriteriaTypes.ExploreArea:
                    {
                        WorldMapOverlayRecord worldOverlayEntry = CliDB.WorldMapOverlayStorage.LookupByKey(criteria.Entry.Asset);
                        if (worldOverlayEntry == null)
                            break;

                        bool matchFound = false;
                        for (int j = 0; j < SharedConst.MaxWorldMapOverlayArea; ++j)
                        {
                            AreaTableRecord area = CliDB.AreaTableStorage.LookupByKey(worldOverlayEntry.AreaID[j]);
                            if (area == null)
                                break;

                            if (area.AreaBit < 0)
                                continue;

                            int playerIndexOffset = (int)((uint)area.AreaBit / 64);
                            if (playerIndexOffset >= PlayerConst.ExploredZonesSize)
                                continue;

                            ulong mask = 1ul << (int)((uint)area.AreaBit % 64);
                            if (Convert.ToBoolean(referencePlayer.m_activePlayerData.ExploredZones[playerIndexOffset] & mask))
                            {
                                matchFound = true;
                                break;
                            }
                        }

                        if (!matchFound)
                            return false;
                        break;
                    }
                case CriteriaTypes.GainReputation:
                    if (miscValue1 != 0 && miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.EquipEpicItem:
                    // miscValue1 = itemSlot miscValue2 = itemid
                    if (miscValue2 == 0 || miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.RollNeedOnLoot:
                case CriteriaTypes.RollGreedOnLoot:
                    {
                        // miscValue1 = itemid miscValue2 = diced value
                        if (miscValue1 == 0 || miscValue2 != criteria.Entry.Asset)
                            return false;

                        ItemTemplate proto = Global.ObjectMgr.GetItemTemplate((uint)miscValue1);
                        if (proto == null)
                            return false;
                        break;
                    }
                case CriteriaTypes.DoEmote:
                    if (miscValue1 == 0 || miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.DamageDone:
                case CriteriaTypes.HealingDone:
                    if (miscValue1 == 0)
                        return false;

                    if ((CriteriaFailEvent)criteria.Entry.FailEvent == CriteriaFailEvent.LeaveBattleground)
                    {
                        if (!referencePlayer.InBattleground())
                            return false;

                        // map specific case (BG in fact) expected player targeted damage/heal
                        if (!unit || !unit.IsTypeId(TypeId.Player))
                            return false;
                    }
                    break;
                case CriteriaTypes.UseGameobject:
                case CriteriaTypes.FishInGameobject:
                    if (miscValue1 == 0 || miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.LearnSkilllineSpells:
                case CriteriaTypes.LearnSkillLine:
                    if (miscValue1 != 0 && miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.LootEpicItem:
                case CriteriaTypes.ReceiveEpicItem:
                    {
                        if (miscValue1 == 0)
                            return false;
                        ItemTemplate proto = Global.ObjectMgr.GetItemTemplate((uint)miscValue1);
                        if (proto == null || proto.GetQuality() < ItemQuality.Epic)
                            return false;
                        break;
                    }
                case CriteriaTypes.HkClass:
                    if (miscValue1 == 0 || miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.HkRace:
                    if (miscValue1 == 0 || miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.BgObjectiveCapture:
                    if (miscValue1 == 0 || miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.HonorableKillAtArea:
                    if (miscValue1 == 0 || miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.Currency:
                    if (miscValue1 == 0 || miscValue2 == 0 || (long)miscValue2 < 0
                        || miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.WinArena:
                    if (miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.HighestTeamRating:
                    return false;
                case CriteriaTypes.PlaceGarrisonBuilding:
                    if (miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                case CriteriaTypes.TravelledToArea:
                    if (miscValue1 != criteria.Entry.Asset)
                        return false;
                    break;
                default:
                    break;
            }
            return true;
        }

        public bool ModifierTreeSatisfied(ModifierTreeNode tree, ulong miscValue1, ulong miscValue2, Unit unit, Player referencePlayer)
        {
            switch ((ModifierTreeOperator)tree.Entry.Operator)
            {
                case ModifierTreeOperator.SingleTrue:
                    return tree.Entry.Type != 0 && ModifierSatisfied(tree.Entry, miscValue1, miscValue2, unit, referencePlayer);
                case ModifierTreeOperator.SingleFalse:
                    return tree.Entry.Type != 0 && !ModifierSatisfied(tree.Entry, miscValue1, miscValue2, unit, referencePlayer);
                case ModifierTreeOperator.All:
                    foreach (ModifierTreeNode node in tree.Children)
                        if (!ModifierTreeSatisfied(node, miscValue1, miscValue2, unit, referencePlayer))
                            return false;
                    return true;
                case ModifierTreeOperator.Some:
                    {
                        sbyte requiredAmount = Math.Max(tree.Entry.Amount, (sbyte)1);
                        foreach (ModifierTreeNode node in tree.Children)
                            if (ModifierTreeSatisfied(node, miscValue1, miscValue2, unit, referencePlayer))
                                if (--requiredAmount == 0)
                                    return true;

                        return false;
                    }
                default:
                    break;
            }

            return false;
        }

        bool ModifierSatisfied(ModifierTreeRecord modifier, ulong miscValue1, ulong miscValue2, Unit unit, Player referencePlayer)
        {
            uint reqValue = modifier.Asset;
            int secondaryAsset = modifier.SecondaryAsset;
            int tertiaryAsset = modifier.TertiaryAsset;

            switch ((ModifierTreeType)modifier.Type)
            {
                case ModifierTreeType.PlayerInebriationLevelEqualOrGreaterThan: // 1
                    {
                        uint inebriation = (uint)Math.Min(Math.Max(referencePlayer.GetDrunkValue(), referencePlayer.m_playerData.FakeInebriation), 100);
                        if (inebriation < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerMeetsCondition: // 2
                    {
                        PlayerConditionRecord playerCondition = CliDB.PlayerConditionStorage.LookupByKey(reqValue);
                        if (playerCondition == null || !ConditionManager.IsPlayerMeetingCondition(referencePlayer, playerCondition))
                            return false;
                        break;
                    }
                case ModifierTreeType.MinimumItemLevel: // 3
                    {
                        // miscValue1 is itemid
                        ItemTemplate item = Global.ObjectMgr.GetItemTemplate((uint)miscValue1);
                        if (item == null || item.GetBaseItemLevel() < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.TargetCreatureId: // 4
                    if (unit == null || unit.GetEntry() != reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetIsPlayer: // 5
                    if (unit == null || !unit.IsTypeId(TypeId.Player))
                        return false;
                    break;
                case ModifierTreeType.TargetIsDead: // 6
                    if (unit == null || unit.IsAlive())
                        return false;
                    break;
                case ModifierTreeType.TargetIsOppositeFaction: // 7
                    if (unit == null || !referencePlayer.IsHostileTo(unit))
                        return false;
                    break;
                case ModifierTreeType.PlayerHasAura: // 8
                    if (!referencePlayer.HasAura(reqValue))
                        return false;
                    break;
                case ModifierTreeType.PlayerHasAuraEffect: // 9
                    if (!referencePlayer.HasAuraType((AuraType)reqValue))
                        return false;
                    break;
                case ModifierTreeType.TargetHasAura: // 10
                    if (unit == null || !unit.HasAura(reqValue))
                        return false;
                    break;
                case ModifierTreeType.TargetHasAuraEffect: // 11
                    if (unit == null || !unit.HasAuraType((AuraType)reqValue))
                        return false;
                    break;
                case ModifierTreeType.TargetHasAuraState: // 12
                    if (unit == null || !unit.HasAuraState((AuraStateType)reqValue))
                        return false;
                    break;
                case ModifierTreeType.PlayerHasAuraState: // 13
                    if (!referencePlayer.HasAuraState((AuraStateType)reqValue))
                        return false;
                    break;
                case ModifierTreeType.ItemQualityIsAtLeast: // 14
                    {
                        // miscValue1 is itemid
                        ItemTemplate item = Global.ObjectMgr.GetItemTemplate((uint)miscValue1);
                        if (item == null || (uint)item.GetQuality() < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.ItemQualityIsExactly: // 15
                    {
                        // miscValue1 is itemid
                        ItemTemplate item = Global.ObjectMgr.GetItemTemplate((uint)miscValue1);
                        if (item == null || (uint)item.GetQuality() != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerIsAlive: // 16
                    if (referencePlayer.IsDead())
                        return false;
                    break;
                case ModifierTreeType.PlayerIsInArea: // 17
                    {
                        uint zoneId, areaId;
                        referencePlayer.GetZoneAndAreaId(out zoneId, out areaId);
                        if (zoneId != reqValue && areaId != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.TargetIsInArea: // 18
                    {
                        if (unit == null)
                            return false;
                        uint zoneId, areaId;
                        unit.GetZoneAndAreaId(out zoneId, out areaId);
                        if (zoneId != reqValue && areaId != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.ItemId: // 19
                    if (miscValue1 != reqValue)
                        return false;
                    break;
                case ModifierTreeType.LegacyDungeonDifficulty: // 20
                    {
                        DifficultyRecord difficulty = CliDB.DifficultyStorage.LookupByKey(referencePlayer.GetMap().GetDifficultyID());
                        if (difficulty == null || difficulty.OldEnumValue == -1 || difficulty.OldEnumValue != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerToTargetLevelDeltaGreaterThan: // 21
                    if (unit == null || referencePlayer.GetLevel() < unit.GetLevel() + reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetToPlayerLevelDeltaGreaterThan: // 22
                    if (!unit || referencePlayer.GetLevel() + reqValue < unit.GetLevel())
                        return false;
                    break;
                case ModifierTreeType.PlayerLevelEqualTargetLevel: // 23
                    if (!unit || referencePlayer.GetLevel() != unit.GetLevel())
                        return false;
                    break;
                case ModifierTreeType.PlayerInArenaWithTeamSize: // 24
                    {
                        Battleground bg = referencePlayer.GetBattleground();
                        if (!bg || !bg.IsArena() || bg.GetArenaType() != (ArenaTypes)reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerRace: // 25
                    if ((uint)referencePlayer.GetRace() != reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerClass: // 26
                    if ((uint)referencePlayer.GetClass() != reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetRace: // 27
                    if (unit == null || !unit.IsTypeId(TypeId.Player) || (uint)unit.GetRace() != reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetClass: // 28
                    if (unit == null || !unit.IsTypeId(TypeId.Player) || (uint)unit.GetClass() != reqValue)
                        return false;
                    break;
                case ModifierTreeType.LessThanTappers: // 29
                    if (referencePlayer.GetGroup() && referencePlayer.GetGroup().GetMembersCount() >= reqValue)
                        return false;
                    break;
                case ModifierTreeType.CreatureType: // 30
                    {
                        if (unit == null)
                            return false;

                        if (!unit.IsTypeId(TypeId.Unit) || (uint)unit.GetCreatureType() != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.CreatureFamily: // 31
                    {
                        if (!unit)
                            return false;
                        if (unit.GetTypeId() != TypeId.Unit || unit.ToCreature().GetCreatureTemplate().Family != (CreatureFamily)reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerMap: // 32
                    if (referencePlayer.GetMapId() != reqValue)
                        return false;
                    break;
                case ModifierTreeType.ClientVersionEqualOrLessThan: // 33
                    if (reqValue < Global.RealmMgr.GetMinorMajorBugfixVersionForBuild(Global.WorldMgr.GetRealm().Build))
                        return false;
                    break;
                case ModifierTreeType.BattlePetTeamLevel: // 34
                    foreach (BattlePetSlot slot in referencePlayer.GetSession().GetBattlePetMgr().GetSlots())
                        if (slot.Pet.Level < reqValue)
                            return false;
                    break;
                case ModifierTreeType.PlayerIsNotInParty: // 35
                    if (referencePlayer.GetGroup())
                        return false;
                    break;
                case ModifierTreeType.PlayerIsInParty: // 36
                    if (!referencePlayer.GetGroup())
                        return false;
                    break;
                case ModifierTreeType.HasPersonalRatingEqualOrGreaterThan: // 37
                    if (referencePlayer.GetMaxPersonalArenaRatingRequirement(0) < reqValue)
                        return false;
                    break;
                case ModifierTreeType.HasTitle: // 38
                    if (!referencePlayer.HasTitle(reqValue))
                        return false;
                    break;
                case ModifierTreeType.PlayerLevelEqual: // 39
                    if (referencePlayer.GetLevel() != reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetLevelEqual: // 40
                    if (unit == null || unit.GetLevelForTarget(referencePlayer) != reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerIsInZone: // 41
                    {
                        uint zoneId = referencePlayer.GetAreaId();
                        AreaTableRecord areaEntry = CliDB.AreaTableStorage.LookupByKey(zoneId);
                        if (areaEntry != null)
                            if (areaEntry.Flags.HasFlag(AreaFlags.Unk9))
                                zoneId = areaEntry.ParentAreaID;
                        if (zoneId != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.TargetIsInZone: // 42
                    {
                        if (!unit)
                            return false;
                        uint zoneId = unit.GetAreaId();
                        AreaTableRecord areaEntry = CliDB.AreaTableStorage.LookupByKey(zoneId);
                        if (areaEntry != null)
                            if (areaEntry.Flags.HasFlag(AreaFlags.Unk9))
                                zoneId = areaEntry.ParentAreaID;
                        if (zoneId != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHealthBelowPercent: // 43
                    if (referencePlayer.GetHealthPct() > (float)reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerHealthAbovePercent: // 44
                    if (referencePlayer.GetHealthPct() < (float)reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerHealthEqualsPercent: // 45
                    if (referencePlayer.GetHealthPct() != (float)reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetHealthBelowPercent: // 46
                    if (unit == null || unit.GetHealthPct() >= (float)reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetHealthAbovePercent: // 47
                    if (!unit || unit.GetHealthPct() < (float)reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetHealthEqualsPercent: // 48
                    if (!unit || unit.GetHealthPct() != (float)reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerHealthBelowValue: // 49
                    if (referencePlayer.GetHealth() > reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerHealthAboveValue: // 50
                    if (referencePlayer.GetHealth() < reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerHealthEqualsValue: // 51
                    if (referencePlayer.GetHealth() != reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetHealthBelowValue: // 52
                    if (!unit || unit.GetHealth() > reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetHealthAboveValue: // 53
                    if (!unit || unit.GetHealth() < reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetHealthEqualsValue: // 54
                    if (!unit || unit.GetHealth() != reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetIsPlayerAndMeetsCondition: // 55
                    {
                        if (unit == null || !unit.IsPlayer())
                            return false;

                        PlayerConditionRecord playerCondition = CliDB.PlayerConditionStorage.LookupByKey(reqValue);
                        if (playerCondition == null || !ConditionManager.IsPlayerMeetingCondition(unit.ToPlayer(), playerCondition))
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasMoreThanAchievementPoints: // 56
                    if (referencePlayer.GetAchievementPoints() <= reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerInLfgDungeon: // 57
                    if (ConditionManager.GetPlayerConditionLfgValue(referencePlayer, PlayerConditionLfgStatus.InLFGDungeon) == 0)
                        return false;
                    break;
                case ModifierTreeType.PlayerInRandomLfgDungeon: // 58
                    if (ConditionManager.GetPlayerConditionLfgValue(referencePlayer, PlayerConditionLfgStatus.InLFGRandomDungeon) == 0)
                        return false;
                    break;
                case ModifierTreeType.PlayerInFirstRandomLfgDungeon: // 59
                    if (ConditionManager.GetPlayerConditionLfgValue(referencePlayer, PlayerConditionLfgStatus.InLFGFirstRandomDungeon) == 0)
                        return false;
                    break;
                case ModifierTreeType.PlayerInRankedArenaMatch: // 60
                    {
                        Battleground bg = referencePlayer.GetBattleground();
                        if (bg == null || !bg.IsArena() || !bg.IsRated())
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerInGuildParty: // 61 NYI
                    return false;
                case ModifierTreeType.PlayerGuildReputationEqualOrGreaterThan: // 62
                    if (referencePlayer.GetReputationMgr().GetReputation(1168) < reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerInRatedBattleground: // 63
                    {
                        Battleground bg = referencePlayer.GetBattleground();
                        if (bg == null || !bg.IsBattleground() || !bg.IsRated())
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerBattlegroundRatingEqualOrGreaterThan: // 64
                    if (referencePlayer.GetRBGPersonalRating() < reqValue)
                        return false;
                    break;
                case ModifierTreeType.ResearchProjectRarity: // 65 NYI
                case ModifierTreeType.ResearchProjectBranch: // 66 NYI
                    return false;
                case ModifierTreeType.WorldStateExpression: // 67
                    WorldStateExpressionRecord worldStateExpression = CliDB.WorldStateExpressionStorage.LookupByKey(reqValue);
                    if (worldStateExpression != null)
                        return ConditionManager.IsPlayerMeetingExpression(referencePlayer, worldStateExpression);
                    return false;
                case ModifierTreeType.DungeonDifficulty: // 68
                    if (referencePlayer.GetMap().GetDifficultyID() != (Difficulty)reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerLevelEqualOrGreaterThan: // 69
                    if (referencePlayer.GetLevel() < reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetLevelEqualOrGreaterThan: // 70
                    if (!unit || unit.GetLevel() < reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerLevelEqualOrLessThan: // 71
                    if (referencePlayer.GetLevel() > reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetLevelEqualOrLessThan: // 72
                    if (!unit || unit.GetLevel() > reqValue)
                        return false;
                    break;
                case ModifierTreeType.ModifierTree: // 73
                    ModifierTreeNode nextModifierTree = Global.CriteriaMgr.GetModifierTree(reqValue);
                    if (nextModifierTree != null)
                        return ModifierTreeSatisfied(nextModifierTree, miscValue1, miscValue2, unit, referencePlayer);
                    return false;
                case ModifierTreeType.PlayerScenario: // 74
                    {
                        Scenario scenario = referencePlayer.GetScenario();
                        if (scenario == null || scenario.GetEntry().Id != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.TillersReputationGreaterThan: // 75
                    if (referencePlayer.GetReputationMgr().GetReputation(1272) < reqValue)
                        return false;
                    break;
                case ModifierTreeType.BattlePetAchievementPointsEqualOrGreaterThan: // 76
                    {
                        static short getRootAchievementCategory(AchievementRecord achievement)
                        {
                            short category = (short)achievement.Category;
                            do
                            {
                                var categoryEntry = CliDB.AchievementCategoryStorage.LookupByKey(category);
                                if (categoryEntry?.Parent == -1)
                                    break;

                                category = categoryEntry.Parent;
                            } while (true);

                            return category;
                        }

                        uint petAchievementPoints = 0;
                        foreach (uint achievementId in referencePlayer.GetCompletedAchievementIds())
                        {
                            var achievement = CliDB.AchievementStorage.LookupByKey(achievementId);
                            if (getRootAchievementCategory(achievement) == SharedConst.AchivementCategoryPetBattles)
                                petAchievementPoints += achievement.Points;
                        }

                        if (petAchievementPoints < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.UniqueBattlePetsEqualOrGreaterThan: // 77
                    if (referencePlayer.GetSession().GetBattlePetMgr().GetPetUniqueSpeciesCount() < reqValue)
                        return false;
                    break;
                case ModifierTreeType.BattlePetType: // 78
                    {
                        var speciesEntry = CliDB.BattlePetSpeciesStorage.LookupByKey(miscValue1);
                        if (speciesEntry?.PetTypeEnum != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.BattlePetHealthPercentLessThan: // 79 NYI - use target battle pet here, the one we were just battling
                    return false;
                case ModifierTreeType.GuildGroupMemberCountEqualOrGreaterThan: // 80
                    {
                        uint guildMemberCount = 0;
                        var group = referencePlayer.GetGroup();
                        if (group != null)
                        {
                            for (var itr = group.GetFirstMember(); itr != null; itr = itr.Next())
                                if (itr.GetSource().GetGuildId() == referencePlayer.GetGuildId())
                                    ++guildMemberCount;
                        }

                        if (guildMemberCount < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.BattlePetOpponentCreatureId: // 81 NYI
                    return false;
                case ModifierTreeType.PlayerScenarioStep: // 82
                    {
                        Scenario scenario = referencePlayer.GetScenario();
                        if (scenario == null)
                            return false;

                        if (scenario.GetStep().OrderIndex != (reqValue - 1))
                            return false;
                        break;
                    }
                case ModifierTreeType.ChallengeModeMedal: // 83
                    return false; // OBSOLETE
                case ModifierTreeType.PlayerOnQuest: // 84
                    if (referencePlayer.FindQuestSlot(reqValue) == SharedConst.MaxQuestLogSize)
                        return false;
                    break;
                case ModifierTreeType.ExaltedWithFaction: // 85
                    if (referencePlayer.GetReputationMgr().GetReputation(reqValue) < 42000)
                        return false;
                    break;
                case ModifierTreeType.EarnedAchievementOnAccount: // 86
                case ModifierTreeType.EarnedAchievementOnPlayer: // 87
                    if (!referencePlayer.HasAchieved(reqValue))
                        return false;
                    break;
                case ModifierTreeType.OrderOfTheCloudSerpentReputationGreaterThan: // 88
                    if (referencePlayer.GetReputationMgr().GetReputation(1271) < reqValue)
                        return false;
                    break;
                case ModifierTreeType.BattlePetQuality: // 89 NYI
                case ModifierTreeType.BattlePetFightWasPVP: // 90 NYI
                    return false;
                case ModifierTreeType.BattlePetSpecies: // 91
                    if (miscValue1 != reqValue)
                        return false;
                    break;
                case ModifierTreeType.ServerExpansionEqualOrGreaterThan: // 92
                    if (WorldConfig.GetIntValue(WorldCfg.Expansion) < reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerHasBattlePetJournalLock: // 93
                    if (!referencePlayer.GetSession().GetBattlePetMgr().HasJournalLock())
                        return false;
                    break;
                case ModifierTreeType.FriendshipRepReactionIsMet: // 94
                    {
                        var friendshipRepReaction = CliDB.FriendshipRepReactionStorage.LookupByKey(reqValue);
                        if (friendshipRepReaction == null)
                            return false;

                        var friendshipReputation = CliDB.FriendshipReputationStorage.LookupByKey(friendshipRepReaction.FriendshipRepID);
                        if (friendshipReputation == null)
                            return false;

                        if (referencePlayer.GetReputation((uint)friendshipReputation.FactionID) < friendshipRepReaction.ReactionThreshold)
                            return false;
                        break;
                    }
                case ModifierTreeType.ReputationWithFactionIsEqualOrGreaterThan: // 95
                    if (referencePlayer.GetReputationMgr().GetReputation(reqValue) < reqValue)
                        return false;
                    break;
                case ModifierTreeType.ItemClassAndSubclass: // 96
                    {
                        ItemTemplate item = Global.ObjectMgr.GetItemTemplate((uint)miscValue1);
                        if (item == null || item.GetClass() != (ItemClass)reqValue || item.GetSubClass() != secondaryAsset)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerGender: // 97
                    if ((int)referencePlayer.GetGender() != reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerNativeGender: // 98
                    if (referencePlayer.GetNativeSex() != (Gender)reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerSkillEqualOrGreaterThan: // 99
                    if (referencePlayer.GetPureSkillValue((SkillType)reqValue) < secondaryAsset)
                        return false;
                    break;
                case ModifierTreeType.PlayerLanguageSkillEqualOrGreaterThan: // 100
                    {
                        var languageDescs = Global.LanguageMgr.GetLanguageDescById((Language)reqValue);
                        if (!languageDescs.Any(desc => referencePlayer.GetSkillValue((SkillType)desc.SkillId) >= secondaryAsset))
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerIsInNormalPhase: // 101
                    if (!PhasingHandler.InDbPhaseShift(referencePlayer, 0, 0, 0))
                        return false;
                    break;
                case ModifierTreeType.PlayerIsInPhase: // 102
                    if (!PhasingHandler.InDbPhaseShift(referencePlayer, 0, (ushort)reqValue, 0))
                        return false;
                    break;
                case ModifierTreeType.PlayerIsInPhaseGroup: // 103
                    if (!PhasingHandler.InDbPhaseShift(referencePlayer, 0, 0, reqValue))
                        return false;
                    break;
                case ModifierTreeType.PlayerKnowsSpell: // 104
                    if (!referencePlayer.HasSpell(reqValue))
                        return false;
                    break;
                case ModifierTreeType.PlayerHasItemQuantity: // 105
                    if (referencePlayer.GetItemCount(reqValue, false) < secondaryAsset)
                        return false;
                    break;
                case ModifierTreeType.PlayerExpansionLevelEqualOrGreaterThan: // 106
                    if (referencePlayer.GetSession().GetExpansion() < (Expansion)reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerHasAuraWithLabel: // 107
                    if (!referencePlayer.HasAura(aura => aura.GetSpellInfo().HasLabel(reqValue)))
                        return false;
                    break;
                case ModifierTreeType.PlayersRealmWorldState: // 108
                    if (Global.WorldMgr.GetWorldState(reqValue) != secondaryAsset)
                        return false;
                    break;
                case ModifierTreeType.TimeBetween: // 109
                    {
                        long from = Time.GetUnixTimeFromPackedTime(reqValue);
                        long to = Time.GetUnixTimeFromPackedTime((uint)secondaryAsset);
                        if (GameTime.GetGameTime() < from || GameTime.GetGameTime() > to)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasCompletedQuest: // 110
                    uint questBit = Global.DB2Mgr.GetQuestUniqueBitFlag(reqValue);
                    if (questBit != 0)
                        if ((referencePlayer.m_activePlayerData.QuestCompleted[((int)questBit - 1) >> 6] & (1ul << (((int)questBit - 1) & 63))) == 0)
                            return false;
                    break;
                case ModifierTreeType.PlayerIsReadyToTurnInQuest: // 111
                    if (referencePlayer.GetQuestStatus(reqValue) != QuestStatus.Complete)
                        return false;
                    break;
                case ModifierTreeType.PlayerHasCompletedQuestObjective: // 112
                    {
                        QuestObjective objective = Global.ObjectMgr.GetQuestObjective(reqValue);
                        if (objective == null)
                            return false;

                        Quest quest = Global.ObjectMgr.GetQuestTemplate(objective.QuestID);
                        if (quest == null)
                            return false;

                        ushort slot = referencePlayer.FindQuestSlot(objective.QuestID);
                        if (slot >= SharedConst.MaxQuestLogSize || referencePlayer.GetQuestRewardStatus(objective.QuestID) || !referencePlayer.IsQuestObjectiveComplete(slot, quest, objective))
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasExploredArea: // 113
                    {
                        AreaTableRecord areaTable = CliDB.AreaTableStorage.LookupByKey(reqValue);
                        if (areaTable == null)
                            return false;

                        if (areaTable.AreaBit <= 0)
                            break; // success

                        int playerIndexOffset = areaTable.AreaBit / 64;
                        if (playerIndexOffset >= PlayerConst.ExploredZonesSize)
                            break;

                        if ((referencePlayer.m_activePlayerData.ExploredZones[playerIndexOffset] & (1ul << (areaTable.AreaBit % 64))) == 0)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasItemQuantityIncludingBank: // 114
                    if (referencePlayer.GetItemCount(reqValue, true) < secondaryAsset)
                        return false;
                    break;
                case ModifierTreeType.Weather: // 115
                    if (referencePlayer.GetMap().GetZoneWeather(referencePlayer.GetZoneId()) != (WeatherState)reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerFaction: // 116
                    {
                        ChrRacesRecord race = CliDB.ChrRacesStorage.LookupByKey(referencePlayer.GetRace());
                        if (race == null)
                            return false;

                        FactionTemplateRecord faction = CliDB.FactionTemplateStorage.LookupByKey(race.FactionID);
                        if (faction == null)
                            return false;

                        int factionIndex = -1;
                        if (faction.FactionGroup.HasAnyFlag((byte)FactionMasks.Horde))
                            factionIndex = 0;
                        else if (faction.FactionGroup.HasAnyFlag((byte)FactionMasks.Alliance))
                            factionIndex = 1;
                        else if (faction.FactionGroup.HasAnyFlag((byte)FactionMasks.Player))
                            factionIndex = 0;
                        if (factionIndex != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.LfgStatusEqual: // 117
                    if (ConditionManager.GetPlayerConditionLfgValue(referencePlayer, (PlayerConditionLfgStatus)reqValue) != secondaryAsset)
                        return false;
                    break;
                case ModifierTreeType.LFgStatusEqualOrGreaterThan: // 118
                    if (ConditionManager.GetPlayerConditionLfgValue(referencePlayer, (PlayerConditionLfgStatus)reqValue) < secondaryAsset)
                        return false;
                    break;
                case ModifierTreeType.PlayerHasCurrencyEqualOrGreaterThan: // 119
                    if (!referencePlayer.HasCurrency(reqValue, (uint)secondaryAsset))
                        return false;
                    break;
                case ModifierTreeType.TargetThreatListSizeLessThan: // 120
                    {
                        if (!unit || !unit.CanHaveThreatList())
                            return false;
                        if (unit.GetThreatManager().GetThreatListSize() >= reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasTrackedCurrencyEqualOrGreaterThan: // 121
                    if (referencePlayer.GetTrackedCurrencyCount(reqValue) < secondaryAsset)
                        return false;
                    break;
                case ModifierTreeType.PlayerMapInstanceType: // 122
                    if ((uint)referencePlayer.GetMap().GetEntry().InstanceType != reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerInTimeWalkerInstance: // 123
                    if (!referencePlayer.HasPlayerFlag(PlayerFlags.Timewalking))
                        return false;
                    break;
                case ModifierTreeType.PvpSeasonIsActive: // 124
                    if (!WorldConfig.GetBoolValue(WorldCfg.ArenaSeasonInProgress))
                        return false;
                    break;
                case ModifierTreeType.PvpSeason: // 125
                    if (WorldConfig.GetIntValue(WorldCfg.ArenaSeasonId) != reqValue)
                        return false;
                    break;
                case ModifierTreeType.GarrisonTierEqualOrGreaterThan: // 126
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)secondaryAsset || garrison.GetSiteLevel().GarrLevel < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonFollowersWithLevelEqualOrGreaterThan: // 127
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null)
                            return false;

                        uint followerCount = garrison.CountFollowers(follower =>
                        {
                            var garrFollower = CliDB.GarrFollowerStorage.LookupByKey(follower.PacketInfo.GarrFollowerID);
                            return garrFollower?.GarrFollowerTypeID == tertiaryAsset && follower.PacketInfo.FollowerLevel >= secondaryAsset;
                        });

                        if (followerCount < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonFollowersWithQualityEqualOrGreaterThan: // 128
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null)
                            return false;

                        uint followerCount = garrison.CountFollowers(follower =>
                        {
                            var garrFollower = CliDB.GarrFollowerStorage.LookupByKey(follower.PacketInfo.GarrFollowerID);
                            return garrFollower?.GarrFollowerTypeID == tertiaryAsset && follower.PacketInfo.Quality >= secondaryAsset;
                        });

                        if (followerCount < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonFollowerWithAbilityAtLevelEqualOrGreaterThan: // 129
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null)
                            return false;

                        uint followerCount = garrison.CountFollowers(follower =>
                        {
                            var garrFollower = CliDB.GarrFollowerStorage.LookupByKey(follower.PacketInfo.GarrFollowerID);
                            return garrFollower?.GarrFollowerTypeID == tertiaryAsset && follower.PacketInfo.FollowerLevel >= reqValue && follower.HasAbility((uint)secondaryAsset);
                        });

                        if (followerCount < 1)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonFollowerWithTraitAtLevelEqualOrGreaterThan: // 130
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null)
                            return false;

                        GarrAbilityRecord traitEntry = CliDB.GarrAbilityStorage.LookupByKey(secondaryAsset);
                        if (traitEntry == null || !traitEntry.Flags.HasAnyFlag(GarrisonAbilityFlags.Trait))
                            return false;

                        uint followerCount = garrison.CountFollowers(follower =>
                        {
                            var garrFollower = CliDB.GarrFollowerStorage.LookupByKey(follower.PacketInfo.GarrFollowerID);
                            return garrFollower?.GarrFollowerTypeID == tertiaryAsset && follower.PacketInfo.FollowerLevel >= reqValue && follower.HasAbility((uint)secondaryAsset);
                        });

                        if (followerCount < 1)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonFollowerWithAbilityAssignedToBuilding: // 131
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)tertiaryAsset)
                            return false;

                        uint followerCount = garrison.CountFollowers(follower =>
                        {
                            GarrBuildingRecord followerBuilding = CliDB.GarrBuildingStorage.LookupByKey(follower.PacketInfo.CurrentBuildingID);
                            if (followerBuilding == null)
                                return false;

                            return followerBuilding.BuildingType == secondaryAsset && follower.HasAbility(reqValue); ;
                        });

                        if (followerCount < 1)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonFollowerWithTraitAssignedToBuilding: // 132
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)tertiaryAsset)
                            return false;

                        GarrAbilityRecord traitEntry = CliDB.GarrAbilityStorage.LookupByKey(reqValue);
                        if (traitEntry == null || !traitEntry.Flags.HasAnyFlag(GarrisonAbilityFlags.Trait))
                            return false;

                        uint followerCount = garrison.CountFollowers(follower =>
                        {
                            GarrBuildingRecord followerBuilding = CliDB.GarrBuildingStorage.LookupByKey(follower.PacketInfo.CurrentBuildingID);
                            if (followerBuilding == null)
                                return false;

                            return followerBuilding.BuildingType == secondaryAsset && follower.HasAbility(reqValue); ;
                        });

                        if (followerCount < 1)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonFollowerWithLevelAssignedToBuilding: // 133
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)tertiaryAsset)
                            return false;

                        uint followerCount = garrison.CountFollowers(follower =>
                        {
                            if (follower.PacketInfo.FollowerLevel < reqValue)
                                return false;

                            GarrBuildingRecord followerBuilding = CliDB.GarrBuildingStorage.LookupByKey(follower.PacketInfo.CurrentBuildingID);
                            if (followerBuilding == null)
                                return false;

                            return followerBuilding.BuildingType == secondaryAsset;
                        });
                        if (followerCount < 1)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonBuildingWithLevelEqualOrGreaterThan: // 134
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)tertiaryAsset)
                            return false;

                        foreach (Garrison.Plot plot in garrison.GetPlots())
                        {
                            if (!plot.BuildingInfo.PacketInfo.HasValue)
                                continue;

                            GarrBuildingRecord building = CliDB.GarrBuildingStorage.LookupByKey(plot.BuildingInfo.PacketInfo.Value.GarrBuildingID);
                            if (building == null || building.UpgradeLevel < reqValue || building.BuildingType != secondaryAsset)
                                continue;

                            return true;
                        }
                        return false;
                    }
                case ModifierTreeType.HasBlueprintForGarrisonBuilding: // 135
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)secondaryAsset)
                            return false;

                        if (!garrison.HasBlueprint(reqValue))
                            return false;
                        break;
                    }
                case ModifierTreeType.HasGarrisonBuildingSpecialization: // 136
                    return false; // OBSOLETE
                case ModifierTreeType.AllGarrisonPlotsAreFull: // 137
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)reqValue)
                            return false;

                        foreach (var plot in garrison.GetPlots())
                            if (!plot.BuildingInfo.PacketInfo.HasValue)
                                return false;
                        break;
                    }
                case ModifierTreeType.PlayerIsInOwnGarrison: // 138
                    if (!referencePlayer.GetMap().IsGarrison() || referencePlayer.GetMap().GetInstanceId() != referencePlayer.GetGUID().GetCounter())
                        return false;
                    break;
                case ModifierTreeType.GarrisonShipmentOfTypeIsPending: // 139 NYI
                    return false;
                case ModifierTreeType.GarrisonBuildingIsUnderConstruction: // 140
                    {
                        GarrBuildingRecord building = CliDB.GarrBuildingStorage.LookupByKey(reqValue);
                        if (building == null)
                            return false;

                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)tertiaryAsset)
                            return false;

                        foreach (Garrison.Plot plot in garrison.GetPlots())
                        {
                            if (!plot.BuildingInfo.PacketInfo.HasValue || plot.BuildingInfo.PacketInfo.Value.GarrBuildingID != reqValue)
                                continue;

                            return !plot.BuildingInfo.PacketInfo.Value.Active;
                        }
                        return false;
                    }
                case ModifierTreeType.GarrisonMissionHasBeenCompleted: // 141 NYI
                    return false;
                case ModifierTreeType.GarrisonBuildingLevelEqual: // 142
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)tertiaryAsset)
                            return false;

                        foreach (Garrison.Plot plot in garrison.GetPlots())
                        {
                            if (!plot.BuildingInfo.PacketInfo.HasValue)
                                continue;

                            GarrBuildingRecord building = CliDB.GarrBuildingStorage.LookupByKey(plot.BuildingInfo.PacketInfo.Value.GarrBuildingID);
                            if (building == null || building.UpgradeLevel != secondaryAsset || building.BuildingType != reqValue)
                                continue;

                            return true;
                        }
                        return false;
                    }
                case ModifierTreeType.GarrisonFollowerHasAbility: // 143
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)secondaryAsset)
                            return false;

                        if (miscValue1 != 0)
                        {
                            Garrison.Follower follower = garrison.GetFollower(miscValue1);
                            if (follower == null)
                                return false;

                            if (!follower.HasAbility(reqValue))
                                return false;
                        }
                        else
                        {
                            uint followerCount = garrison.CountFollowers(follower =>
                            {
                                return follower.HasAbility(reqValue);
                            });

                            if (followerCount < 1)
                                return false;
                        }
                        break;
                    }
                case ModifierTreeType.GarrisonFollowerHasTrait: // 144
                    {
                        GarrAbilityRecord traitEntry = CliDB.GarrAbilityStorage.LookupByKey(reqValue);
                        if (traitEntry == null || !traitEntry.Flags.HasAnyFlag(GarrisonAbilityFlags.Trait))
                            return false;

                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)secondaryAsset)
                            return false;

                        if (miscValue1 != 0)
                        {
                            Garrison.Follower follower = garrison.GetFollower(miscValue1);
                            if (follower == null || !follower.HasAbility(reqValue))
                                return false;
                        }
                        else
                        {
                            uint followerCount = garrison.CountFollowers(follower =>
                            {
                                return follower.HasAbility(reqValue);
                            });

                            if (followerCount < 1)
                                return false;
                        }
                        break;
                    }
                case ModifierTreeType.GarrisonFollowerQualityEqual: // 145
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != GarrisonType.Garrison)
                            return false;

                        if (miscValue1 != 0)
                        {
                            Garrison.Follower follower = garrison.GetFollower(miscValue1);
                            if (follower == null || follower.PacketInfo.Quality < reqValue)
                                return false;
                        }
                        else
                        {
                            uint followerCount = garrison.CountFollowers(follower =>
                            {
                                return follower.PacketInfo.Quality >= reqValue;
                            });

                            if (followerCount < 1)
                                return false;
                        }
                        break;
                    }
                case ModifierTreeType.GarrisonFollowerLevelEqual: // 146
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)secondaryAsset)
                            return false;

                        if (miscValue1 != 0)
                        {
                            Garrison.Follower follower = garrison.GetFollower(miscValue1);
                            if (follower == null || follower.PacketInfo.FollowerLevel != reqValue)
                                return false;
                        }
                        else
                        {
                            uint followerCount = garrison.CountFollowers(follower =>
                            {
                                return follower.PacketInfo.FollowerLevel == reqValue;
                            });

                            if (followerCount < 1)
                                return false;
                        }
                        break;
                    }
                case ModifierTreeType.GarrisonMissionIsRare: // 147 NYI
                case ModifierTreeType.GarrisonMissionIsElite: // 148 NYI
                    return false;
                case ModifierTreeType.CurrentGarrisonBuildingLevelEqual: // 149
                    {
                        if (miscValue1 == 0)
                            return false;

                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null)
                            return false;

                        foreach (var plot in garrison.GetPlots())
                        {
                            if (!plot.BuildingInfo.PacketInfo.HasValue || plot.BuildingInfo.PacketInfo.Value.GarrBuildingID != miscValue1)
                                continue;

                            var building = CliDB.GarrBuildingStorage.LookupByKey(plot.BuildingInfo.PacketInfo.Value.GarrBuildingID);
                            if (building == null || building.UpgradeLevel != reqValue)
                                continue;

                            return true;
                        }
                        break;
                    }
                case ModifierTreeType.GarrisonPlotInstanceHasBuildingThatIsReadyToActivate: // 150
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null)
                            return false;

                        var plot = garrison.GetPlot(reqValue);
                        if (plot == null)
                            return false;

                        if (!plot.BuildingInfo.CanActivate() || !plot.BuildingInfo.PacketInfo.HasValue || plot.BuildingInfo.PacketInfo.Value.Active)
                            return false;
                        break;
                    }
                case ModifierTreeType.BattlePetTeamWithSpeciesEqualOrGreaterThan: // 151
                    {
                        uint count = 0;
                        foreach (BattlePetSlot slot in referencePlayer.GetSession().GetBattlePetMgr().GetSlots())
                            if (slot.Pet.Species == secondaryAsset)
                                ++count;

                        if (count < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.BattlePetTeamWithTypeEqualOrGreaterThan: // 152
                    {
                        uint count = 0;
                        foreach (BattlePetSlot slot in referencePlayer.GetSession().GetBattlePetMgr().GetSlots())
                        {
                            BattlePetSpeciesRecord species = CliDB.BattlePetSpeciesStorage.LookupByKey(slot.Pet.Species);
                            if (species != null)
                                if (species.PetTypeEnum == secondaryAsset)
                                    ++count;
                        }

                        if (count < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PetBattleLastAbility: // 153 NYI
                case ModifierTreeType.PetBattleLastAbilityType: // 154 NYI
                    return false;
                case ModifierTreeType.BattlePetTeamWithAliveEqualOrGreaterThan: // 155
                    {
                        uint count = 0;
                        foreach (var slot in referencePlayer.GetSession().GetBattlePetMgr().GetSlots())
                            if (slot.Pet.Health > 0)
                                ++count;

                        if (count < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.HasGarrisonBuildingActiveSpecialization: // 156
                    return false; // OBSOLETE
                case ModifierTreeType.HasGarrisonFollower: // 157
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null)
                            return false;

                        uint followerCount = garrison.CountFollowers(follower =>
                        {
                            return follower.PacketInfo.GarrFollowerID == reqValue;
                        });

                        if (followerCount < 1)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerQuestObjectiveProgressEqual: // 158
                    {
                        QuestObjective objective = Global.ObjectMgr.GetQuestObjective(reqValue);
                        if (objective == null)
                            return false;

                        if (referencePlayer.GetQuestObjectiveData(objective) != secondaryAsset)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerQuestObjectiveProgressEqualOrGreaterThan: // 159
                    {
                        QuestObjective objective = Global.ObjectMgr.GetQuestObjective(reqValue);
                        if (objective == null)
                            return false;

                        if (referencePlayer.GetQuestObjectiveData(objective) < secondaryAsset)
                            return false;
                        break;
                    }
                case ModifierTreeType.IsPTRRealm: // 160
                case ModifierTreeType.IsBetaRealm: // 161
                case ModifierTreeType.IsQARealm: // 162
                    return false; // always false
                case ModifierTreeType.GarrisonShipmentContainerIsFull: // 163
                    return false;
                case ModifierTreeType.PlayerCountIsValidToStartGarrisonInvasion: // 164
                    return true; // Only 1 player is required and referencePlayer.GetMap() will ALWAYS have at least the referencePlayer on it
                case ModifierTreeType.InstancePlayerCountEqualOrLessThan: // 165
                    if (referencePlayer.GetMap().GetPlayersCountExceptGMs() > reqValue)
                        return false;
                    break;
                case ModifierTreeType.AllGarrisonPlotsFilledWithBuildingsWithLevelEqualOrGreater: // 166
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)reqValue)
                            return false;

                        foreach (var plot in garrison.GetPlots())
                        {
                            if (!plot.BuildingInfo.PacketInfo.HasValue)
                                return false;

                            var building = CliDB.GarrBuildingStorage.LookupByKey(plot.BuildingInfo.PacketInfo.Value.GarrBuildingID);
                            if (building == null || building.UpgradeLevel != reqValue)
                                return false;
                        }
                        break;
                    }
                case ModifierTreeType.GarrisonMissionType: // 167 NYI
                    return false;
                case ModifierTreeType.GarrisonFollowerItemLevelEqualOrGreaterThan: // 168
                    {
                        if (miscValue1 == 0)
                            return false;

                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null)
                            return false;

                        uint followerCount = garrison.CountFollowers(follower =>
                        {
                            return follower.PacketInfo.GarrFollowerID == miscValue1 && follower.GetItemLevel() >= reqValue;
                        });

                        if (followerCount < 1)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonFollowerCountWithItemLevelEqualOrGreaterThan: // 169
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null)
                            return false;

                        uint followerCount = garrison.CountFollowers(follower =>
                        {
                            var garrFollower = CliDB.GarrFollowerStorage.LookupByKey(follower.PacketInfo.GarrFollowerID);
                            return garrFollower?.GarrFollowerTypeID == tertiaryAsset && follower.GetItemLevel() >= secondaryAsset;
                        });

                        if (followerCount < reqValue)
                            return false;

                        break;
                    }
                case ModifierTreeType.GarrisonTierEqual: // 170
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)secondaryAsset || garrison.GetSiteLevel().GarrLevel != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.InstancePlayerCountEqual: // 171
                    if (referencePlayer.GetMap().GetPlayers().Count != reqValue)
                        return false;
                    break;
                case ModifierTreeType.CurrencyId: // 172
                    if (miscValue1 != reqValue)
                        return false;
                    break;
                case ModifierTreeType.SelectionIsPlayerCorpse: // 173
                    if (referencePlayer.GetTarget().GetHigh() != HighGuid.Corpse)
                        return false;
                    break;
                case ModifierTreeType.PlayerCanAcceptQuest: // 174
                    {
                        Quest quest = Global.ObjectMgr.GetQuestTemplate(reqValue);
                        if (quest == null)
                            return false;

                        if (!referencePlayer.CanTakeQuest(quest, false))
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonFollowerCountWithLevelEqualOrGreaterThan: // 175
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)tertiaryAsset)
                            return false;

                        uint followerCount = garrison.CountFollowers(follower =>
                        {
                            var garrFollower = CliDB.GarrFollowerStorage.LookupByKey(follower.PacketInfo.GarrFollowerID);
                            return garrFollower?.GarrFollowerTypeID == tertiaryAsset && follower.PacketInfo.FollowerLevel == secondaryAsset;
                        });

                        if (followerCount < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonFollowerIsInBuilding: // 176
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null)
                            return false;

                        uint followerCount = garrison.CountFollowers(follower =>
                        {
                            return follower.PacketInfo.GarrFollowerID == reqValue && follower.PacketInfo.CurrentBuildingID == secondaryAsset;
                        });

                        if (followerCount < 1)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonMissionCountLessThan: // 177 NYI
                    return false;
                case ModifierTreeType.GarrisonPlotInstanceCountEqualOrGreaterThan: // 178
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null || garrison.GetGarrisonType() != (GarrisonType)reqValue)
                            return false;

                        uint plotCount = 0;
                        foreach (var plot in garrison.GetPlots())
                        {
                            var garrPlotInstance = CliDB.GarrPlotInstanceStorage.LookupByKey(plot.PacketInfo.GarrPlotInstanceID);
                            if (garrPlotInstance == null || garrPlotInstance.GarrPlotID != secondaryAsset)
                                continue;

                            ++plotCount;
                        }

                        if (plotCount < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.CurrencySource: // 179 NYI
                    return false;
                case ModifierTreeType.PlayerIsInNotOwnGarrison: // 180
                    if (!referencePlayer.GetMap().IsGarrison() || referencePlayer.GetMap().GetInstanceId() == referencePlayer.GetGUID().GetCounter())
                        return false;
                    break;
                case ModifierTreeType.HasActiveGarrisonFollower: // 181
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null)
                            return false;

                        uint followerCount = garrison.CountFollowers(follower => follower.PacketInfo.GarrFollowerID == reqValue && (follower.PacketInfo.FollowerStatus & (byte)GarrisonFollowerStatus.Inactive) == 0);
                        if (followerCount < 1)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerDailyRandomValueMod_X_Equals: // 182 NYI
                    return false;
                case ModifierTreeType.PlayerHasMount: // 183
                    {
                        foreach (var pair in referencePlayer.GetSession().GetCollectionMgr().GetAccountMounts())
                        {
                            var mount = Global.DB2Mgr.GetMount(pair.Key);
                            if (mount == null)
                                continue;

                            if (mount.Id == reqValue)
                                return true;
                        }
                        return false;
                    }
                case ModifierTreeType.GarrisonFollowerCountWithInactiveWithItemLevelEqualOrGreaterThan: // 184
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null)
                            return false;

                        uint followerCount = garrison.CountFollowers(follower =>
                        {
                            GarrFollowerRecord garrFollower = CliDB.GarrFollowerStorage.LookupByKey(follower.PacketInfo.GarrFollowerID);
                            if (garrFollower == null)
                                return false;

                            return follower.GetItemLevel() >= secondaryAsset && garrFollower.GarrFollowerTypeID == tertiaryAsset;
                        });

                        if (followerCount < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonFollowerIsOnAMission: // 185
                    {
                        Garrison garrison = referencePlayer.GetGarrison();
                        if (garrison == null)
                            return false;

                        uint followerCount = garrison.CountFollowers(follower => follower.PacketInfo.GarrFollowerID == reqValue && follower.PacketInfo.CurrentMissionID != 0);
                        if (followerCount < 1)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonMissionCountInSetLessThan: // 186 NYI
                    return false;
                case ModifierTreeType.GarrisonFollowerType: // 187
                    {
                        var garrFollower = CliDB.GarrFollowerStorage.LookupByKey(miscValue1);
                        if (garrFollower == null || garrFollower.GarrFollowerTypeID != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerUsedBoostLessThanHoursAgoRealTime: // 188 NYI
                case ModifierTreeType.PlayerUsedBoostLessThanHoursAgoGameTime: // 189 NYI
                    return false;
                case ModifierTreeType.PlayerIsMercenary: // 190
                    if (!referencePlayer.HasPlayerFlagEx(PlayerFlagsEx.MercenaryMode))
                        return false;
                    break;
                case ModifierTreeType.PlayerEffectiveRace: // 191 NYI
                case ModifierTreeType.TargetEffectiveRace: // 192 NYI
                    return false;
                case ModifierTreeType.HonorLevelEqualOrGreaterThan: // 193
                    if (referencePlayer.GetHonorLevel() < reqValue)
                        return false;
                    break;
                case ModifierTreeType.PrestigeLevelEqualOrGreaterThan: // 194
                    return false; // OBSOLOTE
                case ModifierTreeType.GarrisonMissionIsReadyToCollect: // 195 NYI
                case ModifierTreeType.PlayerIsInstanceOwner: // 196 NYI
                    return false;
                case ModifierTreeType.PlayerHasHeirloom: // 197
                    if (!referencePlayer.GetSession().GetCollectionMgr().GetAccountHeirlooms().ContainsKey(reqValue))
                        return false;
                    break;
                case ModifierTreeType.TeamPoints: // 198 NYI
                    return false;
                case ModifierTreeType.PlayerHasToy: // 199
                    if (!referencePlayer.GetSession().GetCollectionMgr().HasToy(reqValue))
                        return false;
                    break;
                case ModifierTreeType.PlayerHasTransmog: // 200
                    {
                        var (PermAppearance, TempAppearance) = referencePlayer.GetSession().GetCollectionMgr().HasItemAppearance(reqValue);
                        if (!PermAppearance || TempAppearance)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonTalentSelected: // 201 NYI
                case ModifierTreeType.GarrisonTalentResearched: // 202 NYI
                    return false;
                case ModifierTreeType.PlayerHasRestriction: // 203
                    {
                        int restrictionIndex = referencePlayer.m_activePlayerData.CharacterRestrictions.FindIndexIf(restriction => restriction.Type == reqValue);
                        if (restrictionIndex < 0)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerCreatedCharacterLessThanHoursAgoRealTime: // 204 NYI
                    return false;
                case ModifierTreeType.PlayerCreatedCharacterLessThanHoursAgoGameTime: // 205
                    if (TimeSpan.FromHours(reqValue) >= TimeSpan.FromSeconds(referencePlayer.GetTotalPlayedTime()))
                        return false;
                    break;
                case ModifierTreeType.QuestHasQuestInfoId: // 206
                    {
                        Quest quest = Global.ObjectMgr.GetQuestTemplate((uint)miscValue1);
                        if (quest == null || quest.Id != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonTalentResearchInProgress: // 207 NYI
                    return false;
                case ModifierTreeType.PlayerEquippedArtifactAppearanceSet: // 208
                    {
                        Aura artifactAura = referencePlayer.GetAura(PlayerConst.ArtifactsAllWeaponsGeneralWeaponEquippedPassive);
                        if (artifactAura != null)
                        {
                            Item artifact = referencePlayer.GetItemByGuid(artifactAura.GetCastItemGUID());
                            if (artifact != null)
                            {
                                ArtifactAppearanceRecord artifactAppearance = CliDB.ArtifactAppearanceStorage.LookupByKey(artifact.GetModifier(ItemModifier.ArtifactAppearanceId));
                                if (artifactAppearance != null)
                                    if (artifactAppearance.ArtifactAppearanceSetID == reqValue)
                                        break;
                            }
                        }
                        return false;
                    }
                case ModifierTreeType.PlayerHasCurrencyEqual: // 209
                    if (referencePlayer.GetCurrency(reqValue) != secondaryAsset)
                        return false;
                    break;
                case ModifierTreeType.MinimumAverageItemHighWaterMarkForSpec: // 210 NYI
                    return false;
                case ModifierTreeType.PlayerScenarioType: // 211
                    {
                        Scenario scenario = referencePlayer.GetScenario();
                        if (scenario == null)
                            return false;

                        if (scenario.GetEntry().Type != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayersAuthExpansionLevelEqualOrGreaterThan: // 212
                    if (referencePlayer.GetSession().GetAccountExpansion() < (Expansion)reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerLastWeek2v2Rating: // 213 NYI
                case ModifierTreeType.PlayerLastWeek3v3Rating: // 214 NYI
                case ModifierTreeType.PlayerLastWeekRBGRating: // 215 NYI
                    return false;
                case ModifierTreeType.GroupMemberCountFromConnectedRealmEqualOrGreaterThan: // 216
                    {
                        uint memberCount = 0;
                        var group = referencePlayer.GetGroup();
                        if (group != null)
                        {
                            for (var itr = group.GetFirstMember(); itr != null; itr = itr.Next())
                                if (itr.GetSource() != referencePlayer && referencePlayer.m_playerData.VirtualPlayerRealm == itr.GetSource().m_playerData.VirtualPlayerRealm)
                                    ++memberCount;
                        }

                        if (memberCount < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.ArtifactTraitUnlockedCountEqualOrGreaterThan: // 217
                    {
                        Item artifact = referencePlayer.GetItemByEntry((uint)secondaryAsset, ItemSearchLocation.Everywhere);
                        if (artifact == null)
                            return false;

                        if (artifact.GetTotalUnlockedArtifactPowers() < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.ParagonReputationLevelEqualOrGreaterThan: // 218
                    if (referencePlayer.GetReputationMgr().GetParagonLevel((uint)miscValue1) < reqValue)
                        return false;
                    return false;
                case ModifierTreeType.GarrisonShipmentIsReady: // 219 NYI
                    return false;
                case ModifierTreeType.PlayerIsInPvpBrawl: // 220
                    {
                        var bg = CliDB.BattlemasterListStorage.LookupByKey(referencePlayer.GetBattlegroundTypeId());
                        if (bg == null || !bg.Flags.HasFlag(BattlemasterListFlags.Brawl))
                            return false;
                        break;
                    }
                case ModifierTreeType.ParagonReputationLevelWithFactionEqualOrGreaterThan: // 221
                    {
                        var faction = CliDB.FactionStorage.LookupByKey(secondaryAsset);
                        if (faction == null)
                            return false;

                        if (referencePlayer.GetReputationMgr().GetParagonLevel(faction.ParagonFactionID) < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasItemWithBonusListFromTreeAndQuality: // 222
                    {
                        var bonusListIDs = Global.DB2Mgr.GetAllItemBonusTreeBonuses(reqValue);
                        if (bonusListIDs.Empty())
                            return false;

                        bool bagScanReachedEnd = referencePlayer.ForEachItem(ItemSearchLocation.Everywhere, item =>
                        {
                            bool hasBonus = item.m_itemData.BonusListIDs._value.Any(bonusListID => bonusListIDs.Contains(bonusListID));
                            return !hasBonus;
                        });

                        if (bagScanReachedEnd)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasEmptyInventorySlotCountEqualOrGreaterThan: // 223
                    if (referencePlayer.GetFreeInventorySlotCount(ItemSearchLocation.Inventory) < reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerHasItemInHistoryOfProgressiveEvent: // 224 NYI
                    return false;
                case ModifierTreeType.PlayerHasArtifactPowerRankCountPurchasedEqualOrGreaterThan: // 225
                    {
                        Aura artifactAura = referencePlayer.GetAura(PlayerConst.ArtifactsAllWeaponsGeneralWeaponEquippedPassive);
                        if (artifactAura == null)
                            return false;

                        Item artifact = referencePlayer.GetItemByGuid(artifactAura.GetCastItemGUID());
                        if (!artifact)
                            return false;

                        var artifactPower = artifact.GetArtifactPower((uint)secondaryAsset);
                        if (artifactPower == null)
                            return false;

                        if (artifactPower.PurchasedRank < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasBoosted: // 226
                    if (referencePlayer.HasLevelBoosted())
                        return false;
                    break;
                case ModifierTreeType.PlayerHasRaceChanged: // 227
                    if (referencePlayer.HasRaceChanged())
                        return false;
                    break;
                case ModifierTreeType.PlayerHasBeenGrantedLevelsFromRaF: // 228
                    if (referencePlayer.HasBeenGrantedLevelsFromRaF())
                        return false;
                    break;
                case ModifierTreeType.IsTournamentRealm: // 229
                    return false;
                case ModifierTreeType.PlayerCanAccessAlliedRaces: // 230
                    if (!referencePlayer.GetSession().CanAccessAlliedRaces())
                        return false;
                    break;
                case ModifierTreeType.GroupMemberCountWithAchievementEqualOrLessThan: // 231
                    {
                        var group = referencePlayer.GetGroup();
                        if (group != null)
                        {
                            uint membersWithAchievement = 0;
                            for (var itr = group.GetFirstMember(); itr != null; itr = itr.Next())
                                if (itr.GetSource().HasAchieved((uint)secondaryAsset))
                                    ++membersWithAchievement;

                            if (membersWithAchievement > reqValue)
                                return false;
                        }
                        // true if no group
                        break;
                    }
                case ModifierTreeType.PlayerMainhandWeaponType: // 232
                    {
                        var visibleItem = referencePlayer.m_playerData.VisibleItems[EquipmentSlot.MainHand];
                        uint itemSubclass = (uint)ItemSubClassWeapon.Fist;
                        ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(visibleItem.ItemID);
                        if (itemTemplate != null)
                        {
                            if (itemTemplate.GetClass() == ItemClass.Weapon)
                            {
                                itemSubclass = itemTemplate.GetSubClass();

                                var itemModifiedAppearance = Global.DB2Mgr.GetItemModifiedAppearance(visibleItem.ItemID, visibleItem.ItemAppearanceModID);
                                if (itemModifiedAppearance != null)
                                {
                                    var itemModifiedAppearaceExtra = CliDB.ItemModifiedAppearanceExtraStorage.LookupByKey(itemModifiedAppearance.Id);
                                    if (itemModifiedAppearaceExtra != null)
                                        if (itemModifiedAppearaceExtra.DisplayWeaponSubclassID > 0)
                                            itemSubclass = (uint)itemModifiedAppearaceExtra.DisplayWeaponSubclassID;
                                }
                            }
                        }
                        if (itemSubclass != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerOffhandWeaponType: // 233
                    {
                        var visibleItem = referencePlayer.m_playerData.VisibleItems[EquipmentSlot.OffHand];
                        uint itemSubclass = (uint)ItemSubClassWeapon.Fist;
                        ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(visibleItem.ItemID);
                        if (itemTemplate != null)
                        {
                            if (itemTemplate.GetClass() == ItemClass.Weapon)
                            {
                                itemSubclass = itemTemplate.GetSubClass();

                                var itemModifiedAppearance = Global.DB2Mgr.GetItemModifiedAppearance(visibleItem.ItemID, visibleItem.ItemAppearanceModID);
                                if (itemModifiedAppearance != null)
                                {
                                    var itemModifiedAppearaceExtra = CliDB.ItemModifiedAppearanceExtraStorage.LookupByKey(itemModifiedAppearance.Id);
                                    if (itemModifiedAppearaceExtra != null)
                                        if (itemModifiedAppearaceExtra.DisplayWeaponSubclassID > 0)
                                            itemSubclass = (uint)itemModifiedAppearaceExtra.DisplayWeaponSubclassID;
                                }
                            }
                        }
                        if (itemSubclass != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerPvpTier: // 234
                    {
                        var pvpTier = CliDB.PvpTierStorage.LookupByKey(reqValue);
                        if (pvpTier == null)
                            return false;

                        if (pvpTier.BracketID >= referencePlayer.m_activePlayerData.PvpInfo.GetSize())
                            return false;

                        var pvpInfo = referencePlayer.m_activePlayerData.PvpInfo[pvpTier.BracketID];
                        if (pvpTier.Id != pvpInfo.PvpTierID || pvpInfo.Disqualified)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerAzeriteLevelEqualOrGreaterThan: // 235
                    {
                        Item heartOfAzeroth = referencePlayer.GetItemByEntry(PlayerConst.ItemIdHeartOfAzeroth, ItemSearchLocation.Everywhere);
                        if (!heartOfAzeroth || heartOfAzeroth.ToAzeriteItem().GetLevel() < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerIsOnQuestInQuestline: // 236
                    {
                        bool isOnQuest = false;
                        var questLineQuests = Global.DB2Mgr.GetQuestsForQuestLine(reqValue);
                        if (!questLineQuests.Empty())
                            isOnQuest = questLineQuests.Any(questLineQuest => referencePlayer.FindQuestSlot(questLineQuest.QuestID) < SharedConst.MaxQuestLogSize);

                        if (!isOnQuest)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerIsQnQuestLinkedToScheduledWorldStateGroup: // 237
                    return false; // OBSOLETE (db2 removed)
                case ModifierTreeType.PlayerIsInRaidGroup: // 238
                    {
                        var group = referencePlayer.GetGroup();
                        if (group == null || !group.IsRaidGroup())
                            return false;

                        break;
                    }
                case ModifierTreeType.PlayerPvpTierInBracketEqualOrGreaterThan: // 239
                    {
                        if (secondaryAsset >= referencePlayer.m_activePlayerData.PvpInfo.GetSize())
                            return false;

                        var pvpInfo = referencePlayer.m_activePlayerData.PvpInfo[secondaryAsset];
                        var pvpTier = CliDB.PvpTierStorage.LookupByKey(pvpInfo.PvpTierID);
                        if (pvpTier == null)
                            return false;

                        if (pvpTier.Rank < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerCanAcceptQuestInQuestline: // 240
                    {
                        var questLineQuests = Global.DB2Mgr.GetQuestsForQuestLine(reqValue);
                        if (questLineQuests.Empty())
                            return false;

                        bool canTakeQuest = questLineQuests.Any(questLineQuest =>
                        {
                            Quest quest = Global.ObjectMgr.GetQuestTemplate(questLineQuest.QuestID);
                            if (quest != null)
                                return referencePlayer.CanTakeQuest(quest, false);

                            return false;
                        });

                        if (!canTakeQuest)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasCompletedQuestline: // 241
                    {
                        var questLineQuests = Global.DB2Mgr.GetQuestsForQuestLine(reqValue);
                        if (questLineQuests.Empty())
                            return false;

                        foreach (var questLineQuest in questLineQuests)
                            if (!referencePlayer.GetQuestRewardStatus(questLineQuest.QuestID))
                                return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasCompletedQuestlineQuestCount: // 242
                    {
                        var questLineQuests = Global.DB2Mgr.GetQuestsForQuestLine(reqValue);
                        if (questLineQuests.Empty())
                            return false;

                        uint completedQuests = 0;
                        foreach (var questLineQuest in questLineQuests)
                            if (referencePlayer.GetQuestRewardStatus(questLineQuest.QuestID))
                                ++completedQuests;

                        if (completedQuests < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasCompletedPercentageOfQuestline: // 243
                    {
                        var questLineQuests = Global.DB2Mgr.GetQuestsForQuestLine(reqValue);
                        if (questLineQuests.Empty())
                            return false;

                        int completedQuests = 0;
                        foreach (var questLineQuest in questLineQuests)
                            if (referencePlayer.GetQuestRewardStatus(questLineQuest.QuestID))
                                ++completedQuests;

                        if (MathFunctions.GetPctOf(completedQuests, questLineQuests.Count) < reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasWarModeEnabled: // 244
                    if (!referencePlayer.HasPlayerLocalFlag(PlayerLocalFlags.WarMode))
                        return false;
                    break;
                case ModifierTreeType.PlayerIsOnWarModeShard: // 245
                    if (!referencePlayer.HasPlayerFlag(PlayerFlags.WarModeActive))
                        return false;
                    break;
                case ModifierTreeType.PlayerIsAllowedToToggleWarModeInArea: // 246
                    if (!referencePlayer.CanEnableWarModeInArea())
                        return false;
                    break;
                case ModifierTreeType.MythicPlusKeystoneLevelEqualOrGreaterThan: // 247 NYI
                case ModifierTreeType.MythicPlusCompletedInTime: // 248 NYI
                case ModifierTreeType.MythicPlusMapChallengeMode: // 249 NYI
                case ModifierTreeType.MythicPlusDisplaySeason: // 250 NYI
                case ModifierTreeType.MythicPlusMilestoneSeason: // 251 NYI
                    return false;
                case ModifierTreeType.PlayerVisibleRace: // 252
                    {
                        CreatureDisplayInfoRecord creatureDisplayInfo = CliDB.CreatureDisplayInfoStorage.LookupByKey(referencePlayer.GetDisplayId());
                        if (creatureDisplayInfo == null)
                            return false;

                        CreatureDisplayInfoExtraRecord creatureDisplayInfoExtra = CliDB.CreatureDisplayInfoExtraStorage.LookupByKey(creatureDisplayInfo.ExtendedDisplayInfoID);
                        if (creatureDisplayInfoExtra == null)
                            return false;

                        if (creatureDisplayInfoExtra.DisplayRaceID != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.TargetVisibleRace: // 253
                    {
                        if (!unit)
                            return false;
                        CreatureDisplayInfoRecord creatureDisplayInfo = CliDB.CreatureDisplayInfoStorage.LookupByKey(unit.GetDisplayId());
                        if (creatureDisplayInfo == null)
                            return false;

                        CreatureDisplayInfoExtraRecord creatureDisplayInfoExtra = CliDB.CreatureDisplayInfoExtraStorage.LookupByKey(creatureDisplayInfo.ExtendedDisplayInfoID);
                        if (creatureDisplayInfoExtra == null)
                            return false;

                        if (creatureDisplayInfoExtra.DisplayRaceID != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.FriendshipRepReactionEqual: // 254
                    {
                        var friendshipRepReaction = CliDB.FriendshipRepReactionStorage.LookupByKey(reqValue);
                        if (friendshipRepReaction == null)
                            return false;

                        var friendshipReputation = CliDB.FriendshipReputationStorage.LookupByKey(friendshipRepReaction.FriendshipRepID);
                        if (friendshipReputation == null)
                            return false;

                        var friendshipReactions = Global.DB2Mgr.GetFriendshipRepReactions(reqValue);
                        if (friendshipReactions == null)
                            return false;

                        int rank = (int)referencePlayer.GetReputationRank((uint)friendshipReputation.FactionID);
                        if (rank >= friendshipReactions.Count)
                            return false;

                        if (friendshipReactions[rank].Id != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerAuraStackCountEqual: // 255
                    if (referencePlayer.GetAuraCount((uint)secondaryAsset) != reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetAuraStackCountEqual: // 256
                    if (!unit || unit.GetAuraCount((uint)secondaryAsset) != reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerAuraStackCountEqualOrGreaterThan: // 257
                    if (referencePlayer.GetAuraCount((uint)secondaryAsset) < reqValue)
                        return false;
                    break;
                case ModifierTreeType.TargetAuraStackCountEqualOrGreaterThan: // 258
                    if (!unit || unit.GetAuraCount((uint)secondaryAsset) < reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerHasAzeriteEssenceRankLessThan: // 259
                    {
                        Item heartOfAzeroth = referencePlayer.GetItemByEntry(PlayerConst.ItemIdHeartOfAzeroth, ItemSearchLocation.Everywhere);
                        if (heartOfAzeroth != null)
                        {
                            AzeriteItem azeriteItem = heartOfAzeroth.ToAzeriteItem();
                            if (azeriteItem != null)
                            {
                                foreach (UnlockedAzeriteEssence essence in azeriteItem.m_azeriteItemData.UnlockedEssences)
                                    if (essence.AzeriteEssenceID == reqValue && essence.Rank < secondaryAsset)
                                        return true;
                            }
                        }
                        return false;
                    }
                case ModifierTreeType.PlayerHasAzeriteEssenceRankEqual: // 260
                    {
                        Item heartOfAzeroth = referencePlayer.GetItemByEntry(PlayerConst.ItemIdHeartOfAzeroth, ItemSearchLocation.Everywhere);
                        if (heartOfAzeroth != null)
                        {
                            AzeriteItem azeriteItem = heartOfAzeroth.ToAzeriteItem();
                            if (azeriteItem != null)
                            {
                                foreach (UnlockedAzeriteEssence essence in azeriteItem.m_azeriteItemData.UnlockedEssences)
                                    if (essence.AzeriteEssenceID == reqValue && essence.Rank == secondaryAsset)
                                        return true;
                            }
                        }
                        return false;
                    }
                case ModifierTreeType.PlayerHasAzeriteEssenceRankGreaterThan: // 261
                    {
                        Item heartOfAzeroth = referencePlayer.GetItemByEntry(PlayerConst.ItemIdHeartOfAzeroth, ItemSearchLocation.Everywhere);
                        if (heartOfAzeroth != null)
                        {
                            AzeriteItem azeriteItem = heartOfAzeroth.ToAzeriteItem();
                            if (azeriteItem != null)
                            {
                                foreach (UnlockedAzeriteEssence essence in azeriteItem.m_azeriteItemData.UnlockedEssences)
                                    if (essence.AzeriteEssenceID == reqValue && essence.Rank > secondaryAsset)
                                        return true;
                            }
                        }
                        return false;
                    }
                case ModifierTreeType.PlayerHasAuraWithEffectIndex: // 262
                    if (referencePlayer.GetAuraEffect(reqValue, (uint)secondaryAsset) == null)
                        return false;
                    break;
                case ModifierTreeType.PlayerLootSpecializationMatchesRole: // 263
                    {
                        ChrSpecializationRecord spec = CliDB.ChrSpecializationStorage.LookupByKey(referencePlayer.GetPrimarySpecialization());
                        if (spec == null || spec.Role != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerIsAtMaxExpansionLevel: // 264
                    if (referencePlayer.GetLevel() != Global.ObjectMgr.GetMaxLevelForExpansion((Expansion)WorldConfig.GetIntValue(WorldCfg.Expansion)))
                        return false;
                    break;
                case ModifierTreeType.TransmogSource: // 265
                    {
                        var itemModifiedAppearance = CliDB.ItemModifiedAppearanceStorage.LookupByKey(miscValue2);
                        if (itemModifiedAppearance == null)
                            return false;

                        if (itemModifiedAppearance.TransmogSourceTypeEnum != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasAzeriteEssenceInSlotAtRankLessThan: // 266
                    {
                        Item heartOfAzeroth = referencePlayer.GetItemByEntry(PlayerConst.ItemIdHeartOfAzeroth, ItemSearchLocation.Everywhere);
                        if (heartOfAzeroth != null)
                        {
                            AzeriteItem azeriteItem = heartOfAzeroth.ToAzeriteItem();
                            if (azeriteItem != null)
                            {
                                SelectedAzeriteEssences selectedEssences = azeriteItem.GetSelectedAzeriteEssences();
                                if (selectedEssences != null)
                                {
                                    foreach (UnlockedAzeriteEssence essence in azeriteItem.m_azeriteItemData.UnlockedEssences)
                                        if (essence.AzeriteEssenceID == selectedEssences.AzeriteEssenceID[(int)reqValue] && essence.Rank < secondaryAsset)
                                            return true;
                                }
                            }
                        }
                        return false;
                    }
                case ModifierTreeType.PlayerHasAzeriteEssenceInSlotAtRankGreaterThan: // 267
                    {
                        Item heartOfAzeroth = referencePlayer.GetItemByEntry(PlayerConst.ItemIdHeartOfAzeroth, ItemSearchLocation.Everywhere);
                        if (heartOfAzeroth != null)
                        {
                            AzeriteItem azeriteItem = heartOfAzeroth.ToAzeriteItem();
                            if (azeriteItem != null)
                            {
                                SelectedAzeriteEssences selectedEssences = azeriteItem.GetSelectedAzeriteEssences();
                                if (selectedEssences != null)
                                {
                                    foreach (UnlockedAzeriteEssence essence in azeriteItem.m_azeriteItemData.UnlockedEssences)
                                        if (essence.AzeriteEssenceID == selectedEssences.AzeriteEssenceID[(int)reqValue] && essence.Rank > secondaryAsset)
                                            return true;
                                }
                            }
                        }
                        return false;
                    }
                case ModifierTreeType.PlayerLevelWithinContentTuning: // 268
                    {
                        uint level = referencePlayer.GetLevel();
                        var levels = Global.DB2Mgr.GetContentTuningData(reqValue, 0);
                        if (levels.HasValue)
                        {
                            if (secondaryAsset != 0)
                                return level >= levels.Value.MinLevelWithDelta && level <= levels.Value.MaxLevelWithDelta;
                            return level >= levels.Value.MinLevel && level <= levels.Value.MaxLevel;
                        }
                        return false;
                    }
                case ModifierTreeType.TargetLevelWithinContentTuning: // 269
                    {
                        if (!unit)
                            return false;

                        uint level = unit.GetLevel();
                        var levels = Global.DB2Mgr.GetContentTuningData(reqValue, 0);
                        if (levels.HasValue)
                        {
                            if (secondaryAsset != 0)
                                return level >= levels.Value.MinLevelWithDelta && level <= levels.Value.MaxLevelWithDelta;
                            return level >= levels.Value.MinLevel && level <= levels.Value.MaxLevel;
                        }
                        return false;
                    }
                case ModifierTreeType.PlayerIsScenarioInitiator: // 270 NYI
                    return false;
                case ModifierTreeType.PlayerHasCompletedQuestOrIsOnQuest: // 271
                    {
                        QuestStatus status = referencePlayer.GetQuestStatus(reqValue);
                        if (status == QuestStatus.None || status == QuestStatus.Failed)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerLevelWithinOrAboveContentTuning: // 272
                    {
                        uint level = referencePlayer.GetLevel();
                        var levels = Global.DB2Mgr.GetContentTuningData(reqValue, 0);
                        if (levels.HasValue)
                            return secondaryAsset != 0 ? level >= levels.Value.MinLevelWithDelta : level >= levels.Value.MinLevel;
                        return false;
                    }
                case ModifierTreeType.TargetLevelWithinOrAboveContentTuning: // 273
                    {
                        if (!unit)
                            return false;

                        uint level = unit.GetLevel();
                        var levels = Global.DB2Mgr.GetContentTuningData(reqValue, 0);
                        if (levels.HasValue)
                            return secondaryAsset != 0 ? level >= levels.Value.MinLevelWithDelta : level >= levels.Value.MinLevel;
                        return false;
                    }
                case ModifierTreeType.PlayerLevelWithinOrAboveLevelRange: // 274 NYI
                case ModifierTreeType.TargetLevelWithinOrAboveLevelRange: // 275 NYI
                    return false;
                case ModifierTreeType.MaxJailersTowerLevelEqualOrGreaterThan: // 276
                    if (referencePlayer.m_activePlayerData.JailersTowerLevelMax < reqValue)
                        return false;
                    break;
                case ModifierTreeType.GroupedWithRaFRecruit: // 277
                    {
                        var group = referencePlayer.GetGroup();
                        if (group == null)
                            return false;

                        for (var itr = group.GetFirstMember(); itr != null; itr = itr.Next())
                            if (itr.GetSource().GetSession().GetRecruiterId() == referencePlayer.GetSession().GetAccountId())
                                return true;

                        return false;
                    }
                case ModifierTreeType.GroupedWithRaFRecruiter: // 278
                    {
                        var group = referencePlayer.GetGroup();
                        if (group == null)
                            return false;

                        for (var itr = group.GetFirstMember(); itr != null; itr = itr.Next())
                            if (itr.GetSource().GetSession().GetAccountId() == referencePlayer.GetSession().GetRecruiterId())
                                return true;

                        return false;
                    }
                case ModifierTreeType.PlayerSpecialization: // 279
                    if (referencePlayer.GetPrimarySpecialization() != reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerMapOrCosmeticChildMap: // 280
                    {
                        MapRecord map = referencePlayer.GetMap().GetEntry();
                        if (map.Id != reqValue && map.CosmeticParentMapID != reqValue)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerCanAccessShadowlandsPrepurchaseContent: // 281
                    if (referencePlayer.GetSession().GetAccountExpansion() < Expansion.ShadowLands)
                        return false;
                    break;
                case ModifierTreeType.PlayerHasEntitlement: // 282 NYI
                case ModifierTreeType.PlayerIsInPartySyncGroup: // 283 NYI
                case ModifierTreeType.QuestHasPartySyncRewards: // 284 NYI
                case ModifierTreeType.HonorGainSource: // 285 NYI
                case ModifierTreeType.JailersTowerActiveFloorIndexEqualOrGreaterThan: // 286 NYI
                case ModifierTreeType.JailersTowerActiveFloorDifficultyEqualOrGreaterThan: // 287 NYI
                    return false;
                case ModifierTreeType.PlayerCovenant: // 288
                    if (referencePlayer.m_playerData.CovenantID != reqValue)
                        return false;
                    break;
                case ModifierTreeType.HasTimeEventPassed: // 289
                    {
                        long eventTimestamp = GameTime.GetGameTime();
                        switch (reqValue)
                        {
                            case 111: // Battle for Azeroth Season 4 Start
                                eventTimestamp = 1579618800L; // January 21, 2020 8:00
                                break;
                            case 120: // Patch 9.0.1
                                eventTimestamp = 1602601200L; // October 13, 2020 8:00
                                break;
                            case 121: // Shadowlands Season 1 Start
                                eventTimestamp = 1607439600L; // December 8, 2020 8:00
                                break;
                            case 123: // Shadowlands Season 1 End
                                      // timestamp = unknown
                                break; ;
                            case 149: // Shadowlands Season 2 End
                                      // timestamp = unknown
                                break;
                            default:
                                break;
                        }
                        if (GameTime.GetGameTime() < eventTimestamp)
                            return false;
                        break;
                    }
                case ModifierTreeType.GarrisonHasPermanentTalent: // 290 NYI
                    return false;
                case ModifierTreeType.HasActiveSoulbind: // 291
                    if (referencePlayer.m_playerData.SoulbindID != reqValue)
                        return false;
                    break;
                case ModifierTreeType.HasMemorizedSpell: // 292 NYI
                    return false;
                case ModifierTreeType.PlayerHasAPACSubscriptionReward_2020: // 293
                case ModifierTreeType.PlayerHasTBCCDEWarpStalker_Mount: // 294
                case ModifierTreeType.PlayerHasTBCCDEDarkPortal_Toy: // 295
                case ModifierTreeType.PlayerHasTBCCDEPathOfIllidan_Toy: // 296
                case ModifierTreeType.PlayerHasImpInABallToySubscriptionReward: // 297
                    return false;
                case ModifierTreeType.PlayerIsInAreaGroup: // 298
                    {
                        var areas = Global.DB2Mgr.GetAreasForGroup(reqValue);
                        AreaTableRecord area = CliDB.AreaTableStorage.LookupByKey(referencePlayer.GetAreaId());
                        if (area != null)
                            foreach (uint areaInGroup in areas)
                                if (areaInGroup == area.Id || areaInGroup == area.ParentAreaID)
                                    return true;
                        return false;
                    }
                case ModifierTreeType.TargetIsInAreaGroup: // 299
                    {
                        if (!unit)
                            return false;

                        var areas = Global.DB2Mgr.GetAreasForGroup(reqValue);
                        var area = CliDB.AreaTableStorage.LookupByKey(unit.GetAreaId());
                        if (area != null)
                            foreach (uint areaInGroup in areas)
                                if (areaInGroup == area.Id || areaInGroup == area.ParentAreaID)
                                    return true;
                        return false;
                    }
                case ModifierTreeType.PlayerIsInChromieTime: // 300
                    if (referencePlayer.m_activePlayerData.UiChromieTimeExpansionID != reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerIsInAnyChromieTime: // 301
                    if (referencePlayer.m_activePlayerData.UiChromieTimeExpansionID == 0)
                        return false;
                    break;
                case ModifierTreeType.ItemIsAzeriteArmor: // 302
                    if (Global.DB2Mgr.GetAzeriteEmpoweredItem((uint)miscValue1) == null)
                        return false;
                    break;
                case ModifierTreeType.PlayerHasRuneforgePower: // 303
                    {
                        int block = (int)reqValue / 32;
                        if (block >= referencePlayer.m_activePlayerData.RuneforgePowers.Size())
                            return false;

                        uint bit = reqValue % 32;
                        return (referencePlayer.m_activePlayerData.RuneforgePowers[block] & (1u << (int)bit)) != 0;
                    }
                case ModifierTreeType.PlayerInChromieTimeForScaling: // 304
                    if ((referencePlayer.m_playerData.CtrOptions._value.ContentTuningConditionMask & 1) == 0)
                        return false;
                    break;
                case ModifierTreeType.IsRaFRecruit: // 305
                    if (referencePlayer.GetSession().GetRecruiterId() == 0)
                        return false;
                    break;
                case ModifierTreeType.AllPlayersInGroupHaveAchievement: // 306
                    {
                        var group = referencePlayer.GetGroup();
                        if (group != null)
                        {
                            for (var itr = group.GetFirstMember(); itr != null; itr = itr.Next())
                                if (!itr.GetSource().HasAchieved(reqValue))
                                    return false;
                        }
                        else if (!referencePlayer.HasAchieved(reqValue))
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasSoulbindConduitRankEqualOrGreaterThan: // 307 NYI
                    return false;
                case ModifierTreeType.PlayerSpellShapeshiftFormCreatureDisplayInfoSelection: // 308
                    {
                        ShapeshiftFormModelData formModelData = Global.DB2Mgr.GetShapeshiftFormModelData(referencePlayer.GetRace(), referencePlayer.GetNativeSex(), (ShapeShiftForm)secondaryAsset);
                        if (formModelData == null)
                            return false;

                        uint formChoice = referencePlayer.GetCustomizationChoice(formModelData.OptionID);
                        var choiceIndex = formModelData.Choices.FindIndex(choice => { return choice.Id == formChoice; });
                        if (choiceIndex == -1)
                            return false;

                        if (reqValue != formModelData.Displays[choiceIndex].DisplayID)
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerSoulbindConduitCountAtRankEqualOrGreaterThan: // 309 NYI
                    return false;
                case ModifierTreeType.PlayerIsRestrictedAccount: // 310
                    return false;
                case ModifierTreeType.PlayerIsFlying: // 311
                    if (!referencePlayer.IsFlying())
                        return false;
                    break;
                case ModifierTreeType.PlayerScenarioIsLastStep: // 312
                    {
                        Scenario scenario = referencePlayer.GetScenario();
                        if (scenario == null)
                            return false;

                        if (scenario.GetStep() != scenario.GetLastStep())
                            return false;
                        break;
                    }
                case ModifierTreeType.PlayerHasWeeklyRewardsAvailable: // 313
                    if (referencePlayer.m_activePlayerData.WeeklyRewardsPeriodSinceOrigin == 0)
                        return false;
                    break;
                case ModifierTreeType.TargetCovenant: // 314
                    if (!unit || !unit.IsPlayer())
                        return false;
                    if (unit.ToPlayer().m_playerData.CovenantID != reqValue)
                        return false;
                    break;
                case ModifierTreeType.PlayerHasTBCCollectorsEdition: // 315
                case ModifierTreeType.PlayerHasWrathCollectorsEdition: // 316
                    return false;
                case ModifierTreeType.GarrisonTalentResearchedAndAtRankEqualOrGreaterThan: // 317 NYI
                case ModifierTreeType.CurrencySpentOnGarrisonTalentResearchEqualOrGreaterThan: // 318 NYI
                case ModifierTreeType.RenownCatchupActive: // 319 NYI
                case ModifierTreeType.RapidRenownCatchupActive: // 320 NYI
                case ModifierTreeType.PlayerMythicPlusRatingEqualOrGreaterThan: // 321 NYI
                case ModifierTreeType.PlayerMythicPlusRunCountInCurrentExpansionEqualOrGreaterThan: // 322 NYI
                    return false;
                default:
                    return false;
            }
            return true;
        }

        public virtual void SendAllData(Player receiver) { }
        public virtual void SendCriteriaUpdate(Criteria criteria, CriteriaProgress progress, TimeSpan timeElapsed, bool timedCompleted) { }
        public virtual void SendCriteriaProgressRemoved(uint criteriaId) { }

        public virtual void CompletedCriteriaTree(CriteriaTree tree, Player referencePlayer) { }
        public virtual void AfterCriteriaTreeUpdate(CriteriaTree tree, Player referencePlayer) { }

        public virtual void SendPacket(ServerPacket data) { }

        public virtual bool RequiredAchievementSatisfied(uint achievementId) { return false; }

        public virtual string GetOwnerInfo() { return ""; }
        public virtual List<Criteria> GetCriteriaByType(CriteriaTypes type, uint asset) { return null; }
    }

    public class CriteriaManager : Singleton<CriteriaManager>
    {
        Dictionary<uint, CriteriaDataSet> _criteriaDataMap = new();

        Dictionary<uint, CriteriaTree> _criteriaTrees = new();
        Dictionary<uint, Criteria> _criteria = new();
        Dictionary<uint, ModifierTreeNode> _criteriaModifiers = new();

        MultiMap<uint, CriteriaTree> _criteriaTreeByCriteria = new();

        // store criterias by type to speed up lookup
        MultiMap<CriteriaTypes, Criteria> _criteriasByType = new();
        MultiMap<uint, Criteria>[] _criteriasByAsset = new MultiMap<uint, Criteria>[(int)CriteriaTypes.TotalTypes];
        MultiMap<CriteriaTypes, Criteria> _guildCriteriasByType = new();
        MultiMap<CriteriaTypes, Criteria> _scenarioCriteriasByType = new();
        MultiMap<CriteriaTypes, Criteria> _questObjectiveCriteriasByType = new();

        MultiMap<CriteriaStartEvent, Criteria> _criteriasByTimedType = new();
        MultiMap<int, Criteria>[] _criteriasByFailEvent = new MultiMap<int, Criteria>[(int)CriteriaFailEvent.Max];

        CriteriaManager()
        {
            for (var i = 0; i < (int)CriteriaTypes.TotalTypes; ++i)
                _criteriasByAsset[i] = new MultiMap<uint, Criteria>();
        }

        public void LoadCriteriaModifiersTree()
        {
            uint oldMSTime = Time.GetMSTime();

            if (CliDB.ModifierTreeStorage.Empty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 criteria modifiers.");
                return;
            }

            // Load modifier tree nodes
            foreach (var tree in CliDB.ModifierTreeStorage.Values)
            {
                ModifierTreeNode node = new();
                node.Entry = tree;
                _criteriaModifiers[node.Entry.Id] = node;
            }

            // Build tree
            foreach (var treeNode in _criteriaModifiers.Values)
            {
                ModifierTreeNode parentNode = _criteriaModifiers.LookupByKey(treeNode.Entry.Parent);
                if (parentNode != null)
                    parentNode.Children.Add(treeNode);
            }

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} criteria modifiers in {1} ms", _criteriaModifiers.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        T GetEntry<T>(Dictionary<uint, T> map, CriteriaTreeRecord tree) where T : new()
        {
            CriteriaTreeRecord cur = tree;
            var obj = map.LookupByKey(tree.Id);
            while (obj == null)
            {
                if (cur.Parent == 0)
                    break;

                cur = CliDB.CriteriaTreeStorage.LookupByKey(cur.Parent);
                if (cur == null)
                    break;

                obj = map.LookupByKey(cur.Id);
            }

            if (obj == null)
                return default;

            return obj;
        }

        public void LoadCriteriaList()
        {
            uint oldMSTime = Time.GetMSTime();

            Dictionary<uint /*criteriaTreeID*/, AchievementRecord> achievementCriteriaTreeIds = new();
            foreach (AchievementRecord achievement in CliDB.AchievementStorage.Values)
                if (achievement.CriteriaTree != 0)
                    achievementCriteriaTreeIds[achievement.CriteriaTree] = achievement;

            Dictionary<uint, ScenarioStepRecord> scenarioCriteriaTreeIds = new();
            foreach (ScenarioStepRecord scenarioStep in CliDB.ScenarioStepStorage.Values)
            {
                if (scenarioStep.CriteriaTreeId != 0)
                    scenarioCriteriaTreeIds[scenarioStep.CriteriaTreeId] = scenarioStep;
            }

            Dictionary<uint /*criteriaTreeID*/, QuestObjective> questObjectiveCriteriaTreeIds = new();
            foreach (var pair in Global.ObjectMgr.GetQuestTemplates())
            {
                foreach (QuestObjective objective in pair.Value.Objectives)
                {
                    if (objective.Type != QuestObjectiveType.CriteriaTree)
                        continue;

                    if (objective.ObjectID != 0)
                        questObjectiveCriteriaTreeIds[(uint)objective.ObjectID] = objective;
                }
            }

            // Load criteria tree nodes
            foreach (CriteriaTreeRecord tree in CliDB.CriteriaTreeStorage.Values)
            {
                // Find linked achievement
                AchievementRecord achievement = GetEntry(achievementCriteriaTreeIds, tree);
                ScenarioStepRecord scenarioStep = GetEntry(scenarioCriteriaTreeIds, tree);
                QuestObjective questObjective = GetEntry(questObjectiveCriteriaTreeIds, tree);
                if (achievement == null && scenarioStep == null && questObjective == null)
                    continue;

                CriteriaTree criteriaTree = new();
                criteriaTree.Id = tree.Id;
                criteriaTree.Achievement = achievement;
                criteriaTree.ScenarioStep = scenarioStep;
                criteriaTree.QuestObjective = questObjective;
                criteriaTree.Entry = tree;

                _criteriaTrees[criteriaTree.Entry.Id] = criteriaTree;
            }

            // Build tree
            foreach (var pair in _criteriaTrees)
            {
                CriteriaTree parent = _criteriaTrees.LookupByKey(pair.Value.Entry.Parent);
                if (parent != null)
                    parent.Children.Add(pair.Value);

                if (CliDB.CriteriaStorage.HasRecord(pair.Value.Entry.CriteriaID))
                    _criteriaTreeByCriteria.Add(pair.Value.Entry.CriteriaID, pair.Value);
            }

            for (var i = 0; i < (int)CriteriaFailEvent.Max; ++i)
                _criteriasByFailEvent[i] = new MultiMap<int, Criteria>();

            // Load criteria
            uint criterias = 0;
            uint guildCriterias = 0;
            uint scenarioCriterias = 0;
            uint questObjectiveCriterias = 0;
            foreach (CriteriaRecord criteriaEntry in CliDB.CriteriaStorage.Values)
            {
                Cypher.Assert(criteriaEntry.Type < CriteriaTypes.TotalTypes,
                    $"CRITERIA_TYPE_TOTAL must be greater than or equal to {criteriaEntry.Type + 1} but is currently equal to {CriteriaTypes.TotalTypes}");
                Cypher.Assert(criteriaEntry.StartEvent < (byte)CriteriaStartEvent.Max, $"CRITERIA_TYPE_TOTAL must be greater than or equal to {criteriaEntry.StartEvent + 1} but is currently equal to {CriteriaStartEvent.Max}");
                Cypher.Assert(criteriaEntry.FailEvent < (byte)CriteriaFailEvent.Max, $"CRITERIA_CONDITION_MAX must be greater than or equal to {criteriaEntry.FailEvent + 1} but is currently equal to {CriteriaFailEvent.Max}");

                var treeList = _criteriaTreeByCriteria.LookupByKey(criteriaEntry.Id);
                if (treeList.Empty())
                    continue;

                Criteria criteria = new();
                criteria.Id = criteriaEntry.Id;
                criteria.Entry = criteriaEntry;
                criteria.Modifier = _criteriaModifiers.LookupByKey(criteriaEntry.ModifierTreeId);

                _criteria[criteria.Id] = criteria;

                foreach (CriteriaTree tree in treeList)
                {
                    tree.Criteria = criteria;

                    AchievementRecord achievement = tree.Achievement;
                    if (achievement != null)
                    {
                        if (achievement.Flags.HasAnyFlag(AchievementFlags.Guild))
                            criteria.FlagsCu |= CriteriaFlagsCu.Guild;
                        else if (achievement.Flags.HasAnyFlag(AchievementFlags.Account))
                            criteria.FlagsCu |= CriteriaFlagsCu.Account;
                        else
                            criteria.FlagsCu |= CriteriaFlagsCu.Player;
                    }
                    else if (tree.ScenarioStep != null)
                        criteria.FlagsCu |= CriteriaFlagsCu.Scenario;
                    else if (tree.QuestObjective != null)
                        criteria.FlagsCu |= CriteriaFlagsCu.QuestObjective;
                }

                if (criteria.FlagsCu.HasAnyFlag(CriteriaFlagsCu.Player | CriteriaFlagsCu.Account))
                {
                    ++criterias;
                    _criteriasByType.Add(criteriaEntry.Type, criteria);
                    if (IsCriteriaTypeStoredByAsset(criteriaEntry.Type))
                    {
                        if (criteriaEntry.Type != CriteriaTypes.ExploreArea)
                            _criteriasByAsset[(int)criteriaEntry.Type].Add(criteriaEntry.Asset, criteria);
                        else
                        {
                            var worldOverlayEntry = CliDB.WorldMapOverlayStorage.LookupByKey(criteriaEntry.Asset);
                            if (worldOverlayEntry == null)
                                break;

                            for (byte j = 0; j < SharedConst.MaxWorldMapOverlayArea; ++j)
                            {
                                if (worldOverlayEntry.AreaID[j] != 0)
                                {
                                    bool valid = true;
                                    for (byte i = 0; i < j; ++i)
                                        if (worldOverlayEntry.AreaID[j] == worldOverlayEntry.AreaID[i])
                                            valid = false;
                                    if (valid)
                                        _criteriasByAsset[(int)criteriaEntry.Type].Add(worldOverlayEntry.AreaID[j], criteria);
                                }
                            }
                        }
                    }
                }

                if (criteria.FlagsCu.HasAnyFlag(CriteriaFlagsCu.Guild))
                {
                    ++guildCriterias;
                    _guildCriteriasByType.Add(criteriaEntry.Type, criteria);
                }

                if (criteria.FlagsCu.HasAnyFlag(CriteriaFlagsCu.Scenario))
                {
                    ++scenarioCriterias;
                    _scenarioCriteriasByType.Add(criteriaEntry.Type, criteria);
                }

                if (criteria.FlagsCu.HasAnyFlag(CriteriaFlagsCu.QuestObjective))
                {
                    ++questObjectiveCriterias;
                    _questObjectiveCriteriasByType.Add(criteriaEntry.Type, criteria);
                }

                if (criteriaEntry.StartTimer != 0)
                    _criteriasByTimedType.Add((CriteriaStartEvent)criteriaEntry.StartEvent, criteria);

                if (criteriaEntry.FailEvent != 0)
                    _criteriasByFailEvent[criteriaEntry.FailEvent].Add((int)criteriaEntry.FailAsset, criteria);
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {criterias} criteria, {guildCriterias} guild criteria, {scenarioCriterias} scenario criteria and {questObjectiveCriterias} quest objective criteria in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadCriteriaData()
        {
            uint oldMSTime = Time.GetMSTime();

            _criteriaDataMap.Clear();                              // need for reload case

            SQLResult result = DB.World.Query("SELECT criteria_id, type, value1, value2, ScriptName FROM criteria_data");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 additional criteria data. DB table `criteria_data` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint criteria_id = result.Read<uint>(0);

                Criteria criteria = GetCriteria(criteria_id);
                if (criteria == null)
                {
                    Log.outError(LogFilter.Sql, "Table `criteria_data` contains data for non-existing criteria (Entry: {0}). Ignored.", criteria_id);
                    continue;
                }

                CriteriaDataType dataType = (CriteriaDataType)result.Read<byte>(1);
                string scriptName = result.Read<string>(4);
                uint scriptId = 0;
                if (!scriptName.IsEmpty())
                {
                    if (dataType != CriteriaDataType.Script)
                        Log.outError(LogFilter.Sql, "Table `criteria_data` contains a ScriptName for non-scripted data type (Entry: {0}, type {1}), useless data.", criteria_id, dataType);
                    else
                        scriptId = Global.ObjectMgr.GetScriptId(scriptName);
                }

                CriteriaData data = new(dataType, result.Read<uint>(2), result.Read<uint>(3), scriptId);

                if (!data.IsValid(criteria))
                    continue;

                // this will allocate empty data set storage
                CriteriaDataSet dataSet = new();
                dataSet.SetCriteriaId(criteria_id);

                // add real data only for not NONE data types
                if (data.DataType != CriteriaDataType.None)
                    dataSet.Add(data);

                _criteriaDataMap[criteria_id] = dataSet;
                // counting data by and data types
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} additional criteria data in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        public CriteriaTree GetCriteriaTree(uint criteriaTreeId)
        {
            return _criteriaTrees.LookupByKey(criteriaTreeId);
        }

        public Criteria GetCriteria(uint criteriaId)
        {
            return _criteria.LookupByKey(criteriaId);
        }

        public ModifierTreeNode GetModifierTree(uint modifierTreeId)
        {
            return _criteriaModifiers.LookupByKey(modifierTreeId);
        }

        bool IsCriteriaTypeStoredByAsset(CriteriaTypes type)
        {
            switch (type)
            {
                case CriteriaTypes.KillCreature:
                case CriteriaTypes.WinBg:
                case CriteriaTypes.ReachSkillLevel:
                case CriteriaTypes.CompleteAchievement:
                case CriteriaTypes.CompleteQuestsInZone:
                case CriteriaTypes.CompleteBattleground:
                case CriteriaTypes.KilledByCreature:
                case CriteriaTypes.CompleteQuest:
                case CriteriaTypes.BeSpellTarget:
                case CriteriaTypes.CastSpell:
                case CriteriaTypes.BgObjectiveCapture:
                case CriteriaTypes.HonorableKillAtArea:
                case CriteriaTypes.LearnSpell:
                case CriteriaTypes.OwnItem:
                case CriteriaTypes.LearnSkillLevel:
                case CriteriaTypes.UseItem:
                case CriteriaTypes.LootItem:
                case CriteriaTypes.ExploreArea:
                case CriteriaTypes.GainReputation:
                case CriteriaTypes.EquipEpicItem:
                case CriteriaTypes.HkClass:
                case CriteriaTypes.HkRace:
                case CriteriaTypes.DoEmote:
                case CriteriaTypes.EquipItem:
                case CriteriaTypes.UseGameobject:
                case CriteriaTypes.BeSpellTarget2:
                case CriteriaTypes.FishInGameobject:
                case CriteriaTypes.LearnSkilllineSpells:
                case CriteriaTypes.LootType:
                case CriteriaTypes.CastSpell2:
                case CriteriaTypes.LearnSkillLine:
                    return true;
                default:
                    return false;
            }
        }

        public List<Criteria> GetPlayerCriteriaByType(CriteriaTypes type, uint asset)
        {
            if (asset != 0 && IsCriteriaTypeStoredByAsset(type))
            {
                if (_criteriasByAsset[(int)type].ContainsKey(asset))
                    return _criteriasByAsset[(int)type][asset];
            }

            return _criteriasByType.LookupByKey(type);
        }

        public List<Criteria> GetGuildCriteriaByType(CriteriaTypes type)
        {
            return _guildCriteriasByType.LookupByKey(type);
        }

        public List<Criteria> GetScenarioCriteriaByType(CriteriaTypes type)
        {
            return _scenarioCriteriasByType.LookupByKey(type);
        }

        public List<Criteria> GetQuestObjectiveCriteriaByType(CriteriaTypes type)
        {
            return _questObjectiveCriteriasByType[type];
        }

        public List<CriteriaTree> GetCriteriaTreesByCriteria(uint criteriaId)
        {
            return _criteriaTreeByCriteria.LookupByKey(criteriaId);
        }

        public List<Criteria> GetTimedCriteriaByType(CriteriaStartEvent startEvent)
        {
            return _criteriasByTimedType.LookupByKey(startEvent);
        }

        public List<Criteria> GetCriteriaByFailEvent(CriteriaFailEvent failEvent, int asset)
        {
            return _criteriasByFailEvent[(int)failEvent].LookupByKey(asset);
        }

        public CriteriaDataSet GetCriteriaDataSet(Criteria criteria)
        {
            return _criteriaDataMap.LookupByKey(criteria.Id);
        }

        public static bool IsGroupCriteriaType(CriteriaTypes type)
        {
            switch (type)
            {
                case CriteriaTypes.KillCreature:
                case CriteriaTypes.WinBg:
                case CriteriaTypes.BeSpellTarget:         // NYI
                case CriteriaTypes.WinRatedArena:
                case CriteriaTypes.BeSpellTarget2:        // NYI
                case CriteriaTypes.WinRatedBattleground:  // NYI
                    return true;
                default:
                    break;
            }

            return false;
        }

        public static void WalkCriteriaTree(CriteriaTree tree, Action<CriteriaTree> func)
        {
            foreach (CriteriaTree node in tree.Children)
                WalkCriteriaTree(node, func);

            func(tree);
        }
    }

    public class ModifierTreeNode
    {
        public ModifierTreeRecord Entry;
        public List<ModifierTreeNode> Children = new();
    }

    public class Criteria
    {
        public uint Id;
        public CriteriaRecord Entry;
        public ModifierTreeNode Modifier;
        public CriteriaFlagsCu FlagsCu;
    }

    public class CriteriaTree
    {
        public uint Id;
        public CriteriaTreeRecord Entry;
        public AchievementRecord Achievement;
        public ScenarioStepRecord ScenarioStep;
        public QuestObjective QuestObjective;
        public Criteria Criteria;
        public List<CriteriaTree> Children = new();
    }

    public class CriteriaProgress
    {
        public ulong Counter;
        public long Date;                                            // latest update time.
        public ObjectGuid PlayerGUID;                               // GUID of the player that completed this criteria (guild achievements)
        public bool Changed;
    }

    [StructLayout(LayoutKind.Explicit)]
    public class CriteriaData
    { 
        [FieldOffset(0)]
        public CriteriaDataType DataType;

        [FieldOffset(4)]
        public CreatureStruct Creature;

        [FieldOffset(4)]
        public ClassRaceStruct ClassRace;

        [FieldOffset(4)]
        public HealthStruct Health;

        [FieldOffset(4)]
        public AuraStruct Aura;

        [FieldOffset(4)]
        public ValueStruct Value;

        [FieldOffset(4)]
        public LevelStruct Level;

        [FieldOffset(4)]
        public GenderStruct Gender;

        [FieldOffset(4)]
        public MapPlayersStruct MapPlayers;

        [FieldOffset(4)]
        public TeamStruct TeamId;

        [FieldOffset(4)]
        public DrunkStruct Drunk;

        [FieldOffset(4)]
        public HolidayStruct Holiday;

        [FieldOffset(4)]
        public BgLossTeamScoreStruct BattlegroundScore;

        [FieldOffset(4)]
        public EquippedItemStruct EquippedItem;

        [FieldOffset(4)]
        public MapIdStruct MapId;

        [FieldOffset(4)]
        public KnownTitleStruct KnownTitle;

        [FieldOffset(4)]
        public GameEventStruct GameEvent;

        [FieldOffset(4)]
        public ItemQualityStruct itemQuality;

        [FieldOffset(4)]
        public RawStruct Raw;

        [FieldOffset(12)]
        public uint ScriptId;

        public CriteriaData()
        {
            DataType = CriteriaDataType.None;

            Raw.Value1 = 0;
            Raw.Value2 = 0;
            ScriptId = 0;
        }

        public CriteriaData(CriteriaDataType _dataType, uint _value1, uint _value2, uint _scriptId)
        {
            DataType = _dataType;

            Raw.Value1 = _value1;
            Raw.Value2 = _value2;
            ScriptId = _scriptId;
        }

        public bool IsValid(Criteria criteria)
        {
            if (DataType >= CriteriaDataType.Max)
            {
                Log.outError(LogFilter.Sql, "Table `criteria_data` for criteria (Entry: {0}) has wrong data type ({1}), ignored.", criteria.Id, DataType);
                return false;
            }

            switch (criteria.Entry.Type)
            {
                case CriteriaTypes.KillCreature:
                case CriteriaTypes.KillCreatureType:
                case CriteriaTypes.WinBg:
                case CriteriaTypes.FallWithoutDying:
                case CriteriaTypes.CompleteQuest:          // only hardcoded list
                case CriteriaTypes.CastSpell:
                case CriteriaTypes.WinRatedArena:
                case CriteriaTypes.DoEmote:
                case CriteriaTypes.SpecialPvpKill:
                case CriteriaTypes.WinDuel:
                case CriteriaTypes.LootType:
                case CriteriaTypes.CastSpell2:
                case CriteriaTypes.BeSpellTarget:
                case CriteriaTypes.BeSpellTarget2:
                case CriteriaTypes.EquipEpicItem:
                case CriteriaTypes.RollNeedOnLoot:
                case CriteriaTypes.RollGreedOnLoot:
                case CriteriaTypes.BgObjectiveCapture:
                case CriteriaTypes.HonorableKill:
                case CriteriaTypes.CompleteDailyQuest:    // only Children's Week achievements
                case CriteriaTypes.UseItem:                // only Children's Week achievements
                case CriteriaTypes.GetKillingBlows:
                case CriteriaTypes.ReachLevel:
                case CriteriaTypes.OnLogin:
                case CriteriaTypes.LootEpicItem:
                case CriteriaTypes.ReceiveEpicItem:
                    break;
                default:
                    if (DataType != CriteriaDataType.Script)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` has data for non-supported criteria type (Entry: {0} Type: {1}), ignored.", criteria.Id, (CriteriaTypes)criteria.Entry.Type);
                        return false;
                    }
                    break;
            }

            switch (DataType)
            {
                case CriteriaDataType.None:
                case CriteriaDataType.InstanceScript:
                    return true;
                case CriteriaDataType.TCreature:
                    if (Creature.Id == 0 || Global.ObjectMgr.GetCreatureTemplate(Creature.Id) == null)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_CREATURE ({2}) has non-existing creature id in value1 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, Creature.Id);
                        return false;
                    }
                    return true;
                case CriteriaDataType.TPlayerClassRace:
                    if (ClassRace.ClassId == 0 && ClassRace.RaceId == 0)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_T_PLAYER_CLASS_RACE ({2}) must not have 0 in either value field, ignored.",
                            criteria.Id, criteria.Entry.Type, DataType);
                        return false;
                    }
                    if (ClassRace.ClassId != 0 && ((1 << (int)(ClassRace.ClassId - 1)) & (int)Class.ClassMaskAllPlayable) == 0)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_T_PLAYER_CLASS_RACE ({2}) has non-existing class in value1 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, ClassRace.ClassId);
                        return false;
                    }
                    if (ClassRace.RaceId != 0 && (SharedConst.GetMaskForRace((Race)ClassRace.RaceId) & (long)SharedConst.RaceMaskAllPlayable) == 0)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_T_PLAYER_CLASS_RACE ({2}) has non-existing race in value2 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, ClassRace.RaceId);
                        return false;
                    }
                    return true;
                case CriteriaDataType.TPlayerLessHealth:
                    if (Health.Percent < 1 || Health.Percent > 100)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_PLAYER_LESS_HEALTH ({2}) has wrong percent value in value1 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, Health.Percent);
                        return false;
                    }
                    return true;
                case CriteriaDataType.SAura:
                case CriteriaDataType.TAura:
                    {
                        SpellInfo spellEntry = Global.SpellMgr.GetSpellInfo(Aura.SpellId, Difficulty.None);
                        if (spellEntry == null)
                        {
                            Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type {2} has wrong spell id in value1 ({3}), ignored.",
                                criteria.Id, criteria.Entry.Type, DataType, Aura.SpellId);
                            return false;
                        }
                        SpellEffectInfo effect = spellEntry.GetEffect(Aura.EffectIndex);
                        if (effect == null)
                        {
                            Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type {2} has wrong spell effect index in value2 ({3}), ignored.",
                                criteria.Id, criteria.Entry.Type, DataType, Aura.EffectIndex);
                            return false;
                        }
                        if (effect.ApplyAuraName == 0)
                        {
                            Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type {2} has non-aura spell effect (ID: {3} Effect: {4}), ignores.",
                                criteria.Id, criteria.Entry.Type, DataType, Aura.SpellId, Aura.EffectIndex);
                            return false;
                        }
                        return true;
                    }
                case CriteriaDataType.Value:
                    if (Value.ComparisonType >= (int)ComparisionType.Max)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_VALUE ({2}) has wrong ComparisionType in value2 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, Value.ComparisonType);
                        return false;
                    }
                    return true;
                case CriteriaDataType.TLevel:
                    if (Level.Min > SharedConst.GTMaxLevel)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_T_LEVEL ({2}) has wrong minlevel in value1 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, Level.Min);
                        return false;
                    }
                    return true;
                case CriteriaDataType.TGender:
                    if (Gender.Gender > (int)Framework.Constants.Gender.None)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_T_GENDER ({2}) has wrong gender in value1 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, Gender.Gender);
                        return false;
                    }
                    return true;
                case CriteriaDataType.Script:
                    if (ScriptId == 0)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_SCRIPT ({2}) does not have ScriptName set, ignored.",
                            criteria.Id, criteria.Entry.Type, DataType);
                        return false;
                    }
                    return true;
                case CriteriaDataType.MapPlayerCount:
                    if (MapPlayers.MaxCount <= 0)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_MAP_PLAYER_COUNT ({2}) has wrong max players count in value1 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, MapPlayers.MaxCount);
                        return false;
                    }
                    return true;
                case CriteriaDataType.TTeam:
                    if (TeamId.Team != (int)Team.Alliance && TeamId.Team != (int)Team.Horde)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_T_TEAM ({2}) has unknown team in value1 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, TeamId.Team);
                        return false;
                    }
                    return true;
                case CriteriaDataType.SDrunk:
                    if (Drunk.State >= 4)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_S_DRUNK ({2}) has unknown drunken state in value1 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, Drunk.State);
                        return false;
                    }
                    return true;
                case CriteriaDataType.Holiday:
                    if (!CliDB.HolidaysStorage.ContainsKey(Holiday.Id))
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data`(Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_HOLIDAY ({2}) has unknown holiday in value1 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, Holiday.Id);
                        return false;
                    }
                    return true;
                case CriteriaDataType.GameEvent:
                    {
                        var events = Global.GameEventMgr.GetEventMap();
                        if (GameEvent.Id < 1 || GameEvent.Id >= events.Length)
                        {
                            Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_GAME_EVENT ({2}) has unknown game_event in value1 ({3}), ignored.",
                                criteria.Id, criteria.Entry.Type, DataType, GameEvent.Id);
                            return false;
                        }
                        return true;
                    }
                case CriteriaDataType.BgLossTeamScore:
                    return true;                                    // not check correctness node indexes
                case CriteriaDataType.SEquippedItem:
                    if (EquippedItem.ItemQuality >= (uint)ItemQuality.Max)
                    {
                        Log.outError(LogFilter.Sql, "Table `achievement_criteria_requirement` (Entry: {0} Type: {1}) for requirement ACHIEVEMENT_CRITERIA_REQUIRE_S_EQUIPED_ITEM ({2}) has unknown quality state in value1 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, EquippedItem.ItemQuality);
                        return false;
                    }
                    return true;
                case CriteriaDataType.MapId:
                    if (!CliDB.MapStorage.ContainsKey(MapId.Id))
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_MAP_ID ({2}) contains an unknown map entry in value1 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, MapId.Id);
                    }
                    return true;
                case CriteriaDataType.SPlayerClassRace:
                    if (ClassRace.ClassId == 0 && ClassRace.RaceId == 0)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_S_PLAYER_CLASS_RACE ({2}) must not have 0 in either value field, ignored.",
                            criteria.Id, criteria.Entry.Type, DataType);
                        return false;
                    }
                    if (ClassRace.ClassId != 0 && ((1 << (int)(ClassRace.ClassId - 1)) & (int)Class.ClassMaskAllPlayable) == 0)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_S_PLAYER_CLASS_RACE ({2}) has non-existing class in value1 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, ClassRace.ClassId);
                        return false;
                    }
                    if (ClassRace.RaceId != 0 && ((ulong)SharedConst.GetMaskForRace((Race)ClassRace.RaceId) & SharedConst.RaceMaskAllPlayable) == 0)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_S_PLAYER_CLASS_RACE ({2}) has non-existing race in value2 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, ClassRace.RaceId);
                        return false;
                    }
                    return true;
                case CriteriaDataType.SKnownTitle:
                    if (!CliDB.CharTitlesStorage.ContainsKey(KnownTitle.Id))
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_S_KNOWN_TITLE ({2}) contains an unknown title_id in value1 ({3}), ignore.",
                            criteria.Id, criteria.Entry.Type, DataType, KnownTitle.Id);
                        return false;
                    }
                    return true;
                case CriteriaDataType.SItemQuality:
                    if (itemQuality.Quality >= (uint)ItemQuality.Max)
                    {
                        Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) for data type CRITERIA_DATA_TYPE_S_ITEM_QUALITY ({2}) contains an unknown quality state value in value1 ({3}), ignored.",
                            criteria.Id, criteria.Entry.Type, DataType, itemQuality.Quality);
                        return false;
                    }
                    return true;
                default:
                    Log.outError(LogFilter.Sql, "Table `criteria_data` (Entry: {0} Type: {1}) contains data of a non-supported data type ({2}), ignored.", criteria.Id, criteria.Entry.Type, DataType);
                    return false;
            }
        }

        public bool Meets(uint criteriaId, Player source, Unit target, uint miscValue1 = 0, uint miscValue2 = 0)
        {
            switch (DataType)
            {
                case CriteriaDataType.None:
                    return true;
                case CriteriaDataType.TCreature:
                    if (target == null || !target.IsTypeId(TypeId.Unit))
                        return false;
                    return target.GetEntry() == Creature.Id;
                case CriteriaDataType.TPlayerClassRace:
                    if (target == null || !target.IsTypeId(TypeId.Player))
                        return false;
                    if (ClassRace.ClassId != 0 && ClassRace.ClassId != (uint)target.ToPlayer().GetClass())
                        return false;
                    if (ClassRace.RaceId != 0 && ClassRace.RaceId != (uint)target.ToPlayer().GetRace())
                        return false;
                    return true;
                case CriteriaDataType.SPlayerClassRace:
                    if (source == null || !source.IsTypeId(TypeId.Player))
                        return false;
                    if (ClassRace.ClassId != 0 && ClassRace.ClassId != (uint)source.ToPlayer().GetClass())
                        return false;
                    if (ClassRace.RaceId != 0 && ClassRace.RaceId != (uint)source.ToPlayer().GetRace())
                        return false;
                    return true;
                case CriteriaDataType.TPlayerLessHealth:
                    if (target == null || !target.IsTypeId(TypeId.Player))
                        return false;
                    return !target.HealthAbovePct((int)Health.Percent);
                case CriteriaDataType.SAura:
                    return source.HasAuraEffect(Aura.SpellId, (byte)Aura.EffectIndex);
                case CriteriaDataType.TAura:
                    return target != null && target.HasAuraEffect(Aura.SpellId, (byte)Aura.EffectIndex);
                case CriteriaDataType.Value:
                    return MathFunctions.CompareValues((ComparisionType)Value.ComparisonType, miscValue1, Value.Value);
                case CriteriaDataType.TLevel:
                    if (target == null)
                        return false;
                    return target.GetLevelForTarget(source) >= Level.Min;
                case CriteriaDataType.TGender:
                    if (target == null)
                        return false;
                    return (uint)target.GetGender() == Gender.Gender;
                case CriteriaDataType.Script:
                    return Global.ScriptMgr.OnCriteriaCheck(ScriptId, source, target);
                case CriteriaDataType.MapPlayerCount:
                    return source.GetMap().GetPlayersCountExceptGMs() <= MapPlayers.MaxCount;
                case CriteriaDataType.TTeam:
                    if (target == null || !target.IsTypeId(TypeId.Player))
                        return false;
                    return (uint)target.ToPlayer().GetTeam() == TeamId.Team;
                case CriteriaDataType.SDrunk:
                    return Player.GetDrunkenstateByValue(source.GetDrunkValue()) >= (DrunkenState)Drunk.State;
                case CriteriaDataType.Holiday:
                    return Global.GameEventMgr.IsHolidayActive((HolidayIds)Holiday.Id);
                case CriteriaDataType.GameEvent:
                    return Global.GameEventMgr.IsEventActive((ushort)GameEvent.Id);
                case CriteriaDataType.BgLossTeamScore:
                    {
                        Battleground bg = source.GetBattleground();
                        if (!bg)
                            return false;

                        int score = (int)bg.GetTeamScore(source.GetTeam() == Team.Alliance ? Framework.Constants.TeamId.Horde : Framework.Constants.TeamId.Alliance);
                        return score >= BattlegroundScore.Min && score <= BattlegroundScore.Max;
                    }
                case CriteriaDataType.InstanceScript:
                    {
                        if (!source.IsInWorld)
                            return false;
                        Map map = source.GetMap();
                        if (!map.IsDungeon())
                        {
                            Log.outError(LogFilter.Achievement, "Achievement system call AchievementCriteriaDataType.InstanceScript ({0}) for achievement criteria {1} for non-dungeon/non-raid map {2}",
                                CriteriaDataType.InstanceScript, criteriaId, map.GetId());
                            return false;
                        }
                        InstanceScript instance = ((InstanceMap)map).GetInstanceScript();
                        if (instance == null)
                        {
                            Log.outError(LogFilter.Achievement, "Achievement system call criteria_data_INSTANCE_SCRIPT ({0}) for achievement criteria {1} for map {2} but map does not have a instance script",
                                CriteriaDataType.InstanceScript, criteriaId, map.GetId());
                            return false;
                        }
                        return instance.CheckAchievementCriteriaMeet(criteriaId, source, target, miscValue1);
                    }
                case CriteriaDataType.SEquippedItem:
                    {
                        Criteria entry = Global.CriteriaMgr.GetCriteria(criteriaId);

                        uint itemId = entry.Entry.Type == CriteriaTypes.EquipEpicItem ? miscValue2 : miscValue1;
                        ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(itemId);
                        if (itemTemplate == null)
                            return false;
                        return itemTemplate.GetBaseItemLevel() >= EquippedItem.ItemLevel && (uint)itemTemplate.GetQuality() >= EquippedItem.ItemQuality;
                    }
                case CriteriaDataType.MapId:
                    return source.GetMapId() == MapId.Id;
                case CriteriaDataType.SKnownTitle:
                    {
                        CharTitlesRecord titleInfo = CliDB.CharTitlesStorage.LookupByKey(KnownTitle.Id);
                        if (titleInfo != null)
                            return source && source.HasTitle(titleInfo.MaskID);

                        return false;
                    }
                case CriteriaDataType.SItemQuality:
                    {
                        ItemTemplate pProto = Global.ObjectMgr.GetItemTemplate(miscValue1);
                        if (pProto == null)
                            return false;
                        return (uint)pProto.GetQuality() == itemQuality.Quality;
                    }
                default:
                    break;
            }
            return false;
        }

        #region Structs
        // criteria_data_TYPE_NONE              = 0 (no data)
        // criteria_data_TYPE_T_CREATURE        = 1
        public struct CreatureStruct
        {
            public uint Id;
        }
        // criteria_data_TYPE_T_PLAYER_CLASS_RACE = 2
        // criteria_data_TYPE_S_PLAYER_CLASS_RACE = 21
        public struct ClassRaceStruct
        {
            public uint ClassId;
            public uint RaceId;
        }
        // criteria_data_TYPE_T_PLAYER_LESS_HEALTH = 3
        public struct HealthStruct
        {
            public uint Percent;
        }
        // criteria_data_TYPE_S_AURA            = 5
        // criteria_data_TYPE_T_AURA            = 7
        public struct AuraStruct
        {
            public uint SpellId;
            public uint EffectIndex;
        }
        // criteria_data_TYPE_VALUE             = 8
        public struct ValueStruct
        {
            public uint Value;
            public uint ComparisonType;
        }
        // criteria_data_TYPE_T_LEVEL           = 9
        public struct LevelStruct
        {
            public uint Min;
        }
        // criteria_data_TYPE_T_GENDER          = 10
        public struct GenderStruct
        {
            public uint Gender;
        }
        // criteria_data_TYPE_SCRIPT            = 11 (no data)
        // criteria_data_TYPE_MAP_PLAYER_COUNT  = 13
        public struct MapPlayersStruct
        {
            public uint MaxCount;
        }
        // criteria_data_TYPE_T_TEAM            = 14
        public struct TeamStruct
        {
            public uint Team;
        }
        // criteria_data_TYPE_S_DRUNK           = 15
        public struct DrunkStruct
        {
            public uint State;
        }
        // criteria_data_TYPE_HOLIDAY           = 16
        public struct HolidayStruct
        {
            public uint Id;
        }
        // criteria_data_TYPE_BG_LOSS_TEAM_SCORE= 17
        public struct BgLossTeamScoreStruct
        {
            public uint Min;
            public uint Max;
        }
        // criteria_data_INSTANCE_SCRIPT        = 18 (no data)
        // criteria_data_TYPE_S_EQUIPED_ITEM    = 19
        public struct EquippedItemStruct
        {
            public uint ItemLevel;
            public uint ItemQuality;
        }
        // criteria_data_TYPE_MAP_ID            = 20
        public struct MapIdStruct
        {
            public uint Id;
        }
        // criteria_data_TYPE_KNOWN_TITLE       = 23
        public struct KnownTitleStruct
        {
            public uint Id;
        }
        // CRITERIA_DATA_TYPE_S_ITEM_QUALITY    = 24
        public struct ItemQualityStruct
        {
            public uint Quality;
        }
        // criteria_data_TYPE_GAME_EVENT           = 25
        public struct GameEventStruct
        {
            public uint Id;
        }
        // raw
        public struct RawStruct
        {
            public uint Value1;
            public uint Value2;
        }
        #endregion
    }

    public class CriteriaDataSet
    {   
        uint _criteriaId;
        List<CriteriaData> _storage = new();

        public void Add(CriteriaData data) { _storage.Add(data); }

        public bool Meets(Player source, Unit target, uint miscValue = 0, uint miscValue2 = 0)
        {
            foreach (var data in _storage)
                if (!data.Meets(_criteriaId, source, target, miscValue, miscValue2))
                    return false;

            return true;
        }

        public void SetCriteriaId(uint id) { _criteriaId = id; }
    }
}
