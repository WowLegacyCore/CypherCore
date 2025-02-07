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
using Framework.IO;
using Game.DataStorage;
using Game.Entities;
using Game.Spells;
using System;
using System.Text;
using System.Collections.Generic;

namespace Game.Chat
{
    [CommandGroup("lookup", RBACPermissions.CommandLookup, true)]
    class LookupCommands
    {
        static int maxResults = 50;

        [Command("area", RBACPermissions.CommandLookupArea, true)]
        static bool Area(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            string namePart = args.NextString().ToLower();

            bool found = false;
            uint count = 0;

            // Search in AreaTable.dbc
            foreach (var areaEntry in CliDB.AreaTableStorage.Values)
            {
                Locale locale = handler.GetSessionDbcLocale();
                string name = areaEntry.AreaName[locale];
                if (string.IsNullOrEmpty(name))
                    continue;

                if (!name.Like(namePart))
                {
                    locale = 0;
                    for (; locale < Locale.Total; ++locale)
                    {
                        if (locale == handler.GetSessionDbcLocale())
                            continue;

                        name = areaEntry.AreaName[locale];
                        if (name.IsEmpty())
                            continue;

                        if (name.Like(namePart))
                            break;
                    }
                }

                if (locale < Locale.Total)
                {
                    if (maxResults != 0 && count++ == maxResults)
                    {
                        handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                        return true;
                    }

                    // send area in "id - [name]" format
                    string ss = "";
                    if (handler.GetSession() != null)
                        ss += areaEntry.Id + " - |cffffffff|Harea:" + areaEntry.Id + "|h[" + name + "]|h|r";
                    else
                        ss += areaEntry.Id + " - " + name;

                    handler.SendSysMessage(ss);

                    if (!found)
                        found = true;
                }
            }

            if (!found)
                handler.SendSysMessage(CypherStrings.CommandNoareafound);

            return true;
        }

        [Command("creature", RBACPermissions.CommandLookupCreature, true)]
        static bool Creature(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            string namePart = args.NextString().ToLower();

            bool found = false;
            uint count = 0;

            var ctc = Global.ObjectMgr.GetCreatureTemplates();
            foreach (var template in ctc)
            {
                uint id = template.Value.Entry;
                byte localeIndex = handler.GetSessionDbLocaleIndex();
                CreatureLocale creatureLocale = Global.ObjectMgr.GetCreatureLocale(id);
                if (creatureLocale != null)
                {
                    if (creatureLocale.Name.Length > localeIndex && !string.IsNullOrEmpty(creatureLocale.Name[localeIndex]))
                    {
                        string name = creatureLocale.Name[localeIndex];

                        if (name.Like(namePart))
                        {
                            if (maxResults != 0 && count++ == maxResults)
                            {
                                handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                                return true;
                            }

                            if (handler.GetSession() != null)
                                handler.SendSysMessage(CypherStrings.CreatureEntryListChat, id, id, name);
                            else
                                handler.SendSysMessage(CypherStrings.CreatureEntryListConsole, id, name);

                            if (!found)
                                found = true;

                            continue;
                        }
                    }
                }

                string _name = template.Value.Name;
                if (string.IsNullOrEmpty(_name))
                    continue;

                if (_name.Like(namePart))
                {
                    if (maxResults != 0 && count++ == maxResults)
                    {
                        handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                        return true;
                    }

                    if (handler.GetSession() != null)
                        handler.SendSysMessage(CypherStrings.CreatureEntryListChat, id, id, _name);
                    else
                        handler.SendSysMessage(CypherStrings.CreatureEntryListConsole, id, _name);

                    if (!found)
                        found = true;
                }
            }

            if (!found)
                handler.SendSysMessage(CypherStrings.CommandNocreaturefound);

            return true;
        }

        [Command("event", RBACPermissions.CommandLookupEvent, true)]
        static bool Event(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            string namePart = args.NextString().ToLower();

            bool found = false;
            uint count = 0;

            var events = Global.GameEventMgr.GetEventMap();
            var activeEvents = Global.GameEventMgr.GetActiveEventList();

            for (ushort id = 0; id < events.Length; ++id)
            {
                GameEventData eventData = events[id];

                string descr = eventData.description;
                if (string.IsNullOrEmpty(descr))
                    continue;

                if (descr.Like(namePart))
                {
                    if (maxResults != 0 && count++ == maxResults)
                    {
                        handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                        return true;
                    }

                    string active = activeEvents.Contains(id) ? handler.GetCypherString(CypherStrings.Active) : "";

                    if (handler.GetSession() != null)
                        handler.SendSysMessage(CypherStrings.EventEntryListChat, id, id, eventData.description, active);
                    else
                        handler.SendSysMessage(CypherStrings.EventEntryListConsole, id, eventData.description, active);

                    if (!found)
                        found = true;
                }
            }

            if (!found)
                handler.SendSysMessage(CypherStrings.Noeventfound);

            return true;
        }

