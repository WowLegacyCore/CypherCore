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
using Framework.Database;
using Framework.Dynamic;
using Framework.GameMath;
using Framework.IO;
using Game.Conditions;
using Game.DataStorage;
using Game.Entities;
using Game.Mails;
using Game.Maps;
using Game.Misc;
using Game.Scripting;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Game
{
    public sealed class ObjectManager : Singleton<ObjectManager>
    {
        ObjectManager()
        {
            for (var i = 0; i < SharedConst.MaxCreatureDifficulties; ++i)
            {
                _difficultyEntries[i] = new List<uint>();
                _hasDifficultyEntries[i] = new List<uint>();
            }
        }

        //Static Methods
        public static bool NormalizePlayerName(ref string name)
        {
            if (name.IsEmpty())
                return false;

            //CultureInfo cultureInfo = Thread.CurrentThread.CurrentCulture;
            //TextInfo textInfo = cultureInfo.TextInfo;

            //str = textInfo.ToTitleCase(str);

            name = name.ToLower();

            var charArray = name.ToCharArray();
            charArray[0] = char.ToUpper(charArray[0]);

            name = new string(charArray);
            return true;
        }
        public static ExtendedPlayerName ExtractExtendedPlayerName(string name)
        {
            int pos = name.IndexOf('-');
            if (pos != -1)
                return new ExtendedPlayerName(name.Substring(0, pos), name[(pos + 1)..]);
            else
                return new ExtendedPlayerName(name, "");
        }
        static LanguageType GetRealmLanguageType(bool create)
        {
            switch ((RealmZones)WorldConfig.GetIntValue(WorldCfg.RealmZone))
            {
                case RealmZones.Unknown:                            // any language
                case RealmZones.Development:
                case RealmZones.TestServer:
                case RealmZones.QaServer:
                    return LanguageType.Any;
                case RealmZones.UnitedStates:                      // extended-Latin
                case RealmZones.Oceanic:
                case RealmZones.LatinAmerica:
                case RealmZones.English:
                case RealmZones.German:
                case RealmZones.French:
                case RealmZones.Spanish:
                    return LanguageType.ExtendenLatin;
                case RealmZones.Korea:                              // East-Asian
                case RealmZones.Taiwan:
                case RealmZones.China:
                    return LanguageType.EastAsia;
                case RealmZones.Russian:                            // Cyrillic
                    return LanguageType.Cyrillic;
                default:
                    return create ? LanguageType.BasicLatin : LanguageType.Any;        // basic-Latin at create, any at login
            }
        }

        public static CreatureModel ChooseDisplayId(CreatureTemplate cinfo, CreatureData data = null)
        {
            // Load creature model (display id)
            if (data != null && data.displayid != 0)
            {
                CreatureModel model = cinfo.GetModelWithDisplayId(data.displayid);
                if (model != null)
                    return model;
            }

            if (!cinfo.FlagsExtra.HasAnyFlag(CreatureFlagsExtra.Trigger))
            {
                CreatureModel model = cinfo.GetRandomValidModel();
                if (model != null)
                    return model;
            }

            // Triggers by default receive the invisible model
            return cinfo.GetFirstInvisibleModel();
        }

        public static void ChooseCreatureFlags(CreatureTemplate cInfo, out ulong npcFlag, out uint unitFlags, out uint unitFlags2, out uint unitFlags3, out uint dynamicFlags, CreatureData data = null)
        {
            npcFlag = (ulong)cInfo.Npcflag;
            unitFlags = (uint)cInfo.UnitFlags;
            unitFlags2 = cInfo.UnitFlags2;
            unitFlags3 = cInfo.UnitFlags3;
            dynamicFlags = cInfo.DynamicFlags;

            if (data != null)
            {
                if (data.npcflag != 0)
                    npcFlag = data.npcflag;

                if (data.unit_flags != 0)
                    unitFlags = data.unit_flags;

                if (data.unit_flags2 != 0)
                    unitFlags2 = data.unit_flags2;

                if (data.unit_flags3 != 0)
                    unitFlags3 = data.unit_flags3;

                if (data.dynamicflags != 0)
                    dynamicFlags = data.dynamicflags;
            }
        }

        public static ResponseCodes CheckPlayerName(string name, Locale locale, bool create = false)
        {
            if (name.Length > 12)
                return ResponseCodes.CharNameTooLong;

            uint minName = WorldConfig.GetUIntValue(WorldCfg.MinPlayerName);
            if (name.Length < minName)
                return ResponseCodes.CharNameTooShort;

            uint strictMask = WorldConfig.GetUIntValue(WorldCfg.StrictPlayerNames);
            if (!IsValidString(name, strictMask, false, create))
                return ResponseCodes.CharNameMixedLanguages;

            name = name.ToLower();
            for (int i = 2; i < name.Length; ++i)
                if (name[i] == name[i - 1] && name[i] == name[i - 2])
                    return ResponseCodes.CharNameThreeConsecutive;

            return Global.DB2Mgr.ValidateName(name, locale);
        }
        public static PetNameInvalidReason CheckPetName(string name)
        {
            if (name.Length > 12)
                return PetNameInvalidReason.TooLong;

            uint minName = WorldConfig.GetUIntValue(WorldCfg.MinPetName);
            if (name.Length < minName)
                return PetNameInvalidReason.TooShort;

            uint strictMask = WorldConfig.GetUIntValue(WorldCfg.StrictPetNames);
            if (!IsValidString(name, strictMask, false))
                return PetNameInvalidReason.MixedLanguages;

            return PetNameInvalidReason.Success;
        }
        public static bool IsValidCharterName(string name)
        {
            if (name.Length > 24)
                return false;

            uint minName = WorldConfig.GetUIntValue(WorldCfg.MinCharterName);
            if (name.Length < minName)
                return false;

            uint strictMask = WorldConfig.GetUIntValue(WorldCfg.StrictCharterNames);

            return IsValidString(name, strictMask, true);
        }
        public static void AddLocaleString(string value, Locale locale, StringArray data)
        {
            if (!string.IsNullOrEmpty(value))
                data[(int)locale] = value;
        }
        public static void GetLocaleString(StringArray data, Locale locale, ref string value)
        {
            if (data.Length > (int)locale && !string.IsNullOrEmpty(data[(int)locale]))
                value = data[(int)locale];
        }

        static bool IsValidString(string str, uint strictMask, bool numericOrSpace, bool create = false)
        {
            if (strictMask == 0)                                       // any language, ignore realm
            {
                if (IsCultureString(LanguageType.BasicLatin, str, numericOrSpace))
                    return true;
                if (IsCultureString(LanguageType.ExtendenLatin, str, numericOrSpace))
                    return true;
                if (IsCultureString(LanguageType.Cyrillic, str, numericOrSpace))
                    return true;
                if (IsCultureString(LanguageType.EastAsia, str, numericOrSpace))
                    return true;
                return false;
            }

            if (Convert.ToBoolean(strictMask & 0x2))                                    // realm zone specific
            {
                LanguageType lt = GetRealmLanguageType(create);
                if (lt.HasAnyFlag(LanguageType.ExtendenLatin))
                {
                    if (IsCultureString(LanguageType.BasicLatin, str, numericOrSpace))
                        return true;
                    if (IsCultureString(LanguageType.ExtendenLatin, str, numericOrSpace))
                        return true;
                }
                if (lt.HasAnyFlag(LanguageType.Cyrillic))
                    if (IsCultureString(LanguageType.Cyrillic, str, numericOrSpace))
                        return true;
                if (lt.HasAnyFlag(LanguageType.EastAsia))
                    if (IsCultureString(LanguageType.EastAsia, str, numericOrSpace))
                        return true;
            }

            if (Convert.ToBoolean(strictMask & 0x1))                                    // basic Latin
            {
                if (IsCultureString(LanguageType.BasicLatin, str, numericOrSpace))
                    return true;
            }

            return false;
        }
        static bool IsCultureString(LanguageType culture, string str, bool numericOrSpace)
        {
            foreach (var wchar in str)
            {
                if (numericOrSpace && (char.IsNumber(wchar) || char.IsWhiteSpace(wchar)))
                    return true;

                switch (culture)
                {
                    case LanguageType.BasicLatin:
                        if (wchar >= 'a' && wchar <= 'z')                      // LATIN SMALL LETTER A - LATIN SMALL LETTER Z
                            return true;
                        if (wchar >= 'A' && wchar <= 'Z')                      // LATIN CAPITAL LETTER A - LATIN CAPITAL LETTER Z
                            return true;
                        return false;
                    case LanguageType.ExtendenLatin:
                        if (wchar >= 0x00C0 && wchar <= 0x00D6)                  // LATIN CAPITAL LETTER A WITH GRAVE - LATIN CAPITAL LETTER O WITH DIAERESIS
                            return true;
                        if (wchar >= 0x00D8 && wchar <= 0x00DE)                  // LATIN CAPITAL LETTER O WITH STROKE - LATIN CAPITAL LETTER THORN
                            return true;
                        if (wchar == 0x00DF)                                     // LATIN SMALL LETTER SHARP S
                            return true;
                        if (wchar >= 0x00E0 && wchar <= 0x00F6)                  // LATIN SMALL LETTER A WITH GRAVE - LATIN SMALL LETTER O WITH DIAERESIS
                            return true;
                        if (wchar >= 0x00F8 && wchar <= 0x00FE)                  // LATIN SMALL LETTER O WITH STROKE - LATIN SMALL LETTER THORN
                            return true;
                        if (wchar >= 0x0100 && wchar <= 0x012F)                  // LATIN CAPITAL LETTER A WITH MACRON - LATIN SMALL LETTER I WITH OGONEK
                            return true;
                        if (wchar == 0x1E9E)                                     // LATIN CAPITAL LETTER SHARP S
                            return true;
                        return false;
                    case LanguageType.Cyrillic:
                        if (wchar >= 0x0410 && wchar <= 0x044F)                  // CYRILLIC CAPITAL LETTER A - CYRILLIC SMALL LETTER YA
                            return true;
                        if (wchar == 0x0401 || wchar == 0x0451)                  // CYRILLIC CAPITAL LETTER IO, CYRILLIC SMALL LETTER IO
                            return true;
                        return false;
                    case LanguageType.EastAsia:
                        if (wchar >= 0x1100 && wchar <= 0x11F9)                  // Hangul Jamo
                            return true;
                        if (wchar >= 0x3041 && wchar <= 0x30FF)                  // Hiragana + Katakana
                            return true;
                        if (wchar >= 0x3131 && wchar <= 0x318E)                  // Hangul Compatibility Jamo
                            return true;
                        if (wchar >= 0x31F0 && wchar <= 0x31FF)                  // Katakana Phonetic Ext.
                            return true;
                        if (wchar >= 0x3400 && wchar <= 0x4DB5)                  // CJK Ideographs Ext. A
                            return true;
                        if (wchar >= 0x4E00 && wchar <= 0x9FC3)                  // Unified CJK Ideographs
                            return true;
                        if (wchar >= 0xAC00 && wchar <= 0xD7A3)                  // Hangul Syllables
                            return true;
                        if (wchar >= 0xFF01 && wchar <= 0xFFEE)                  // Halfwidth forms
                            return true;
                        return false;
                }
            }

            return false;
        }

        //General
        public void LoadCypherStrings()
        {
            var time = Time.GetMSTime();
            CypherStringStorage.Clear();

            SQLResult result = DB.World.Query("SELECT entry, content_default, content_loc1, content_loc2, content_loc3, content_loc4, content_loc5, content_loc6, content_loc7, content_loc8 FROM trinity_string");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 CypherStrings. DB table `trinity_string` is empty.");
                Global.WorldMgr.StopNow();
                return;
            }
            uint count = 0;
            do
            {
                uint entry = result.Read<uint>(0);

                CypherStringStorage[entry] = new StringArray((int)SharedConst.DefaultLocale + 1);
                count++;

                for (var i = SharedConst.DefaultLocale; i >= 0; --i)
                    AddLocaleString(result.Read<string>((int)i + 1).ConvertFormatSyntax(), i, CypherStringStorage[entry]);
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} CypherStrings in {1} ms", count, Time.GetMSTimeDiffToNow(time));
        }

        public void LoadRaceAndClassExpansionRequirements()
        {
            uint oldMSTime = Time.GetMSTime();
            _raceUnlockRequirementStorage.Clear();

            //                                         0       1          2
            SQLResult result = DB.World.Query("SELECT raceID, expansion, achievementId FROM `race_unlock_requirement`");
            if (!result.IsEmpty())
            {
                uint count = 0;
                do
                {
                    byte raceID = result.Read<byte>(0);
                    byte expansion = result.Read<byte>(1);
                    uint achievementId = result.Read<uint>(2);

                    ChrRacesRecord raceEntry = CliDB.ChrRacesStorage.LookupByKey(raceID);
                    if (raceEntry == null)
                    {
                        Log.outError(LogFilter.Sql, "Race {0} defined in `race_unlock_requirement` does not exists, skipped.", raceID);
                        continue;
                    }

                    if (expansion >= (int)Expansion.MaxAccountExpansions)
                    {
                        Log.outError(LogFilter.Sql, "Race {0} defined in `race_unlock_requirement` has incorrect expansion {1}, skipped.", raceID, expansion);
                        continue;
                    }

                    if (achievementId != 0 && !CliDB.AchievementStorage.ContainsKey(achievementId))
                    {
                        Log.outError(LogFilter.Sql, $"Race {raceID} defined in `race_unlock_requirement` has incorrect achievement {achievementId}, skipped.");
                        continue;
                    }

                    RaceUnlockRequirement raceUnlockRequirement = new();
                    raceUnlockRequirement.Expansion = expansion;
                    raceUnlockRequirement.AchievementId = achievementId;

                    _raceUnlockRequirementStorage[raceID] = raceUnlockRequirement;

                    ++count;
                }
                while (result.NextRow());
                Log.outInfo(LogFilter.ServerLoading, "Loaded {0} race expansion requirements in {1} ms.", count, Time.GetMSTimeDiffToNow(oldMSTime));
            }
            else
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 race expansion requirements. DB table `race_expansion_requirement` is empty.");

            oldMSTime = Time.GetMSTime();
            _classExpansionRequirementStorage.Clear();

            //                               0        1       2                     3
            result = DB.World.Query("SELECT ClassID, RaceID, ActiveExpansionLevel, AccountExpansionLevel FROM `class_expansion_requirement`");
            if (!result.IsEmpty())
            {
                Dictionary<byte, Dictionary<byte, Tuple<byte, byte>>> temp = new();
                uint count = 0;
                do
                {
                    byte classID = result.Read<byte>(0);
                    byte raceID = result.Read<byte>(1);
                    byte activeExpansionLevel = result.Read<byte>(2);
                    byte accountExpansionLevel = result.Read<byte>(3);

                    ChrClassesRecord classEntry = CliDB.ChrClassesStorage.LookupByKey(classID);
                    if (classEntry == null)
                    {
                        Log.outError(LogFilter.Sql, $"Class {classID} (race {raceID}) defined in `class_expansion_requirement` does not exists, skipped.");
                        continue;
                    }

                    ChrRacesRecord raceEntry = CliDB.ChrRacesStorage.LookupByKey(raceID);
                    if (raceEntry == null)
                    {
                        Log.outError(LogFilter.Sql, $"Race {raceID} (class {classID}) defined in `class_expansion_requirement` does not exists, skipped.");
                        continue;
                    }

                    if (activeExpansionLevel >= (int)Expansion.Max)
                    {
                        Log.outError(LogFilter.Sql, $"Class {classID} Race {raceID} defined in `class_expansion_requirement` has incorrect ActiveExpansionLevel {activeExpansionLevel}, skipped.");
                        continue;
                    }

                    if (accountExpansionLevel >= (int)Expansion.MaxAccountExpansions)
                    {
                        Log.outError(LogFilter.Sql, $"Class {classID} Race {raceID} defined in `class_expansion_requirement` has incorrect AccountExpansionLevel {accountExpansionLevel}, skipped.");
                        continue;
                    }

                    if (!temp.ContainsKey(raceID))
                        temp[raceID] = new Dictionary<byte, Tuple<byte, byte>>();

                    temp[raceID][classID] = Tuple.Create(activeExpansionLevel, accountExpansionLevel);

                    ++count;
                }
                while (result.NextRow());

                foreach (var race in temp)
                {
                    RaceClassAvailability raceClassAvailability = new();
                    raceClassAvailability.RaceID = race.Key;

                    foreach (var class_ in race.Value)
                    {
                        ClassAvailability classAvailability = new();
                        classAvailability.ClassID = class_.Key;
                        classAvailability.ActiveExpansionLevel = class_.Value.Item1;
                        classAvailability.AccountExpansionLevel = class_.Value.Item2;

                        raceClassAvailability.Classes.Add(classAvailability);
                    }

                    _classExpansionRequirementStorage.Add(raceClassAvailability);
                }

                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} class expansion requirements in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
            }
            else
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 class expansion requirements. DB table `class_expansion_requirement` is empty.");
        }
        public void LoadRealmNames()
        {
            uint oldMSTime = Time.GetMSTime();
            _realmNameStorage.Clear();

            //                                         0   1
            SQLResult result = DB.Login.Query("SELECT id, name FROM `realmlist`");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 realm names. DB table `realmlist` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint realm = result.Read<uint>(0);
                string realmName = result.Read<string>(1);

                _realmNameStorage[realm] = realmName;

                ++count;
            }
            while (result.NextRow());
            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} realm names in {1} ms.", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        public string GetCypherString(uint entry, Locale locale = Locale.enUS)
        {
            if (!CypherStringStorage.ContainsKey(entry))
            {
                Log.outError(LogFilter.Sql, "Cypher string entry {0} not found in DB.", entry);
                return "<Error>";
            }

            var cs = CypherStringStorage[entry];
            if (cs.Length > (int)locale && !string.IsNullOrEmpty(cs[(int)locale]))
                return cs[(int)locale];

            return cs[(int)SharedConst.DefaultLocale];
        }
        public string GetCypherString(CypherStrings cmd, Locale locale = Locale.enUS)
        {
            return GetCypherString((uint)cmd, locale);
        }

        public string GetRealmName(uint realm)
        {
            return _realmNameStorage.LookupByKey(realm);
        }
        public bool GetRealmName(uint realmId, ref string name, ref string normalizedName)
        {
            var realmName = _realmNameStorage.LookupByKey(realmId);
            if (realmName != null)
            {
                name = realmName;
                normalizedName = realmName.Normalize();
                return true;
            }
            return false;
        }
        public Dictionary<byte, RaceUnlockRequirement> GetRaceUnlockRequirements() { return _raceUnlockRequirementStorage; }
        public RaceUnlockRequirement GetRaceUnlockRequirement(Race race) { return _raceUnlockRequirementStorage.LookupByKey((byte)race); }
        public List<RaceClassAvailability> GetClassExpansionRequirements() { return _classExpansionRequirementStorage; }
        public ClassAvailability GetClassExpansionRequirement(Race raceId, Class classId)
        {
            var raceClassAvailability = _classExpansionRequirementStorage.Find(raceClass =>
            {
                return raceClass.RaceID == (byte)raceId;
            });

            if (raceClassAvailability == null)
                return null;

            var classAvailability = raceClassAvailability.Classes.Find(availability =>
            {
                return availability.ClassID == (byte)classId;
            });

            if (classAvailability == null)
                return null;

            return classAvailability;
        }
        public PlayerChoice GetPlayerChoice(int choiceId)
        {
            return _playerChoices.LookupByKey(choiceId);
        }
        public PlayerChoiceLocale GetPlayerChoiceLocale(int ChoiceID)
        {
            return _playerChoiceLocales.LookupByKey(ChoiceID);
        }

        //Gossip
        public void LoadGossipMenu()
        {
            uint oldMSTime = Time.GetMSTime();

            gossipMenusStorage.Clear();

            SQLResult result = DB.World.Query("SELECT MenuId, TextId FROM gossip_menu");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gossip_menu entries. DB table `gossip_menu` is empty!");
                return;
            }

            do
            {
                GossipMenus gMenu = new();

                gMenu.MenuId = result.Read<uint>(0);
                gMenu.TextId = result.Read<uint>(1);

                if (GetNpcText(gMenu.TextId) == null)
                {
                    Log.outError(LogFilter.Sql, "Table gossip_menu: Id {0} is using non-existing TextId {1}", gMenu.MenuId, gMenu.TextId);
                    continue;
                }

                gossipMenusStorage.Add(gMenu.MenuId, gMenu);
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} gossip_menu Ids in {1} ms", gossipMenusStorage.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadGossipMenuItems()
        {
            uint oldMSTime = Time.GetMSTime();

            gossipMenuItemsStorage.Clear();
            
            //                                          0         1              2             3             4                        5             6
            SQLResult result = DB.World.Query("SELECT o.MenuId, o.OptionIndex, o.OptionIcon, o.OptionText, o.OptionBroadcastTextId, o.OptionType, o.OptionNpcFlag, " +
                //   7                8              9            10           11          12
                "oa.ActionMenuId, oa.ActionPoiId, ob.BoxCoded, ob.BoxMoney, ob.BoxText, ob.BoxBroadcastTextId " +
                "FROM gossip_menu_option o " +
                "LEFT JOIN gossip_menu_option_action oa ON o.MenuId = oa.MenuId AND o.OptionIndex = oa.OptionIndex " +
                "LEFT JOIN gossip_menu_option_box ob ON o.MenuId = ob.MenuId AND o.OptionIndex = ob.OptionIndex " +
                "ORDER BY o.MenuId, o.OptionIndex");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gossip_menu_option Ids. DB table `gossip_menu_option` is empty!");
                return;
            }

            do
            {
                GossipMenuItems gMenuItem = new();

                gMenuItem.MenuId = result.Read<uint>(0);
                gMenuItem.OptionIndex = result.Read<uint>(1);
                gMenuItem.OptionIcon = (GossipOptionIcon)result.Read<byte>(2);
                gMenuItem.OptionText = result.Read<string>(3);
                gMenuItem.OptionBroadcastTextId = result.Read<uint>(4);
                gMenuItem.OptionType = (GossipOption)result.Read<uint>(5);
                gMenuItem.OptionNpcFlag = (NPCFlags)result.Read<ulong>(6);
                gMenuItem.ActionMenuId = result.Read<uint>(7);
                gMenuItem.ActionPoiId = result.Read<uint>(8);
                gMenuItem.BoxCoded = result.Read<bool>(9);
                gMenuItem.BoxMoney = result.Read<uint>(10);
                gMenuItem.BoxText = result.Read<string>(11);
                gMenuItem.BoxBroadcastTextId = result.Read<uint>(12);

                if (gMenuItem.OptionIcon >= GossipOptionIcon.Max)
                {
                    Log.outError(LogFilter.Sql, $"Table gossip_menu_option for MenuId {gMenuItem.MenuId}, OptionIndex {gMenuItem.OptionIndex} has unknown icon id {gMenuItem.OptionIcon}. Replacing with GossipOptionIcon.Chat");
                    gMenuItem.OptionIcon = GossipOptionIcon.Chat;
                }

                if (gMenuItem.OptionBroadcastTextId != 0)
                {
                    if (!CliDB.BroadcastTextStorage.ContainsKey(gMenuItem.OptionBroadcastTextId))
                    {
                        Log.outError(LogFilter.Sql, $"Table `gossip_menu_option` for MenuId {gMenuItem.MenuId}, OptionIndex {gMenuItem.OptionIndex} has non-existing or incompatible OptionBroadcastTextId {gMenuItem.OptionBroadcastTextId}, ignoring.");
                        gMenuItem.OptionBroadcastTextId = 0;
                    }
                }

                if (gMenuItem.OptionType >= GossipOption.Max)
                    Log.outError(LogFilter.Sql, $"Table gossip_menu_option for MenuId {gMenuItem.MenuId}, OptionIndex {gMenuItem.OptionIndex} has unknown option id {gMenuItem.OptionType}. Option will not be used");

                if (gMenuItem.ActionPoiId != 0 && GetPointOfInterest(gMenuItem.ActionPoiId) == null)
                {
                    Log.outError(LogFilter.Sql, $"Table gossip_menu_option for MenuId {gMenuItem.MenuId}, OptionIndex {gMenuItem.OptionIndex} use non-existing actionpoiid {gMenuItem.ActionPoiId}, ignoring");
                    gMenuItem.ActionPoiId = 0;
                }

                if (gMenuItem.BoxBroadcastTextId != 0)
                {
                    if (!CliDB.BroadcastTextStorage.ContainsKey(gMenuItem.BoxBroadcastTextId))
                    {
                        Log.outError(LogFilter.Sql, $"Table `gossip_menu_option` for MenuId {gMenuItem.MenuId}, OptionIndex {gMenuItem.OptionIndex} has non-existing or incompatible BoxBroadcastTextId {gMenuItem.BoxBroadcastTextId}, ignoring.");
                        gMenuItem.BoxBroadcastTextId = 0;
                    }
                }

                gossipMenuItemsStorage.Add(gMenuItem.MenuId, gMenuItem);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {gossipMenuItemsStorage.Count} gossip_menu_option entries in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }
        public void LoadPointsOfInterest()
        {
            uint oldMSTime = Time.GetMSTime();

            pointsOfInterestStorage.Clear(); // need for reload case

            //                                         0   1          2          3     4      5           6     7
            SQLResult result = DB.World.Query("SELECT ID, PositionX, PositionY, Icon, Flags, Importance, Name, Unknown905 FROM points_of_interest");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 Points of Interest definitions. DB table `points_of_interest` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint id = result.Read<uint>(0);

                PointOfInterest POI = new();
                POI.Id = id;
                POI.Pos = new Vector2(result.Read<float>(1), result.Read<float>(2));
                POI.Icon = result.Read<uint>(3);
                POI.Flags = result.Read<uint>(4);
                POI.Importance = result.Read<uint>(5);
                POI.Name = result.Read<string>(6);
                POI.Unknown905 = result.Read<uint>(7);

                if (!GridDefines.IsValidMapCoord(POI.Pos.X, POI.Pos.Y))
                {
                    Log.outError(LogFilter.Sql, $"Table `points_of_interest` (ID: {id}) have invalid coordinates (PositionX: {POI.Pos.X} PositionY: {POI.Pos.Y}), ignored.");
                    continue;
                }

                pointsOfInterestStorage[id] = POI;

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} Points of Interest definitions in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }

        public List<GossipMenus> GetGossipMenusMapBounds(uint uiMenuId)
        {
            return gossipMenusStorage.LookupByKey(uiMenuId);
        }
        public List<GossipMenuItems> GetGossipMenuItemsMapBounds(uint uiMenuId)
        {
            return gossipMenuItemsStorage.LookupByKey(uiMenuId);
        }
        public PointOfInterest GetPointOfInterest(uint id)
        {
            return pointsOfInterestStorage.LookupByKey(id);
        }

        public void LoadGraveyardZones()
        {
            uint oldMSTime = Time.GetMSTime();

            GraveYardStorage.Clear();                                  // need for reload case

            //                                         0       1         2
            SQLResult result = DB.World.Query("SELECT ID, GhostZone, faction FROM graveyard_zone");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 graveyard-zone links. DB table `graveyard_zone` is empty.");
                return;
            }

            uint count = 0;

            do
            {
                ++count;
                uint safeLocId = result.Read<uint>(0);
                uint zoneId = result.Read<uint>(1);
                Team team = (Team)result.Read<uint>(2);

                WorldSafeLocsEntry entry = GetWorldSafeLoc(safeLocId);
                if (entry == null)
                {
                    Log.outError(LogFilter.Sql, "Table `graveyard_zone` has a record for not existing graveyard (WorldSafeLocs.dbc id) {0}, skipped.", safeLocId);
                    continue;
                }

                AreaTableRecord areaEntry = CliDB.AreaTableStorage.LookupByKey(zoneId);
                if (areaEntry == null)
                {
                    Log.outError(LogFilter.Sql, "Table `graveyard_zone` has a record for not existing zone id ({0}), skipped.", zoneId);
                    continue;
                }

                if (team != 0 && team != Team.Horde && team != Team.Alliance)
                {
                    Log.outError(LogFilter.Sql, "Table `graveyard_zone` has a record for non player faction ({0}), skipped.", team);
                    continue;
                }

                if (!AddGraveYardLink(safeLocId, zoneId, team, false))
                    Log.outError(LogFilter.Sql, "Table `graveyard_zone` has a duplicate record for Graveyard (ID: {0}) and Zone (ID: {1}), skipped.", safeLocId, zoneId);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} graveyard-zone links in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadWorldSafeLocs()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0   1      2     3     4     5
            SQLResult result = DB.World.Query("SELECT ID, MapID, LocX, LocY, LocZ, Facing FROM world_safe_locs");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 world locations. DB table `world_safe_locs` is empty.");
                return;
            }
            do
            {
                uint id = result.Read<uint>(0);
                WorldLocation loc = new(result.Read<uint>(1), result.Read<float>(2), result.Read<float>(3), result.Read<float>(4), result.Read<float>(5));
                if (!GridDefines.IsValidMapCoord(loc))
                {
                    Log.outError(LogFilter.Sql, $"World location (ID: {id}) has a invalid position MapID: {loc.GetMapId()} {loc}, skipped");
                    continue;
                }

                WorldSafeLocsEntry worldSafeLocs = new();
                worldSafeLocs.Id = id;
                worldSafeLocs.Loc = loc;
                _worldSafeLocs[id] = worldSafeLocs;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_worldSafeLocs.Count} world locations {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }

        WorldSafeLocsEntry GetDefaultGraveYard(Team team)
        {
            if (team == Team.Horde)
                return GetWorldSafeLoc(10);
            else if (team == Team.Alliance)
                return GetWorldSafeLoc(4);
            else return null;
        }
        public WorldSafeLocsEntry GetClosestGraveYard(WorldLocation location, Team team, WorldObject conditionObject)
        {
            float x, y, z;
            location.GetPosition(out x, out y, out z);
            uint MapId = location.GetMapId();

            // search for zone associated closest graveyard
            uint zoneId = Global.MapMgr.GetZoneId(conditionObject ? conditionObject.GetPhaseShift() : PhasingHandler.EmptyPhaseShift, MapId, x, y, z);
            if (zoneId == 0)
            {
                if (z > -500)
                {
                    Log.outError(LogFilter.Server, "ZoneId not found for map {0} coords ({1}, {2}, {3})", MapId, x, y, z);
                    return GetDefaultGraveYard(team);
                }
            }

            // Simulate std. algorithm:
            //   found some graveyard associated to (ghost_zone, ghost_map)
            //
            //   if mapId == graveyard.mapId (ghost in plain zone or city or Battleground) and search graveyard at same map
            //     then check faction
            //   if mapId != graveyard.mapId (ghost in instance) and search any graveyard associated
            //     then check faction
            var range = GraveYardStorage.LookupByKey(zoneId);
            MapRecord mapEntry = CliDB.MapStorage.LookupByKey(MapId);

            ConditionSourceInfo conditionSource = new(conditionObject);

            // not need to check validity of map object; MapId _MUST_ be valid here
            if (range.Empty() && !mapEntry.IsBattlegroundOrArena())
            {
                if (zoneId != 0) // zone == 0 can't be fixed, used by bliz for bugged zones
                    Log.outError(LogFilter.Sql, "Table `game_graveyard_zone` incomplete: Zone {0} Team {1} does not have a linked graveyard.", zoneId, team);
                return GetDefaultGraveYard(team);
            }

            // at corpse map
            bool foundNear = false;
            float distNear = 10000;
            WorldSafeLocsEntry entryNear = null;

            // at entrance map for corpse map
            bool foundEntr = false;
            float distEntr = 10000;
            WorldSafeLocsEntry entryEntr = null;

            // some where other
            WorldSafeLocsEntry entryFar = null;

            foreach (var data in range)
            {
                WorldSafeLocsEntry entry = GetWorldSafeLoc(data.safeLocId);
                if (entry == null)
                {
                    Log.outError(LogFilter.Sql, "Table `game_graveyard_zone` has record for not existing graveyard (WorldSafeLocs.dbc id) {0}, skipped.", data.safeLocId);
                    continue;
                }

                // skip enemy faction graveyard
                // team == 0 case can be at call from .neargrave
                if (data.team != 0 && team != 0 && data.team != (uint)team)
                    continue;

                if (conditionObject)
                {
                    if (!Global.ConditionMgr.IsObjectMeetingNotGroupedConditions(ConditionSourceType.Graveyard, data.safeLocId, conditionSource))
                        continue;

                    if (entry.Loc.GetMapId() == mapEntry.ParentMapID && !conditionObject.GetPhaseShift().HasVisibleMapId(entry.Loc.GetMapId()))
                        continue;
                }

                // find now nearest graveyard at other map
                if (MapId != entry.Loc.GetMapId() && entry.Loc.GetMapId() != mapEntry.ParentMapID)
                {
                    // if find graveyard at different map from where entrance placed (or no entrance data), use any first
                    if (mapEntry == null
                        || mapEntry.CorpseMapID < 0
                        || mapEntry.CorpseMapID != entry.Loc.GetMapId()
                        || (mapEntry.Corpse.X == 0 && mapEntry.Corpse.Y == 0))
                    {
                        // not have any corrdinates for check distance anyway
                        entryFar = entry;
                        continue;
                    }

                    // at entrance map calculate distance (2D);
                    float dist2 = (entry.Loc.GetPositionX() - mapEntry.Corpse.X) * (entry.Loc.GetPositionX() - mapEntry.Corpse.X)
                        + (entry.Loc.GetPositionY() - mapEntry.Corpse.Y) * (entry.Loc.GetPositionY() - mapEntry.Corpse.Y);
                    if (foundEntr)
                    {
                        if (dist2 < distEntr)
                        {
                            distEntr = dist2;
                            entryEntr = entry;
                        }
                    }
                    else
                    {
                        foundEntr = true;
                        distEntr = dist2;
                        entryEntr = entry;
                    }
                }
                // find now nearest graveyard at same map
                else
                {
                    float dist2 = (entry.Loc.GetPositionX() - x) * (entry.Loc.GetPositionX() - x) + (entry.Loc.GetPositionY() - y) * (entry.Loc.GetPositionY() - y) + (entry.Loc.GetPositionZ() - z) * (entry.Loc.GetPositionZ() - z);
                    if (foundNear)
                    {
                        if (dist2 < distNear)
                        {
                            distNear = dist2;
                            entryNear = entry;
                        }
                    }
                    else
                    {
                        foundNear = true;
                        distNear = dist2;
                        entryNear = entry;
                    }
                }
            }

            if (entryNear != null)
                return entryNear;

            if (entryEntr != null)
                return entryEntr;

            return entryFar;
        }
        public GraveYardData FindGraveYardData(uint id, uint zoneId)
        {
            var range = GraveYardStorage.LookupByKey(zoneId);
            foreach (var data in range)
            {
                if (data.safeLocId == id)
                    return data;
            }
            return null;
        }
        public WorldSafeLocsEntry GetWorldSafeLoc(uint id)
        {
            return _worldSafeLocs.LookupByKey(id);
        }
        public Dictionary<uint, WorldSafeLocsEntry> GetWorldSafeLocs()
        {
            return _worldSafeLocs;
        }

        public bool AddGraveYardLink(uint id, uint zoneId, Team team, bool persist = true)
        {
            if (FindGraveYardData(id, zoneId) != null)
                return false;

            // add link to loaded data
            GraveYardData data = new();
            data.safeLocId = id;
            data.team = (uint)team;

            GraveYardStorage.Add(zoneId, data);

            // add link to DB
            if (persist)
            {
                PreparedStatement stmt = DB.World.GetPreparedStatement(WorldStatements.INS_GRAVEYARD_ZONE);

                stmt.AddValue(0, id);
                stmt.AddValue(1, zoneId);
                stmt.AddValue(2, (uint)team);

                DB.World.Execute(stmt);
            }

            return true;
        }
        public void RemoveGraveYardLink(uint id, uint zoneId, Team team, bool persist = false)
        {
            var range = GraveYardStorage.LookupByKey(zoneId);
            if (range.Empty())
            {
                Log.outError(LogFilter.Sql, "Table `game_graveyard_zone` incomplete: Zone {0} Team {1} does not have a linked graveyard.", zoneId, team);
                return;
            }

            bool found = false;


            foreach (var data in range)
            {
                // skip not matching safezone id
                if (data.safeLocId != id)
                    continue;

                // skip enemy faction graveyard at same map (normal area, city, or Battleground)
                // team == 0 case can be at call from .neargrave
                if (data.team != 0 && team != 0 && data.team != (uint)team)
                    continue;

                found = true;
                break;
            }

            // no match, return
            if (!found)
                return;

            // remove from links
            GraveYardStorage.Remove(zoneId);

            // remove link from DB
            if (persist)
            {
                PreparedStatement stmt = DB.World.GetPreparedStatement(WorldStatements.DEL_GRAVEYARD_ZONE);

                stmt.AddValue(0, id);
                stmt.AddValue(1, zoneId);
                stmt.AddValue(2, (uint)team);

                DB.World.Execute(stmt);
            }
        }

        //Scripts
        public void LoadScriptNames()
        {
            uint oldMSTime = Time.GetMSTime();

            scriptNamesStorage.Add("");
            SQLResult result = DB.World.Query(
              "SELECT DISTINCT(ScriptName) FROM battleground_template WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(ScriptName) FROM conversation_template WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(ScriptName) FROM creature WHERE ScriptName <> '' " +        
              "UNION SELECT DISTINCT(ScriptName) FROM creature_template WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(ScriptName) FROM criteria_data WHERE ScriptName <> '' AND type = 11 " +
              "UNION SELECT DISTINCT(ScriptName) FROM gameobject WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(ScriptName) FROM gameobject_template WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(ScriptName) FROM item_script_names WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(ScriptName) FROM areatrigger_scripts WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(ScriptName) FROM areatrigger_template WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(ScriptName) FROM spell_script_names WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(ScriptName) FROM transports WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(ScriptName) FROM game_weather WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(ScriptName) FROM conditions WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(ScriptName) FROM outdoorpvp_template WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(ScriptName) FROM scene_template WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(ScriptName) FROM quest_template_addon WHERE ScriptName <> '' " +
              "UNION SELECT DISTINCT(script) FROM instance_template WHERE script <> ''");

            if (result.IsEmpty())
            {
                Log.outError(LogFilter.ServerLoading, "Loaded empty set of Script Names!");
                return;
            }

            uint count = 1;
            do
            {
                scriptNamesStorage.Add(result.Read<string>(0));
                ++count;
            }
            while (result.NextRow());

            scriptNamesStorage.Sort();
            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} Script Names in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadAreaTriggerScripts()
        {
            uint oldMSTime = Time.GetMSTime();

            areaTriggerScriptStorage.Clear();                            // need for reload case
            SQLResult result = DB.World.Query("SELECT entry, ScriptName FROM areatrigger_scripts");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 areatrigger scripts. DB table `areatrigger_scripts` is empty.");
                return;
            }
            uint count = 0;
            do
            {
                uint id = result.Read<uint>(0);
                string scriptName = result.Read<string>(1);

                AreaTriggerRecord atEntry = CliDB.AreaTriggerStorage.LookupByKey(id);
                if (atEntry == null)
                {
                    Log.outError(LogFilter.Sql, "Area trigger (Id:{0}) does not exist in `AreaTrigger.dbc`.", id);
                    continue;
                }
                ++count;
                areaTriggerScriptStorage[id] = GetScriptId(scriptName);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} areatrigger scripts in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        void LoadScripts(ScriptsType type)
        {
            uint oldMSTime = Time.GetMSTime();

            var scripts = GetScriptsMapByType(type);
            if (scripts == null)
                return;

            string tableName = GetScriptsTableNameByType(type);
            if (string.IsNullOrEmpty(tableName))
                return;

            if (Global.MapMgr.IsScriptScheduled())                    // function cannot be called when scripts are in use.
                return;

            Log.outInfo(LogFilter.ServerLoading, "Loading {0}...", tableName);

            scripts.Clear();                                       // need for reload support

            bool isSpellScriptTable = (type == ScriptsType.Spell);
            //                                         0    1       2         3         4          5    6  7  8  9
            SQLResult result = DB.World.Query("SELECT id, delay, command, datalong, datalong2, dataint, x, y, z, o{0} FROM {1}", isSpellScriptTable ? ", effIndex" : "", tableName);
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 script definitions. DB table `{0}` is empty!", tableName);
                return;
            }

            uint count = 0;
            do
            {
                ScriptInfo tmp = new();
                tmp.type = type;
                tmp.id = result.Read<uint>(0);
                if (isSpellScriptTable)
                    tmp.id |= result.Read<uint>(10) << 24;
                tmp.delay = result.Read<uint>(1);
                tmp.command = (ScriptCommands)result.Read<uint>(2);
                unsafe
                {
                    tmp.Raw.nData[0] = result.Read<uint>(3);
                    tmp.Raw.nData[1] = result.Read<uint>(4);
                    tmp.Raw.nData[2] = (uint)result.Read<int>(5);
                    tmp.Raw.fData[0] = result.Read<float>(6);
                    tmp.Raw.fData[1] = result.Read<float>(7);
                    tmp.Raw.fData[2] = result.Read<float>(8);
                    tmp.Raw.fData[3] = result.Read<float>(9);
                }

                // generic command args check
                switch (tmp.command)
                {
                    case ScriptCommands.Talk:
                        {
                            if (tmp.Talk.ChatType > ChatMsg.RaidBossWhisper)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has invalid talk type (datalong = {1}) in SCRIPT_COMMAND_TALK for script id {2}",
                                    tableName, tmp.Talk.ChatType, tmp.id);
                                continue;
                            }
                            if (!CliDB.BroadcastTextStorage.ContainsKey((uint)tmp.Talk.TextID))
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has invalid talk text id (dataint = {1}) in SCRIPT_COMMAND_TALK for script id {2}",
                                    tableName, tmp.Talk.TextID, tmp.id);
                                continue;
                            }
                            break;
                        }

                    case ScriptCommands.Emote:
                        {
                            if (!CliDB.EmotesStorage.ContainsKey(tmp.Emote.EmoteID))
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has invalid emote id (datalong = {1}) in SCRIPT_COMMAND_EMOTE for script id {2}",
                                    tableName, tmp.Emote.EmoteID, tmp.id);
                                continue;
                            }
                            break;
                        }

                    case ScriptCommands.TeleportTo:
                        {
                            if (!CliDB.MapStorage.ContainsKey(tmp.TeleportTo.MapID))
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has invalid map (Id: {1}) in SCRIPT_COMMAND_TELEPORT_TO for script id {2}",
                                    tableName, tmp.TeleportTo.MapID, tmp.id);
                                continue;
                            }

                            if (!GridDefines.IsValidMapCoord(tmp.TeleportTo.DestX, tmp.TeleportTo.DestY, tmp.TeleportTo.DestZ, tmp.TeleportTo.Orientation))
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has invalid coordinates (X: {1} Y: {2} Z: {3} O: {4}) in SCRIPT_COMMAND_TELEPORT_TO for script id {5}",
                                    tableName, tmp.TeleportTo.DestX, tmp.TeleportTo.DestY, tmp.TeleportTo.DestZ, tmp.TeleportTo.Orientation, tmp.id);
                                continue;
                            }
                            break;
                        }

                    case ScriptCommands.QuestExplored:
                        {
                            Quest quest = GetQuestTemplate(tmp.QuestExplored.QuestID);
                            if (quest == null)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has invalid quest (ID: {1}) in SCRIPT_COMMAND_QUEST_EXPLORED in `datalong` for script id {2}",
                                    tableName, tmp.QuestExplored.QuestID, tmp.id);
                                continue;
                            }

                            if (!quest.HasSpecialFlag(QuestSpecialFlags.ExplorationOrEvent))
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has quest (ID: {1}) in SCRIPT_COMMAND_QUEST_EXPLORED in `datalong` for script id {2}, but quest not have flag QUEST_SPECIAL_FLAGS_EXPLORATION_OR_EVENT in quest flags. Script command or quest flags wrong. Quest modified to require objective.",
                                    tableName, tmp.QuestExplored.QuestID, tmp.id);

                                // this will prevent quest completing without objective
                                quest.SetSpecialFlag(QuestSpecialFlags.ExplorationOrEvent);

                                // continue; - quest objective requirement set and command can be allowed
                            }

                            if (tmp.QuestExplored.Distance > SharedConst.DefaultVisibilityDistance)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has too large distance ({1}) for exploring objective complete in `datalong2` in SCRIPT_COMMAND_QUEST_EXPLORED in `datalong` for script id {2}",
                                    tableName, tmp.QuestExplored.Distance, tmp.id);
                                continue;
                            }

                            if (tmp.QuestExplored.Distance != 0 && tmp.QuestExplored.Distance > SharedConst.DefaultVisibilityDistance)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has too large distance ({1}) for exploring objective complete in `datalong2` in SCRIPT_COMMAND_QUEST_EXPLORED in `datalong` for script id {2}, max distance is {3} or 0 for disable distance check",
                                    tableName, tmp.QuestExplored.Distance, tmp.id, SharedConst.DefaultVisibilityDistance);
                                continue;
                            }

                            if (tmp.QuestExplored.Distance != 0 && tmp.QuestExplored.Distance < SharedConst.InteractionDistance)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has too small distance ({1}) for exploring objective complete in `datalong2` in SCRIPT_COMMAND_QUEST_EXPLORED in `datalong` for script id {2}, min distance is {3} or 0 for disable distance check",
                                    tableName, tmp.QuestExplored.Distance, tmp.id, SharedConst.InteractionDistance);
                                continue;
                            }

                            break;
                        }

                    case ScriptCommands.KillCredit:
                        {
                            if (GetCreatureTemplate(tmp.KillCredit.CreatureEntry) == null)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has invalid creature (Entry: {1}) in SCRIPT_COMMAND_KILL_CREDIT for script id {2}",
                                    tableName, tmp.KillCredit.CreatureEntry, tmp.id);
                                continue;
                            }
                            break;
                        }

                    case ScriptCommands.RespawnGameobject:
                        {
                            GameObjectData data = GetGameObjectData(tmp.RespawnGameObject.GOGuid);
                            if (data == null)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has invalid gameobject (GUID: {1}) in SCRIPT_COMMAND_RESPAWN_GAMEOBJECT for script id {2}",
                                    tableName, tmp.RespawnGameObject.GOGuid, tmp.id);
                                continue;
                            }

                            GameObjectTemplate info = GetGameObjectTemplate(data.Id);
                            if (info == null)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has gameobject with invalid entry (GUID: {1} Entry: {2}) in SCRIPT_COMMAND_RESPAWN_GAMEOBJECT for script id {3}",
                                    tableName, tmp.RespawnGameObject.GOGuid, data.Id, tmp.id);
                                continue;
                            }

                            if (info.type == GameObjectTypes.FishingNode || info.type == GameObjectTypes.FishingHole || info.type == GameObjectTypes.Door ||
                                info.type == GameObjectTypes.Button || info.type == GameObjectTypes.Trap)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` have gameobject type ({1}) unsupported by command SCRIPT_COMMAND_RESPAWN_GAMEOBJECT for script id {2}",
                                    tableName, info.entry, tmp.id);
                                continue;
                            }
                            break;
                        }

                    case ScriptCommands.TempSummonCreature:
                        {
                            if (!GridDefines.IsValidMapCoord(tmp.TempSummonCreature.PosX, tmp.TempSummonCreature.PosY, tmp.TempSummonCreature.PosZ, tmp.TempSummonCreature.Orientation))
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has invalid coordinates (X: {1} Y: {2} Z: {3} O: {4}) in SCRIPT_COMMAND_TEMP_SUMMON_CREATURE for script id {5}",
                                    tableName, tmp.TempSummonCreature.PosX, tmp.TempSummonCreature.PosY, tmp.TempSummonCreature.PosZ, tmp.TempSummonCreature.Orientation, tmp.id);
                                continue;
                            }

                            if (GetCreatureTemplate(tmp.TempSummonCreature.CreatureEntry) == null)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has invalid creature (Entry: {1}) in SCRIPT_COMMAND_TEMP_SUMMON_CREATURE for script id {2}",
                                    tableName, tmp.TempSummonCreature.CreatureEntry, tmp.id);
                                continue;
                            }
                            break;
                        }

                    case ScriptCommands.OpenDoor:
                    case ScriptCommands.CloseDoor:
                        {
                            GameObjectData data = GetGameObjectData(tmp.ToggleDoor.GOGuid);
                            if (data == null)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has invalid gameobject (GUID: {1}) in {2} for script id {3}",
                                    tableName, tmp.ToggleDoor.GOGuid, tmp.command, tmp.id);
                                continue;
                            }

                            GameObjectTemplate info = GetGameObjectTemplate(data.Id);
                            if (info == null)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has gameobject with invalid entry (GUID: {1} Entry: {2}) in {3} for script id {4}",
                                    tableName, tmp.ToggleDoor.GOGuid, data.Id, tmp.command, tmp.id);
                                continue;
                            }

                            if (info.type != GameObjectTypes.Door)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has gameobject type ({1}) non supported by command {2} for script id {3}",
                                    tableName, info.entry, tmp.command, tmp.id);
                                continue;
                            }

                            break;
                        }

                    case ScriptCommands.RemoveAura:
                        {
                            if (!Global.SpellMgr.HasSpellInfo(tmp.RemoveAura.SpellID, Difficulty.None))
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` using non-existent spell (id: {1}) in SCRIPT_COMMAND_REMOVE_AURA for script id {2}",
                                    tableName, tmp.RemoveAura.SpellID, tmp.id);
                                continue;
                            }
                            if (Convert.ToBoolean((int)tmp.RemoveAura.Flags & ~0x1))                    // 1 bits (0, 1)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` using unknown flags in datalong2 ({1}) in SCRIPT_COMMAND_REMOVE_AURA for script id {2}",
                                    tableName, tmp.RemoveAura.Flags, tmp.id);
                                continue;
                            }
                            break;
                        }

                    case ScriptCommands.CastSpell:
                        {
                            if (!Global.SpellMgr.HasSpellInfo(tmp.CastSpell.SpellID, Difficulty.None))
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` using non-existent spell (id: {1}) in SCRIPT_COMMAND_CAST_SPELL for script id {2}",
                                    tableName, tmp.CastSpell.SpellID, tmp.id);
                                continue;
                            }
                            if ((int)tmp.CastSpell.Flags > 4)                      // targeting type
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` using unknown target in datalong2 ({1}) in SCRIPT_COMMAND_CAST_SPELL for script id {2}",
                                    tableName, tmp.CastSpell.Flags, tmp.id);
                                continue;
                            }
                            if ((int)tmp.CastSpell.Flags != 4 && Convert.ToBoolean(tmp.CastSpell.CreatureEntry & ~0x1))                      // 1 bit (0, 1)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` using unknown flags in dataint ({1}) in SCRIPT_COMMAND_CAST_SPELL for script id {2}",
                                    tableName, tmp.CastSpell.CreatureEntry, tmp.id);
                                continue;
                            }
                            else if ((int)tmp.CastSpell.Flags == 4 && GetCreatureTemplate((uint)tmp.CastSpell.CreatureEntry) == null)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` using invalid creature entry in dataint ({1}) in SCRIPT_COMMAND_CAST_SPELL for script id {2}",
                                    tableName, tmp.CastSpell.CreatureEntry, tmp.id);
                                continue;
                            }
                            break;
                        }

                    case ScriptCommands.CreateItem:
                        {
                            if (GetItemTemplate(tmp.CreateItem.ItemEntry) == null)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has nonexistent item (entry: {1}) in SCRIPT_COMMAND_CREATE_ITEM for script id {2}",
                                    tableName, tmp.CreateItem.ItemEntry, tmp.id);
                                continue;
                            }
                            if (tmp.CreateItem.Amount == 0)
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` SCRIPT_COMMAND_CREATE_ITEM but amount is {1} for script id {2}",
                                    tableName, tmp.CreateItem.Amount, tmp.id);
                                continue;
                            }
                            break;
                        }
                    case ScriptCommands.PlayAnimkit:
                        {
                            if (!CliDB.AnimKitStorage.ContainsKey(tmp.PlayAnimKit.AnimKitID))
                            {
                                Log.outError(LogFilter.Sql, "Table `{0}` has invalid AnimKid id (datalong = {1}) in SCRIPT_COMMAND_PLAY_ANIMKIT for script id {2}",
                                    tableName, tmp.PlayAnimKit.AnimKitID, tmp.id);
                                continue;
                            }
                            break;
                        }
                    case ScriptCommands.FieldSetDeprecated:
                    case ScriptCommands.FlagSetDeprecated:
                    case ScriptCommands.FlagRemoveDeprecated:
                        {
                            Log.outError(LogFilter.Sql, $"Table `{tableName}` uses deprecated direct updatefield modify command {tmp.command} for script id {tmp.id}");
                            continue;
                        }
                    default:
                        break;
                }

                if (!scripts.ContainsKey(tmp.id))
                    scripts[tmp.id] = new MultiMap<uint, ScriptInfo>();

                scripts[tmp.id].Add(tmp.delay, tmp);

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} script definitions in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadSpellScripts()
        {
            LoadScripts(ScriptsType.Spell);

            // check ids
            foreach (var script in sSpellScripts)
            {
                uint spellId = script.Key & 0x00FFFFFF;
                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
                if (spellInfo == null)
                {
                    Log.outError(LogFilter.Sql, "Table `spell_scripts` has not existing spell (Id: {0}) as script id", spellId);
                    continue;
                }

                byte i = (byte)((script.Key >> 24) & 0x000000FF);
                //check for correct spellEffect
                SpellEffectInfo effect = spellInfo.GetEffect(i);
                if (effect != null && (effect.Effect == 0 || (effect.Effect != SpellEffectName.ScriptEffect && effect.Effect != SpellEffectName.Dummy)))
                    Log.outError(LogFilter.Sql, "Table `spell_scripts` - spell {0} effect {1} is not SPELL_EFFECT_SCRIPT_EFFECT or SPELL_EFFECT_DUMMY", spellId, i);
            }
        }
        public void LoadEventScripts()
        {
            LoadScripts(ScriptsType.Event);

            List<uint> evt_scripts = new();
            // Load all possible script entries from gameobjects
            foreach (var go in _gameObjectTemplateStorage)
            {
                uint eventId = go.Value.GetEventScriptId();
                if (eventId != 0)
                    evt_scripts.Add(eventId);
            }

            // Load all possible script entries from spells
            foreach (SpellNameRecord spellNameEntry in CliDB.SpellNameStorage.Values)
            {
                SpellInfo spell = Global.SpellMgr.GetSpellInfo(spellNameEntry.Id, Difficulty.None);
                if (spell != null)
                {
                    foreach (SpellEffectInfo effect in spell.GetEffects())
                    {
                        if (effect != null && effect.Effect == SpellEffectName.SendEvent)
                            if (effect.MiscValue != 0)
                                evt_scripts.Add((uint)effect.MiscValue);
                    }
                }
            }

            foreach (var path_idx in CliDB.TaxiPathNodesByPath)
            {
                for (uint node_idx = 0; node_idx < path_idx.Value.Length; ++node_idx)
                {
                    TaxiPathNodeRecord node = path_idx.Value[node_idx];

                    if (node.ArrivalEventID != 0)
                        evt_scripts.Add(node.ArrivalEventID);

                    if (node.DepartureEventID != 0)
                        evt_scripts.Add(node.DepartureEventID);
                }
            }

            // Then check if all scripts are in above list of possible script entries
            foreach (var script in sEventScripts)
            {
                var id = evt_scripts.Find(p => p == script.Key);
                if (id == 0)
                    Log.outError(LogFilter.Sql, "Table `event_scripts` has script (Id: {0}) not referring to any gameobject_template type 10 data2 field, type 3 data6 field, type 13 data 2 field or any spell effect {1}",
                        script.Key, SpellEffectName.SendEvent);
            }
        }

        //Load WP Scripts
        public void LoadWaypointScripts()
        {
            LoadScripts(ScriptsType.Waypoint);

            List<uint> actionSet = new();

            foreach (var script in sWaypointScripts)
                actionSet.Add(script.Key);

            PreparedStatement stmt = DB.World.GetPreparedStatement(WorldStatements.SEL_WAYPOINT_DATA_ACTION);
            SQLResult result = DB.World.Query(stmt);

            if (!result.IsEmpty())
            {
                do
                {
                    uint action = result.Read<uint>(0);

                    actionSet.Remove(action);
                }
                while (result.NextRow());
            }

            foreach (var id in actionSet)
                Log.outError(LogFilter.Sql, "There is no waypoint which links to the waypoint script {0}", id);
        }
        public void LoadSpellScriptNames()
        {
            uint oldMSTime = Time.GetMSTime();

            spellScriptsStorage.Clear();                            // need for reload case

            SQLResult result = DB.World.Query("SELECT spell_id, ScriptName FROM spell_script_names");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 spell script names. DB table `spell_script_names` is empty!");
                return;
            }

            uint count = 0;
            do
            {
                int spellId = result.Read<int>(0);
                string scriptName = result.Read<string>(1);

                bool allRanks = false;
                if (spellId < 0)
                {
                    allRanks = true;
                    spellId = -spellId;
                }

                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo((uint)spellId, Difficulty.None);
                if (spellInfo == null)
                {
                    Log.outError(LogFilter.Sql, "Scriptname: `{0}` spell (Id: {1}) does not exist.", scriptName, spellId);
                    continue;
                }

                if (allRanks)
                {
                    if (!spellInfo.IsRanked())
                        Log.outError(LogFilter.Sql, "Scriptname: `{0}` spell (Id: {1}) has no ranks of spell.", scriptName, spellId);

                    if (spellInfo.GetFirstRankSpell().Id != spellId)
                    {
                        Log.outError(LogFilter.Sql, "Scriptname: `{0}` spell (Id: {1}) is not first rank of spell.", scriptName, spellId);
                        continue;
                    }
                    while (spellInfo != null)
                    {
                        spellScriptsStorage.Add(spellInfo.Id, GetScriptId(scriptName));
                        spellInfo = spellInfo.GetNextRankSpell();
                    }
                }
                else
                {
                    if (spellInfo.IsRanked())
                        Log.outError(LogFilter.Sql, "Scriptname: `{0}` spell (Id: {1}) is ranked spell. Perhaps not all ranks are assigned to this script.", scriptName, spellId);

                    spellScriptsStorage.Add(spellInfo.Id, GetScriptId(scriptName));
                }

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} spell script names in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void ValidateSpellScripts()
        {
            uint oldMSTime = Time.GetMSTime();

            if (spellScriptsStorage.Empty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Validated 0 scripts.");
                return;
            }

            uint count = 0;

            foreach (var script in spellScriptsStorage.KeyValueList)
            {
                SpellInfo spellEntry = Global.SpellMgr.GetSpellInfo(script.Key, Difficulty.None);

                Dictionary<SpellScriptLoader, uint> SpellScriptLoaders = Global.ScriptMgr.CreateSpellScriptLoaders(script.Key);
                foreach (var pair in SpellScriptLoaders)
                {
                    SpellScript spellScript = pair.Key.GetSpellScript();
                    bool valid = true;

                    if (spellScript == null)
                    {
                        Log.outError(LogFilter.Scripts, "Functions GetSpellScript() of script `{0}` do not return object - script skipped", GetScriptName(pair.Value));
                        valid = false;
                    }

                    if (spellScript != null)
                    {
                        spellScript._Init(pair.Key.GetName(), spellEntry.Id);
                        spellScript._Register();
                        if (!spellScript._Validate(spellEntry))
                            valid = false;
                    }

                    if (!valid)
                        spellScriptsStorage.Remove(script);
                }

                Dictionary<AuraScriptLoader, uint> AuraScriptLoaders = Global.ScriptMgr.CreateAuraScriptLoaders(script.Key);
                foreach (var pair in AuraScriptLoaders)
                {
                    AuraScript auraScript = pair.Key.GetAuraScript();
                    bool valid = true;

                    if (auraScript == null)
                    {
                        Log.outError(LogFilter.Scripts, "Functions GetAuraScript() of script `{0}` do not return object - script skipped", GetScriptName(pair.Value));
                        valid = false;
                    }

                    if (auraScript != null)
                    {
                        auraScript._Init(pair.Key.GetName(), spellEntry.Id);
                        auraScript._Register();
                        if (!auraScript._Validate(spellEntry))
                            valid = false;
                    }

                    if (!valid)
                        spellScriptsStorage.Remove(script);
                }
                ++count;
            }

            Log.outInfo(LogFilter.ServerLoading, "Validated {0} scripts in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        public List<uint> GetSpellScriptsBounds(uint spellId)
        {
            return spellScriptsStorage.LookupByKey(spellId);
        }
        public string GetScriptName(uint id)
        {
            return id < scriptNamesStorage.Count ? scriptNamesStorage[(int)id] : "";
        }
        public uint GetScriptId(string name)
        {
            // use binary search to find the script name in the sorted vector
            // assume "" is the first element
            if (string.IsNullOrEmpty(name))
                return 0;

            if (!scriptNamesStorage.Contains(name))
                return 0;

            return (uint)scriptNamesStorage.IndexOf(name);
        }
        public uint GetAreaTriggerScriptId(uint triggerid)
        {
            return areaTriggerScriptStorage.LookupByKey(triggerid);
        }
        public Dictionary<uint, MultiMap<uint, ScriptInfo>> GetScriptsMapByType(ScriptsType type)
        {
            switch (type)
            {
                case ScriptsType.Spell:
                    return sSpellScripts;
                case ScriptsType.Event:
                    return sEventScripts;
                case ScriptsType.Waypoint:
                    return sWaypointScripts;
                default:
                    return null;
            }
        }
        public string GetScriptsTableNameByType(ScriptsType type)
        {
            switch (type)
            {
                case ScriptsType.Spell:
                    return "spell_scripts";
                case ScriptsType.Event:
                    return "event_scripts";
                case ScriptsType.Waypoint:
                    return "waypoint_scripts";
                default:
                    return "";
            }
        }

        //Creatures
        public void LoadCreatureTemplates()
        {
            var time = Time.GetMSTime();

            PreparedStatement stmt = DB.World.GetPreparedStatement(WorldStatements.SEL_CREATURE_TEMPLATE);
            stmt.AddValue(0, 0);
            stmt.AddValue(1, 1);

            SQLResult result = DB.World.Query(stmt);
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creatures. DB table `creature_template` is empty.");
                return;
            }

            do
            {
                LoadCreatureTemplate(result.GetFields());
            } while (result.NextRow());

            // We load the creature models after loading but before checking
            LoadCreatureTemplateModels();

            // Checking needs to be done after loading because of the difficulty self referencing
            foreach (var template in _creatureTemplateStorage.Values)
                CheckCreatureTemplate(template);

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} creature definitions in {1} ms", _creatureTemplateStorage.Count, Time.GetMSTimeDiffToNow(time));
        }
        public void LoadCreatureTemplate(SQLFields fields)
        {
            uint entry = fields.Read<uint>(0);

            CreatureTemplate creature = new();
            creature.Entry = entry;

            for (var i = 0; i < SharedConst.MaxCreatureDifficulties; ++i)
                creature.DifficultyEntry[i] = fields.Read<uint>(1 + i);

            for (var i = 0; i < 2; ++i)
                creature.KillCredit[i] = fields.Read<uint>(4 + i);

            creature.Name = fields.Read<string>(6);
            creature.FemaleName = fields.Read<string>(7);
            creature.SubName = fields.Read<string>(8);
            creature.TitleAlt = fields.Read<string>(9);
            creature.IconName = fields.Read<string>(10);
            creature.GossipMenuId = fields.Read<uint>(11);
            creature.Minlevel = fields.Read<short>(12);
            creature.Maxlevel = fields.Read<short>(13);
            creature.HealthScalingExpansion = fields.Read<int>(14);
            creature.RequiredExpansion = fields.Read<uint>(15);
            creature.VignetteID = fields.Read<uint>(16);
            creature.Faction = fields.Read<uint>(17);
            creature.Npcflag = fields.Read<ulong>(18);
            creature.SpeedWalk = fields.Read<float>(19);
            creature.SpeedRun = fields.Read<float>(20);
            creature.Scale = fields.Read<float>(21);
            creature.Rank = (CreatureEliteType)fields.Read<uint>(22);
            creature.DmgSchool = fields.Read<uint>(23);
            creature.BaseAttackTime = fields.Read<uint>(24);
            creature.RangeAttackTime = fields.Read<uint>(25);
            creature.BaseVariance = fields.Read<float>(26);
            creature.RangeVariance = fields.Read<float>(27);
            creature.UnitClass = fields.Read<uint>(28);
            creature.UnitFlags = (UnitFlags)fields.Read<uint>(29);
            creature.UnitFlags2 = fields.Read<uint>(30);
            creature.UnitFlags3 = fields.Read<uint>(31);
            creature.DynamicFlags = fields.Read<uint>(32);
            creature.Family = (CreatureFamily)fields.Read<uint>(33);
            creature.TrainerClass = (Class)fields.Read<byte>(34);
            creature.CreatureType = (CreatureType)fields.Read<byte>(35);
            creature.TypeFlags = (CreatureTypeFlags)fields.Read<uint>(36);
            creature.TypeFlags2 = fields.Read<uint>(37);
            creature.LootId = fields.Read<uint>(38);
            creature.PickPocketId = fields.Read<uint>(39);
            creature.SkinLootId = fields.Read<uint>(40);

            for (var i = (int)SpellSchools.Holy; i < (int)SpellSchools.Max; ++i)
                creature.Resistance[i] = fields.Read<int>(41 + i - 1);

            for (var i = 0; i < SharedConst.MaxCreatureSpells; ++i)
                creature.Spells[i] = fields.Read<uint>(47 + i);

            creature.VehicleId = fields.Read<uint>(55);
            creature.MinGold = fields.Read<uint>(56);
            creature.MaxGold = fields.Read<uint>(57);
            creature.AIName = fields.Read<string>(58);
            creature.MovementType = fields.Read<uint>(59);
            creature.InhabitType = (InhabitType)fields.Read<uint>(60);
            creature.HoverHeight = fields.Read<float>(61);
            creature.ModHealth = fields.Read<float>(62);
            creature.ModHealthExtra = fields.Read<float>(63);
            creature.ModMana = fields.Read<float>(64);
            creature.ModManaExtra = fields.Read<float>(65);
            creature.ModArmor = fields.Read<float>(66);
            creature.ModDamage = fields.Read<float>(67);
            creature.ModExperience = fields.Read<float>(68);
            creature.RacialLeader = fields.Read<bool>(69);
            creature.MovementId = fields.Read<uint>(70);
            creature.WidgetSetID = fields.Read<int>(71);
            creature.WidgetSetUnitConditionID = fields.Read<int>(72);
            creature.RegenHealth = fields.Read<bool>(73);
            creature.MechanicImmuneMask = fields.Read<uint>(74);
            creature.SpellSchoolImmuneMask = fields.Read<uint>(75);
            creature.FlagsExtra = (CreatureFlagsExtra)fields.Read<uint>(76);
            creature.ScriptID = GetScriptId(fields.Read<string>(77));

            _creatureTemplateStorage[entry] = creature;
        }

        void LoadCreatureTemplateModels()
        {
            uint oldMSTime = Time.GetMSTime();
            //                                         0           1                  2             3
            SQLResult result = DB.World.Query("SELECT CreatureID, CreatureDisplayID, DisplayScale, Probability FROM creature_template_model ORDER BY Idx ASC");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature template model definitions. DB table `creature_template_model` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint creatureId = result.Read<uint>(0);
                uint creatureDisplayId = result.Read<uint>(1);
                float displayScale = result.Read<float>(2);
                float probability = result.Read<float>(3);

                CreatureTemplate cInfo = GetCreatureTemplate(creatureId);
                if (cInfo == null)
                {
                    Log.outError(LogFilter.Sql, $"Creature template (Entry: {creatureId}) does not exist but has a record in `creature_template_model`");
                    continue;
                }

                CreatureDisplayInfoRecord displayEntry = CliDB.CreatureDisplayInfoStorage.LookupByKey(creatureDisplayId);
                if (displayEntry == null)
                {
                    Log.outError(LogFilter.Sql, $"Creature (Entry: {creatureId}) lists non-existing CreatureDisplayID id ({creatureDisplayId}), this can crash the client.");
                    continue;
                }

                CreatureModelInfo modelInfo = GetCreatureModelInfo(creatureDisplayId);
                if (modelInfo == null)
                    Log.outError(LogFilter.Sql, $"No model data exist for `CreatureDisplayID` = {creatureDisplayId} listed by creature (Entry: {creatureId}).");

                if (displayScale <= 0.0f)
                    displayScale = 1.0f;

                cInfo.Models.Add(new CreatureModel(creatureDisplayId, displayScale, probability));
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature template models in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }
        public void LoadCreatureTemplateAddons()
        {
            var time = Time.GetMSTime();
            //                                        0       1        2      3       4       5      6          7                8             9                       10
            SQLResult result = DB.World.Query("SELECT entry, path_id, mount, bytes1, bytes2, emote, aiAnimKit, movementAnimKit, meleeAnimKit, visibilityDistanceType, auras FROM creature_template_addon");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature template addon definitions. DB table `creature_template_addon` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint entry = result.Read<uint>(0);
                if (GetCreatureTemplate(entry) == null)
                {
                    Log.outError(LogFilter.Sql, "Creature template (Entry: {0}) does not exist but has a record in `creature_template_addon`", entry);
                    continue;
                }

                CreatureAddon creatureAddon = new();
                creatureAddon.path_id = result.Read<uint>(1);
                creatureAddon.mount = result.Read<uint>(2);
                creatureAddon.bytes1 = result.Read<uint>(3);
                creatureAddon.bytes2 = result.Read<uint>(4);
                creatureAddon.emote = result.Read<uint>(5);
                creatureAddon.aiAnimKit = result.Read<ushort>(6);
                creatureAddon.movementAnimKit = result.Read<ushort>(7);
                creatureAddon.meleeAnimKit = result.Read<ushort>(8);
                creatureAddon.visibilityDistanceType = (VisibilityDistanceType)result.Read<byte>(9);

                var tokens = new StringArray(result.Read<string>(10), ' ');

                creatureAddon.auras = new uint[tokens.Length];
                byte i = 0;
                for (var c = 0; c < tokens.Length; ++c)
                {
                    string id = tokens[c].Trim().Replace(",", "");
                    if (!uint.TryParse(id, out uint spellId))
                        continue;

                    SpellInfo AdditionalSpellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
                    if (AdditionalSpellInfo == null)
                    {
                        Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has wrong spell {1} defined in `auras` field in `creature_template_addon`.", entry, spellId);
                        continue;
                    }

                    if (AdditionalSpellInfo.HasAura(AuraType.ControlVehicle))
                        Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has SPELL_AURA_CONTROL_VEHICLE aura {1} defined in `auras` field in `creature_template_addon`.", entry, spellId);

                    if (creatureAddon.auras.Contains(spellId))
                    {
                        Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has duplicate aura (spell {1}) in `auras` field in `creature_template_addon`.", entry, spellId);
                        continue;
                    }

                    creatureAddon.auras[i++] = spellId;
                }

                if (creatureAddon.mount != 0)
                {
                    if (CliDB.CreatureDisplayInfoStorage.LookupByKey(creatureAddon.mount) == null)
                    {
                        Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has invalid displayInfoId ({1}) for mount defined in `creature_template_addon`", entry, creatureAddon.mount);
                        creatureAddon.mount = 0;
                    }
                }

                if (!CliDB.EmotesStorage.ContainsKey(creatureAddon.emote))
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has invalid emote ({1}) defined in `creatureaddon`.", entry, creatureAddon.emote);
                    creatureAddon.emote = 0;
                }

                if (creatureAddon.aiAnimKit != 0 && !CliDB.AnimKitStorage.ContainsKey(creatureAddon.aiAnimKit))
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has invalid aiAnimKit ({1}) defined in `creature_template_addon`.", entry, creatureAddon.aiAnimKit);
                    creatureAddon.aiAnimKit = 0;
                }

                if (creatureAddon.movementAnimKit != 0 && !CliDB.AnimKitStorage.ContainsKey(creatureAddon.movementAnimKit))
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has invalid movementAnimKit ({1}) defined in `creature_template_addon`.", entry, creatureAddon.movementAnimKit);
                    creatureAddon.movementAnimKit = 0;
                }

                if (creatureAddon.meleeAnimKit != 0 && !CliDB.AnimKitStorage.ContainsKey(creatureAddon.meleeAnimKit))
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has invalid meleeAnimKit ({1}) defined in `creature_template_addon`.", entry, creatureAddon.meleeAnimKit);
                    creatureAddon.meleeAnimKit = 0;
                }

                if (creatureAddon.visibilityDistanceType >= VisibilityDistanceType.Max)
                {
                    Log.outError(LogFilter.Sql, $"Creature (Entry: {entry}) has invalid visibilityDistanceType ({creatureAddon.visibilityDistanceType}) defined in `creature_template_addon`.");
                    creatureAddon.visibilityDistanceType = VisibilityDistanceType.Normal;
                }

                creatureTemplateAddonStorage.Add(entry, creatureAddon);
                count++;
            }
            while (result.NextRow());
            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} creature template addons in {1} ms", count, Time.GetMSTimeDiffToNow(time));
        }
        public void LoadCreatureAddons()
        {
            var time = Time.GetMSTime();
            //                                        0       1       2      3       4       5        6             7              8          9                       10
            SQLResult result = DB.World.Query("SELECT guid, path_id, mount, bytes1, bytes2, emote, aiAnimKit, movementAnimKit, meleeAnimKit, visibilityDistanceType, auras FROM creature_addon");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature addon definitions. DB table `creature_addon` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                ulong guid = result.Read<ulong>(0);
                CreatureData creData = GetCreatureData(guid);
                if (creData == null)
                {
                    Log.outError(LogFilter.Sql, "Creature (GUID: {0}) does not exist but has a record in `creatureaddon`", guid);
                    continue;
                }

                CreatureAddon creatureAddon = new();

                creatureAddon.path_id = result.Read<uint>(1);
                if (creData.movementType == (byte)MovementGeneratorType.Waypoint && creatureAddon.path_id == 0)
                {
                    creData.movementType = (byte)MovementGeneratorType.Idle;
                    Log.outError(LogFilter.Sql, "Creature (GUID {0}) has movement type set to WAYPOINTMOTIONTYPE but no path assigned", guid);
                }

                creatureAddon.mount = result.Read<uint>(2);
                creatureAddon.bytes1 = result.Read<uint>(3);
                creatureAddon.bytes2 = result.Read<uint>(4);
                creatureAddon.emote = result.Read<uint>(5);
                creatureAddon.aiAnimKit = result.Read<ushort>(6);
                creatureAddon.movementAnimKit = result.Read<ushort>(7);
                creatureAddon.meleeAnimKit = result.Read<ushort>(8);
                creatureAddon.visibilityDistanceType = (VisibilityDistanceType)result.Read<byte>(9);

                var tokens = new StringArray(result.Read<string>(10), ' ');
                byte i = 0;
                creatureAddon.auras = new uint[tokens.Length];
                for (var c = 0; c < tokens.Length; ++c)
                {
                    string id = tokens[c].Trim().Replace(",", "");
                    if (!uint.TryParse(id, out uint spellId))
                        continue;

                    SpellInfo AdditionalSpellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
                    if (AdditionalSpellInfo == null)
                    {
                        Log.outError(LogFilter.Sql, "Creature (GUID: {0}) has wrong spell {1} defined in `auras` field in `creatureaddon`.", guid, spellId);
                        continue;
                    }

                    if (AdditionalSpellInfo.HasAura(AuraType.ControlVehicle))
                        Log.outError(LogFilter.Sql, "Creature (GUID: {0}) has SPELL_AURA_CONTROL_VEHICLE aura {1} defined in `auras` field in `creature_addon`.", guid, spellId);

                    if (creatureAddon.auras.Contains(spellId))
                    {
                        Log.outError(LogFilter.Sql, "Creature (GUID: {0}) has duplicate aura (spell {1}) in `auras` field in `creature_addon`.", guid, spellId);
                        continue;
                    }

                    creatureAddon.auras[i++] = spellId;
                }

                if (creatureAddon.mount != 0)
                {
                    if (!CliDB.CreatureDisplayInfoStorage.ContainsKey(creatureAddon.mount))
                    {
                        Log.outError(LogFilter.Sql, "Creature (GUID: {0}) has invalid displayInfoId ({1}) for mount defined in `creatureaddon`", guid, creatureAddon.mount);
                        creatureAddon.mount = 0;
                    }
                }

                if (!CliDB.EmotesStorage.ContainsKey(creatureAddon.emote))
                {
                    Log.outError(LogFilter.Sql, "Creature (GUID: {0}) has invalid emote ({1}) defined in `creatureaddon`.", guid, creatureAddon.emote);
                    creatureAddon.emote = 0;
                }


                if (creatureAddon.aiAnimKit != 0 && !CliDB.AnimKitStorage.ContainsKey(creatureAddon.aiAnimKit))
                {
                    Log.outError(LogFilter.Sql, "Creature (Guid: {0}) has invalid aiAnimKit ({1}) defined in `creature_addon`.", guid, creatureAddon.aiAnimKit);
                    creatureAddon.aiAnimKit = 0;
                }

                if (creatureAddon.movementAnimKit != 0 && !CliDB.AnimKitStorage.ContainsKey(creatureAddon.movementAnimKit))
                {
                    Log.outError(LogFilter.Sql, "Creature (Guid: {0}) has invalid movementAnimKit ({1}) defined in `creature_addon`.", guid, creatureAddon.movementAnimKit);
                    creatureAddon.movementAnimKit = 0;
                }

                if (creatureAddon.meleeAnimKit != 0 && !CliDB.AnimKitStorage.ContainsKey(creatureAddon.meleeAnimKit))
                {
                    Log.outError(LogFilter.Sql, "Creature (Guid: {0}) has invalid meleeAnimKit ({1}) defined in `creature_addon`.", guid, creatureAddon.meleeAnimKit);
                    creatureAddon.meleeAnimKit = 0;
                }

                if (creatureAddon.visibilityDistanceType >= VisibilityDistanceType.Max)
                {
                    Log.outError(LogFilter.Sql, $"Creature (GUID: {guid}) has invalid visibilityDistanceType ({creatureAddon.visibilityDistanceType}) defined in `creature_addon`.");
                    creatureAddon.visibilityDistanceType = VisibilityDistanceType.Normal;
                }

                creatureAddonStorage.Add(guid, creatureAddon);
                count++;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} creature addons in {1} ms", count, Time.GetMSTimeDiffToNow(time));
        }
        public void LoadCreatureQuestItems()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                          0              1      2
            SQLResult result = DB.World.Query("SELECT CreatureEntry, ItemId, Idx FROM creature_questitem ORDER BY Idx ASC");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature quest items. DB table `creature_questitem` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint entry = result.Read<uint>(0);
                uint item = result.Read<uint>(1);
                uint idx = result.Read<uint>(2);

                if (!_creatureTemplateStorage.ContainsKey(entry))
                {
                    Log.outError(LogFilter.Sql, "Table `creature_questitem` has data for nonexistent creature (entry: {0}, idx: {1}), skipped", entry, idx);
                    continue;
                }

                if (!CliDB.ItemStorage.ContainsKey(item))
                {
                    Log.outError(LogFilter.Sql, "Table `creature_questitem` has nonexistent item (ID: {0}) in creature (entry: {1}, idx: {2}), skipped", item, entry, idx);
                    continue;
                }

                _creatureQuestItemStorage.Add(entry, item);

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} creature quest items in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadEquipmentTemplates()
        {
            var time = Time.GetMSTime();
            //                                                0   1        2                 3            4
            SQLResult result = DB.World.Query("SELECT CreatureID, ID, ItemID1, AppearanceModID1, ItemVisual1, " +
                //5                 6            7       8                 9           10
                "ItemID2, AppearanceModID2, ItemVisual2, ItemID3, AppearanceModID3, ItemVisual3 " +
                "FROM creature_equip_template");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature equipment templates. DB table `creature_equip_template` is empty!");
                return;
            }

            uint count = 0;
            do
            {
                uint entry = result.Read<uint>(0);

                if (GetCreatureTemplate(entry) == null)
                {
                    Log.outError(LogFilter.Sql, "Creature template (CreatureID: {0}) does not exist but has a record in `creature_equip_template`", entry);
                    continue;
                }

                uint id = result.Read<uint>(1);

                EquipmentInfo equipmentInfo = new();

                for (var i = 0; i < SharedConst.MaxEquipmentItems; ++i)
                {
                    equipmentInfo.Items[i].ItemId = result.Read<uint>(2 + i * 3);
                    equipmentInfo.Items[i].AppearanceModId = result.Read<ushort>(3 + i * 3);
                    equipmentInfo.Items[i].ItemVisual = result.Read<ushort>(4 + i * 3);

                    if (equipmentInfo.Items[i].ItemId == 0)
                        continue;

                    var dbcItem = CliDB.ItemStorage.LookupByKey(equipmentInfo.Items[i].ItemId);
                    if (dbcItem == null)
                    {
                        Log.outError(LogFilter.Sql, "Unknown item (ID: {0}) in creature_equip_template.ItemID{1} for CreatureID  = {2}, forced to 0.",
                            equipmentInfo.Items[i].ItemId, i + 1, entry);
                        equipmentInfo.Items[i].ItemId = 0;
                        continue;
                    }

                    if (Global.DB2Mgr.GetItemModifiedAppearance(equipmentInfo.Items[i].ItemId, equipmentInfo.Items[i].AppearanceModId) == null)
                    {
                        Log.outError(LogFilter.Sql, "Unknown item appearance for (ID: {0}, AppearanceModID: {1}) pair in creature_equip_template.ItemID{2} creature_equip_template.AppearanceModID{3} " +
                            "for CreatureID: {4} and ID: {5}, forced to default.",
                            equipmentInfo.Items[i].ItemId, equipmentInfo.Items[i].AppearanceModId, i + 1, i + 1, entry, id);
                        ItemModifiedAppearanceRecord defaultAppearance = Global.DB2Mgr.GetDefaultItemModifiedAppearance(equipmentInfo.Items[i].ItemId);
                        if (defaultAppearance != null)
                            equipmentInfo.Items[i].AppearanceModId = defaultAppearance.ItemAppearanceModifierID;
                        else
                            equipmentInfo.Items[i].AppearanceModId = 0;
                        continue;
                    }

                    if (dbcItem.inventoryType != InventoryType.Weapon &&
                        dbcItem.inventoryType != InventoryType.Shield &&
                        dbcItem.inventoryType != InventoryType.Ranged &&
                        dbcItem.inventoryType != InventoryType.Weapon2Hand &&
                        dbcItem.inventoryType != InventoryType.WeaponMainhand &&
                        dbcItem.inventoryType != InventoryType.WeaponOffhand &&
                        dbcItem.inventoryType != InventoryType.Holdable &&
                        dbcItem.inventoryType != InventoryType.Thrown &&
                        dbcItem.inventoryType != InventoryType.RangedRight)
                    {
                        Log.outError(LogFilter.Sql, "Item (ID {0}) in creature_equip_template.ItemID{1} for CreatureID  = {2} is not equipable in a hand, forced to 0.",
                            equipmentInfo.Items[i].ItemId, i + 1, entry);
                        equipmentInfo.Items[i].ItemId = 0;
                    }
                }

                equipmentInfoStorage.Add(entry, Tuple.Create(id, equipmentInfo));
                ++count;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} equipment templates in {1} ms", count, Time.GetMSTimeDiffToNow(time));
        }
        public void LoadCreatureClassLevelStats()
        {
            var time = Time.GetMSTime();

            creatureBaseStatsStorage.Clear();

            //                                         0      1      2         3            4
            SQLResult result = DB.World.Query("SELECT level, class, basemana, attackpower, rangedattackpower FROM creature_classlevelstats");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature base stats. DB table `creature_classlevelstats` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                byte Level = result.Read<byte>(0);
                byte _class = result.Read<byte>(1);

                if (_class == 0 || ((1 << (_class - 1)) & (int)Class.ClassMaskAllCreatures) == 0)
                    Log.outError(LogFilter.Sql, "Creature base stats for level {0} has invalid class {1}", Level, _class);

                CreatureBaseStats stats = new();

                stats.BaseMana = result.Read<uint>(2);
                stats.AttackPower = result.Read<ushort>(3);
                stats.RangedAttackPower = result.Read<ushort>(4);

                creatureBaseStatsStorage.Add(MathFunctions.MakePair16(Level, _class), stats);

                ++count;
            } while (result.NextRow());

            foreach (var creatureTemplate in _creatureTemplateStorage.Values)
            {
                for (short lvl = creatureTemplate.Minlevel; lvl <= creatureTemplate.Maxlevel; ++lvl)
                {
                    if (creatureBaseStatsStorage.LookupByKey(MathFunctions.MakePair16((uint)lvl, creatureTemplate.UnitClass)) == null)
                        Log.outError(LogFilter.Sql, "Missing base stats for creature class {0} level {1}", creatureTemplate.UnitClass, lvl);
                }
            }

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} creature base stats in {1} ms", count, Time.GetMSTimeDiffToNow(time));
        }
        public void LoadCreatureModelInfo()
        {
            var time = Time.GetMSTime();
            SQLResult result = DB.World.Query("SELECT DisplayID, BoundingRadius, CombatReach, DisplayID_Other_Gender FROM creature_model_info");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature model definitions. DB table `creaturemodelinfo` is empty.");
                return;
            }

            // List of model FileDataIDs that the client treats as invisible stalker
            uint[] trigggerCreatureModelFileID = { 124640, 124641, 124642, 343863, 439302 };

            uint count = 0;
            do
            {
                uint displayId = result.Read<uint>(0);

                var creatureDisplay = CliDB.CreatureDisplayInfoStorage.LookupByKey(displayId);
                if (creatureDisplay == null)
                {
                    Log.outError(LogFilter.Sql, "Table `creature_model_info` has a non-existent DisplayID (ID: {0}). Skipped.", displayId);
                    continue;
                }

                CreatureModelInfo modelInfo = new();
                modelInfo.BoundingRadius = result.Read<float>(1);
                modelInfo.CombatReach = result.Read<float>(2);
                modelInfo.DisplayIdOtherGender = result.Read<uint>(3);
                modelInfo.gender = creatureDisplay.Gender;

                // Checks
                if (modelInfo.gender == (sbyte)Gender.Unknown)
                    modelInfo.gender = (sbyte)Gender.Male;

                if (modelInfo.DisplayIdOtherGender != 0 && !CliDB.CreatureDisplayInfoStorage.ContainsKey(modelInfo.DisplayIdOtherGender))
                {
                    Log.outError(LogFilter.Sql, "Table `creature_model_info` has a non-existent DisplayID_Other_Gender (ID: {0}) being used by DisplayID (ID: {1}).", modelInfo.DisplayIdOtherGender, displayId);
                    modelInfo.DisplayIdOtherGender = 0;
                }

                if (modelInfo.CombatReach < 0.1f)
                    modelInfo.CombatReach = SharedConst.DefaultPlayerCombatReach;

                CreatureModelDataRecord modelData = CliDB.CreatureModelDataStorage.LookupByKey(creatureDisplay.ModelID);
                if (modelData != null)
                {
                    for (uint i = 0; i < 5; ++i)
                    {
                        if (modelData.FileDataID == trigggerCreatureModelFileID[i])
                        {
                            modelInfo.IsTrigger = true;
                            break;
                        }
                    }
                }

                creatureModelStorage.Add(displayId, modelInfo);
                count++;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} creature model based info in {1} ms", count, Time.GetMSTimeDiffToNow(time));
        }
        public void LoadCreatureScalingData()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0      1             2                3                4                     5                     6
            SQLResult result = DB.World.Query("SELECT Entry, DifficultyID, LevelScalingMin, LevelScalingMax, LevelScalingDeltaMin, LevelScalingDeltaMax, ContentTuningID FROM creature_template_scaling ORDER BY Entry");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature template scaling definitions. DB table `creature_template_scaling` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint entry = result.Read<uint>(0);
                Difficulty difficulty = (Difficulty)result.Read<byte>(1);

                var template = _creatureTemplateStorage.LookupByKey(entry);
                if (template == null)
                {
                    Log.outError(LogFilter.Sql, $"Creature template (Entry: {entry}) does not exist but has a record in `creature_template_scaling`");
                    continue;
                }

                CreatureLevelScaling creatureLevelScaling = new();
                creatureLevelScaling.MinLevel = result.Read<ushort>(2);
                creatureLevelScaling.MaxLevel = result.Read<ushort>(3);
                creatureLevelScaling.DeltaLevelMin = result.Read<short>(4);
                creatureLevelScaling.DeltaLevelMax = result.Read<short>(5);
                creatureLevelScaling.ContentTuningID = result.Read<uint>(6);

                template.scalingStorage[difficulty] = creatureLevelScaling;

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature template scaling data in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }
        public void CheckCreatureTemplate(CreatureTemplate cInfo)
        {
            if (cInfo == null)
                return;

            bool ok = true;                                     // bool to allow continue outside this loop
            for (uint diff = 0; diff < SharedConst.MaxCreatureDifficulties && ok; ++diff)
            {
                if (cInfo.DifficultyEntry[diff] == 0)
                    continue;
                ok = false;                                     // will be set to true at the end of this loop again

                CreatureTemplate difficultyInfo = GetCreatureTemplate(cInfo.DifficultyEntry[diff]);
                if (difficultyInfo == null)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has `difficulty_entry_{1}`={2} but creature entry {3} does not exist.",
                        cInfo.Entry, diff + 1, cInfo.DifficultyEntry[diff], cInfo.DifficultyEntry[diff]);
                    continue;
                }

                bool ok2 = true;
                for (uint diff2 = 0; diff2 < SharedConst.MaxCreatureDifficulties && ok2; ++diff2)
                {
                    ok2 = false;
                    if (_difficultyEntries[diff2].Contains(cInfo.Entry))
                    {
                        Log.outError(LogFilter.Sql, "Creature (Entry: {0}) is listed as `difficulty_entry_{1}` of another creature, but itself lists {2} in `difficulty_entry_{3}`.",
                            cInfo.Entry, diff2 + 1, cInfo.DifficultyEntry[diff], diff + 1);
                        continue;
                    }

                    if (_difficultyEntries[diff2].Contains(cInfo.DifficultyEntry[diff]))
                    {
                        Log.outError(LogFilter.Sql, "Creature (Entry: {0}) already listed as `difficulty_entry_{1}` for another entry.", cInfo.DifficultyEntry[diff], diff2 + 1);
                        continue;
                    }

                    if (_hasDifficultyEntries[diff2].Contains(cInfo.DifficultyEntry[diff]))
                    {
                        Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has `difficulty_entry_{1}`={2} but creature entry {3} has itself a value in `difficulty_entry_{4}`.",
                            cInfo.Entry, diff + 1, cInfo.DifficultyEntry[diff], cInfo.DifficultyEntry[diff], diff2 + 1);
                        continue;
                    }
                    ok2 = true;
                }
                if (!ok2)
                    continue;

                if (cInfo.HealthScalingExpansion > difficultyInfo.HealthScalingExpansion)
                {
                    Log.outError(LogFilter.Sql, "Creature (Id: {0}, Expansion {1}) has different `HealthScalingExpansion` in difficulty {2} mode (Id: {3}, Expansion: {4}).",
                        cInfo.Entry, cInfo.HealthScalingExpansion, diff + 1, cInfo.DifficultyEntry[diff], difficultyInfo.HealthScalingExpansion);
                }

                if (cInfo.Minlevel > difficultyInfo.Minlevel)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}, minlevel: {1}) has lower `minlevel` in difficulty {2} mode (Entry: {3}, minlevel: {4}).",
                        cInfo.Entry, cInfo.Minlevel, diff + 1, cInfo.DifficultyEntry[diff], difficultyInfo.Minlevel);
                }

                if (cInfo.Maxlevel > difficultyInfo.Maxlevel)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}, maxlevel: {1}) has lower `maxlevel` in difficulty {2} mode (Entry: {3}, maxlevel: {4}).",
                        cInfo.Entry, cInfo.Maxlevel, diff + 1, cInfo.DifficultyEntry[diff], difficultyInfo.Maxlevel);
                }

                if (cInfo.Faction != difficultyInfo.Faction)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}, faction: {1}) has different `faction` in difficulty {2} mode (Entry: {3}, faction: {4}).",
                        cInfo.Entry, cInfo.Faction, diff + 1, cInfo.DifficultyEntry[diff], difficultyInfo.Faction);
                }

                if (cInfo.UnitClass != difficultyInfo.UnitClass)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}, class: {1}) has different `unit_class` in difficulty {2} mode (Entry: {3}, class: {4}).",
                        cInfo.Entry, cInfo.UnitClass, diff + 1, cInfo.DifficultyEntry[diff], difficultyInfo.UnitClass);
                    continue;
                }

                if (cInfo.Npcflag != difficultyInfo.Npcflag)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has different `npcflag` in difficulty {1} mode (Entry: {2}).", cInfo.Entry, diff + 1, cInfo.DifficultyEntry[diff]);
                    Log.outError(LogFilter.Sql, "Possible FIX: UPDATE `creature_template` SET `npcflag`=`npcflag`^{0} WHERE `entry`={1};", cInfo.Npcflag ^ difficultyInfo.Npcflag, cInfo.DifficultyEntry[diff]);
                    continue;
                }

                if (cInfo.DmgSchool != difficultyInfo.DmgSchool)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}, `dmgschool`: {1}) has different `dmgschool` in difficulty {2} mode (Entry: {3}, `dmgschool`: {4}).",
                        cInfo.Entry, cInfo.DmgSchool, diff + 1, cInfo.DifficultyEntry[diff], difficultyInfo.DmgSchool);
                    Log.outError(LogFilter.Sql, "Possible FIX: UPDATE `creature_template` SET `dmgschool`={0} WHERE `entry`={1};", cInfo.DmgSchool, cInfo.DifficultyEntry[diff]);
                }

                if (cInfo.UnitFlags2 != difficultyInfo.UnitFlags2)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}, `unit_flags2`: {1}) has different `unit_flags2` in difficulty {2} mode (Entry: {3}, `unit_flags2`: {4}).",
                        cInfo.Entry, cInfo.UnitFlags2, diff + 1, cInfo.DifficultyEntry[diff], difficultyInfo.UnitFlags2);
                    Log.outError(LogFilter.Sql, "Possible FIX: UPDATE `creature_template` SET `unit_flags2`=`unit_flags2`^{0} WHERE `entry`={1};", cInfo.UnitFlags2 ^ difficultyInfo.UnitFlags2, cInfo.DifficultyEntry[diff]);
                }

                if (cInfo.Family != difficultyInfo.Family)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}, family: {1}) has different `family` in difficulty {2} mode (Entry: {3}, family: {4}).",
                        cInfo.Entry, cInfo.Family, diff + 1, cInfo.DifficultyEntry[diff], difficultyInfo.Family);
                }

                if (cInfo.TrainerClass != difficultyInfo.TrainerClass)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has different `trainer_class` in difficulty {1} mode (Entry: {2}).", cInfo.Entry, diff + 1, cInfo.DifficultyEntry[diff]);
                    continue;
                }

                if (cInfo.CreatureType != difficultyInfo.CreatureType)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}, type: {1}) has different `type` in difficulty {2} mode (Entry: {3}, type: {4}).",
                        cInfo.Entry, cInfo.CreatureType, diff + 1, cInfo.DifficultyEntry[diff], difficultyInfo.CreatureType);
                }

                if (cInfo.VehicleId == 0 && difficultyInfo.VehicleId != 0)
                {
                    Log.outError(LogFilter.Sql, "Non-vehicle Creature (Entry: {0}, VehicleId: {1}) has `VehicleId` set in difficulty {2} mode (Entry: {3}, VehicleId: {4}).",
                        cInfo.Entry, cInfo.VehicleId, diff + 1, cInfo.DifficultyEntry[diff], difficultyInfo.VehicleId);
                }

                if (cInfo.RegenHealth != difficultyInfo.RegenHealth)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}, RegenHealth: {1}) has different `RegenHealth` in difficulty {2} mode (Entry: {3}, RegenHealth: {4}).",
                        cInfo.Entry, cInfo.RegenHealth, diff + 1, cInfo.DifficultyEntry[diff], difficultyInfo.RegenHealth);
                    Log.outError(LogFilter.Sql, "Possible FIX: UPDATE `creature_template` SET `RegenHealth`={0} WHERE `entry`={1};", cInfo.RegenHealth, cInfo.DifficultyEntry[diff]);
                }

                uint differenceMask = cInfo.MechanicImmuneMask & (~difficultyInfo.MechanicImmuneMask);
                if (differenceMask != 0)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}, mechanic_immune_mask: {1}) has weaker immunities in difficulty {2} mode (Entry: {3}, mechanic_immune_mask: {4}).",
                        cInfo.Entry, cInfo.MechanicImmuneMask, diff + 1, cInfo.DifficultyEntry[diff], difficultyInfo.MechanicImmuneMask);
                    Log.outError(LogFilter.Sql, "Possible FIX: UPDATE `creature_template` SET `mechanic_immune_mask`=`mechanic_immune_mask`|{0} WHERE `entry`={1};", differenceMask, cInfo.DifficultyEntry[diff]);
                }

                differenceMask = (uint)((cInfo.FlagsExtra ^ difficultyInfo.FlagsExtra) & (~CreatureFlagsExtra.InstanceBind));
                if (differenceMask != 0)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}, flags_extra: {1}) has different `flags_extra` in difficulty {2} mode (Entry: {3}, flags_extra: {4}).",
                        cInfo.Entry, cInfo.FlagsExtra, diff + 1, cInfo.DifficultyEntry[diff], difficultyInfo.FlagsExtra);
                    Log.outError(LogFilter.Sql, "Possible FIX: UPDATE `creature_template` SET `flags_extra`=`flags_extra`^{0} WHERE `entry`={1};", differenceMask, cInfo.DifficultyEntry[diff]);
                }

                if (difficultyInfo.AIName.IsEmpty())
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}) lists difficulty {1} mode entry {2} with `AIName` filled in. `AIName` of difficulty 0 mode creature is always used instead.",
                        cInfo.Entry, diff + 1, cInfo.DifficultyEntry[diff]);
                    continue;
                }

                if (difficultyInfo.ScriptID != 0)
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}) lists difficulty {1} mode entry {2} with `ScriptName` filled in. `ScriptName` of difficulty 0 mode creature is always used instead.",
                        cInfo.Entry, diff + 1, cInfo.DifficultyEntry[diff]);
                    continue;
                }

                _hasDifficultyEntries[diff].Add(cInfo.Entry);
                _difficultyEntries[diff].Add(cInfo.DifficultyEntry[diff]);
                ok = true;
            }

            if (cInfo.MinGold > cInfo.MaxGold)
            {
                Log.outError(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) has `mingold` {cInfo.MinGold} which is greater than `maxgold` {cInfo.MaxGold}, setting `maxgold` to {cInfo.MinGold}.");
                cInfo.MaxGold = cInfo.MinGold;
            }

            if (!CliDB.FactionTemplateStorage.ContainsKey(cInfo.Faction))
            {
                Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has non-existing faction template ({1}). This can lead to crashes, set to faction 35", cInfo.Entry, cInfo.Faction);
                cInfo.Faction = 35;
            }

            for (int k = 0; k < SharedConst.MaxCreatureKillCredit; ++k)
            {
                if (cInfo.KillCredit[k] != 0)
                {
                    if (GetCreatureTemplate(cInfo.KillCredit[k]) == null)
                    {
                        Log.outError(LogFilter.Sql, "Creature (Entry: {0}) lists non-existing creature entry {1} in `KillCredit{2}`.", cInfo.Entry, cInfo.KillCredit[k], k + 1);
                        cInfo.KillCredit[k] = 0;
                    }
                }
            }

            if (cInfo.Models.Empty())
                Log.outError(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) does not have any existing display id in creature_template_model.");

            if (cInfo.UnitClass == 0 || ((1 << ((int)cInfo.UnitClass - 1)) & (int)Class.ClassMaskAllCreatures) == 0)
            {
                Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has invalid unit_class ({1}) in creature_template. Set to 1 (UNIT_CLASS_WARRIOR).", cInfo.Entry, cInfo.UnitClass);
                cInfo.UnitClass = (uint)Class.Warrior;
            }

            if (cInfo.DmgSchool >= (uint)SpellSchools.Max)
            {
                Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has invalid spell school value ({1}) in `dmgschool`.", cInfo.Entry, cInfo.DmgSchool);
                cInfo.DmgSchool = (uint)SpellSchools.Normal;
            }

            if (cInfo.BaseAttackTime == 0)
                cInfo.BaseAttackTime = SharedConst.BaseAttackTime;

            if (cInfo.RangeAttackTime == 0)
                cInfo.RangeAttackTime = SharedConst.BaseAttackTime;

            if (cInfo.SpeedWalk == 0.0f)
            {
                Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has wrong value ({1}) in speed_walk, set to 1.", cInfo.Entry, cInfo.SpeedWalk);
                cInfo.SpeedWalk = 1.0f;
            }

            if (cInfo.SpeedRun == 0.0f)
            {
                Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has wrong value ({1}) in speed_run, set to 1.14286.", cInfo.Entry, cInfo.SpeedRun);
                cInfo.SpeedRun = 1.14286f;
            }

            if (cInfo.CreatureType != 0 && !CliDB.CreatureTypeStorage.ContainsKey((uint)cInfo.CreatureType))
            {
                Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has invalid creature type ({1}) in `type`.", cInfo.Entry, cInfo.CreatureType);
                cInfo.CreatureType = CreatureType.Humanoid;
            }

            if (cInfo.Family != 0 && !CliDB.CreatureFamilyStorage.ContainsKey(cInfo.Family))
            {
                Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has invalid creature family ({1}) in `family`.", cInfo.Entry, cInfo.Family);
                cInfo.Family = CreatureFamily.None;
            }

            if (cInfo.InhabitType <= 0 || cInfo.InhabitType > InhabitType.Anywhere)
            {
                Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has wrong value ({1}) in `InhabitType`, creature will not correctly walk/swim/fly.", cInfo.Entry, cInfo.InhabitType);
                cInfo.InhabitType = InhabitType.Anywhere;
            }

            if (cInfo.HoverHeight < 0.0f)
            {
                Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has wrong value ({1}) in `HoverHeight`", cInfo.Entry, cInfo.HoverHeight);
                cInfo.HoverHeight = 1.0f;
            }

            if (cInfo.VehicleId != 0)
            {
                if (!CliDB.VehicleStorage.ContainsKey(cInfo.VehicleId))
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has a non-existing VehicleId ({1}). This *WILL* cause the client to freeze!", cInfo.Entry, cInfo.VehicleId);
                    cInfo.VehicleId = 0;
                }
            }

            for (byte j = 0; j < SharedConst.MaxCreatureSpells; ++j)
            {
                if (cInfo.Spells[j] != 0 && !Global.SpellMgr.HasSpellInfo(cInfo.Spells[j], Difficulty.None))
                {
                    Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has non-existing Spell{1} ({2}), set to 0.", cInfo.Entry, j + 1, cInfo.Spells[j]);
                    cInfo.Spells[j] = 0;
                }
            }

            if (cInfo.MovementType >= (uint)MovementGeneratorType.MaxDB)
            {
                Log.outError(LogFilter.Sql, "Creature (Entry: {0}) has wrong movement generator type ({1}), ignored and set to IDLE.", cInfo.Entry, cInfo.MovementType);
                cInfo.MovementType = (uint)MovementGeneratorType.Idle;
            }

            if (cInfo.HealthScalingExpansion < (int)Expansion.LevelCurrent || cInfo.HealthScalingExpansion >= (int)Expansion.Max)
            {
                Log.outError(LogFilter.Sql, "Table `creature_template` lists creature (Id: {0}) with invalid `HealthScalingExpansion` {1}. Ignored and set to 0.", cInfo.Entry, cInfo.HealthScalingExpansion);
                cInfo.HealthScalingExpansion = 0;
            }

            if (cInfo.RequiredExpansion > (int)Expansion.Max)
            {
                Log.outError(LogFilter.Sql, "Table `creature_template` lists creature (Entry: {0}) with `RequiredExpansion` {1}. Ignored and set to 0.", cInfo.Entry, cInfo.RequiredExpansion);
                cInfo.RequiredExpansion = 0;
            }

            uint badFlags = (uint)(cInfo.FlagsExtra & ~CreatureFlagsExtra.DBAllowed);
            if (badFlags != 0)
            {
                Log.outError(LogFilter.Sql, "Table `creature_template` lists creature (Entry: {0}) with disallowed `flags_extra` {1}, removing incorrect flag.", cInfo.Entry, badFlags);
                cInfo.FlagsExtra &= CreatureFlagsExtra.DBAllowed;
            }

            var levels = cInfo.GetMinMaxLevel();
            if (levels[0] < 1 || levels[0] > SharedConst.StrongMaxLevel)
            {
                Log.outError(LogFilter.Sql, $"Creature (ID: {cInfo.Entry}): Calculated minLevel {cInfo.Minlevel} is not within [1, 255], value has been set to {(cInfo.HealthScalingExpansion == (int)Expansion.LevelCurrent ? SharedConst.MaxLevel : 1)}.");
                cInfo.Minlevel = (short)(cInfo.HealthScalingExpansion == (int)Expansion.LevelCurrent ? 0 : 1);
            }

            if (levels[1] < 1 || levels[1] > SharedConst.StrongMaxLevel)
            {
                Log.outError(LogFilter.Sql, $"Creature (ID: {cInfo.Entry}): Calculated maxLevel {cInfo.Maxlevel} is not within [1, 255], value has been set to {(cInfo.HealthScalingExpansion == (int)Expansion.LevelCurrent ? SharedConst.MaxLevel : 1)}.");
                cInfo.Maxlevel = (short)(cInfo.HealthScalingExpansion == (int)Expansion.LevelCurrent ? 0 : 1);
            }

            cInfo.ModDamage *= Creature._GetDamageMod(cInfo.Rank);
        }
        public void LoadLinkedRespawn()
        {
            uint oldMSTime = Time.GetMSTime();

            linkedRespawnStorage.Clear();
            //                                                 0        1          2
            SQLResult result = DB.World.Query("SELECT guid, linkedGuid, linkType FROM linked_respawn ORDER BY guid ASC");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 linked respawns. DB table `linked_respawn` is empty.");
                return;
            }

            do
            {
                ulong guidLow = result.Read<ulong>(0);
                ulong linkedGuidLow = result.Read<ulong>(1);
                byte linkType = result.Read<byte>(2);

                ObjectGuid guid = ObjectGuid.Empty;
                ObjectGuid linkedGuid = ObjectGuid.Empty;
                bool error = false;
                switch ((CreatureLinkedRespawnType)linkType)
                {
                    case CreatureLinkedRespawnType.CreatureToCreature:
                        {
                            CreatureData slave = GetCreatureData(guidLow);
                            if (slave == null)
                            {
                                Log.outError(LogFilter.Sql, "Couldn't get creature data for GUIDLow {0}", guidLow);
                                error = true;
                                break;
                            }

                            CreatureData master = GetCreatureData(linkedGuidLow);
                            if (master == null)
                            {
                                Log.outError(LogFilter.Sql, "Couldn't get creature data for GUIDLow {0}", linkedGuidLow);
                                error = true;
                                break;
                            }

                            MapRecord map = CliDB.MapStorage.LookupByKey(master.spawnPoint.GetMapId());
                            if (map == null || !map.Instanceable() || (master.spawnPoint.GetMapId() != slave.spawnPoint.GetMapId()))
                            {
                                Log.outError(LogFilter.Sql, "Creature '{0}' linking to '{1}' on an unpermitted map.", guidLow, linkedGuidLow);
                                error = true;
                                break;
                            }

                            // they must have a possibility to meet (normal/heroic difficulty)
                            if (!master.spawnDifficulties.Intersect(slave.spawnDifficulties).Any())
                            {
                                Log.outError(LogFilter.Sql, "LinkedRespawn: Creature '{0}' linking to '{1}' with not corresponding spawnMask", guidLow, linkedGuidLow);
                                error = true;
                                break;
                            }

                            guid = ObjectGuid.Create(HighGuid.Creature, slave.spawnPoint.GetMapId(), slave.Id, guidLow);
                            linkedGuid = ObjectGuid.Create(HighGuid.Creature, master.spawnPoint.GetMapId(), master.Id, linkedGuidLow);
                            break;
                        }
                    case CreatureLinkedRespawnType.CreatureToGO:
                        {
                            CreatureData slave = GetCreatureData(guidLow);
                            if (slave == null)
                            {
                                Log.outError(LogFilter.Sql, "Couldn't get creature data for GUIDLow {0}", guidLow);
                                error = true;
                                break;
                            }

                            GameObjectData master = GetGameObjectData(linkedGuidLow);
                            if (master == null)
                            {
                                Log.outError(LogFilter.Sql, "Couldn't get gameobject data for GUIDLow {0}", linkedGuidLow);
                                error = true;
                                break;
                            }

                            MapRecord map = CliDB.MapStorage.LookupByKey(master.spawnPoint.GetMapId());
                            if (map == null || !map.Instanceable() || (master.spawnPoint.GetMapId() != slave.spawnPoint.GetMapId()))
                            {
                                Log.outError(LogFilter.Sql, "Creature '{0}' linking to '{1}' on an unpermitted map.", guidLow, linkedGuidLow);
                                error = true;
                                break;
                            }

                            // they must have a possibility to meet (normal/heroic difficulty)
                            if (!master.spawnDifficulties.Intersect(slave.spawnDifficulties).Any())
                            {
                                Log.outError(LogFilter.Sql, "LinkedRespawn: Creature '{0}' linking to '{1}' with not corresponding spawnMask", guidLow, linkedGuidLow);
                                error = true;
                                break;
                            }

                            guid = ObjectGuid.Create(HighGuid.Creature, slave.spawnPoint.GetMapId(), slave.Id, guidLow);
                            linkedGuid = ObjectGuid.Create(HighGuid.GameObject, master.spawnPoint.GetMapId(), master.Id, linkedGuidLow);
                            break;
                        }
                    case CreatureLinkedRespawnType.GOToGO:
                        {
                            GameObjectData slave = GetGameObjectData(guidLow);
                            if (slave == null)
                            {
                                Log.outError(LogFilter.Sql, "Couldn't get gameobject data for GUIDLow {0}", guidLow);
                                error = true;
                                break;
                            }

                            GameObjectData master = GetGameObjectData(linkedGuidLow);
                            if (master == null)
                            {
                                Log.outError(LogFilter.Sql, "Couldn't get gameobject data for GUIDLow {0}", linkedGuidLow);
                                error = true;
                                break;
                            }

                            MapRecord map = CliDB.MapStorage.LookupByKey(master.spawnPoint.GetMapId());
                            if (map == null || !map.Instanceable() || (master.spawnPoint.GetMapId() != slave.spawnPoint.GetMapId()))
                            {
                                Log.outError(LogFilter.Sql, "Creature '{0}' linking to '{1}' on an unpermitted map.", guidLow, linkedGuidLow);
                                error = true;
                                break;
                            }

                            // they must have a possibility to meet (normal/heroic difficulty)
                            if (!master.spawnDifficulties.Intersect(slave.spawnDifficulties).Any())
                            {
                                Log.outError(LogFilter.Sql, "LinkedRespawn: Creature '{0}' linking to '{1}' with not corresponding spawnMask", guidLow, linkedGuidLow);
                                error = true;
                                break;
                            }

                            guid = ObjectGuid.Create(HighGuid.GameObject, slave.spawnPoint.GetMapId(), slave.Id, guidLow);
                            linkedGuid = ObjectGuid.Create(HighGuid.GameObject, master.spawnPoint.GetMapId(), master.Id, linkedGuidLow);
                            break;
                        }
                    case CreatureLinkedRespawnType.GOToCreature:
                        {
                            GameObjectData slave = GetGameObjectData(guidLow);
                            if (slave == null)
                            {
                                Log.outError(LogFilter.Sql, "Couldn't get gameobject data for GUIDLow {0}", guidLow);
                                error = true;
                                break;
                            }

                            CreatureData master = GetCreatureData(linkedGuidLow);
                            if (master == null)
                            {
                                Log.outError(LogFilter.Sql, "Couldn't get creature data for GUIDLow {0}", linkedGuidLow);
                                error = true;
                                break;
                            }

                            MapRecord map = CliDB.MapStorage.LookupByKey(master.spawnPoint.GetMapId());
                            if (map == null || !map.Instanceable() || (master.spawnPoint.GetMapId() != slave.spawnPoint.GetMapId()))
                            {
                                Log.outError(LogFilter.Sql, "Creature '{0}' linking to '{1}' on an unpermitted map.", guidLow, linkedGuidLow);
                                error = true;
                                break;
                            }

                            // they must have a possibility to meet (normal/heroic difficulty)
                            if (!master.spawnDifficulties.Intersect(slave.spawnDifficulties).Any())
                            {
                                Log.outError(LogFilter.Sql, "LinkedRespawn: Creature '{0}' linking to '{1}' with not corresponding spawnMask", guidLow, linkedGuidLow);
                                error = true;
                                break;
                            }

                            guid = ObjectGuid.Create(HighGuid.GameObject, slave.spawnPoint.GetMapId(), slave.Id, guidLow);
                            linkedGuid = ObjectGuid.Create(HighGuid.Creature, master.spawnPoint.GetMapId(), master.Id, linkedGuidLow);
                            break;
                        }

                }

                if (!error)
                    linkedRespawnStorage[guid] = linkedGuid;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} linked respawns in {1} ms", linkedRespawnStorage.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadNPCText()
        {
            uint oldMSTime = Time.GetMSTime();

            _npcTextStorage.Clear();

            SQLResult result = DB.World.Query("SELECT ID, Probability0, Probability1, Probability2, Probability3, Probability4, Probability5, Probability6, Probability7, " +
                "BroadcastTextID0, BroadcastTextID1, BroadcastTextID2, BroadcastTextID3, BroadcastTextID4, BroadcastTextID5, BroadcastTextID6, BroadcastTextID7 FROM npc_text");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 npc texts, table is empty!");
                return;
            }

            do
            {
                uint textID = result.Read<uint>(0);
                if (textID == 0)
                {
                    Log.outError(LogFilter.Sql, "Table `npc_text` has record wit reserved id 0, ignore.");
                    continue;
                }

                NpcText npcText = new();
                for (int i = 0; i < SharedConst.MaxNpcTextOptions; i++)
                {
                    npcText.Data[i].Probability = result.Read<float>(1 + i);
                    npcText.Data[i].BroadcastTextID = result.Read<uint>(9 + i);
                }

                for (int i = 0; i < SharedConst.MaxNpcTextOptions; i++)
                {
                    if (npcText.Data[i].BroadcastTextID != 0)
                    {
                        if (!CliDB.BroadcastTextStorage.ContainsKey(npcText.Data[i].BroadcastTextID))
                        {
                            Log.outError(LogFilter.Sql, "NPCText (Id: {0}) has a non-existing or incompatible BroadcastText (ID: {1}, Index: {2})", textID, npcText.Data[i].BroadcastTextID, i);
                            npcText.Data[i].BroadcastTextID = 0;
                        }
                    }
                }

                for (byte i = 0; i < SharedConst.MaxNpcTextOptions; i++)
                {
                    if (npcText.Data[i].Probability > 0 && npcText.Data[i].BroadcastTextID == 0)
                    {
                        Log.outError(LogFilter.Sql, "NPCText (ID: {0}) has a probability (Index: {1}) set, but no BroadcastTextID to go with it", textID, i);
                        npcText.Data[i].Probability = 0;
                    }
                }
                _npcTextStorage[textID] = npcText;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} npc texts in {1} ms", _npcTextStorage.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        public void LoadTrainers()
        {
            uint oldMSTime = Time.GetMSTime();

            // For reload case
            _trainers.Clear();

            MultiMap<uint, TrainerSpell> spellsByTrainer = new();
            SQLResult trainerSpellsResult = DB.World.Query("SELECT TrainerId, SpellId, MoneyCost, ReqSkillLine, ReqSkillRank, ReqAbility1, ReqAbility2, ReqAbility3, ReqLevel FROM trainer_spell");
            if (!trainerSpellsResult.IsEmpty())
            {
                do
                {
                    TrainerSpell spell = new();
                    uint trainerId = trainerSpellsResult.Read<uint>(0);
                    spell.SpellId = trainerSpellsResult.Read<uint>(1);
                    spell.MoneyCost = trainerSpellsResult.Read<uint>(2);
                    spell.ReqSkillLine = trainerSpellsResult.Read<uint>(3);
                    spell.ReqSkillRank = trainerSpellsResult.Read<uint>(4);
                    spell.ReqAbility[0] = trainerSpellsResult.Read<uint>(5);
                    spell.ReqAbility[1] = trainerSpellsResult.Read<uint>(6);
                    spell.ReqAbility[2] = trainerSpellsResult.Read<uint>(7);
                    spell.ReqLevel = trainerSpellsResult.Read<byte>(8);

                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spell.SpellId, Difficulty.None);
                    if (spellInfo == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `trainer_spell` references non-existing spell (SpellId: {spell.SpellId}) for TrainerId {trainerId}, ignoring");
                        continue;
                    }

                    if (spell.ReqSkillLine != 0 && !CliDB.SkillLineStorage.ContainsKey(spell.ReqSkillLine))
                    {
                        Log.outError(LogFilter.Sql, $"Table `trainer_spell` references non-existing skill (ReqSkillLine: {spell.ReqSkillLine}) for TrainerId {trainerId} and SpellId {spell.SpellId}, ignoring");
                        continue;
                    }

                    bool allReqValid = true;
                    for (var i = 0; i < spell.ReqAbility.Count; ++i)
                    {
                        uint requiredSpell = spell.ReqAbility[i];
                        if (requiredSpell != 0 && !Global.SpellMgr.HasSpellInfo(requiredSpell, Difficulty.None))
                        {
                            Log.outError(LogFilter.Sql, $"Table `trainer_spell` references non-existing spell (ReqAbility {i + 1}: {requiredSpell}) for TrainerId {trainerId} and SpellId {spell.SpellId}, ignoring");
                            allReqValid = false;
                        }
                    }

                    if (!allReqValid)
                        continue;

                    spellsByTrainer.Add(trainerId, spell);

                } while (trainerSpellsResult.NextRow());
            }

            SQLResult trainersResult = DB.World.Query("SELECT Id, Type, Greeting FROM trainer");
            if (!trainersResult.IsEmpty())
            {
                do
                {
                    uint trainerId = trainersResult.Read<uint>(0);
                    TrainerType trainerType = (TrainerType)trainersResult.Read<byte>(1);
                    string greeting = trainersResult.Read<string>(2);
                    List<TrainerSpell> spells = new();
                    var spellList = spellsByTrainer.LookupByKey(trainerId);
                    if (spellList != null)
                    {
                        spells = spellList;
                        spellsByTrainer.Remove(trainerId);
                    }

                    _trainers.Add(trainerId, new Trainer(trainerId, trainerType, greeting, spells));

                } while (trainersResult.NextRow());
            }

            foreach (var unusedSpells in spellsByTrainer)
            {
                Log.outError(LogFilter.Sql, $"Table `trainer_spell` references non-existing trainer (TrainerId: {unusedSpells.Key}) for SpellId {unusedSpells.Value.SpellId}, ignoring");

            }

            SQLResult trainerLocalesResult = DB.World.Query("SELECT Id, locale, Greeting_lang FROM trainer_locale");
            if (!trainerLocalesResult.IsEmpty())
            {
                do
                {
                    uint trainerId = trainerLocalesResult.Read<uint>(0);
                    string localeName = trainerLocalesResult.Read<string>(1);

                    Locale locale = localeName.ToEnum<Locale>();
                    if (!SharedConst.IsValidLocale(locale) || locale == Locale.enUS)
                        continue;

                    Trainer trainer = _trainers.LookupByKey(trainerId);
                    if (trainer != null)
                        trainer.AddGreetingLocale(locale, trainerLocalesResult.Read<String>(2));
                    else
                        Log.outError(LogFilter.Sql, $"Table `trainer_locale` references non-existing trainer (TrainerId: {trainerId}) for locale {localeName}, ignoring");

                } while (trainerLocalesResult.NextRow());
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_trainers.Count} Trainers in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }
        public void LoadCreatureTrainers()
        {
            uint oldMSTime = Time.GetMSTime();

            _creatureDefaultTrainers.Clear();

            SQLResult result = DB.World.Query("SELECT CreatureId, TrainerId, MenuId, OptionIndex FROM creature_trainer");
            if (!result.IsEmpty())
            {
                do
                {
                    uint creatureId = result.Read<uint>(0);
                    uint trainerId = result.Read<uint>(1);
                    uint gossipMenuId = result.Read<uint>(2);
                    uint gossipOptionIndex = result.Read<uint>(3);

                    if (GetCreatureTemplate(creatureId) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `creature_trainer` references non-existing creature template (CreatureId: {creatureId}), ignoring");
                        continue;
                    }

                    if (GetTrainer(trainerId) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `creature_trainer` references non-existing trainer (TrainerId: {trainerId}) for CreatureId {creatureId} MenuId {gossipMenuId} OptionIndex {gossipOptionIndex}, ignoring");
                        continue;
                    }

                    if (gossipMenuId != 0 || gossipOptionIndex != 0)
                    {
                        var gossipMenuItems = GetGossipMenuItemsMapBounds(gossipMenuId);
                        var gossipOptionItr = gossipMenuItems.Find(entry =>
                        {
                            return entry.OptionIndex == gossipOptionIndex;
                        });

                        if (gossipOptionItr == null)
                        {
                            Log.outError(LogFilter.Sql, $"Table `creature_trainer` references non-existing gossip menu option (MenuId {gossipMenuId} OptionIndex {gossipOptionIndex}) for CreatureId {creatureId} and TrainerId {trainerId}, ignoring");
                            continue;
                        }
                    }

                    _creatureDefaultTrainers[(creatureId, gossipMenuId, gossipOptionIndex)] = trainerId;
                } while (result.NextRow());
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_creatureDefaultTrainers.Count} default trainers in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }
        public void LoadVendors()
        {
            var time = Time.GetMSTime();
            // For reload case
            cacheVendorItemStorage.Clear();

            List<uint> skipvendors = new();

            SQLResult result = DB.World.Query("SELECT entry, item, maxcount, incrtime, ExtendedCost, type, BonusListIDs, PlayerConditionID, IgnoreFiltering FROM npc_vendor ORDER BY entry, slot ASC");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 Vendors. DB table `npc_vendor` is empty!");
                return;
            }

            uint count = 0;

            do
            {
                uint entry = result.Read<uint>(0);
                int itemid = result.Read<int>(1);

                // if item is a negative, its a reference
                if (itemid < 0)
                    count += LoadReferenceVendor((int)entry, -itemid, skipvendors);
                else
                {
                    VendorItem vItem = new();
                    vItem.item = (uint)itemid;
                    vItem.maxcount = result.Read<uint>(2);
                    vItem.incrtime = result.Read<uint>(3);
                    vItem.ExtendedCost = result.Read<uint>(4);
                    vItem.Type = (ItemVendorType)result.Read<byte>(5);
                    vItem.PlayerConditionId = result.Read<uint>(7);
                    vItem.IgnoreFiltering = result.Read<bool>(8);

                    var bonusListIDsTok = new StringArray(result.Read<string>(6), ' ');
                    if (!bonusListIDsTok.IsEmpty())
                    {
                        foreach (string token in bonusListIDsTok)
                        {
                            if (uint.TryParse(token, out uint id))
                                vItem.BonusListIDs.Add(id);
                        }
                    }

                    if (!IsVendorItemValid(entry, vItem, null, skipvendors))
                        continue;

                    if (cacheVendorItemStorage.LookupByKey(entry) == null)
                        cacheVendorItemStorage.Add(entry, new VendorItemData());

                    cacheVendorItemStorage[entry].AddItem(vItem);
                    ++count;
                }
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} Vendors in {1} ms", count, Time.GetMSTimeDiffToNow(time));
        }
        uint LoadReferenceVendor(int vendor, int item, List<uint> skip_vendors)
        {
            // find all items from the reference vendor
            PreparedStatement stmt = DB.World.GetPreparedStatement(WorldStatements.SEL_NPC_VENDOR_REF);
            stmt.AddValue(0, item);
            SQLResult result = DB.World.Query(stmt);

            if (result.IsEmpty())
                return 0;

            uint count = 0;
            do
            {
                int item_id = result.Read<int>(0);

                // if item is a negative, its a reference
                if (item_id < 0)
                    count += LoadReferenceVendor(vendor, -item_id, skip_vendors);
                else
                {
                    VendorItem vItem = new();
                    vItem.item = (uint)item_id;
                    vItem.maxcount = result.Read<uint>(1);
                    vItem.incrtime = result.Read<uint>(2);
                    vItem.ExtendedCost = result.Read<uint>(3);
                    vItem.Type = (ItemVendorType)result.Read<byte>(4);
                    vItem.PlayerConditionId = result.Read<uint>(6);
                    vItem.IgnoreFiltering = result.Read<bool>(7);

                    var bonusListIDsTok = new StringArray(result.Read<string>(5), ' ');
                    if (!bonusListIDsTok.IsEmpty())
                    {
                        foreach (string token in bonusListIDsTok)
                        {
                            if (uint.TryParse(token, out uint id))
                                vItem.BonusListIDs.Add(id);
                        }
                    }

                    if (!IsVendorItemValid((uint)vendor, vItem, null, skip_vendors))
                        continue;

                    VendorItemData vList = cacheVendorItemStorage.LookupByKey((uint)vendor);
                    if (vList == null)
                        continue;

                    vList.AddItem(vItem);
                    ++count;
                }
            } while (result.NextRow());

            return count;
        }
        public void LoadCreatures()
        {
            var time = Time.GetMSTime();

            //                                         0              1   2    3           4           5           6            7        8             9              10
            SQLResult result = DB.World.Query("SELECT creature.guid, id, map, position_x, position_y, position_z, orientation, modelid, equipment_id, spawntimesecs, spawndist, " +
                //11               12         13       14            15                 16          17          18                19                   20                    21
                "currentwaypoint, curhealth, curmana, MovementType, spawnDifficulties, eventEntry, pool_entry, creature.npcflag, creature.unit_flags, creature.unit_flags2, creature.unit_flags3, " +
                //22                     23                      24                25                   26                       27
                "creature.dynamicflags, creature.phaseUseFlags, creature.phaseid, creature.phasegroup, creature.terrainSwapMap, creature.ScriptName " +
                "FROM creature LEFT OUTER JOIN game_event_creature ON creature.guid = game_event_creature.guid LEFT OUTER JOIN pool_creature ON creature.guid = pool_creature.guid");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creatures. DB table `creature` is empty.");
                return;
            }

            // Build single time for check spawnmask
            Dictionary<uint, List<Difficulty>> spawnMasks = new();
            foreach (var mapDifficultyPair in Global.DB2Mgr.GetMapDifficulties())
            {
                foreach (var difficultyPair in mapDifficultyPair.Value)
                {
                    if (!spawnMasks.ContainsKey(mapDifficultyPair.Key))
                        spawnMasks[mapDifficultyPair.Key] = new List<Difficulty>();

                    spawnMasks[mapDifficultyPair.Key].Add((Difficulty)difficultyPair.Key);
                }
            }

            PhaseShift phaseShift = new();

            uint count = 0;
            do
            {
                ulong guid = result.Read<ulong>(0);
                uint entry = result.Read<uint>(1);

                CreatureTemplate cInfo = GetCreatureTemplate(entry);
                if (cInfo == null)
                {
                    Log.outError(LogFilter.Sql, "Table `creature` has creature (GUID: {0}) with non existing creature entry {1}, skipped.", guid, entry);
                    continue;
                }

                CreatureData data = new();
                data.spawnId = guid;
                data.Id = entry;
                data.spawnPoint = new WorldLocation(result.Read<ushort>(2), result.Read<float>(3), result.Read<float>(4), result.Read<float>(5), result.Read<float>(6));                    
                data.displayid = result.Read<uint>(7);
                data.equipmentId = result.Read<sbyte>(8);
                data.spawntimesecs = result.Read<int>(9);
                data.spawndist = result.Read<float>(10);
                data.currentwaypoint = result.Read<uint>(11);
                data.curhealth = result.Read<uint>(12);
                data.curmana = result.Read<uint>(13);
                data.movementType = result.Read<byte>(14);
                data.spawnDifficulties = ParseSpawnDifficulties(result.Read<string>(15), "creature", guid, data.spawnPoint.GetMapId(), spawnMasks.LookupByKey(data.spawnPoint.GetMapId()));
                short gameEvent = result.Read<short>(16);
                uint PoolId = result.Read<uint>(17);
                data.npcflag = result.Read<ulong>(18);
                data.unit_flags = result.Read<uint>(19);
                data.unit_flags2 = result.Read<uint>(20);
                data.unit_flags3 = result.Read<uint>(21);
                data.dynamicflags = result.Read<uint>(22);
                data.phaseUseFlags = (PhaseUseFlagsValues)result.Read<byte>(23);
                data.phaseId = result.Read<uint>(24);
                data.phaseGroup = result.Read<uint>(25);
                data.terrainSwapMap = result.Read<int>(26);
                data.ScriptId = GetScriptId(result.Read<string>(27));
                data.spawnGroupData = _spawnGroupDataStorage[0];

                var mapEntry = CliDB.MapStorage.LookupByKey(data.spawnPoint.GetMapId());
                if (mapEntry == null)
                {
                    Log.outError(LogFilter.Sql, "Table `creature` have creature (GUID: {0}) that spawned at not existed map (Id: {1}), skipped.", guid, data.spawnPoint.GetMapId());
                    continue;
                }

                if (data.spawnDifficulties.Empty())
                {
                    Log.outError(LogFilter.Sql, $"Table `creature` has creature (GUID: {guid}) that is not spawned in any difficulty, skipped.");
                    continue;
                }

                bool ok = true;
                for (uint diff = 0; diff < SharedConst.MaxCreatureDifficulties && ok; ++diff)
                {
                    if (_difficultyEntries[diff].Contains(data.Id))
                    {
                        Log.outError(LogFilter.Sql, "Table `creature` have creature (GUID: {0}) that listed as difficulty {1} template (entry: {2}) in `creaturetemplate`, skipped.", guid, diff + 1, data.Id);
                        ok = false;
                    }
                }
                if (!ok)
                    continue;

                // -1 random, 0 no equipment,
                if (data.equipmentId != 0)
                {
                    if (GetEquipmentInfo(data.Id, data.equipmentId) == null)
                    {
                        Log.outError(LogFilter.Sql, "Table `creature` have creature (Entry: {0}) with equipmentid {1} not found in table `creatureequiptemplate`, set to no equipment.", data.Id, data.equipmentId);
                        data.equipmentId = 0;
                    }
                }

                if (cInfo.FlagsExtra.HasAnyFlag(CreatureFlagsExtra.InstanceBind))
                {
                    if (!mapEntry.IsDungeon())
                        Log.outError(LogFilter.Sql, "Table `creature` have creature (GUID: {0} Entry: {1}) with `creature_template`.`flagsextra` including CREATUREFLAGEXTRAINSTANCEBIND " +
                            "but creature are not in instance.", guid, data.Id);
                }

                if (data.spawndist < 0.0f)
                {
                    Log.outError(LogFilter.Sql, "Table `creature` have creature (GUID: {0} Entry: {1}) with `spawndist`< 0, set to 0.", guid, data.Id);
                    data.spawndist = 0.0f;
                }
                else if (data.movementType == (byte)MovementGeneratorType.Random)
                {
                    if (MathFunctions.fuzzyEq(data.spawndist, 0.0f))
                    {
                        Log.outError(LogFilter.Sql, "Table `creature` have creature (GUID: {0} Entry: {1}) with `MovementType`=1 (random movement) but with `spawndist`=0, replace by idle movement type (0).", guid, data.Id);
                        data.movementType = (byte)MovementGeneratorType.Idle;
                    }
                }
                else if (data.movementType == (byte)MovementGeneratorType.Idle)
                {
                    if (data.spawndist != 0.0f)
                    {
                        Log.outError(LogFilter.Sql, "Table `creature` have creature (GUID: {0} Entry: {1}) with `MovementType`=0 (idle) have `spawndist`<>0, set to 0.", guid, data.Id);
                        data.spawndist = 0.0f;
                    }
                }

                if (Convert.ToBoolean(data.phaseUseFlags & ~PhaseUseFlagsValues.All))
                {
                    Log.outError(LogFilter.Sql, "Table `creature` have creature (GUID: {0} Entry: {1}) has unknown `phaseUseFlags` set, removed unknown value.", guid, data.Id);
                    data.phaseUseFlags &= PhaseUseFlagsValues.All;
                }

                if (data.phaseUseFlags.HasAnyFlag(PhaseUseFlagsValues.AlwaysVisible) && data.phaseUseFlags.HasAnyFlag(PhaseUseFlagsValues.Inverse))
                {
                    Log.outError(LogFilter.Sql, "Table `creature` have creature (GUID: {0} Entry: {1}) has both `phaseUseFlags` PHASE_USE_FLAGS_ALWAYS_VISIBLE and PHASE_USE_FLAGS_INVERSE," +
                        " removing PHASE_USE_FLAGS_INVERSE.", guid, data.Id);
                    data.phaseUseFlags &= ~PhaseUseFlagsValues.Inverse;
                }

                if (data.phaseGroup != 0 && data.phaseId != 0)
                {
                    Log.outError(LogFilter.Sql, "Table `creature` have creature (GUID: {0} Entry: {1}) with both `phaseid` and `phasegroup` set, `phasegroup` set to 0", guid, data.Id);
                    data.phaseGroup = 0;
                }

                if (data.phaseId != 0)
                {
                    if (!CliDB.PhaseStorage.ContainsKey(data.phaseId))
                    {
                        Log.outError(LogFilter.Sql, "Table `creature` have creature (GUID: {0} Entry: {1}) with `phaseid` {2} does not exist, set to 0", guid, data.Id, data.phaseId);
                        data.phaseId = 0;
                    }
                }

                if (data.phaseGroup != 0)
                {
                    if (Global.DB2Mgr.GetPhasesForGroup(data.phaseGroup).Empty())
                    {
                        Log.outError(LogFilter.Sql, "Table `creature` have creature (GUID: {0} Entry: {1}) with `phasegroup` {2} does not exist, set to 0", guid, data.Id, data.phaseGroup);
                        data.phaseGroup = 0;
                    }
                }

                if (data.terrainSwapMap != -1)
                {
                    MapRecord terrainSwapEntry = CliDB.MapStorage.LookupByKey(data.terrainSwapMap);
                    if (terrainSwapEntry == null)
                    {
                        Log.outError(LogFilter.Sql, "Table `creature` have creature (GUID: {0} Entry: {1}) with `terrainSwapMap` {2} does not exist, set to -1", guid, data.Id, data.terrainSwapMap);
                        data.terrainSwapMap = -1;
                    }
                    else if (terrainSwapEntry.ParentMapID != data.spawnPoint.GetMapId())
                    {
                        Log.outError(LogFilter.Sql, "Table `creature` have creature (GUID: {0} Entry: {1}) with `terrainSwapMap` {2} which cannot be used on spawn map, set to -1", guid, data.Id, data.terrainSwapMap);
                        data.terrainSwapMap = -1;
                    }
                }

                if (WorldConfig.GetBoolValue(WorldCfg.CalculateCreatureZoneAreaData))
                {
                    PhasingHandler.InitDbVisibleMapId(phaseShift, data.terrainSwapMap);
                    Global.MapMgr.GetZoneAndAreaId(phaseShift, out uint zoneId, out uint areaId, data.spawnPoint);

                    PreparedStatement stmt = DB.World.GetPreparedStatement(WorldStatements.UPD_CREATURE_ZONE_AREA_DATA);
                    stmt.AddValue(0, zoneId);
                    stmt.AddValue(1, areaId);
                    stmt.AddValue(2, guid);

                    DB.World.Execute(stmt);
                }

                // Add to grid if not managed by the game event or pool system
                if (gameEvent == 0 && PoolId == 0)
                    AddCreatureToGrid(guid, data);

                creatureDataStorage[guid] = data;
                count++;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} creatures in {1} ms", count, Time.GetMSTimeDiffToNow(time));
        }

        public void AddCreatureToGrid(ulong guid, CreatureData data)
        {
            foreach (Difficulty difficulty in data.spawnDifficulties)
            {
                CellCoord cellCoord = GridDefines.ComputeCellCoord(data.spawnPoint.GetPositionX(), data.spawnPoint.GetPositionY());
                var cellguids = CreateCellObjectGuids(data.spawnPoint.GetMapId(), difficulty, cellCoord.GetId());
                cellguids.creatures.Add(guid);
            }
        }
        public void RemoveCreatureFromGrid(ulong guid, CreatureData data)
        {
            foreach (Difficulty difficulty in data.spawnDifficulties)
            {
                CellCoord cellCoord = GridDefines.ComputeCellCoord(data.spawnPoint.GetPositionX(), data.spawnPoint.GetPositionY());
                CellObjectGuids cellguids = GetCellObjectGuids(data.spawnPoint.GetMapId(), difficulty, cellCoord.GetId());
                if (cellguids == null)
                    return;

                cellguids.creatures.Remove(guid);
            }
        }
        public ulong AddCreatureData(uint entry, uint mapId, Position pos, uint spawntimedelay)
        {
            CreatureTemplate cInfo = GetCreatureTemplate(entry);
            if (cInfo == null)
                return 0;

            uint level = cInfo.Minlevel == cInfo.Maxlevel ? (uint)cInfo.Minlevel : RandomHelper.URand(cInfo.Minlevel, cInfo.Maxlevel); // Only used for extracting creature base stats
            CreatureBaseStats stats = GetCreatureBaseStats(level, cInfo.UnitClass);
            Map map = Global.MapMgr.CreateBaseMap(mapId);
            if (!map)
                return 0;

            CreatureLevelScaling scaling = cInfo.GetLevelScaling(map.GetDifficultyID());

            ulong spawnId = GenerateCreatureSpawnId();
            CreatureData data = NewOrExistCreatureData(spawnId);
            data.spawnId = spawnId;
            data.Id = entry;
            data.spawnPoint.WorldRelocate(mapId, pos);
            data.displayid = 0;
            data.equipmentId = 0;

            data.spawntimesecs = (int)spawntimedelay;
            data.spawndist = 0;
            data.currentwaypoint = 0;
            data.curhealth = (uint)(Global.DB2Mgr.EvaluateExpectedStat(ExpectedStatType.CreatureHealth, level, cInfo.HealthScalingExpansion, scaling.ContentTuningID, (Class)cInfo.UnitClass) * cInfo.ModHealth * cInfo.ModHealthExtra);
            data.curmana = stats.GenerateMana(cInfo);
            data.movementType = (byte)cInfo.MovementType;
            data.spawnDifficulties.Add(Difficulty.None);
            data.dbData = false;
            data.npcflag = (uint)cInfo.Npcflag;
            data.unit_flags = (uint)cInfo.UnitFlags;
            data.dynamicflags = cInfo.DynamicFlags;
            data.spawnGroupData = GetLegacySpawnGroup();

            AddCreatureToGrid(spawnId, data);

            // We use spawn coords to spawn
            if (!map.Instanceable() && !map.IsRemovalGrid(data.spawnPoint))
            {
                Creature creature = Creature.CreateCreatureFromDB(spawnId, map, true, true);
                if (!creature)
                {
                    Log.outError(LogFilter.Server, "AddCreature: Cannot add creature entry {0} to map", entry);
                    return 0;
                }
            }

            return spawnId;
        }
        public List<uint> GetCreatureQuestItemList(uint id)
        {
            return _creatureQuestItemStorage.LookupByKey(id);
        }
        public CreatureAddon GetCreatureAddon(ulong lowguid)
        {
            return creatureAddonStorage.LookupByKey(lowguid);
        }
        public CreatureTemplate GetCreatureTemplate(uint entry)
        {
            return _creatureTemplateStorage.LookupByKey(entry);
        }
        public CreatureAddon GetCreatureTemplateAddon(uint entry)
        {
            return creatureTemplateAddonStorage.LookupByKey(entry);
        }
        public uint GetCreatureDefaultTrainer(uint creatureId)
        {
            return GetCreatureTrainerForGossipOption(creatureId, 0, 0);
        }
        public uint GetCreatureTrainerForGossipOption(uint creatureId, uint gossipMenuId, uint gossipOptionIndex)
        {
            return _creatureDefaultTrainers.LookupByKey((creatureId, gossipMenuId, gossipOptionIndex));
        }
        public Dictionary<uint, CreatureTemplate> GetCreatureTemplates()
        {
            return _creatureTemplateStorage;
        }
        public Dictionary<ulong, CreatureData> GetAllCreatureData() { return creatureDataStorage; }
        public CreatureData GetCreatureData(ulong guid)
        {
            return creatureDataStorage.LookupByKey(guid);
        }
        public ObjectGuid GetLinkedRespawnGuid(ObjectGuid guid)
        {
            var retGuid = linkedRespawnStorage.LookupByKey(guid);
            if (retGuid.IsEmpty())
                return ObjectGuid.Empty;
            return retGuid;
        }
        public bool SetCreatureLinkedRespawn(ulong guidLow, ulong linkedGuidLow)
        {
            if (guidLow == 0)
                return false;

            CreatureData master = GetCreatureData(guidLow);
            ObjectGuid guid = ObjectGuid.Create(HighGuid.Creature, master.spawnPoint.GetMapId(), master.Id, guidLow);
            PreparedStatement stmt;

            if (linkedGuidLow == 0) // we're removing the linking
            {
                linkedRespawnStorage.Remove(guid);
                stmt = DB.World.GetPreparedStatement(WorldStatements.DEL_LINKED_RESPAWN);
                stmt.AddValue(0, guidLow);
                stmt.AddValue(1, (uint)CreatureLinkedRespawnType.CreatureToCreature);
                DB.World.Execute(stmt);
                return true;
            }

            CreatureData slave = GetCreatureData(linkedGuidLow);
            if (slave == null)
            {
                Log.outError(LogFilter.Sql, "Creature '{0}' linking to non-existent creature '{1}'.", guidLow, linkedGuidLow);
                return false;
            }

            MapRecord map = CliDB.MapStorage.LookupByKey(master.spawnPoint.GetMapId());
            if (map == null || !map.Instanceable() || (master.spawnPoint.GetMapId() != slave.spawnPoint.GetMapId()))
            {
                Log.outError(LogFilter.Sql, "Creature '{0}' linking to '{1}' on an unpermitted map.", guidLow, linkedGuidLow);
                return false;
            }

            // they must have a possibility to meet (normal/heroic difficulty)
            if (!master.spawnDifficulties.Intersect(slave.spawnDifficulties).Any())
            {
                Log.outError(LogFilter.Sql, "LinkedRespawn: Creature '{0}' linking to '{1}' with not corresponding spawnMask", guidLow, linkedGuidLow);
                return false;
            }

            ObjectGuid linkedGuid = ObjectGuid.Create(HighGuid.Creature, slave.spawnPoint.GetMapId(), slave.Id, linkedGuidLow);

            linkedRespawnStorage[guid] = linkedGuid;
            stmt = DB.World.GetPreparedStatement(WorldStatements.REP_LINKED_RESPAWN);
            stmt.AddValue(0, guidLow);
            stmt.AddValue(1, linkedGuidLow);
            stmt.AddValue(2, (uint)CreatureLinkedRespawnType.CreatureToCreature);
            DB.World.Execute(stmt);
            return true;
        }
        public CreatureData NewOrExistCreatureData(ulong guid)
        {
            if (!creatureDataStorage.ContainsKey(guid))
                creatureDataStorage[guid] = new CreatureData();
            return creatureDataStorage[guid];
        }
        public void DeleteCreatureData(ulong guid)
        {
            CreatureData data = GetCreatureData(guid);
            if (data != null)
            {
                RemoveCreatureFromGrid(guid, data);
                OnDeleteSpawnData(data);
            }
            
            creatureDataStorage.Remove(guid);
        }
        public CreatureBaseStats GetCreatureBaseStats(uint level, uint unitClass)
        {
            var stats = creatureBaseStatsStorage.LookupByKey(MathFunctions.MakePair16(level, unitClass));
            if (stats != null)
                return stats;

            return new DefaultCreatureBaseStats();
        }
        public CreatureModelInfo GetCreatureModelRandomGender(ref CreatureModel model, CreatureTemplate creatureTemplate)
        {
            CreatureModelInfo modelInfo = GetCreatureModelInfo(model.CreatureDisplayID);
            if (modelInfo == null)
                return null;

            // If a model for another gender exists, 50% chance to use it
            if (modelInfo.DisplayIdOtherGender != 0 && RandomHelper.URand(0, 1) == 0)
            {
                CreatureModelInfo minfotmp = GetCreatureModelInfo(modelInfo.DisplayIdOtherGender);
                if (minfotmp == null)
                    Log.outError(LogFilter.Sql, $"Model (Entry: {model.CreatureDisplayID}) has modelidothergender {modelInfo.DisplayIdOtherGender} not found in table `creaturemodelinfo`. ");
                else
                {
                    // DisplayID changed
                    model.CreatureDisplayID = modelInfo.DisplayIdOtherGender;
                    if (creatureTemplate != null)
                    {
                        var creatureModel = creatureTemplate.Models.Find(templateModel =>
                        {
                            return templateModel.CreatureDisplayID == modelInfo.DisplayIdOtherGender;
                        });

                        if (creatureModel != null)
                           model = creatureModel;
                    }
                    return minfotmp;
                }
            }

            return modelInfo;
        }
        public CreatureModelInfo GetCreatureModelInfo(uint modelId)
        {
            return creatureModelStorage.LookupByKey(modelId);
        }
        public NpcText GetNpcText(uint textId)
        {
            return _npcTextStorage.LookupByKey(textId);
        }

        //GameObjects
        public void LoadGameObjectTemplate()
        {
            var time = Time.GetMSTime();

            foreach (GameObjectsRecord db2go in CliDB.GameObjectsStorage.Values)
            {
                GameObjectTemplate go = new();
                go.entry = db2go.Id;
                go.type = db2go.TypeID;
                go.displayId = db2go.DisplayID;
                go.name = db2go.Name[Global.WorldMgr.GetDefaultDbcLocale()];
                go.size = db2go.Scale;

                unsafe
                {
                    for (byte x = 0; x < db2go.PropValue.Length; ++x)
                        go.Raw.data[x] = db2go.PropValue[x];
                }

                go.ContentTuningId = 0;
                go.ScriptId = 0;

                _gameObjectTemplateStorage[db2go.Id] = go;
            }

            //                                          0      1     2          3     4         5               6     7
            SQLResult result = DB.World.Query("SELECT entry, type, displayId, name, IconName, castBarCaption, unk1, size, " +
                //8      9      10     11     12     13     14     15     16     17     18      19      20
                "Data0, Data1, Data2, Data3, Data4, Data5, Data6, Data7, Data8, Data9, Data10, Data11, Data12, " +
                //21      22      23      24      25      26      27      28      29      30      31      32      33      34      35      36
                "Data13, Data14, Data15, Data16, Data17, Data18, Data19, Data20, Data21, Data22, Data23, Data24, Data25, Data26, Data27, Data28, " +
                //37      38       39     40      41      42               43      44
                "Data29, Data30, Data31, Data32, Data33, ContentTuningId, AIName, ScriptName FROM gameobject_template");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gameobject definitions. DB table `gameobject_template` is empty.");
            }
            else
            {
                do
                {
                    uint entry = result.Read<uint>(0);

                    GameObjectTemplate got = new();

                    got.entry = entry;
                    got.type = (GameObjectTypes)result.Read<uint>(1);
                    got.displayId = result.Read<uint>(2);
                    got.name = result.Read<string>(3);
                    got.IconName = result.Read<string>(4);
                    got.castBarCaption = result.Read<string>(5);
                    got.unk1 = result.Read<string>(6);
                    got.size = result.Read<float>(7);

                    unsafe
                    {
                        for (byte x = 0; x < SharedConst.MaxGOData; ++x)
                            got.Raw.data[x] = result.Read<int>(8 + x);
                    }

                    got.ContentTuningId = result.Read<uint>(42);
                    got.AIName = result.Read<string>(43);
                    got.ScriptId = Global.ObjectMgr.GetScriptId(result.Read<string>(44));

                    switch (got.type)
                    {
                        case GameObjectTypes.Door:                      //0
                            if (got.Door.open != 0)
                                CheckGOLockId(got, got.Door.open, 1);
                            CheckGONoDamageImmuneId(got, got.Door.noDamageImmune, 3);
                            break;
                        case GameObjectTypes.Button:                    //1
                            if (got.Button.open != 0)
                                CheckGOLockId(got, got.Button.open, 1);
                            CheckGONoDamageImmuneId(got, got.Button.noDamageImmune, 4);
                            break;
                        case GameObjectTypes.QuestGiver:                //2
                            if (got.QuestGiver.open != 0)
                                CheckGOLockId(got, got.QuestGiver.open, 0);
                            CheckGONoDamageImmuneId(got, got.QuestGiver.noDamageImmune, 5);
                            break;
                        case GameObjectTypes.Chest:                     //3
                            if (got.Chest.open != 0)
                                CheckGOLockId(got, got.Chest.open, 0);

                            CheckGOConsumable(got, got.Chest.consumable, 3);

                            if (got.Chest.linkedTrap != 0)              // linked trap
                                CheckGOLinkedTrapId(got, got.Chest.linkedTrap, 7);
                            break;
                        case GameObjectTypes.Trap:                      //6
                            if (got.Trap.open != 0)
                                CheckGOLockId(got, got.Trap.open, 0);
                            break;
                        case GameObjectTypes.Chair:                     //7
                            CheckAndFixGOChairHeightId(got, ref got.Chair.chairheight, 1);
                            break;
                        case GameObjectTypes.SpellFocus:               //8
                            if (got.SpellFocus.spellFocusType != 0)
                            {
                                if (!CliDB.SpellFocusObjectStorage.ContainsKey(got.SpellFocus.spellFocusType))
                                    Log.outError(LogFilter.Sql, "GameObject (Entry: {0} GoType: {1}) have data0={2} but SpellFocus (Id: {3}) not exist.",
                                        entry, got.type, got.SpellFocus.spellFocusType, got.SpellFocus.spellFocusType);
                            }

                            if (got.SpellFocus.linkedTrap != 0)        // linked trap
                                CheckGOLinkedTrapId(got, got.SpellFocus.linkedTrap, 2);
                            break;
                        case GameObjectTypes.Goober:                    //10
                            if (got.Goober.open != 0)
                                CheckGOLockId(got, got.Goober.open, 0);

                            CheckGOConsumable(got, got.Goober.consumable, 3);

                            if (got.Goober.pageID != 0)                  // pageId
                            {
                                if (GetPageText(got.Goober.pageID) == null)
                                    Log.outError(LogFilter.Sql, "GameObject (Entry: {0} GoType: {1}) have data7={2} but PageText (Entry {3}) not exist.", entry, got.type, got.Goober.pageID, got.Goober.pageID);
                            }
                            CheckGONoDamageImmuneId(got, got.Goober.noDamageImmune, 11);
                            if (got.Goober.linkedTrap != 0)            // linked trap
                                CheckGOLinkedTrapId(got, got.Goober.linkedTrap, 12);
                            break;
                        case GameObjectTypes.AreaDamage:                //12
                            if (got.AreaDamage.open != 0)
                                CheckGOLockId(got, got.AreaDamage.open, 0);
                            break;
                        case GameObjectTypes.Camera:                    //13
                            if (got.Camera.open != 0)
                                CheckGOLockId(got, got.Camera.open, 0);
                            break;
                        case GameObjectTypes.MapObjTransport:              //15
                            {
                                if (got.MoTransport.taxiPathID != 0)
                                {
                                    if (got.MoTransport.taxiPathID >= CliDB.TaxiPathNodesByPath.Count || CliDB.TaxiPathNodesByPath[got.MoTransport.taxiPathID].Empty())
                                        Log.outError(LogFilter.Sql, "GameObject (Entry: {0} GoType: {1}) have data0={2} but TaxiPath (Id: {3}) not exist.",
                                        entry, got.type, got.MoTransport.taxiPathID, got.MoTransport.taxiPathID);
                                }
                                int transportMap = got.MoTransport.SpawnMap;
                                if (transportMap != 0)
                                    _transportMaps.Add((ushort)transportMap);
                                break;
                            }
                        case GameObjectTypes.SpellCaster:               //22
                                                                        // always must have spell
                            CheckGOSpellId(got, got.SpellCaster.spell, 0);
                            break;
                        case GameObjectTypes.FlagStand:                 //24
                            if (got.FlagStand.open != 0)
                                CheckGOLockId(got, got.FlagStand.open, 0);
                            CheckGONoDamageImmuneId(got, got.FlagStand.noDamageImmune, 5);
                            break;
                        case GameObjectTypes.FishingHole:               //25
                            if (got.FishingHole.open != 0)
                                CheckGOLockId(got, got.FishingHole.open, 4);
                            break;
                        case GameObjectTypes.FlagDrop:                  //26
                            if (got.FlagDrop.open != 0)
                                CheckGOLockId(got, got.FlagDrop.open, 0);
                            CheckGONoDamageImmuneId(got, got.FlagDrop.noDamageImmune, 3);
                            break;
                        case GameObjectTypes.BarberChair:              //32
                            CheckAndFixGOChairHeightId(got, ref got.BarberChair.chairheight, 0);
                            if (got.BarberChair.SitAnimKit != 0 && !CliDB.AnimKitStorage.ContainsKey(got.BarberChair.SitAnimKit))
                            {
                                Log.outError(LogFilter.Sql, "GameObject (Entry: {0} GoType: {1}) have data2 = {2} but AnimKit.dbc (Id: {3}) not exist, set to 0.",
                                   entry, got.type, got.BarberChair.SitAnimKit, got.BarberChair.SitAnimKit);
                                got.BarberChair.SitAnimKit = 0;
                            }
                            break;
                        case GameObjectTypes.GarrisonBuilding:
                            {
                                int transportMap = got.garrisonBuilding.SpawnMap;
                                if (transportMap != 0)
                                    _transportMaps.Add((ushort)transportMap);
                            }
                            break;
                        case GameObjectTypes.GatheringNode:
                            if (got.GatheringNode.open != 0)
                                CheckGOLockId(got, got.GatheringNode.open, 0);
                            if (got.GatheringNode.linkedTrap != 0)
                                CheckGOLinkedTrapId(got, got.GatheringNode.linkedTrap, 20);
                            break;
                    }

                    _gameObjectTemplateStorage[entry] = got;
                }
                while (result.NextRow());

                Log.outInfo(LogFilter.ServerLoading, "Loaded {0} game object templates in {1} ms", _gameObjectTemplateStorage.Count, Time.GetMSTimeDiffToNow(time));
            }
        }
        public void LoadGameObjectTemplateAddons()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0       1       2      3        4        5              6
            SQLResult result = DB.World.Query("SELECT entry, faction, flags, mingold, maxgold, WorldEffectID, AIAnimKitID FROM gameobject_template_addon");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gameobject template addon definitions. DB table `gameobject_template_addon` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint entry = result.Read<uint>(0);

                GameObjectTemplate got = Global.ObjectMgr.GetGameObjectTemplate(entry);
                if (got == null)
                {
                    Log.outError(LogFilter.Sql, $"GameObject template (Entry: {entry}) does not exist but has a record in `gameobject_template_addon`");
                    continue;
                }

                GameObjectTemplateAddon gameObjectAddon = new();
                gameObjectAddon.faction = result.Read<ushort>(1);
                gameObjectAddon.flags = result.Read<uint>(2);
                gameObjectAddon.mingold = result.Read<uint>(3);
                gameObjectAddon.maxgold = result.Read<uint>(4);
                gameObjectAddon.WorldEffectID = result.Read<uint>(5);
                gameObjectAddon.AIAnimKitID = result.Read<uint>(6);

                // checks
                if (gameObjectAddon.faction != 0 && !CliDB.FactionTemplateStorage.ContainsKey(gameObjectAddon.faction))
                    Log.outError(LogFilter.Sql, $"GameObject (Entry: {entry}) has invalid faction ({gameObjectAddon.faction}) defined in `gameobject_template_addon`.");

                if (gameObjectAddon.maxgold > 0)
                {
                    switch (got.type)
                    {
                        case GameObjectTypes.Chest:
                        case GameObjectTypes.FishingHole:
                            break;
                        default:
                            Log.outError(LogFilter.Sql, $"GameObject (Entry {entry} GoType: {got.type}) cannot be looted but has maxgold set in `gameobject_template_addon`.");
                            break;
                    }
                }

                if (gameObjectAddon.WorldEffectID != 0 && !CliDB.WorldEffectStorage.ContainsKey(gameObjectAddon.WorldEffectID))
                {
                    Log.outError(LogFilter.Sql, $"GameObject (Entry: {entry}) has invalid WorldEffectID ({gameObjectAddon.WorldEffectID}) defined in `gameobject_template_addon`, set to 0.");
                    gameObjectAddon.WorldEffectID = 0;
                }

                if (gameObjectAddon.AIAnimKitID != 0 && !CliDB.AnimKitStorage.ContainsKey(gameObjectAddon.AIAnimKitID))
                {
                    Log.outError(LogFilter.Sql, $"GameObject (Entry: {entry}) has invalid AIAnimKitID ({gameObjectAddon.AIAnimKitID}) defined in `gameobject_template_addon`, set to 0.");
                    gameObjectAddon.AIAnimKitID = 0;
                }

                _gameObjectTemplateAddonStorage[entry] = gameObjectAddon;
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} game object template addons in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadGameObjects()
        {
            var time = Time.GetMSTime();
            //                                         0                1   2    3           4           5           6
            SQLResult result = DB.World.Query("SELECT gameobject.guid, id, map, position_x, position_y, position_z, orientation, " +
                //7          8          9          10         11             12            13     14                 15          16
                "rotation0, rotation1, rotation2, rotation3, spawntimesecs, animprogress, state, spawnDifficulties, eventEntry, pool_entry, " +
                //17             18       19          20              21
                "phaseUseFlags, phaseid, phasegroup, terrainSwapMap, ScriptName " +
                "FROM gameobject LEFT OUTER JOIN game_event_gameobject ON gameobject.guid = game_event_gameobject.guid " +
                "LEFT OUTER JOIN pool_gameobject ON gameobject.guid = pool_gameobject.guid");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gameobjects. DB table `gameobject` is empty.");

                return;
            }
            uint count = 0;

            // build single time for check spawnmask
            Dictionary<uint, List<Difficulty>> spawnMasks = new();
            foreach (var mapDifficultyPair in Global.DB2Mgr.GetMapDifficulties())
            {
                foreach (var difficultyPair in mapDifficultyPair.Value)
                {
                    if (!spawnMasks.ContainsKey(mapDifficultyPair.Key))
                        spawnMasks[mapDifficultyPair.Key] = new List<Difficulty>();

                    spawnMasks[mapDifficultyPair.Key].Add((Difficulty)difficultyPair.Key);
                }
            }

            PhaseShift phaseShift = new();

            do
            {
                ulong guid = result.Read<ulong>(0);
                uint entry = result.Read<uint>(1);

                GameObjectTemplate gInfo = GetGameObjectTemplate(entry);
                if (gInfo == null)
                {
                    Log.outError(LogFilter.Sql, "Table `gameobject` has gameobject (GUID: {0}) with non existing gameobject entry {1}, skipped.", guid, entry);
                    continue;
                }

                if (gInfo.displayId == 0)
                {
                    switch (gInfo.type)
                    {
                        case GameObjectTypes.Trap:
                        case GameObjectTypes.SpellFocus:
                            break;
                        default:
                            Log.outError(LogFilter.Sql, "Gameobject (GUID: {0} Entry {1} GoType: {2}) doesn't have a displayId ({3}), not loaded.", guid, entry, gInfo.type, gInfo.displayId);
                            break;
                    }
                }

                if (gInfo.displayId != 0 && !CliDB.GameObjectDisplayInfoStorage.ContainsKey(gInfo.displayId))
                {
                    Log.outError(LogFilter.Sql, "Gameobject (GUID: {0} Entry {1} GoType: {2}) has an invalid displayId ({3}), not loaded.", guid, entry, gInfo.type, gInfo.displayId);
                    continue;
                }

                GameObjectData data = new();
                data.spawnId = guid;
                data.Id = entry;
                data.spawnPoint = new WorldLocation(result.Read<ushort>(2), result.Read<float>(3), result.Read<float>(4), result.Read<float>(5), result.Read<float>(6));
                data.rotation.X = result.Read<float>(7);
                data.rotation.Y = result.Read<float>(8);
                data.rotation.Z = result.Read<float>(9);
                data.rotation.W = result.Read<float>(10);
                data.spawntimesecs = result.Read<int>(11);
                data.spawnGroupData = _spawnGroupDataStorage[0];

                var mapEntry = CliDB.MapStorage.LookupByKey(data.spawnPoint.GetMapId());
                if (mapEntry == null)
                {
                    Log.outError(LogFilter.Sql, "Table `gameobject` has gameobject (GUID: {0} Entry: {1}) spawned on a non-existed map (Id: {2}), skip", guid, data.Id, data.spawnPoint.GetMapId());
                    continue;
                }

                if (data.spawntimesecs == 0 && gInfo.IsDespawnAtAction())
                {
                    Log.outError(LogFilter.Sql, "Table `gameobject` has gameobject (GUID: {0} Entry: {1}) with `spawntimesecs` (0) value, but the gameobejct is marked as despawnable at action.", guid, data.Id);
                }

                data.animprogress = result.Read<uint>(12);
                data.artKit = 0;

                uint gostate = result.Read<uint>(13);
                if (gostate >= (uint)GameObjectState.Max)
                {
                    if (gInfo.type != GameObjectTypes.Transport || gostate > (int)GameObjectState.TransportActive + SharedConst.MaxTransportStopFrames)
                    {
                        Log.outError(LogFilter.Sql, "Table `gameobject` has gameobject (GUID: {0} Entry: {1}) with invalid `state` ({2}) value, skip", guid, data.Id, gostate);
                        continue;
                    }
                }
                data.goState = (GameObjectState)gostate;

                data.spawnDifficulties = ParseSpawnDifficulties(result.Read<string>(14), "gameobject", guid, data.spawnPoint.GetMapId(), spawnMasks.LookupByKey(data.spawnPoint.GetMapId()));
                if (data.spawnDifficulties.Empty())
                {
                    Log.outError(LogFilter.Sql, $"Table `creature` has creature (GUID: {guid}) that is not spawned in any difficulty, skipped.");
                    continue;
                }

                short gameEvent = result.Read<sbyte>(15);
                uint PoolId = result.Read<uint>(16);
                data.phaseUseFlags = (PhaseUseFlagsValues)result.Read<byte>(17);
                data.phaseId = result.Read<uint>(18);
                data.phaseGroup = result.Read<uint>(19);

                if (Convert.ToBoolean(data.phaseUseFlags & ~PhaseUseFlagsValues.All))
                {
                    Log.outError(LogFilter.Sql, "Table `gameobject` have gameobject (GUID: {0} Entry: {1}) has unknown `phaseUseFlags` set, removed unknown value.", guid, data.Id);
                    data.phaseUseFlags &= PhaseUseFlagsValues.All;
                }

                if (data.phaseUseFlags.HasAnyFlag(PhaseUseFlagsValues.AlwaysVisible) && data.phaseUseFlags.HasAnyFlag(PhaseUseFlagsValues.Inverse))
                {
                    Log.outError(LogFilter.Sql, "Table `gameobject` have gameobject (GUID: {0} Entry: {1}) has both `phaseUseFlags` PHASE_USE_FLAGS_ALWAYS_VISIBLE and PHASE_USE_FLAGS_INVERSE," +
                        " removing PHASE_USE_FLAGS_INVERSE.", guid, data.Id);
                    data.phaseUseFlags &= ~PhaseUseFlagsValues.Inverse;
                }

                if (data.phaseGroup != 0 && data.phaseId != 0)
                {
                    Log.outError(LogFilter.Sql, "Table `gameobject` have gameobject (GUID: {0} Entry: {1}) with both `phaseid` and `phasegroup` set, `phasegroup` set to 0", guid, data.Id);
                    data.phaseGroup = 0;
                }

                if (data.phaseId != 0)
                {
                    if (!CliDB.PhaseStorage.ContainsKey(data.phaseId))
                    {
                        Log.outError(LogFilter.Sql, "Table `gameobject` have gameobject (GUID: {0} Entry: {1}) with `phaseid` {2} does not exist, set to 0", guid, data.Id, data.phaseId);
                        data.phaseId = 0;
                    }
                }

                if (data.phaseGroup != 0)
                {
                    if (Global.DB2Mgr.GetPhasesForGroup(data.phaseGroup).Empty())
                    {
                        Log.outError(LogFilter.Sql, "Table `gameobject` have gameobject (GUID: {0} Entry: {1}) with `phaseGroup` {2} does not exist, set to 0", guid, data.Id, data.phaseGroup);
                        data.phaseGroup = 0;
                    }
                }

                data.terrainSwapMap = result.Read<int>(20);
                if (data.terrainSwapMap != -1)
                {
                    MapRecord terrainSwapEntry = CliDB.MapStorage.LookupByKey(data.terrainSwapMap);
                    if (terrainSwapEntry == null)
                    {
                        Log.outError(LogFilter.Sql, "Table `gameobject` have gameobject (GUID: {0} Entry: {1}) with `terrainSwapMap` {2} does not exist, set to -1", guid, data.Id, data.terrainSwapMap);
                        data.terrainSwapMap = -1;
                    }
                    else if (terrainSwapEntry.ParentMapID != data.spawnPoint.GetMapId())
                    {
                        Log.outError(LogFilter.Sql, "Table `gameobject` have gameobject (GUID: {0} Entry: {1}) with `terrainSwapMap` {2} which cannot be used on spawn map, set to -1", guid, data.Id, data.terrainSwapMap);
                        data.terrainSwapMap = -1;
                    }
                }

                data.ScriptId = GetScriptId(result.Read<string>(21));

                if (data.rotation.X < -1.0f || data.rotation.X > 1.0f)
                {
                    Log.outError(LogFilter.Sql, "Table `gameobject` has gameobject (GUID: {0} Entry: {1}) with invalid rotationX ({2}) value, skip", guid, data.Id, data.rotation.X);
                    continue;
                }

                if (data.rotation.Y < -1.0f || data.rotation.Y > 1.0f)
                {
                    Log.outError(LogFilter.Sql, "Table `gameobject` has gameobject (GUID: {0} Entry: {1}) with invalid rotationY ({2}) value, skip", guid, data.Id, data.rotation.Y);
                    continue;
                }

                if (data.rotation.Z < -1.0f || data.rotation.Z > 1.0f)
                {
                    Log.outError(LogFilter.Sql, "Table `gameobject` has gameobject (GUID: {0} Entry: {1}) with invalid rotationZ ({2}) value, skip", guid, data.Id, data.rotation.Z);
                    continue;
                }

                if (data.rotation.W < -1.0f || data.rotation.W > 1.0f)
                {
                    Log.outError(LogFilter.Sql, "Table `gameobject` has gameobject (GUID: {0} Entry: {1}) with invalid rotationW ({2}) value, skip", guid, data.Id, data.rotation.W);
                    continue;
                }

                if (!GridDefines.IsValidMapCoord(data.spawnPoint))
                {
                    Log.outError(LogFilter.Sql, "Table `gameobject` has gameobject (GUID: {0} Entry: {1}) with invalid coordinates, skip", guid, data.Id);
                    continue;
                }

                if (WorldConfig.GetBoolValue(WorldCfg.CalculateGameobjectZoneAreaData))
                {
                    PhasingHandler.InitDbVisibleMapId(phaseShift, data.terrainSwapMap);
                    Global.MapMgr.GetZoneAndAreaId(phaseShift, out uint zoneId, out uint areaId, data.spawnPoint);

                    PreparedStatement stmt = DB.World.GetPreparedStatement(WorldStatements.UPD_GAMEOBJECT_ZONE_AREA_DATA);
                    stmt.AddValue(0, zoneId);
                    stmt.AddValue(1, areaId);
                    stmt.AddValue(2, guid);
                    DB.World.Execute(stmt);
                }

                if (gameEvent == 0 && PoolId == 0)                      // if not this is to be managed by GameEvent System or Pool system
                    AddGameObjectToGrid(guid, data);

                gameObjectDataStorage[guid] = data;
                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} gameobjects in {1} ms", count, Time.GetMSTimeDiffToNow(time));
        }
        public void LoadGameObjectAddons()
        {
            uint oldMSTime = Time.GetMSTime();

            _gameObjectAddonStorage.Clear();

            //                                         0     1                 2                 3                 4                 5                 6                  7              8
            SQLResult result = DB.World.Query("SELECT guid, parent_rotation0, parent_rotation1, parent_rotation2, parent_rotation3, invisibilityType, invisibilityValue, WorldEffectID, AIAnimKitID FROM gameobject_addon");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gameobject addon definitions. DB table `gameobject_addon` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                ulong guid = result.Read<ulong>(0);

                GameObjectData goData = GetGameObjectData(guid);
                if (goData == null)
                {
                    Log.outError(LogFilter.Sql, $"GameObject (GUID: {guid}) does not exist but has a record in `gameobject_addon`");
                    continue;
                }

                GameObjectAddon gameObjectAddon = new();
                gameObjectAddon.ParentRotation = new Quaternion(result.Read<float>(1), result.Read<float>(2), result.Read<float>(3), result.Read<float>(4));
                gameObjectAddon.invisibilityType = (InvisibilityType)result.Read<byte>(5);
                gameObjectAddon.invisibilityValue = result.Read<uint>(6);
                gameObjectAddon.WorldEffectID = result.Read<uint>(7);
                gameObjectAddon.AIAnimKitID = result.Read<uint>(8);

                if (gameObjectAddon.invisibilityType >= InvisibilityType.Max)
                {
                    Log.outError(LogFilter.Sql, $"GameObject (GUID: {guid}) has invalid InvisibilityType in `gameobject_addon`, disabled invisibility");
                    gameObjectAddon.invisibilityType = InvisibilityType.General;
                    gameObjectAddon.invisibilityValue = 0;
                }

                if (gameObjectAddon.invisibilityType != 0 && gameObjectAddon.invisibilityValue == 0)
                {
                    Log.outError(LogFilter.Sql, $"GameObject (GUID: {guid}) has InvisibilityType set but has no InvisibilityValue in `gameobject_addon`, set to 1");
                    gameObjectAddon.invisibilityValue = 1;
                }

                if (!gameObjectAddon.ParentRotation.isUnit())
                {
                    Log.outError(LogFilter.Sql, $"GameObject (GUID: {guid}) has invalid parent rotation in `gameobject_addon`, set to default");
                    gameObjectAddon.ParentRotation = Quaternion.WAxis;
                }

                if (gameObjectAddon.WorldEffectID != 0 && !CliDB.WorldEffectStorage.ContainsKey(gameObjectAddon.WorldEffectID))
                {
                    Log.outError(LogFilter.Sql, $"GameObject (GUID: {guid}) has invalid WorldEffectID ({gameObjectAddon.WorldEffectID}) in `gameobject_addon`, set to 0.");
                    gameObjectAddon.WorldEffectID = 0;
                }

                if (gameObjectAddon.AIAnimKitID != 0 && !CliDB.AnimKitStorage.ContainsKey(gameObjectAddon.AIAnimKitID))
                {
                    Log.outError(LogFilter.Sql, $"GameObject (GUID: {guid}) has invalid AIAnimKitID ({gameObjectAddon.AIAnimKitID}) in `gameobject_addon`, set to 0.");
                    gameObjectAddon.AIAnimKitID = 0;
                }

                _gameObjectAddonStorage[guid] = gameObjectAddon;
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} gameobject addons in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }
        public void LoadGameObjectQuestItems()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                           0                1
            SQLResult result = DB.World.Query("SELECT GameObjectEntry, ItemId, Idx FROM gameobject_questitem ORDER BY Idx ASC");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gameobject quest items. DB table `gameobject_questitem` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint entry = result.Read<uint>(0);
                uint item = result.Read<uint>(1);
                uint idx = result.Read<uint>(2);

                if (!_gameObjectTemplateStorage.ContainsKey(entry))
                {
                    Log.outError(LogFilter.Sql, "Table `gameobject_questitem` has data for nonexistent gameobject (entry: {0}, idx: {1}), skipped", entry, idx);
                    continue;
                }

                if (!CliDB.ItemStorage.ContainsKey(item))
                {
                    Log.outError(LogFilter.Sql, "Table `gameobject_questitem` has nonexistent item (ID: {0}) in gameobject (entry: {1}, idx: {2}), skipped", item, entry, idx);
                    continue;
                }

                _gameObjectQuestItemStorage.Add(entry, item);

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} gameobject quest items in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadGameObjectForQuests()
        {
            uint oldMSTime = Time.GetMSTime();

            _gameObjectForQuestStorage.Clear();                         // need for reload case

            if (GetGameObjectTemplates().Empty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 GameObjects for quests");
                return;
            }

            uint count = 0;

            // collect GO entries for GO that must activated
            foreach (var pair in GetGameObjectTemplates())
            {
                switch (pair.Value.type)
                {
                    case GameObjectTypes.QuestGiver:
                        break;
                    case GameObjectTypes.Chest:
                        {
                            // scan GO chest with loot including quest items
                            uint lootId = pair.Value.GetLootId();

                            // find quest loot for GO
                            if (pair.Value.Chest.questID != 0 || Loots.LootStorage.Gameobject.HaveQuestLootFor(lootId))
                                break;

                            continue;
                        }
                    case GameObjectTypes.Generic:
                        {
                            if (pair.Value.Generic.questID > 0)            //quests objects
                                break;

                            continue;
                        }
                    case GameObjectTypes.Goober:
                        {
                            if (pair.Value.Goober.questID > 0)              //quests objects
                                break;

                            continue;
                        }
                    default:
                        continue;
                }

                _gameObjectForQuestStorage.Add(pair.Value.entry);
                ++count;
            }

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} GameObjects for quests in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void AddGameObjectToGrid(ulong guid, GameObjectData data)
        {
            foreach (Difficulty difficulty in data.spawnDifficulties)
            {
                CellCoord cellCoord = GridDefines.ComputeCellCoord(data.spawnPoint.GetPositionX(), data.spawnPoint.GetPositionY());
                var cellguids = CreateCellObjectGuids(data.spawnPoint.GetMapId(), difficulty, cellCoord.GetId());
                cellguids.gameobjects.Add(guid);
            }
        }
        public void RemoveGameObjectFromGrid(ulong guid, GameObjectData data)
        {
            foreach (Difficulty difficulty in data.spawnDifficulties)
            {
                CellCoord cellCoord = GridDefines.ComputeCellCoord(data.spawnPoint.GetPositionX(), data.spawnPoint.GetPositionY());
                CellObjectGuids cellguids = GetCellObjectGuids(data.spawnPoint.GetMapId(), difficulty, cellCoord.GetId());
                if (cellguids == null)
                    return;

                cellguids.gameobjects.Remove(guid);
            }
        }
        public ulong AddGameObjectData(uint entry, uint mapId, Position pos, Quaternion rot, uint spawntimedelay)
        {
            GameObjectTemplate goinfo = GetGameObjectTemplate(entry);
            if (goinfo == null)
                return 0;

            Map map = Global.MapMgr.CreateBaseMap(mapId);
            if (map == null)
                return 0;

            ulong spawnId = GenerateGameObjectSpawnId();
            GameObjectData data = NewOrExistGameObjectData(spawnId);
            data.spawnId = spawnId;
            data.Id = entry;
            data.spawnPoint.WorldRelocate(mapId, pos);
            data.rotation = rot;
            data.spawntimesecs = (int)spawntimedelay;
            data.animprogress = 100;
            data.spawnDifficulties.Add(Difficulty.None);
            data.goState = GameObjectState.Ready;
            data.artKit = (byte)(goinfo.type == GameObjectTypes.ControlZone ? 21 : 0);
            data.dbData = false;
            data.spawnGroupData = GetLegacySpawnGroup();

            AddGameObjectToGrid(spawnId, data);

            // Spawn if necessary (loaded grids only)
            // We use spawn coords to spawn
            if (!map.Instanceable() && map.IsGridLoaded(data.spawnPoint))
            {
                GameObject go = GameObject.CreateGameObjectFromDB(spawnId, map);
                if (!go)
                {
                    Log.outError(LogFilter.Server, "AddGameObjectData: cannot add gameobject entry {0} to map", entry);
                    return 0;
                }
            }

            Log.outDebug(LogFilter.Maps, $"AddGameObjectData: dbguid:{spawnId} entry:{entry} map:{mapId} pos:{data.spawnPoint}");

            return spawnId;
        }

        public GameObjectAddon GetGameObjectAddon(ulong lowguid)
        {
            return _gameObjectAddonStorage.LookupByKey(lowguid);
        }
        public List<uint> GetGameObjectQuestItemList(uint id)
        {
            return _gameObjectQuestItemStorage.LookupByKey(id);
        }
        MultiMap<uint, uint> GetGameObjectQuestItemMap() { return _gameObjectQuestItemStorage; }
        public Dictionary<ulong, GameObjectData> GetAllGameObjectData() { return gameObjectDataStorage; }
        public GameObjectData GetGameObjectData(ulong guid)
        {
            return gameObjectDataStorage.LookupByKey(guid);
        }
        public void DeleteGameObjectData(ulong guid)
        {
            GameObjectData data = GetGameObjectData(guid);
            if (data != null)
            { 
                RemoveGameObjectFromGrid(guid, data);
                OnDeleteSpawnData(data);
            }

            gameObjectDataStorage.Remove(guid);
        }
        public GameObjectData NewOrExistGameObjectData(ulong guid)
        {
            if (!gameObjectDataStorage.ContainsKey(guid))
                gameObjectDataStorage[guid] = new GameObjectData();

            return gameObjectDataStorage[guid];
        }
        public GameObjectTemplate GetGameObjectTemplate(uint entry)
        {
            return _gameObjectTemplateStorage.LookupByKey(entry);
        }
        public GameObjectTemplateAddon GetGameObjectTemplateAddon(uint entry)
        {
            return _gameObjectTemplateAddonStorage.LookupByKey(entry);
        }
        public Dictionary<uint, GameObjectTemplate> GetGameObjectTemplates()
        {
            return _gameObjectTemplateStorage;
        }
        public bool IsGameObjectForQuests(uint entry)
        {
            return _gameObjectForQuestStorage.Contains(entry);
        }
        void CheckGOLockId(GameObjectTemplate goInfo, uint dataN, uint N)
        {
            if (CliDB.LockStorage.ContainsKey(dataN))
                return;

            Log.outError(LogFilter.Sql, "Gameobject (Entry: {0} GoType: {1}) have data{2}={3} but lock (Id: {4}) not found.", goInfo.entry, goInfo.type, N, goInfo.Door.open, goInfo.Door.open);
        }
        void CheckGOLinkedTrapId(GameObjectTemplate goInfo, uint dataN, uint N)
        {
            GameObjectTemplate trapInfo = Global.ObjectMgr.GetGameObjectTemplate(dataN);
            if (trapInfo != null)
            {
                if (trapInfo.type != GameObjectTypes.Trap)
                    Log.outError(LogFilter.Sql, "Gameobject (Entry: {0} GoType: {1}) have data{2}={3} but GO (Entry {4}) have not GAMEOBJECT_TYPE_TRAP type.", goInfo.entry, goInfo.type, N, dataN, dataN);
            }
        }
        void CheckGOSpellId(GameObjectTemplate goInfo, uint dataN, uint N)
        {
            if (Global.SpellMgr.HasSpellInfo(dataN, Difficulty.None))
                return;

            Log.outError(LogFilter.Sql, "Gameobject (Entry: {0} GoType: {1}) have data{2}={3} but Spell (Entry {4}) not exist.", goInfo.entry, goInfo.type, N, dataN, dataN);
        }
        void CheckAndFixGOChairHeightId(GameObjectTemplate goInfo, ref uint dataN, uint N)
        {
            if (dataN <= (UnitStandStateType.SitHighChair - UnitStandStateType.SitLowChair))
                return;

            Log.outError(LogFilter.Sql, "Gameobject (Entry: {0} GoType: {1}) have data{2}={3} but correct chair height in range 0..{4}.", goInfo.entry, goInfo.type, N, dataN, UnitStandStateType.SitHighChair - UnitStandStateType.SitLowChair);

            // prevent client and server unexpected work
            dataN = 0;
        }
        void CheckGONoDamageImmuneId(GameObjectTemplate goTemplate, uint dataN, uint N)
        {
            // 0/1 correct values
            if (dataN <= 1)
                return;

            Log.outError(LogFilter.Sql, "Gameobject (Entry: {0} GoType: {1}) have data{2}={3} but expected boolean (0/1) noDamageImmune field value.", goTemplate.entry, goTemplate.type, N, dataN);
        }
        void CheckGOConsumable(GameObjectTemplate goInfo, uint dataN, uint N)
        {
            // 0/1 correct values
            if (dataN <= 1)
                return;

            Log.outError(LogFilter.Sql, "Gameobject (Entry: {0} GoType: {1}) have data{2}={3} but expected boolean (0/1) consumable field value.",
                goInfo.entry, goInfo.type, N, dataN);
        }

        List<Difficulty> ParseSpawnDifficulties(string difficultyString, string table, ulong spawnId, uint mapId, List<Difficulty> mapDifficulties)
        {
            List<Difficulty> difficulties = new();
            StringArray tokens = new(difficultyString, ',');
            if (tokens.Length == 0)
                return difficulties;

            bool isTransportMap = IsTransportMap(mapId);
            foreach (string token in tokens)
            {
                Difficulty difficultyId = (Difficulty)Enum.Parse(typeof(Difficulty), token);
                if (difficultyId != 0 && !CliDB.DifficultyStorage.ContainsKey(difficultyId))
                {
                    Log.outError(LogFilter.Sql, $"Table `{table}` has {table} (GUID: {spawnId}) with non invalid difficulty id {difficultyId}, skipped.");
                    continue;
                }

                if (!isTransportMap && !mapDifficulties.Contains(difficultyId))
                {
                    Log.outError(LogFilter.Sql, $"Table `{table}` has {table} (GUID: {spawnId}) has unsupported difficulty {difficultyId} for map (Id: {mapId}).");
                    continue;
                }

                difficulties.Add(difficultyId);
            }

            difficulties.Sort();
            return difficulties;
        }

        //Items
        public void LoadItemTemplates()
        {
            var oldMSTime = Time.GetMSTime();
            uint sparseCount = 0;

            foreach (var sparse in CliDB.ItemSparseStorage.Values)
            {
                ItemRecord db2Data = CliDB.ItemStorage.LookupByKey(sparse.Id);
                if (db2Data == null)
                    continue;

                var itemTemplate = new ItemTemplate(db2Data, sparse);
                itemTemplate.MaxDurability = FillMaxDurability(db2Data.ClassID, db2Data.SubclassID, sparse.inventoryType, (ItemQuality)sparse.OverallQualityID, sparse.ItemLevel);

                var itemSpecOverrides = Global.DB2Mgr.GetItemSpecOverrides(sparse.Id);
                if (itemSpecOverrides != null)
                {
                    foreach (ItemSpecOverrideRecord itemSpecOverride in itemSpecOverrides)
                    {
                        ChrSpecializationRecord specialization = CliDB.ChrSpecializationStorage.LookupByKey(itemSpecOverride.SpecID);
                        if (specialization != null)
                        {
                            itemTemplate.ItemSpecClassMask |= 1u << (specialization.ClassID - 1);
                            itemTemplate.Specializations[0].Set(ItemTemplate.CalculateItemSpecBit(specialization), true);

                            itemTemplate.Specializations[1] = itemTemplate.Specializations[1].Or(itemTemplate.Specializations[0]);
                            itemTemplate.Specializations[2] = itemTemplate.Specializations[2].Or(itemTemplate.Specializations[0]);
                        }
                    }
                }
                else
                {
                    ItemSpecStats itemSpecStats = new(db2Data, sparse);

                    foreach (ItemSpecRecord itemSpec in CliDB.ItemSpecStorage.Values)
                    {
                        if (itemSpecStats.ItemType != itemSpec.ItemType)
                            continue;

                        bool hasPrimary = itemSpec.PrimaryStat == ItemSpecStat.None;
                        bool hasSecondary = itemSpec.SecondaryStat == ItemSpecStat.None;
                        for (uint i = 0; i < itemSpecStats.ItemSpecStatCount; ++i)
                        {
                            if (itemSpecStats.ItemSpecStatTypes[i] == itemSpec.PrimaryStat)
                                hasPrimary = true;
                            if (itemSpecStats.ItemSpecStatTypes[i] == itemSpec.SecondaryStat)
                                hasSecondary = true;
                        }

                        if (!hasPrimary || !hasSecondary)
                            continue;

                        ChrSpecializationRecord specialization = CliDB.ChrSpecializationStorage.LookupByKey(itemSpec.SpecializationID);
                        if (specialization != null)
                        {
                            if (Convert.ToBoolean((1 << (specialization.ClassID - 1)) & sparse.AllowableClass))
                            {
                                itemTemplate.ItemSpecClassMask |= 1u << (specialization.ClassID - 1);
                                int specBit = ItemTemplate.CalculateItemSpecBit(specialization);
                                itemTemplate.Specializations[0].Set(specBit, true);
                                if (itemSpec.MaxLevel > 40)
                                    itemTemplate.Specializations[1].Set(specBit, true);
                                if (itemSpec.MaxLevel >= 110)
                                    itemTemplate.Specializations[2].Set(specBit, true);
                            }
                        }
                    }
                }

                // Items that have no specializations set can be used by everyone
                foreach (var specs in itemTemplate.Specializations)
                    if (specs.Count == 0)
                        specs.SetAll(true);

                ++sparseCount;
                ItemTemplateStorage.Add(sparse.Id, itemTemplate);
            }

            // Load item effects (spells)
            foreach (var effectEntry in CliDB.ItemEffectStorage.Values)
            {
                var itemTemplate = ItemTemplateStorage.LookupByKey(effectEntry.ParentItemID);
                if (itemTemplate == null)
                    continue;

                itemTemplate.Effects.Add(effectEntry);
            }

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} item templates in {1} ms", sparseCount, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        static float[] qualityMultipliers = new float[]
        {
            0.92f, 0.92f, 0.92f, 1.11f, 1.32f, 1.61f, 0.0f, 0.0f
        };

        static float[] armorMultipliers = new float[]
        {
            0.00f, // INVTYPE_NON_EQUIP
            0.60f, // INVTYPE_HEAD
            0.00f, // INVTYPE_NECK
            0.60f, // INVTYPE_SHOULDERS
            0.00f, // INVTYPE_BODY
            1.00f, // INVTYPE_CHEST
            0.33f, // INVTYPE_WAIST
            0.72f, // INVTYPE_LEGS
            0.48f, // INVTYPE_FEET
            0.33f, // INVTYPE_WRISTS
            0.33f, // INVTYPE_HANDS
            0.00f, // INVTYPE_FINGER
            0.00f, // INVTYPE_TRINKET
            0.00f, // INVTYPE_WEAPON
            0.72f, // INVTYPE_SHIELD
            0.00f, // INVTYPE_RANGED
            0.00f, // INVTYPE_CLOAK
            0.00f, // INVTYPE_2HWEAPON
            0.00f, // INVTYPE_BAG
            0.00f, // INVTYPE_TABARD
            1.00f, // INVTYPE_ROBE
            0.00f, // INVTYPE_WEAPONMAINHAND
            0.00f, // INVTYPE_WEAPONOFFHAND
            0.00f, // INVTYPE_HOLDABLE
            0.00f, // INVTYPE_AMMO
            0.00f, // INVTYPE_THROWN
            0.00f, // INVTYPE_RANGEDRIGHT
            0.00f, // INVTYPE_QUIVER
            0.00f, // INVTYPE_RELIC
        };

        static float[] weaponMultipliers = new float[]
        {
            0.91f, // ITEM_SUBCLASS_WEAPON_AXE
            1.00f, // ITEM_SUBCLASS_WEAPON_AXE2
            1.00f, // ITEM_SUBCLASS_WEAPON_BOW
            1.00f, // ITEM_SUBCLASS_WEAPON_GUN
            0.91f, // ITEM_SUBCLASS_WEAPON_MACE
            1.00f, // ITEM_SUBCLASS_WEAPON_MACE2
            1.00f, // ITEM_SUBCLASS_WEAPON_POLEARM
            0.91f, // ITEM_SUBCLASS_WEAPON_SWORD
            1.00f, // ITEM_SUBCLASS_WEAPON_SWORD2
            1.00f, // ITEM_SUBCLASS_WEAPON_WARGLAIVES
            1.00f, // ITEM_SUBCLASS_WEAPON_STAFF
            0.00f, // ITEM_SUBCLASS_WEAPON_EXOTIC
            0.00f, // ITEM_SUBCLASS_WEAPON_EXOTIC2
            0.66f, // ITEM_SUBCLASS_WEAPON_FIST_WEAPON
            0.00f, // ITEM_SUBCLASS_WEAPON_MISCELLANEOUS
            0.66f, // ITEM_SUBCLASS_WEAPON_DAGGER
            0.00f, // ITEM_SUBCLASS_WEAPON_THROWN
            0.00f, // ITEM_SUBCLASS_WEAPON_SPEAR
            1.00f, // ITEM_SUBCLASS_WEAPON_CROSSBOW
            0.66f, // ITEM_SUBCLASS_WEAPON_WAND
            0.66f, // ITEM_SUBCLASS_WEAPON_FISHING_POLE
        };

        uint FillMaxDurability(ItemClass itemClass, uint itemSubClass, InventoryType inventoryType, ItemQuality quality, uint itemLevel)
        {
            if (itemClass != ItemClass.Armor && itemClass != ItemClass.Weapon)
                return 0;

            float levelPenalty = 1.0f;
            if (itemLevel <= 28)
                levelPenalty = 0.966f - (28u - itemLevel) / 54.0f;

            if (itemClass == ItemClass.Armor)
            {
                if (inventoryType > InventoryType.Robe)
                    return 0;

                return 5 * (uint)(Math.Round(25.0f * qualityMultipliers[(int)quality] * armorMultipliers[(int)inventoryType] * levelPenalty));
            }

            return 5 * (uint)(Math.Round(18.0f * qualityMultipliers[(int)quality] * weaponMultipliers[itemSubClass] * levelPenalty));
        }
        public void LoadItemTemplateAddon()
        {
            var time = Time.GetMSTime();

            uint count = 0;
            SQLResult result = DB.World.Query("SELECT Id, FlagsCu, FoodType, MinMoneyLoot, MaxMoneyLoot, SpellPPMChance, RandomBonusListTemplateId FROM item_template_addon");
            if (!result.IsEmpty())
            {
                do
                {
                    uint itemId = result.Read<uint>(0);
                    ItemTemplate itemTemplate = GetItemTemplate(itemId);
                    if (itemTemplate == null)
                    {
                        Log.outError(LogFilter.Sql, "Item {0} specified in `itemtemplateaddon` does not exist, skipped.", itemId);
                        continue;
                    }

                    uint minMoneyLoot = result.Read<uint>(3);
                    uint maxMoneyLoot = result.Read<uint>(4);
                    if (minMoneyLoot > maxMoneyLoot)
                    {
                        Log.outError(LogFilter.Sql, "Minimum money loot specified in `itemtemplateaddon` for item {0} was greater than maximum amount, swapping.", itemId);
                        uint temp = minMoneyLoot;
                        minMoneyLoot = maxMoneyLoot;
                        maxMoneyLoot = temp;
                    }
                    itemTemplate.FlagsCu = (ItemFlagsCustom)result.Read<uint>(1);
                    itemTemplate.FoodType = result.Read<uint>(2);
                    itemTemplate.MinMoneyLoot = minMoneyLoot;
                    itemTemplate.MaxMoneyLoot = maxMoneyLoot;
                    itemTemplate.SpellPPMRate = result.Read<float>(5);
                    itemTemplate.RandomBonusListTemplateId = result.Read<uint>(6);
                    ++count;
                } while (result.NextRow());
            }

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} item addon templates in {1} ms", count, Time.GetMSTimeDiffToNow(time));
        }
        public void LoadItemScriptNames()
        {
            uint oldMSTime = Time.GetMSTime();
            uint count = 0;

            SQLResult result = DB.World.Query("SELECT Id, ScriptName FROM item_script_names");
            if (!result.IsEmpty())
            {
                do
                {
                    uint itemId = result.Read<uint>(0);
                    if (GetItemTemplate(itemId) == null)
                    {
                        Log.outError(LogFilter.Sql, "Item {0} specified in `item_script_names` does not exist, skipped.", itemId);
                        continue;
                    }

                    ItemTemplateStorage[itemId].ScriptId = GetScriptId(result.Read<string>(1));
                    ++count;
                } while (result.NextRow());
            }

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} item script names in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        public ItemTemplate GetItemTemplate(uint ItemId)
        {
            return ItemTemplateStorage.LookupByKey(ItemId);
        }
        public Dictionary<uint, ItemTemplate> GetItemTemplates()
        {
            return ItemTemplateStorage;
        }
        public Trainer GetTrainer(uint trainerId)
        {
            return _trainers.LookupByKey(trainerId);
        }
        public void AddVendorItem(uint entry, VendorItem vItem, bool persist = true)
        {
            VendorItemData vList = cacheVendorItemStorage[entry];
            vList.AddItem(vItem);

            if (persist)
            {
                PreparedStatement stmt = DB.World.GetPreparedStatement(WorldStatements.INS_NPC_VENDOR);

                stmt.AddValue(0, entry);
                stmt.AddValue(1, vItem.item);
                stmt.AddValue(2, vItem.maxcount);
                stmt.AddValue(3, vItem.incrtime);
                stmt.AddValue(4, vItem.ExtendedCost);
                stmt.AddValue(5, (byte)vItem.Type);

                DB.World.Execute(stmt);
            }
        }
        public bool RemoveVendorItem(uint entry, uint item, ItemVendorType type, bool persist = true)
        {
            var iter = cacheVendorItemStorage.LookupByKey(entry);
            if (iter == null)
                return false;

            if (!iter.RemoveItem(item, type))
                return false;

            if (persist)
            {
                PreparedStatement stmt = DB.World.GetPreparedStatement(WorldStatements.DEL_NPC_VENDOR);

                stmt.AddValue(0, entry);
                stmt.AddValue(1, item);
                stmt.AddValue(2, (byte)type);

                DB.World.Execute(stmt);
            }

            return true;
        }
        public bool IsVendorItemValid(uint vendorentry, VendorItem vItem, Player player = null, List<uint> skipvendors = null, ulong ORnpcflag = 0)
        {
            CreatureTemplate cInfo = GetCreatureTemplate(vendorentry);
            if (cInfo == null)
            {
                if (player != null)
                    player.SendSysMessage(CypherStrings.CommandVendorselection);
                else
                    Log.outError(LogFilter.Sql, "Table `(gameevent)npcvendor` have data for not existed creature template (Entry: {0}), ignore", vendorentry);
                return false;
            }

            if (!Convert.ToBoolean(((ulong)cInfo.Npcflag | ORnpcflag) & (ulong)NPCFlags.Vendor))
            {
                if (skipvendors == null || skipvendors.Count == 0)
                {
                    if (player != null)
                        player.SendSysMessage(CypherStrings.CommandVendorselection);
                    else
                        Log.outError(LogFilter.Sql, "Table `(gameevent)npcvendor` have data for not creature template (Entry: {0}) without vendor flag, ignore", vendorentry);

                    if (skipvendors != null)
                        skipvendors.Add(vendorentry);
                }
                return false;
            }

            if ((vItem.Type == ItemVendorType.Item && GetItemTemplate(vItem.item) == null) ||
                (vItem.Type == ItemVendorType.Currency && CliDB.CurrencyTypesStorage.LookupByKey(vItem.item) == null))
            {
                if (player != null)
                    player.SendSysMessage(CypherStrings.ItemNotFound, vItem.item, vItem.Type);
                else
                    Log.outError(LogFilter.Sql, "Table `(gameevent)npcvendor` for Vendor (Entry: {0}) have in item list non-existed item ({1}, type {2}), ignore", vendorentry, vItem.item, vItem.Type);
                return false;
            }

            if (vItem.PlayerConditionId != 0 && !CliDB.PlayerConditionStorage.ContainsKey(vItem.PlayerConditionId))
            {
                Log.outError(LogFilter.Sql, "Table `(game_event_)npc_vendor` has Item (Entry: {0}) with invalid PlayerConditionId ({1}) for vendor ({2}), ignore", vItem.item, vItem.PlayerConditionId, vendorentry);
                return false;
            }

            if (vItem.ExtendedCost != 0 && !CliDB.ItemExtendedCostStorage.ContainsKey(vItem.ExtendedCost))
            {
                if (player != null)
                    player.SendSysMessage(CypherStrings.ExtendedCostNotExist, vItem.ExtendedCost);
                else
                    Log.outError(LogFilter.Sql, "Table `(gameevent)npcvendor` have Item (Entry: {0}) with wrong ExtendedCost ({1}) for vendor ({2}), ignore", vItem.item, vItem.ExtendedCost, vendorentry);
                return false;
            }

            if (vItem.Type == ItemVendorType.Item) // not applicable to currencies
            {
                if (vItem.maxcount > 0 && vItem.incrtime == 0)
                {
                    if (player != null)
                        player.SendSysMessage("MaxCount != 0 ({0}) but IncrTime == 0", vItem.maxcount);
                    else
                        Log.outError(LogFilter.Sql, "Table `(gameevent)npcvendor` has `maxcount` ({0}) for item {1} of vendor (Entry: {2}) but `incrtime`=0, ignore", vItem.maxcount, vItem.item, vendorentry);
                    return false;
                }
                else if (vItem.maxcount == 0 && vItem.incrtime > 0)
                {
                    if (player != null)
                        player.SendSysMessage("MaxCount == 0 but IncrTime<>= 0");
                    else
                        Log.outError(LogFilter.Sql, "Table `(gameevent)npcvendor` has `maxcount`=0 for item {0} of vendor (Entry: {0}) but `incrtime`<>0, ignore", vItem.item, vendorentry);
                    return false;
                }

                foreach (uint bonusList in vItem.BonusListIDs)
                {
                    if (Global.DB2Mgr.GetItemBonusList(bonusList) == null)
                    {
                        Log.outError(LogFilter.Sql, "Table `(game_event_)npc_vendor` have Item (Entry: {0}) with invalid bonus {1} for vendor ({2}), ignore", vItem.item, bonusList, vendorentry);
                        return false;
                    }
                }
            }

            VendorItemData vItems = GetNpcVendorItemList(vendorentry);
            if (vItems == null)
                return true;                                        // later checks for non-empty lists

            if (vItems.FindItemCostPair(vItem.item, vItem.ExtendedCost, vItem.Type) != null)
            {
                if (player != null)
                    player.SendSysMessage(CypherStrings.ItemAlreadyInList, vItem.item, vItem.ExtendedCost, vItem.Type);
                else
                    Log.outError(LogFilter.Sql, "Table `npcvendor` has duplicate items {0} (with extended cost {1}, type {2}) for vendor (Entry: {3}), ignoring", vItem.item, vItem.ExtendedCost, vItem.Type, vendorentry);
                return false;
            }

            if (vItem.Type == ItemVendorType.Currency && vItem.maxcount == 0)
            {
                Log.outError(LogFilter.Sql, "Table `(game_event_)npc_vendor` have Item (Entry: {0}, type: {1}) with missing maxcount for vendor ({2}), ignore", vItem.item, vItem.Type, vendorentry);
                return false;
            }

            return true;
        }
        public VendorItemData GetNpcVendorItemList(uint entry)
        {
            return cacheVendorItemStorage.LookupByKey(entry);
        }
        public EquipmentInfo GetEquipmentInfo(uint entry, int id)
        {
            var equip = equipmentInfoStorage.LookupByKey(entry);
            if (equip.Empty())
                return null;

            if (id == -1)
                return equip[RandomHelper.IRand(0, equip.Count - 1)].Item2;
            else
                return equip.Find(p => p.Item1 == id)?.Item2;
        }

        //Maps
        public void LoadInstanceTemplate()
        {
            var time = Time.GetMSTime();
            //                                          0     1       2        4
            SQLResult result = DB.World.Query("SELECT map, parent, script, allowMount FROM instance_template");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 instance templates. DB table `instance_template` is empty!");
                return;
            }

            uint count = 0;
            do
            {
                uint mapID = result.Read<uint>(0);

                if (!Global.MapMgr.IsValidMAP(mapID, true))
                {
                    Log.outError(LogFilter.Sql, "ObjectMgr.LoadInstanceTemplate: bad mapid {0} for template!", mapID);
                    continue;
                }

                var instanceTemplate = new InstanceTemplate();
                instanceTemplate.AllowMount = result.Read<bool>(3);
                instanceTemplate.Parent = result.Read<uint>(1);
                instanceTemplate.ScriptId = Global.ObjectMgr.GetScriptId(result.Read<string>(2));

                instanceTemplateStorage.Add(mapID, instanceTemplate);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} instance templates in {1} ms", count, Time.GetMSTimeDiffToNow(time));
        }
        public void LoadGameTele()
        {
            uint oldMSTime = Time.GetMSTime();

            gameTeleStorage.Clear();

            //                                          0       1           2           3           4        5     6
            SQLResult result = DB.World.Query("SELECT id, position_x, position_y, position_z, orientation, map, name FROM game_tele");

            if (result.IsEmpty())
            {
                Log.outError(LogFilter.ServerLoading, "Loaded 0 GameTeleports. DB table `game_tele` is empty!");
                return;
            }

            uint count = 0;

            do
            {
                uint id = result.Read<uint>(0);

                GameTele gt = new();

                gt.posX = result.Read<float>(1);
                gt.posY = result.Read<float>(2);
                gt.posZ = result.Read<float>(3);
                gt.orientation = result.Read<float>(4);
                gt.mapId = result.Read<uint>(5);
                gt.name = result.Read<string>(6);

                gt.nameLow = gt.name.ToLowerInvariant();

                if (!GridDefines.IsValidMapCoord(gt.mapId, gt.posX, gt.posY, gt.posZ, gt.orientation))
                {
                    Log.outError(LogFilter.Sql, "Wrong position for id {0} (name: {1}) in `game_tele` table, ignoring.", id, gt.name);
                    continue;
                }
                gameTeleStorage.Add(id, gt);
                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} GameTeleports in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadAreaTriggerTeleports()
        {
            uint oldMSTime = Time.GetMSTime();

            _areaTriggerStorage.Clear();                                  // need for reload case

            //                                         0   1
            SQLResult result = DB.World.Query("SELECT ID, PortLocID FROM areatrigger_teleport");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 area trigger teleport definitions. DB table `areatrigger_teleport` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                ++count;

                uint Trigger_ID = result.Read<uint>(0);
                uint PortLocID = result.Read<uint>(1);

                WorldSafeLocsEntry portLoc = GetWorldSafeLoc(PortLocID);
                if (portLoc == null)
                {
                    Log.outError(LogFilter.Sql, "Area Trigger (ID: {0}) has a non-existing Port Loc (ID: {1}) in WorldSafeLocs.dbc, skipped", Trigger_ID, PortLocID);
                    continue;
                }

                AreaTriggerStruct at = new();
                at.target_mapId = portLoc.Loc.GetMapId();
                at.target_X = portLoc.Loc.GetPositionX();
                at.target_Y = portLoc.Loc.GetPositionY();
                at.target_Z = portLoc.Loc.GetPositionZ();
                at.target_Orientation = portLoc.Loc.GetOrientation();
                at.PortLocId = portLoc.Id;

                AreaTriggerRecord atEntry = CliDB.AreaTriggerStorage.LookupByKey(Trigger_ID);
                if (atEntry == null)
                {
                    Log.outError(LogFilter.Sql, "Area trigger (ID: {0}) does not exist in `AreaTrigger.dbc`.", Trigger_ID);
                    continue;
                }

                _areaTriggerStorage[Trigger_ID] = at;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} area trigger teleport definitions in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadAccessRequirements()
        {
            uint oldMSTime = Time.GetMSTime();

            _accessRequirementStorage.Clear();

            //                                          0      1           2          3          4           5      6             7             8                      9     
            SQLResult result = DB.World.Query("SELECT mapid, difficulty, level_min, level_max, item, item2, quest_done_A, quest_done_H, completed_achievement, quest_failed_text FROM access_requirement");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 access requirement definitions. DB table `access_requirement` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint mapid = result.Read<uint>(0);
                if (!CliDB.MapStorage.ContainsKey(mapid))
                {
                    Log.outError(LogFilter.Sql, "Map {0} referenced in `access_requirement` does not exist, skipped.", mapid);
                    continue;
                }

                uint difficulty = result.Read<uint>(1);
                if (Global.DB2Mgr.GetMapDifficultyData(mapid, (Difficulty)difficulty) == null)
                {
                    Log.outError(LogFilter.Sql, "Map {0} referenced in `access_requirement` does not have difficulty {1}, skipped", mapid, difficulty);
                    continue;
                }

                ulong requirementId = MathFunctions.MakePair64(mapid, difficulty);

                AccessRequirement ar = new();
                ar.levelMin = result.Read<byte>(2);
                ar.levelMax = result.Read<byte>(3);
                ar.item = result.Read<uint>(4);
                ar.item2 = result.Read<uint>(5);
                ar.quest_A = result.Read<uint>(6);
                ar.quest_H = result.Read<uint>(7);
                ar.achievement = result.Read<uint>(8);
                ar.questFailedText = result.Read<string>(9);

                if (ar.item != 0)
                {
                    ItemTemplate pProto = GetItemTemplate(ar.item);
                    if (pProto == null)
                    {
                        Log.outError(LogFilter.Sql, "Key item {0} does not exist for map {1} difficulty {2}, removing key requirement.", ar.item, mapid, difficulty);
                        ar.item = 0;
                    }
                }

                if (ar.item2 != 0)
                {
                    ItemTemplate pProto = GetItemTemplate(ar.item2);
                    if (pProto == null)
                    {
                        Log.outError(LogFilter.Sql, "Second item {0} does not exist for map {1} difficulty {2}, removing key requirement.", ar.item2, mapid, difficulty);
                        ar.item2 = 0;
                    }
                }

                if (ar.quest_A != 0)
                {
                    if (GetQuestTemplate(ar.quest_A) == null)
                    {
                        Log.outError(LogFilter.Sql, "Required Alliance Quest {0} not exist for map {1} difficulty {2}, remove quest done requirement.", ar.quest_A, mapid, difficulty);
                        ar.quest_A = 0;
                    }
                }

                if (ar.quest_H != 0)
                {
                    if (GetQuestTemplate(ar.quest_H) == null)
                    {
                        Log.outError(LogFilter.Sql, "Required Horde Quest {0} not exist for map {1} difficulty {2}, remove quest done requirement.", ar.quest_H, mapid, difficulty);
                        ar.quest_H = 0;
                    }
                }

                if (ar.achievement != 0)
                {
                    if (!CliDB.AchievementStorage.ContainsKey(ar.achievement))
                    {
                        Log.outError(LogFilter.Sql, "Required Achievement {0} not exist for map {1} difficulty {2}, remove quest done requirement.", ar.achievement, mapid, difficulty);
                        ar.achievement = 0;
                    }
                }

                _accessRequirementStorage[requirementId] = ar;
                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} access requirement definitions in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadInstanceEncounters()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                           0         1            2                3
            SQLResult result = DB.World.Query("SELECT entry, creditType, creditEntry, lastEncounterDungeon FROM instance_encounters");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 instance encounters, table is empty!");
                return;
            }

            uint count = 0;
            Dictionary<uint, Tuple<uint, DungeonEncounterRecord>> dungeonLastBosses = new();
            do
            {
                uint entry = result.Read<uint>(0);
                EncounterCreditType creditType = (EncounterCreditType)result.Read<byte>(1);
                uint creditEntry = result.Read<uint>(2);
                uint lastEncounterDungeon = result.Read<uint>(3);
                DungeonEncounterRecord dungeonEncounter = CliDB.DungeonEncounterStorage.LookupByKey(entry);
                if (dungeonEncounter == null)
                {
                    Log.outError(LogFilter.Sql, "Table `instance_encounters` has an invalid encounter id {0}, skipped!", entry);
                    continue;
                }

                if (lastEncounterDungeon != 0 && Global.LFGMgr.GetLFGDungeonEntry(lastEncounterDungeon) == 0)
                {
                    Log.outError(LogFilter.Sql, "Table `instance_encounters` has an encounter {0} ({1}) marked as final for invalid dungeon id {2}, skipped!",
                        entry, dungeonEncounter.Name[Global.WorldMgr.GetDefaultDbcLocale()], lastEncounterDungeon);
                    continue;
                }

                var pair = dungeonLastBosses.LookupByKey(lastEncounterDungeon);
                if (lastEncounterDungeon != 0)
                {
                    if (pair != null)
                    {
                        Log.outError(LogFilter.Sql, "Table `instance_encounters` specified encounter {0} ({1}) as last encounter but {2} ({3}) is already marked as one, skipped!",
                            entry, dungeonEncounter.Name[Global.WorldMgr.GetDefaultDbcLocale()], pair.Item1, pair.Item2.Name[Global.WorldMgr.GetDefaultDbcLocale()]);
                        continue;
                    }

                    dungeonLastBosses[lastEncounterDungeon] = Tuple.Create(entry, dungeonEncounter);
                }

                switch (creditType)
                {
                    case EncounterCreditType.KillCreature:
                        {
                            CreatureTemplate creatureInfo = GetCreatureTemplate(creditEntry);
                            if (creatureInfo == null)
                            {
                                Log.outError(LogFilter.Sql, "Table `instance_encounters` has an invalid creature (entry {0}) linked to the encounter {1} ({2}), skipped!",
                                    creditEntry, entry, dungeonEncounter.Name[Global.WorldMgr.GetDefaultDbcLocale()]);
                                continue;
                            }
                            creatureInfo.FlagsExtra |= CreatureFlagsExtra.DungeonBoss;
                            for (byte diff = 0; diff < SharedConst.MaxCreatureDifficulties; ++diff)
                            {
                                uint diffEntry = creatureInfo.DifficultyEntry[diff];
                                if (diffEntry != 0)
                                {
                                    CreatureTemplate diffInfo = GetCreatureTemplate(diffEntry);
                                    if (diffInfo != null)
                                        diffInfo.FlagsExtra |= CreatureFlagsExtra.DungeonBoss;
                                }
                        }
                        break;
                        }
                    case EncounterCreditType.CastSpell:
                        if (!Global.SpellMgr.HasSpellInfo(creditEntry, Difficulty.None))
                        {
                            Log.outError(LogFilter.Sql, "Table `instance_encounters` has an invalid spell (entry {0}) linked to the encounter {1} ({2}), skipped!",
                                creditEntry, entry, dungeonEncounter.Name[Global.WorldMgr.GetDefaultDbcLocale()]);
                            continue;
                        }
                        break;
                    default:
                        Log.outError(LogFilter.Sql, "Table `instance_encounters` has an invalid credit type ({0}) for encounter {1} ({2}), skipped!",
                            creditType, entry, dungeonEncounter.Name[Global.WorldMgr.GetDefaultDbcLocale()]);
                        continue;
                }

                if (dungeonEncounter.DifficultyID == 0)
                {
                    foreach (var difficulty in CliDB.DifficultyStorage.Values)
                    {
                        if (Global.DB2Mgr.GetMapDifficultyData((uint)dungeonEncounter.MapID, (Difficulty)difficulty.Id) != null)
                            _dungeonEncounterStorage.Add(MathFunctions.MakePair64((uint)dungeonEncounter.MapID, difficulty.Id), new DungeonEncounter(dungeonEncounter, creditType, creditEntry, lastEncounterDungeon));
                    }
                }
                else
                    _dungeonEncounterStorage.Add(MathFunctions.MakePair64((uint)dungeonEncounter.MapID, (uint)dungeonEncounter.DifficultyID), new DungeonEncounter(dungeonEncounter, creditType, creditEntry, lastEncounterDungeon));

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} instance encounters in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadSpawnGroupTemplates()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0        1          2
            SQLResult result = DB.World.Query("SELECT groupId, groupName, groupFlags FROM spawn_group_template");
            if (!result.IsEmpty())
            {
                do
                {
                    uint groupId = result.Read<uint>(0);
                    SpawnGroupTemplateData group = new();
                    group.groupId = groupId;
                    group.name = result.Read<string>(1);
                    group.mapId = 0xFFFFFFFF;
                    SpawnGroupFlags flags = (SpawnGroupFlags)result.Read<uint>(2);
                    if (flags.HasAnyFlag(~SpawnGroupFlags.All))
                    {
                        flags &= SpawnGroupFlags.All;
                        Log.outError(LogFilter.Sql, $"Invalid spawn group flag {flags} on group ID {groupId} ({group.name}), reduced to valid flag {group.flags}.");
                    }
                    if (flags.HasAnyFlag(SpawnGroupFlags.System) && flags.HasAnyFlag(SpawnGroupFlags.ManualSpawn))
                    {
                        flags &= ~SpawnGroupFlags.ManualSpawn;
                        Log.outError(LogFilter.Sql, $"System spawn group {groupId} ({group.name}) has invalid manual spawn flag. Ignored.");
                    }
                    group.flags = flags;

                    _spawnGroupDataStorage[groupId] = group;
                } while (result.NextRow());
            }

            if (!_spawnGroupDataStorage.ContainsKey(0))
            {
                Log.outError(LogFilter.Sql, "Default spawn group (index 0) is missing from DB! Manually inserted.");
                SpawnGroupTemplateData data = new();
                data.groupId = 0;
                data.name = "Default Group";
                data.mapId = 0;
                data.flags = SpawnGroupFlags.System;
                _spawnGroupDataStorage[0] = data;
            }

            if (!_spawnGroupDataStorage.ContainsKey(1))
            {

                Log.outError(LogFilter.Sql, "Default legacy spawn group (index 1) is missing from DB! Manually inserted.");
                SpawnGroupTemplateData data = new();
                data.groupId = 1;
                data.name = "Legacy Group";
                data.mapId = 0;
                data.flags = SpawnGroupFlags.System | SpawnGroupFlags.CompatibilityMode;
                _spawnGroupDataStorage[1] = data;
            }

            if (!result.IsEmpty())
                Log.outInfo(LogFilter.ServerLoading, $"Loaded {_spawnGroupDataStorage.Count} spawn group templates in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
            else
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 spawn group templates. DB table `spawn_group_template` is empty.");
        }
        public void LoadSpawnGroups()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0        1          2
            SQLResult result = DB.World.Query("SELECT groupId, spawnType, spawnId FROM spawn_group");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 spawn group members. DB table `spawn_group` is empty.");
                return;
            }

            uint numMembers = 0;
            do
            {
                uint groupId = result.Read<uint>(0);
                SpawnObjectType spawnType = (SpawnObjectType)result.Read<byte>(1);
                if (spawnType >= SpawnObjectType.Max)
                {
                    Log.outError(LogFilter.Sql, $"Spawn data with invalid type {spawnType} listed for spawn group {groupId}. Skipped.");
                    continue;
                }
                ulong spawnId = result.Read<ulong>(2);

                SpawnData data = GetSpawnData(spawnType, spawnId);
                if (data == null)
                {
                    Log.outError(LogFilter.Sql, $"Spawn data with ID ({spawnType},{spawnId}) not found, but is listed as a member of spawn group {groupId}!");
                    continue;
                }
                else if (data.spawnGroupData.groupId != 0)
                {
                    Log.outError(LogFilter.Sql, $"Spawn with ID ({spawnType},{spawnId}) is listed as a member of spawn group {groupId}, but is already a member of spawn group {data.spawnGroupData.groupId}. Skipping.");
                    continue;
                }
                var groupTemplate = _spawnGroupDataStorage.LookupByKey(groupId);
                if (groupTemplate == null)
                {
                    Log.outError(LogFilter.Sql, $"Spawn group {groupId} assigned to spawn ID ({spawnType},{spawnId}), but group is found!");
                    continue;
                }
                else
                {
                    if (groupTemplate.mapId == 0xFFFFFFFF)
                        groupTemplate.mapId = data.spawnPoint.GetMapId();
                    else if (groupTemplate.mapId != data.spawnPoint.GetMapId() && !groupTemplate.flags.HasAnyFlag(SpawnGroupFlags.System))
                    {
                        Log.outError(LogFilter.Sql, $"Spawn group {groupId} has map ID {groupTemplate.mapId}, but spawn ({spawnType},{spawnId}) has map id {data.spawnPoint.GetMapId()} - spawn NOT added to group!");
                        continue;
                    }
                    data.spawnGroupData = groupTemplate;
                    if (!groupTemplate.flags.HasAnyFlag(SpawnGroupFlags.System))
                        _spawnGroupMapStorage.Add(groupId, data);
                    ++numMembers;
                }
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {numMembers} spawn group members in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }
        public void LoadInstanceSpawnGroups()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0              1            2           3             4
            SQLResult result = DB.World.Query("SELECT instanceMapId, bossStateId, bossStates, spawnGroupId, flags FROM instance_spawn_groups");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 instance spawn groups. DB table `instance_spawn_groups` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                ushort instanceMapId = result.Read<ushort>(0);
                uint spawnGroupId = result.Read<uint>(3);

                var spawnGroupTemplate = _spawnGroupDataStorage.LookupByKey(spawnGroupId);
                if (spawnGroupTemplate == null || spawnGroupTemplate.flags.HasAnyFlag(SpawnGroupFlags.System))
                {
                    Log.outError(LogFilter.Sql, $"Invalid spawn group {spawnGroupId} specified for instance {instanceMapId}. Skipped.");
                    continue;
                }

                InstanceSpawnGroupInfo info = new();
                info.SpawnGroupId = spawnGroupId;
                info.BossStateId = result.Read<byte>(1);

                byte ALL_STATES = (1 << (int)EncounterState.ToBeDecided) - 1;
                byte states = result.Read<byte>(2);
                if ((states & ~ALL_STATES) != 0)
                {
                    info.BossStates = (byte)(states & ALL_STATES);
                    Log.outError(LogFilter.Sql, $"Instance spawn group ({instanceMapId},{spawnGroupId}) had invalid boss state mask {states} - truncated to {info.BossStates}.");
                }
                else
                    info.BossStates = states;

                InstanceSpawnGroupFlags flags = (InstanceSpawnGroupFlags)result.Read<byte>(4);
                if ((flags & ~InstanceSpawnGroupFlags.All) != 0)
                {
                    info.Flags = flags & InstanceSpawnGroupFlags.All;
                    Log.outError(LogFilter.Sql, $"Instance spawn group ({instanceMapId},{spawnGroupId}) had invalid flags {flags} - truncated to {info.Flags}.");
                }
                else
                    info.Flags = flags;

                _instanceSpawnGroupStorage.Add(instanceMapId, info);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} instance spawn groups in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }
        void OnDeleteSpawnData(SpawnData data)
        {
            var templateIt = _spawnGroupDataStorage.LookupByKey(data.spawnGroupData.groupId);
            Cypher.Assert(templateIt != null, $"Creature data for ({data.type},{data.spawnId}) is being deleted and has invalid spawn group index {data.spawnGroupData.groupId}!");

            if (templateIt.flags.HasAnyFlag(SpawnGroupFlags.System)) // system groups don't store their members in the map
                return;

            var spawnDatas = _spawnGroupMapStorage.LookupByKey(data.spawnGroupData.groupId);
            foreach (var it in spawnDatas)
            {
                if (it != data)
                    continue;
                _spawnGroupMapStorage.Remove(data.spawnGroupData.groupId, it);
                return;
            }

            Cypher.Assert(false, $"Spawn data ({data.type},{data.spawnId}) being removed is member of spawn group {data.spawnGroupData.groupId}, but not actually listed in the lookup table for that group!");
        }

        public InstanceTemplate GetInstanceTemplate(uint mapID)
        {
            return instanceTemplateStorage.LookupByKey(mapID);
        }
        public GameTele GetGameTele(uint id)
        {
            return gameTeleStorage.LookupByKey(id);
        }
        public GameTele GetGameTele(string name)
        {
            return gameTeleStorage.Values.FirstOrDefault(p => p.nameLow == name.ToLower());
        }
        public bool AddGameTele(GameTele tele)
        {
            // find max id
            uint newId = 0;
            foreach (var itr in gameTeleStorage)
                if (itr.Key > newId)
                    newId = itr.Key;

            // use next
            ++newId;

            gameTeleStorage[newId] = tele;

            PreparedStatement stmt = DB.World.GetPreparedStatement(WorldStatements.INS_GAME_TELE);

            stmt.AddValue(0, newId);
            stmt.AddValue(1, tele.posX);
            stmt.AddValue(2, tele.posY);
            stmt.AddValue(3, tele.posZ);
            stmt.AddValue(4, tele.orientation);
            stmt.AddValue(5, tele.mapId);
            stmt.AddValue(6, tele.name);

            DB.World.Execute(stmt);

            return true;
        }
        public bool DeleteGameTele(string name)
        {
            name = name.ToLowerInvariant();

            foreach (var pair in gameTeleStorage.ToList())
            {
                if (pair.Value.nameLow == name)
                {
                    PreparedStatement stmt = DB.World.GetPreparedStatement(WorldStatements.DEL_GAME_TELE);
                    stmt.AddValue(0, pair.Value.name);
                    DB.World.Execute(stmt);

                    gameTeleStorage.Remove(pair.Key);
                    return true;
                }
            }

            return false;
        }
        public List<DungeonEncounter> GetDungeonEncounterList(uint mapId, Difficulty difficulty)
        {
            return _dungeonEncounterStorage.LookupByKey(MathFunctions.MakePair64(mapId, (uint)difficulty));
        }
        public bool IsTransportMap(uint mapId) { return _transportMaps.Contains((ushort)mapId); }
        public SpawnGroupTemplateData GetSpawnGroupData(uint groupId) { return _spawnGroupDataStorage.LookupByKey(groupId); }
        public SpawnGroupTemplateData GetDefaultSpawnGroup() { return _spawnGroupDataStorage.ElementAt(0).Value; }
        public SpawnGroupTemplateData GetLegacySpawnGroup() { return _spawnGroupDataStorage.ElementAt(1).Value; }
        public List<SpawnData> GetSpawnDataForGroup(uint groupId) { return _spawnGroupMapStorage.LookupByKey(groupId); }
        public SpawnData GetSpawnData(SpawnObjectType type, ulong guid)
        {
            if (type == SpawnObjectType.Creature)
                return GetCreatureData(guid);
            else if (type == SpawnObjectType.GameObject)
                return GetGameObjectData(guid);
            else
                Cypher.Assert(false, $"Invalid spawn object type {type}");

            return null;
        }
        public List<InstanceSpawnGroupInfo> GetSpawnGroupsForInstance(uint instanceId) { return _instanceSpawnGroupStorage.LookupByKey(instanceId); }
        
        //Player
        public void LoadPlayerInfo()
        {
            for (uint race = 0; race < (int)Race.Max; ++race)
                _playerInfo[race] = new PlayerInfo[(int)Class.Max];

            var time = Time.GetMSTime();
            // Load playercreate
            {
                //                                         0     1      2    3     4           5           6           7
                SQLResult result = DB.World.Query("SELECT race, class, map, zone, position_x, position_y, position_z, orientation FROM playercreateinfo");

                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 player create definitions. DB table `playercreateinfo` is empty.");
                    return;
                }

                uint count = 0;
                do
                {
                    uint currentrace = result.Read<uint>(0);
                    uint currentclass = result.Read<uint>(1);
                    uint mapId = result.Read<uint>(2);
                    uint zoneId = result.Read<uint>(3);
                    float positionX = result.Read<float>(4);
                    float positionY = result.Read<float>(5);
                    float positionZ = result.Read<float>(6);
                    float orientation = result.Read<float>(7);

                    if (!CliDB.ChrRacesStorage.ContainsKey(currentrace))
                    {
                        Log.outError(LogFilter.Sql, $"Wrong race {currentrace} in `playercreateinfo` table, ignoring.");
                        continue;
                    }

                    if (!CliDB.ChrClassesStorage.ContainsKey(currentclass))
                    {
                        Log.outError(LogFilter.Sql, $"Wrong class {currentclass} in `playercreateinfo` table, ignoring.");
                        continue;
                    }

                    // accept DB data only for valid position (and non instanceable)
                    if (!GridDefines.IsValidMapCoord(mapId, positionX, positionY, positionZ, orientation))
                    {
                        Log.outError(LogFilter.Sql, $"Wrong home position for class {currentclass} race {currentrace} pair in `playercreateinfo` table, ignoring.");
                        continue;
                    }

                    if (CliDB.MapStorage.LookupByKey(mapId).Instanceable())
                    {
                        Log.outError(LogFilter.Sql, $"Home position in instanceable map for class {currentclass} race {currentrace} pair in `playercreateinfo` table, ignoring.");
                        continue;
                    }

                    ChrModelRecord maleModel = Global.DB2Mgr.GetChrModel((Race)currentrace, Gender.Male);
                    if (maleModel == null)
                    {
                        Log.outError(LogFilter.Sql, $"Missing male model for race {currentrace}, ignoring.");
                        continue;
                    }

                    ChrModelRecord femaleModel = Global.DB2Mgr.GetChrModel((Race)currentrace, Gender.Female);
                    if (femaleModel == null)
                    {
                        Log.outError(LogFilter.Sql, $"Missing female model for race {currentrace}, ignoring.");
                        continue;
                    }

                    PlayerInfo pInfo = new();
                    pInfo.MapId = mapId;
                    pInfo.ZoneId = zoneId;
                    pInfo.PositionX = positionX;
                    pInfo.PositionY = positionY;
                    pInfo.PositionZ = positionZ;
                    pInfo.Orientation = orientation;

                    pInfo.DisplayId_m = maleModel.DisplayID;
                    pInfo.DisplayId_f = femaleModel.DisplayID;

                    _playerInfo[currentrace][currentclass] = pInfo;

                    ++count;
                } while (result.NextRow());

                Log.outInfo(LogFilter.ServerLoading, "Loaded {0} player create definitions in {1} ms", count, Time.GetMSTimeDiffToNow(time));
            }
            time = Time.GetMSTime();
            // Load playercreate items
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Items Data...");
            {
                MultiMap<uint, ItemTemplate> itemsByCharacterLoadout = new();
                foreach (CharacterLoadoutItemRecord characterLoadoutItem in CliDB.CharacterLoadoutItemStorage.Values)
                {
                    ItemTemplate itemTemplate = GetItemTemplate(characterLoadoutItem.ItemID);
                    if (itemTemplate != null)
                        itemsByCharacterLoadout.Add(characterLoadoutItem.CharacterLoadoutID, itemTemplate);
                }

                foreach (CharacterLoadoutRecord characterLoadout in CliDB.CharacterLoadoutStorage.Values)
                {
                    if (!characterLoadout.IsForNewCharacter())
                        continue;

                    var items = itemsByCharacterLoadout.LookupByKey(characterLoadout.Id);
                    if (items.Empty())
                        continue;

                    for (var raceIndex = Race.Human; raceIndex < Race.Max; ++raceIndex)
                    {
                        if (!characterLoadout.RaceMask.HasAnyFlag(SharedConst.GetMaskForRace(raceIndex)))
                            continue;

                        PlayerInfo playerInfo = _playerInfo[(int)raceIndex][characterLoadout.ChrClassID];
                        if (playerInfo != null)
                        {
                            foreach (ItemTemplate itemTemplate in items)
                            {
                                // BuyCount by default
                                uint count = itemTemplate.GetBuyCount();

                                // special amount for food/drink
                                if (itemTemplate.GetClass() == ItemClass.Consumable && (ItemSubClassConsumable)itemTemplate.GetSubClass() == ItemSubClassConsumable.FoodDrink)
                                {
                                    if (!itemTemplate.Effects.Empty())
                                    {
                                        switch ((SpellCategories)itemTemplate.Effects[0].SpellCategoryID)
                                        {
                                            case SpellCategories.Food:                                // food
                                                count = characterLoadout.ChrClassID == (int)Class.Deathknight ? 10 : 4u;
                                                break;
                                            case SpellCategories.Drink:                                // drink
                                                count = 2;
                                                break;
                                        }
                                    }
                                    if (itemTemplate.GetMaxStackSize() < count)
                                        count = itemTemplate.GetMaxStackSize();
                                }

                                playerInfo.item.Add(new PlayerCreateInfoItem(itemTemplate.GetId(), count));
                            }
                        }
                    }
                }
            }            
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Items Override Data...");
            {
                //                                         0     1      2       3
                SQLResult result = DB.World.Query("SELECT race, class, itemid, amount FROM playercreateinfo_item");

                if (result.IsEmpty())
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 custom player create items. DB table `playercreateinfo_item` is empty.");
                else
                {
                    uint count = 0;
                    do
                    {
                        uint currentrace = result.Read<uint>(0);
                        if (currentrace >= (int)Race.Max)
                        {
                            Log.outError(LogFilter.Sql, "Wrong race {0} in `playercreateinfo_item` table, ignoring.", currentrace);
                            continue;
                        }

                        uint currentclass = result.Read<uint>(1);
                        if (currentclass >= (int)Class.Max)
                        {
                            Log.outError(LogFilter.Sql, "Wrong class {0} in `playercreateinfo_item` table, ignoring.", currentclass);
                            continue;
                        }

                        uint itemid = result.Read<uint>(2);
                        if (GetItemTemplate(itemid).GetId() == 0)
                        {
                            Log.outError(LogFilter.Sql, "Item id {0} (race {1} class {2}) in `playercreateinfo_item` table but not listed in `itemtemplate`, ignoring.", itemid, currentrace, currentclass);
                            continue;
                        }

                        int amount = result.Read<int>(3);

                        if (amount == 0)
                        {
                            Log.outError(LogFilter.Sql, "Item id {0} (class {1} race {2}) have amount == 0 in `playercreateinfo_item` table, ignoring.", itemid, currentrace, currentclass);
                            continue;
                        }

                        if (currentrace == 0 || currentclass == 0)
                        {
                            uint minrace = currentrace != 0 ? currentrace : 1;
                            uint maxrace = currentrace != 0 ? currentrace + 1 : (int)Race.Max;
                            uint minclass = currentclass != 0 ? currentclass : 1;
                            uint maxclass = currentclass != 0 ? currentclass + 1 : (int)Class.Max;
                            for (var r = minrace; r < maxrace; ++r)
                                for (var c = minclass; c < maxclass; ++c)
                                    PlayerCreateInfoAddItemHelper(r, c, itemid, amount);

                        }
                        else
                            PlayerCreateInfoAddItemHelper(currentrace, currentclass, itemid, amount);

                        ++count;
                    } while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading, "Loaded {0} custom player create items in {1} ms", count, Time.GetMSTimeDiffToNow(time));
                }
            }

            // Load playercreate skills
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Skill Data...");
            {
                uint oldMSTime = Time.GetMSTime();

                foreach (SkillRaceClassInfoRecord rcInfo in CliDB.SkillRaceClassInfoStorage.Values)
                {
                    if (rcInfo.Availability == 1)
                    {
                        for (Race raceIndex = Race.Human; raceIndex < Race.Max; ++raceIndex)
                        {
                            if (rcInfo.RaceMask == -1 || Convert.ToBoolean(SharedConst.GetMaskForRace(raceIndex) & rcInfo.RaceMask))
                            {
                                for (int classIndex = (int)Class.Warrior; classIndex < (int)Class.Max; ++classIndex)
                                {
                                    if (rcInfo.ClassMask == -1 || Convert.ToBoolean((1 << (classIndex - 1)) & rcInfo.ClassMask))
                                    {
                                        PlayerInfo info = _playerInfo[(int)raceIndex][classIndex];
                                        if (info != null)
                                            info.skills.Add(rcInfo);
                                    }
                                }
                            }
                        }
                    }
                }
                Log.outInfo(LogFilter.ServerLoading, "Loaded player create skills in {0} ms", Time.GetMSTimeDiffToNow(oldMSTime));
            }

            // Load playercreate custom spells
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Custom Spell Data...");
            {
                uint oldMSTime = Time.GetMSTime();

                SQLResult result = DB.World.Query("SELECT racemask, classmask, Spell FROM playercreateinfo_spell_custom");
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 player create custom spells. DB table `playercreateinfo_spell_custom` is empty.");
                }
                else
                {
                    uint count = 0;
                    do
                    {
                        ulong raceMask = result.Read<ulong>(0);
                        uint classMask = result.Read<uint>(1);
                        uint spellId = result.Read<uint>(2);

                        if (raceMask != 0 && !Convert.ToBoolean(raceMask & SharedConst.RaceMaskAllPlayable))
                        {
                            Log.outError(LogFilter.Sql, "Wrong race mask {0} in `playercreateinfo_spell_custom` table, ignoring.", raceMask);
                            continue;
                        }

                        if (classMask != 0 && !Convert.ToBoolean(classMask & (int)Class.ClassMaskAllPlayable))
                        {
                            Log.outError(LogFilter.Sql, "Wrong class mask {0} in `playercreateinfo_spell_custom` table, ignoring.", classMask);
                            continue;
                        }

                        for (Race raceIndex = Race.Human; raceIndex < Race.Max; ++raceIndex)
                        {
                            if (raceMask == 0 || Convert.ToBoolean((ulong)SharedConst.GetMaskForRace(raceIndex) & raceMask))
                            {
                                for (int classIndex = (int)Class.Warrior; classIndex < (int)Class.Max; ++classIndex)
                                {
                                    if (classMask == 0 || Convert.ToBoolean((1 << (classIndex - 1)) & classMask))
                                    {
                                        PlayerInfo info = _playerInfo[(int)raceIndex][classIndex];
                                        if (info != null)
                                        {
                                            info.customSpells.Add(spellId);
                                            ++count;
                                        }
                                        // We need something better here, the check is not accounting for spells used by multiple races/classes but not all of them.
                                        // Either split the masks per class, or per race, which kind of kills the point yet.
                                    }
                                }
                            }
                        }
                    }
                    while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading, "Loaded {0} custom player create spells in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
                }
            }

            // Load playercreate cast spell
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Cast Spell Data...");
            {
                uint oldMSTime = Time.GetMSTime();

                SQLResult result = DB.World.Query("SELECT raceMask, classMask, spell FROM playercreateinfo_cast_spell");

                if (result.IsEmpty())
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 player create cast spells. DB table `playercreateinfo_cast_spell` is empty.");
                else
                {
                    uint count = 0;

                    do
                    {
                        ulong raceMask = result.Read<ulong>(0);
                        uint classMask = result.Read<uint>(1);
                        uint spellId = result.Read<uint>(2);

                        if (raceMask != 0 && (raceMask & SharedConst.RaceMaskAllPlayable) == 0)
                        {
                            Log.outError(LogFilter.Sql, "Wrong race mask {0} in `playercreateinfo_cast_spell` table, ignoring.", raceMask);
                            continue;
                        }

                        if (classMask != 0 && !classMask.HasAnyFlag<uint>((uint)Class.ClassMaskAllPlayable))
                        {
                            Log.outError(LogFilter.Sql, "Wrong class mask {0} in `playercreateinfo_cast_spell` table, ignoring.", classMask);
                            continue;
                        }

                        for (Race raceIndex = Race.Human; raceIndex < Race.Max; ++raceIndex)
                        {
                            if (raceMask == 0 || Convert.ToBoolean((ulong)SharedConst.GetMaskForRace(raceIndex) & raceMask))
                            {
                                for (int classIndex = (int)Class.Warrior; classIndex < (int)Class.Max; ++classIndex)
                                {
                                    if (classMask == 0 || Convert.ToBoolean((1 << (classIndex - 1)) & classMask))
                                    {
                                        PlayerInfo info = _playerInfo[(int)raceIndex][classIndex];
                                        if (info != null)
                                        {
                                            info.castSpells.Add(spellId);
                                            ++count;
                                        }
                                    }
                                }
                            }
                        }
                    } while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading, "Loaded {0} player create cast spells in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
                }
            }

            // Load playercreate actions
            time = Time.GetMSTime();
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Action Data...");
            {
                //                                         0     1      2       3       4
                SQLResult result = DB.World.Query("SELECT race, class, button, action, type FROM playercreateinfo_action");

                if (result.IsEmpty())
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 player create actions. DB table `playercreateinfo_action` is empty.");
                else
                {
                    uint count = 0;
                    do
                    {
                        uint currentrace = result.Read<uint>(0);
                        if (currentrace >= (int)Race.Max)
                        {
                            Log.outError(LogFilter.Sql, "Wrong race {0} in `playercreateinfo_action` table, ignoring.", currentrace);
                            continue;
                        }

                        uint currentclass = result.Read<uint>(1);
                        if (currentclass >= (int)Class.Max)
                        {
                            Log.outError(LogFilter.Sql, "Wrong class {0} in `playercreateinfo_action` table, ignoring.", currentclass);
                            continue;
                        }
                        PlayerInfo info = _playerInfo[currentrace][currentclass];
                        if (info != null)
                            info.action.Add(new PlayerCreateInfoAction(result.Read<byte>(2), result.Read<uint>(3), result.Read<byte>(4)));

                        ++count;
                    } while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading, "Loaded {0} player create actions in {1} ms", count, Time.GetMSTimeDiffToNow(time));
                }
            }
            time = Time.GetMSTime();
            // Loading levels data (class/race dependent)
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Level Stats Data...");
            {
                short[][] raceStatModifiers = new short[(int)Race.Max][];
                for (var i = 0; i < (int)Race.Max; ++i)
                    raceStatModifiers[i] = new short[(int)Stats.Max];


                //                                         0     1    2    3    4 
                SQLResult result = DB.World.Query("SELECT race, str, agi, sta, inte FROM player_racestats");
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 level stats definitions. DB table `player_racestats` is empty.");
                    Global.WorldMgr.StopNow();
                    return;
                }

                do
                {
                    uint currentrace = result.Read<uint>(0);
                    if (currentrace >= (int)Race.Max)
                    {
                        Log.outError(LogFilter.Sql, $"Wrong race {currentrace} in `player_racestats` table, ignoring.");
                        continue;
                    }

                    for (int i = 0; i < (int)Stats.Max; ++i)
                        raceStatModifiers[currentrace][i] = result.Read<short>(i + 1);

                } while (result.NextRow());

                //                               0      1      2    3    4    5
                result = DB.World.Query("SELECT class, level, str, agi, sta, inte FROM player_classlevelstats");
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 level stats definitions. DB table `player_classlevelstats` is empty.");
                    Global.WorldMgr.StopNow();
                    return;
                }

                uint count = 0;

                do
                {
                    uint currentclass = result.Read<byte>(0);
                    if (currentclass >= (int)Class.Max)
                    {
                        Log.outError(LogFilter.Sql, "Wrong class {0} in `player_classlevelstats` table, ignoring.", currentclass);
                        continue;
                    }

                    uint currentlevel = result.Read<uint>(1);
                    if (currentlevel > WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                    {
                        if (currentlevel > 255)        // hardcoded level maximum
                            Log.outError(LogFilter.Sql, $"Wrong (> 255) level {currentlevel} in `player_classlevelstats` table, ignoring.");
                        else
                            Log.outError(LogFilter.Sql, $"Unused (> MaxPlayerLevel in worldserver.conf) level {currentlevel} in `player_levelstats` table, ignoring.");

                        continue;
                    }

                    for (var race = 0; race < raceStatModifiers.Length; ++race)
                    {
                        var pInfo = _playerInfo[race][currentclass];
                        if (pInfo == null)
                            continue;

                        if (pInfo.levelInfo[currentlevel - 1] == null)
                            pInfo.levelInfo[currentlevel - 1] = new PlayerLevelInfo();

                        var levelinfo = pInfo.levelInfo[currentlevel - 1];

                        for (var i = 0; i < (int)Stats.Max; i++)
                            levelinfo.stats[i] = (ushort)(result.Read<ushort>(i + 2) + raceStatModifiers[race][i]);
                    }

                    ++count;
                } while (result.NextRow());

                // Fill gaps and check integrity
                for (uint race = 0; race < (int)Race.Max; ++race)
                {
                    // skip non existed races
                    if (!CliDB.ChrRacesStorage.ContainsKey(race) || _playerInfo[race][0] == null)
                        continue;

                    for (uint _class = 0; _class < (int)Class.Max; ++_class)
                    {
                        // skip non existed classes
                        if (CliDB.ChrClassesStorage.LookupByKey(_class) == null || _playerInfo[race][_class] == null)
                            continue;

                        PlayerInfo pInfo = _playerInfo[race][_class];
                        if (pInfo == null)
                            continue;

                        // skip non loaded combinations
                        if (pInfo.DisplayId_m == 0 || pInfo.DisplayId_f == 0)
                            continue;

                        // skip expansion races if not playing with expansion
                        if (WorldConfig.GetIntValue(WorldCfg.Expansion) < (int)Expansion.BurningCrusade && (race == (int)Race.BloodElf || race == (int)Race.Draenei))
                            continue;

                        // skip expansion classes if not playing with expansion
                        if (WorldConfig.GetIntValue(WorldCfg.Expansion) < (int)Expansion.WrathOfTheLichKing && _class == (int)Class.Deathknight)
                            continue;

                        if (WorldConfig.GetIntValue(WorldCfg.Expansion) < (int)Expansion.MistsOfPandaria && (race == (int)Race.PandarenNeutral || race == (int)Race.PandarenHorde || race == (int)Race.PandarenAlliance))
                            continue;

                        if (WorldConfig.GetIntValue(WorldCfg.Expansion) < (int)Expansion.Legion && _class == (int)Class.DemonHunter)
                            continue;

                        // fatal error if no level 1 data
                        if (pInfo.levelInfo == null || pInfo.levelInfo[0] == null)
                        {
                            Log.outError(LogFilter.Sql, "Race {0} Class {1} Level 1 does not have stats data!", race, _class);
                            Global.WorldMgr.StopNow();
                            return;
                        }

                        // fill level gaps
                        for (var level = 1; level < WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel); ++level)
                        {
                            if (pInfo.levelInfo[level] == null)
                            {
                                Log.outError(LogFilter.Sql, "Race {0} Class {1} Level {2} does not have stats data. Using stats data of level {3}.", race, _class, level + 1, level);
                                pInfo.levelInfo[level] = pInfo.levelInfo[level - 1];
                            }
                        }
                    }
                }

                Log.outInfo(LogFilter.ServerLoading, "Loaded {0} level stats definitions in {1} ms", count, Time.GetMSTimeDiffToNow(time));
            }
            time = Time.GetMSTime();
            // Loading xp per level data
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create XP Data...");
            {
                _playerXPperLevel = new uint[CliDB.XpGameTable.GetTableRowCount() + 1];

                //                                          0      1
                SQLResult result = DB.World.Query("SELECT Level, Experience FROM player_xp_for_level");

                // load the DBC's levels at first...
                for (uint level = 1; level < CliDB.XpGameTable.GetTableRowCount(); ++level)
                    _playerXPperLevel[level] = (uint)CliDB.XpGameTable.GetRow(level).Total;

                uint count = 0;
                // ...overwrite if needed (custom values)
                if (!result.IsEmpty())
                {
                    do
                    {
                        uint currentlevel = result.Read<byte>(0);
                        uint currentxp = result.Read<uint>(1);

                        if (currentlevel >= WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                        {
                            if (currentlevel > SharedConst.StrongMaxLevel)        // hardcoded level maximum
                                Log.outError(LogFilter.Sql, "Wrong (> {0}) level {1} in `player_xp_for_level` table, ignoring.", 255, currentlevel);
                            else
                            {
                                Log.outError(LogFilter.Sql, "Unused (> MaxPlayerLevel in worldserver.conf) level {0} in `player_xp_for_levels` table, ignoring.", currentlevel);
                                ++count;                                // make result loading percent "expected" correct in case disabled detail mode for example.
                            }
                            continue;
                        }
                        //PlayerXPperLevel
                        _playerXPperLevel[currentlevel] = currentxp;
                        ++count;
                    } while (result.NextRow());

                    // fill level gaps
                    for (var level = 1; level < WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel); ++level)
                    {
                        if (_playerXPperLevel[level] == 0)
                        {
                            Log.outError(LogFilter.Sql, "Level {0} does not have XP for level data. Using data of level [{1}] + 12000.", level + 1, level);
                            _playerXPperLevel[level] = _playerXPperLevel[level - 1] + 12000;
                        }
                    }
                }

                Log.outInfo(LogFilter.ServerLoading, "Loaded {0} xp for level definition(s) from database in {1} ms", count, Time.GetMSTimeDiffToNow(time));
            }
        }
        void PlayerCreateInfoAddItemHelper(uint race, uint class_, uint itemId, int count)
        {
            if (_playerInfo[race][class_] == null)
                return;

            if (count > 0)
                _playerInfo[race][class_].item.Add(new PlayerCreateInfoItem(itemId, (uint)count));
            else
            {
                if (count < -1)
                    Log.outError(LogFilter.Sql, "Invalid count {0} specified on item {1} be removed from original player create info (use -1)!", count, itemId);

                var items = _playerInfo[race][class_].item;
                items.RemoveAll(item => { return item.item_id == itemId; });
            }
        }

        public PlayerInfo GetPlayerInfo(Race raceId, Class classId)
        {
            if (raceId >= Race.Max)
                return null;

            if (classId >= Class.Max)
                return null;

            var info = _playerInfo[(int)raceId][(int)classId];
            if (info == null)
                return null;

            return info;
        }
        public void GetPlayerClassLevelInfo(Class _class, uint level, out uint baseMana)
        {
            baseMana = 0;
            if (level < 1 || _class >= Class.Max)
                return;

            if (level > WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                level = (byte)WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel);

            GtBaseMPRecord mp = CliDB.BaseMPGameTable.GetRow(level);
            if (mp == null)
            {
                Log.outError(LogFilter.Sql, "Tried to get non-existant Class-Level combination data for base mp. Class {0} Level {1}", _class, level);
                return;
            }

            baseMana = (uint)CliDB.GetGameTableColumnForClass(mp, _class);
        }
        public PlayerLevelInfo GetPlayerLevelInfo(Race race, Class _class, uint level)
        {
            if (level < 1 || race >= Race.Max || _class >= Class.Max)
                return null;

            PlayerInfo pInfo = _playerInfo[(int)race][(int)_class];
            if (pInfo.DisplayId_m == 0 || pInfo.DisplayId_f == 0)
                return null;

            if (level <= WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                return pInfo.levelInfo[level - 1];
            else
                return BuildPlayerLevelInfo(race, _class, level);
        }

        PlayerLevelInfo BuildPlayerLevelInfo(Race race, Class _class, uint level)
        {
            // base data (last known level)
            var info = _playerInfo[(byte)race][(int)_class].levelInfo[WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel) - 1];

            for (int lvl = WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel) - 1; lvl < level; ++lvl)
            {
                switch (_class)
                {
                    case Class.Warrior:
                        info.stats[0] += (lvl > 23 ? 2 : (lvl > 1 ? 1 : 0));
                        info.stats[1] += (lvl > 23 ? 2 : (lvl > 1 ? 1 : 0));
                        info.stats[2] += (lvl > 36 ? 1 : (lvl > 6 && (lvl % 2) != 0 ? 1 : 0));
                        info.stats[3] += (lvl > 9 && (lvl % 2) == 0 ? 1 : 0);
                        break;
                    case Class.Paladin:
                        info.stats[0] += (lvl > 3 ? 1 : 0);
                        info.stats[1] += (lvl > 33 ? 2 : (lvl > 1 ? 1 : 0));
                        info.stats[2] += (lvl > 38 ? 1 : (lvl > 7 && (lvl % 2) == 0 ? 1 : 0));
                        info.stats[3] += (lvl > 6 && (lvl % 2) != 0 ? 1 : 0);
                        break;
                    case Class.Hunter:
                        info.stats[0] += (lvl > 4 ? 1 : 0);
                        info.stats[1] += (lvl > 4 ? 1 : 0);
                        info.stats[2] += (lvl > 33 ? 2 : (lvl > 1 ? 1 : 0));
                        info.stats[3] += (lvl > 8 && (lvl % 2) != 0 ? 1 : 0);
                        break;
                    case Class.Rogue:
                        info.stats[0] += (lvl > 5 ? 1 : 0);
                        info.stats[1] += (lvl > 4 ? 1 : 0);
                        info.stats[2] += (lvl > 16 ? 2 : (lvl > 1 ? 1 : 0));
                        info.stats[3] += (lvl > 8 && (lvl % 2) == 0 ? 1 : 0);
                        break;
                    case Class.Priest:
                        info.stats[0] += (lvl > 9 && (lvl % 2) == 0 ? 1 : 0);
                        info.stats[1] += (lvl > 5 ? 1 : 0);
                        info.stats[2] += (lvl > 38 ? 1 : (lvl > 8 && (lvl % 2) != 0 ? 1 : 0));
                        info.stats[3] += (lvl > 22 ? 2 : (lvl > 1 ? 1 : 0));
                        break;
                    case Class.Shaman:
                        info.stats[0] += (lvl > 34 ? 1 : (lvl > 6 && (lvl % 2) != 0 ? 1 : 0));
                        info.stats[1] += (lvl > 4 ? 1 : 0);
                        info.stats[2] += (lvl > 7 && (lvl % 2) == 0 ? 1 : 0);
                        info.stats[3] += (lvl > 5 ? 1 : 0);
                        break;
                    case Class.Mage:
                        info.stats[0] += (lvl > 9 && (lvl % 2) == 0 ? 1 : 0);
                        info.stats[1] += (lvl > 5 ? 1 : 0);
                        info.stats[2] += (lvl > 9 && (lvl % 2) == 0 ? 1 : 0);
                        info.stats[3] += (lvl > 24 ? 2 : (lvl > 1 ? 1 : 0));
                        break;
                    case Class.Warlock:
                        info.stats[0] += (lvl > 9 && (lvl % 2) == 0 ? 1 : 0);
                        info.stats[1] += (lvl > 38 ? 2 : (lvl > 3 ? 1 : 0));
                        info.stats[2] += (lvl > 9 && (lvl % 2) == 0 ? 1 : 0);
                        info.stats[3] += (lvl > 33 ? 2 : (lvl > 2 ? 1 : 0));
                        break;
                    case Class.Druid:
                        info.stats[0] += (lvl > 38 ? 2 : (lvl > 6 && (lvl % 2) != 0 ? 1 : 0));
                        info.stats[1] += (lvl > 32 ? 2 : (lvl > 4 ? 1 : 0));
                        info.stats[2] += (lvl > 38 ? 2 : (lvl > 8 && (lvl % 2) != 0 ? 1 : 0));
                        info.stats[3] += (lvl > 38 ? 3 : (lvl > 4 ? 1 : 0));
                        break;
                }
            }

            return info;
        }

        //Pets
        public void LoadPetLevelInfo()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0               1      2   3     4    5    6    7     8    9
            SQLResult result = DB.World.Query("SELECT creature_entry, level, hp, mana, str, agi, sta, inte, spi, armor FROM pet_levelstats");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 level pet stats definitions. DB table `pet_levelstats` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint creatureid = result.Read<uint>(0);
                if (GetCreatureTemplate(creatureid) == null)
                {
                    Log.outError(LogFilter.Sql, "Wrong creature id {0} in `pet_levelstats` table, ignoring.", creatureid);
                    continue;
                }

                uint currentlevel = result.Read<uint>(1);
                if (currentlevel > WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                {
                    if (currentlevel > SharedConst.StrongMaxLevel)        // hardcoded level maximum
                        Log.outError(LogFilter.Sql, "Wrong (> {0}) level {1} in `pet_levelstats` table, ignoring.", SharedConst.StrongMaxLevel, currentlevel);
                    else
                    {
                        Log.outInfo(LogFilter.Server, "Unused (> MaxPlayerLevel in worldserver.conf) level {0} in `pet_levelstats` table, ignoring.", currentlevel);
                        ++count;                                // make result loading percent "expected" correct in case disabled detail mode for example.
                    }
                    continue;
                }
                else if (currentlevel < 1)
                {
                    Log.outError(LogFilter.Sql, "Wrong (<1) level {0} in `pet_levelstats` table, ignoring.", currentlevel);
                    continue;
                }

                var pInfoMapEntry = petInfoStore.LookupByKey(creatureid);

                if (pInfoMapEntry == null)
                    pInfoMapEntry = new PetLevelInfo[WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel)];

                PetLevelInfo pLevelInfo = new();
                pLevelInfo.health = result.Read<uint>(2);
                pLevelInfo.mana = result.Read<uint>(3);
                pLevelInfo.armor = result.Read<uint>(9);

                for (int i = 0; i < (int)Stats.Max; i++)
                {
                    pLevelInfo.stats[i] = result.Read<uint>(i + 4);
                }

                pInfoMapEntry[currentlevel - 1] = pLevelInfo;

                ++count;
            }
            while (result.NextRow());

            // Fill gaps and check integrity
            foreach (var map in petInfoStore)
            {
                var pInfo = map.Value;

                // fatal error if no level 1 data
                if (pInfo == null || pInfo[0].health == 0)
                {
                    Log.outError(LogFilter.Sql, "Creature {0} does not have pet stats data for Level 1!", map.Key);
                    Global.WorldMgr.StopNow();
                }

                // fill level gaps
                for (byte level = 1; level < WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel); ++level)
                {
                    if (pInfo[level].health == 0)
                    {
                        Log.outError(LogFilter.Sql, "Creature {0} has no data for Level {1} pet stats data, using data of Level {2}.", map.Key, level + 1, level);
                        pInfo[level] = pInfo[level - 1];
                    }
                }
            }

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} level pet stats definitions in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadPetNames()
        {
            uint oldMSTime = Time.GetMSTime();
            //                                          0     1      2
            SQLResult result = DB.World.Query("SELECT word, entry, half FROM pet_name_generation");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 pet name parts. DB table `pet_name_generation` is empty!");
                return;
            }

            uint count = 0;

            do
            {
                string word = result.Read<string>(0);
                uint entry = result.Read<uint>(1);
                bool half = result.Read<bool>(2);
                if (half)
                    _petHalfName1.Add(entry, word);
                else
                    _petHalfName0.Add(entry, word);
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} pet name parts in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadPetNumber()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.Characters.Query("SELECT MAX(id) FROM character_pet");
            if (!result.IsEmpty())
                _hiPetNumber = result.Read<uint>(0) + 1;

            Log.outInfo(LogFilter.ServerLoading, "Loaded the max pet number: {0} in {1} ms", _hiPetNumber - 1, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        public PetLevelInfo GetPetLevelInfo(uint creatureid, uint level)
        {
            if (level > WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                level = WorldConfig.GetUIntValue(WorldCfg.MaxPlayerLevel);

            var petinfo = petInfoStore.LookupByKey(creatureid);

            if (petinfo == null)
                return null;

            return petinfo[level - 1];                           // data for level 1 stored in [0] array element, ...
        }
        public string GeneratePetName(uint entry)
        {
            var list0 = _petHalfName0[entry];
            var list1 = _petHalfName1[entry];

            if (list0.Empty() || list1.Empty())
            {
                CreatureTemplate cinfo = GetCreatureTemplate(entry);
                if (cinfo == null)
                    return "";

                string petname = Global.DB2Mgr.GetCreatureFamilyPetName(cinfo.Family, Global.WorldMgr.GetDefaultDbcLocale());
                if (!string.IsNullOrEmpty(petname))
                    return petname;
                else
                    return cinfo.Name;
            }

            return list0[RandomHelper.IRand(0, list0.Count - 1)] + list1[RandomHelper.IRand(0, list1.Count - 1)];
        }
        public uint GeneratePetNumber()
        {
            if (_hiPetNumber >= 0xFFFFFFFE)
            {
                Log.outError(LogFilter.Misc, "_hiPetNumber Id overflow!! Can't continue, shutting down server. ");
                Global.WorldMgr.StopNow(ShutdownExitCode.Error);
            }
            return _hiPetNumber++;
        }

        //Faction Change
        public void LoadFactionChangeAchievements()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT alliance_id, horde_id FROM player_factionchange_achievement");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 faction change achievement pairs. DB table `player_factionchange_achievement` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint alliance = result.Read<uint>(0);
                uint horde = result.Read<uint>(1);

                if (!CliDB.AchievementStorage.ContainsKey(alliance))
                    Log.outError(LogFilter.Sql, "Achievement {0} (alliance_id) referenced in `player_factionchange_achievement` does not exist, pair skipped!", alliance);
                else if (!CliDB.AchievementStorage.ContainsKey(horde))
                    Log.outError(LogFilter.Sql, "Achievement {0} (horde_id) referenced in `player_factionchange_achievement` does not exist, pair skipped!", horde);
                else
                    FactionChangeAchievements[alliance] = horde;

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} faction change achievement pairs in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadFactionChangeItems()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT alliance_id, horde_id FROM player_factionchange_items");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 faction change item pairs. DB table `player_factionchange_items` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint alliance = result.Read<uint>(0);
                uint horde = result.Read<uint>(1);

                if (GetItemTemplate(alliance) == null)
                    Log.outError(LogFilter.Sql, "Item {0} (alliance_id) referenced in `player_factionchange_items` does not exist, pair skipped!", alliance);
                else if (GetItemTemplate(horde) == null)
                    Log.outError(LogFilter.Sql, "Item {0} (horde_id) referenced in `player_factionchange_items` does not exist, pair skipped!", horde);
                else
                    FactionChangeItems[alliance] = horde;

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} faction change item pairs in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadFactionChangeQuests()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT alliance_id, horde_id FROM player_factionchange_quests");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 faction change quest pairs. DB table `player_factionchange_quests` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint alliance = result.Read<uint>(0);
                uint horde = result.Read<uint>(1);

                if (Global.ObjectMgr.GetQuestTemplate(alliance) == null)
                    Log.outError(LogFilter.Sql, "Quest {0} (alliance_id) referenced in `player_factionchange_quests` does not exist, pair skipped!", alliance);
                else if (Global.ObjectMgr.GetQuestTemplate(horde) == null)
                    Log.outError(LogFilter.Sql, "Quest {0} (horde_id) referenced in `player_factionchange_quests` does not exist, pair skipped!", horde);
                else
                    FactionChangeQuests[alliance] = horde;

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} faction change quest pairs in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadFactionChangeReputations()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT alliance_id, horde_id FROM player_factionchange_reputations");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 faction change reputation pairs. DB table `player_factionchange_reputations` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint alliance = result.Read<uint>(0);
                uint horde = result.Read<uint>(1);

                if (!CliDB.FactionStorage.ContainsKey(alliance))
                    Log.outError(LogFilter.Sql, "Reputation {0} (alliance_id) referenced in `player_factionchange_reputations` does not exist, pair skipped!", alliance);
                else if (!CliDB.FactionStorage.ContainsKey(horde))
                    Log.outError(LogFilter.Sql, "Reputation {0} (horde_id) referenced in `player_factionchange_reputations` does not exist, pair skipped!", horde);
                else
                    FactionChangeReputation[alliance] = horde;

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} faction change reputation pairs in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadFactionChangeSpells()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT alliance_id, horde_id FROM player_factionchange_spells");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 faction change spell pairs. DB table `player_factionchange_spells` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint alliance = result.Read<uint>(0);
                uint horde = result.Read<uint>(1);

                if (!Global.SpellMgr.HasSpellInfo(alliance, Difficulty.None))
                    Log.outError(LogFilter.Sql, "Spell {0} (alliance_id) referenced in `player_factionchange_spells` does not exist, pair skipped!", alliance);
                else if (!Global.SpellMgr.HasSpellInfo(horde, Difficulty.None))
                    Log.outError(LogFilter.Sql, "Spell {0} (horde_id) referenced in `player_factionchange_spells` does not exist, pair skipped!", horde);
                else
                    FactionChangeSpells[alliance] = horde;

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} faction change spell pairs in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadFactionChangeTitles()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT alliance_id, horde_id FROM player_factionchange_titles");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 faction change title pairs. DB table `player_factionchange_title` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint alliance = result.Read<uint>(0);
                uint horde = result.Read<uint>(1);

                if (!CliDB.CharTitlesStorage.ContainsKey(alliance))
                    Log.outError(LogFilter.Sql, "Title {0} (alliance_id) referenced in `player_factionchange_title` does not exist, pair skipped!", alliance);
                else if (!CliDB.CharTitlesStorage.ContainsKey(horde))
                    Log.outError(LogFilter.Sql, "Title {0} (horde_id) referenced in `player_factionchange_title` does not exist, pair skipped!", horde);
                else
                    FactionChangeTitles[alliance] = horde;

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} faction change title pairs in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        //Quests
        public void LoadQuests()
        {
            uint oldMSTime = Time.GetMSTime();

            // For reload case
            _questTemplates.Clear();
            _questTemplatesAutoPush.Clear();
            _questObjectives.Clear();
            _exclusiveQuestGroups.Clear();

            SQLResult result = DB.World.Query("SELECT " +
                //0   1          2               3                4            5            6                  7                8                   9
                "ID, QuestType, QuestPackageID, ContentTuningID, QuestSortID, QuestInfoID, SuggestedGroupNum, RewardNextQuest, RewardXPDifficulty, RewardXPMultiplier, " +
                //10           11                     12                     13                14           15           16               17
                "RewardMoney, RewardMoneyDifficulty, RewardMoneyMultiplier, RewardBonusMoney, RewardSpell, RewardHonor, RewardKillHonor, StartItem, " +
                //18                          19                          20                        21     22       23
                "RewardArtifactXPDifficulty, RewardArtifactXPMultiplier, RewardArtifactCategoryID, Flags, FlagsEx, FlagsEx2, " +
                //24           25             26         27                 28           29             30         31
                "RewardItem1, RewardAmount1, ItemDrop1, ItemDropQuantity1, RewardItem2, RewardAmount2, ItemDrop2, ItemDropQuantity2, " +
                //32           33             34         35                 36           37             38         39
                "RewardItem3, RewardAmount3, ItemDrop3, ItemDropQuantity3, RewardItem4, RewardAmount4, ItemDrop4, ItemDropQuantity4, " +
                //40                   41                         42                          43                   44                         45
                "RewardChoiceItemID1, RewardChoiceItemQuantity1, RewardChoiceItemDisplayID1, RewardChoiceItemID2, RewardChoiceItemQuantity2, RewardChoiceItemDisplayID2, " +
                //46                   47                         48                          49                   50                         51
                "RewardChoiceItemID3, RewardChoiceItemQuantity3, RewardChoiceItemDisplayID3, RewardChoiceItemID4, RewardChoiceItemQuantity4, RewardChoiceItemDisplayID4, " +
                //52                   53                         54                          55                   56                         57
                "RewardChoiceItemID5, RewardChoiceItemQuantity5, RewardChoiceItemDisplayID5, RewardChoiceItemID6, RewardChoiceItemQuantity6, RewardChoiceItemDisplayID6, " +
                //58            59    60    61           62           63                 64                 65                 66             67                  68
                "POIContinent, POIx, POIy, POIPriority, RewardTitle, RewardArenaPoints, RewardSkillLineID, RewardNumSkillUps, PortraitGiver, PortraitGiverMount, PortraitTurnIn, " +
                //69                70                   71                      72                   73                74                   75                      76
                "RewardFactionID1, RewardFactionValue1, RewardFactionOverride1, RewardFactionCapIn1, RewardFactionID2, RewardFactionValue2, RewardFactionOverride2, RewardFactionCapIn2, " +
                //77                78                   79                      80                   81                82                   83                      84
                "RewardFactionID3, RewardFactionValue3, RewardFactionOverride3, RewardFactionCapIn3, RewardFactionID4, RewardFactionValue4, RewardFactionOverride4, RewardFactionCapIn4, " +
                //85                86                   87                      88                   89
                "RewardFactionID5, RewardFactionValue5, RewardFactionOverride5, RewardFactionCapIn5, RewardFactionFlags, " +
                //90                 91                  92                 93                  94                 95                  96                 97
                "RewardCurrencyID1, RewardCurrencyQty1, RewardCurrencyID2, RewardCurrencyQty2, RewardCurrencyID3, RewardCurrencyQty3, RewardCurrencyID4, RewardCurrencyQty4, " +
                //98                  99                  100          101          102             103               104        105                  106
                "AcceptedSoundKitID, CompleteSoundKitID, AreaGroupID, TimeAllowed, AllowableRaces, TreasurePickerID, Expansion, ManagedWorldStateID, QuestSessionBonus, " +
                //107       108             109               110              111                112                113                 114                 115
                "LogTitle, LogDescription, QuestDescription, AreaDescription, PortraitGiverText, PortraitGiverName, PortraitTurnInText, PortraitTurnInName, QuestCompletionLog" +
                " FROM quest_template");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quests definitions. DB table `quest_template` is empty.");
                return;
            }

            // create multimap previous quest for each existed quest
            // some quests can have many previous maps set by NextQuestId in previous quest
            // for example set of race quests can lead to single not race specific quest
            do
            {
                Quest newQuest = new(result.GetFields());
                _questTemplates[newQuest.Id] = newQuest;
                if (newQuest.IsAutoPush())
                    _questTemplatesAutoPush.Add(newQuest);
            }
            while (result.NextRow());


            // Load `quest_reward_choice_items`
            //                               0        1      2      3      4      5      6
            result = DB.World.Query("SELECT QuestID, Type1, Type2, Type3, Type4, Type5, Type6 FROM quest_reward_choice_items");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest reward choice items. DB table `quest_reward_choice_items` is empty.");
            }
            else
            {
                do
                {
                    uint questId = result.Read<uint>(0);

                    var quest = _questTemplates.LookupByKey(questId);
                    if (quest != null)
                        quest.LoadRewardChoiceItems(result.GetFields());
                    else
                        Log.outError(LogFilter.Sql, $"Table `quest_reward_choice_items` has data for quest {questId} but such quest does not exist");
                } while (result.NextRow());
            }


            // Load `quest_reward_display_spell`
            //                               0        1        2
            result = DB.World.Query("SELECT QuestID, SpellID, PlayerConditionID FROM quest_reward_display_spell ORDER BY QuestID ASC, Idx ASC");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest reward display spells. DB table `quest_reward_display_spell` is empty.");
            }
            else
            {
                do
                {
                    uint questId = result.Read<uint>(0);

                    var quest = _questTemplates.LookupByKey(questId);
                    if (quest != null)
                        quest.LoadRewardDisplaySpell(result.GetFields());
                    else
                        Log.outError(LogFilter.Sql, $"Table `quest_reward_display_spell` has data for quest {questId} but such quest does not exist");
                } while (result.NextRow());
            }


            // Load `quest_details`
            //                               0   1       2       3       4       5            6            7            8
            result = DB.World.Query("SELECT ID, Emote1, Emote2, Emote3, Emote4, EmoteDelay1, EmoteDelay2, EmoteDelay3, EmoteDelay4 FROM quest_details");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest details. DB table `quest_details` is empty.");
            }
            else
            {
                do
                {
                    uint questId = result.Read<uint>(0);

                    var quest = _questTemplates.LookupByKey(questId);
                    if (quest != null)
                        quest.LoadQuestDetails(result.GetFields());
                    else
                        Log.outError(LogFilter.Sql, "Table `quest_details` has data for quest {0} but such quest does not exist", questId);
                } while (result.NextRow());
            }

            // Load `quest_request_items`
            //                               0   1                2                  3                     4                       5
            result = DB.World.Query("SELECT ID, EmoteOnComplete, EmoteOnIncomplete, EmoteOnCompleteDelay, EmoteOnIncompleteDelay, CompletionText FROM quest_request_items");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest request items. DB table `quest_request_items` is empty.");
            }
            else
            {
                do
                {
                    uint questId = result.Read<uint>(0);

                    var quest = _questTemplates.LookupByKey(questId);
                    if (quest != null)
                        quest.LoadQuestRequestItems(result.GetFields());
                    else
                        Log.outError(LogFilter.Sql, "Table `quest_request_items` has data for quest {0} but such quest does not exist", questId);
                } while (result.NextRow());
            }

            // Load `quest_offer_reward`
            //                               0   1       2       3       4       5            6            7            8            9
            result = DB.World.Query("SELECT ID, Emote1, Emote2, Emote3, Emote4, EmoteDelay1, EmoteDelay2, EmoteDelay3, EmoteDelay4, RewardText FROM quest_offer_reward");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest reward emotes. DB table `quest_offer_reward` is empty.");
            }
            else
            {
                do
                {
                    uint questId = result.Read<uint>(0);

                    var quest = _questTemplates.LookupByKey(questId);
                    if (quest != null)
                        quest.LoadQuestOfferReward(result.GetFields());
                    else
                        Log.outError(LogFilter.Sql, "Table `quest_offer_reward` has data for quest {0} but such quest does not exist", questId);
                } while (result.NextRow());
            }

            // Load `quest_template_addon`
            //                               0   1         2                 3              4            5            6               7                     8
            result = DB.World.Query("SELECT ID, MaxLevel, AllowableClasses, SourceSpellID, PrevQuestID, NextQuestID, ExclusiveGroup, RewardMailTemplateID, RewardMailDelay, " +
                //9               10                   11                     12                     13                   14                   15
                "RequiredSkillID, RequiredSkillPoints, RequiredMinRepFaction, RequiredMaxRepFaction, RequiredMinRepValue, RequiredMaxRepValue, ProvidedItemCount, " +
                //16           17
                "SpecialFlags, ScriptName FROM quest_template_addon LEFT JOIN quest_mail_sender ON Id=QuestId");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest template addons. DB table `quest_template_addon` is empty.");
            }
            else
            {
                do
                {
                    uint questId = result.Read<uint>(0);

                    var quest = _questTemplates.LookupByKey(questId);
                    if (quest != null)
                        quest.LoadQuestTemplateAddon(result.GetFields());
                    else
                        Log.outError(LogFilter.Sql, "Table `quest_template_addon` has data for quest {0} but such quest does not exist", questId);
                } while (result.NextRow());
            }

            // Load `quest_mail_sender`
            //                               0        1
            result = DB.World.Query("SELECT QuestId, RewardMailSenderEntry FROM quest_mail_sender");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest mail senders. DB table `quest_mail_sender` is empty.");
            }
            else
            {
                do
                {
                    uint questId = result.Read<uint>(0);

                    var quest = _questTemplates.LookupByKey(questId);
                    if (quest != null)
                        quest.LoadQuestMailSender(result.GetFields());
                    else
                        Log.outError(LogFilter.Sql, "Table `quest_mail_sender` has data for quest {0} but such quest does not exist", questId);
                } while (result.NextRow());
            }

            // Load `quest_objectives`
            //                               0        1   2     3             4         5       6      7       8                  9
            result = DB.World.Query("SELECT QuestID, ID, Type, StorageIndex, ObjectID, Amount, Flags, Flags2, ProgressBarWeight, Description FROM quest_objectives ORDER BY `Order` ASC, StorageIndex ASC");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest objectives. DB table `quest_objectives` is empty.");
            }
            else
            {
                do
                {
                    uint questId = result.Read<uint>(0);
                    var quest = _questTemplates.LookupByKey(questId);
                    if (quest != null)
                        quest.LoadQuestObjective(result.GetFields());
                    else
                        Log.outError(LogFilter.Sql, "Table `quest_objectives` has objective for quest {0} but such quest does not exist", questId);
                } while (result.NextRow());
            }

            // Load `quest_visual_effect` join table with quest_objectives because visual effects are based on objective ID (core stores objectives by their index in quest)
            //                                 0     1     2          3        4
            result = DB.World.Query("SELECT v.ID, o.ID, o.QuestID, v.Index, v.VisualEffect FROM quest_visual_effect AS v LEFT JOIN quest_objectives AS o ON v.ID = o.ID ORDER BY v.Index DESC");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest visual effects. DB table `quest_visual_effect` is empty.");
            }
            else
            {
                do
                {
                    uint vID = result.Read<uint>(0);
                    uint oID = result.Read<uint>(1);

                    if (vID == 0)
                    {
                        Log.outError(LogFilter.Sql, "Table `quest_visual_effect` has visual effect for null objective id");
                        continue;
                    }

                    // objID will be null if match for table join is not found
                    if (vID != oID)
                    {
                        Log.outError(LogFilter.Sql, "Table `quest_visual_effect` has visual effect for objective {0} but such objective does not exist.", vID);
                        continue;
                    }

                    uint questId = result.Read<uint>(2);

                    // Do not throw error here because error for non existing quest is thrown while loading quest objectives. we do not need duplication
                    var quest = _questTemplates.LookupByKey(questId);
                    if (quest != null)
                        quest.LoadQuestObjectiveVisualEffect(result.GetFields());
                } while (result.NextRow());
            }

            Dictionary<uint, uint> usedMailTemplates = new();

            // Post processing
            foreach (var qinfo in _questTemplates.Values)
            {
                // skip post-loading checks for disabled quests
                if (Global.DisableMgr.IsDisabledFor(DisableType.Quest, qinfo.Id, null))
                    continue;

                // additional quest integrity checks (GO, creaturetemplate and itemtemplate must be loaded already)

                if (qinfo.Type >= QuestType.Max)
                    Log.outError(LogFilter.Sql, "Quest {0} has `Method` = {1}, expected values are 0, 1 or 2.", qinfo.Id, qinfo.Type);

                if (Convert.ToBoolean(qinfo.SpecialFlags & ~QuestSpecialFlags.DbAllowed))
                {
                    Log.outError(LogFilter.Sql, "Quest {0} has `SpecialFlags` = {1} > max allowed value. Correct `SpecialFlags` to value <= {2}",
                        qinfo.Id, qinfo.SpecialFlags, QuestSpecialFlags.DbAllowed);
                    qinfo.SpecialFlags &= QuestSpecialFlags.DbAllowed;
                }

                if (qinfo.Flags.HasAnyFlag(QuestFlags.Daily) && qinfo.Flags.HasAnyFlag(QuestFlags.Weekly))
                {
                    Log.outError(LogFilter.Sql, "Weekly Quest {0} is marked as daily quest in `Flags`, removed daily flag.", qinfo.Id);
                    qinfo.Flags &= ~QuestFlags.Daily;
                }

                if (qinfo.Flags.HasAnyFlag(QuestFlags.Daily))
                {
                    if (!qinfo.SpecialFlags.HasAnyFlag(QuestSpecialFlags.Repeatable))
                    {
                        Log.outError(LogFilter.Sql, "Daily Quest {0} not marked as repeatable in `SpecialFlags`, added.", qinfo.Id);
                        qinfo.SpecialFlags |= QuestSpecialFlags.Repeatable;
                    }
                }

                if (qinfo.Flags.HasAnyFlag(QuestFlags.Weekly))
                {
                    if (!qinfo.SpecialFlags.HasAnyFlag(QuestSpecialFlags.Repeatable))
                    {
                        Log.outError(LogFilter.Sql, "Weekly Quest {0} not marked as repeatable in `SpecialFlags`, added.", qinfo.Id);
                        qinfo.SpecialFlags |= QuestSpecialFlags.Repeatable;
                    }
                }

                if (qinfo.SpecialFlags.HasAnyFlag(QuestSpecialFlags.Monthly))
                {
                    if (!qinfo.SpecialFlags.HasAnyFlag(QuestSpecialFlags.Repeatable))
                    {
                        Log.outError(LogFilter.Sql, "Monthly quest {0} not marked as repeatable in `SpecialFlags`, added.", qinfo.Id);
                        qinfo.SpecialFlags |= QuestSpecialFlags.Repeatable;
                    }
                }

                if (Convert.ToBoolean(qinfo.Flags & QuestFlags.Tracking))
                {
                    // at auto-reward can be rewarded only RewardChoiceItemId[0]
                    for (int j = 1; j < qinfo.RewardChoiceItemId.Length; ++j)
                    {
                        var id = qinfo.RewardChoiceItemId[j];
                        if (id != 0)
                        {
                            Log.outError(LogFilter.Sql, "Quest {0} has `RewardChoiceItemId{1}` = {2} but item from `RewardChoiceItemId{3}` can't be rewarded with quest flag QUESTFLAGSTRACKING.",
                                qinfo.Id, j + 1, id, j + 1);
                            // no changes, quest ignore this data
                        }
                    }
                }

                if (qinfo.ContentTuningId != 0 && !CliDB.ContentTuningStorage.ContainsKey(qinfo.ContentTuningId))
                    Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `ContentTuningID` = {qinfo.ContentTuningId} but content tuning with this id does not exist.");

                // client quest log visual (area case)
                if (qinfo.QuestSortID > 0)
                {
                    if (!CliDB.AreaTableStorage.ContainsKey(qinfo.QuestSortID))
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `ZoneOrSort` = {1} (zone case) but zone with this id does not exist.",
                            qinfo.Id, qinfo.QuestSortID);
                        // no changes, quest not dependent from this value but can have problems at client
                    }
                }
                // client quest log visual (sort case)
                if (qinfo.QuestSortID < 0)
                {
                    var qSort = CliDB.QuestSortStorage.LookupByKey((uint)-qinfo.QuestSortID);
                    if (qSort == null)
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `ZoneOrSort` = {1} (sort case) but quest sort with this id does not exist.",
                            qinfo.Id, qinfo.QuestSortID);
                        // no changes, quest not dependent from this value but can have problems at client (note some may be 0, we must allow this so no check)
                    }
                    //check for proper RequiredSkillId value (skill case)
                    var skillid = SharedConst.SkillByQuestSort(-qinfo.QuestSortID);
                    if (skillid != SkillType.None)
                    {
                        if (qinfo.RequiredSkillId != (uint)skillid)
                        {
                            Log.outError(LogFilter.Sql, "Quest {0} has `ZoneOrSort` = {1} but `RequiredSkillId` does not have a corresponding value ({2}).",
                                qinfo.Id, qinfo.QuestSortID, skillid);
                            //override, and force proper value here?
                        }
                    }
                }

                // AllowableClasses, can be 0/CLASSMASK_ALL_PLAYABLE to allow any class
                if (qinfo.AllowableClasses != 0)
                {
                    if (!Convert.ToBoolean(qinfo.AllowableClasses & (uint)Class.ClassMaskAllPlayable))
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} does not contain any playable classes in `RequiredClasses` ({1}), value set to 0 (all classes).", qinfo.Id, qinfo.AllowableClasses);
                        qinfo.AllowableClasses = 0;
                    }
                }
                // AllowableRaces, can be -1/RACEMASK_ALL_PLAYABLE to allow any race
                if (qinfo.AllowableRaces != -1)
                {
                    if (qinfo.AllowableRaces > 0 && !Convert.ToBoolean(qinfo.AllowableRaces & (long)SharedConst.RaceMaskAllPlayable))
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} does not contain any playable races in `RequiredRaces` ({1}), value set to 0 (all races).", qinfo.Id, qinfo.AllowableRaces);
                        qinfo.AllowableRaces = -1;
                    }
                }
                // RequiredSkillId, can be 0
                if (qinfo.RequiredSkillId != 0)
                {
                    if (!CliDB.SkillLineStorage.ContainsKey(qinfo.RequiredSkillId))
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `RequiredSkillId` = {1} but this skill does not exist",
                            qinfo.Id, qinfo.RequiredSkillId);
                    }
                }

                if (qinfo.RequiredSkillPoints != 0)
                {
                    if (qinfo.RequiredSkillPoints > Global.WorldMgr.GetConfigMaxSkillValue())
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `RequiredSkillPoints` = {1} but max possible skill is {2}, quest can't be done.",
                            qinfo.Id, qinfo.RequiredSkillPoints, Global.WorldMgr.GetConfigMaxSkillValue());
                        // no changes, quest can't be done for this requirement
                    }
                }
                // else Skill quests can have 0 skill level, this is ok

                if (qinfo.RequiredMinRepFaction != 0 && !CliDB.FactionStorage.ContainsKey(qinfo.RequiredMinRepFaction))
                {
                    Log.outError(LogFilter.Sql, "Quest {0} has `RequiredMinRepFaction` = {1} but faction template {2} does not exist, quest can't be done.",
                        qinfo.Id, qinfo.RequiredMinRepFaction, qinfo.RequiredMinRepFaction);
                    // no changes, quest can't be done for this requirement
                }

                if (qinfo.RequiredMaxRepFaction != 0 && !CliDB.FactionStorage.ContainsKey(qinfo.RequiredMaxRepFaction))
                {
                    Log.outError(LogFilter.Sql, "Quest {0} has `RequiredMaxRepFaction` = {1} but faction template {2} does not exist, quest can't be done.",
                        qinfo.Id, qinfo.RequiredMaxRepFaction, qinfo.RequiredMaxRepFaction);
                    // no changes, quest can't be done for this requirement
                }

                if (qinfo.RequiredMinRepValue != 0 && qinfo.RequiredMinRepValue > SharedConst.ReputationCap)
                {
                    Log.outError(LogFilter.Sql, "Quest {0} has `RequiredMinRepValue` = {1} but max reputation is {2}, quest can't be done.",
                        qinfo.Id, qinfo.RequiredMinRepValue, SharedConst.ReputationCap);
                    // no changes, quest can't be done for this requirement
                }

                if (qinfo.RequiredMinRepValue != 0 && qinfo.RequiredMaxRepValue != 0 && qinfo.RequiredMaxRepValue <= qinfo.RequiredMinRepValue)
                {
                    Log.outError(LogFilter.Sql, "Quest {0} has `RequiredMaxRepValue` = {1} and `RequiredMinRepValue` = {2}, quest can't be done.",
                        qinfo.Id, qinfo.RequiredMaxRepValue, qinfo.RequiredMinRepValue);
                    // no changes, quest can't be done for this requirement
                }

                if (qinfo.RequiredMinRepFaction == 0 && qinfo.RequiredMinRepValue != 0)
                {
                    Log.outError(LogFilter.Sql, "Quest {0} has `RequiredMinRepValue` = {1} but `RequiredMinRepFaction` is 0, value has no effect",
                        qinfo.Id, qinfo.RequiredMinRepValue);
                    // warning
                }

                if (qinfo.RequiredMaxRepFaction == 0 && qinfo.RequiredMaxRepValue != 0)
                {
                    Log.outError(LogFilter.Sql, "Quest {0} has `RequiredMaxRepValue` = {1} but `RequiredMaxRepFaction` is 0, value has no effect",
                        qinfo.Id, qinfo.RequiredMaxRepValue);
                    // warning
                }

                if (qinfo.RewardTitleId != 0 && !CliDB.CharTitlesStorage.ContainsKey(qinfo.RewardTitleId))
                {
                    Log.outError(LogFilter.Sql, "Quest {0} has `RewardTitleId` = {1} but CharTitle Id {1} does not exist, quest can't be rewarded with title.",
                        qinfo.Id, qinfo.RewardTitleId);
                    qinfo.RewardTitleId = 0;
                    // quest can't reward this title
                }

                if (qinfo.SourceItemId != 0)
                {
                    if (Global.ObjectMgr.GetItemTemplate(qinfo.SourceItemId) == null)
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `SourceItemId` = {1} but item with entry {2} does not exist, quest can't be done.",
                            qinfo.Id, qinfo.SourceItemId, qinfo.SourceItemId);
                        qinfo.SourceItemId = 0;                       // quest can't be done for this requirement
                    }
                    else if (qinfo.SourceItemIdCount == 0)
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `StartItem` = {1} but `ProvidedItemCount` = 0, set to 1 but need fix in DB.",
                            qinfo.Id, qinfo.SourceItemId);
                        qinfo.SourceItemIdCount = 1;                    // update to 1 for allow quest work for backward compatibility with DB
                    }
                }
                else if (qinfo.SourceItemIdCount > 0)
                {
                    Log.outError(LogFilter.Sql, "Quest {0} has `SourceItemId` = 0 but `SourceItemIdCount` = {1}, useless value.",
                        qinfo.Id, qinfo.SourceItemIdCount);
                    qinfo.SourceItemIdCount = 0;                          // no quest work changes in fact
                }

                if (qinfo.SourceSpellID != 0)
                {
                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(qinfo.SourceSpellID, Difficulty.None);
                    if (spellInfo == null)
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `SourceSpellid` = {1} but spell {1} doesn't exist, quest can't be done.",
                            qinfo.Id, qinfo.SourceSpellID);
                        qinfo.SourceSpellID = 0;                        // quest can't be done for this requirement
                    }
                    else if (!Global.SpellMgr.IsSpellValid(spellInfo))
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `SourceSpellid` = {1} but spell {1} is broken, quest can't be done.",
                            qinfo.Id, qinfo.SourceSpellID);
                        qinfo.SourceSpellID = 0;                        // quest can't be done for this requirement
                    }
                }

                foreach (QuestObjective obj in qinfo.Objectives)
                {
                    // Store objective for lookup by id
                    _questObjectives[obj.Id] = obj;

                    // Check storage index for objectives which store data
                    if (obj.StorageIndex < 0)
                    {
                        switch (obj.Type)
                        {
                            case QuestObjectiveType.Monster:
                            case QuestObjectiveType.Item:
                            case QuestObjectiveType.GameObject:
                            case QuestObjectiveType.TalkTo:
                            case QuestObjectiveType.PlayerKills:
                            case QuestObjectiveType.AreaTrigger:
                            case QuestObjectiveType.WinPetBattleAgainstNpc:
                            case QuestObjectiveType.ObtainCurrency:
                                Log.outError(LogFilter.Sql, "Quest {0} objective {1} has invalid StorageIndex = {2} for objective type {3}", qinfo.Id, obj.Id, obj.StorageIndex, obj.Type);
                                break;
                            default:
                                break;
                        }
                    }

                    switch (obj.Type)
                    {
                        case QuestObjectiveType.Item:
                            if (GetItemTemplate((uint)obj.ObjectID) == null)
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing item entry {obj.ObjectID}, quest can't be done.");
                            break;
                        case QuestObjectiveType.Monster:
                            if (GetCreatureTemplate((uint)obj.ObjectID) == null)
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing creature entry {obj.ObjectID}, quest can't be done.");
                            break;
                        case QuestObjectiveType.GameObject:
                            if (GetGameObjectTemplate((uint)obj.ObjectID) == null)
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing gameobject entry {obj.ObjectID}, quest can't be done.");
                            break;
                        case QuestObjectiveType.TalkTo:
                            if (Global.ObjectMgr.GetCreatureTemplate((uint)obj.ObjectID) == null)
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing creature entry {obj.ObjectID}, quest can't be done.");
                            break;
                        case QuestObjectiveType.MinReputation:
                        case QuestObjectiveType.MaxReputation:
                        case QuestObjectiveType.IncreaseReputation:
                            if (!CliDB.FactionStorage.ContainsKey((uint)obj.ObjectID))
                                Log.outError(LogFilter.Sql, "Quest {0} objective {1} has non existing faction id {2}", qinfo.Id, obj.Id, obj.ObjectID);
                            break;
                        case QuestObjectiveType.PlayerKills:
                            if (obj.Amount <= 0)
                                Log.outError(LogFilter.Sql, "Quest {0} objective {1} has invalid player kills count {2}", qinfo.Id, obj.Id, obj.Amount);
                            break;
                        case QuestObjectiveType.Currency:
                        case QuestObjectiveType.HaveCurrency:
                        case QuestObjectiveType.ObtainCurrency:
                            if (!CliDB.CurrencyTypesStorage.ContainsKey((uint)obj.ObjectID))
                                Log.outError(LogFilter.Sql, "Quest {0} objective {1} has non existing currency {2}", qinfo.Id, obj.Id, obj.ObjectID);
                            if (obj.Amount <= 0)
                                Log.outError(LogFilter.Sql, "Quest {0} objective {1} has invalid currency amount {2}", qinfo.Id, obj.Id, obj.Amount);
                            break;
                        case QuestObjectiveType.LearnSpell:
                            if (!Global.SpellMgr.HasSpellInfo((uint)obj.ObjectID, Difficulty.None))
                                Log.outError(LogFilter.Sql, "Quest {0} objective {1} has non existing spell id {2}", qinfo.Id, obj.Id, obj.ObjectID);
                            break;
                        case QuestObjectiveType.WinPetBattleAgainstNpc:
                            if (obj.ObjectID != 0 && Global.ObjectMgr.GetCreatureTemplate((uint)obj.ObjectID) == null)
                                Log.outError(LogFilter.Sql, "Quest {0} objective {1} has non existing creature entry {2}, quest can't be done.", qinfo.Id, obj.Id, obj.ObjectID);
                            break;
                        case QuestObjectiveType.DefeatBattlePet:
                            if (!CliDB.BattlePetSpeciesStorage.ContainsKey((uint)obj.ObjectID))
                                Log.outError(LogFilter.Sql, "Quest {0} objective {1} has non existing battlepet species id {2}", qinfo.Id, obj.Id, obj.ObjectID);
                            break;
                        case QuestObjectiveType.CriteriaTree:
                            if (!CliDB.CriteriaTreeStorage.ContainsKey((uint)obj.ObjectID))
                                Log.outError(LogFilter.Sql, "Quest {0} objective {1} has non existing criteria tree id {2}", qinfo.Id, obj.Id, obj.ObjectID);
                            break;
                        case QuestObjectiveType.AreaTrigger:
                            if (!CliDB.AreaTriggerStorage.ContainsKey((uint)obj.ObjectID) && obj.ObjectID != -1)
                                Log.outError(LogFilter.Sql, "Quest {0} objective {1} has non existing AreaTrigger.db2 id {2}", qinfo.Id, obj.Id, obj.ObjectID);
                            break;
                        case QuestObjectiveType.AreaTriggerEnter:
                        case QuestObjectiveType.AreaTriggerExit:
                            if (Global.AreaTriggerDataStorage.GetAreaTriggerTemplate(new AreaTriggerId((uint)obj.ObjectID, false)) == null && Global.AreaTriggerDataStorage.GetAreaTriggerTemplate(new AreaTriggerId((uint)obj.ObjectID, true)) != null)
                                Log.outError(LogFilter.Sql, "Quest {0} objective {1} has non existing areatrigger id {2}", qinfo.Id, obj.Id, obj.ObjectID);
                            break;
                        case QuestObjectiveType.Money:
                        case QuestObjectiveType.WinPvpPetBattles:
                            break;
                        default:
                            Log.outError(LogFilter.Sql, "Quest {0} objective {1} has unhandled type {2}", qinfo.Id, obj.Id, obj.Type);
                            break;
                    }

                    if (obj.Flags.HasAnyFlag(QuestObjectiveFlags.Sequenced))
                        qinfo.SetSpecialFlag(QuestSpecialFlags.SequencedObjectives);
                }

                for (var j = 0; j < SharedConst.QuestItemDropCount; j++)
                {
                    var id = qinfo.ItemDrop[j];
                    if (id != 0)
                    {
                        if (GetItemTemplate(id) == null)
                        {
                            Log.outError(LogFilter.Sql, "Quest {0} has `RequiredSourceItemId{1}` = {2} but item with entry {2} does not exist, quest can't be done.",
                                qinfo.Id, j + 1, id);
                            // no changes, quest can't be done for this requirement
                        }
                    }
                    else
                    {
                        if (qinfo.ItemDropQuantity[j] > 0)
                        {
                            Log.outError(LogFilter.Sql, "Quest {0} has `RequiredSourceItemId{1}` = 0 but `RequiredSourceItemCount{1}` = {2}.",
                                qinfo.Id, j + 1, qinfo.ItemDropQuantity[j]);
                            // no changes, quest ignore this data
                        }
                    }
                }

                for (var j = 0; j < SharedConst.QuestRewardChoicesCount; ++j)
                {
                    var id = qinfo.RewardChoiceItemId[j];
                    if (id != 0)
                    {
                        switch (qinfo.RewardChoiceItemType[j])
                        {
                            case LootItemType.Item:
                                if (Global.ObjectMgr.GetItemTemplate(id) == null)
                                {
                                    Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardChoiceItemId{j + 1}` = {id} but item with entry {id} does not exist, quest will not reward this item.");
                                    qinfo.RewardChoiceItemId[j] = 0;          // no changes, quest will not reward this
                                }
                                break;
                            case LootItemType.Currency:
                                if (!CliDB.CurrencyTypesStorage.HasRecord(id))
                                {
                                    Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardChoiceItemId{j + 1}` = {id} but currency with id {id} does not exist, quest will not reward this currency.");
                                    qinfo.RewardChoiceItemId[j] = 0;          // no changes, quest will not reward this
                                }
                                break;
                            default:
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardChoiceItemType{j + 1}` = {qinfo.RewardChoiceItemType[j]} but it is not a valid item type, reward removed.");
                                qinfo.RewardChoiceItemId[j] = 0;
                                break;
                        }

                        if (qinfo.RewardChoiceItemCount[j] == 0)
                            Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardChoiceItemId{j + 1}` = {id} but `RewardChoiceItemCount{j + 1}` = 0, quest can't be done.");
                    }
                    else if (qinfo.RewardChoiceItemCount[j] > 0)
                    {
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardChoiceItemId{j + 1}` = 0 but `RewardChoiceItemCount{j + 1}` = {qinfo.RewardChoiceItemCount[j]}.");
                        // no changes, quest ignore this data
                    }
                }

                for (var j = 0; j < SharedConst.QuestRewardItemCount; ++j)
                {
                    var id = qinfo.RewardItemId[j];
                    if (id != 0)
                    {
                        if (Global.ObjectMgr.GetItemTemplate(id) == null)
                        {
                            Log.outError(LogFilter.Sql, "Quest {0} has `RewardItemId{1}` = {2} but item with entry {3} does not exist, quest will not reward this item.",
                                qinfo.Id, j + 1, id, id);
                            qinfo.RewardItemId[j] = 0;                // no changes, quest will not reward this item
                        }

                        if (qinfo.RewardItemCount[j] == 0)
                        {
                            Log.outError(LogFilter.Sql, "Quest {0} has `RewardItemId{1}` = {2} but `RewardItemIdCount{3}` = 0, quest will not reward this item.",
                                qinfo.Id, j + 1, id, j + 1);
                            // no changes
                        }
                    }
                    else if (qinfo.RewardItemCount[j] > 0)
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `RewardItemId{1}` = 0 but `RewardItemIdCount{2}` = {3}.",
                            qinfo.Id, j + 1, j + 1, qinfo.RewardItemCount[j]);
                        // no changes, quest ignore this data
                    }
                }

                for (var j = 0; j < SharedConst.QuestRewardReputationsCount; ++j)
                {
                    if (qinfo.RewardFactionId[j] != 0)
                    {
                        if (Math.Abs(qinfo.RewardFactionValue[j]) > 9)
                        {
                            Log.outError(LogFilter.Sql, "Quest {0} has RewardFactionValueId{1} = {2}. That is outside the range of valid values (-9 to 9).", qinfo.Id, j + 1, qinfo.RewardFactionValue[j]);
                        }
                        if (!CliDB.FactionStorage.ContainsKey(qinfo.RewardFactionId[j]))
                        {
                            Log.outError(LogFilter.Sql, "Quest {0} has `RewardFactionId{1}` = {2} but raw faction (faction.dbc) {3} does not exist, quest will not reward reputation for this faction.",
                                qinfo.Id, j + 1, qinfo.RewardFactionId[j], qinfo.RewardFactionId[j]);
                            qinfo.RewardFactionId[j] = 0;            // quest will not reward this
                        }
                    }

                    else if (qinfo.RewardFactionOverride[j] != 0)
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `RewardFactionId{1}` = 0 but `RewardFactionValueIdOverride{2}` = {3}.",
                            qinfo.Id, j + 1, j + 1, qinfo.RewardFactionOverride[j]);
                        // no changes, quest ignore this data
                    }
                }

                if (qinfo.RewardSpell > 0)
                {
                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(qinfo.RewardSpell, Difficulty.None);
                    if (spellInfo == null)
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `RewardSpellCast` = {1} but spell {2} does not exist, quest will not have a spell reward.",
                            qinfo.Id, qinfo.RewardSpell, qinfo.RewardSpell);
                        qinfo.RewardSpell = 0;                    // no spell will be casted on player
                    }

                    else if (!Global.SpellMgr.IsSpellValid(spellInfo))
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `RewardSpellCast` = {1} but spell {2} is broken, quest will not have a spell reward.",
                            qinfo.Id, qinfo.RewardSpell, qinfo.RewardSpell);
                        qinfo.RewardSpell = 0;                    // no spell will be casted on player
                    }
                }

                if (qinfo.RewardMailTemplateId != 0)
                {
                    if (!CliDB.MailTemplateStorage.ContainsKey(qinfo.RewardMailTemplateId))
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `RewardMailTemplateId` = {1} but mail template {2} does not exist, quest will not have a mail reward.",
                            qinfo.Id, qinfo.RewardMailTemplateId, qinfo.RewardMailTemplateId);
                        qinfo.RewardMailTemplateId = 0;               // no mail will send to player
                        qinfo.RewardMailDelay = 0;                // no mail will send to player
                        qinfo.RewardMailSenderEntry = 0;
                    }
                    else if (usedMailTemplates.ContainsKey(qinfo.RewardMailTemplateId))
                    {
                        var usedId = usedMailTemplates.LookupByKey(qinfo.RewardMailTemplateId);
                        Log.outError(LogFilter.Sql, "Quest {0} has `RewardMailTemplateId` = {1} but mail template  {2} already used for quest {3}, quest will not have a mail reward.",
                            qinfo.Id, qinfo.RewardMailTemplateId, qinfo.RewardMailTemplateId, usedId);
                        qinfo.RewardMailTemplateId = 0;               // no mail will send to player
                        qinfo.RewardMailDelay = 0;                // no mail will send to player
                        qinfo.RewardMailSenderEntry = 0;
                    }
                    else
                        usedMailTemplates[qinfo.RewardMailTemplateId] = qinfo.Id;
                }

                if (qinfo.NextQuestInChain != 0)
                {
                    if (!_questTemplates.ContainsKey(qinfo.NextQuestInChain))
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `NextQuestIdChain` = {1} but quest {2} does not exist, quest chain will not work.",
                            qinfo.Id, qinfo.NextQuestInChain, qinfo.NextQuestInChain);
                        qinfo.NextQuestInChain = 0;
                    }
                }

                for (var j = 0; j < SharedConst.QuestRewardCurrencyCount; ++j)
                {
                    if (qinfo.RewardCurrencyId[j] != 0)
                    {
                        if (qinfo.RewardCurrencyCount[j] == 0)
                        {
                            Log.outError(LogFilter.Sql, "Quest {0} has `RewardCurrencyId{1}` = {2} but `RewardCurrencyCount{3}` = 0, quest can't be done.",
                                qinfo.Id, j + 1, qinfo.RewardCurrencyId[j], j + 1);
                            // no changes, quest can't be done for this requirement
                        }

                        if (!CliDB.CurrencyTypesStorage.ContainsKey(qinfo.RewardCurrencyId[j]))
                        {
                            Log.outError(LogFilter.Sql, "Quest {0} has `RewardCurrencyId{1}` = {2} but currency with entry {3} does not exist, quest can't be done.",
                                qinfo.Id, j + 1, qinfo.RewardCurrencyId[j], qinfo.RewardCurrencyId[j]);
                            qinfo.RewardCurrencyCount[j] = 0;             // prevent incorrect work of quest
                        }
                    }
                    else if (qinfo.RewardCurrencyCount[j] > 0)
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `RewardCurrencyId{1}` = 0 but `RewardCurrencyCount{2}` = {3}, quest can't be done.",
                            qinfo.Id, j + 1, j + 1, qinfo.RewardCurrencyCount[j]);
                        qinfo.RewardCurrencyCount[j] = 0;                 // prevent incorrect work of quest
                    }
                }

                if (qinfo.SoundAccept != 0)
                {
                    if (!CliDB.SoundKitStorage.ContainsKey(qinfo.SoundAccept))
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `SoundAccept` = {1} but sound {2} does not exist, set to 0.",
                            qinfo.Id, qinfo.SoundAccept, qinfo.SoundAccept);
                        qinfo.SoundAccept = 0;                        // no sound will be played
                    }
                }

                if (qinfo.SoundTurnIn != 0)
                {
                    if (!CliDB.SoundKitStorage.ContainsKey(qinfo.SoundTurnIn))
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `SoundTurnIn` = {1} but sound {2} does not exist, set to 0.",
                            qinfo.Id, qinfo.SoundTurnIn, qinfo.SoundTurnIn);
                        qinfo.SoundTurnIn = 0;                        // no sound will be played
                    }
                }

                if (qinfo.RewardSkillId > 0)
                {
                    if (!CliDB.SkillLineStorage.ContainsKey(qinfo.RewardSkillId))
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `RewardSkillId` = {1} but this skill does not exist",
                            qinfo.Id, qinfo.RewardSkillId);
                    }
                    if (qinfo.RewardSkillPoints == 0)
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `RewardSkillId` = {1} but `RewardSkillPoints` is 0",
                            qinfo.Id, qinfo.RewardSkillId);
                    }
                }

                if (qinfo.RewardSkillPoints != 0)
                {
                    if (qinfo.RewardSkillPoints > Global.WorldMgr.GetConfigMaxSkillValue())
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `RewardSkillPoints` = {1} but max possible skill is {2}, quest can't be done.",
                            qinfo.Id, qinfo.RewardSkillPoints, Global.WorldMgr.GetConfigMaxSkillValue());
                        // no changes, quest can't be done for this requirement
                    }
                    if (qinfo.RewardSkillId == 0)
                    {
                        Log.outError(LogFilter.Sql, "Quest {0} has `RewardSkillPoints` = {1} but `RewardSkillId` is 0",
                            qinfo.Id, qinfo.RewardSkillPoints);
                    }
                }

                // fill additional data stores
                if (qinfo.PrevQuestId != 0)
                {
                    if (!_questTemplates.ContainsKey((uint)Math.Abs(qinfo.PrevQuestId)))
                        Log.outError(LogFilter.Sql, "Quest {0} has PrevQuestId {1}, but no such quest", qinfo.Id, qinfo.PrevQuestId);
                }

                if (qinfo.NextQuestId != 0)
                {
                    var nextquest = _questTemplates.LookupByKey(qinfo.NextQuestId);
                    if (nextquest == null)
                        Log.outError(LogFilter.Sql, "Quest {0} has NextQuestId {1}, but no such quest", qinfo.Id, qinfo.NextQuestId);
                    else
                        nextquest.DependentPreviousQuests.Add(qinfo.Id);
                }

                if (qinfo.ExclusiveGroup != 0)
                    _exclusiveQuestGroups.Add(qinfo.ExclusiveGroup, qinfo.Id);
            }

            // check QUEST_SPECIAL_FLAGS_EXPLORATION_OR_EVENT for spell with SPELL_EFFECT_QUEST_COMPLETE
            foreach (SpellNameRecord spellNameEntry in CliDB.SpellNameStorage.Values)
            {
                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellNameEntry.Id, Difficulty.None);
                if (spellInfo == null)
                    continue;

                foreach (SpellEffectInfo effect in spellInfo.GetEffects())
                {
                    if (effect == null || effect.Effect != SpellEffectName.QuestComplete)
                        continue;

                    uint questId = (uint)effect.MiscValue;
                    Quest quest = GetQuestTemplate(questId);

                    // some quest referenced in spells not exist (outdated spells)
                    if (quest == null)
                        continue;

                    if (!quest.HasSpecialFlag(QuestSpecialFlags.ExplorationOrEvent))
                    {
                        Log.outError(LogFilter.Sql, "Spell (id: {0}) have SPELL_EFFECT_QUEST_COMPLETE for quest {1}, but quest not have flag QUEST_SPECIAL_FLAGS_EXPLORATION_OR_EVENT. " +
                            "Quest flags must be fixed, quest modified to enable objective.", spellInfo.Id, questId);

                        // this will prevent quest completing without objective
                        quest.SetSpecialFlag(QuestSpecialFlags.ExplorationOrEvent);
                    }
                }
            }

            // Make all paragon reward quests repeatable
            foreach (ParagonReputationRecord paragonReputation in CliDB.ParagonReputationStorage.Values)
            {
                Quest quest = GetQuestTemplate((uint)paragonReputation.QuestID);
                if (quest != null)
                    quest.SetSpecialFlag(QuestSpecialFlags.Repeatable);
            }

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} quests definitions in {1} ms", _questTemplates.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadQuestStartersAndEnders()
        {
            Log.outInfo(LogFilter.ServerLoading, "Loading GO Start Quest Data...");
            LoadGameobjectQuestStarters();
            Log.outInfo(LogFilter.ServerLoading, "Loading GO End Quest Data...");
            LoadGameobjectQuestEnders();
            Log.outInfo(LogFilter.ServerLoading, "Loading Creature Start Quest Data...");
            LoadCreatureQuestStarters();
            Log.outInfo(LogFilter.ServerLoading, "Loading Creature End Quest Data...");
            LoadCreatureQuestEnders();
        }
        public void LoadGameobjectQuestStarters()
        {
            LoadQuestRelationsHelper(_goQuestRelations, null, "gameobject_queststarter", true, true);

            foreach (var pair in _goQuestRelations)
            {
                GameObjectTemplate goInfo = GetGameObjectTemplate(pair.Key);
                if (goInfo == null)
                    Log.outError(LogFilter.Sql, "Table `gameobject_queststarter` have data for not existed gameobject entry ({0}) and existed quest {1}", pair.Key, pair.Value);
                else if (goInfo.type != GameObjectTypes.QuestGiver)
                    Log.outError(LogFilter.Sql, "Table `gameobject_queststarter` have data gameobject entry ({0}) for quest {1}, but GO is not GAMEOBJECT_TYPE_QUESTGIVER", pair.Key, pair.Value);
            }
        }
        public void LoadGameobjectQuestEnders()
        {
            LoadQuestRelationsHelper(_goQuestInvolvedRelations, _goQuestInvolvedRelationsReverse, "gameobject_questender", false, true);

            foreach (var pair in _goQuestInvolvedRelations)
            {
                GameObjectTemplate goInfo = GetGameObjectTemplate(pair.Key);
                if (goInfo == null)
                    Log.outError(LogFilter.Sql, "Table `gameobject_questender` have data for not existed gameobject entry ({0}) and existed quest {1}", pair.Key, pair.Value);
                else if (goInfo.type != GameObjectTypes.QuestGiver)
                    Log.outError(LogFilter.Sql, "Table `gameobject_questender` have data gameobject entry ({0}) for quest {1}, but GO is not GAMEOBJECT_TYPE_QUESTGIVER", pair.Key, pair.Value);
            }
        }
        public void LoadCreatureQuestStarters()
        {
            LoadQuestRelationsHelper(_creatureQuestRelations, null, "creature_queststarter", true, false);

            foreach (var pair in _creatureQuestRelations)
            {
                CreatureTemplate cInfo = GetCreatureTemplate(pair.Key);
                if (cInfo == null)
                    Log.outError(LogFilter.Sql, "Table `creature_queststarter` have data for not existed creature entry ({0}) and existed quest {1}", pair.Key, pair.Value);
                else if (!Convert.ToBoolean(cInfo.Npcflag & (uint)NPCFlags.QuestGiver))
                    Log.outError(LogFilter.Sql, "Table `creature_queststarter` has creature entry ({0}) for quest {1}, but npcflag does not include UNIT_NPC_FLAG_QUESTGIVER", pair.Key, pair.Value);
            }
        }
        public void LoadCreatureQuestEnders()
        {
            LoadQuestRelationsHelper(_creatureQuestInvolvedRelations, _creatureQuestInvolvedRelationsReverse, "creature_questender", false, false);

            foreach (var pair in _creatureQuestInvolvedRelations)
            {
                CreatureTemplate cInfo = GetCreatureTemplate(pair.Key);
                if (cInfo == null)
                    Log.outError(LogFilter.Sql, "Table `creature_questender` have data for not existed creature entry ({0}) and existed quest {1}", pair.Key, pair.Value);
                else if (!Convert.ToBoolean(cInfo.Npcflag & (uint)NPCFlags.QuestGiver))
                    Log.outError(LogFilter.Sql, "Table `creature_questender` has creature entry ({0}) for quest {1}, but npcflag does not include UNIT_NPC_FLAG_QUESTGIVER", pair.Key, pair.Value);
            }
        }
        void LoadQuestRelationsHelper(MultiMap<uint, uint> map, MultiMap<uint, uint> reverseMap, string table, bool starter, bool go)
        {
            uint oldMSTime = Time.GetMSTime();

            map.Clear();                                            // need for reload case

            uint count = 0;

            SQLResult result = DB.World.Query("SELECT id, quest, pool_entry FROM {0} qr LEFT JOIN pool_quest pq ON qr.quest = pq.entry", table);

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest relations from `{0}`, table is empty.", table);
                return;
            }

            var poolRelationMap = go ? Global.PoolMgr.mQuestGORelation : Global.PoolMgr.mQuestCreatureRelation;
            if (starter)
                poolRelationMap.Clear();

            do
            {
                uint id = result.Read<uint>(0);
                uint quest = result.Read<uint>(1);
                uint poolId = result.Read<uint>(2);

                if (!_questTemplates.ContainsKey(quest))
                {
                    Log.outError(LogFilter.Sql, "Table `{0}`: Quest {1} listed for entry {2} does not exist.", table, quest, id);
                    continue;
                }

                if (poolId == 0 || !starter)
                {
                    map.Add(id, quest);
                    if (reverseMap != null)
                        reverseMap.Add(quest, id);
                }
                else
                    poolRelationMap.Add(quest, id);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} quest relations from {1} in {2} ms", count, table, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadQuestPOI()
        {
            uint oldMSTime = Time.GetMSTime();

            _questPOIStorage.Clear();                              // need for reload case

            //                                         0        1          2     3               4                 5              6      7        8         9      10             11                 12                           13               14
            SQLResult result = DB.World.Query("SELECT QuestID, BlobIndex, Idx1, ObjectiveIndex, QuestObjectiveID, QuestObjectID, MapID, UiMapID, Priority, Flags, WorldEffectID, PlayerConditionID, NavigationPlayerConditionID, SpawnTrackingID, AlwaysAllowMergingBlobs FROM quest_poi order by QuestID, Idx1");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest POI definitions. DB table `quest_poi` is empty.");
                return;
            }

            Dictionary<uint, MultiMap<int, QuestPOIBlobPoint>> allPoints = new();

            //                                               0        1    2  3  4
            SQLResult pointsResult = DB.World.Query("SELECT QuestID, Idx1, X, Y, Z FROM quest_poi_points ORDER BY QuestID DESC, Idx1, Idx2");
            if (!pointsResult.IsEmpty())
            {
                do
                {
                    uint questId = pointsResult.Read<uint>(0);
                    int Idx1 = pointsResult.Read<int>(1);
                    int x = pointsResult.Read<int>(2);
                    int y = pointsResult.Read<int>(3);
                    int z = pointsResult.Read<int>(4);

                    if (!allPoints.ContainsKey(questId))
                        allPoints[questId] = new MultiMap<int, QuestPOIBlobPoint>();

                    allPoints[questId].Add(Idx1, new QuestPOIBlobPoint(x, y, z));
                } while (pointsResult.NextRow());
            }

            do
            {
                uint questID = (uint)result.Read<int>(0);
                int blobIndex = result.Read<int>(1);
                int idx1 = result.Read<int>(2);
                int objectiveIndex = result.Read<int>(3);
                int questObjectiveID = result.Read<int>(4);
                int questObjectID = result.Read<int>(5);
                int mapID = result.Read<int>(6);
                int uiMapId = result.Read<int>(7);
                int priority = result.Read<int>(8);
                int flags = result.Read<int>(9);
                int worldEffectID = result.Read<int>(10);
                int playerConditionID = result.Read<int>(11);
                int navigationPlayerConditionID = result.Read<int>(12);
                int spawnTrackingID = result.Read<int>(13);
                bool alwaysAllowMergingBlobs = result.Read<bool>(14);

                if (Global.ObjectMgr.GetQuestTemplate(questID) == null)
                    Log.outError(LogFilter.Sql, $"`quest_poi` quest id ({questID}) Idx1 ({idx1}) does not exist in `quest_template`");

                var blobs = allPoints.LookupByKey(questID);
                if (blobs != null)
                {
                    var points = blobs.LookupByKey(idx1);
                    if (!points.Empty())
                    {
                        if (!_questPOIStorage.ContainsKey(questID))
                            _questPOIStorage[questID] = new QuestPOIData(questID);

                        QuestPOIData poiData = _questPOIStorage[questID];
                        poiData.QuestID = questID;
                        poiData.Blobs.Add(new QuestPOIBlobData(blobIndex, objectiveIndex, questObjectiveID, questObjectID, mapID, uiMapId, priority, flags,
                            worldEffectID, playerConditionID, navigationPlayerConditionID, spawnTrackingID, points, alwaysAllowMergingBlobs));
                        continue;
                    }
                }
                Log.outError(LogFilter.Sql, $"Table quest_poi references unknown quest points for quest {questID} POI id {blobIndex}");

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} quest POI definitions in {1} ms", _questPOIStorage.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadQuestAreaTriggers()
        {
            uint oldMSTime = Time.GetMSTime();

            _questAreaTriggerStorage.Clear();                           // need for reload case

            SQLResult result = DB.World.Query("SELECT id, quest FROM areatrigger_involvedrelation");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest trigger points. DB table `areatrigger_involvedrelation` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                ++count;

                uint trigger_ID = result.Read<uint>(0);
                uint quest_ID = result.Read<uint>(1);

                AreaTriggerRecord atEntry = CliDB.AreaTriggerStorage.LookupByKey(trigger_ID);
                if (atEntry == null)
                {
                    Log.outError(LogFilter.Sql, "Area trigger (ID:{0}) does not exist in `AreaTrigger.dbc`.", trigger_ID);
                    continue;
                }

                Quest quest = GetQuestTemplate(quest_ID);

                if (quest == null)
                {
                    Log.outError(LogFilter.Sql, "Table `areatrigger_involvedrelation` has record (id: {0}) for not existing quest {1}", trigger_ID, quest_ID);
                    continue;
                }

                if (!quest.HasSpecialFlag(QuestSpecialFlags.ExplorationOrEvent))
                {
                    Log.outError(LogFilter.Sql, "Table `areatrigger_involvedrelation` has record (id: {0}) for not quest {1}, but quest not have flag QUEST_SPECIAL_FLAGS_EXPLORATION_OR_EVENT. Trigger or quest flags must be fixed, quest modified to require objective.", trigger_ID, quest_ID);

                    // this will prevent quest completing without objective
                    quest.SetSpecialFlag(QuestSpecialFlags.ExplorationOrEvent);

                    // continue; - quest modified to required objective and trigger can be allowed.
                }

                _questAreaTriggerStorage.Add(trigger_ID, quest_ID);

            } while (result.NextRow());

            foreach (var pair in _questObjectives)
            {
                QuestObjective objective = pair.Value;
                if (objective.Type == QuestObjectiveType.AreaTrigger)
                    _questAreaTriggerStorage.Add((uint)objective.ObjectID, objective.QuestID);
            }

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} quest trigger points in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadQuestGreetings()
        {
            uint oldMSTime = Time.GetMSTime();

            for (var i = 0; i < 2; ++i)
                _questGreetingStorage[i] = new Dictionary<uint, QuestGreeting>();

            //                                         0   1          2                3     
            SQLResult result = DB.World.Query("SELECT ID, type, GreetEmoteType, GreetEmoteDelay, Greeting FROM quest_greeting");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 npc texts, table is empty!");
                return;
            }

            uint count = 0;
            do
            {
                uint id = result.Read<uint>(0);
                byte type = result.Read<byte>(1);

                switch (type)
                {
                    case 0: // Creature
                        if (Global.ObjectMgr.GetCreatureTemplate(id) == null)
                        {
                            Log.outError(LogFilter.Sql, "Table `quest_greeting`: creature template entry {0} does not exist.", id);
                            continue;
                        }
                        break;
                    case 1: // GameObject
                        if (Global.ObjectMgr.GetGameObjectTemplate(id) == null)
                        {
                            Log.outError(LogFilter.Sql, "Table `quest_greeting`: gameobject template entry {0} does not exist.", id);
                            continue;
                        }
                        break;
                    default:
                        continue;
                }

                ushort greetEmoteType = result.Read<ushort>(2);
                uint greetEmoteDelay = result.Read<uint>(3);
                string greeting = result.Read<string>(4);

                _questGreetingStorage[type][id] = new QuestGreeting(greetEmoteType, greetEmoteDelay, greeting);
                count++;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} quest_greeting in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }

        public Quest GetQuestTemplate(uint questId)
        {
            return _questTemplates.LookupByKey(questId);
        }
        public Dictionary<uint, Quest> GetQuestTemplates()
        {
            return _questTemplates;
        }
        public List<Quest> GetQuestTemplatesAutoPush()
        {
            return _questTemplatesAutoPush;
        }
        public MultiMap<uint, uint> GetGOQuestRelationMap()
        {
            return _goQuestRelations;
        }
        public List<uint> GetGOQuestRelationBounds(uint go_entry)
        {
            return _goQuestRelations.LookupByKey(go_entry);
        }
        public List<uint> GetGOQuestInvolvedRelationBounds(uint go_entry)
        {
            return _goQuestInvolvedRelations.LookupByKey(go_entry);
        }
        public List<uint> GetGOQuestInvolvedRelationReverseBounds(uint questId)
        {
            return _goQuestInvolvedRelationsReverse.LookupByKey(questId);
        }
        public MultiMap<uint, uint> GetCreatureQuestRelationMap()
        {
            return _creatureQuestRelations;
        }
        public List<uint> GetCreatureQuestRelationBounds(uint creature_entry)
        {
            return _creatureQuestRelations.LookupByKey(creature_entry);
        }
        public List<uint> GetCreatureQuestInvolvedRelationBounds(uint creature_entry)
        {
            return _creatureQuestInvolvedRelations.LookupByKey(creature_entry);
        }
        public List<uint> GetCreatureQuestInvolvedRelationReverseBounds(uint questId)
        {
            return _creatureQuestInvolvedRelationsReverse.LookupByKey(questId);
        }
        public QuestPOIData GetQuestPOIData(uint questId)
        {
            return _questPOIStorage.LookupByKey(questId);
        }
        public QuestObjective GetQuestObjective(uint questObjectiveId)
        {
            return _questObjectives.LookupByKey(questObjectiveId);
        }
        public List<uint> GetQuestsForAreaTrigger(uint triggerId)
        {
            return _questAreaTriggerStorage.LookupByKey(triggerId);
        }
        public QuestGreeting GetQuestGreeting(TypeId type, uint id)
        {
            byte typeIndex;
            if (type == TypeId.Unit)
                typeIndex = 0;
            else if (type == TypeId.GameObject)
                typeIndex = 1;
            else
                return null;

            return _questGreetingStorage[typeIndex].LookupByKey(id);
        }
        public QuestGreetingLocale GetQuestGreetingLocale(TypeId type, uint id)
        {
            byte typeIndex;
            if (type == TypeId.Unit)
                typeIndex = 0;
            else if (type == TypeId.GameObject)
                typeIndex = 1;
            else
                return null;

            return _questGreetingLocaleStorage[typeIndex].LookupByKey(id);
        }
        public List<uint> GetExclusiveQuestGroupBounds(int exclusiveGroupId)
        {
            return _exclusiveQuestGroups.LookupByKey(exclusiveGroupId);
        }        
        
        //Spells /Skills / Phases
        public void LoadPhases()
        {
            foreach (PhaseRecord phase in CliDB.PhaseStorage.Values)
                _phaseInfoById.Add(phase.Id, new PhaseInfoStruct(phase.Id));

            foreach (MapRecord map in CliDB.MapStorage.Values)
                if (map.ParentMapID != -1)
                    _terrainSwapInfoById.Add(map.Id, new TerrainSwapInfo(map.Id));

            Log.outInfo(LogFilter.ServerLoading, "Loading Terrain World Map definitions...");
            LoadTerrainWorldMaps();

            Log.outInfo(LogFilter.ServerLoading, "Loading Terrain Swap Default definitions...");
            LoadTerrainSwapDefaults();

            Log.outInfo(LogFilter.ServerLoading, "Loading Phase Area definitions...");
            LoadAreaPhases();
        }
        public void UnloadPhaseConditions()
        {
            foreach (var pair in _phaseInfoByArea)
                    pair.Value.Conditions.Clear();
        }
        void LoadTerrainWorldMaps()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0               1
            SQLResult result = DB.World.Query("SELECT TerrainSwapMap, UiMapPhaseId  FROM `terrain_worldmap`");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 terrain world maps. DB table `terrain_worldmap` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint mapId = result.Read<uint>(0);
                uint uiMapPhaseId = result.Read<uint>(1);

                if (!CliDB.MapStorage.ContainsKey(mapId))
                {
                    Log.outError(LogFilter.Sql, "TerrainSwapMap {0} defined in `terrain_worldmap` does not exist, skipped.", mapId);
                    continue;
                }

                if (!Global.DB2Mgr.IsUiMapPhase((int)uiMapPhaseId))
                {
                    Log.outError(LogFilter.Sql, $"Phase {uiMapPhaseId} defined in `terrain_worldmap` is not a valid terrain swap phase, skipped.");
                    continue;
                }

                if (!_terrainSwapInfoById.ContainsKey(mapId))
                    _terrainSwapInfoById.Add(mapId, new TerrainSwapInfo());

                TerrainSwapInfo terrainSwapInfo = _terrainSwapInfoById[mapId];
                terrainSwapInfo.Id = mapId;
                terrainSwapInfo.UiMapPhaseIDs.Add(uiMapPhaseId);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} terrain world maps in {1} ms.", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        void LoadTerrainSwapDefaults()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT MapId, TerrainSwapMap FROM `terrain_swap_defaults`");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 terrain swap defaults. DB table `terrain_swap_defaults` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint mapId = result.Read<uint>(0);
                if (!CliDB.MapStorage.ContainsKey(mapId))
                {
                    Log.outError(LogFilter.Sql, "Map {0} defined in `terrain_swap_defaults` does not exist, skipped.", mapId);
                    continue;
                }

                uint terrainSwap = result.Read<uint>(1);
                if (!CliDB.MapStorage.ContainsKey(terrainSwap))
                {
                    Log.outError(LogFilter.Sql, "TerrainSwapMap {0} defined in `terrain_swap_defaults` does not exist, skipped.", terrainSwap);
                    continue;
                }

                TerrainSwapInfo terrainSwapInfo = _terrainSwapInfoById[terrainSwap];
                terrainSwapInfo.Id = terrainSwap;
                _terrainSwapInfoByMap[mapId].Add(terrainSwapInfo);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} terrain swap defaults in {1} ms.", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        void LoadAreaPhases()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0       1
            SQLResult result = DB.World.Query("SELECT AreaId, PhaseId FROM `phase_area`");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 phase areas. DB table `phase_area` is empty.");
                return;
            }

            PhaseInfoStruct getOrCreatePhaseIfMissing(uint phaseId)
            {
                PhaseInfoStruct phaseInfo = _phaseInfoById[phaseId];
                phaseInfo.Id = phaseId;
                return phaseInfo;
            }

            uint count = 0;
            do
            {
                uint area = result.Read<uint>(0);
                uint phaseId = result.Read<uint>(1);

                if (!CliDB.AreaTableStorage.ContainsKey(area))
                {
                    Log.outError(LogFilter.Sql, $"Area {area} defined in `phase_area` does not exist, skipped.");
                    continue;
                }

                if (!CliDB.PhaseStorage.ContainsKey(phaseId))
                {
                    Log.outError(LogFilter.Sql, $"Phase {phaseId} defined in `phase_area` does not exist, skipped.");
                    continue;
                }

                PhaseInfoStruct phase = getOrCreatePhaseIfMissing(phaseId);
                phase.Areas.Add(area);
                _phaseInfoByArea[area].Add(new PhaseAreaInfo(phase));

                ++count;
            } while (result.NextRow());

            foreach (var pair in _phaseInfoByArea)
            {
                uint parentAreaId = pair.Key;
                do
                {
                    AreaTableRecord area = CliDB.AreaTableStorage.LookupByKey(parentAreaId);
                    if (area == null)
                        break;

                    parentAreaId = area.ParentAreaID;
                    if (parentAreaId == 0)
                        break;

                    var parentAreaPhases = _phaseInfoByArea.LookupByKey(parentAreaId);
                    foreach (PhaseAreaInfo parentAreaPhase in parentAreaPhases)
                        if (parentAreaPhase.PhaseInfo.Id == pair.Value.PhaseInfo.Id)
                            parentAreaPhase.SubAreaExclusions.Add(pair.Key);

                } while (true);
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} phase areas in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }
        public void LoadNPCSpellClickSpells()
        {
            uint oldMSTime = Time.GetMSTime();

            _spellClickInfoStorage.Clear();
            //                                           0          1         2            3
            SQLResult result = DB.World.Query("SELECT npc_entry, spell_id, cast_flags, user_type FROM npc_spellclick_spells");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 spellclick spells. DB table `npc_spellclick_spells` is empty.");
                return;
            }

            uint count = 0;

            do
            {
                uint npc_entry = result.Read<uint>(0);
                CreatureTemplate cInfo = GetCreatureTemplate(npc_entry);
                if (cInfo == null)
                {
                    Log.outError(LogFilter.Sql, "Table npc_spellclick_spells references unknown creature_template {0}. Skipping entry.", npc_entry);
                    continue;
                }

                uint spellid = result.Read<uint>(1);
                SpellInfo spellinfo = Global.SpellMgr.GetSpellInfo(spellid, Difficulty.None);
                if (spellinfo == null)
                {
                    Log.outError(LogFilter.Sql, "Table npc_spellclick_spells creature: {0} references unknown spellid {1}. Skipping entry.", npc_entry, spellid);
                    continue;
                }

                SpellClickUserTypes userType = (SpellClickUserTypes)result.Read<byte>(3);
                if (userType >= SpellClickUserTypes.Max)
                    Log.outError(LogFilter.Sql, "Table npc_spellclick_spells creature: {0} references unknown user type {1}. Skipping entry.", npc_entry, userType);

                byte castFlags = result.Read<byte>(2);
                SpellClickInfo info = new();
                info.spellId = spellid;
                info.castFlags = castFlags;
                info.userType = userType;
                _spellClickInfoStorage.Add(npc_entry, info);

                ++count;
            }
            while (result.NextRow());

            // all spellclick data loaded, now we check if there are creatures with NPC_FLAG_SPELLCLICK but with no data
            // NOTE: It *CAN* be the other way around: no spellclick flag but with spellclick data, in case of creature-only vehicle accessories
            var ctc = GetCreatureTemplates();
            foreach (var creature in ctc.Values)
            {
                if (creature.Npcflag.HasAnyFlag((uint)NPCFlags.SpellClick) && !_spellClickInfoStorage.ContainsKey(creature.Entry))
                {
                    Log.outError(LogFilter.Sql, "npc_spellclick_spells: Creature template {0} has UNIT_NPC_FLAG_SPELLCLICK but no data in spellclick table! Removing flag", creature.Entry);
                    creature.Npcflag &= ~(uint)NPCFlags.SpellClick;
                }
            }

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} spellclick definitions in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadFishingBaseSkillLevel()
        {
            uint oldMSTime = Time.GetMSTime();

            _fishingBaseForAreaStorage.Clear();                            // for reload case

            SQLResult result = DB.World.Query("SELECT entry, skill FROM skill_fishing_base_level");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 areas for fishing base skill level. DB table `skill_fishing_base_level` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint entry = result.Read<uint>(0);
                int skill = result.Read<int>(1);

                AreaTableRecord fArea = CliDB.AreaTableStorage.LookupByKey(entry);
                if (fArea == null)
                {
                    Log.outError(LogFilter.Sql, "AreaId {0} defined in `skill_fishing_base_level` does not exist", entry);
                    continue;
                }

                _fishingBaseForAreaStorage[entry] = skill;
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} areas for fishing base skill level in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadSkillTiers()
        {
            uint oldMSTime = Time.GetMSTime();

            _skillTiers.Clear();

            SQLResult result = DB.World.Query("SELECT ID, Value1, Value2, Value3, Value4, Value5, Value6, Value7, Value8, Value9, Value10, " +
                " Value11, Value12, Value13, Value14, Value15, Value16 FROM skill_tiers");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 skill max values. DB table `skill_tiers` is empty.");
                return;
            }

            do
            {
                uint id = result.Read<uint>(0);
                SkillTiersEntry tier = new();
                for (int i = 0; i < SkillConst.MaxSkillStep; ++i)
                    tier.Value[i] = result.Read<uint>(1 + i);

                _skillTiers[id] = tier;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} skill max values in {1} ms", _skillTiers.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        public PhaseInfoStruct GetPhaseInfo(uint phaseId)
        {
            return _phaseInfoById.LookupByKey(phaseId);
        }
        public List<PhaseAreaInfo> GetPhasesForArea(uint areaId)
        {
            return _phaseInfoByArea.LookupByKey(areaId);
        }
        public TerrainSwapInfo GetTerrainSwapInfo(uint terrainSwapId)
        {
            return _terrainSwapInfoById.LookupByKey(terrainSwapId);
        }
        public List<SpellClickInfo> GetSpellClickInfoMapBounds(uint creature_id)
        {
            return _spellClickInfoStorage.LookupByKey(creature_id);
        }
        public int GetFishingBaseSkillLevel(uint entry)
        {
            return _fishingBaseForAreaStorage.LookupByKey(entry);
        }
        public SkillTiersEntry GetSkillTier(uint skillTierId)
        {
            return _skillTiers.LookupByKey(skillTierId);
        }
        public MultiMap<uint, TerrainSwapInfo> GetTerrainSwaps() { return _terrainSwapInfoByMap; }

        //Locales
        public void LoadCreatureLocales()
        {
            uint oldMSTime = Time.GetMSTime();

            _creatureLocaleStorage.Clear(); // need for reload case

            //                                         0      1       2     3        4      5
            SQLResult result = DB.World.Query("SELECT entry, locale, Name, NameAlt, Title, TitleAlt FROM creature_template_locale");

            if (result.IsEmpty())
                return;

            do
            {
                uint id = result.Read<uint>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == Locale.enUS)
                    continue;

                if (!_creatureLocaleStorage.ContainsKey(id))
                    _creatureLocaleStorage[id] = new CreatureLocale();

                CreatureLocale data = _creatureLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.Name);
                AddLocaleString(result.Read<string>(3), locale, data.NameAlt);
                AddLocaleString(result.Read<string>(4), locale, data.Title);
                AddLocaleString(result.Read<string>(5), locale, data.TitleAlt);

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} creature locale strings in {1} ms", _creatureLocaleStorage.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadGameObjectLocales()
        {
            uint oldMSTime = Time.GetMSTime();

            _gameObjectLocaleStorage.Clear(); // need for reload case

            //                                               0      1       2     3               4
            SQLResult result = DB.World.Query("SELECT entry, locale, name, castBarCaption, unk1 FROM gameobject_template_locale");
            if (result.IsEmpty())
                return;

            do
            {
                uint id = result.Read<uint>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == Locale.enUS)
                    continue;

                if (!_gameObjectLocaleStorage.ContainsKey(id))
                    _gameObjectLocaleStorage[id] = new GameObjectLocale();

                GameObjectLocale data = _gameObjectLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.Name);
                AddLocaleString(result.Read<string>(3), locale, data.CastBarCaption);
                AddLocaleString(result.Read<string>(4), locale, data.Unk1);

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} gameobject_template_locale locale strings in {1} ms", _gameObjectLocaleStorage.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadQuestTemplateLocale()
        {
            uint oldMSTime = Time.GetMSTime();

            _questObjectivesLocaleStorage.Clear(); // need for reload case
            //                                         0     1     2           3                 4                5                 6                  7                   8                   9                  10
            SQLResult result = DB.World.Query("SELECT Id, locale, LogTitle, LogDescription, QuestDescription, AreaDescription, PortraitGiverText, PortraitGiverName, PortraitTurnInText, PortraitTurnInName, QuestCompletionLog" +
                " FROM quest_template_locale");
            if (result.IsEmpty())
                return;

            do
            {
                uint id = result.Read<uint>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == Locale.enUS)
                    continue;

                if (!_questTemplateLocaleStorage.ContainsKey(id))
                    _questTemplateLocaleStorage[id] = new QuestTemplateLocale();

                QuestTemplateLocale data = _questTemplateLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.LogTitle);
                AddLocaleString(result.Read<string>(3), locale, data.LogDescription);
                AddLocaleString(result.Read<string>(4), locale, data.QuestDescription);
                AddLocaleString(result.Read<string>(5), locale, data.AreaDescription);
                AddLocaleString(result.Read<string>(6), locale, data.PortraitGiverText);
                AddLocaleString(result.Read<string>(7), locale, data.PortraitGiverName);
                AddLocaleString(result.Read<string>(8), locale, data.PortraitTurnInText);
                AddLocaleString(result.Read<string>(9), locale, data.PortraitTurnInName);
                AddLocaleString(result.Read<string>(10), locale, data.QuestCompletionLog);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} Quest Tempalate locale strings in {1} ms", _questTemplateLocaleStorage.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadQuestObjectivesLocale()
        {
            uint oldMSTime = Time.GetMSTime();

            _questObjectivesLocaleStorage.Clear(); // need for reload case
            //                                        0     1          2
            SQLResult result = DB.World.Query("SELECT Id, locale, Description FROM quest_objectives_locale");
            if (result.IsEmpty())
                return;

            do
            {
                uint id = result.Read<uint>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == Locale.enUS)
                    continue;

                if (!_questObjectivesLocaleStorage.ContainsKey(id))
                    _questObjectivesLocaleStorage[id] = new QuestObjectivesLocale();

                QuestObjectivesLocale data = _questObjectivesLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.Description);
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} Quest Objectives locale strings in {1} ms", _questObjectivesLocaleStorage.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadQuestGreetingLocales()
        {
            uint oldMSTime = Time.GetMSTime();

            for (var i = 0; i < 2; ++i)
                _questGreetingLocaleStorage[i] = new Dictionary<uint, QuestGreetingLocale>();

            //                                         0   1     2       3
            SQLResult result = DB.World.Query("SELECT Id, type, locale, Greeting FROM quest_greeting_locale");
            if (result.IsEmpty())
                return;

            uint count = 0;
            do
            {
                uint id = result.Read<uint>(0);
                byte type = result.Read<byte>(1);
                switch (type)
                {
                    case 0: // Creature
                        if (GetCreatureTemplate(id) == null)
                        {
                            Log.outError(LogFilter.Sql, $"Table `quest_greeting_locale`: creature template entry {id} does not exist.");
                            continue;
                        }
                        break;
                    case 1: // GameObject
                        if (GetGameObjectTemplate(id) == null)
                        {
                            Log.outError(LogFilter.Sql, $"Table `quest_greeting_locale`: gameobject template entry {id} does not exist.");
                            continue;
                        }
                        break;
                    default:
                        continue;
                }

                string localeName = result.Read<string>(2);

                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == Locale.enUS)
                    continue;

                if (!_questGreetingLocaleStorage[type].ContainsKey(id))
                    _questGreetingLocaleStorage[type][id] = new QuestGreetingLocale();

                QuestGreetingLocale data = _questGreetingLocaleStorage[type][id];
                AddLocaleString(result.Read<string>(3), locale, data.Greeting);
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} Quest Greeting locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }
        public void LoadQuestOfferRewardLocale()
        {
            uint oldMSTime = Time.GetMSTime();

            _questOfferRewardLocaleStorage.Clear(); // need for reload case
                                                    //                                               0     1          2
            SQLResult result = DB.World.Query("SELECT Id, locale, RewardText FROM quest_offer_reward_locale");
            if (result.IsEmpty())
                return;

            do
            {
                uint id = result.Read<uint>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == Locale.enUS)
                    continue;

                if (!_questOfferRewardLocaleStorage.ContainsKey(id))
                    _questOfferRewardLocaleStorage[id] = new QuestOfferRewardLocale();

                QuestOfferRewardLocale data = _questOfferRewardLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.RewardText);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} Quest Offer Reward locale strings in {1} ms", _questOfferRewardLocaleStorage.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadQuestRequestItemsLocale()
        {
            uint oldMSTime = Time.GetMSTime();

            _questRequestItemsLocaleStorage.Clear(); // need for reload case
                                                   //                                               0     1          2
            SQLResult result = DB.World.Query("SELECT Id, locale, CompletionText FROM quest_request_items_locale");
            if (result.IsEmpty())
                return;

            do
            {
                uint id = result.Read<uint>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == Locale.enUS)
                    continue;

                if (!_questRequestItemsLocaleStorage.ContainsKey(id))
                    _questRequestItemsLocaleStorage[id] = new QuestRequestItemsLocale();

                QuestRequestItemsLocale data = _questRequestItemsLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.CompletionText);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} Quest Request Items locale strings in {1} ms", _questRequestItemsLocaleStorage.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadGossipMenuItemsLocales()
        {
            uint oldMSTime = Time.GetMSTime();

            _gossipMenuItemsLocaleStorage.Clear();                              // need for reload case

            //                                         0       1            2       3           4
            SQLResult result = DB.World.Query("SELECT MenuId, OptionIndex, Locale, OptionText, BoxText FROM gossip_menu_option_locale");
            if (result.IsEmpty())
                return;

            do
            {
                uint menuId = result.Read<uint>(0);
                uint optionIndex = result.Read<uint>(1);
                string localeName = result.Read<string>(2);

                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == Locale.enUS)
                    continue;

                GossipMenuItemsLocale data = new();
                AddLocaleString(result.Read<string>(3), locale, data.OptionText);
                AddLocaleString(result.Read<string>(4), locale, data.BoxText);

                _gossipMenuItemsLocaleStorage[Tuple.Create(menuId, optionIndex)] = data;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} gossip_menu_option locale strings in {1} ms", _gossipMenuItemsLocaleStorage.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadPageTextLocales()
        {
            uint oldMSTime = Time.GetMSTime();

            _pageTextLocaleStorage.Clear(); // needed for reload case

            //                                               0      1     2
            SQLResult result = DB.World.Query("SELECT ID, locale, Text FROM page_text_locale");
            if (result.IsEmpty())
                return;

            do
            {
                uint id = result.Read<uint>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == Locale.enUS)
                    continue;

                if (!_pageTextLocaleStorage.ContainsKey(id))
                    _pageTextLocaleStorage[id] = new PageTextLocale();

                PageTextLocale data = _pageTextLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.Text);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} PageText locale strings in {1} ms", _pageTextLocaleStorage.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadPointOfInterestLocales()
        {
            uint oldMSTime = Time.GetMSTime();

            _pointOfInterestLocaleStorage.Clear(); // need for reload case

            //                                        0      1      2
            SQLResult result = DB.World.Query("SELECT ID, locale, Name FROM points_of_interest_locale");
            if (result.IsEmpty())
                return;

            do
            {
                uint id = result.Read<uint>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == Locale.enUS)
                    continue;

                if (!_pointOfInterestLocaleStorage.ContainsKey(id))
                    _pointOfInterestLocaleStorage[id] = new PointOfInterestLocale();

                PointOfInterestLocale data = _pointOfInterestLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.Name);
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} points_of_interest locale strings in {1} ms", _pointOfInterestLocaleStorage.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        public CreatureLocale GetCreatureLocale(uint entry)
        {
            return _creatureLocaleStorage.LookupByKey(entry);
        }
        public GameObjectLocale GetGameObjectLocale(uint entry)
        {
            return _gameObjectLocaleStorage.LookupByKey(entry);
        }
        public QuestTemplateLocale GetQuestLocale(uint entry)
        {
            return _questTemplateLocaleStorage.LookupByKey(entry);
        }
        public QuestOfferRewardLocale GetQuestOfferRewardLocale(uint entry)
        {
            return _questOfferRewardLocaleStorage.LookupByKey(entry);
        }
        public QuestRequestItemsLocale GetQuestRequestItemsLocale(uint entry)
        {
            return _questRequestItemsLocaleStorage.LookupByKey(entry);
        }
        public QuestObjectivesLocale GetQuestObjectivesLocale(uint entry)
        {
            return _questObjectivesLocaleStorage.LookupByKey(entry);
        }
        public GossipMenuItemsLocale GetGossipMenuItemsLocale(uint menuId, uint optionIndex)
        {
            return _gossipMenuItemsLocaleStorage.LookupByKey(Tuple.Create(menuId, optionIndex));
        }
        public PageTextLocale GetPageTextLocale(uint entry)
        {
            return _pageTextLocaleStorage.LookupByKey(entry);
        }
        public PointOfInterestLocale GetPointOfInterestLocale(uint id)
        {
            return _pointOfInterestLocaleStorage.LookupByKey(id);
        }

        //General
        public void LoadReputationRewardRate()
        {
            uint oldMSTime = Time.GetMSTime();

            _repRewardRateStorage.Clear();                             // for reload case

            //                                          0          1             2                  3                  4                 5                      6             7
            SQLResult result = DB.World.Query("SELECT faction, quest_rate, quest_daily_rate, quest_weekly_rate, quest_monthly_rate, quest_repeatable_rate, creature_rate, spell_rate FROM reputation_reward_rate");
            if (result.IsEmpty())
            {
                Log.outError(LogFilter.ServerLoading, "Loaded `reputation_reward_rate`, table is empty!");
                return;
            }
            uint count = 0;
            do
            {
                uint factionId = result.Read<uint>(0);

                RepRewardRate repRate = new();

                repRate.questRate = result.Read<float>(1);
                repRate.questDailyRate = result.Read<float>(2);
                repRate.questWeeklyRate = result.Read<float>(3);
                repRate.questMonthlyRate = result.Read<float>(4);
                repRate.questRepeatableRate = result.Read<float>(5);
                repRate.creatureRate = result.Read<float>(6);
                repRate.spellRate = result.Read<float>(7);

                FactionRecord factionEntry = CliDB.FactionStorage.LookupByKey(factionId);
                if (factionEntry == null)
                {
                    Log.outError(LogFilter.Sql, "Faction (faction.dbc) {0} does not exist but is used in `reputation_reward_rate`", factionId);
                    continue;
                }

                if (repRate.questRate < 0.0f)
                {
                    Log.outError(LogFilter.Sql, "Table reputation_reward_rate has quest_rate with invalid rate {0}, skipping data for faction {1}", repRate.questRate, factionId);
                    continue;
                }

                if (repRate.questDailyRate < 0.0f)
                {
                    Log.outError(LogFilter.Sql, "Table reputation_reward_rate has quest_daily_rate with invalid rate {0}, skipping data for faction {1}", repRate.questDailyRate, factionId);
                    continue;
                }

                if (repRate.questWeeklyRate < 0.0f)
                {
                    Log.outError(LogFilter.Sql, "Table reputation_reward_rate has quest_weekly_rate with invalid rate {0}, skipping data for faction {1}", repRate.questWeeklyRate, factionId);
                    continue;
                }

                if (repRate.questMonthlyRate < 0.0f)
                {
                    Log.outError(LogFilter.Sql, "Table reputation_reward_rate has quest_monthly_rate with invalid rate {0}, skipping data for faction {1}", repRate.questMonthlyRate, factionId);
                    continue;
                }

                if (repRate.questRepeatableRate < 0.0f)
                {
                    Log.outError(LogFilter.Sql, "Table reputation_reward_rate has quest_repeatable_rate with invalid rate {0}, skipping data for faction {1}", repRate.questRepeatableRate, factionId);
                    continue;
                }

                if (repRate.creatureRate < 0.0f)
                {
                    Log.outError(LogFilter.Sql, "Table reputation_reward_rate has creature_rate with invalid rate {0}, skipping data for faction {1}", repRate.creatureRate, factionId);
                    continue;
                }

                if (repRate.spellRate < 0.0f)
                {
                    Log.outError(LogFilter.Sql, "Table reputation_reward_rate has spell_rate with invalid rate {0}, skipping data for faction {1}", repRate.spellRate, factionId);
                    continue;
                }

                _repRewardRateStorage[factionId] = repRate;

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} reputation_reward_rate in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadReputationOnKill()
        {
            uint oldMSTime = Time.GetMSTime();

            // For reload case
            _repOnKillStorage.Clear();

            //                                                0            1                     2
            SQLResult result = DB.World.Query("SELECT creature_id, RewOnKillRepFaction1, RewOnKillRepFaction2, " +
                //   3             4             5                   6             7             8                   9
                "IsTeamAward1, MaxStanding1, RewOnKillRepValue1, IsTeamAward2, MaxStanding2, RewOnKillRepValue2, TeamDependent " +
                "FROM creature_onkill_reputation");

            if (result.IsEmpty())
            {
                Log.outError(LogFilter.ServerLoading, "oaded 0 creature award reputation definitions. DB table `creature_onkill_reputation` is empty.");
                return;
            }
            uint count = 0;
            do
            {
                uint creature_id = result.Read<uint>(0);

                ReputationOnKillEntry repOnKill = new();
                repOnKill.RepFaction1 = result.Read<ushort>(1);
                repOnKill.RepFaction2 = result.Read<ushort>(2);
                repOnKill.IsTeamAward1 = result.Read<bool>(3);
                repOnKill.ReputationMaxCap1 = result.Read<byte>(4);
                repOnKill.RepValue1 = result.Read<int>(5);
                repOnKill.IsTeamAward2 = result.Read<bool>(6);
                repOnKill.ReputationMaxCap2 = result.Read<byte>(7);
                repOnKill.RepValue2 = result.Read<int>(8);
                repOnKill.TeamDependent = result.Read<bool>(9);

                if (GetCreatureTemplate(creature_id) == null)
                {
                    Log.outError(LogFilter.Sql, "Table `creature_onkill_reputation` have data for not existed creature entry ({0}), skipped", creature_id);
                    continue;
                }

                if (repOnKill.RepFaction1 != 0)
                {
                    FactionRecord factionEntry1 = CliDB.FactionStorage.LookupByKey(repOnKill.RepFaction1);
                    if (factionEntry1 == null)
                    {
                        Log.outError(LogFilter.Sql, "Faction (faction.dbc) {0} does not exist but is used in `creature_onkill_reputation`", repOnKill.RepFaction1);
                        continue;
                    }
                }

                if (repOnKill.RepFaction2 != 0)
                {
                    FactionRecord factionEntry2 = CliDB.FactionStorage.LookupByKey(repOnKill.RepFaction2);
                    if (factionEntry2 == null)
                    {
                        Log.outError(LogFilter.Sql, "Faction (faction.dbc) {0} does not exist but is used in `creature_onkill_reputation`", repOnKill.RepFaction2);
                        continue;
                    }
                }

                _repOnKillStorage[creature_id] = repOnKill;

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} creature award reputation definitions in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadReputationSpilloverTemplate()
        {
            var oldMSTime = Time.GetMSTime();

            _repSpilloverTemplateStorage.Clear();                      // for reload case

            //                                        0        1         2       3       4         5       6       7         8       9       10        11      12      13        14      15
            SQLResult result = DB.World.Query("SELECT faction, faction1, rate_1, rank_1, faction2, rate_2, rank_2, faction3, rate_3, rank_3, faction4, rate_4, rank_4, faction5, rate_5, rank_5 FROM " +
            "reputation_spillover_template");
            if (result.IsEmpty())
            {
                Log.outError(LogFilter.ServerLoading, "Loaded `reputation_spillover_template`, table is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint factionId = result.Read<uint>(0);

                RepSpilloverTemplate repTemplate = new();
                repTemplate.faction[0] = result.Read<uint>(1);
                repTemplate.faction_rate[0] = result.Read<float>(2);
                repTemplate.faction_rank[0] = result.Read<uint>(3);
                repTemplate.faction[1] = result.Read<uint>(4);
                repTemplate.faction_rate[1] = result.Read<float>(5);
                repTemplate.faction_rank[1] = result.Read<uint>(6);
                repTemplate.faction[2] = result.Read<uint>(7);
                repTemplate.faction_rate[2] = result.Read<float>(8);
                repTemplate.faction_rank[2] = result.Read<uint>(9);
                repTemplate.faction[3] = result.Read<uint>(10);
                repTemplate.faction_rate[3] = result.Read<float>(11);
                repTemplate.faction_rank[3] = result.Read<uint>(12);
                repTemplate.faction[4] = result.Read<uint>(13);
                repTemplate.faction_rate[4] = result.Read<float>(14);
                repTemplate.faction_rank[4] = result.Read<uint>(15);

                var factionEntry = CliDB.FactionStorage.LookupByKey(factionId);
                if (factionEntry == null)
                {
                    Log.outError(LogFilter.Sql, "Faction (faction.dbc) {0} does not exist but is used in `reputation_spillover_template`", factionId);
                    continue;
                }

                if (factionEntry.ParentFactionID == 0)
                {
                    Log.outError(LogFilter.Sql, "Faction (faction.dbc) {0} in `reputation_spillover_template` does not belong to any team, skipping", factionId);
                    continue;
                }

                bool invalidSpilloverFaction = false;
                for (var i = 0; i < 5; ++i)
                {
                    if (repTemplate.faction[i] != 0)
                    {
                        var factionSpillover = CliDB.FactionStorage.LookupByKey(repTemplate.faction[i]);
                        if (factionSpillover.Id == 0)
                        {
                            Log.outError(LogFilter.Sql, "Spillover faction (faction.dbc) {0} does not exist but is used in `reputation_spillover_template` for faction {1}, skipping", repTemplate.faction[i], factionId);
                            invalidSpilloverFaction = true;
                            break;
                        }

                        if (!factionSpillover.CanHaveReputation())
                        {
                            Log.outError(LogFilter.Sql, "Spillover faction (faction.dbc) {0} for faction {1} in `reputation_spillover_template` can not be listed for client, and then useless, skipping",
                                repTemplate.faction[i], factionId);
                            invalidSpilloverFaction = true;
                            break;
                        }

                        if (repTemplate.faction_rank[i] >= (uint)ReputationRank.Max)
                        {
                            Log.outError(LogFilter.Sql, "Rank {0} used in `reputation_spillover_template` for spillover faction {1} is not valid, skipping", repTemplate.faction_rank[i], repTemplate.faction[i]);
                            invalidSpilloverFaction = true;
                            break;
                        }
                    }
                }

                if (invalidSpilloverFaction)
                    continue;

                _repSpilloverTemplateStorage[factionId] = repTemplate;
                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} reputation_spillover_template in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadTavernAreaTriggers()
        {
            uint oldMSTime = Time.GetMSTime();

            _tavernAreaTriggerStorage.Clear();                          // need for reload case

            SQLResult result = DB.World.Query("SELECT id FROM areatrigger_tavern");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 tavern triggers. DB table `areatrigger_tavern` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                ++count;

                uint Trigger_ID = result.Read<uint>(0);

                AreaTriggerRecord atEntry = CliDB.AreaTriggerStorage.LookupByKey(Trigger_ID);
                if (atEntry == null)
                {
                    Log.outError(LogFilter.Sql, "Area trigger (ID:{0}) does not exist in `AreaTrigger.dbc`.", Trigger_ID);
                    continue;
                }

                _tavernAreaTriggerStorage.Add(Trigger_ID);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} tavern triggers in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadMailLevelRewards()
        {
            uint oldMSTime = Time.GetMSTime();

            _mailLevelRewardStorage.Clear();                           // for reload case

            //                                           0        1             2            3
            SQLResult result = DB.World.Query("SELECT level, raceMask, mailTemplateId, senderEntry FROM mail_level_reward");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 level dependent mail rewards. DB table `mail_level_reward` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                byte level = result.Read<byte>(0);
                ulong raceMask = result.Read<ulong>(1);
                uint mailTemplateId = result.Read<uint>(2);
                uint senderEntry = result.Read<uint>(3);

                if (level > WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                {
                    Log.outError(LogFilter.Sql, "Table `mail_level_reward` have data for level {0} that more supported by client ({1}), ignoring.", level, WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel));
                    continue;
                }

                if (!Convert.ToBoolean(raceMask & SharedConst.RaceMaskAllPlayable))
                {
                    Log.outError(LogFilter.Sql, "Table `mail_level_reward` have raceMask ({0}) for level {1} that not include any player races, ignoring.", raceMask, level);
                    continue;
                }

                if (!CliDB.MailTemplateStorage.ContainsKey(mailTemplateId))
                {
                    Log.outError(LogFilter.Sql, "Table `mail_level_reward` have invalid mailTemplateId ({0}) for level {1} that invalid not include any player races, ignoring.", mailTemplateId, level);
                    continue;
                }

                if (GetCreatureTemplate(senderEntry) == null)
                {
                    Log.outError(LogFilter.Sql, "Table `mail_level_reward` have not existed sender creature entry ({0}) for level {1} that invalid not include any player races, ignoring.", senderEntry, level);
                    continue;
                }

                _mailLevelRewardStorage.Add(level, new MailLevelReward(raceMask, mailTemplateId, senderEntry));

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} level dependent mail rewards in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadExplorationBaseXP()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT level, basexp FROM exploration_basexp");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 BaseXP definitions. DB table `exploration_basexp` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                byte level = result.Read<byte>(0);
                uint basexp = result.Read<uint>(1);
                _baseXPTable[level] = basexp;
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} BaseXP definitions in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadTempSummons()
        {
            uint oldMSTime = Time.GetMSTime();

            _tempSummonDataStorage.Clear();   // needed for reload case

            //                                             0           1             2        3      4           5           6           7            8           9
            SQLResult result = DB.World.Query("SELECT summonerId, summonerType, groupId, entry, position_x, position_y, position_z, orientation, summonType, summonTime FROM creature_summon_groups");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 temp summons. DB table `creature_summon_groups` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint summonerId = result.Read<uint>(0);
                SummonerType summonerType = (SummonerType)result.Read<byte>(1);
                byte group = result.Read<byte>(2);

                switch (summonerType)
                {
                    case SummonerType.Creature:
                        if (GetCreatureTemplate(summonerId) == null)
                        {
                            Log.outError(LogFilter.Sql, "Table `creature_summon_groups` has summoner with non existing entry {0} for creature summoner type, skipped.", summonerId);
                            continue;
                        }
                        break;
                    case SummonerType.GameObject:
                        if (GetGameObjectTemplate(summonerId) == null)
                        {
                            Log.outError(LogFilter.Sql, "Table `creature_summon_groups` has summoner with non existing entry {0} for gameobject summoner type, skipped.", summonerId);
                            continue;
                        }
                        break;
                    case SummonerType.Map:
                        if (!CliDB.MapStorage.ContainsKey(summonerId))
                        {
                            Log.outError(LogFilter.Sql, "Table `creature_summon_groups` has summoner with non existing entry {0} for map summoner type, skipped.", summonerId);
                            continue;
                        }
                        break;
                    default:
                        Log.outError(LogFilter.Sql, "Table `creature_summon_groups` has unhandled summoner type {0} for summoner {1}, skipped.", summonerType, summonerId);
                        continue;
                }

                TempSummonData data = new();
                data.entry = result.Read<uint>(3);

                if (GetCreatureTemplate(data.entry) == null)
                {
                    Log.outError(LogFilter.Sql, "Table `creature_summon_groups` has creature in group [Summoner ID: {0}, Summoner Type: {1}, Group ID: {2}] with non existing creature entry {3}, skipped.",
                        summonerId, summonerType, group, data.entry);
                    continue;
                }

                float posX = result.Read<float>(4);
                float posY = result.Read<float>(5);
                float posZ = result.Read<float>(6);
                float orientation = result.Read<float>(7);

                data.pos = new Position(posX, posY, posZ, orientation);

                data.type = (TempSummonType)result.Read<byte>(8);

                if (data.type > TempSummonType.ManualDespawn)
                {
                    Log.outError(LogFilter.Sql, "Table `creature_summon_groups` has unhandled temp summon type {0} in group [Summoner ID: {1}, Summoner Type: {2}, Group ID: {3}] for creature entry {4}, skipped.",
                        data.type, summonerId, summonerType, group, data.entry);
                    continue;
                }

                data.time = result.Read<uint>(9);

                Tuple<uint, SummonerType, byte> key = Tuple.Create(summonerId, summonerType, group);
                _tempSummonDataStorage.Add(key, data);

                ++count;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} temp summons in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadPageTexts()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0   1     2           3                 4
            SQLResult result = DB.World.Query("SELECT ID, text, NextPageID, PlayerConditionID, Flags FROM page_text");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 page texts. DB table `page_text` is empty!");
                return;
            }

            uint count = 0;
            do
            {
                uint id = result.Read<uint>(0);

                PageText pageText = new();
                pageText.Text = result.Read<string>(1);
                pageText.NextPageID = result.Read<uint>(2);
                pageText.PlayerConditionID = result.Read<int>(3);
                pageText.Flags = result.Read<byte>(4);

                _pageTextStorage[id] = pageText;
                ++count;
            }
            while (result.NextRow());

            foreach (var pair in _pageTextStorage)
            {
                if (pair.Value.NextPageID != 0)
                {
                    if (!_pageTextStorage.ContainsKey(pair.Value.NextPageID))
                        Log.outError(LogFilter.Sql, "Page text (ID: {0}) has non-existing `NextPageID` ({1})", pair.Key, pair.Value.NextPageID);

                }
            }
            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} page texts in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadReservedPlayersNames()
        {
            uint oldMSTime = Time.GetMSTime();

            _reservedNamesStorage.Clear();                                // need for reload case

            SQLResult result = DB.Characters.Query("SELECT name FROM reserved_name");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 reserved player names. DB table `reserved_name` is empty!");
                return;
            }

            uint count = 0;
            do
            {
                string name = result.Read<string>(0);

                _reservedNamesStorage.Add(name.ToLower());
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} reserved player names in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        //not very fast function but it is called only once a day, or on starting-up
        public void ReturnOrDeleteOldMails(bool serverUp)
        {
            uint oldMSTime = Time.GetMSTime();

            long curTime = GameTime.GetGameTime();
            DateTime lt = Time.UnixTimeToDateTime(curTime).ToLocalTime();
            Log.outInfo(LogFilter.Server, "Returning mails current time: hour: {0}, minute: {1}, second: {2} ", lt.Hour, lt.Minute, lt.Second);

            PreparedStatement stmt;
            // Delete all old mails without item and without body immediately, if starting server
            if (!serverUp)
            {
                stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_EMPTY_EXPIRED_MAIL);
                stmt.AddValue(0, curTime);
                DB.Characters.Execute(stmt);
            }
            stmt = DB.Characters.GetPreparedStatement(CharStatements.SEL_EXPIRED_MAIL);
            stmt.AddValue(0, curTime);
            SQLResult result = DB.Characters.Query(stmt);
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "No expired mails found.");
                return;                                             // any mails need to be returned or deleted
            }

            MultiMap<uint, MailItemInfo> itemsCache = new();
            stmt = DB.Characters.GetPreparedStatement(CharStatements.SEL_EXPIRED_MAIL_ITEMS);
            stmt.AddValue(0, curTime);
            SQLResult items = DB.Characters.Query(stmt);
            if (!items.IsEmpty())
            {
                MailItemInfo item = new();
                do
                {
                    item.item_guid = result.Read<uint>(0);
                    item.item_template = result.Read<uint>(1);
                    uint mailId = result.Read<uint>(2);
                    itemsCache.Add(mailId, item);
                } while (items.NextRow());
            }

            uint deletedCount = 0;
            uint returnedCount = 0;
            do
            {
                Mail m = new();
                m.messageID = result.Read<uint>(0);
                m.messageType = (MailMessageType)result.Read<byte>(1);
                m.sender = result.Read<uint>(2);
                m.receiver = result.Read<ulong>(3);
                bool has_items = result.Read<bool>(4);
                m.expire_time = result.Read<long>(5);
                m.deliver_time = 0;
                m.COD = result.Read<ulong>(6);
                m.checkMask = (MailCheckMask)result.Read<byte>(7);
                m.mailTemplateId = result.Read<ushort>(8);

                Player player = null;
                if (serverUp)
                    player = Global.ObjAccessor.FindPlayer(ObjectGuid.Create(HighGuid.Player, m.receiver));

                if (player && player.m_mailsLoaded)
                {                                                   // this code will run very improbably (the time is between 4 and 5 am, in game is online a player, who has old mail
                    // his in mailbox and he has already listed his mails)
                    continue;
                }

                // Delete or return mail
                if (has_items)
                {
                    // read items from cache
                    List<MailItemInfo> temp = itemsCache[m.messageID];
                    Extensions.Swap(ref m.items, ref temp);

                    // if it is mail from non-player, or if it's already return mail, it shouldn't be returned, but deleted
                    if (m.messageType != MailMessageType.Normal || (m.checkMask.HasAnyFlag(MailCheckMask.CodPayment | MailCheckMask.Returned)))
                    {
                        // mail open and then not returned
                        foreach (var itemInfo in m.items)
                        {
                            Item.DeleteFromDB(null, itemInfo.item_guid);
                            AzeriteItem.DeleteFromDB(null, itemInfo.item_guid);
                            AzeriteEmpoweredItem.DeleteFromDB(null, itemInfo.item_guid);
                        }

                        stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_MAIL_ITEM_BY_ID);
                        stmt.AddValue(0, m.messageID);
                        DB.Characters.Execute(stmt);
                    }
                    else
                    {
                        // Mail will be returned
                        stmt = DB.Characters.GetPreparedStatement(CharStatements.UPD_MAIL_RETURNED);
                        stmt.AddValue(0, m.receiver);
                        stmt.AddValue(1, m.sender);
                        stmt.AddValue(2, curTime + 30 * Time.Day);
                        stmt.AddValue(3, curTime);
                        stmt.AddValue(4, (byte)MailCheckMask.Returned);
                        stmt.AddValue(5, m.messageID);
                        DB.Characters.Execute(stmt);
                        foreach (var itemInfo in m.items)
                        {
                            // Update receiver in mail items for its proper delivery, and in instance_item for avoid lost item at sender delete
                            stmt = DB.Characters.GetPreparedStatement(CharStatements.UPD_MAIL_ITEM_RECEIVER);
                            stmt.AddValue(0, m.sender);
                            stmt.AddValue(1, itemInfo.item_guid);
                            DB.Characters.Execute(stmt);

                            stmt = DB.Characters.GetPreparedStatement(CharStatements.UPD_ITEM_OWNER);
                            stmt.AddValue(0, m.sender);
                            stmt.AddValue(1, itemInfo.item_guid);
                            DB.Characters.Execute(stmt);
                        }
                        ++returnedCount;
                        continue;
                    }
                }

                stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_MAIL_BY_ID);
                stmt.AddValue(0, m.messageID);
                DB.Characters.Execute(stmt);
                ++deletedCount;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Processed {0} expired mails: {1} deleted and {2} returned in {3} ms", deletedCount + returnedCount, deletedCount, returnedCount, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadSceneTemplates()
        {
            uint oldMSTime = Time.GetMSTime();
            _sceneTemplateStorage.Clear();

            SQLResult result = DB.World.Query("SELECT SceneId, Flags, ScriptPackageID, Encrypted, ScriptName FROM scene_template");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 scene templates. DB table `scene_template` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                uint sceneId = result.Read<uint>(0);
                SceneTemplate sceneTemplate = new();
                sceneTemplate.SceneId = sceneId;
                sceneTemplate.PlaybackFlags = (SceneFlags)result.Read<uint>(1);
                sceneTemplate.ScenePackageId = result.Read<uint>(2);
                sceneTemplate.Encrypted = result.Read<byte>(3) != 0;
                sceneTemplate.ScriptId = GetScriptId(result.Read<string>(4));

                _sceneTemplateStorage[sceneId] = sceneTemplate;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} scene templates in {1} ms.", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
        public void LoadPlayerChoices()
        {
            uint oldMSTime = Time.GetMSTime();
            _playerChoices.Clear();

            SQLResult choiceResult = DB.World.Query("SELECT ChoiceId, UiTextureKitId, SoundKitId, Question, HideWarboardHeader, KeepOpenAfterChoice FROM playerchoice");
            if (choiceResult.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 player choices. DB table `playerchoice` is empty.");
                return;
            }

            uint responseCount = 0;
            uint rewardCount = 0;
            uint itemRewardCount = 0;
            uint currencyRewardCount = 0;
            uint factionRewardCount = 0;
            uint itemChoiceRewardCount = 0;
            uint mawPowersCount = 0;

            do
            {
                PlayerChoice choice = new();
                choice.ChoiceId = choiceResult.Read<int>(0);
                choice.UiTextureKitId = choiceResult.Read<int>(1);
                choice.SoundKitId = choiceResult.Read<uint>(2);
                choice.Question = choiceResult.Read<string>(3);
                choice.HideWarboardHeader = choiceResult.Read<bool>(4);
                choice.KeepOpenAfterChoice = choiceResult.Read<bool>(5);

                _playerChoices[choice.ChoiceId] = choice;

            } while (choiceResult.NextRow());

            //                                            0         1           2                   3                4      5
            SQLResult responses = DB.World.Query("SELECT ChoiceId, ResponseId, ResponseIdentifier, ChoiceArtFileId, Flags, WidgetSetID, " +
                //6                        7           8        9               10      11      12         13              14           15            16
                "UiTextureAtlasElementID, SoundKitID, GroupID, UiTextureKitID, Answer, Header, SubHeader, ButtonTooltip, Description, Confirmation, RewardQuestID " +
                "FROM playerchoice_response ORDER BY `Index` ASC");
            if (!responses.IsEmpty())
            {
                do
                {
                    int choiceId = responses.Read<int>(0);
                    int responseId = responses.Read<int>(1);

                    if (!_playerChoices.ContainsKey(choiceId))
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response` references non-existing ChoiceId: {choiceId} (ResponseId: {responseId}), skipped");
                        continue;
                    }

                    PlayerChoice choice = _playerChoices[choiceId];
                    PlayerChoiceResponse response = new();

                    response.ResponseId = responseId;
                    response.ResponseIdentifier = responses.Read<ushort>(2);
                    response.ChoiceArtFileId = responses.Read<int>(3);
                    response.Flags = responses.Read<int>(4);
                    response.WidgetSetID = responses.Read<uint>(5);
                    response.UiTextureAtlasElementID = responses.Read<uint>(6);
                    response.SoundKitID = responses.Read<uint>(7);
                    response.GroupID = responses.Read<byte>(8);
                    response.UiTextureKitID = responses.Read<int>(9);
                    response.Answer = responses.Read<string>(10);
                    response.Header = responses.Read<string>(11);
                    response.SubHeader = responses.Read<string>(12);
                    response.ButtonTooltip = responses.Read<string>(13);
                    response.Description = responses.Read<string>(14);
                    response.Confirmation = responses.Read<string>(15);
                    if (!responses.IsNull(16))
                        response.RewardQuestID.Set(responses.Read<uint>(16));

                    choice.Responses.Add(response);

                    ++responseCount;

                } while (responses.NextRow());
            }

            SQLResult rewards = DB.World.Query("SELECT ChoiceId, ResponseId, TitleId, PackageId, SkillLineId, SkillPointCount, ArenaPointCount, HonorPointCount, Money, Xp FROM playerchoice_response_reward");
            if (!rewards.IsEmpty())
            {
                do
                {
                    int choiceId = rewards.Read<int>(0);
                    int responseId = rewards.Read<int>(1);

                    if (!_playerChoices.ContainsKey(choiceId))
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward` references non-existing ChoiceId: {choiceId} (ResponseId: {responseId}), skipped");
                        continue;
                    }

                    PlayerChoice choice = _playerChoices[choiceId];
                    var response = choice.Responses.Find(playerChoiceResponse => playerChoiceResponse.ResponseId == responseId);
                    if (response == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward` references non-existing ResponseId: {responseId} for ChoiceId {choiceId}, skipped");
                        continue;
                    }

                    PlayerChoiceResponseReward reward = new();
                    reward.TitleId = rewards.Read<int>(2);
                    reward.PackageId = rewards.Read<int>(3);
                    reward.SkillLineId = rewards.Read<int>(4);
                    reward.SkillPointCount = rewards.Read<uint>(5);
                    reward.ArenaPointCount = rewards.Read<uint>(6);
                    reward.HonorPointCount = rewards.Read<uint>(7);
                    reward.Money = rewards.Read<ulong>(8);
                    reward.Xp = rewards.Read<uint>(9);

                    if (reward.TitleId != 0 && !CliDB.CharTitlesStorage.ContainsKey(reward.TitleId))
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward` references non-existing Title {reward.TitleId} for ChoiceId {choiceId}, ResponseId: {responseId}, set to 0");
                        reward.TitleId = 0;
                    }

                    if (reward.PackageId != 0 && Global.DB2Mgr.GetQuestPackageItems((uint)reward.PackageId) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward` references non-existing QuestPackage {reward.TitleId} for ChoiceId {choiceId}, ResponseId: {responseId}, set to 0");
                        reward.PackageId = 0;
                    }

                    if (reward.SkillLineId != 0 && !CliDB.SkillLineStorage.ContainsKey(reward.SkillLineId))
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward` references non-existing SkillLine {reward.TitleId} for ChoiceId {choiceId}, ResponseId: {responseId}, set to 0");
                        reward.SkillLineId = 0;
                        reward.SkillPointCount = 0;
                    }

                    response.Reward.Set(reward);
                    ++rewardCount;

                } while (rewards.NextRow());
            }

            SQLResult rewardItem = DB.World.Query("SELECT ChoiceId, ResponseId, ItemId, BonusListIDs, Quantity FROM playerchoice_response_reward_item ORDER BY `Index` ASC");
            if (!rewardItem.IsEmpty())
            {
                do
                {
                    int choiceId = rewardItem.Read<int>(0);
                    int responseId = rewardItem.Read<int>(1);
                    uint itemId = rewardItem.Read<uint>(2);
                    StringArray bonusListIDsTok = new(rewardItem.Read<string>(3), ' ');
                    List<uint> bonusListIds = new();
                    if (!bonusListIDsTok.IsEmpty())
                    {
                        foreach (uint token in bonusListIDsTok)
                            bonusListIds.Add(token);
                    }

                    int quantity = rewardItem.Read<int>(4);

                    PlayerChoice choice = _playerChoices[choiceId];
                    if (choice == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_item` references non-existing ChoiceId: {choiceId} (ResponseId: {responseId}), skipped");
                        continue;
                    }

                    var response = choice.Responses.Find(playerChoiceResponse => playerChoiceResponse.ResponseId == responseId);
                    if (response == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_item` references non-existing ResponseId: {responseId} for ChoiceId {choiceId}, skipped");
                        continue;
                    }

                    if (!response.Reward.HasValue)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_item` references non-existing player choice reward for ChoiceId {choiceId}, ResponseId: {responseId}, skipped");
                        continue;
                    }

                    if (GetItemTemplate(itemId) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_item` references non-existing item {itemId} for ChoiceId {choiceId}, ResponseId: {responseId}, skipped");
                        continue;
                    }

                    response.Reward.Value.Items.Add(new PlayerChoiceResponseRewardItem(itemId, bonusListIds, quantity));
                    itemRewardCount++;

                } while (rewardItem.NextRow());
            }

            SQLResult rewardCurrency = DB.World.Query("SELECT ChoiceId, ResponseId, CurrencyId, Quantity FROM playerchoice_response_reward_currency ORDER BY `Index` ASC");
            if (!rewardCurrency.IsEmpty())
            {
                do
                {
                    int choiceId = rewardCurrency.Read<int>(0);
                    int responseId = rewardCurrency.Read<int>(1);
                    uint currencyId = rewardCurrency.Read<uint>(2);
                    int quantity = rewardCurrency.Read<int>(3);

                    PlayerChoice choice = _playerChoices.LookupByKey(choiceId);
                    if (choice == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_currency` references non-existing ChoiceId: {choiceId} (ResponseId: {responseId}), skipped");
                        continue;
                    }

                    var response = choice.Responses.Find(playerChoiceResponse => playerChoiceResponse.ResponseId == responseId);
                    if (response == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_currency` references non-existing ResponseId: {responseId} for ChoiceId {choiceId}, skipped");
                        continue;
                    }

                    if (!response.Reward.HasValue)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_currency` references non-existing player choice reward for ChoiceId {choiceId}, ResponseId: {responseId}, skipped");
                        continue;
                    }

                    if (!CliDB.CurrencyTypesStorage.ContainsKey(currencyId))
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_currency` references non-existing currency {currencyId} for ChoiceId {choiceId}, ResponseId: {responseId}, skipped");
                        continue;
                    }

                    response.Reward.Value.Currency.Add(new PlayerChoiceResponseRewardEntry(currencyId, quantity));
                    currencyRewardCount++;

                } while (rewardCurrency.NextRow());
            }

            SQLResult rewardFaction = DB.World.Query("SELECT ChoiceId, ResponseId, FactionId, Quantity FROM playerchoice_response_reward_faction ORDER BY `Index` ASC");
            if (!rewardFaction.IsEmpty())
            {
                do
                {
                    int choiceId = rewardFaction.Read<int>(0);
                    int responseId = rewardFaction.Read<int>(1);
                    uint factionId = rewardFaction.Read<uint>(2);
                    int quantity = rewardFaction.Read<int>(3);

                    PlayerChoice choice = _playerChoices.LookupByKey(choiceId);
                    if (choice == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_faction` references non-existing ChoiceId: {choiceId} (ResponseId: {responseId}), skipped");
                        continue;
                    }

                    var response = choice.Responses.Find(playerChoiceResponse => playerChoiceResponse.ResponseId == responseId);
                    if (response == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_faction` references non-existing ResponseId: {responseId} for ChoiceId {choiceId}, skipped");
                        continue;
                    }

                    if (!response.Reward.HasValue)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_faction` references non-existing player choice reward for ChoiceId {choiceId}, ResponseId: {responseId}, skipped");
                        continue;
                    }

                    if (!CliDB.FactionStorage.ContainsKey(factionId))
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_faction` references non-existing faction {factionId} for ChoiceId {choiceId}, ResponseId: {responseId}, skipped");
                        continue;
                    }

                    response.Reward.Value.Faction.Add(new PlayerChoiceResponseRewardEntry(factionId, quantity));
                    factionRewardCount++;

                } while (rewardFaction.NextRow());
            }

            SQLResult rewardItems = DB.World.Query("SELECT ChoiceId, ResponseId, ItemId, BonusListIDs, Quantity FROM playerchoice_response_reward_item_choice ORDER BY `Index` ASC");
            if (!rewardItems.IsEmpty())
            {
                do
                {
                    int choiceId = rewardItems.Read<int>(0);
                    int responseId = rewardItems.Read<int>(1);
                    uint itemId = rewardItems.Read<uint>(2);
                    StringArray bonusListIDsTok = new(rewardItems.Read<string>(3), ' ');
                    List<uint> bonusListIds = new();
                    foreach (string token in bonusListIDsTok)
                        bonusListIds.Add(uint.Parse(token));

                    int quantity = rewardItems.Read<int>(4);

                    PlayerChoice choice = _playerChoices.LookupByKey(choiceId);
                    if (choice == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_item_choice` references non-existing ChoiceId: {choiceId} (ResponseId: {responseId}), skipped");
                        continue;
                    }

                    var response = choice.Responses.Find(playerChoiceResponse => playerChoiceResponse.ResponseId == responseId);
                    if (response == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_item_choice` references non-existing ResponseId: {responseId} for ChoiceId {choiceId}, skipped");
                        continue;
                    }

                    if (!response.Reward.HasValue)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_item_choice` references non-existing player choice reward for ChoiceId {choiceId}, ResponseId: {responseId}, skipped");
                        continue;
                    }

                    if (GetItemTemplate(itemId) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward_item_choice` references non-existing item {itemId} for ChoiceId {choiceId}, ResponseId: {responseId}, skipped");
                        continue;
                    }

                    response.Reward.Value.ItemChoices.Add(new PlayerChoiceResponseRewardItem(itemId, bonusListIds, quantity));
                    itemChoiceRewardCount++;

                } while (rewards.NextRow());
            }

            SQLResult mawPowersResult = DB.World.Query("SELECT ChoiceId, ResponseId, TypeArtFileID, Rarity, RarityColor, SpellID, MaxStacks FROM playerchoice_response_maw_power");
            if (!mawPowersResult.IsEmpty())
            {
                do
                {
                    int choiceId = mawPowersResult.Read<int>(0);
                    int responseId = mawPowersResult.Read<int>(1);

                    PlayerChoice choice = _playerChoices.LookupByKey(choiceId);
                    if (choice == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_maw_power` references non-existing ChoiceId: {choiceId} (ResponseId: {responseId}), skipped");
                        continue;
                    }

                    var response = choice.Responses.Find(playerChoiceResponse => { return playerChoiceResponse.ResponseId == responseId; });
                    if (response == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_maw_power` references non-existing ResponseId: {responseId} for ChoiceId {choiceId}, skipped");
                        continue;
                    }

                    PlayerChoiceResponseMawPower mawPower = new();
                    mawPower.TypeArtFileID = mawPowersResult.Read<int>(2);
                    mawPower.Rarity = mawPowersResult.Read<int>(3);
                    mawPower.RarityColor = mawPowersResult.Read<uint>(4);
                    mawPower.SpellID = mawPowersResult.Read<int>(5);
                    mawPower.MaxStacks = mawPowersResult.Read<int>(6);
                    response.MawPower.Set(mawPower);

                    ++mawPowersCount;

                } while (mawPowersResult.NextRow());
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_playerChoices.Count} player choices, {responseCount} responses, {rewardCount} rewards, {itemRewardCount} item rewards, " +
                $"{currencyRewardCount} currency rewards, {factionRewardCount} faction rewards, {itemChoiceRewardCount} item choice rewards and {mawPowersCount} maw powers in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }
        public void LoadPlayerChoicesLocale()
        {
            uint oldMSTime = Time.GetMSTime();

            // need for reload case
            _playerChoiceLocales.Clear();

            //                                               0         1       2
            SQLResult result = DB.World.Query("SELECT ChoiceId, locale, Question FROM playerchoice_locale");
            if (!result.IsEmpty())
            {
                do
                {
                    int choiceId = result.Read<int>(0);
                    string localeName = result.Read<string>(1);
                    Locale locale = localeName.ToEnum<Locale>();
                    if (!SharedConst.IsValidLocale(locale) || locale == Locale.enUS)
                        continue;

                    if (GetPlayerChoice(choiceId) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_locale` references non-existing ChoiceId: {choiceId} for locale {localeName}, skipped");
                        continue;
                    }

                    if (!_playerChoiceLocales.ContainsKey(choiceId))
                        _playerChoiceLocales[choiceId] = new PlayerChoiceLocale();

                    PlayerChoiceLocale data = _playerChoiceLocales[choiceId];
                    AddLocaleString(result.Read<string>(2), locale, data.Question);
                } while (result.NextRow());

                Log.outInfo(LogFilter.ServerLoading, $"Loaded {_playerChoiceLocales.Count} Player Choice locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
            }

            oldMSTime = Time.GetMSTime();

            //                               0         1           2       3       4       5          6               7            8
            result = DB.World.Query("SELECT ChoiceID, ResponseID, locale, Answer, Header, SubHeader, ButtonTooltip, Description, Confirmation FROM playerchoice_response_locale");
            if (!result.IsEmpty())
            {
                uint count = 0;
                do
                {
                    int choiceId = result.Read<int>(0);
                    int responseId = result.Read<int>(1);
                    string localeName = result.Read<string>(2);
                    Locale locale = localeName.ToEnum<Locale>();
                    if (!SharedConst.IsValidLocale(locale) || locale == Locale.enUS)
                        continue;

                    var playerChoiceLocale = _playerChoiceLocales.LookupByKey(choiceId);
                    if (playerChoiceLocale == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_locale` references non-existing ChoiceId: {choiceId} for ResponseId {responseId} locale {localeName}, skipped");
                        continue;
                    }

                    PlayerChoice playerChoice = GetPlayerChoice(choiceId);
                    if (playerChoice.GetResponse(responseId) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_locale` references non-existing ResponseId: {responseId} for ChoiceId {choiceId} locale {localeName}, skipped");
                        continue;
                    }

                    PlayerChoiceResponseLocale data = playerChoiceLocale.Responses[responseId];
                    AddLocaleString(result.Read<string>(3), locale, data.Answer);
                    AddLocaleString(result.Read<string>(4), locale, data.Header);
                    AddLocaleString(result.Read<string>(5), locale, data.SubHeader);
                    AddLocaleString(result.Read<string>(6), locale, data.ButtonTooltip);
                    AddLocaleString(result.Read<string>(7), locale, data.Description);
                    AddLocaleString(result.Read<string>(8), locale, data.Confirmation);
                    count++;
                } while (result.NextRow());

                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} Player Choice Response locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
            }
        }
        public void InitializeQueriesData(QueryDataGroup mask)
        {
            // cache disabled
            if (!WorldConfig.GetBoolValue(WorldCfg.CacheDataQueries))
                return;

            // Initialize Query data for creatures
            if (mask.HasAnyFlag(QueryDataGroup.Creatures))
                foreach (var creaturePair in _creatureTemplateStorage)
                    creaturePair.Value.InitializeQueryData();

            // Initialize Query Data for gameobjects
            if (mask.HasAnyFlag(QueryDataGroup.Gameobjects))
                foreach (var gameobjectPair in _gameObjectTemplateStorage)
                    gameobjectPair.Value.InitializeQueryData();

            // Initialize Query Data for quests
            if (mask.HasAnyFlag(QueryDataGroup.Quests))
                foreach (var questPair in _questTemplates)
                    questPair.Value.InitializeQueryData();

            // Initialize Quest POI data
            if (mask.HasAnyFlag(QueryDataGroup.POIs))
                foreach (var poiPair in _questPOIStorage)
                    poiPair.Value.InitializeQueryData();
        }

        public MailLevelReward GetMailLevelReward(uint level, ulong raceMask)
        {
            var mailList = _mailLevelRewardStorage.LookupByKey((byte)level);
            if (mailList.Empty())
                return null;

            foreach (var mailReward in mailList)
                if (Convert.ToBoolean(mailReward.raceMask & raceMask))
                    return mailReward;

            return null;
        }
        public RepRewardRate GetRepRewardRate(uint factionId)
        {
            return _repRewardRateStorage.LookupByKey(factionId);
        }
        public RepSpilloverTemplate GetRepSpillover(uint factionId)
        {
            return _repSpilloverTemplateStorage.LookupByKey(factionId);
        }
        public ReputationOnKillEntry GetReputationOnKilEntry(uint id)
        {
            return _repOnKillStorage.LookupByKey(id);
        }
        public void SetHighestGuids()
        {
            SQLResult result = DB.Characters.Query("SELECT MAX(guid) FROM characters");
            if (!result.IsEmpty())
                GetGuidSequenceGenerator(HighGuid.Player).Set(result.Read<ulong>(0) + 1);

            result = DB.Characters.Query("SELECT MAX(guid) FROM item_instance");
            if (!result.IsEmpty())
                GetGuidSequenceGenerator(HighGuid.Item).Set(result.Read<ulong>(0) + 1);

            // Cleanup other tables from not existed guids ( >= hiItemGuid)
            DB.Characters.Execute("DELETE FROM character_inventory WHERE item >= {0}", GetGuidSequenceGenerator(HighGuid.Item).GetNextAfterMaxUsed());      // One-time query
            DB.Characters.Execute("DELETE FROM mail_items WHERE item_guid >= {0}", GetGuidSequenceGenerator(HighGuid.Item).GetNextAfterMaxUsed());          // One-time query
            DB.Characters.Execute("DELETE a, ab, ai FROM auctionhouse a LEFT JOIN auction_bidders ab ON ab.auctionId = a.id LEFT JOIN auction_items ai ON ai.auctionId = a.id WHERE ai.itemGuid >= '{0}'",
              GetGuidSequenceGenerator(HighGuid.Item).GetNextAfterMaxUsed());       // One-time query
            DB.Characters.Execute("DELETE FROM guild_bank_item WHERE item_guid >= {0}", GetGuidSequenceGenerator(HighGuid.Item).GetNextAfterMaxUsed());     // One-time query

            result = DB.World.Query("SELECT MAX(guid) FROM transports");
            if (!result.IsEmpty())
                GetGuidSequenceGenerator(HighGuid.Transport).Set(result.Read<ulong>(0) + 1);

            result = DB.Characters.Query("SELECT MAX(id) FROM auctionhouse");
            if (!result.IsEmpty())
                _auctionId = result.Read<uint>(0) + 1;

            result = DB.Characters.Query("SELECT MAX(id) FROM mail");
            if (!result.IsEmpty())
                _mailId = result.Read<uint>(0) + 1;

            result = DB.Characters.Query("SELECT MAX(arenateamid) FROM arena_team");
            if (!result.IsEmpty())
                Global.ArenaTeamMgr.SetNextArenaTeamId(result.Read<uint>(0) + 1);

            result = DB.Characters.Query("SELECT MAX(maxguid) FROM ((SELECT MAX(setguid) AS maxguid FROM character_equipmentsets) UNION (SELECT MAX(setguid) AS maxguid FROM character_transmog_outfits)) allsets");
            if (!result.IsEmpty())
                _equipmentSetGuid = result.Read<ulong>(0) + 1;

            result = DB.Characters.Query("SELECT MAX(guildId) FROM guild");
            if (!result.IsEmpty())
                Global.GuildMgr.SetNextGuildId(result.Read<uint>(0) + 1);

            result = DB.Characters.Query("SELECT MAX(itemId) from character_void_storage");
            if (!result.IsEmpty())
                _voidItemId = result.Read<ulong>(0) + 1;

            result = DB.World.Query("SELECT MAX(guid) FROM creature");
            if (!result.IsEmpty())
                _creatureSpawnId = result.Read<ulong>(0) + 1;

            result = DB.World.Query("SELECT MAX(guid) FROM gameobject");
            if (!result.IsEmpty())
                _gameObjectSpawnId = result.Read<ulong>(0) + 1;
        }
        public uint GenerateAuctionID()
        {
            if (_auctionId >= 0xFFFFFFFE)
            {
                Log.outError(LogFilter.Server, "Auctions ids overflow!! Can't continue, shutting down server. ");
                Global.WorldMgr.StopNow();
            }
            return _auctionId++;
        }
        public ulong GenerateEquipmentSetGuid()
        {
            if (_equipmentSetGuid >= 0xFFFFFFFFFFFFFFFE)
            {
                Log.outError(LogFilter.Server, "EquipmentSet guid overflow!! Can't continue, shutting down server. ");
                Global.WorldMgr.StopNow();
            }
            return _equipmentSetGuid++;
        }
        public uint GenerateMailID()
        {
            if (_mailId >= 0xFFFFFFFE)
            {
                Log.outError(LogFilter.Server, "Mail ids overflow!! Can't continue, shutting down server. ");
                Global.WorldMgr.StopNow();
            }
            return _mailId++;
        }
        public ulong GenerateVoidStorageItemId()
        {
            if (_voidItemId >= 0xFFFFFFFFFFFFFFFE)
            {
                Log.outError(LogFilter.Misc, "_voidItemId overflow!! Can't continue, shutting down server. ");
                Global.WorldMgr.StopNow(ShutdownExitCode.Error);
            }
            return _voidItemId++;
        }
        public ulong GenerateCreatureSpawnId()
        {
            if (_creatureSpawnId >= 0xFFFFFFFFFFFFFFFE)
            {
                Log.outFatal(LogFilter.Server, "Creature spawn id overflow!! Can't continue, shutting down server. ");
                Global.WorldMgr.StopNow();
            }
            return _creatureSpawnId++;
        }
        public ulong GenerateGameObjectSpawnId()
        {
            if (_gameObjectSpawnId >= 0xFFFFFFFFFFFFFFFE)
            {
                Log.outFatal(LogFilter.Server, "GameObject spawn id overflow!! Can't continue, shutting down server. ");
                Global.WorldMgr.StopNow();
            }
            return _gameObjectSpawnId++;
        }
        public ObjectGuidGenerator GetGenerator(HighGuid high)
        {
            Cypher.Assert(ObjectGuid.IsGlobal(high) || ObjectGuid.IsRealmSpecific(high), "Only global guid can be generated in ObjectMgr context");

            return GetGuidSequenceGenerator(high);
        }
        ObjectGuidGenerator GetGuidSequenceGenerator(HighGuid high)
        {
            if (!_guidGenerators.ContainsKey(high))
                _guidGenerators[high] = new ObjectGuidGenerator(high);

            return _guidGenerators[high];
        }

        public uint GetBaseXP(uint level)
        {
            return _baseXPTable.ContainsKey(level) ? _baseXPTable[level] : 0;
        }
        public uint GetXPForLevel(uint level)
        {
            if (level < _playerXPperLevel.Length)
                return _playerXPperLevel[level];
            return 0;
        }
        public uint GetMaxLevelForExpansion(Expansion expansion)
        {
            switch (expansion)
            {
                case Expansion.Classic:
                    return 30;
                case Expansion.BurningCrusade:
                    return 30;
                case Expansion.WrathOfTheLichKing:
                    return 30;
                case Expansion.Cataclysm:
                    return 35;
                case Expansion.MistsOfPandaria:
                    return 35;
                case Expansion.WarlordsOfDraenor:
                    return 40;
                case Expansion.Legion:
                    return 45;
                case Expansion.BattleForAzeroth:
                    return 50;
                case Expansion.ShadowLands:
                    return 60;
                default:
                    break;
            }
            return 0;
        }
        CellObjectGuids CreateCellObjectGuids(uint mapid, Difficulty difficulty, uint cellid)
        {
            uint newid = MathFunctions.MakePair32(mapid, (uint)difficulty);

            if (!mapObjectGuidsStore.ContainsKey(newid))
                mapObjectGuidsStore.Add(newid, new Dictionary<uint, CellObjectGuids>());

            if (!mapObjectGuidsStore[newid].ContainsKey(cellid))
                mapObjectGuidsStore[newid].Add(cellid, new CellObjectGuids());

            return mapObjectGuidsStore[newid][cellid];
        }
        public CellObjectGuids GetCellObjectGuids(uint mapid, Difficulty difficulty, uint cellid)
        {
            uint newid = MathFunctions.MakePair32(mapid, (uint)difficulty);

            if (mapObjectGuidsStore.ContainsKey(newid) && mapObjectGuidsStore[newid].ContainsKey(cellid))
                return mapObjectGuidsStore[newid][cellid];

            return null;
        }
        public Dictionary<uint, CellObjectGuids> GetMapObjectGuids(uint mapid, byte spawnMode)
        {
            var pair = MathFunctions.MakePair32(mapid, spawnMode);
            return mapObjectGuidsStore.LookupByKey(pair);
        }
        public PageText GetPageText(uint pageEntry)
        {
            return _pageTextStorage.LookupByKey(pageEntry);
        }

        public uint GetNearestTaxiNode(float x, float y, float z, uint mapid, Team team)
        {
            bool found = false;
            float dist = 10000;
            uint id = 0;

            TaxiNodeFlags requireFlag = (team == Team.Alliance) ? TaxiNodeFlags.Alliance : TaxiNodeFlags.Horde;
            foreach (var node in CliDB.TaxiNodesStorage.Values)
            {
                var i = node.Id;
                if (node.ContinentID != mapid || !node.Flags.HasAnyFlag(requireFlag))
                    continue;

                uint field = (i - 1) / 8;
                byte submask = (byte)(1 << (int)((i - 1) % 8));

                // skip not taxi network nodes
                if ((CliDB.TaxiNodesMask[field] & submask) == 0)
                    continue;

                float dist2 = (node.Pos.X - x) * (node.Pos.X - x) + (node.Pos.Y - y) * (node.Pos.Y - y) + (node.Pos.Z - z) * (node.Pos.Z - z);
                if (found)
                {
                    if (dist2 < dist)
                    {
                        dist = dist2;
                        id = i;
                    }
                }
                else
                {
                    found = true;
                    dist = dist2;
                    id = i;
                }
            }

            return id;
        }
        public void GetTaxiPath(uint source, uint destination, out uint path, out uint cost)
        {
            var pathSet = CliDB.TaxiPathSetBySource.LookupByKey(source);
            if (pathSet == null)
            {
                path = 0;
                cost = 0;
                return;
            }

            var dest_i = pathSet.LookupByKey(destination);
            if (dest_i == null)
            {
                path = 0;
                cost = 0;
                return;
            }

            cost = dest_i.price;
            path = dest_i.Id;
        }
        public uint GetTaxiMountDisplayId(uint id, Team team, bool allowed_alt_team = false)
        {
            CreatureModel mountModel = new();
            CreatureTemplate mount_info = null;

            // select mount creature id
            TaxiNodesRecord node = CliDB.TaxiNodesStorage.LookupByKey(id);
            if (node != null)
            {
                uint mount_entry;
                if (team == Team.Alliance)
                    mount_entry = node.MountCreatureID[1];
                else
                    mount_entry = node.MountCreatureID[0];

                // Fix for Alliance not being able to use Acherus taxi
                // only one mount type for both sides
                if (mount_entry == 0 && allowed_alt_team)
                {
                    // Simply reverse the selection. At least one team in theory should have a valid mount ID to choose.
                    mount_entry = team == Team.Alliance ? node.MountCreatureID[0] : node.MountCreatureID[1];
                }

                mount_info = GetCreatureTemplate(mount_entry);
                if (mount_info != null)
                {
                    CreatureModel model = mount_info.GetRandomValidModel();
                    if (model == null)
                    {
                        Log.outError(LogFilter.Sql, $"No displayid found for the taxi mount with the entry {mount_entry}! Can't load it!");
                        return 0;
                    }
                    mountModel = model;
                }
            }

            // minfo is not actually used but the mount_id was updated
            GetCreatureModelRandomGender(ref mountModel, mount_info);

            return mountModel.CreatureDisplayID;
        }

        public AreaTriggerStruct GetAreaTrigger(uint trigger)
        {
            return _areaTriggerStorage.LookupByKey(trigger);
        }
        public AccessRequirement GetAccessRequirement(uint mapid, Difficulty difficulty)
        {
            return _accessRequirementStorage.LookupByKey(MathFunctions.MakePair64(mapid, (uint)difficulty));
        }
        public bool IsTavernAreaTrigger(uint Trigger_ID)
        {
            return _tavernAreaTriggerStorage.Contains(Trigger_ID);
        }
        public AreaTriggerStruct GetGoBackTrigger(uint Map)
        {
            bool useParentDbValue = false;
            uint parentId = 0;
            MapRecord mapEntry = CliDB.MapStorage.LookupByKey(Map);
            if (mapEntry == null || mapEntry.CorpseMapID < 0)
                return null;

            if (mapEntry.IsDungeon())
            {
                InstanceTemplate iTemplate = GetInstanceTemplate(Map);

                if (iTemplate == null)
                    return null;

                parentId = iTemplate.Parent;
                useParentDbValue = true;
            }

            uint entrance_map = (uint)mapEntry.CorpseMapID;
            foreach (var pair in _areaTriggerStorage)
            {
                if ((!useParentDbValue && pair.Value.target_mapId == entrance_map) || (useParentDbValue && pair.Value.target_mapId == parentId))
                {
                    AreaTriggerRecord atEntry = CliDB.AreaTriggerStorage.LookupByKey(pair.Key);
                    if (atEntry != null && atEntry.ContinentID == Map)
                        return pair.Value;
                }
            }
            return null;
        }
        public AreaTriggerStruct GetMapEntranceTrigger(uint Map)
        {
            foreach (var pair in _areaTriggerStorage)
            {
                if (pair.Value.target_mapId == Map)
                {
                    AreaTriggerRecord atEntry = CliDB.AreaTriggerStorage.LookupByKey(pair.Key);
                    if (atEntry != null)
                        return pair.Value;
                }
            }
            return null;
        }

        public SceneTemplate GetSceneTemplate(uint sceneId)
        {
            return _sceneTemplateStorage.LookupByKey(sceneId);
        }

        public List<TempSummonData> GetSummonGroup(uint summonerId, SummonerType summonerType, byte group)
        {
            Tuple<uint, SummonerType,byte> key = Tuple.Create(summonerId, summonerType, group);
            return _tempSummonDataStorage.LookupByKey(key);
        }

        public bool IsReservedName(string name)
        {
            return _reservedNamesStorage.Contains(name.ToLower());
        }

        //Vehicles
        public void LoadVehicleTemplate()
        {
            uint oldMSTime = Time.GetMSTime();

            _vehicleTemplateStore.Clear();

            //                                         0           1
            SQLResult result = DB.World.Query("SELECT creatureId, despawnDelayMs FROM vehicle_template");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 vehicle template. DB table `vehicle_template` is empty.");
                return;
            }

            do
            {
                uint creatureId = result.Read<uint>(0);

                if (Global.ObjectMgr.GetCreatureTemplate(creatureId) == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `vehicle_template`: Vehicle {creatureId} does not exist.");
                    continue;
                }

                VehicleTemplate vehicleTemplate = new();
                vehicleTemplate.DespawnDelay = TimeSpan.FromMilliseconds(result.Read<int>(1));
                _vehicleTemplateStore[creatureId] = vehicleTemplate;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_vehicleTemplateStore.Count} Vehicle Template entries in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }
        public void LoadVehicleTemplateAccessories()
        {
            uint oldMSTime = Time.GetMSTime();

            _vehicleTemplateAccessoryStore.Clear();                           // needed for reload case

            uint count = 0;

            //                                          0             1              2          3           4             5
            SQLResult result = DB.World.Query("SELECT `entry`, `accessory_entry`, `seat_id`, `minion`, `summontype`, `summontimer` FROM `vehicle_template_accessory`");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 vehicle template accessories. DB table `vehicle_template_accessory` is empty.");
                return;
            }

            do
            {
                uint entry = result.Read<uint>(0);
                uint accessory = result.Read<uint>(1);
                sbyte seatId = result.Read<sbyte>(2);
                bool isMinion = result.Read<bool>(3);
                byte summonType = result.Read<byte>(4);
                uint summonTimer = result.Read<uint>(5);

                if (GetCreatureTemplate(entry) == null)
                {
                    Log.outError(LogFilter.Sql, "Table `vehicle_template_accessory`: creature template entry {0} does not exist.", entry);
                    continue;
                }

                if (GetCreatureTemplate(accessory) == null)
                {
                    Log.outError(LogFilter.Sql, "Table `vehicle_template_accessory`: Accessory {0} does not exist.", accessory);
                    continue;
                }

                if (!_spellClickInfoStorage.ContainsKey(entry))
                {
                    Log.outError(LogFilter.Sql, "Table `vehicle_template_accessory`: creature template entry {0} has no data in npc_spellclick_spells", entry);
                    continue;
                }

                _vehicleTemplateAccessoryStore.Add(entry, new VehicleAccessory(accessory, seatId, isMinion, summonType, summonTimer));

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} Vehicle Template Accessories in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        public void LoadVehicleAccessories()
        {
            uint oldMSTime = Time.GetMSTime();

            _vehicleAccessoryStore.Clear();                           // needed for reload case

            uint count = 0;

            //                                          0             1             2          3           4             5
            SQLResult result = DB.World.Query("SELECT `guid`, `accessory_entry`, `seat_id`, `minion`, `summontype`, `summontimer` FROM `vehicle_accessory`");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 Vehicle Accessories in {0} ms", Time.GetMSTimeDiffToNow(oldMSTime));
                return;
            }

            do
            {
                uint uiGUID = result.Read<uint>(0);
                uint uiAccessory = result.Read<uint>(1);
                sbyte uiSeat = result.Read<sbyte>(2);
                bool bMinion = result.Read<bool>(3);
                byte uiSummonType = result.Read<byte>(4);
                uint uiSummonTimer = result.Read<uint>(5);

                if (GetCreatureTemplate(uiAccessory) == null)
                {
                    Log.outError(LogFilter.Sql, "Table `vehicle_accessory`: Accessory {0} does not exist.", uiAccessory);
                    continue;
                }

                _vehicleAccessoryStore.Add(uiGUID, new VehicleAccessory(uiAccessory, uiSeat, bMinion, uiSummonType, uiSummonTimer));

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} Vehicle Accessories in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        public VehicleTemplate GetVehicleTemplate(Vehicle veh)
        {
            return _vehicleTemplateStore.LookupByKey(veh.GetCreatureEntry());
        }
        public List<VehicleAccessory> GetVehicleAccessoryList(Vehicle veh)
        {
            Creature cre = veh.GetBase().ToCreature();
            if (cre != null)
            {
                // Give preference to GUID-based accessories
                var list = _vehicleAccessoryStore.LookupByKey(cre.GetSpawnId());
                if (!list.Empty())
                    return list;
            }

            // Otherwise return entry-based
            return _vehicleTemplateAccessoryStore.LookupByKey(veh.GetCreatureEntry());
        }

        #region Fields
        //General
        Dictionary<uint, StringArray> CypherStringStorage = new();
        Dictionary<uint, RepRewardRate> _repRewardRateStorage = new();
        Dictionary<uint, ReputationOnKillEntry> _repOnKillStorage = new();
        Dictionary<uint, RepSpilloverTemplate> _repSpilloverTemplateStorage = new();
        MultiMap<byte, MailLevelReward> _mailLevelRewardStorage = new();
        MultiMap<Tuple<uint, SummonerType, byte>, TempSummonData> _tempSummonDataStorage = new();
        Dictionary<int /*choiceId*/, PlayerChoice> _playerChoices = new();
        Dictionary<uint, PageText> _pageTextStorage = new();
        List<string> _reservedNamesStorage = new();
        Dictionary<uint, SceneTemplate> _sceneTemplateStorage = new();

        Dictionary<byte, RaceUnlockRequirement> _raceUnlockRequirementStorage = new();
        List<RaceClassAvailability> _classExpansionRequirementStorage = new();
        Dictionary<uint, string> _realmNameStorage = new();

        //Quest
        Dictionary<uint, Quest> _questTemplates = new();
        List<Quest> _questTemplatesAutoPush = new();
        MultiMap<uint, uint> _goQuestRelations = new();
        MultiMap<uint, uint> _goQuestInvolvedRelations = new();
        MultiMap<uint, uint> _goQuestInvolvedRelationsReverse = new();
        MultiMap<uint, uint> _creatureQuestRelations = new();
        MultiMap<uint, uint> _creatureQuestInvolvedRelations = new();
        MultiMap<uint, uint> _creatureQuestInvolvedRelationsReverse = new();
        MultiMap<int, uint> _exclusiveQuestGroups = new();
        Dictionary<uint, QuestPOIData> _questPOIStorage = new();
        MultiMap<uint, uint> _questAreaTriggerStorage = new();
        Dictionary<uint, QuestObjective> _questObjectives = new();
        Dictionary<uint, QuestGreeting>[] _questGreetingStorage = new Dictionary<uint, QuestGreeting>[2];
        Dictionary<uint, QuestGreetingLocale>[] _questGreetingLocaleStorage = new Dictionary<uint, QuestGreetingLocale>[2];

        //Scripts
        List<string> scriptNamesStorage = new();
        MultiMap<uint, uint> spellScriptsStorage = new();
        public Dictionary<uint, MultiMap<uint, ScriptInfo>> sSpellScripts = new();
        public Dictionary<uint, MultiMap<uint, ScriptInfo>> sEventScripts = new();
        public Dictionary<uint, MultiMap<uint, ScriptInfo>> sWaypointScripts = new();
        Dictionary<uint, uint> areaTriggerScriptStorage = new();

        //Maps
        public Dictionary<uint, GameTele> gameTeleStorage = new();
        Dictionary<uint, Dictionary<uint, CellObjectGuids>> mapObjectGuidsStore = new();
        Dictionary<uint, InstanceTemplate> instanceTemplateStorage = new();
        public MultiMap<uint, GraveYardData> GraveYardStorage = new();
        List<ushort> _transportMaps = new();
        Dictionary<uint, SpawnGroupTemplateData> _spawnGroupDataStorage = new();
        MultiMap<uint, SpawnData> _spawnGroupMapStorage = new();
        MultiMap<ushort, InstanceSpawnGroupInfo> _instanceSpawnGroupStorage = new();

        //Spells /Skills / Phases
        Dictionary<uint, PhaseInfoStruct> _phaseInfoById = new();
        Dictionary<uint, TerrainSwapInfo> _terrainSwapInfoById = new();
        MultiMap<uint, PhaseAreaInfo> _phaseInfoByArea = new();
        MultiMap<uint, TerrainSwapInfo> _terrainSwapInfoByMap = new();
        MultiMap<uint, SpellClickInfo> _spellClickInfoStorage = new();
        Dictionary<uint, int> _fishingBaseForAreaStorage = new();
        Dictionary<uint, SkillTiersEntry> _skillTiers = new();

        //Gossip
        MultiMap<uint, GossipMenuItems> gossipMenuItemsStorage = new();
        MultiMap<uint, GossipMenus> gossipMenusStorage = new();
        Dictionary<uint, PointOfInterest> pointsOfInterestStorage = new();

        //Creature
        Dictionary<uint, CreatureTemplate> _creatureTemplateStorage = new();
        Dictionary<uint, CreatureModelInfo> creatureModelStorage = new();
        Dictionary<ulong, CreatureData> creatureDataStorage = new();
        Dictionary<ulong, CreatureAddon> creatureAddonStorage = new();
        MultiMap<uint, uint> _creatureQuestItemStorage = new();
        Dictionary<uint, CreatureAddon> creatureTemplateAddonStorage = new();
        MultiMap<uint, Tuple<uint, EquipmentInfo>> equipmentInfoStorage = new();
        Dictionary<ObjectGuid, ObjectGuid> linkedRespawnStorage = new();
        Dictionary<uint, CreatureBaseStats> creatureBaseStatsStorage = new();
        Dictionary<uint, VendorItemData> cacheVendorItemStorage = new();
        Dictionary<uint, Trainer> _trainers = new();
        Dictionary<(uint creatureId, uint gossipMenuId, uint gossipOptionIndex), uint> _creatureDefaultTrainers = new();
        List<uint>[] _difficultyEntries = new List<uint>[SharedConst.MaxCreatureDifficulties]; // already loaded difficulty 1 value in creatures, used in CheckCreatureTemplate
        List<uint>[] _hasDifficultyEntries = new List<uint>[SharedConst.MaxCreatureDifficulties]; // already loaded creatures with difficulty 1 values, used in CheckCreatureTemplate
        Dictionary<uint, NpcText> _npcTextStorage = new();

        //GameObject
        Dictionary<uint, GameObjectTemplate> _gameObjectTemplateStorage = new();
        Dictionary<ulong, GameObjectData> gameObjectDataStorage = new();
        Dictionary<ulong, GameObjectTemplateAddon> _gameObjectTemplateAddonStorage = new();
        Dictionary<ulong, GameObjectAddon> _gameObjectAddonStorage = new();
        MultiMap<uint, uint> _gameObjectQuestItemStorage = new();
        List<uint> _gameObjectForQuestStorage = new();

        //Item
        Dictionary<uint, ItemTemplate> ItemTemplateStorage = new();

        //Player
        PlayerInfo[][] _playerInfo = new PlayerInfo[(int)Race.Max][];

        //Faction Change
        public Dictionary<uint, uint> FactionChangeAchievements = new();
        public Dictionary<uint, uint> FactionChangeItems = new();
        public Dictionary<uint, uint> FactionChangeQuests = new();
        public Dictionary<uint, uint> FactionChangeReputation = new();
        public Dictionary<uint, uint> FactionChangeSpells = new();
        public Dictionary<uint, uint> FactionChangeTitles = new();

        //Pets
        Dictionary<uint, PetLevelInfo[]> petInfoStore = new();
        MultiMap<uint, string> _petHalfName0 = new();
        MultiMap<uint, string> _petHalfName1 = new();

        //Vehicles
        Dictionary<uint, VehicleTemplate> _vehicleTemplateStore = new();
        MultiMap<uint, VehicleAccessory> _vehicleTemplateAccessoryStore = new();
        MultiMap<ulong, VehicleAccessory> _vehicleAccessoryStore = new();

        //Locales
        Dictionary<uint, CreatureLocale> _creatureLocaleStorage = new();
        Dictionary<uint, GameObjectLocale> _gameObjectLocaleStorage = new();
        Dictionary<uint, QuestTemplateLocale> _questTemplateLocaleStorage = new();
        Dictionary<uint, QuestObjectivesLocale> _questObjectivesLocaleStorage = new();
        Dictionary<uint, QuestOfferRewardLocale> _questOfferRewardLocaleStorage = new();
        Dictionary<uint, QuestRequestItemsLocale> _questRequestItemsLocaleStorage = new();
        Dictionary<Tuple<uint, uint>, GossipMenuItemsLocale> _gossipMenuItemsLocaleStorage = new();
        Dictionary<uint, PageTextLocale> _pageTextLocaleStorage = new();
        Dictionary<uint, PointOfInterestLocale> _pointOfInterestLocaleStorage = new();
        Dictionary<int, PlayerChoiceLocale> _playerChoiceLocales = new();

        List<uint> _tavernAreaTriggerStorage = new();
        Dictionary<uint, AreaTriggerStruct> _areaTriggerStorage = new();
        Dictionary<ulong, AccessRequirement> _accessRequirementStorage = new();
        MultiMap<ulong, DungeonEncounter> _dungeonEncounterStorage = new();
        Dictionary<uint, WorldSafeLocsEntry> _worldSafeLocs = new();

        Dictionary<HighGuid, ObjectGuidGenerator> _guidGenerators = new();
        // first free id for selected id type
        uint _auctionId;
        ulong _equipmentSetGuid;
        uint _mailId;
        uint _hiPetNumber;
        ulong _creatureSpawnId;
        ulong _gameObjectSpawnId;
        ulong _voidItemId;
        uint[] _playerXPperLevel;
        Dictionary<uint, uint> _baseXPTable = new();
        #endregion
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ScriptInfo
    {
        [FieldOffset(0)]
        public ScriptsType type;

        [FieldOffset(4)]
        public uint id;

        [FieldOffset(8)]
        public uint delay;

        [FieldOffset(12)]
        public ScriptCommands command;

        [FieldOffset(16)]
        public raw Raw;

        [FieldOffset(16)]
        public talk Talk;

        [FieldOffset(16)]
        public emote Emote;

        [FieldOffset(16)]
        public fieldset FieldSet;

        [FieldOffset(16)]
        public moveto MoveTo;

        [FieldOffset(16)]
        public flagtoggle FlagToggle;

        [FieldOffset(16)]
        public teleportto TeleportTo;

        [FieldOffset(16)]
        public questexplored QuestExplored;

        [FieldOffset(16)]
        public killcredit KillCredit;

        [FieldOffset(16)]
        public respawngameobject RespawnGameObject;

        [FieldOffset(16)]
        public tempsummoncreature TempSummonCreature;

        [FieldOffset(16)]
        public toggledoor ToggleDoor;

        [FieldOffset(16)]
        public removeaura RemoveAura;

        [FieldOffset(16)]
        public castspell CastSpell;

        [FieldOffset(16)]
        public playsound PlaySound;

        [FieldOffset(16)]
        public createitem CreateItem;

        [FieldOffset(16)]
        public despawnself DespawnSelf;

        [FieldOffset(16)]
        public loadpath LoadPath;

        [FieldOffset(16)]
        public callscript CallScript;

        [FieldOffset(16)]
        public kill Kill;

        [FieldOffset(16)]
        public orientation Orientation;

        [FieldOffset(16)]
        public equip Equip;

        [FieldOffset(16)]
        public model Model;

        [FieldOffset(16)]
        public playmovie PlayMovie;

        [FieldOffset(16)]
        public movement Movement;

        [FieldOffset(16)]
        public playanimkit PlayAnimKit;

        public string GetDebugInfo()
        {
            return $"{command} ('{Global.ObjectMgr.GetScriptsTableNameByType(type)}' script id: {id})";
        }

        #region Structs
        public unsafe struct raw
        {
            public fixed uint nData[3];
            public fixed float fData[4];
        }

        public struct talk                   // TALK (0)
        {
            public ChatMsg ChatType;        // datalong
            public eScriptFlags Flags;           // datalong2
            public int TextID;          // dataint
        }

        public struct emote                   // EMOTE (1)
        {
            public uint EmoteID;         // datalong
            public eScriptFlags Flags;           // datalong2
        }

        public struct fieldset                    // FIELDSET (2)
        {
            public uint FieldID;         // datalong
            public uint FieldValue;      // datalong2
        }

        public struct moveto                   // MOVETO (3)
        {
            public uint Unused1;         // datalong
            public uint TravelTime;      // datalong2
            public int Unused2;         // dataint

            public float DestX;
            public float DestY;
            public float DestZ;
        }

        public struct flagtoggle                   // FLAGSET (4)
        // FLAGREMOVE (5)
        {
            public uint FieldID;         // datalong
            public uint FieldValue;      // datalong2
        }

        public struct teleportto                 // TELEPORTTO (6)
        {
            public uint MapID;           // datalong
            public eScriptFlags Flags;           // datalong2
            public int Unused1;         // dataint

            public float DestX;
            public float DestY;
            public float DestZ;
            public float Orientation;
        }

        public struct questexplored                  // QUESTEXPLORED (7)
        {
            public uint QuestID;         // datalong
            public uint Distance;        // datalong2
        }

        public struct killcredit                    // KILLCREDIT (8)
        {
            public uint CreatureEntry;   // datalong
            public eScriptFlags Flags;           // datalong2
        }

        public struct respawngameobject                 // RESPAWNGAMEOBJECT (9)
        {
            public uint GOGuid;          // datalong
            public uint DespawnDelay;    // datalong2
        }

        public struct tempsummoncreature                // TEMPSUMMONCREATURE (10)
        {
            public uint CreatureEntry;   // datalong
            public uint DespawnDelay;    // datalong2
            public int Unused1;         // dataint

            public float PosX;
            public float PosY;
            public float PosZ;
            public float Orientation;
        }

        public struct toggledoor                  // CLOSEDOOR (12)
        // OPENDOOR (11)
        {
            public uint GOGuid;          // datalong
            public uint ResetDelay;      // datalong2
        }

        // ACTIVATEOBJECT (13)

        public struct removeaura                   // REMOVEAURA (14)
        {
            public uint SpellID;         // datalong
            public eScriptFlags Flags;           // datalong2
        }

        public struct castspell                  // CASTSPELL (15)
        {
            public uint SpellID;         // datalong
            public eScriptFlags Flags;           // datalong2
            public int CreatureEntry;   // dataint

            public float SearchRadius;
        }

        public struct playsound                     // PLAYSOUND (16)
        {
            public uint SoundID;         // datalong
            public eScriptFlags Flags;           // datalong2
        }

        public struct createitem                   // CREATEITEM (17)
        {
            public uint ItemEntry;       // datalong
            public uint Amount;          // datalong2
        }

        public struct despawnself                 // DESPAWNSELF (18)
        {
            public uint DespawnDelay;    // datalong
        }

        public struct loadpath                    // LOADPATH (20)
        {
            public uint PathID;          // datalong
            public uint IsRepeatable;    // datalong2
        }

        public struct callscript                   // CALLSCRIPTTOUNIT (21)
        {
            public uint CreatureEntry;   // datalong
            public uint ScriptID;        // datalong2
            public uint ScriptType;      // dataint
        }

        public struct kill                    // KILL (22)
        {
            public uint Unused1;         // datalong
            public uint Unused2;         // datalong2
            public int RemoveCorpse;    // dataint
        }

        public struct orientation                    // ORIENTATION (30)
        {
            public eScriptFlags Flags;           // datalong
            public uint Unused1;         // datalong2
            public int Unused2;         // dataint

            public float Unused3;
            public float Unused4;
            public float Unused5;
            public float _Orientation;
        }

        public struct equip                  // EQUIP (31)
        {
            public uint EquipmentID;     // datalong
        }

        public struct model                    // MODEL (32)
        {
            public uint ModelID;         // datalong
        }

        // CLOSEGOSSIP (33)

        public struct playmovie                    // PLAYMOVIE (34)
        {
            public uint MovieID;         // datalong
        }

        public struct movement                      // SCRIPT_COMMAND_MOVEMENT (35)
        {
            public uint MovementType;     // datalong
            public uint MovementDistance; // datalong2
            public int Path;             // dataint
        }

        public struct playanimkit                  // SCRIPT_COMMAND_PLAY_ANIMKIT (36)
        {
            public uint AnimKitID;       // datalong
        }
        #endregion
    }

    public class CellObjectGuids
    {
        public SortedSet<ulong> creatures = new();
        public SortedSet<ulong> gameobjects = new();
    }

    public class GameTele
    {
        public float posX;
        public float posY;
        public float posZ;
        public float orientation;
        public uint mapId;
        public string name;
        public string nameLow;
    }

    public class PetLevelInfo
    {
        public PetLevelInfo()
        {
            health = 0;
            mana = 0;
        }

        public uint[] stats = new uint[(int)Stats.Max];
        public uint health;
        public uint mana;
        public uint armor;
    }

    public struct InstanceSpawnGroupInfo
    {
        public byte BossStateId;
        public byte BossStates;
        public uint SpawnGroupId;
        public InstanceSpawnGroupFlags Flags;
    }
    
    public class SpellClickInfo
    {
        public uint spellId;
        public byte castFlags;
        public SpellClickUserTypes userType;

        // helpers
        public bool IsFitToRequirements(Unit clicker, Unit clickee)
        {
            Player playerClicker = clicker.ToPlayer();
            if (playerClicker == null)
                return true;

            Unit summoner = null;
            // Check summoners for party
            if (clickee.IsSummon())
                summoner = clickee.ToTempSummon().GetSummoner();
            if (summoner == null)
                summoner = clickee;

            // This only applies to players
            switch (userType)
            {
                case SpellClickUserTypes.Friend:
                    if (!playerClicker.IsFriendlyTo(summoner))
                        return false;
                    break;
                case SpellClickUserTypes.Raid:
                    if (!playerClicker.IsInRaidWith(summoner))
                        return false;
                    break;
                case SpellClickUserTypes.Party:
                    if (!playerClicker.IsInPartyWith(summoner))
                        return false;
                    break;
                default:
                    break;
            }

            return true;
        }
    }

    public class WorldSafeLocsEntry
    {
        public uint Id;
        public WorldLocation Loc;
    }

    public class GraveYardData
    {
        public uint safeLocId;
        public uint team;
    }

    public class QuestPOIBlobData
    {
        public int BlobIndex;
        public int ObjectiveIndex;
        public int QuestObjectiveID;
        public int QuestObjectID;
        public int MapID;
        public int UiMapID;
        public int Priority;
        public int Flags;
        public int WorldEffectID;
        public int PlayerConditionID;
        public int NavigationPlayerConditionID;
        public int SpawnTrackingID;
        public List<QuestPOIBlobPoint> Points;
        public bool AlwaysAllowMergingBlobs;

        public QuestPOIBlobData(int blobIndex, int objectiveIndex, int questObjectiveID, int questObjectID, int mapID, int uiMapID, int priority, int flags, 
            int worldEffectID, int playerConditionID, int navigationPlayerConditionID, int spawnTrackingID, List<QuestPOIBlobPoint> points, bool alwaysAllowMergingBlobs)
        {
            BlobIndex = blobIndex;
            ObjectiveIndex = objectiveIndex;
            QuestObjectiveID = questObjectiveID;
            QuestObjectID = questObjectID;
            MapID = mapID;
            UiMapID = uiMapID;
            Priority = priority;
            Flags = flags;
            WorldEffectID = worldEffectID;
            PlayerConditionID = playerConditionID;
            NavigationPlayerConditionID = navigationPlayerConditionID;
            SpawnTrackingID = spawnTrackingID;
            Points = points;
            AlwaysAllowMergingBlobs = alwaysAllowMergingBlobs;
        } 
    }

    public class QuestPOIBlobPoint
    {  
        public int X;
        public int Y;
        public int Z;

        public QuestPOIBlobPoint(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class QuestPOIData
    {
        public uint QuestID;
        public List<QuestPOIBlobData> Blobs;
        public ByteBuffer QueryDataBuffer;

        public QuestPOIData(uint questId)
        {
            QuestID = questId;
            Blobs = new List<QuestPOIBlobData>();
            QueryDataBuffer = new ByteBuffer();
        }

        public void InitializeQueryData()
        {
            Write(QueryDataBuffer);
        }

        public void Write(ByteBuffer data)
        {
            data.WriteUInt32(QuestID);
            data.WriteInt32(Blobs.Count);

            foreach (QuestPOIBlobData questPOIBlobData in Blobs)
            {
                data.WriteInt32(questPOIBlobData.BlobIndex);
                data.WriteInt32(questPOIBlobData.ObjectiveIndex);
                data.WriteInt32(questPOIBlobData.QuestObjectiveID);
                data.WriteInt32(questPOIBlobData.QuestObjectID);
                data.WriteInt32(questPOIBlobData.MapID);
                data.WriteInt32(questPOIBlobData.UiMapID);
                data.WriteInt32(questPOIBlobData.Priority);
                data.WriteInt32(questPOIBlobData.Flags);
                data.WriteInt32(questPOIBlobData.WorldEffectID);
                data.WriteInt32(questPOIBlobData.PlayerConditionID);
                data.WriteInt32(questPOIBlobData.NavigationPlayerConditionID);
                data.WriteInt32(questPOIBlobData.SpawnTrackingID);
                data.WriteInt32(questPOIBlobData.Points.Count);

                foreach (QuestPOIBlobPoint questPOIBlobPoint in questPOIBlobData.Points)
                {
                    data.WriteInt16((short)questPOIBlobPoint.X);
                    data.WriteInt16((short)questPOIBlobPoint.Y);
                    data.WriteInt16((short)questPOIBlobPoint.Z);
                }

                data.WriteBit(questPOIBlobData.AlwaysAllowMergingBlobs);
                data.FlushBits();
            }
        }
    }

    public class AreaTriggerStruct
    {
        public uint target_mapId;
        public float target_X;
        public float target_Y;
        public float target_Z;
        public float target_Orientation;
        public uint PortLocId;
    }

    public class DungeonEncounter
    {
        public DungeonEncounter(DungeonEncounterRecord _dbcEntry, EncounterCreditType _creditType, uint _creditEntry, uint _lastEncounterDungeon)
        {
            dbcEntry = _dbcEntry;
            creditType = _creditType;
            creditEntry = _creditEntry;
            lastEncounterDungeon = _lastEncounterDungeon;
        }

        public DungeonEncounterRecord dbcEntry;
        public EncounterCreditType creditType;
        public uint creditEntry;
        public uint lastEncounterDungeon;
    }

    public class MailLevelReward
    {
        public MailLevelReward(ulong _raceMask = 0, uint _mailTemplateId = 0, uint _senderEntry = 0)
        {
            raceMask = _raceMask;
            mailTemplateId = _mailTemplateId;
            senderEntry = _senderEntry;
        }

        public ulong raceMask;
        public uint mailTemplateId;
        public uint senderEntry;
    }

    public class PageText
    {
        public string Text;
        public uint NextPageID;
        public int PlayerConditionID;
        public byte Flags;
    }

    public struct ExtendedPlayerName
    {
        public ExtendedPlayerName(string name, string realmName)
        {
            Name = name;
            Realm = realmName;
        }

        public string Name;
        public string Realm;
    }

    public class LanguageDesc
    {
        public uint SpellId;
        public uint SkillId;

        public LanguageDesc() { }
        public LanguageDesc(uint spellId, uint skillId)
        {
            SpellId = spellId;
            SkillId = skillId;
        }

        public override int GetHashCode()
        {
            return SpellId.GetHashCode() ^ SkillId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is LanguageDesc)
                return (LanguageDesc)obj == this;

            return false;
        }

        public static bool operator ==(LanguageDesc left, LanguageDesc right)
        {
            return left.SpellId == right.SpellId && left.SkillId == right.SkillId;
        }

        public static bool operator !=(LanguageDesc left, LanguageDesc right)
        {
            return !(left == right);
        }
    }

    class ItemSpecStats
    {
        public ItemSpecStats(ItemRecord item, ItemSparseRecord sparse)
        {
            if (item.ClassID == ItemClass.Weapon)
            {
                ItemType = 5;
                switch ((ItemSubClassWeapon)item.SubclassID)
                {
                    case ItemSubClassWeapon.Axe:
                        AddStat(ItemSpecStat.OneHandedAxe);
                        break;
                    case ItemSubClassWeapon.Axe2:
                        AddStat(ItemSpecStat.TwoHandedAxe);
                        break;
                    case ItemSubClassWeapon.Bow:
                        AddStat(ItemSpecStat.Bow);
                        break;
                    case ItemSubClassWeapon.Gun:
                        AddStat(ItemSpecStat.Gun);
                        break;
                    case ItemSubClassWeapon.Mace:
                        AddStat(ItemSpecStat.OneHandedMace);
                        break;
                    case ItemSubClassWeapon.Mace2:
                        AddStat(ItemSpecStat.TwoHandedMace);
                        break;
                    case ItemSubClassWeapon.Polearm:
                        AddStat(ItemSpecStat.Polearm);
                        break;
                    case ItemSubClassWeapon.Sword:
                        AddStat(ItemSpecStat.OneHandedSword);
                        break;
                    case ItemSubClassWeapon.Sword2:
                        AddStat(ItemSpecStat.TwoHandedSword);
                        break;
                    case ItemSubClassWeapon.Warglaives:
                        AddStat(ItemSpecStat.Warglaives);
                        break;
                    case ItemSubClassWeapon.Staff:
                        AddStat(ItemSpecStat.Staff);
                        break;
                    case ItemSubClassWeapon.Fist:
                        AddStat(ItemSpecStat.FistWeapon);
                        break;
                    case ItemSubClassWeapon.Dagger:
                        AddStat(ItemSpecStat.Dagger);
                        break;
                    case ItemSubClassWeapon.Thrown:
                        AddStat(ItemSpecStat.Thrown);
                        break;
                    case ItemSubClassWeapon.Crossbow:
                        AddStat(ItemSpecStat.Crossbow);
                        break;
                    case ItemSubClassWeapon.Wand:
                        AddStat(ItemSpecStat.Wand);
                        break;
                    default:
                        break;
                }
            }
            else if (item.ClassID == ItemClass.Armor)
            {
                switch ((ItemSubClassArmor)item.SubclassID)
                {
                    case ItemSubClassArmor.Cloth:
                        if (sparse.inventoryType != InventoryType.Cloak)
                        {
                            ItemType = 1;
                            break;
                        }

                        ItemType = 0;
                        AddStat(ItemSpecStat.Cloak);
                        break;
                    case ItemSubClassArmor.Leather:
                        ItemType = 2;
                        break;
                    case ItemSubClassArmor.Mail:
                        ItemType = 3;
                        break;
                    case ItemSubClassArmor.Plate:
                        ItemType = 4;
                        break;
                    default:
                        if (item.SubclassID == (int)ItemSubClassArmor.Shield)
                        {
                            ItemType = 6;
                            AddStat(ItemSpecStat.Shield);
                        }
                        else if (item.SubclassID > (int)ItemSubClassArmor.Shield && item.SubclassID <= (int)ItemSubClassArmor.Relic)
                        {
                            ItemType = 6;
                            AddStat(ItemSpecStat.Relic);
                        }
                        else
                            ItemType = 0;
                        break;
                }
            }
            else if (item.ClassID == ItemClass.Gem)
            {
                ItemType = 7;
                GemPropertiesRecord gem = CliDB.GemPropertiesStorage.LookupByKey(sparse.GemProperties);
                if (gem != null)
                {
                    if (gem.Type.HasAnyFlag(SocketColor.RelicIron))
                        AddStat(ItemSpecStat.RelicIron);
                    if (gem.Type.HasAnyFlag(SocketColor.RelicBlood))
                        AddStat(ItemSpecStat.RelicBlood);
                    if (gem.Type.HasAnyFlag(SocketColor.RelicShadow))
                        AddStat(ItemSpecStat.RelicShadow);
                    if (gem.Type.HasAnyFlag(SocketColor.RelicFel))
                        AddStat(ItemSpecStat.RelicFel);
                    if (gem.Type.HasAnyFlag(SocketColor.RelicArcane))
                        AddStat(ItemSpecStat.RelicArcane);
                    if (gem.Type.HasAnyFlag(SocketColor.RelicFrost))
                        AddStat(ItemSpecStat.RelicFrost);
                    if (gem.Type.HasAnyFlag(SocketColor.RelicFire))
                        AddStat(ItemSpecStat.RelicFire);
                    if (gem.Type.HasAnyFlag(SocketColor.RelicWater))
                        AddStat(ItemSpecStat.RelicWater);
                    if (gem.Type.HasAnyFlag(SocketColor.RelicLife))
                        AddStat(ItemSpecStat.RelicLife);
                    if (gem.Type.HasAnyFlag(SocketColor.RelicWind))
                        AddStat(ItemSpecStat.RelicWind);
                    if (gem.Type.HasAnyFlag(SocketColor.RelicHoly))
                        AddStat(ItemSpecStat.RelicHoly);
                }
            }
            else
                ItemType = 0;

            for (uint i = 0; i < ItemConst.MaxStats; ++i)
                if (sparse.StatModifierBonusStat[i] != -1)
                    AddModStat(sparse.StatModifierBonusStat[i]);
        }

        void AddStat(ItemSpecStat statType)
        {
            if (ItemSpecStatCount >= ItemConst.MaxStats)
                return;

            for (uint i = 0; i < ItemConst.MaxStats; ++i)
                if (ItemSpecStatTypes[i] == statType)
                    return;

            ItemSpecStatTypes[ItemSpecStatCount++] = statType;
        }

        void AddModStat(int itemStatType)
        {
            switch ((ItemModType)itemStatType)
            {
                case ItemModType.Agility:
                    AddStat(ItemSpecStat.Agility);
                    break;
                case ItemModType.Strength:
                    AddStat(ItemSpecStat.Strength);
                    break;
                case ItemModType.Intellect:
                    AddStat(ItemSpecStat.Intellect);
                    break;
                case ItemModType.DodgeRating:
                    AddStat(ItemSpecStat.Dodge);
                    break;
                case ItemModType.ParryRating:
                    AddStat(ItemSpecStat.Parry);
                    break;
                case ItemModType.CritMeleeRating:
                case ItemModType.CritRangedRating:
                case ItemModType.CritSpellRating:
                case ItemModType.CritRating:
                    AddStat(ItemSpecStat.Crit);
                    break;
                case ItemModType.HasteRating:
                    AddStat(ItemSpecStat.Haste);
                    break;
                case ItemModType.HitRating:
                    AddStat(ItemSpecStat.Hit);
                    break;
                case ItemModType.ExtraArmor:
                    AddStat(ItemSpecStat.BonusArmor);
                    break;
                case ItemModType.AgiStrInt:
                    AddStat(ItemSpecStat.Agility);
                    AddStat(ItemSpecStat.Strength);
                    AddStat(ItemSpecStat.Intellect);
                    break;
                case ItemModType.AgiStr:
                    AddStat(ItemSpecStat.Agility);
                    AddStat(ItemSpecStat.Strength);
                    break;
                case ItemModType.AgiInt:
                    AddStat(ItemSpecStat.Agility);
                    AddStat(ItemSpecStat.Intellect);
                    break;
                case ItemModType.StrInt:
                    AddStat(ItemSpecStat.Strength);
                    AddStat(ItemSpecStat.Intellect);
                    break;
            }
        }

        public uint ItemType;
        public ItemSpecStat[] ItemSpecStatTypes = new ItemSpecStat[ItemConst.MaxStats];
        public uint ItemSpecStatCount;
    }

    public class SkillTiersEntry
    {
        public uint Id;
        public uint[] Value = new uint[SkillConst.MaxSkillStep];
    }

    public class TerrainSwapInfo
    {
        public TerrainSwapInfo() { }
        public TerrainSwapInfo(uint id)
        {
            Id = id;
        }

        public uint Id;
        public List<uint> UiMapPhaseIDs = new();
    }

    public class PhaseInfoStruct
    {
        public PhaseInfoStruct(uint id)
        {
            Id = id;
        }

        public bool IsAllowedInArea(uint areaId)
        {
            return Areas.Any(areaToCheck => Global.DB2Mgr.IsInArea(areaId, areaToCheck));
        }

        public uint Id;
        public List<uint> Areas = new();
    }

    public class PhaseAreaInfo
    {
        public PhaseAreaInfo(PhaseInfoStruct phaseInfo)
        {
            PhaseInfo = phaseInfo;
        }

        public PhaseInfoStruct PhaseInfo;
        public List<uint> SubAreaExclusions = new();
        public List<Condition> Conditions = new();
    }

    public class SceneTemplate
    {
        public uint SceneId;
        public SceneFlags PlaybackFlags;
        public uint ScenePackageId;
        public bool Encrypted;
        public uint ScriptId;
    }

    public class DefaultCreatureBaseStats : CreatureBaseStats
    {
        public DefaultCreatureBaseStats()
        {
            BaseMana = 0;
            AttackPower = 0;
            RangedAttackPower = 0;
        }
    }

    public class GossipMenuItemsLocale
    {
        public StringArray OptionText = new((int)Locale.Total);
        public StringArray BoxText = new((int)Locale.Total);
    }

    public class PlayerChoiceLocale
    {
        public StringArray Question = new((int)Locale.Total);
        public Dictionary<int /*ResponseId*/, PlayerChoiceResponseLocale> Responses = new();
    }

    public class PlayerChoiceResponseLocale
    {
        public StringArray Answer = new((int)Locale.Total);
        public StringArray Header = new((int)Locale.Total);
        public StringArray SubHeader = new((int)Locale.Total);
        public StringArray ButtonTooltip = new((int)Locale.Total);
        public StringArray Description = new((int)Locale.Total);
        public StringArray Confirmation = new((int)Locale.Total);
    }

    public class PlayerChoiceResponseRewardItem
    {
        public PlayerChoiceResponseRewardItem() { }
        public PlayerChoiceResponseRewardItem(uint id, List<uint> bonusListIDs, int quantity)
        {
            Id = id;
            BonusListIDs = bonusListIDs;
            Quantity = quantity;
        }

        public uint Id;
        public List<uint> BonusListIDs = new();
        public int Quantity;
    }

    public class PlayerChoiceResponseRewardEntry
    {
        public PlayerChoiceResponseRewardEntry(uint id, int quantity)
        {
            Id = id;
            Quantity = quantity;
        }

        public uint Id;
        public int Quantity;
    }

    public class PlayerChoiceResponseReward
    {
        public int TitleId;
        public int PackageId;
        public int SkillLineId;
        public uint SkillPointCount;
        public uint ArenaPointCount;
        public uint HonorPointCount;
        public ulong Money;
        public uint Xp;

        public List<PlayerChoiceResponseRewardItem> Items = new();
        public List<PlayerChoiceResponseRewardEntry> Currency = new();
        public List<PlayerChoiceResponseRewardEntry> Faction = new();
        public List<PlayerChoiceResponseRewardItem> ItemChoices = new();
    }

    public struct PlayerChoiceResponseMawPower
    {
        public int TypeArtFileID;
        public int Rarity;
        public uint RarityColor;
        public int SpellID;
        public int MaxStacks;
    }

    public class PlayerChoiceResponse
    {
        public int ResponseId;
        public ushort ResponseIdentifier;
        public int ChoiceArtFileId;
        public int Flags;
        public uint WidgetSetID;
        public uint UiTextureAtlasElementID;
        public uint SoundKitID;
        public byte GroupID;
        public int UiTextureKitID;
        public string Answer;
        public string Header;
        public string SubHeader;
        public string ButtonTooltip;
        public string Description;
        public string Confirmation;
        public Optional<PlayerChoiceResponseReward> Reward;
        public Optional<uint> RewardQuestID;
        public Optional<PlayerChoiceResponseMawPower> MawPower;
    }

    public class PlayerChoice
    {
        public PlayerChoiceResponse GetResponse(int responseId)
        {
            return Responses.FirstOrDefault(playerChoiceResponse => playerChoiceResponse.ResponseId == responseId);
        }

        public int ChoiceId;
        public int UiTextureKitId;
        public uint SoundKitId;
        public string Question;
        public List<PlayerChoiceResponse> Responses = new();
        public bool HideWarboardHeader;
        public bool KeepOpenAfterChoice;
    }

    public class ClassAvailability
    {
        public byte ClassID;
        public byte ActiveExpansionLevel;
        public byte AccountExpansionLevel;
    }

    public class RaceClassAvailability
    {
        public byte RaceID;
        public List<ClassAvailability> Classes = new();
    }

    public class RaceUnlockRequirement
    {
        public byte Expansion;
        public uint AchievementId;
    }
}
