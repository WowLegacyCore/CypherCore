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
using Game.Conditions;
using Game.DataStorage;
using Game.Entities;
using Game.Groups;
using Game.Loots;
using Game.Spells;
using System;
using System.Collections.Generic;
using Framework.IO;

namespace Game
{
    public sealed class ConditionManager : Singleton<ConditionManager>
    {
        ConditionManager() { }

        public GridMapTypeMask GetSearcherTypeMaskForConditionList(List<Condition> conditions)
        {
            if (conditions.Empty())
                return GridMapTypeMask.All;
            //     groupId, typeMask
            Dictionary<uint, GridMapTypeMask> elseGroupSearcherTypeMasks = new();
            foreach (var i in conditions)
            {
                // no point of having not loaded conditions in list
                Cypher.Assert(i.IsLoaded(), "ConditionMgr.GetSearcherTypeMaskForConditionList - not yet loaded condition found in list");
                // group not filled yet, fill with widest mask possible
                if (!elseGroupSearcherTypeMasks.ContainsKey(i.ElseGroup))
                    elseGroupSearcherTypeMasks[i.ElseGroup] = GridMapTypeMask.All;
                // no point of checking anymore, empty mask
                else if (elseGroupSearcherTypeMasks[i.ElseGroup] == 0)
                    continue;

                if (i.ReferenceId != 0) // handle reference
                {
                    var refe = ConditionReferenceStore.LookupByKey(i.ReferenceId);
                    Cypher.Assert(refe.Empty(), "ConditionMgr.GetSearcherTypeMaskForConditionList - incorrect reference");
                    elseGroupSearcherTypeMasks[i.ElseGroup] &= GetSearcherTypeMaskForConditionList(refe);
                }
                else // handle normal condition
                {
                    // object will match conditions in one ElseGroupStore only when it matches all of them
                    // so, let's find a smallest possible mask which satisfies all conditions
                    elseGroupSearcherTypeMasks[i.ElseGroup] &= i.GetSearcherTypeMaskForCondition();
                }
            }
            // object will match condition when one of the checks in ElseGroupStore is matching
            // so, let's include all possible masks
            GridMapTypeMask mask = 0;
            foreach (var i in elseGroupSearcherTypeMasks)
                mask |= i.Value;

            return mask;
        }

        public bool IsObjectMeetToConditionList(ConditionSourceInfo sourceInfo, List<Condition> conditions)
        {
            //     groupId, groupCheckPassed
            Dictionary<uint, bool> elseGroupStore = new();
            foreach (var condition in conditions)
            {
                Log.outDebug(LogFilter.Condition, "ConditionMgr.IsPlayerMeetToConditionList condType: {0} val1: {1}", condition.ConditionType, condition.ConditionValue1);
                if (condition.IsLoaded())
                {
                    //! Find ElseGroup in ElseGroupStore
                    //! If not found, add an entry in the store and set to true (placeholder)
                    if (!elseGroupStore.ContainsKey(condition.ElseGroup))
                        elseGroupStore[condition.ElseGroup] = true;
                    else if (!elseGroupStore[condition.ElseGroup]) //! If another condition in this group was unmatched before this, don't bother checking (the group is false anyway)
                        continue;

                    if (condition.ReferenceId != 0)//handle reference
                    {
                        var refe = ConditionReferenceStore.LookupByKey(condition.ReferenceId);
                        if (!refe.Empty())
                        {
                            if (!IsObjectMeetToConditionList(sourceInfo, refe))
                                elseGroupStore[condition.ElseGroup] = false;
                        }
                        else
                        {
                            Log.outDebug(LogFilter.Condition, "IsPlayerMeetToConditionList: Reference template -{0} not found",
                                condition.ReferenceId);//checked at loading, should never happen
                        }

                    }
                    else //handle normal condition
                    {
                        if (!condition.Meets(sourceInfo))
                            elseGroupStore[condition.ElseGroup] = false;
                    }
                }
            }
            foreach (var i in elseGroupStore)
                if (i.Value)
                    return true;

            return false;
        }

        public bool IsObjectMeetToConditions(WorldObject obj, List<Condition> conditions)
        {
            ConditionSourceInfo srcInfo = new(obj);
            return IsObjectMeetToConditions(srcInfo, conditions);
        }

        public bool IsObjectMeetToConditions(WorldObject obj1, WorldObject obj2, List<Condition> conditions)
        {
            ConditionSourceInfo srcInfo = new(obj1, obj2);
            return IsObjectMeetToConditions(srcInfo, conditions);
        }

        public bool IsObjectMeetToConditions(ConditionSourceInfo sourceInfo, List<Condition> conditions)
        {
            if (conditions.Empty())
                return true;

            Log.outDebug(LogFilter.Condition, "ConditionMgr.IsObjectMeetToConditions");
            return IsObjectMeetToConditionList(sourceInfo, conditions);
        }

        public bool CanHaveSourceGroupSet(ConditionSourceType sourceType)
        {
            return (sourceType == ConditionSourceType.CreatureLootTemplate ||
                    sourceType == ConditionSourceType.DisenchantLootTemplate ||
                    sourceType == ConditionSourceType.FishingLootTemplate ||
                    sourceType == ConditionSourceType.GameobjectLootTemplate ||
                    sourceType == ConditionSourceType.ItemLootTemplate ||
                    sourceType == ConditionSourceType.MailLootTemplate ||
                    sourceType == ConditionSourceType.MillingLootTemplate ||
                    sourceType == ConditionSourceType.PickpocketingLootTemplate ||
                    sourceType == ConditionSourceType.ProspectingLootTemplate ||
                    sourceType == ConditionSourceType.ReferenceLootTemplate ||
                    sourceType == ConditionSourceType.SkinningLootTemplate ||
                    sourceType == ConditionSourceType.SpellLootTemplate ||
                    sourceType == ConditionSourceType.GossipMenu ||
                    sourceType == ConditionSourceType.GossipMenuOption ||
                    sourceType == ConditionSourceType.VehicleSpell ||
                    sourceType == ConditionSourceType.SpellImplicitTarget ||
                    sourceType == ConditionSourceType.SpellClickEvent ||
                    sourceType == ConditionSourceType.SmartEvent ||
                    sourceType == ConditionSourceType.NpcVendor ||
                    sourceType == ConditionSourceType.Phase);
        }

        public bool CanHaveSourceIdSet(ConditionSourceType sourceType)
        {
            return (sourceType == ConditionSourceType.SmartEvent);
        }

        public bool IsObjectMeetingNotGroupedConditions(ConditionSourceType sourceType, uint entry, ConditionSourceInfo sourceInfo)
        {
            if (sourceType > ConditionSourceType.None && sourceType < ConditionSourceType.Max)
            {
                var conditions = ConditionStore[sourceType].LookupByKey(entry);
                if (!conditions.Empty())
                {
                    Log.outDebug(LogFilter.Condition, "GetConditionsForNotGroupedEntry: found conditions for type {0} and entry {1}", sourceType, entry);
                    return IsObjectMeetToConditions(sourceInfo, conditions);
                }
            }

            return true;
        }

        public bool IsObjectMeetingNotGroupedConditions(ConditionSourceType sourceType, uint entry, WorldObject target0, WorldObject target1 = null, WorldObject target2 = null)
        {
            ConditionSourceInfo conditionSource = new(target0, target1, target2);
            return IsObjectMeetingNotGroupedConditions(sourceType, entry, conditionSource);
        }

        public bool HasConditionsForNotGroupedEntry(ConditionSourceType sourceType, uint entry)
        {
            if (sourceType > ConditionSourceType.None && sourceType < ConditionSourceType.Max)
                if (ConditionStore[sourceType].ContainsKey(entry))
                    return true;

            return false;
        }

        public bool IsObjectMeetingSpellClickConditions(uint creatureId, uint spellId, WorldObject clicker, WorldObject target)
        {
            var multiMap = SpellClickEventConditionStore.LookupByKey(creatureId);
            if (multiMap != null)
            {
                var conditions = multiMap.LookupByKey(spellId);
                if (!conditions.Empty())
                {
                    Log.outDebug(LogFilter.Condition, "GetConditionsForSpellClickEvent: found conditions for SpellClickEvent entry {0} spell {1}", creatureId, spellId);
                    ConditionSourceInfo sourceInfo = new(clicker, target);
                    return IsObjectMeetToConditions(sourceInfo, conditions);
                }
            }
            return true;
        }

        public List<Condition> GetConditionsForSpellClickEvent(uint creatureId, uint spellId)
        {
            var multiMap = SpellClickEventConditionStore.LookupByKey(creatureId);
            if (multiMap != null)
            {
                var conditions = multiMap.LookupByKey(spellId);
                if (!conditions.Empty())
                {
                    Log.outDebug(LogFilter.Condition, "GetConditionsForSpellClickEvent: found conditions for SpellClickEvent entry {0} spell {1}", creatureId, spellId);
                    return conditions;
                }
            }
            return null;
        }

        public bool IsObjectMeetingVehicleSpellConditions(uint creatureId, uint spellId, Player player, Unit vehicle)
        {
            var multiMap = VehicleSpellConditionStore.LookupByKey(creatureId);
            if (multiMap != null)
            {
                var conditions = multiMap.LookupByKey(spellId);
                if (!conditions.Empty())
                {
                    Log.outDebug(LogFilter.Condition, "GetConditionsForVehicleSpell: found conditions for Vehicle entry {0} spell {1}", creatureId, spellId);
                    ConditionSourceInfo sourceInfo = new(player, vehicle);
                    return IsObjectMeetToConditions(sourceInfo, conditions);
                }
            }
            return true;
        }

        public bool IsObjectMeetingSmartEventConditions(long entryOrGuid, uint eventId, SmartScriptType sourceType, Unit unit, WorldObject baseObject)
        {
            var multiMap = SmartEventConditionStore.LookupByKey(Tuple.Create((int)entryOrGuid, (uint)sourceType));
            if (multiMap != null)
            {
                var conditions = multiMap.LookupByKey(eventId + 1);
                if (!conditions.Empty())
                {
                    Log.outDebug(LogFilter.Condition, "GetConditionsForSmartEvent: found conditions for Smart Event entry or guid {0} eventId {1}", entryOrGuid, eventId);
                    ConditionSourceInfo sourceInfo = new(unit, baseObject);
                    return IsObjectMeetToConditions(sourceInfo, conditions);
                }
            }
            return true;
        }

        public bool IsObjectMeetingVendorItemConditions(uint creatureId, uint itemId, Player player, Creature vendor)
        {
            var multiMap = NpcVendorConditionContainerStore.LookupByKey(creatureId);
            if (multiMap != null)
            {
                var conditions = multiMap.LookupByKey(itemId);
                if (!conditions.Empty())
                {
                    Log.outDebug(LogFilter.Condition, "GetConditionsForNpcVendorEvent: found conditions for creature entry {0} item {1}", creatureId, itemId);
                    ConditionSourceInfo sourceInfo = new(player, vendor);
                    return IsObjectMeetToConditions(sourceInfo, conditions);
                }
            }
            return true;
        }