        [Command("faction", RBACPermissions.CommandLookupFaction, true)]
        static bool Faction(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            // Can be NULL at console call
            Player target = handler.GetSelectedPlayer();

            string namePart = args.NextString().ToLower();

            bool found = false;
            uint count = 0;


            foreach (var factionEntry in CliDB.FactionStorage.Values)
            {
                FactionState factionState = target ? target.GetReputationMgr().GetState(factionEntry) : null;

                Locale locale = handler.GetSessionDbcLocale();
                string name = factionEntry.Name[locale];
                if (string.IsNullOrEmpty(name))
                    continue;

                if (!name.Like(namePart))
                {
                    locale = 0;
                    for (; locale < Locale.Total; ++locale)
                    {
                        if (locale == handler.GetSessionDbcLocale())
                            continue;

                        name = factionEntry.Name[locale];
                        if (name.IsEmpty())
                            continue;

                        if (name.Like(namePart))
                            break;
                    }
                }

                if (locale < Locale.Total)
                {
                    if (maxResults != 0 && count++ == maxResults)
                    {
                        handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                        return true;
                    }

                    // send faction in "id - [faction] rank reputation [visible] [at war] [own team] [unknown] [invisible] [inactive]" format
                    // or              "id - [faction] [no reputation]" format
                    StringBuilder ss = new();
                    if (handler.GetSession() != null)
                        ss.AppendFormat("{0} - |cffffffff|Hfaction:{0}|h[{1}]|h|r", factionEntry.Id, name);
                    else
                        ss.Append(factionEntry.Id + " - " + name);

                    if (factionState != null) // and then target != NULL also
                    {
                        uint index = target.GetReputationMgr().GetReputationRankStrIndex(factionEntry);
                        string rankName = handler.GetCypherString((CypherStrings)index);

                        ss.AppendFormat(" {0}|h|r ({1})", rankName, target.GetReputationMgr().GetReputation(factionEntry));

                        if (factionState.Flags.HasFlag(ReputationFlags.Visible))
                            ss.Append(handler.GetCypherString(CypherStrings.FactionVisible));
                        if (factionState.Flags.HasFlag(ReputationFlags.AtWar))
                            ss.Append(handler.GetCypherString(CypherStrings.FactionAtwar));
                        if (factionState.Flags.HasFlag(ReputationFlags.Peaceful))
                            ss.Append(handler.GetCypherString(CypherStrings.FactionPeaceForced));
                        if (factionState.Flags.HasFlag(ReputationFlags.Hidden))
                            ss.Append(handler.GetCypherString(CypherStrings.FactionHidden));
                        if (factionState.Flags.HasFlag(ReputationFlags.Header))
                            ss.Append(handler.GetCypherString(CypherStrings.FactionInvisibleForced));
                        if (factionState.Flags.HasFlag(ReputationFlags.Inactive))
                            ss.Append(handler.GetCypherString(CypherStrings.FactionInactive));
                    }
                    else
                        ss.Append(handler.GetCypherString(CypherStrings.FactionNoreputation));

                    handler.SendSysMessage(ss.ToString());

                    if (!found)
                        found = true;
                }

            }

            if (!found)
                handler.SendSysMessage(CypherStrings.CommandFactionNotfound);
            return true;
        }

        [Command("item", RBACPermissions.CommandLookupItem, true)]
        static bool Item(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            string namePart = args.NextString("");

            bool found = false;
            uint count = 0;

            // Search in ItemSparse
            var its = Global.ObjectMgr.GetItemTemplates();
            foreach (var template in its.Values)
            {
                string name = template.GetName(handler.GetSessionDbcLocale());
                if (string.IsNullOrEmpty(name))
                    continue;

                if (name.Like(namePart))
                {
                    if (maxResults != 0 && count++ == maxResults)
                    {
                        handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                        return true;
                    }

                    if (handler.GetSession() != null)
                        handler.SendSysMessage(CypherStrings.ItemListChat, template.GetId(), template.GetId(), name);
                    else
                        handler.SendSysMessage(CypherStrings.ItemListConsole, template.GetId(), name);

                    if (!found)
                        found = true;
                }
            }

            if (!found)
                handler.SendSysMessage(CypherStrings.CommandNoitemfound);

            return true;
        }

