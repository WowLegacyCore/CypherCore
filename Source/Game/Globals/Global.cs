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

using Game;
using Game.AI;
using Game.Arenas;
using Game.BattleFields;
using Game.BattleGrounds;
using Game.Cache;
using Game.Chat;
using Game.Collision;
using Game.DataStorage;
using Game.DungeonFinding;
using Game.Entities;
using Game.Groups;
using Game.Loots;
using Game.Maps;
using Game.PvP;
using Game.Scripting;
using Game.Services;
using Game.SupportSystem;

public static class Global
{
    //Main
    public static ObjectAccessor ObjAccessor => ObjectAccessor.Instance;
    public static ObjectManager ObjectMgr => ObjectManager.Instance;
    public static WorldManager WorldMgr => WorldManager.Instance;
    public static RealmManager RealmMgr => RealmManager.Instance;
    public static WorldServiceManager ServiceMgr => WorldServiceManager.Instance;

    //Guild
    public static PetitionManager PetitionMgr => PetitionManager.Instance;
    public static GuildManager GuildMgr => GuildManager.Instance;

    //Social
    public static CalendarManager CalendarMgr => CalendarManager.Instance;
    public static SocialManager SocialMgr => SocialManager.Instance;
    public static WhoListStorageManager WhoListStorageMgr => WhoListStorageManager.Instance;

    //Scripts
    public static ScriptManager ScriptMgr => ScriptManager.Instance;
    public static SmartAIManager SmartAIMgr => SmartAIManager.Instance;

    //Groups
    public static GroupManager GroupMgr => GroupManager.Instance;
    public static LFGManager LFGMgr => LFGManager.Instance;
    public static ArenaTeamManager ArenaTeamMgr => ArenaTeamManager.Instance;

    //Maps System
    public static MapManager MapMgr => MapManager.Instance;
    public static MMapManager MMapMgr => MMapManager.Instance;
    public static VMapManager VMapMgr => VMapManager.Instance;
    public static WaypointManager WaypointMgr => WaypointManager.Instance;
    public static TransportManager TransportMgr => TransportManager.Instance;
    public static InstanceSaveManager InstanceSaveMgr => InstanceSaveManager.Instance;
    public static TaxiPathGraph TaxiPathGraph => TaxiPathGraph.Instance;

    //PVP
    public static BattlegroundManager BattlegroundMgr => BattlegroundManager.Instance;
    public static OutdoorPvPManager OutdoorPvPMgr => OutdoorPvPManager.Instance;
    public static BattleFieldManager BattleFieldMgr => BattleFieldManager.Instance;

    //Account
    public static AccountManager AccountMgr => AccountManager.Instance;
    public static BNetAccountManager BNetAccountMgr => BNetAccountManager.Instance;

    //DataStorage
    public static AreaTriggerDataStorage AreaTriggerDataStorage => AreaTriggerDataStorage.Instance;
    public static CharacterTemplateDataStorage CharacterTemplateDataStorage => CharacterTemplateDataStorage.Instance;
    public static ConversationDataStorage ConversationDataStorage => ConversationDataStorage.Instance;
    public static CharacterCache CharacterCacheStorage => CharacterCache.Instance;
    public static LootItemStorage LootItemStorage => LootItemStorage.Instance;

    //Misc
    public static ConditionManager ConditionMgr => ConditionManager.Instance;
    public static DB2Manager DB2Mgr => DB2Manager.Instance;
    public static DisableManager DisableMgr => DisableManager.Instance;
    public static PoolManager PoolMgr => PoolManager.Instance;
    public static WeatherManager WeatherMgr => WeatherManager.Instance;

    public static GameEventManager GameEventMgr => GameEventManager.Instance;
    public static LanguageManager LanguageMgr => LanguageManager.Instance;
    public static CreatureTextManager CreatureTextMgr => CreatureTextManager.Instance;
    public static AuctionManager AuctionHouseMgr => AuctionManager.Instance;

    public static SpellManager SpellMgr => SpellManager.Instance;
    public static SupportManager SupportMgr => SupportManager.Instance;
    public static WardenCheckManager WardenCheckMgr => WardenCheckManager.Instance;
}