        public void LoadConditions(bool isReload = false)
        {
            uint oldMSTime = Time.GetMSTime();

            Clean();

            //must clear all custom handled cases (groupped types) before reload
            if (isReload)
            {
                Log.outInfo(LogFilter.Server, "Reseting Loot Conditions...");
                LootStorage.Creature.ResetConditions();
                LootStorage.Fishing.ResetConditions();
                LootStorage.Gameobject.ResetConditions();
                LootStorage.Items.ResetConditions();
                LootStorage.Mail.ResetConditions();
                LootStorage.Milling.ResetConditions();
                LootStorage.Pickpocketing.ResetConditions();
                LootStorage.Reference.ResetConditions();
                LootStorage.Skinning.ResetConditions();
                LootStorage.Disenchant.ResetConditions();
                LootStorage.Prospecting.ResetConditions();
                LootStorage.Spell.ResetConditions();

                Log.outInfo(LogFilter.Server, "Re-Loading `gossip_menu` Table for Conditions!");
                Global.ObjectMgr.LoadGossipMenu();

                Log.outInfo(LogFilter.Server, "Re-Loading `gossip_menu_option` Table for Conditions!");
                Global.ObjectMgr.LoadGossipMenuItems();
                Global.SpellMgr.UnloadSpellInfoImplicitTargetConditionLists();

                Global.ObjectMgr.UnloadPhaseConditions();
            }

            SQLResult result = DB.World.Query("SELECT SourceTypeOrReferenceId, SourceGroup, SourceEntry, SourceId, ElseGroup, ConditionTypeOrReference, ConditionTarget, " +
                " ConditionValue1, ConditionValue2, ConditionValue3, NegativeCondition, ErrorType, ErrorTextId, ScriptName FROM conditions");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 conditions. DB table `conditions` is empty!");
                return;
            }

            uint count = 0;
            do
            {
                Condition cond = new();
                int iSourceTypeOrReferenceId = result.Read<int>(0);
                cond.SourceGroup = result.Read<uint>(1);
                cond.SourceEntry = result.Read<int>(2);
                cond.SourceId = result.Read<uint>(3);
                cond.ElseGroup = result.Read<uint>(4);
                int iConditionTypeOrReference = result.Read<int>(5);
                cond.ConditionTarget = result.Read<byte>(6);
                cond.ConditionValue1 = result.Read<uint>(7);
                cond.ConditionValue2 = result.Read<uint>(8);
                cond.ConditionValue3 = result.Read<uint>(9);
                cond.NegativeCondition = result.Read<byte>(10) != 0;
                cond.ErrorType = result.Read<uint>(11);
                cond.ErrorTextId = result.Read<uint>(12);
                cond.ScriptId = Global.ObjectMgr.GetScriptId(result.Read<string>(13));

                if (iConditionTypeOrReference >= 0)
                    cond.ConditionType = (ConditionTypes)iConditionTypeOrReference;

                if (iSourceTypeOrReferenceId >= 0)
                    cond.SourceType = (ConditionSourceType)iSourceTypeOrReferenceId;

                if (iConditionTypeOrReference < 0)//it has a reference
                {
                    if (iConditionTypeOrReference == iSourceTypeOrReferenceId)//self referencing, skip
                    {
                        Log.outError(LogFilter.Sql, "Condition reference {1} is referencing self, skipped", iSourceTypeOrReferenceId);
                        continue;
                    }
                    cond.ReferenceId = (uint)Math.Abs(iConditionTypeOrReference);

                    string rowType = "reference template";
                    if (iSourceTypeOrReferenceId >= 0)
                        rowType = "reference";
                    //check for useless data
                    if (cond.ConditionTarget != 0)
                        Log.outError(LogFilter.Sql, "Condition {0} {1} has useless data in ConditionTarget ({2})!", rowType, iSourceTypeOrReferenceId, cond.ConditionTarget);
                    if (cond.ConditionValue1 != 0)
                        Log.outError(LogFilter.Sql, "Condition {0} {1} has useless data in value1 ({2})!", rowType, iSourceTypeOrReferenceId, cond.ConditionValue1);
                    if (cond.ConditionValue2 != 0)
                        Log.outError(LogFilter.Sql, "Condition {0} {1} has useless data in value2 ({2})!", rowType, iSourceTypeOrReferenceId, cond.ConditionValue2);
                    if (cond.ConditionValue3 != 0)
                        Log.outError(LogFilter.Sql, "Condition {0} {1} has useless data in value3 ({2})!", rowType, iSourceTypeOrReferenceId, cond.ConditionValue3);
                    if (cond.NegativeCondition)
                        Log.outError(LogFilter.Sql, "Condition {0} {1} has useless data in NegativeCondition ({2})!", rowType, iSourceTypeOrReferenceId, cond.NegativeCondition);
                    if (cond.SourceGroup != 0 && iSourceTypeOrReferenceId < 0)
                        Log.outError(LogFilter.Sql, "Condition {0} {1} has useless data in SourceGroup ({2})!", rowType, iSourceTypeOrReferenceId, cond.SourceGroup);
                    if (cond.SourceEntry != 0 && iSourceTypeOrReferenceId < 0)
                        Log.outError(LogFilter.Sql, "Condition {0} {1} has useless data in SourceEntry ({2})!", rowType, iSourceTypeOrReferenceId, cond.SourceEntry);
                }
                else if (!IsConditionTypeValid(cond))//doesn't have reference, validate ConditionType
                    continue;

                if (iSourceTypeOrReferenceId < 0)//it is a reference template
                {
                    ConditionReferenceStore.Add((uint)Math.Abs(iSourceTypeOrReferenceId), cond);//add to reference storage
                    count++;
                    continue;
                }//end of reference templates

                //if not a reference and SourceType is invalid, skip
                if (iConditionTypeOrReference >= 0 && !IsSourceTypeValid(cond))
                    continue;

                //Grouping is only allowed for some types (loot templates, gossip menus, gossip items)
                if (cond.SourceGroup != 0 && !CanHaveSourceGroupSet(cond.SourceType))
                {
                    Log.outError(LogFilter.Sql, "{0} has not allowed value of SourceGroup = {1}!", cond.ToString(), cond.SourceGroup);
                    continue;
                }
                if (cond.SourceId != 0 && !CanHaveSourceIdSet(cond.SourceType))
                {
                    Log.outError(LogFilter.Sql, "{0} has not allowed value of SourceId = {1}!", cond.ToString(), cond.SourceId);
                    continue;
                }

                if (cond.ErrorType != 0 && cond.SourceType != ConditionSourceType.Spell)
                {
                    Log.outError(LogFilter.Sql, "{0} can't have ErrorType ({1}), set to 0!", cond.ToString(), cond.ErrorType);
                    cond.ErrorType = 0;
                }

                if (cond.ErrorTextId != 0 && cond.ErrorType == 0)
                {
                    Log.outError(LogFilter.Sql, "{0} has any ErrorType, ErrorTextId ({1}) is set, set to 0!", cond.ToString(), cond.ErrorTextId);
                    cond.ErrorTextId = 0;
                }

                if (cond.SourceGroup != 0)
                {
                    bool valid = false;
                    // handle grouped conditions
                    switch (cond.SourceType)
                    {
                        case ConditionSourceType.CreatureLootTemplate:
                            valid = AddToLootTemplate(cond, LootStorage.Creature.GetLootForConditionFill(cond.SourceGroup));
                            break;
                        case ConditionSourceType.DisenchantLootTemplate:
                            valid = AddToLootTemplate(cond, LootStorage.Disenchant.GetLootForConditionFill(cond.SourceGroup));
                            break;
                        case ConditionSourceType.FishingLootTemplate:
                            valid = AddToLootTemplate(cond, LootStorage.Fishing.GetLootForConditionFill(cond.SourceGroup));
                            break;
                        case ConditionSourceType.GameobjectLootTemplate:
                            valid = AddToLootTemplate(cond, LootStorage.Gameobject.GetLootForConditionFill(cond.SourceGroup));
                            break;
                        case ConditionSourceType.ItemLootTemplate:
                            valid = AddToLootTemplate(cond, LootStorage.Items.GetLootForConditionFill(cond.SourceGroup));
                            break;
                        case ConditionSourceType.MailLootTemplate:
                            valid = AddToLootTemplate(cond, LootStorage.Mail.GetLootForConditionFill(cond.SourceGroup));
                            break;
                        case ConditionSourceType.MillingLootTemplate:
                            valid = AddToLootTemplate(cond, LootStorage.Milling.GetLootForConditionFill(cond.SourceGroup));
                            break;
                        case ConditionSourceType.PickpocketingLootTemplate:
                            valid = AddToLootTemplate(cond, LootStorage.Pickpocketing.GetLootForConditionFill(cond.SourceGroup));
                            break;
                        case ConditionSourceType.ProspectingLootTemplate:
                            valid = AddToLootTemplate(cond, LootStorage.Prospecting.GetLootForConditionFill(cond.SourceGroup));
                            break;
                        case ConditionSourceType.ReferenceLootTemplate:
                            valid = AddToLootTemplate(cond, LootStorage.Reference.GetLootForConditionFill(cond.SourceGroup));
                            break;
                        case ConditionSourceType.SkinningLootTemplate:
                            valid = AddToLootTemplate(cond, LootStorage.Skinning.GetLootForConditionFill(cond.SourceGroup));
                            break;
                        case ConditionSourceType.SpellLootTemplate:
                            valid = AddToLootTemplate(cond, LootStorage.Spell.GetLootForConditionFill(cond.SourceGroup));
                            break;
                        case ConditionSourceType.GossipMenu:
                            valid = AddToGossipMenus(cond);
                            break;
                        case ConditionSourceType.GossipMenuOption:
                            valid = AddToGossipMenuItems(cond);
                            break;
                        case ConditionSourceType.SpellClickEvent:
                            {
                                if (!SpellClickEventConditionStore.ContainsKey(cond.SourceGroup))
                                    SpellClickEventConditionStore[cond.SourceGroup] = new MultiMap<uint, Condition>();

                                SpellClickEventConditionStore[cond.SourceGroup].Add((uint)cond.SourceEntry, cond);
                                ++count;
                                continue;   // do not add to m_AllocatedMemory to avoid double deleting
                            }
                        case ConditionSourceType.SpellImplicitTarget:
                            valid = AddToSpellImplicitTargetConditions(cond);
                            break;
                        case ConditionSourceType.VehicleSpell:
                            {
                                if (!VehicleSpellConditionStore.ContainsKey(cond.SourceGroup))
                                    VehicleSpellConditionStore[cond.SourceGroup] = new MultiMap<uint, Condition>();

                                VehicleSpellConditionStore[cond.SourceGroup].Add((uint)cond.SourceEntry, cond);
                                ++count;
                                continue;   // do not add to m_AllocatedMemory to avoid double deleting
                            }
                        case ConditionSourceType.SmartEvent:
                            {
                                //! TODO: PAIR_32 ?
                                var key = Tuple.Create(cond.SourceEntry, cond.SourceId);
                                if (!SmartEventConditionStore.ContainsKey(key))
                                    SmartEventConditionStore[key] = new MultiMap<uint, Condition>();

                                SmartEventConditionStore[key].Add(cond.SourceGroup, cond);
                                ++count;
                                continue;
                            }
                        case ConditionSourceType.NpcVendor:
                            {
                                if (!NpcVendorConditionContainerStore.ContainsKey(cond.SourceGroup))
                                    NpcVendorConditionContainerStore[cond.SourceGroup] = new MultiMap<uint, Condition>();

                                NpcVendorConditionContainerStore[cond.SourceGroup].Add((uint)cond.SourceEntry, cond);
                                ++count;
                                continue;
                            }
                        case ConditionSourceType.Phase:
                            valid = AddToPhases(cond);
                            break;
                        default:
                            break;
                    }

                    if (!valid)
                        Log.outError(LogFilter.Sql, "{0} Not handled grouped condition.", cond.ToString());
                    else
                        ++count;

                    continue;
                }

                //add new Condition to storage based on Type/Entry
                ConditionStore[cond.SourceType].Add((uint)cond.SourceEntry, cond);
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} conditions in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        bool AddToLootTemplate(Condition cond, LootTemplate loot)
        {
            if (loot == null)
            {
                Log.outError(LogFilter.Sql, "{0} LootTemplate {1} not found.", cond.ToString(), cond.SourceGroup);
                return false;
            }

            if (loot.AddConditionItem(cond))
                return true;

            Log.outError(LogFilter.Sql, "{0} Item {1} not found in LootTemplate {2}.", cond.ToString(), cond.SourceEntry, cond.SourceGroup);
            return false;
        }