        [Command("itemset", RBACPermissions.CommandLookupItemset, true)]
        static bool ItemSet(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            string namePart = args.NextString().ToLower();

            bool found = false;
            uint count = 0;

            // Search in ItemSet.dbc
            foreach (var set in CliDB.ItemSetStorage.Values)
            {
                Locale locale = handler.GetSessionDbcLocale();
                string name = set.Name[locale];
                if (name.IsEmpty())
                    continue;

                if (!name.Like(namePart))
                {
                    locale = 0;
                    for (; locale < Locale.Total; ++locale)
                    {
                        if (locale == handler.GetSessionDbcLocale())
                            continue;

                        name = set.Name[locale];
                        if (name.IsEmpty())
                            continue;

                        if (name.Like(namePart))
                            break;
                    }
                }

                if (locale < Locale.Total)
                {
                    if (maxResults != 0 && count++ == maxResults)
                    {
                        handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                        return true;
                    }

                    // send item set in "id - [namedlink locale]" format
                    if (handler.GetSession() != null)
                        handler.SendSysMessage(CypherStrings.ItemsetListChat, set.Id, set.Id, name, "");
                    else
                        handler.SendSysMessage(CypherStrings.ItemsetListConsole, set.Id, name, "");

                    if (!found)
                        found = true;
                }
            }
            if (!found)
                handler.SendSysMessage(CypherStrings.CommandNoitemsetfound);

            return true;
        }

        [Command("object", RBACPermissions.CommandLookupObject, true)]
        static bool Object(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            string namePart = args.NextString();

            bool found = false;
            uint count = 0;

            var gotc = Global.ObjectMgr.GetGameObjectTemplates();
            foreach (var template in gotc.Values)
            {
                byte localeIndex = handler.GetSessionDbLocaleIndex();

                GameObjectLocale objectLocalte = Global.ObjectMgr.GetGameObjectLocale(template.entry);
                if (objectLocalte != null)
                {
                    if (objectLocalte.Name.Length > localeIndex && !string.IsNullOrEmpty(objectLocalte.Name[localeIndex]))
                    {
                        string name = objectLocalte.Name[localeIndex];

                        if (name.Like(namePart))
                        {
                            if (maxResults != 0 && count++ == maxResults)
                            {
                                handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                                return true;
                            }

                            if (handler.GetSession() != null)
                                handler.SendSysMessage(CypherStrings.GoEntryListChat, template.entry, template.entry, name);
                            else
                                handler.SendSysMessage(CypherStrings.GoEntryListConsole, template.entry, name);

                            if (!found)
                                found = true;

                            continue;
                        }
                    }
                }

                string _name = template.name;
                if (string.IsNullOrEmpty(_name))
                    continue;

                if (_name.Like(namePart))
                {
                    if (maxResults != 0 && count++ == maxResults)
                    {
                        handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                        return true;
                    }

                    if (handler.GetSession() != null)
                        handler.SendSysMessage(CypherStrings.GoEntryListChat, template.entry, template.entry, _name);
                    else
                        handler.SendSysMessage(CypherStrings.GoEntryListConsole, template.entry, _name);

                    if (!found)
                        found = true;
                }
            }

            if (!found)
                handler.SendSysMessage(CypherStrings.CommandNogameobjectfound);

            return true;
        }

        [Command("quest", RBACPermissions.CommandLookupQuest, true)]
        static bool Quest(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            // can be NULL at console call
            Player target = handler.GetSelectedPlayer();

            string namePart = args.NextString().ToLower();

            bool found = false;
            uint count = 0;

            var qTemplates = Global.ObjectMgr.GetQuestTemplates();
            foreach (var qInfo in qTemplates.Values)
            {
                int localeIndex = handler.GetSessionDbLocaleIndex();
                QuestTemplateLocale questLocale = Global.ObjectMgr.GetQuestLocale(qInfo.Id);
                if (questLocale != null)
                {
                    if (questLocale.LogTitle.Length > localeIndex && !questLocale.LogTitle[localeIndex].IsEmpty())
                    {
                        string title = questLocale.LogTitle[localeIndex];

                        if (title.Like(namePart))
                        {
                            if (maxResults != 0 && count++ == maxResults)
                            {
                                handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                                return true;
                            }

                            string statusStr = "";

                            if (target)
                            {
                                switch (target.GetQuestStatus(qInfo.Id))
                                {
                                    case QuestStatus.Complete:
                                        statusStr = handler.GetCypherString(CypherStrings.CommandQuestComplete);
                                        break;
                                    case QuestStatus.Incomplete:
                                        statusStr = handler.GetCypherString(CypherStrings.CommandQuestActive);
                                        break;
                                    case QuestStatus.Rewarded:
                                        statusStr = handler.GetCypherString(CypherStrings.CommandQuestRewarded);
                                        break;
                                    default:
                                        break;
                                }
                            }

                            if (handler.GetSession() != null)
                            {
                                int maxLevel = 0;
                                var questLevels = Global.DB2Mgr.GetContentTuningData(qInfo.ContentTuningId, handler.GetSession().GetPlayer().m_playerData.CtrOptions.GetValue().ContentTuningConditionMask);
                                if (questLevels.HasValue)
                                    maxLevel = questLevels.Value.MaxLevel;

                                int scalingFactionGroup = 0;
                                ContentTuningRecord contentTuning = CliDB.ContentTuningStorage.LookupByKey(qInfo.ContentTuningId);
                                if (contentTuning != null)
                                    scalingFactionGroup = contentTuning.GetScalingFactionGroup();

                                handler.SendSysMessage(CypherStrings.QuestListChat, qInfo.Id, qInfo.Id,
                                    handler.GetSession().GetPlayer().GetQuestLevel(qInfo),
                                    handler.GetSession().GetPlayer().GetQuestMinLevel(qInfo),
                                    maxLevel, scalingFactionGroup,
                                    title, statusStr);
                            }
                            else
                                handler.SendSysMessage(CypherStrings.QuestListConsole, qInfo.Id, title, statusStr);

                            if (!found)
                                found = true;

                            continue;
                        }
                    }
                }

                string _title = qInfo.LogTitle;
                if (string.IsNullOrEmpty(_title))
                    continue;

                if (_title.Like(namePart))
                {
                    if (maxResults != 0 && count++ == maxResults)
                    {
                        handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                        return true;
                    }

                    string statusStr = "";

                    if (target)
                    {
                        QuestStatus status = target.GetQuestStatus(qInfo.Id);

                        switch (status)
                        {
                            case QuestStatus.Complete:
                                statusStr = handler.GetCypherString(CypherStrings.CommandQuestComplete);
                                break;
                            case QuestStatus.Incomplete:
                                statusStr = handler.GetCypherString(CypherStrings.CommandQuestActive);
                                break;
                            case QuestStatus.Rewarded:
                                statusStr = handler.GetCypherString(CypherStrings.CommandQuestRewarded);
                                break;
                            default:
                                break;
                        }
                    }

                    if (handler.GetSession() != null)
                    {
                        int maxLevel = 0;
                        var questLevels = Global.DB2Mgr.GetContentTuningData(qInfo.ContentTuningId, handler.GetSession().GetPlayer().m_playerData.CtrOptions.GetValue().ContentTuningConditionMask);
                        if (questLevels.HasValue)
                            maxLevel = questLevels.Value.MaxLevel;

                        int scalingFactionGroup = 0;
                        ContentTuningRecord contentTuning = CliDB.ContentTuningStorage.LookupByKey(qInfo.ContentTuningId);
                        if (contentTuning != null)
                            scalingFactionGroup = contentTuning.GetScalingFactionGroup();

                        handler.SendSysMessage(CypherStrings.QuestListChat, qInfo.Id, qInfo.Id,
                            handler.GetSession().GetPlayer().GetQuestLevel(qInfo),
                            handler.GetSession().GetPlayer().GetQuestMinLevel(qInfo),
                            maxLevel, scalingFactionGroup,
                            _title, statusStr);
                    }
                    else
                        handler.SendSysMessage(CypherStrings.QuestListConsole, qInfo.Id, _title, statusStr);

                    if (!found)
                        found = true;
                }
            }

            if (!found)
                handler.SendSysMessage(CypherStrings.CommandNoquestfound);

            return true;
        }