        bool AddToGossipMenus(Condition cond)
        {
            var pMenuBounds = Global.ObjectMgr.GetGossipMenusMapBounds(cond.SourceGroup);

            foreach (var menu in pMenuBounds)
            {
                if (menu.MenuId == cond.SourceGroup && menu.TextId == cond.SourceEntry)
                {
                    menu.Conditions.Add(cond);
                    return true;
                }
            }

            Log.outError(LogFilter.Sql, "{0} GossipMenu {1} not found.", cond.ToString(), cond.SourceGroup);
            return false;
        }

        bool AddToGossipMenuItems(Condition cond)
        {
            var pMenuItemBounds = Global.ObjectMgr.GetGossipMenuItemsMapBounds(cond.SourceGroup);
            foreach (var menuItems in pMenuItemBounds)
            {
                if (menuItems.MenuId == cond.SourceGroup && menuItems.OptionIndex == cond.SourceEntry)
                {
                    menuItems.Conditions.Add(cond);
                    return true;
                }
            }

            Log.outError(LogFilter.Sql, "{0} GossipMenuId {1} Item {2} not found.", cond.ToString(), cond.SourceGroup, cond.SourceEntry);
            return false;
        }

        bool AddToSpellImplicitTargetConditions(Condition cond)
        {
            Global.SpellMgr.ForEachSpellInfoDifficulty((uint)cond.SourceEntry, spellInfo =>
            {
                uint conditionEffMask = cond.SourceGroup;
                List<uint> sharedMasks = new();
                for (byte i = 0; i < SpellConst.MaxEffects; ++i)
                {
                    SpellEffectInfo effect = spellInfo.GetEffect(i);
                    if (effect == null)
                        continue;

                    // check if effect is already a part of some shared mask
                    bool found = false;
                    foreach (var value in sharedMasks)
                    {
                        if (((1 << i) & value) != 0)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found)
                        continue;

                    // build new shared mask with found effect
                    uint sharedMask = (uint)(1 << i);
                    List<Condition> cmp = effect.ImplicitTargetConditions;
                    for (byte effIndex = (byte)(i + 1); effIndex < SpellConst.MaxEffects; ++effIndex)
                    {
                        SpellEffectInfo inner = spellInfo.GetEffect(effIndex);
                        if (inner == null)
                            continue;

                        if (inner.ImplicitTargetConditions == cmp)
                            sharedMask |= (uint)(1 << effIndex);
                    }
                    sharedMasks.Add(sharedMask);
                }

                foreach (var value in sharedMasks)
                {
                    // some effect indexes should have same data
                    uint commonMask = (value & conditionEffMask);
                    if (commonMask != 0)
                    {
                        byte firstEffIndex = 0;
                        for (; firstEffIndex < SpellConst.MaxEffects; ++firstEffIndex)
                            if (((1 << firstEffIndex) & value) != 0)
                                break;

                        if (firstEffIndex >= SpellConst.MaxEffects)
                            return;

                        SpellEffectInfo effect = spellInfo.GetEffect(firstEffIndex);
                        if (effect == null)
                            continue;

                        // get shared data
                        List<Condition> sharedList = effect.ImplicitTargetConditions;

                        // there's already data entry for that sharedMask
                        if (sharedList != null)
                        {
                            // we have overlapping masks in db
                            if (conditionEffMask != value)
                            {
                                Log.outError(LogFilter.Sql, "{0} in `condition` table, has incorrect SourceGroup {1} (spell effectMask) set - " +
                                   "effect masks are overlapping (all SourceGroup values having given bit set must be equal) - ignoring.", cond.ToString(), cond.SourceGroup);
                                return;
                            }
                        }
                        // no data for shared mask, we can create new submask
                        else
                        {
                            // add new list, create new shared mask
                            sharedList = new List<Condition>();
                            bool assigned = false;
                            for (byte i = firstEffIndex; i < SpellConst.MaxEffects; ++i)
                            {
                                SpellEffectInfo eff = spellInfo.GetEffect(i);
                                if (eff == null)
                                    continue;

                                if (((1 << i) & commonMask) != 0)
                                {
                                    eff.ImplicitTargetConditions = sharedList;
                                    assigned = true;
                                }
                            }

                            if (!assigned)
                                break;
                        }
                        sharedList.Add(cond);
                        break;
                    }
                }
            });
            return true;
        }

        bool AddToPhases(Condition cond)
        {
            if (cond.SourceEntry == 0)
            {
                PhaseInfoStruct phaseInfo = Global.ObjectMgr.GetPhaseInfo(cond.SourceGroup);
                if (phaseInfo != null)
                {
                    bool found = false;
                    foreach (uint areaId in phaseInfo.Areas)
                    {
                        List<PhaseAreaInfo> phases = Global.ObjectMgr.GetPhasesForArea(areaId);
                        if (phases != null)
                        {
                                foreach (PhaseAreaInfo phase in phases)
                            {
                                if (phase.PhaseInfo.Id == cond.SourceGroup)
                                {
                                    phase.Conditions.Add(cond);
                                    found = true;
                                }
                            }
                        }
                    }

                    if (found)
                        return true;
                }
            }
            else
            {
                var phases = Global.ObjectMgr.GetPhasesForArea((uint)cond.SourceEntry);
                foreach (PhaseAreaInfo phase in phases)
                {
                    if (phase.PhaseInfo.Id == cond.SourceGroup)
                    {
                        phase.Conditions.Add(cond);
                        return true;
                    }
                }
            }

            Log.outError(LogFilter.Sql, "{0} Area {1} does not have phase {2}.", cond.ToString(), cond.SourceGroup, cond.SourceEntry);
            return false;
        }