        [Command("skill", RBACPermissions.CommandLookupSkill, true)]
        static bool Skill(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            // can be NULL in console call
            Player target = handler.GetSelectedPlayer();

            string namePart = args.NextString();

            bool found = false;
            uint count = 0;
            // Search in SkillLine.dbc
            foreach (var skillInfo in CliDB.SkillLineStorage.Values)
            {
                Locale locale = handler.GetSessionDbcLocale();
                string name = skillInfo.DisplayName[locale];
                if (string.IsNullOrEmpty(name))
                    continue;

                if (!name.Like(namePart))
                {
                    locale = 0;
                    for (; locale < Locale.Total; ++locale)
                    {
                        if (locale == handler.GetSessionDbcLocale())
                            continue;

                        name = skillInfo.DisplayName[locale];
                        if (name.IsEmpty())
                            continue;

                        if (name.Like(namePart))
                            break;
                    }
                }

                if (locale < Locale.Total)
                {
                    if (maxResults != 0 && count++ == maxResults)
                    {
                        handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                        return true;
                    }

                    string valStr = "";
                    string knownStr = "";
                    if (target && target.HasSkill((SkillType)skillInfo.Id))
                    {
                        knownStr = handler.GetCypherString(CypherStrings.Known);
                        uint curValue = target.GetPureSkillValue((SkillType)skillInfo.Id);
                        uint maxValue = target.GetPureMaxSkillValue((SkillType)skillInfo.Id);
                        uint permValue = target.GetSkillPermBonusValue(skillInfo.Id);
                        uint tempValue = target.GetSkillTempBonusValue(skillInfo.Id);

                        string valFormat = handler.GetCypherString(CypherStrings.SkillValues);
                        valStr = string.Format(valFormat, curValue, maxValue, permValue, tempValue);
                    }

                    // send skill in "id - [namedlink locale]" format
                    if (handler.GetSession() != null)
                        handler.SendSysMessage(CypherStrings.SkillListChat, skillInfo.Id, skillInfo.Id, name, "", knownStr, valStr);
                    else
                        handler.SendSysMessage(CypherStrings.SkillListConsole, skillInfo.Id, name, "", knownStr, valStr);

                    if (!found)
                        found = true;
                }

            }
            if (!found)
                handler.SendSysMessage(CypherStrings.CommandNoskillfound);

            return true;
        }

        [Command("taxinode", RBACPermissions.CommandLookupTaxinode, true)]
        static bool TaxiNode(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            string namePart = args.NextString();

            bool found = false;
            uint count = 0;
            Locale locale = handler.GetSessionDbcLocale();

            // Search in TaxiNodes.dbc
            foreach (var nodeEntry in CliDB.TaxiNodesStorage.Values)
            {
                string name = nodeEntry.Name[locale];
                if (string.IsNullOrEmpty(name))
                    continue;

                if (!name.Like(namePart))
                    continue;

                if (maxResults != 0 && count++ == maxResults)
                {
                    handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                    return true;
                }

                // send taxinode in "id - [name] (Map:m X:x Y:y Z:z)" format
                if (handler.GetSession() != null)
                    handler.SendSysMessage(CypherStrings.TaxinodeEntryListChat, nodeEntry.Id, nodeEntry.Id, name, "",
                        nodeEntry.ContinentID, nodeEntry.Pos.X, nodeEntry.Pos.Y, nodeEntry.Pos.Z);
                else
                    handler.SendSysMessage(CypherStrings.TaxinodeEntryListConsole, nodeEntry.Id, name, "",
                        nodeEntry.ContinentID, nodeEntry.Pos.X, nodeEntry.Pos.Y, nodeEntry.Pos.Z);

                if (!found)
                    found = true;

            }
            if (!found)
                handler.SendSysMessage(CypherStrings.CommandNotaxinodefound);

            return true;
        }

        [Command("tele", RBACPermissions.CommandLookupTele, true)]
        static bool Tele(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
            {
                handler.SendSysMessage(CypherStrings.CommandTeleParameter);
                return false;
            }

            string namePart = args.NextString().ToLower();

            StringBuilder reply = new();
            uint count = 0;
            bool limitReached = false;

            foreach (var tele in Global.ObjectMgr.gameTeleStorage)
            {
                if (!tele.Value.name.Like(namePart))
                    continue;

                if (maxResults != 0 && count++ == maxResults)
                {
                    limitReached = true;
                    break;
                }

                if (handler.GetPlayer() != null)
                    reply.AppendFormat("  |cffffffff|Htele:{0}|h[{1}]|h|r\n", tele.Key, tele.Value.name);
                else
                    reply.AppendFormat("  {0} : {1}\n", tele.Key, tele.Value.name);
            }

            if (reply.Capacity == 0)
                handler.SendSysMessage(CypherStrings.CommandTeleNolocation);
            else
                handler.SendSysMessage(CypherStrings.CommandTeleLocation, reply.ToString());

            if (limitReached)
                handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);

            return true;
        }

        [Command("title", RBACPermissions.CommandLookupTitle, true)]
        static bool Title(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            // can be NULL in console call
            Player target = handler.GetSelectedPlayer();

            // title name have single string arg for player name
            string targetName = target ? target.GetName() : "NAME";

            string namePart = args.NextString();

            uint counter = 0;                                     // Counter for figure out that we found smth.
            // Search in CharTitles.dbc
            foreach (var titleInfo in CliDB.CharTitlesStorage.Values)
            {
                for (Gender gender = Gender.Male; gender <= Gender.Female; ++gender)
                {
                    if (target && target.GetGender() != gender)
                        continue;

                    Locale locale = handler.GetSessionDbcLocale();
                    string name = gender == Gender.Male ? titleInfo.Name[locale]: titleInfo.Name1[locale];
                    if (string.IsNullOrEmpty(name))
                        continue;

                    if (!name.Like(namePart))
                    {
                        locale = 0;
                        for (; locale < Locale.Total; ++locale)
                        {
                            if (locale == handler.GetSessionDbcLocale())
                                continue;

                            name = (gender == Gender.Male ? titleInfo.Name : titleInfo.Name1)[locale];
                            if (name.IsEmpty())
                                continue;

                            if (name.Like(namePart))
                                break;
                        }
                    }

                    if (locale < Locale.Total)
                    {
                        if (maxResults != 0 && counter == maxResults)
                        {
                            handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                            return true;
                        }

                        string knownStr = target && target.HasTitle(titleInfo) ? handler.GetCypherString(CypherStrings.Known) : "";

                        string activeStr = target && target.m_playerData.PlayerTitle == titleInfo.MaskID
                            ? handler.GetCypherString(CypherStrings.Active) : "";

                        string titleNameStr = string.Format(name.ConvertFormatSyntax(), targetName);

                        // send title in "id (idx:idx) - [namedlink locale]" format
                        if (handler.GetSession() != null)
                            handler.SendSysMessage(CypherStrings.TitleListChat, titleInfo.Id, titleInfo.MaskID, titleInfo.Id, titleNameStr, "", knownStr, activeStr);
                        else
                            handler.SendSysMessage(CypherStrings.TitleListConsole, titleInfo.Id, titleInfo.MaskID, titleNameStr, "", knownStr, activeStr);

                        ++counter;
                    }
                }

            }
            if (counter == 0)  // if counter == 0 then we found nth
                handler.SendSysMessage(CypherStrings.CommandNotitlefound);

            return true;
        }

        [Command("map", RBACPermissions.CommandLookupMap, true)]
        static bool Map(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            string namePart = args.NextString();

            uint counter = 0;

            // search in Map.dbc
            foreach (var mapInfo in CliDB.MapStorage.Values)
            {
                Locale locale = handler.GetSessionDbcLocale();
                string name = mapInfo.MapName[locale];
                if (string.IsNullOrEmpty(name))
                    continue;

                if (!name.Like(namePart) && handler.GetSession())
                {
                    locale = 0;
                    for (; locale < Locale.Total; ++locale)
                    {
                        if (locale == handler.GetSessionDbcLocale())
                            continue;

                        name = mapInfo.MapName[locale];
                        if (name.IsEmpty())
                            continue;

                        if (name.Like(namePart))
                            break;
                    }
                }

                if (locale < Locale.Total)
                { 
                    if (maxResults != 0 && counter == maxResults)
                    {
                        handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                        return true;
                    }

                    StringBuilder ss = new();
                    ss.Append(mapInfo.Id + " - [" + name + ']');

                    if (mapInfo.IsContinent())
                        ss.Append(handler.GetCypherString(CypherStrings.Continent));

                    switch (mapInfo.InstanceType)
                    {
                        case MapTypes.Instance:
                            ss.Append(handler.GetCypherString(CypherStrings.Instance));
                            break;
                        case MapTypes.Raid:
                            ss.Append(handler.GetCypherString(CypherStrings.Raid));
                            break;
                        case MapTypes.Battleground:
                            ss.Append(handler.GetCypherString(CypherStrings.Battleground));
                            break;
                        case MapTypes.Arena:
                            ss.Append(handler.GetCypherString(CypherStrings.Arena));
                            break;
                    }

                    handler.SendSysMessage(ss.ToString());

                    ++counter;
                }
            }

            if (counter == 0)
                handler.SendSysMessage(CypherStrings.CommandNomapfound);

            return true;
        }

        [CommandGroup("player", RBACPermissions.CommandLookupPlayer, true)]
        class PlayerCommandGroup
        {
            [Command("ip", RBACPermissions.CommandLookupPlayerIp)]
            static bool IP(StringArguments args, CommandHandler handler)
            {
                string ip;
                int limit;
                string limitStr;

                Player target = handler.GetSelectedPlayer();
                if (args.Empty())
                {
                    // NULL only if used from console
                    if (!target || target == handler.GetSession().GetPlayer())
                        return false;

                    ip = target.GetSession().GetRemoteAddress();
                    limit = -1;
                }
                else
                {
                    ip = args.NextString();
                    limitStr = args.NextString();
                    if (!int.TryParse(limitStr, out limit))
                        limit = -1;
                }

                PreparedStatement stmt = DB.Login.GetPreparedStatement(LoginStatements.SEL_ACCOUNT_BY_IP);
                stmt.AddValue(0, ip);
                return LookupPlayerSearchCommand(DB.Login.Query(stmt), limit, handler);
            }