        bool IsSourceTypeValid(Condition cond)
        {
            switch (cond.SourceType)
            {
                case ConditionSourceType.CreatureLootTemplate:
                    {
                        if (!LootStorage.Creature.HaveLootFor(cond.SourceGroup))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceGroup in `condition` table, does not exist in `creature_loot_template`, ignoring.", cond.ToString());
                            return false;
                        }

                        LootTemplate loot = LootStorage.Creature.GetLootForConditionFill(cond.SourceGroup);
                        ItemTemplate pItemProto = Global.ObjectMgr.GetItemTemplate((uint)cond.SourceEntry);
                        if (pItemProto == null && !loot.IsReference((uint)cond.SourceEntry))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceType, SourceEntry in `condition` table, Item does not exist, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.DisenchantLootTemplate:
                    {
                        if (!LootStorage.Disenchant.HaveLootFor(cond.SourceGroup))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceGroup in `condition` table, does not exist in `disenchant_loot_template`, ignoring.", cond.ToString());
                            return false;
                        }

                        LootTemplate loot = LootStorage.Disenchant.GetLootForConditionFill(cond.SourceGroup);
                        ItemTemplate pItemProto = Global.ObjectMgr.GetItemTemplate((uint)cond.SourceEntry);
                        if (pItemProto == null && !loot.IsReference((uint)cond.SourceEntry))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceType, SourceEntry in `condition` table, item does not exist, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.FishingLootTemplate:
                    {
                        if (!LootStorage.Fishing.HaveLootFor(cond.SourceGroup))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceGroup in `condition` table, does not exist in `fishing_loot_template`, ignoring.", cond.ToString());
                            return false;
                        }

                        LootTemplate loot = LootStorage.Fishing.GetLootForConditionFill(cond.SourceGroup);
                        ItemTemplate pItemProto = Global.ObjectMgr.GetItemTemplate((uint)cond.SourceEntry);
                        if (pItemProto == null && !loot.IsReference((uint)cond.SourceEntry))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceType, SourceEntry in `condition` table, item does not exist, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.GameobjectLootTemplate:
                    {
                        if (!LootStorage.Gameobject.HaveLootFor(cond.SourceGroup))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceGroup in `condition` table, does not exist in `gameobject_loot_template`, ignoring.", cond.ToString());
                            return false;
                        }

                        LootTemplate loot = LootStorage.Gameobject.GetLootForConditionFill(cond.SourceGroup);
                        ItemTemplate pItemProto = Global.ObjectMgr.GetItemTemplate((uint)cond.SourceEntry);
                        if (pItemProto == null && !loot.IsReference((uint)cond.SourceEntry))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceType, SourceEntry in `condition` table, item does not exist, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.ItemLootTemplate:
                    {
                        if (!LootStorage.Items.HaveLootFor(cond.SourceGroup))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceGroup in `condition` table, does not exist in `item_loot_template`, ignoring.", cond.ToString());
                            return false;
                        }

                        LootTemplate loot = LootStorage.Items.GetLootForConditionFill(cond.SourceGroup);
                        ItemTemplate pItemProto = Global.ObjectMgr.GetItemTemplate((uint)cond.SourceEntry);
                        if (pItemProto == null && !loot.IsReference((uint)cond.SourceEntry))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceType, SourceEntry in `condition` table, item does not exist, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.MailLootTemplate:
                    {
                        if (!LootStorage.Mail.HaveLootFor(cond.SourceGroup))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceGroup in `condition` table, does not exist in `mail_loot_template`, ignoring.", cond.ToString());
                            return false;
                        }

                        LootTemplate loot = LootStorage.Mail.GetLootForConditionFill(cond.SourceGroup);
                        ItemTemplate pItemProto = Global.ObjectMgr.GetItemTemplate((uint)cond.SourceEntry);
                        if (pItemProto == null && !loot.IsReference((uint)cond.SourceEntry))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceType, SourceEntry in `condition` table, item does not exist, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.MillingLootTemplate:
                    {
                        if (!LootStorage.Milling.HaveLootFor(cond.SourceGroup))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceGroup in `condition` table, does not exist in `milling_loot_template`, ignoring.", cond.ToString());
                            return false;
                        }

                        LootTemplate loot = LootStorage.Milling.GetLootForConditionFill(cond.SourceGroup);
                        ItemTemplate pItemProto = Global.ObjectMgr.GetItemTemplate((uint)cond.SourceEntry);
                        if (pItemProto == null && !loot.IsReference((uint)cond.SourceEntry))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceType, SourceEntry in `condition` table, item does not exist, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.PickpocketingLootTemplate:
                    {
                        if (!LootStorage.Pickpocketing.HaveLootFor(cond.SourceGroup))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceGroup in `condition` table, does not exist in `pickpocketing_loot_template`, ignoring.", cond.ToString());
                            return false;
                        }

                        LootTemplate loot = LootStorage.Pickpocketing.GetLootForConditionFill(cond.SourceGroup);
                        ItemTemplate pItemProto = Global.ObjectMgr.GetItemTemplate((uint)cond.SourceEntry);
                        if (pItemProto == null && !loot.IsReference((uint)cond.SourceEntry))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceType, SourceEntry in `condition` table, item does not exist, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.ProspectingLootTemplate:
                    {
                        if (!LootStorage.Prospecting.HaveLootFor(cond.SourceGroup))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceGroup in `condition` table, does not exist in `prospecting_loot_template`, ignoring.", cond.ToString());
                            return false;
                        }

                        LootTemplate loot = LootStorage.Prospecting.GetLootForConditionFill(cond.SourceGroup);
                        ItemTemplate pItemProto = Global.ObjectMgr.GetItemTemplate((uint)cond.SourceEntry);
                        if (pItemProto == null && !loot.IsReference((uint)cond.SourceEntry))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceType, SourceEntry in `condition` table, item does not exist, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.ReferenceLootTemplate:
                    {
                        if (!LootStorage.Reference.HaveLootFor(cond.SourceGroup))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceGroup in `condition` table, does not exist in `reference_loot_template`, ignoring.", cond.ToString());
                            return false;
                        }

                        LootTemplate loot = LootStorage.Reference.GetLootForConditionFill(cond.SourceGroup);
                        ItemTemplate pItemProto = Global.ObjectMgr.GetItemTemplate((uint)cond.SourceEntry);
                        if (pItemProto == null && !loot.IsReference((uint)cond.SourceEntry))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceType, SourceEntry in `condition` table, item does not exist, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.SkinningLootTemplate:
                    {
                        if (!LootStorage.Skinning.HaveLootFor(cond.SourceGroup))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceGroup in `condition` table, does not exist in `skinning_loot_template`, ignoring.", cond.ToString());
                            return false;
                        }

                        LootTemplate loot = LootStorage.Skinning.GetLootForConditionFill(cond.SourceGroup);
                        ItemTemplate pItemProto = Global.ObjectMgr.GetItemTemplate((uint)cond.SourceEntry);
                        if (pItemProto == null && !loot.IsReference((uint)cond.SourceEntry))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceType, SourceEntry in `condition` table, item does not exist, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.SpellLootTemplate:
                    {
                        if (!LootStorage.Spell.HaveLootFor(cond.SourceGroup))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceGroup in `condition` table, does not exist in `spell_loot_template`, ignoring.", cond.ToString());
                            return false;
                        }

                        LootTemplate loot = LootStorage.Spell.GetLootForConditionFill(cond.SourceGroup);
                        ItemTemplate pItemProto = Global.ObjectMgr.GetItemTemplate((uint)cond.SourceEntry);
                        if (pItemProto == null && !loot.IsReference((uint)cond.SourceEntry))
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceType, SourceEntry in `condition` table, item does not exist, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.SpellImplicitTarget:
                    {
                        SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo((uint)cond.SourceEntry, Difficulty.None);
                        if (spellInfo == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceEntry in `condition` table does not exist in `spell.dbc`, ignoring.", cond.ToString());
                            return false;
                        }

                        if ((cond.SourceGroup > SpellConst.MaxEffectMask) || cond.SourceGroup == 0)
                        {
                            Log.outError(LogFilter.Sql, "{0} in `condition` table, has incorrect SourceGroup (spell effectMask) set, ignoring.", cond.ToString());
                            return false;
                        }

                        uint origGroup = cond.SourceGroup;

                        for (byte i = 0; i < SpellConst.MaxEffects; ++i)
                        {
                            if (((1 << i) & cond.SourceGroup) == 0)
                                continue;

                            SpellEffectInfo effect = spellInfo.GetEffect(i);
                            if (effect == null)
                                continue;

                            if (effect.ChainTargets > 0)
                                continue;

                            switch (effect.TargetA.GetSelectionCategory())
                            {
                                case SpellTargetSelectionCategories.Nearby:
                                case SpellTargetSelectionCategories.Cone:
                                case SpellTargetSelectionCategories.Area:
                                case SpellTargetSelectionCategories.Traj:
                                    continue;
                                default:
                                    break;
                            }

                            switch (effect.TargetB.GetSelectionCategory())
                            {
                                case SpellTargetSelectionCategories.Nearby:
                                case SpellTargetSelectionCategories.Cone:
                                case SpellTargetSelectionCategories.Area:
                                case SpellTargetSelectionCategories.Traj:
                                    continue;
                                default:
                                    break;
                            }

                            Log.outError(LogFilter.Sql, "SourceEntry {0} SourceGroup {1} in `condition` table - spell {2} does not have implicit targets of types: _AREA_, _CONE_, _NEARBY_, _CHAIN_ for effect {3}, SourceGroup needs correction, ignoring.", cond.SourceEntry, origGroup, cond.SourceEntry, i);
                            cond.SourceGroup &= ~(uint)(1 << i);
                        }
                        // all effects were removed, no need to add the condition at all
                        if (cond.SourceGroup == 0)
                            return false;
                        break;
                    }
                case ConditionSourceType.CreatureTemplateVehicle:
                    {
                        if (Global.ObjectMgr.GetCreatureTemplate((uint)cond.SourceEntry) == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceEntry in `condition` table does not exist in `creature_template`, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.Spell:
                case ConditionSourceType.SpellProc:
                    {
                        SpellInfo spellProto = Global.SpellMgr.GetSpellInfo((uint)cond.SourceEntry, Difficulty.None);
                        if (spellProto == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceEntry in `condition` table does not exist in `spell.dbc`, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.QuestAvailable:
                    if (Global.ObjectMgr.GetQuestTemplate((uint)cond.SourceEntry) == null)
                    {
                        Log.outError(LogFilter.Sql, "{0} SourceEntry specifies non-existing quest, skipped.", cond.ToString());
                        return false;
                    }
                    break;
                case ConditionSourceType.VehicleSpell:
                    if (Global.ObjectMgr.GetCreatureTemplate(cond.SourceGroup) == null)
                    {
                        Log.outError(LogFilter.Sql, "{0} SourceEntry in `condition` table does not exist in `creature_template`, ignoring.", cond.ToString());
                        return false;
                    }

                    if (!Global.SpellMgr.HasSpellInfo((uint)cond.SourceEntry, Difficulty.None))
                    {
                        Log.outError(LogFilter.Sql, "{0} SourceEntry in `condition` table does not exist in `spell.dbc`, ignoring.", cond.ToString());
                        return false;
                    }
                    break;
                case ConditionSourceType.SpellClickEvent:
                    if (Global.ObjectMgr.GetCreatureTemplate(cond.SourceGroup) == null)
                    {
                        Log.outError(LogFilter.Sql, "{0} SourceEntry in `condition` table does not exist in `creature_template`, ignoring.", cond.ToString());
                        return false;
                    }

                    if (!Global.SpellMgr.HasSpellInfo((uint)cond.SourceEntry, Difficulty.None))
                    {
                        Log.outError(LogFilter.Sql, "{0} SourceEntry in `condition` table does not exist in `spell.dbc`, ignoring.", cond.ToString());
                        return false;
                    }
                    break;
                case ConditionSourceType.NpcVendor:
                    {
                        if (Global.ObjectMgr.GetCreatureTemplate(cond.SourceGroup) == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceEntry in `condition` table does not exist in `creature_template`, ignoring.", cond.ToString());
                            return false;
                        }
                        ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate((uint)cond.SourceEntry);
                        if (itemTemplate == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} SourceEntry in `condition` table item does not exist, ignoring.", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionSourceType.TerrainSwap:
                    if (!CliDB.MapStorage.ContainsKey((uint)cond.SourceEntry))
                    {
                        Log.outError(LogFilter.Sql, "{0} SourceEntry in `condition` table does not exist in Map.dbc, ignoring.", cond.ToString());
                        return false;
                    }
                    break;
                case ConditionSourceType.Phase:
                    if (cond.SourceEntry != 0 && !CliDB.AreaTableStorage.ContainsKey(cond.SourceEntry))
                    {
                        Log.outError(LogFilter.Sql, "{0} SourceEntry in `condition` table does not exist in AreaTable.dbc, ignoring.", cond.ToString());
                        return false;
                    }
                    break;
                case ConditionSourceType.GossipMenu:
                case ConditionSourceType.GossipMenuOption:
                case ConditionSourceType.SmartEvent:
                    break;
                case ConditionSourceType.Graveyard:
                    if (Global.ObjectMgr.GetWorldSafeLoc((uint)cond.SourceEntry) == null)
                    {
                        Log.outError(LogFilter.Sql, $"{cond.ToString()} SourceEntry in `condition` table, does not exist in WorldSafeLocs.db2, ignoring.");
                        return false;
                    }
                    break;
                default:
                    Log.outError(LogFilter.Sql, $"{cond.ToString()} Invalid ConditionSourceType in `condition` table, ignoring.");
                    break;
            }

            return true;
        }

        bool IsConditionTypeValid(Condition cond)
        {
            switch (cond.ConditionType)
            {
                case ConditionTypes.Aura:
                    {
                        if (!Global.SpellMgr.HasSpellInfo(cond.ConditionValue1, Difficulty.None))
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing spell (Id: {1}), skipped", cond.ToString(), cond.ConditionValue1);
                            return false;
                        }

                        if (cond.ConditionValue2 > 2)
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing effect index ({1}) (must be 0..2), skipped", cond.ToString(), cond.ConditionValue2);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.Item:
                    {
                        ItemTemplate proto = Global.ObjectMgr.GetItemTemplate(cond.ConditionValue1);
                        if (proto == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} Item ({1}) does not exist, skipped", cond.ToString(), cond.ConditionValue1);
                            return false;
                        }

                        if (cond.ConditionValue2 == 0)
                        {
                            Log.outError(LogFilter.Sql, "{0} Zero item count in ConditionValue2, skipped", cond.ToString());
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.ItemEquipped:
                    {
                        ItemTemplate proto = Global.ObjectMgr.GetItemTemplate(cond.ConditionValue1);
                        if (proto == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} Item ({1}) does not exist, skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.Zoneid:
                    {
                        AreaTableRecord areaEntry = CliDB.AreaTableStorage.LookupByKey(cond.ConditionValue1);
                        if (areaEntry == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} Area ({1}) does not exist, skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }

                        if (areaEntry.ParentAreaID != 0)
                        {
                            Log.outError(LogFilter.Sql, "{0} requires to be in area ({1}) which is a subzone but zone expected, skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.ReputationRank:
                    {
                        if (!CliDB.FactionStorage.ContainsKey(cond.ConditionValue1))
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing faction ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.Team:
                    {
                        if (cond.ConditionValue1 != (uint)Team.Alliance && cond.ConditionValue1 != (uint)Team.Horde)
                        {
                            Log.outError(LogFilter.Sql, "{0} specifies unknown team ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.Skill:
                    {
                        SkillLineRecord pSkill = CliDB.SkillLineStorage.LookupByKey(cond.ConditionValue1);
                        if (pSkill == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} specifies non-existing skill ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }

                        if (cond.ConditionValue2 < 1 || cond.ConditionValue2 > Global.WorldMgr.GetConfigMaxSkillValue())
                        {
                            Log.outError(LogFilter.Sql, "{0} specifies skill ({1}) with invalid value ({1}), skipped.", cond.ToString(true), cond.ConditionValue1, cond.ConditionValue2);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.Queststate:
                    if (cond.ConditionValue2 >= (1 << (int)QuestStatus.Max))
                    {
                        Log.outError(LogFilter.Sql, "{0} has invalid state mask ({1}), skipped.", cond.ToString(true), cond.ConditionValue2);
                        return false;
                    }

                    if (Global.ObjectMgr.GetQuestTemplate(cond.ConditionValue1) == null)
                    {
                        Log.outError(LogFilter.Sql, "{0} points to non-existing quest ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                        return false;
                    }
                    break;
                case ConditionTypes.QuestRewarded:
                case ConditionTypes.QuestTaken:
                case ConditionTypes.QuestNone:
                case ConditionTypes.QuestComplete:
                case ConditionTypes.DailyQuestDone:
                    {
                        if (Global.ObjectMgr.GetQuestTemplate(cond.ConditionValue1) == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} points to non-existing quest ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.ActiveEvent:
                    {
                        var events = Global.GameEventMgr.GetEventMap();
                        if (cond.ConditionValue1 >= events.Length || !events[cond.ConditionValue1].IsValid())
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing event id ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.Achievement:
                    {
                        AchievementRecord achievement = CliDB.AchievementStorage.LookupByKey(cond.ConditionValue1);
                        if (achievement == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing achivement id ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.Class:
                    {
                        if (Convert.ToBoolean(cond.ConditionValue1 & ~(uint)Class.ClassMaskAllPlayable))
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing classmask ({1}), skipped.", cond.ToString(true), cond.ConditionValue1 & ~(uint)Class.ClassMaskAllPlayable);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.Race:
                    {
                        if (Convert.ToBoolean(cond.ConditionValue1 & ~SharedConst.RaceMaskAllPlayable))
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing racemask ({1}), skipped.", cond.ToString(true), cond.ConditionValue1 & ~SharedConst.RaceMaskAllPlayable);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.Gender:
                    {
                        if (!Player.IsValidGender((Gender)cond.ConditionValue1))
                        {
                            Log.outError(LogFilter.Sql, "{0} has invalid gender ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.Mapid:
                    {
                        MapRecord me = CliDB.MapStorage.LookupByKey(cond.ConditionValue1);
                        if (me == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing map ({1}), skipped", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.Spell:
                    {
                        if (!Global.SpellMgr.HasSpellInfo(cond.ConditionValue1, Difficulty.None))
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing spell (Id: {1}), skipped", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.Level:
                    {
                        if (cond.ConditionValue2 >= (uint)ComparisionType.Max)
                        {
                            Log.outError(LogFilter.Sql, "{0} has invalid ComparisionType ({1}), skipped.", cond.ToString(true), cond.ConditionValue2);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.DrunkenState:
                    {
                        if (cond.ConditionValue1 > (uint)DrunkenState.Smashed)
                        {
                            Log.outError(LogFilter.Sql, "{0} has invalid state ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.NearCreature:
                    {
                        if (Global.ObjectMgr.GetCreatureTemplate(cond.ConditionValue1) == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing creature template entry ({1}), skipped", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.NearGameobject:
                    {
                        if (Global.ObjectMgr.GetGameObjectTemplate(cond.ConditionValue1) == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing gameobject template entry ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.ObjectEntryGuid:
                    {
                        switch ((TypeId)cond.ConditionValue1)
                        {
                            case TypeId.Unit:
                                if (cond.ConditionValue2 != 0 && Global.ObjectMgr.GetCreatureTemplate(cond.ConditionValue2) == null)
                                {
                                    Log.outError(LogFilter.Sql, "{0} has non existing creature template entry ({1}), skipped.", cond.ToString(true), cond.ConditionValue2);
                                    return false;
                                }
                                if (cond.ConditionValue3 != 0)
                                {
                                    CreatureData creatureData = Global.ObjectMgr.GetCreatureData(cond.ConditionValue3);
                                    if (creatureData != null)
                                    {
                                        if (cond.ConditionValue2 != 0 && creatureData.Id != cond.ConditionValue2)
                                        {
                                            Log.outError(LogFilter.Sql, "{0} has guid {1} set but does not match creature entry ({1}), skipped.", cond.ToString(true), cond.ConditionValue3, cond.ConditionValue2);
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        Log.outError(LogFilter.Sql, "{0} has non existing creature guid ({1}), skipped.", cond.ToString(true), cond.ConditionValue3);
                                        return false;
                                    }
                                }
                                break;
                            case TypeId.GameObject:
                                if (cond.ConditionValue2 != 0 && Global.ObjectMgr.GetGameObjectTemplate(cond.ConditionValue2) == null)
                                {
                                    Log.outError(LogFilter.Sql, "{0} has non existing gameobject template entry ({1}), skipped.", cond.ToString(true), cond.ConditionValue2);
                                    return false;
                                }
                                if (cond.ConditionValue3 != 0)
                                {
                                    GameObjectData goData = Global.ObjectMgr.GetGameObjectData(cond.ConditionValue3);
                                    if (goData != null)
                                    {
                                        if (cond.ConditionValue2 != 0 && goData.Id != cond.ConditionValue2)
                                        {
                                            Log.outError(LogFilter.Sql, "{0} has guid {1} set but does not match gameobject entry ({1}), skipped.", cond.ToString(true), cond.ConditionValue3, cond.ConditionValue2);
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        Log.outError(LogFilter.Sql, "{0} has non existing gameobject guid ({1}), skipped.", cond.ToString(true), cond.ConditionValue3);
                                        return false;
                                    }
                                }
                                break;
                            case TypeId.Player:
                            case TypeId.Corpse:
                                if (cond.ConditionValue2 != 0)
                                    LogUselessConditionValue(cond, 2, cond.ConditionValue2);
                                if (cond.ConditionValue3 != 0)
                                    LogUselessConditionValue(cond, 3, cond.ConditionValue3);
                                break;
                            default:
                                Log.outError(LogFilter.Sql, "{0} has wrong typeid set ({1}), skipped", cond.ToString(true), cond.ConditionValue1);
                                return false;
                        }
                        break;
                    }
                case ConditionTypes.TypeMask:
                    {
                        if (cond.ConditionValue1 == 0 || Convert.ToBoolean(cond.ConditionValue1 & ~(uint)(TypeMask.Unit | TypeMask.Player | TypeMask.GameObject | TypeMask.Corpse)))
                        {
                            Log.outError(LogFilter.Sql, "{0} has invalid typemask set ({1}), skipped.", cond.ToString(true), cond.ConditionValue2);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.RelationTo:
                    {
                        if (cond.ConditionValue1 >= cond.GetMaxAvailableConditionTargets())
                        {
                            Log.outError(LogFilter.Sql, "{0} has invalid ConditionValue1(ConditionTarget selection) ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        if (cond.ConditionValue1 == cond.ConditionTarget)
                        {
                            Log.outError(LogFilter.Sql, "{0} has ConditionValue1(ConditionTarget selection) set to self ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        if (cond.ConditionValue2 >= (uint)RelationType.Max)
                        {
                            Log.outError(LogFilter.Sql, "{0} has invalid ConditionValue2(RelationType) ({1}), skipped.", cond.ToString(true), cond.ConditionValue2);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.ReactionTo:
                    {
                        if (cond.ConditionValue1 >= cond.GetMaxAvailableConditionTargets())
                        {
                            Log.outError(LogFilter.Sql, "{0} has invalid ConditionValue1(ConditionTarget selection) ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        if (cond.ConditionValue1 == cond.ConditionTarget)
                        {
                            Log.outError(LogFilter.Sql, "{0} has ConditionValue1(ConditionTarget selection) set to self ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        if (cond.ConditionValue2 == 0)
                        {
                            Log.outError(LogFilter.Sql, "{0} has invalid ConditionValue2(rankMask) ({1}), skipped.", cond.ToString(true), cond.ConditionValue2);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.DistanceTo:
                    {
                        if (cond.ConditionValue1 >= cond.GetMaxAvailableConditionTargets())
                        {
                            Log.outError(LogFilter.Sql, "{0} has invalid ConditionValue1(ConditionTarget selection) ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        if (cond.ConditionValue1 == cond.ConditionTarget)
                        {
                            Log.outError(LogFilter.Sql, "{0} has ConditionValue1(ConditionTarget selection) set to self ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        if (cond.ConditionValue3 >= (uint)ComparisionType.Max)
                        {
                            Log.outError(LogFilter.Sql, "{0} has invalid ComparisionType ({1}), skipped.", cond.ToString(true), cond.ConditionValue3);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.HpVal:
                    {
                        if (cond.ConditionValue2 >= (uint)ComparisionType.Max)
                        {
                            Log.outError(LogFilter.Sql, "{0} has invalid ComparisionType ({1}), skipped.", cond.ToString(true), cond.ConditionValue2);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.HpPct:
                    {
                        if (cond.ConditionValue1 > 100)
                        {
                            Log.outError(LogFilter.Sql, "{0} has too big percent value ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        if (cond.ConditionValue2 >= (uint)ComparisionType.Max)
                        {
                            Log.outError(LogFilter.Sql, "{0} has invalid ComparisionType ({1}), skipped.", cond.ToString(true), cond.ConditionValue2);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.WorldState:
                    {
                        if (Global.WorldMgr.GetWorldState((WorldStates)cond.ConditionValue1) == 0)
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing world state in value1 ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.PhaseId:
                    {
                        if (!CliDB.PhaseStorage.ContainsKey(cond.ConditionValue1))
                        {
                            Log.outError(LogFilter.Sql, "{0} has nonexistent phaseid in value1 ({1}), skipped", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.Title:
                    {
                        CharTitlesRecord titleEntry = CliDB.CharTitlesStorage.LookupByKey(cond.ConditionValue1);
                        if (titleEntry == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing title in value1 ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.SpawnmaskDeprecated:
                    {
                        Log.outError(LogFilter.Sql, $"{cond.ToString(true)} using deprecated condition type CONDITION_SPAWNMASK.");
                        return false;
                    }
                case ConditionTypes.UnitState:
                    {
                        if (cond.ConditionValue1 > (uint)UnitState.AllStateSupported)
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing UnitState in value1 ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.CreatureType:
                    {
                        if (cond.ConditionValue1 == 0 || cond.ConditionValue1 > (uint)CreatureType.GasCloud)
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing CreatureType in value1 ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.RealmAchievement:
                    {
                        AchievementRecord achievement = CliDB.AchievementStorage.LookupByKey(cond.ConditionValue1);
                        if (achievement == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} has non existing realm first achivement id ({1}), skipped.", cond.ToString(), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.StandState:
                    {
                        bool valid;
                        switch (cond.ConditionValue1)
                        {
                            case 0:
                                valid = cond.ConditionValue2 <= (uint)UnitStandStateType.Submerged;
                                break;
                            case 1:
                                valid = cond.ConditionValue2 <= 1;
                                break;
                            default:
                                valid = false;
                                break;
                        }
                        if (!valid)
                        {
                            Log.outError(LogFilter.Sql, "{0} has non-existing stand state ({1},{2}), skipped.", cond.ToString(true), cond.ConditionValue1, cond.ConditionValue2);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.ObjectiveComplete:
                    {
                        QuestObjective obj = Global.ObjectMgr.GetQuestObjective(cond.ConditionValue1);
                        if (obj == null)
                        {
                            Log.outError(LogFilter.Sql, "{0} points to non-existing quest objective ({1}), skipped.", cond.ToString(true), cond.ConditionValue1);
                            return false;
                        }
                        break;
                    }
                case ConditionTypes.PetType:
                    if (cond.ConditionValue1 >= (1 << (int)PetType.Max))
                    {
                        Log.outError(LogFilter.Sql, "{0} has non-existing pet type {1}, skipped.", cond.ToString(true), cond.ConditionValue1);
                        return false;
                    }
                    break;
                case ConditionTypes.Alive:
                case ConditionTypes.Areaid:
                case ConditionTypes.InstanceInfo:
                case ConditionTypes.TerrainSwap:
                case ConditionTypes.InWater:
                case ConditionTypes.Charmed:
                case ConditionTypes.Taxi:
                    break;
                case ConditionTypes.DifficultyId:
                    if (!CliDB.DifficultyStorage.ContainsKey(cond.ConditionValue1))
                    {
                        Log.outError(LogFilter.Sql, $"{cond.ToString(true)} has non existing difficulty in value1 ({cond.ConditionValue1}), skipped.");
                        return false;
                    }
                    break;
                default:
                    Log.outError(LogFilter.Sql, $"{cond.ToString()} Invalid ConditionType in `condition` table, ignoring.");
                    return false;
            }

            if (cond.ConditionTarget >= cond.GetMaxAvailableConditionTargets())
            {
                Log.outError(LogFilter.Sql, $"{cond.ToString(true)} in `condition` table, has incorrect ConditionTarget set, ignoring.");
                return false;
            }

            if (cond.ConditionValue1 != 0 && !StaticConditionTypeData[(int)cond.ConditionType].HasConditionValue1)
                LogUselessConditionValue(cond, 1, cond.ConditionValue1);
            if (cond.ConditionValue2 != 0 && !StaticConditionTypeData[(int)cond.ConditionType].HasConditionValue2)
                LogUselessConditionValue(cond, 2, cond.ConditionValue2);
            if (cond.ConditionValue3 != 0 && !StaticConditionTypeData[(int)cond.ConditionType].HasConditionValue3)
                LogUselessConditionValue(cond, 3, cond.ConditionValue3);

            return true;
        }

        void LogUselessConditionValue(Condition cond, byte index, uint value)
        {
            Log.outError(LogFilter.Sql, "{0} has useless data in ConditionValue{1} ({2})!", cond.ToString(true), index, value);
        }

        void Clean()
        {
            ConditionReferenceStore.Clear();

            ConditionStore.Clear();
            for (ConditionSourceType i = 0; i < ConditionSourceType.Max; ++i)
                ConditionStore[i] = new MultiMap<uint, Condition>();//add new empty list for SourceType

            VehicleSpellConditionStore.Clear();

            SmartEventConditionStore.Clear();

            SpellClickEventConditionStore.Clear();

            NpcVendorConditionContainerStore.Clear();
        }

        static bool PlayerConditionCompare(int comparisonType, int value1, int value2)
        {
            switch (comparisonType)
            {
                case 1:
                    return value1 == value2;
                case 2:
                    return value1 != value2;
                case 3:
                    return value1 > value2;
                case 4:
                    return value1 >= value2;
                case 5:
                    return value1 < value2;
                case 6:
                    return value1 <= value2;
                default:
                    break;
            }
            return false;
        }

        static bool PlayerConditionLogic(uint logic, bool[] results)
        {
            Cypher.Assert(results.Length < 16, "Logic array size must be equal to or less than 16");

            for (var i = 0; i < results.Length; ++i)
            {
                if (Convert.ToBoolean((logic >> (16 + i)) & 1))
                    results[i] ^= true;
            }

            bool result = results[0];
            for (var i = 1; i < results.Length; ++i)
            {
                switch ((logic >> (2 * (i - 1))) & 3)
                {
                    case 1:
                        result = result && results[i];
                        break;
                    case 2:
                        result = result || results[i];
                        break;
                    default:
                        break;
                }
            }

            return result;
        }

        public static uint GetPlayerConditionLfgValue(Player player, PlayerConditionLfgStatus status)
        {
            if (player.GetGroup() == null)
                return 0;

            switch (status)
            {
                case PlayerConditionLfgStatus.InLFGDungeon:
                    return Global.LFGMgr.InLfgDungeonMap(player.GetGUID(), player.GetMapId(), player.GetMap().GetDifficultyID()) ? 1 : 0u;
                case PlayerConditionLfgStatus.InLFGRandomDungeon:
                    return Global.LFGMgr.InLfgDungeonMap(player.GetGUID(), player.GetMapId(), player.GetMap().GetDifficultyID()) &&
                        Global.LFGMgr.SelectedRandomLfgDungeon(player.GetGUID()) ? 1 : 0u;
                case PlayerConditionLfgStatus.InLFGFirstRandomDungeon:
                    {
                        if (!Global.LFGMgr.InLfgDungeonMap(player.GetGUID(), player.GetMapId(), player.GetMap().GetDifficultyID()))
                            return 0;

                        uint selectedRandomDungeon = Global.LFGMgr.GetSelectedRandomDungeon(player.GetGUID());
                        if (selectedRandomDungeon == 0)
                            return 0;

                        DungeonFinding.LfgReward reward = Global.LFGMgr.GetRandomDungeonReward(selectedRandomDungeon, player.GetLevel());
                        if (reward != null)
                        {
                            Quest quest = Global.ObjectMgr.GetQuestTemplate(reward.firstQuest);
                            if (quest != null)
                                if (player.CanRewardQuest(quest, false))
                                    return 1;
                        }
                        return 0;
                    }
                case PlayerConditionLfgStatus.PartialClear:
                    break;
                case PlayerConditionLfgStatus.StrangerCount:
                    break;
                case PlayerConditionLfgStatus.VoteKickCount:
                    break;
                case PlayerConditionLfgStatus.BootCount:
                    break;
                case PlayerConditionLfgStatus.GearDiff:
                    break;
                default:
                    break;
            }

            return 0;
        }

        public static bool IsPlayerMeetingCondition(Player player, PlayerConditionRecord condition)
        {
            ContentTuningLevels? levels = Global.DB2Mgr.GetContentTuningData(condition.ContentTuningID, player.m_playerData.CtrOptions.GetValue().ContentTuningConditionMask);
            if (levels.HasValue)
            {
                byte minLevel = (byte)(condition.Flags.HasAnyFlag(0x800) ? levels.Value.MinLevelWithDelta : levels.Value.MinLevel);
                byte maxLevel = 0;
                if (!condition.Flags.HasAnyFlag(0x20))
                    maxLevel = (byte)(condition.Flags.HasAnyFlag(0x800) ? levels.Value.MaxLevelWithDelta : levels.Value.MaxLevel);
                if (condition.Flags.HasAnyFlag(0x80))
                {
                    if (minLevel != 0 && player.GetLevel() >= minLevel && (maxLevel == 0 || player.GetLevel() <= maxLevel))
                        return false;

                    if (maxLevel != 0 && player.GetLevel() <= maxLevel && (minLevel == 0 || player.GetLevel() >= minLevel))
                        return false;
                }
                else
                {
                    if (minLevel != 0 && player.GetLevel() < minLevel)
                        return false;

                    if (maxLevel != 0 && player.GetLevel() > maxLevel)
                        return false;
                }
            }

            if (condition.RaceMask != 0 && !Convert.ToBoolean(SharedConst.GetMaskForRace(player.GetRace()) & condition.RaceMask))
                return false;

            if (condition.ClassMask != 0 && !Convert.ToBoolean(player.GetClassMask() & condition.ClassMask))
                return false;

            if (condition.Gender >= 0 && (int)player.GetGender() != condition.Gender)
                return false;

            if (condition.NativeGender >= 0 && player.GetNativeSex() != (Gender)condition.NativeGender)
                return false;

            if (condition.PowerType != -1 && condition.PowerTypeComp != 0)
            {
                int requiredPowerValue = Convert.ToBoolean(condition.Flags & 4) ? player.GetMaxPower((PowerType)condition.PowerType) : condition.PowerTypeValue;
                if (!PlayerConditionCompare(condition.PowerTypeComp, player.GetPower((PowerType)condition.PowerType), requiredPowerValue))
                    return false;
            }

            if (condition.ChrSpecializationIndex >= 0 || condition.ChrSpecializationRole >= 0)
            {
                ChrSpecializationRecord spec = CliDB.ChrSpecializationStorage.LookupByKey(player.GetPrimarySpecialization());
                if (spec != null)
                {
                    if (condition.ChrSpecializationIndex >= 0 && spec.OrderIndex != condition.ChrSpecializationIndex)
                        return false;

                    if (condition.ChrSpecializationRole >= 0 && spec.Role != condition.ChrSpecializationRole)
                        return false;
                }
            }

            bool[] results;

            if (condition.SkillID[0] != 0 || condition.SkillID[1] != 0 || condition.SkillID[2] != 0 || condition.SkillID[3] != 0)
            {
                results = new bool[condition.SkillID.Length];
                for (var i = 0; i < results.Length; ++i)
                    results[i] = true;

                for (var i = 0; i < condition.SkillID.Length; ++i)
                {
                    if (condition.SkillID[i] != 0)
                    {
                        ushort skillValue = player.GetSkillValue((SkillType)condition.SkillID[i]);
                        results[i] = skillValue != 0 && skillValue > condition.MinSkill[i] && skillValue < condition.MaxSkill[i];
                    }
                }

                if (!PlayerConditionLogic(condition.SkillLogic, results))
                    return false;
            }

            if (condition.LanguageID != 0)
            {
                int languageSkill = 0;
                if (player.HasAuraTypeWithMiscvalue(AuraType.ComprehendLanguage, condition.LanguageID))
                    languageSkill = 300;
                else
                {
                    foreach (var languageDesc in Global.LanguageMgr.GetLanguageDescById((Language)condition.LanguageID))
                        languageSkill = Math.Max(languageSkill, player.GetSkillValue((SkillType)languageDesc.SkillId));
                }

                if (condition.MinLanguage != 0 && languageSkill < condition.MinLanguage)
                    return false;

                if (condition.MaxLanguage != 0 && languageSkill > condition.MaxLanguage)
                    return false;
            }

            if (condition.MinFactionID[0] != 0 && condition.MinFactionID[1] != 0 && condition.MinFactionID[2] != 0 && condition.MaxFactionID != 0)
            {
                if (condition.MinFactionID[0] == 0 && condition.MinFactionID[1] == 0 && condition.MinFactionID[2] == 0)
                {
                    ReputationRank forcedRank = player.GetReputationMgr().GetForcedRankIfAny(condition.MaxFactionID);
                    if (forcedRank != 0)
                    {
                        if ((uint)forcedRank > condition.MaxReputation)
                            return false;
                    }
                    else if ((uint)player.GetReputationRank(condition.MaxFactionID) > condition.MaxReputation)
                        return false;
                }
                else
                {
                    results = new bool[condition.MinFactionID.Length + 1];
                    for (var i = 0; i < results.Length; ++i)
                        results[i] = true;

                    for (var i = 0; i < condition.MinFactionID.Length; ++i)
                    {
                        if (condition.MinFactionID[i] != 0)
                        {
                            ReputationRank forcedRank = player.GetReputationMgr().GetForcedRankIfAny(condition.MinFactionID[i]);
                            if (forcedRank != 0)
                                results[i] = (uint)forcedRank >= condition.MinReputation[i];
                            else
                                results[i] = (uint)player.GetReputationRank(condition.MinFactionID[i]) >= condition.MinReputation[i];
                        }
                    }
                    ReputationRank forcedRank1 = player.GetReputationMgr().GetForcedRankIfAny(condition.MaxFactionID);
                    if (forcedRank1 != 0)
                        results[3] = (uint)forcedRank1 <= condition.MaxReputation;
                    else
                        results[3] = (uint)player.GetReputationRank(condition.MaxFactionID) <= condition.MaxReputation;

                    if (!PlayerConditionLogic(condition.ReputationLogic, results))
                        return false;
                }
            }

            if (condition.PvpMedal != 0 && !Convert.ToBoolean((1 << (condition.PvpMedal - 1)) & player.m_activePlayerData.PvpMedals))
                return false;

            if (condition.LifetimeMaxPVPRank != 0 && player.m_activePlayerData.LifetimeMaxRank != condition.LifetimeMaxPVPRank)
                return false;

            if (condition.MovementFlags[0] != 0 && !Convert.ToBoolean((uint)player.GetUnitMovementFlags() & condition.MovementFlags[0]))
                return false;

            if (condition.MovementFlags[1] != 0 && !Convert.ToBoolean((uint)player.GetUnitMovementFlags2() & condition.MovementFlags[1]))
                return false;

            if (condition.WeaponSubclassMask != 0)
            {
                Item mainHand = player.GetItemByPos(InventorySlots.Bag0, EquipmentSlot.MainHand);
                if (!mainHand || !Convert.ToBoolean((1 << (int)mainHand.GetTemplate().GetSubClass()) & condition.WeaponSubclassMask))
                    return false;
            }

            if (condition.PartyStatus != 0)
            {
                Group group = player.GetGroup();
                switch (condition.PartyStatus)
                {
                    case 1:
                        if (group)
                            return false;
                        break;
                    case 2:
                        if (!group)
                            return false;
                        break;
                    case 3:
                        if (!group || group.IsRaidGroup())
                            return false;
                        break;
                    case 4:
                        if (!group || !group.IsRaidGroup())
                            return false;
                        break;
                    case 5:
                        if (group && group.IsRaidGroup())
                            return false;
                        break;
                    default:
                        break;
                }
            }

            if (condition.PrevQuestID[0] != 0)
            {
                results = new bool[condition.PrevQuestID.Length];
                for (var i = 0; i < results.Length; ++i)
                    results[i] = true;

                for (var i = 0; i < condition.PrevQuestID.Length; ++i)
                {
                    uint questBit = Global.DB2Mgr.GetQuestUniqueBitFlag(condition.PrevQuestID[i]);
                    if (questBit != 0)
                        results[i] = (player.m_activePlayerData.QuestCompleted[((int)questBit - 1) >> 6] & (1ul << (((int)questBit - 1) & 63))) != 0;
                }

                if (!PlayerConditionLogic(condition.PrevQuestLogic, results))
                    return false;
            }

            if (condition.CurrQuestID[0] != 0)
            {
                results = new bool[condition.CurrQuestID.Length];
                for (var i = 0; i < results.Length; ++i)
                    results[i] = true;

                for (var i = 0; i < condition.CurrQuestID.Length; ++i)
                {
                    if (condition.CurrQuestID[i] != 0)
                        results[i] = player.FindQuestSlot(condition.CurrQuestID[i]) != SharedConst.MaxQuestLogSize;
                }

                if (!PlayerConditionLogic(condition.CurrQuestLogic, results))
                    return false;
            }

            if (condition.CurrentCompletedQuestID[0] != 0)
            {
                results = new bool[condition.CurrentCompletedQuestID.Length];
                for (var i = 0; i < results.Length; ++i)
                    results[i] = true;

                for (var i = 0; i < condition.CurrentCompletedQuestID.Length; ++i)
                {
                    if (condition.CurrentCompletedQuestID[i] != 0)
                        results[i] = player.GetQuestStatus(condition.CurrentCompletedQuestID[i]) == QuestStatus.Complete;
                }

                if (!PlayerConditionLogic(condition.CurrentCompletedQuestLogic, results))
                    return false;
            }


            if (condition.SpellID[0] != 0)
            {
                results = new bool[condition.SpellID.Length];
                for (var i = 0; i < results.Length; ++i)
                    results[i] = true;

                for (var i = 0; i < condition.SpellID.Length; ++i)
                {
                    if (condition.SpellID[i] != 0)
                        results[i] = player.HasSpell(condition.SpellID[i]);
                }

                if (!PlayerConditionLogic(condition.SpellLogic, results))
                    return false;
            }

            if (condition.ItemID[0] != 0)
            {
                results = new bool[condition.ItemID.Length];
                for (var i = 0; i < results.Length; ++i)
                    results[i] = true;

                for (var i = 0; i < condition.ItemID.Length; ++i)
                {
                    if (condition.ItemID[i] != 0)
                        results[i] = player.GetItemCount(condition.ItemID[i], condition.ItemFlags != 0) >= condition.ItemCount[i];
                }

                if (!PlayerConditionLogic(condition.ItemLogic, results))
                    return false;
            }

            if (condition.CurrencyID[0] != 0)
            {
                results = new bool[condition.CurrencyID.Length];
                for (var i = 0; i < results.Length; ++i)
                    results[i] = true;

                for (var i = 0; i < condition.CurrencyID.Length; ++i)
                {
                    if (condition.CurrencyID[i] != 0)
                        results[i] = player.GetCurrency(condition.CurrencyID[i]) >= condition.CurrencyCount[i];
                }

                if (!PlayerConditionLogic(condition.CurrencyLogic, results))
                    return false;
            }

            if (condition.Explored[0] != 0 || condition.Explored[1] != 0)
            {
                for (var i = 0; i < condition.Explored.Length; ++i)
                {
                    AreaTableRecord area = CliDB.AreaTableStorage.LookupByKey(condition.Explored[i]);
                    if (area != null)
                        if (area.AreaBit != -1 && !Convert.ToBoolean(player.m_activePlayerData.ExploredZones[area.AreaBit / 64] & (1ul << ((int)area.AreaBit % 64))))
                            return false;
                }
            }

            if (condition.AuraSpellID[0] != 0)
            {
                results = new bool[condition.AuraSpellID.Length];
                for (var i = 0; i < results.Length; ++i)
                    results[i] = true;

                for (var i = 0; i < condition.AuraSpellID.Length; ++i)
                {
                    if (condition.AuraSpellID[i] != 0)
                    {
                        if (condition.AuraStacks[i] != 0)
                            results[i] = player.GetAuraCount(condition.AuraSpellID[i]) >= condition.AuraStacks[i];
                        else
                            results[i] = player.HasAura(condition.AuraSpellID[i]);
                    }
                }

                if (!PlayerConditionLogic(condition.AuraSpellLogic, results))
                    return false;
            }

            if (condition.Time[0] != 0)
            {
                var from = Time.GetUnixTimeFromPackedTime(condition.Time[0]);
                var to = Time.GetUnixTimeFromPackedTime(condition.Time[1]);

                if (GameTime.GetGameTime() < from || GameTime.GetGameTime() > to)
                    return false;
            }

            if (condition.WorldStateExpressionID != 0)
            {
                var worldStateExpression = CliDB.WorldStateExpressionStorage.LookupByKey(condition.WorldStateExpressionID);
                if (worldStateExpression == null)
                    return false;

                if (!IsPlayerMeetingExpression(player, worldStateExpression))
                    return false;
            }

            if (condition.WeatherID != 0)
                if (player.GetMap().GetZoneWeather(player.GetZoneId()) != (WeatherState)condition.WeatherID)
                    return false;

            if (condition.Achievement[0] != 0)
            {
                results = new bool[condition.Achievement.Length];
                for (var i = 0; i < results.Length; ++i)
                    results[i] = true;

                for (var i = 0; i < condition.Achievement.Length; ++i)
                {
                    if (condition.Achievement[i] != 0)
                    {
                        // if (condition.Flags & 2) { any character on account completed it } else { current character only }
                        // TODO: part of accountwide achievements
                        results[i] = player.HasAchieved(condition.Achievement[i]);
                    }
                }

                if (!PlayerConditionLogic(condition.AchievementLogic, results))
                    return false;
            }

            if (condition.LfgStatus[0] != 0)
            {
                results = new bool[condition.LfgStatus.Length];
                for (var i = 0; i < results.Length; ++i)
                    results[i] = true;

                for (int i = 0; i < condition.LfgStatus.Length; ++i)
                    if (condition.LfgStatus[i] != 0)
                        results[i] = PlayerConditionCompare(condition.LfgCompare[i], (int)GetPlayerConditionLfgValue(player, (PlayerConditionLfgStatus)condition.LfgStatus[i]), (int)condition.LfgValue[i]);

                if (!PlayerConditionLogic(condition.LfgLogic, results))
                    return false;
            }

            if (condition.AreaID[0] != 0)
            {
                results = new bool[condition.AreaID.Length];
                for (var i = 0; i < results.Length; ++i)
                    results[i] = true;

                for (var i = 0; i < condition.AreaID.Length; ++i)
                    if (condition.AreaID[i] != 0)
                        results[i] = player.GetAreaId() == condition.AreaID[i] || player.GetZoneId() == condition.AreaID[i];

                if (!PlayerConditionLogic(condition.AreaLogic, results))
                    return false;
            }

            if (condition.MinExpansionLevel != -1 && (int)player.GetSession().GetExpansion() < condition.MinExpansionLevel)
                return false;

            if (condition.MaxExpansionLevel != -1 && (int)player.GetSession().GetExpansion() > condition.MaxExpansionLevel)
                return false;

            if (condition.MinExpansionLevel != -1 && condition.MinExpansionTier != -1 && !player.IsGameMaster()
                && ((condition.MinExpansionLevel == WorldConfig.GetIntValue(WorldCfg.Expansion) && condition.MinExpansionTier > 0) /*TODO: implement tier*/
                || condition.MinExpansionLevel > WorldConfig.GetIntValue(WorldCfg.Expansion)))
                return false;

            if (condition.PhaseID != 0 || condition.PhaseGroupID != 0 || condition.PhaseUseFlags != 0)
                if (!PhasingHandler.InDbPhaseShift(player, (PhaseUseFlagsValues)condition.PhaseUseFlags, condition.PhaseID, condition.PhaseGroupID))
                    return false;

            if (condition.QuestKillID != 0)
            {
                Quest quest = Global.ObjectMgr.GetQuestTemplate(condition.QuestKillID);
                ushort questSlot = player.FindQuestSlot(condition.QuestKillID);

                if (quest != null && player.GetQuestStatus(condition.QuestKillID) != QuestStatus.Complete && questSlot < SharedConst.MaxQuestLogSize)
                {
                    results = new bool[condition.QuestKillMonster.Length];
                    for (var i = 0; i < results.Length; ++i)
                        results[i] = true;

                    for (var i = 0; i < condition.QuestKillMonster.Length; ++i)
                    {
                        if (condition.QuestKillMonster[i] != 0)
                        {
                            var questObjective = quest.Objectives.Find(objective => objective.Type == QuestObjectiveType.Monster && objective.ObjectID == condition.QuestKillMonster[i]);

                            if (questObjective != null)
                                results[i] = player.GetQuestSlotObjectiveData(questSlot, questObjective) >= questObjective.Amount;
                        }
                    }

                    if (!PlayerConditionLogic(condition.QuestKillLogic, results))
                        return false;
                }
            }

            if (condition.MinAvgItemLevel != 0 && Math.Floor(player.m_playerData.AvgItemLevel[0]) < condition.MinAvgItemLevel)
                return false;

            if (condition.MaxAvgItemLevel != 0 && Math.Floor(player.m_playerData.AvgItemLevel[0]) > condition.MaxAvgItemLevel)
                return false;

            if (condition.MinAvgEquippedItemLevel != 0 && Math.Floor(player.m_playerData.AvgItemLevel[1]) < condition.MinAvgEquippedItemLevel)
                return false;

            if (condition.MaxAvgEquippedItemLevel != 0 && Math.Floor(player.m_playerData.AvgItemLevel[1]) > condition.MaxAvgEquippedItemLevel)
                return false;

            if (condition.ModifierTreeID != 0 && !player.ModifierTreeSatisfied(condition.ModifierTreeID))
                return false;

            if (condition.CovenantID != 0 && player.m_playerData.CovenantID != condition.CovenantID)
                return false;

            return true;
        }

        public static bool IsPlayerMeetingExpression(Player player, WorldStateExpressionRecord expression)
        {
            ByteBuffer buffer = new(expression.Expression.ToByteArray());
            if (buffer.GetSize() == 0)
                return false;

            bool enabled = buffer.ReadBool();
            if (!enabled)
                return false;

            bool finalResult = EvalRelOp(buffer, player);
            WorldStateExpressionLogic resultLogic = (WorldStateExpressionLogic)buffer.ReadUInt8();

            while (resultLogic != WorldStateExpressionLogic.None)
            {
                bool secondResult = EvalRelOp(buffer, player);

                switch (resultLogic)
                {
                    case WorldStateExpressionLogic.And:
                        finalResult = finalResult && secondResult;
                        break;
                    case WorldStateExpressionLogic.Or:
                        finalResult = finalResult || secondResult;
                        break;
                    case WorldStateExpressionLogic.Xor:
                        finalResult = finalResult != secondResult;
                        break;
                    default:
                        break;
                }

                if (buffer.GetCurrentStream().Position < buffer.GetSize())
                    break;

                resultLogic = (WorldStateExpressionLogic)buffer.ReadUInt8();
            }

            return finalResult;
        }

        static int EvalSingleValue(ByteBuffer buffer, Player player)
        {
            WorldStateExpressionValueType valueType = (WorldStateExpressionValueType)buffer.ReadUInt8();
            int value = 0;

            switch (valueType)
            {
                case WorldStateExpressionValueType.Constant:
                    {
                        value = buffer.ReadInt32();
                        break;
                    }
                case WorldStateExpressionValueType.WorldState:
                    {
                        uint worldStateId = buffer.ReadUInt32();
                        value = (int)Global.WorldMgr.GetWorldState(worldStateId);
                        break;
                    }
                case WorldStateExpressionValueType.Function:
                    {
                        var functionType = (WorldStateExpressionFunctions)buffer.ReadUInt32();
                        int arg1 = EvalSingleValue(buffer, player);
                        int arg2 = EvalSingleValue(buffer, player);

                        if (functionType >= WorldStateExpressionFunctions.Max)
                            return 0;

                        value = WorldStateExpressionFunction(functionType, player, arg1, arg2);
                        break;
                    }
                default:
                    break;
            }

            return value;
        }

        static int WorldStateExpressionFunction(WorldStateExpressionFunctions functionType, Player player, int arg1, int arg2)
        {
            switch (functionType)
            {
                case WorldStateExpressionFunctions.Random:
                    return (int)RandomHelper.URand(Math.Min(arg1, arg2), Math.Max(arg1, arg2));
                case WorldStateExpressionFunctions.Month:
                    return GameTime.GetDateAndTime().Month + 1;
                case WorldStateExpressionFunctions.Day:
                    return GameTime.GetDateAndTime().Day + 1;
                case WorldStateExpressionFunctions.TimeOfDay:
                    DateTime localTime = GameTime.GetDateAndTime();
                    return localTime.Hour * Time.Minute + localTime.Minute;
                case WorldStateExpressionFunctions.Region:
                    return Global.WorldMgr.GetRealmId().Region;
                case WorldStateExpressionFunctions.ClockHour:
                    int currentHour = GameTime.GetDateAndTime().Hour + 1;
                    return currentHour <= 12 ? (currentHour != 0 ? currentHour : 12) : currentHour - 12;
                case WorldStateExpressionFunctions.OldDifficultyId:
                    var difficulty = CliDB.DifficultyStorage.LookupByKey(player.GetMap().GetDifficultyID());
                    if (difficulty != null)
                        return difficulty.OldEnumValue;

                    return -1;
                case WorldStateExpressionFunctions.HolidayActive:
                    return Global.GameEventMgr.IsHolidayActive((HolidayIds)arg1) ? 1 : 0;
                case WorldStateExpressionFunctions.TimerCurrentTime:
                    return (int)GameTime.GetGameTime();
                case WorldStateExpressionFunctions.WeekNumber:
                    long now = GameTime.GetGameTime();
                    uint raidOrigin = 1135695600;
                    Cfg_RegionsRecord region = CliDB.CfgRegionsStorage.LookupByKey(Global.WorldMgr.GetRealmId().Region);
                    if (region != null)
                        raidOrigin = region.Raidorigin;

                    return (int)(now - raidOrigin) / Time.Week;
                case WorldStateExpressionFunctions.DifficultyId:
                    return (int)player.GetMap().GetDifficultyID();
                case WorldStateExpressionFunctions.WarModeActive:
                    return player.HasPlayerFlag(PlayerFlags.WarModeActive) ? 1 : 0;
                case WorldStateExpressionFunctions.WorldStateExpression:
                    var worldStateExpression = CliDB.WorldStateExpressionStorage.LookupByKey(arg1);
                    if (worldStateExpression != null)
                        return IsPlayerMeetingExpression(player, worldStateExpression) ? 1 : 0;

                    return 0;
                case WorldStateExpressionFunctions.MersenneRandom:
                    if (arg1 == 1)
                        return 1;

                    //todo fix me
                    // init with predetermined seed                      
                    //std::mt19937 mt(arg2? arg2 : 1);
                    //value = mt() % arg1 + 1;
                    return 0;
                case WorldStateExpressionFunctions.None:
                case WorldStateExpressionFunctions.HolidayStart:
                case WorldStateExpressionFunctions.HolidayLeft:
                case WorldStateExpressionFunctions.Unk13:
                case WorldStateExpressionFunctions.Unk14:
                case WorldStateExpressionFunctions.Unk17:
                case WorldStateExpressionFunctions.Unk18:
                case WorldStateExpressionFunctions.Unk19:
                case WorldStateExpressionFunctions.Unk20:
                case WorldStateExpressionFunctions.Unk21:
                case WorldStateExpressionFunctions.KeystoneAffix:
                case WorldStateExpressionFunctions.Unk24:
                case WorldStateExpressionFunctions.Unk25:
                case WorldStateExpressionFunctions.Unk26:
                case WorldStateExpressionFunctions.Unk27:
                case WorldStateExpressionFunctions.KeystoneLevel:
                case WorldStateExpressionFunctions.Unk29:
                case WorldStateExpressionFunctions.Unk30:
                case WorldStateExpressionFunctions.Unk31:
                case WorldStateExpressionFunctions.Unk32:
                case WorldStateExpressionFunctions.Unk34:
                case WorldStateExpressionFunctions.Unk35:
                case WorldStateExpressionFunctions.Unk36:
                case WorldStateExpressionFunctions.UiWidgetData:
                default:
                    return 0;
            }
        }

        static int EvalValue(ByteBuffer buffer, Player player)
        {
            int leftValue = EvalSingleValue(buffer, player);

            WorldStateExpressionOperatorType operatorType = (WorldStateExpressionOperatorType)buffer.ReadUInt8();
            if (operatorType == WorldStateExpressionOperatorType.None)
                return leftValue;

            int rightValue = EvalSingleValue(buffer, player);

            switch (operatorType)
            {
                case WorldStateExpressionOperatorType.Sum:
                    return leftValue + rightValue;
                case WorldStateExpressionOperatorType.Substraction:
                    return leftValue - rightValue;
                case WorldStateExpressionOperatorType.Multiplication:
                    return leftValue * rightValue;
                case WorldStateExpressionOperatorType.Division:
                    return rightValue == 0 ? 0 : leftValue / rightValue;
                case WorldStateExpressionOperatorType.Remainder:
                    return rightValue == 0 ? 0 : leftValue % rightValue;
                default:
                    break;
            }

            return leftValue;
        }

        static bool EvalRelOp(ByteBuffer buffer, Player player)
        {
            int leftValue = EvalValue(buffer, player);

            WorldStateExpressionComparisonType compareLogic = (WorldStateExpressionComparisonType)buffer.ReadUInt8();
            if (compareLogic == WorldStateExpressionComparisonType.None)
                return leftValue != 0;

            int rightValue = EvalValue(buffer, player);

            switch (compareLogic)
            {
                case WorldStateExpressionComparisonType.Equal:
                    return leftValue == rightValue;
                case WorldStateExpressionComparisonType.NotEqual:
                    return leftValue != rightValue;
                case WorldStateExpressionComparisonType.Less:
                    return leftValue < rightValue;
                case WorldStateExpressionComparisonType.LessOrEqual:
                    return leftValue <= rightValue;
                case WorldStateExpressionComparisonType.Greater:
                    return leftValue > rightValue;
                case WorldStateExpressionComparisonType.GreaterOrEqual:
                    return leftValue >= rightValue;
                default:
                    break;
            }

            return false;
        }

        Dictionary<ConditionSourceType, MultiMap<uint, Condition>> ConditionStore = new();
        MultiMap<uint, Condition> ConditionReferenceStore = new();
        Dictionary<uint, MultiMap<uint, Condition>> VehicleSpellConditionStore = new();
        Dictionary<uint, MultiMap<uint, Condition>> SpellClickEventConditionStore = new();
        Dictionary<uint, MultiMap<uint, Condition>> NpcVendorConditionContainerStore = new();
        Dictionary<Tuple<int, uint>, MultiMap<uint, Condition>> SmartEventConditionStore = new();

        public string[] StaticSourceTypeData =
        {
            "None",
            "Creature Loot",
            "Disenchant Loot",
            "Fishing Loot",
            "GameObject Loot",
            "Item Loot",
            "Mail Loot",
            "Milling Loot",
            "Pickpocketing Loot",
            "Prospecting Loot",
            "Reference Loot",
            "Skinning Loot",
            "Spell Loot",
            "Spell Impl. Target",
            "Gossip Menu",
            "Gossip Menu Option",
            "Creature Vehicle",
            "Spell Expl. Target",
            "Spell Click Event",
            "Quest Accept",
            "Quest Show Mark",
            "Vehicle Spell",
            "SmartScript",
            "Npc Vendor",
            "Spell Proc",
            "Terrain Swap",
            "Phase"
        };

        public ConditionTypeInfo[] StaticConditionTypeData =
        {
            new ConditionTypeInfo("None",                 false,false, false),
            new ConditionTypeInfo("Aura",                 true, true,  true ),
            new ConditionTypeInfo("Item Stored",          true, true,  true ),
            new ConditionTypeInfo("Item Equipped",        true, false, false),
            new ConditionTypeInfo("Zone",                 true, false, false),
            new ConditionTypeInfo("Reputation",           true, true,  false),
            new ConditionTypeInfo("Team",                 true, false, false),
            new ConditionTypeInfo("Skill",                true, true,  false),
            new ConditionTypeInfo("Quest Rewarded",       true, false, false),
            new ConditionTypeInfo("Quest Taken",          true, false, false),
            new ConditionTypeInfo("Drunken",              true, false, false),
            new ConditionTypeInfo("WorldState",           true, true,  false),
            new ConditionTypeInfo("Active Event",         true, false, false),
            new ConditionTypeInfo("Instance Info",        true, true,  true ),
            new ConditionTypeInfo("Quest None",           true, false, false),
            new ConditionTypeInfo("Class",                true, false, false),
            new ConditionTypeInfo("Race",                 true, false, false),
            new ConditionTypeInfo("Achievement",          true, false, false),
            new ConditionTypeInfo("Title",                true, false, false),
            new ConditionTypeInfo("SpawnMask",            true, false, false),
            new ConditionTypeInfo("Gender",               true, false, false),
            new ConditionTypeInfo("Unit State",           true, false, false),
            new ConditionTypeInfo("Map",                  true, false, false),
            new ConditionTypeInfo("Area",                 true, false, false),
            new ConditionTypeInfo("CreatureType",         true, false, false),
            new ConditionTypeInfo("Spell Known",          true, false, false),
            new ConditionTypeInfo("Phase",                true, false, false),
            new ConditionTypeInfo("Level",                true, true,  false),
            new ConditionTypeInfo("Quest Completed",      true, false, false),
            new ConditionTypeInfo("Near Creature",        true, true,  true),
            new ConditionTypeInfo("Near GameObject",      true, true,  false),
            new ConditionTypeInfo("Object Entry or Guid", true, true,  true ),
            new ConditionTypeInfo("Object TypeMask",      true, false, false),
            new ConditionTypeInfo("Relation",             true, true,  false),
            new ConditionTypeInfo("Reaction",             true, true,  false),
            new ConditionTypeInfo("Distance",             true, true,  true ),
            new ConditionTypeInfo("Alive",                false,false, false),
            new ConditionTypeInfo("Health Value",         true, true,  false),
            new ConditionTypeInfo("Health Pct",           true, true,  false),
            new ConditionTypeInfo("Realm Achievement",    true, false, false),
            new ConditionTypeInfo("In Water",             false,false, false),
            new ConditionTypeInfo("Terrain Swap",         true, false, false),
            new ConditionTypeInfo("Sit/stand state",      true, true, false),
            new ConditionTypeInfo("Daily Quest Completed",true, false, false),
            new ConditionTypeInfo("Charmed",              false,false, false),
            new ConditionTypeInfo("Pet type",             true, false, false),
            new ConditionTypeInfo("On Taxi",              false,false, false),
            new ConditionTypeInfo("Quest state mask",     true, true, false),
            new ConditionTypeInfo("Objective Complete",   true, false, false),
            new ConditionTypeInfo("Map Difficulty",       true, false, false)
        };

        public struct ConditionTypeInfo
        {
            public ConditionTypeInfo(string name, params bool[] args)
            {
                Name = name;
                HasConditionValue1 = args[0];
                HasConditionValue2 = args[1];
                HasConditionValue3 = args[2];
            }

            public string Name;
            public bool HasConditionValue1;
            public bool HasConditionValue2;
            public bool HasConditionValue3;
        }
    }
}