            [Command("account", RBACPermissions.CommandLookupPlayerAccount)]
            static bool Account(StringArguments args, CommandHandler handler)
            {
                if (args.Empty())
                    return false;

                string account = args.NextString();
                string limitStr = args.NextString();
                if (!int.TryParse(limitStr, out int limit))
                    limit = -1;

                PreparedStatement stmt = DB.Login.GetPreparedStatement(LoginStatements.SEL_ACCOUNT_LIST_BY_NAME);
                stmt.AddValue(0, account);
                return LookupPlayerSearchCommand(DB.Login.Query(stmt), limit, handler);
            }

            [Command("email", RBACPermissions.CommandLookupPlayerEmail)]
            static bool Email(StringArguments args, CommandHandler handler)
            {
                if (args.Empty())
                    return false;

                string email = args.NextString();
                string limitStr = args.NextString();
                if (!int.TryParse(limitStr, out int limit))
                    limit = -1;

                PreparedStatement stmt = DB.Login.GetPreparedStatement(LoginStatements.SEL_ACCOUNT_LIST_BY_EMAIL);
                stmt.AddValue(0, email);
                return LookupPlayerSearchCommand(DB.Login.Query(stmt), limit, handler);
            }

            static bool LookupPlayerSearchCommand(SQLResult result, int limit, CommandHandler handler)
            {
                if (result.IsEmpty())
                {
                    handler.SendSysMessage(CypherStrings.NoPlayersFound);
                    return false;
                }

                int counter = 0;
                uint count = 0;
                do
                {
                    if (maxResults != 0 && count++ == maxResults)
                    {
                        handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                        return true;
                    }
                    uint accountId = result.Read<uint>(0);
                    string accountName = result.Read<string>(1);

                    PreparedStatement stmt = DB.Characters.GetPreparedStatement(CharStatements.SEL_CHAR_GUID_NAME_BY_ACC);
                    stmt.AddValue(0, accountId);
                    SQLResult result2 = DB.Characters.Query(stmt);

                    if (!result2.IsEmpty())
                    {
                        handler.SendSysMessage(CypherStrings.LookupPlayerAccount, accountName, accountId);

                        do
                        {
                            ObjectGuid guid = ObjectGuid.Create(HighGuid.Player, result2.Read<ulong>(0));
                            string name = result2.Read<string>(1);

                            handler.SendSysMessage(CypherStrings.LookupPlayerCharacter, name, guid.ToString());
                            ++counter;
                        }
                        while (result2.NextRow() && (limit == -1 || counter < limit));
                    }
                }
                while (result.NextRow());

                if (counter == 0) // empty accounts only
                {
                    handler.SendSysMessage(CypherStrings.NoPlayersFound);
                    return false;
                }
                return true;
            }
        }

        [CommandGroup("spell",RBACPermissions.CommandLookupSpell)]
        class SpellCommandGroup
        {
            [Command("", RBACPermissions.CommandLookupSpell)]
            static bool Default(StringArguments args, CommandHandler handler)
            {
                if (args.Empty())
                    return false;

                // can be NULL at console call
                Player target = handler.GetSelectedPlayer();

                string namePart = args.NextString();

                bool found = false;
                uint count = 0;

                // Search in SpellName.dbc
                foreach (SpellNameRecord spellName in CliDB.SpellNameStorage.Values)
                {
                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellName.Id, Difficulty.None);
                    if (spellInfo != null)
                    {
                        Locale locale = handler.GetSessionDbcLocale();
                        string name = spellInfo.SpellName[locale];
                        if (name.IsEmpty())
                            continue;

                        if (!name.Like(namePart))
                        {
                            locale = 0;
                            for (; locale < Locale.Total; ++locale)
                            {
                                if (locale == handler.GetSessionDbcLocale())
                                    continue;

                                name = spellInfo.SpellName[locale];
                                if (name.IsEmpty())
                                    continue;

                                if (name.Like(namePart))
                                    break;
                            }
                        }

                        if (locale < Locale.Total)
                        {
                            if (maxResults != 0 && count++ == maxResults)
                            {
                                handler.SendSysMessage(CypherStrings.CommandLookupMaxResults, maxResults);
                                return true;
                            }

                            bool known = target && target.HasSpell(spellInfo.Id);
                            SpellEffectInfo effect = spellInfo.GetEffect(0);
                            bool learn = effect?.Effect == SpellEffectName.LearnSpell;

                            SpellInfo learnSpellInfo = effect != null ? Global.SpellMgr.GetSpellInfo(effect.TriggerSpell, spellInfo.Difficulty) : null;

                            bool talent = spellInfo.HasAttribute(SpellCustomAttributes.IsTalent);
                            bool passive = spellInfo.IsPassive();
                            bool active = target && target.HasAura(spellInfo.Id);

                            // unit32 used to prevent interpreting public byte as char at output
                            // find rank of learned spell for learning spell, or talent rank
                            uint rank = learn && learnSpellInfo != null ? learnSpellInfo.GetRank() : spellInfo.GetRank();

                            // send spell in "id - [name, rank N] [talent] [passive] [learn] [known]" format
                            StringBuilder ss = new();
                            if (handler.GetSession() != null)
                                ss.Append(spellInfo.Id + " - |cffffffff|Hspell:" + spellInfo.Id + "|h[" + name);
                            else
                                ss.Append(spellInfo.Id + " - " + name);

                            // include rank in link name
                            if (rank != 0)
                                ss.Append(handler.GetCypherString(CypherStrings.SpellRank) + rank);

                            if (handler.GetSession() != null)
                                ss.Append("]|h|r");

                            if (talent)
                                ss.Append(handler.GetCypherString(CypherStrings.Talent));
                            if (passive)
                                ss.Append(handler.GetCypherString(CypherStrings.Passive));
                            if (learn)
                                ss.Append(handler.GetCypherString(CypherStrings.Learn));
                            if (known)
                                ss.Append(handler.GetCypherString(CypherStrings.Known));
                            if (active)
                                ss.Append(handler.GetCypherString(CypherStrings.Active));

                            handler.SendSysMessage(ss.ToString());

                            if (!found)
                                found = true;
                        }
                    }
                }
                if (!found)
                    handler.SendSysMessage(CypherStrings.CommandNospellfound);

                return true;
            }

            [Command("id", RBACPermissions.CommandLookupSpellId)]
            static bool SpellId(StringArguments args, CommandHandler handler)
            {
                if (args.Empty())
                    return false;

                // can be NULL at console call
                Player target = handler.GetSelectedPlayer();

                uint id = args.NextUInt32();

                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(id, Difficulty.None);
                if (spellInfo != null)
                {
                    Locale locale = handler.GetSessionDbcLocale();
                    string name = spellInfo.SpellName[locale];
                    if (string.IsNullOrEmpty(name))
                    {
                        handler.SendSysMessage(CypherStrings.CommandNospellfound);
                        return true;
                    }

                    bool known = target && target.HasSpell(id);
                    SpellEffectInfo effect = spellInfo.GetEffect(0);
                    bool learn = (effect.Effect == SpellEffectName.LearnSpell);

                    SpellInfo learnSpellInfo = Global.SpellMgr.GetSpellInfo(effect.TriggerSpell, Difficulty.None);

                    bool talent = spellInfo.HasAttribute(SpellCustomAttributes.IsTalent);
                    bool passive = spellInfo.IsPassive();
                    bool active = target && target.HasAura(id);

                    // unit32 used to prevent interpreting public byte as char at output
                    // find rank of learned spell for learning spell, or talent rank
                    uint rank = learn && learnSpellInfo != null ? learnSpellInfo.GetRank() : spellInfo.GetRank();

                    // send spell in "id - [name, rank N] [talent] [passive] [learn] [known]" format
                    StringBuilder ss = new();
                    if (handler.GetSession() != null)
                        ss.Append(id + " - |cffffffff|Hspell:" + id + "|h[" + name);
                    else
                        ss.Append(id + " - " + name);

                    // include rank in link name
                    if (rank != 0)
                        ss.Append(handler.GetCypherString(CypherStrings.SpellRank) + rank);

                    if (handler.GetSession() != null)
                        ss.Append("]|h|r");

                    if (talent)
                        ss.Append(handler.GetCypherString(CypherStrings.Talent));
                    if (passive)
                        ss.Append(handler.GetCypherString(CypherStrings.Passive));
                    if (learn)
                        ss.Append(handler.GetCypherString(CypherStrings.Learn));
                    if (known)
                        ss.Append(handler.GetCypherString(CypherStrings.Known));
                    if (active)
                        ss.Append(handler.GetCypherString(CypherStrings.Active));

                    handler.SendSysMessage(ss.ToString());
                }
                else
                    handler.SendSysMessage(CypherStrings.CommandNospellfound);

                return true;
            }
        }
    }
}
