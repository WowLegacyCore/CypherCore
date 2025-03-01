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
using Framework.IO;
using Game.DataStorage;
using Game.Entities;
using Game.Groups;
using Game.Maps;
using Game.Movement;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Chat
{
    class MiscCommands
    {
        [CommandNonGroup("additem", RBACPermissions.CommandAdditem)]
        static bool HandleAddItemCommand(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            uint itemId = 0;

            if (args[0] == '[')                                        // [name] manual form
            {
                string itemName = args.NextString("]");

                if (!string.IsNullOrEmpty(itemName))
                {
                    var record = CliDB.ItemSparseStorage.Values.FirstOrDefault(itemSparse =>
                    {
                        for (Locale i = 0; i < Locale.Total; ++i)
                            if (itemName == itemSparse.Display[i])
                                return true;
                        return false;
                    });

                    if (record == null)
                    {
                        handler.SendSysMessage(CypherStrings.CommandCouldnotfind, itemName);
                        return false;
                    }
                    itemId = record.Id;
                }
                else
                    return false;
            }
            else                                                    // item_id or [name] Shift-click form |color|Hitem:item_id:0:0:0|h[name]|h|r
            {
                string idStr = handler.ExtractKeyFromLink(args, "Hitem");
                if (string.IsNullOrEmpty(idStr))
                    return false;

                if (!uint.TryParse(idStr, out itemId))
                    return false;
            }

            int count = args.NextInt32();
            if (count == 0)
                count = 1;

            List<uint> bonusListIDs = new();
            var bonuses = args.NextString();
            var context = args.NextString();

            // semicolon separated bonuslist ids (parse them after all arguments are extracted by strtok!)
            if (!bonuses.IsEmpty())
            {
                var tokens = new StringArray(bonuses, ';');
                for (var i = 0; i < tokens.Length; ++i)
                {
                    if (uint.TryParse(tokens[i], out uint id))
                        bonusListIDs.Add(id);
                }
            }

            ItemContext itemContext = ItemContext.None;
            if (!context.IsEmpty())
            {
                itemContext = context.ToEnum<ItemContext>();
                if (itemContext != ItemContext.None && itemContext < ItemContext.Max)
                {
                    var contextBonuses = Global.DB2Mgr.GetDefaultItemBonusTree(itemId, itemContext);
                    bonusListIDs.AddRange(contextBonuses);
                }
            }

            Player player = handler.GetSession().GetPlayer();
            Player playerTarget = handler.GetSelectedPlayer();
            if (!playerTarget)
                playerTarget = player;

            Log.outDebug(LogFilter.Server, Global.ObjectMgr.GetCypherString(CypherStrings.Additem), itemId, count);

            ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(itemId);
            if (itemTemplate == null)
            {
                handler.SendSysMessage(CypherStrings.CommandItemidinvalid, itemId);
                return false;
            }

            // Subtract
            if (count < 0)
            {
                playerTarget.DestroyItemCount(itemId, (uint)-count, true, false);
                handler.SendSysMessage(CypherStrings.Removeitem, itemId, -count, handler.GetNameLink(playerTarget));
                return true;
            }

            // Adding items
            uint noSpaceForCount = 0;

            // check space and find places
            List<ItemPosCount> dest = new();
            InventoryResult msg = playerTarget.CanStoreNewItem(ItemConst.NullBag, ItemConst.NullSlot, dest, itemId, (uint)count, out noSpaceForCount);
            if (msg != InventoryResult.Ok)                               // convert to possible store amount
                count -= (int)noSpaceForCount;

            if (count == 0 || dest.Empty())                         // can't add any
            {
                handler.SendSysMessage(CypherStrings.ItemCannotCreate, itemId, noSpaceForCount);
                return false;
            }

            Item item = playerTarget.StoreNewItem(dest, itemId, true, ItemEnchantmentManager.GenerateItemRandomBonusListId(itemId), null, itemContext, bonusListIDs);

            // remove binding (let GM give it to another player later)
            if (player == playerTarget)
            {
                foreach (var posCount in dest)
                {
                    Item item1 = player.GetItemByPos(posCount.pos);
                    if (item1)
                        item1.SetBinding(false);
                }
            }

            if (count > 0 && item)
            {
                player.SendNewItem(item, (uint)count, false, true);
                if (player != playerTarget)
                    playerTarget.SendNewItem(item, (uint)count, true, false);
            }

            if (noSpaceForCount > 0)
                handler.SendSysMessage(CypherStrings.ItemCannotCreate, itemId, noSpaceForCount);

            return true;
        }

        [CommandNonGroup("additemset", RBACPermissions.CommandAdditemset)]
        static bool HandleAddItemSetCommand(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            string idStr = handler.ExtractKeyFromLink(args, "Hitemset"); // number or [name] Shift-click form |color|Hitemset:itemset_id|h[name]|h|r
            if (string.IsNullOrEmpty(idStr))
                return false;

            // prevent generation all items with itemset field value '0'
            if (!uint.TryParse(idStr, out uint itemSetId) || itemSetId == 0)
            {
                handler.SendSysMessage(CypherStrings.NoItemsFromItemsetFound, itemSetId);
                return false;
            }

            List<uint> bonusListIDs = new();
            var bonuses = args.NextString();
            var context = args.NextString();

            // semicolon separated bonuslist ids (parse them after all arguments are extracted by strtok!)
            if (!bonuses.IsEmpty())
            {
                var tokens = new StringArray(bonuses, ';');
                for (var i = 0; i < tokens.Length; ++i)
                {
                    if (uint.TryParse(tokens[i], out uint id))
                        bonusListIDs.Add(id);
                }
            }

            ItemContext itemContext = ItemContext.None;
            if (!context.IsEmpty())
                itemContext = context.ToEnum<ItemContext>();

            Player player = handler.GetSession().GetPlayer();
            Player playerTarget = handler.GetSelectedPlayer();
            if (!playerTarget)
                playerTarget = player;

            Log.outDebug(LogFilter.Server, Global.ObjectMgr.GetCypherString(CypherStrings.Additemset), itemSetId);

            bool found = false;
            var its = Global.ObjectMgr.GetItemTemplates();
            foreach (var template in its)
            {
                if (template.Value.GetItemSet() != itemSetId)
                    continue;

                found = true;
                List<ItemPosCount> dest = new();
                InventoryResult msg = playerTarget.CanStoreNewItem(ItemConst.NullBag, ItemConst.NullSlot, dest, template.Value.GetId(), 1);
                if (msg == InventoryResult.Ok)
                {
                    List<uint> bonusListIDsForItem = new(bonusListIDs); // copy, bonuses for each depending on context might be different for each item
                    if (itemContext != ItemContext.None && itemContext < ItemContext.Max)
                    {
                        var contextBonuses = Global.DB2Mgr.GetDefaultItemBonusTree(template.Value.GetId(), itemContext);
                        bonusListIDsForItem.AddRange(contextBonuses);
                    }

                    Item item = playerTarget.StoreNewItem(dest, template.Value.GetId(), true, 0, null, itemContext, bonusListIDsForItem);

                    // remove binding (let GM give it to another player later)
                    if (player == playerTarget)
                        item.SetBinding(false);

                    player.SendNewItem(item, 1, false, true);
                    if (player != playerTarget)
                        playerTarget.SendNewItem(item, 1, true, false);
                }
                else
                {
                    player.SendEquipError(msg, null, null, template.Value.GetId());
                    handler.SendSysMessage(CypherStrings.ItemCannotCreate, template.Value.GetId(), 1);
                }
            }

            if (!found)
            {
                handler.SendSysMessage(CypherStrings.CommandNoitemsetfound, itemSetId);
                return false;
            }
            return true;
        }

        // Teleport to Player
        [CommandNonGroup("appear", RBACPermissions.CommandAppear)]
        static bool HandleAppearCommand(StringArguments args, CommandHandler handler)
        {
            Player target;
            ObjectGuid targetGuid;
            string targetName;
            if (!handler.ExtractPlayerTarget(args, out target, out targetGuid, out targetName))
                return false;

            Player _player = handler.GetSession().GetPlayer();
            if (target == _player || targetGuid == _player.GetGUID())
            {
                handler.SendSysMessage(CypherStrings.CantTeleportSelf);
                return false;
            }

            if (target)
            {
                // check online security
                if (handler.HasLowerSecurity(target, ObjectGuid.Empty))
                    return false;

                string chrNameLink = handler.PlayerLink(targetName);

                Map map = target.GetMap();
                if (map.IsBattlegroundOrArena())
                {
                    // only allow if gm mode is on
                    if (!_player.IsGameMaster())
                    {
                        handler.SendSysMessage(CypherStrings.CannotGoToBgGm, chrNameLink);
                        return false;
                    }
                    // if both players are in different bgs
                    else if (_player.GetBattlegroundId() != 0 && _player.GetBattlegroundId() != target.GetBattlegroundId())
                        _player.LeaveBattleground(false); // Note: should be changed so _player gets no Deserter debuff

                    // all's well, set bg id
                    // when porting out from the bg, it will be reset to 0
                    _player.SetBattlegroundId(target.GetBattlegroundId(), target.GetBattlegroundTypeId());
                    // remember current position as entry point for return at bg end teleportation
                    if (!_player.GetMap().IsBattlegroundOrArena())
                        _player.SetBattlegroundEntryPoint();
                }
                else if (map.IsDungeon())
                {
                    // we have to go to instance, and can go to player only if:
                    //   1) we are in his group (either as leader or as member)
                    //   2) we are not bound to any group and have GM mode on
                    if (_player.GetGroup())
                    {
                        // we are in group, we can go only if we are in the player group
                        if (_player.GetGroup() != target.GetGroup())
                        {
                            handler.SendSysMessage(CypherStrings.CannotGoToInstParty, chrNameLink);
                            return false;
                        }
                    }
                    else
                    {
                        // we are not in group, let's verify our GM mode
                        if (!_player.IsGameMaster())
                        {
                            handler.SendSysMessage(CypherStrings.CannotGoToInstGm, chrNameLink);
                            return false;
                        }
                    }

                    // if the player or the player's group is bound to another instance
                    // the player will not be bound to another one
                    InstanceBind bind = _player.GetBoundInstance(target.GetMapId(), target.GetDifficultyID(map.GetEntry()));
                    if (bind == null)
                    {
                        Group group = _player.GetGroup();
                        // if no bind exists, create a solo bind
                        InstanceBind gBind = group ? group.GetBoundInstance(target) : null;                // if no bind exists, create a solo bind
                        if (gBind == null)
                        {
                            InstanceSave save = Global.InstanceSaveMgr.GetInstanceSave(target.GetInstanceId());
                            if (save != null)
                                _player.BindToInstance(save, !save.CanReset());
                        }
                    }

                    if (map.IsRaid())
                    {
                        _player.SetRaidDifficultyID(target.GetRaidDifficultyID());
                        _player.SetLegacyRaidDifficultyID(target.GetLegacyRaidDifficultyID());
                    }
                    else
                        _player.SetDungeonDifficultyID(target.GetDungeonDifficultyID());
                }

                handler.SendSysMessage(CypherStrings.AppearingAt, chrNameLink);

                // stop flight if need
                if (_player.IsInFlight())
                {
                    _player.GetMotionMaster().MovementExpired();
                    _player.CleanupAfterTaxiFlight();
                }
                // save only in non-flight case
                else
                    _player.SaveRecallPosition();

                // to point to see at target with same orientation
                float x, y, z;
                target.GetContactPoint(_player, out x, out y, out z);

                _player.TeleportTo(target.GetMapId(), x, y, z, _player.GetAngle(target), TeleportToOptions.GMMode);
                PhasingHandler.InheritPhaseShift(_player, target);
                _player.UpdateObjectVisibility();
            }
            else
            {
                // check offline security
                if (handler.HasLowerSecurity(null, targetGuid))
                    return false;

                string nameLink = handler.PlayerLink(targetName);

                handler.SendSysMessage(CypherStrings.AppearingAt, nameLink);

                // to point where player stay (if loaded)
                WorldLocation loc;
                if (!Player.LoadPositionFromDB(out loc, out _, targetGuid))
                    return false;

                // stop flight if need
                if (_player.IsInFlight())
                {
                    _player.GetMotionMaster().MovementExpired();
                    _player.CleanupAfterTaxiFlight();
                }
                // save only in non-flight case
                else
                    _player.SaveRecallPosition();

                loc.SetOrientation(_player.GetOrientation());
                _player.TeleportTo(loc);
            }

            return true;
        }

        [CommandNonGroup("bank", RBACPermissions.CommandBank)]
        static bool HandleBankCommand(StringArguments args, CommandHandler handler)
        {
            handler.GetSession().SendShowBank(handler.GetSession().GetPlayer().GetGUID());
            return true;
        }

        [CommandNonGroup("bindsight", RBACPermissions.CommandBindsight)]
        static bool HandleBindSightCommand(StringArguments args, CommandHandler handler)
        {
            Unit unit = handler.GetSelectedUnit();
            if (!unit)
                return false;

            handler.GetSession().GetPlayer().CastSpell(unit, 6277, true);
            return true;
        }

        [CommandNonGroup("combatstop", RBACPermissions.CommandCombatstop, true)]
        static bool HandleCombatStopCommand(StringArguments args, CommandHandler handler)
        {
            Player target = null;

            if (!args.Empty())
            {
                target = Global.ObjAccessor.FindPlayerByName(args.NextString());
                if (!target)
                {
                    handler.SendSysMessage(CypherStrings.PlayerNotFound);
                    return false;
                }
            }

            if (!target)
            {
                if (!handler.ExtractPlayerTarget(args, out target))
                    return false;
            }

            // check online security
            if (handler.HasLowerSecurity(target, ObjectGuid.Empty))
                return false;

            target.CombatStop();
            return true;
        }

        [CommandNonGroup("cometome", RBACPermissions.CommandCometome)]
        static bool HandleComeToMeCommand(StringArguments args, CommandHandler handler)
        {
            Creature caster = handler.GetSelectedCreature();
            if (!caster)
            {
                handler.SendSysMessage(CypherStrings.SelectCreature);
                return false;
            }

            Player player = handler.GetSession().GetPlayer();
            caster.GetMotionMaster().MovePoint(0, player.GetPositionX(), player.GetPositionY(), player.GetPositionZ());

            return true;
        }

        [CommandNonGroup("commands", RBACPermissions.CommandCommands, true)]
        static bool HandleCommandsCommand(StringArguments args, CommandHandler handler)
        {
            string list = "";
            foreach (var command in CommandManager.GetCommands())
            {
                if (handler.IsAvailable(command))
                {
                    if (handler.GetSession() != null)
                        list += "\n    ";
                    else
                        list += "\n\r    ";

                    list += command.Name;

                    if (!command.ChildCommands.Empty())
                        list += " ...";
                }
            }

            if (list.IsEmpty())
                return false;

            handler.SendSysMessage(CypherStrings.AvailableCmd);
            handler.SendSysMessage(list);
            return true;
        }

        [CommandNonGroup("damage", RBACPermissions.CommandDamage)]
        static bool HandleDamageCommand(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            string str = args.NextString();

            if (str == "go")
            {
                ulong guidLow = args.NextUInt64();
                if (guidLow == 0)
                {
                    handler.SendSysMessage(CypherStrings.BadValue);
                    return false;
                }

                int damage = args.NextInt32();
                if (damage == 0)
                {
                    handler.SendSysMessage(CypherStrings.BadValue);
                    return false;
                }
                Player player = handler.GetSession().GetPlayer();
                if (player)
                {
                    GameObject go = handler.GetObjectFromPlayerMapByDbGuid(guidLow);
                    if (!go)
                    {
                        handler.SendSysMessage(CypherStrings.CommandObjnotfound, guidLow);
                        return false;
                    }

                    if (!go.IsDestructibleBuilding())
                    {
                        handler.SendSysMessage(CypherStrings.InvalidGameobjectType);
                        return false;
                    }

                    go.ModifyHealth(-damage, player);
                    handler.SendSysMessage(CypherStrings.GameobjectDamaged, go.GetName(), guidLow, -damage, go.GetGoValue().Building.Health);
                }

                return true;
            }

            Unit target = handler.GetSelectedUnit();
            if (!target || handler.GetSession().GetPlayer().GetTarget().IsEmpty())
            {
                handler.SendSysMessage(CypherStrings.SelectCharOrCreature);
                return false;
            }
            Player player_ = target.ToPlayer();
            if (player_)
                if (handler.HasLowerSecurity(player_, ObjectGuid.Empty, false))
                    return false;

            if (!target.IsAlive())
                return true;

            if (!int.TryParse(str, out int damage_int))
                return false;

            if (damage_int <= 0)
                return true;

            uint damage_ = (uint)damage_int;

            string schoolStr = args.NextString();

            Player attacker = handler.GetSession().GetPlayer();

            // flat melee damage without resistence/etc reduction
            if (string.IsNullOrEmpty(schoolStr))
            {
                Unit.DealDamage(attacker, target, damage_, null, DamageEffectType.Direct, SpellSchoolMask.Normal, null, false);
                if (target != attacker)
                    attacker.SendAttackStateUpdate(HitInfo.AffectsVictim, target, SpellSchoolMask.Normal, damage_, 0, 0, VictimState.Hit, 0);
                return true;
            }

            if (!int.TryParse(schoolStr, out int school) || school >= (int)SpellSchools.Max)
                return false;

            SpellSchoolMask schoolmask = (SpellSchoolMask)(1 << school);

            if (Unit.IsDamageReducedByArmor(schoolmask))
                damage_ = Unit.CalcArmorReducedDamage(handler.GetPlayer(), target, damage_, null, WeaponAttackType.BaseAttack);

            string spellStr = args.NextString();

            // melee damage by specific school
            if (string.IsNullOrEmpty(spellStr))
            {
                DamageInfo dmgInfo = new(attacker, target, damage_, null, schoolmask, DamageEffectType.SpellDirect, WeaponAttackType.BaseAttack);
                Unit.CalcAbsorbResist(dmgInfo);

                if (dmgInfo.GetDamage() == 0)
                    return true;

                damage_ = dmgInfo.GetDamage();

                uint absorb = dmgInfo.GetAbsorb();
                uint resist = dmgInfo.GetResist();
                Unit.DealDamageMods(attacker, target, ref damage_, ref absorb);
                Unit.DealDamage(attacker, target, damage_, null, DamageEffectType.Direct, schoolmask, null, false);
                attacker.SendAttackStateUpdate(HitInfo.AffectsVictim, target, schoolmask, damage_, absorb, resist, VictimState.Hit, 0);
                return true;
            }

            // non-melee damage
            // number or [name] Shift-click form |color|Hspell:spell_id|h[name]|h|r or Htalent form
            uint spellid = handler.ExtractSpellIdFromLink(args);
            if (spellid == 0)
                return false;

            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellid, attacker.GetMap().GetDifficultyID());
            if (spellInfo == null)
                return false;

            SpellNonMeleeDamage damageInfo = new(attacker, target, spellInfo, new SpellCastVisual(spellInfo.GetSpellXSpellVisualId(attacker), 0), spellInfo.SchoolMask);
            damageInfo.damage = damage_;
            Unit.DealDamageMods(damageInfo.attacker, damageInfo.target, ref damageInfo.damage, ref damageInfo.absorb);
            target.DealSpellDamage(damageInfo, true);
            target.SendSpellNonMeleeDamageLog(damageInfo);
            return true;
        }

        [CommandNonGroup("dev", RBACPermissions.CommandDev)]
        static bool HandleDevCommand(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
            {
                if (handler.GetSession().GetPlayer().HasPlayerFlag(PlayerFlags.Developer))
                    handler.GetSession().SendNotification(CypherStrings.DevOn);
                else
                    handler.GetSession().SendNotification(CypherStrings.DevOff);
                return true;
            }

            string argstr = args.NextString();

            if (argstr == "on")
            {
                handler.GetSession().GetPlayer().HasPlayerFlag(PlayerFlags.Developer);
                handler.GetSession().SendNotification(CypherStrings.DevOn);
                return true;
            }

            if (argstr == "off")
            {
                handler.GetSession().GetPlayer().RemovePlayerFlag(PlayerFlags.Developer);
                handler.GetSession().SendNotification(CypherStrings.DevOff);
                return true;
            }

            handler.SendSysMessage(CypherStrings.UseBol);
            return false;
        }

        [CommandNonGroup("die", RBACPermissions.CommandDie)]
        static bool HandleDieCommand(StringArguments args, CommandHandler handler)
        {
            Unit target = handler.GetSelectedUnit();

            if (!target && handler.GetPlayer().GetTarget().IsEmpty())
            {
                handler.SendSysMessage(CypherStrings.SelectCharOrCreature);
                return false;
            }

            Player player = target.ToPlayer();
            if (player)
                if (handler.HasLowerSecurity(player, ObjectGuid.Empty, false))
                    return false;

            if (target.IsAlive())
                Unit.Kill(handler.GetSession().GetPlayer(), target);

            return true;
        }

        [CommandNonGroup("dismount", RBACPermissions.CommandDismount)]
        static bool HandleDismountCommand(StringArguments args, CommandHandler handler)
        {
            Player player = handler.GetSelectedPlayerOrSelf();

            // If player is not mounted, so go out :)
            if (!player.IsMounted())
            {
                handler.SendSysMessage(CypherStrings.CharNonMounted);
                return false;
            }

            if (player.IsInFlight())
            {
                handler.SendSysMessage(CypherStrings.CharInFlight);
                return false;
            }

            player.Dismount();
            player.RemoveAurasByType(AuraType.Mounted);
            return true;
        }

        [CommandNonGroup("distance", RBACPermissions.CommandDistance)]
        static bool HandleGetDistanceCommand(StringArguments args, CommandHandler handler)
        {
            WorldObject obj;

            if (!args.Empty())
            {
                HighGuid guidHigh = 0;
                ulong guidLow = handler.ExtractLowGuidFromLink(args, ref guidHigh);
                if (guidLow == 0)
                    return false;
                switch (guidHigh)
                {
                    case HighGuid.Player:
                        {
                            obj = Global.ObjAccessor.FindPlayer(ObjectGuid.Create(HighGuid.Player, guidLow));
                            if (!obj)
                            {
                                handler.SendSysMessage(CypherStrings.PlayerNotFound);
                            }
                            break;
                        }
                    case HighGuid.Creature:
                        {
                            obj = handler.GetCreatureFromPlayerMapByDbGuid(guidLow);
                            if (!obj)
                            {
                                handler.SendSysMessage(CypherStrings.CommandNocreaturefound);
                            }
                            break;
                        }
                    case HighGuid.GameObject:
                        {
                            obj = handler.GetObjectFromPlayerMapByDbGuid(guidLow);
                            if (!obj)
                            {
                                handler.SendSysMessage(CypherStrings.CommandNogameobjectfound);
                            }
                            break;
                        }
                    default:
                        return false;
                }
                if (!obj)
                    return false;
            }
            else
            {
                obj = handler.GetSelectedUnit();

                if (!obj)
                {
                    handler.SendSysMessage(CypherStrings.SelectCharOrCreature);
                    return false;
                }
            }

            handler.SendSysMessage(CypherStrings.Distance, handler.GetSession().GetPlayer().GetDistance(obj), handler.GetSession().GetPlayer().GetDistance2d(obj), handler.GetSession().GetPlayer().GetExactDist(obj), handler.GetSession().GetPlayer().GetExactDist2d(obj));
            return true;
        }

        [CommandNonGroup("freeze", RBACPermissions.CommandFreeze)]
        static bool HandleFreezeCommand(StringArguments args, CommandHandler handler)
        {
            Player player = handler.GetSelectedPlayer(); // Selected player, if any. Might be null.
            int freezeDuration = 0; // Freeze Duration (in seconds)
            bool canApplyFreeze = false; // Determines if every possible argument is set so Freeze can be applied
            bool getDurationFromConfig = false; // If there's no given duration, we'll retrieve the world cfg value later

            if (args.Empty())
            {
                // Might have a selected player. We'll check it later
                // Get the duration from world cfg
                getDurationFromConfig = true;
            }
            else
            {
                // Get the args that we might have (up to 2)
                string arg1 = args.NextString();
                string arg2 = args.NextString();

                // Analyze them to see if we got either a playerName or duration or both
                if (!arg1.IsEmpty())
                {
                    if (arg1.IsNumber())
                    {
                        // case 2: .freeze duration
                        // We have a selected player. We'll check him later
                        if (!int.TryParse(arg1, out freezeDuration))
                            return false;
                        canApplyFreeze = true;
                    }
                    else
                    {
                        // case 3 or 4: .freeze player duration | .freeze player
                        // find the player
                        string name = arg1;
                        ObjectManager.NormalizePlayerName(ref name);
                        player = Global.ObjAccessor.FindPlayerByName(name);
                        // Check if we have duration set
                        if (!arg2.IsEmpty() && arg2.IsNumber())
                        {
                            if (!int.TryParse(arg2, out freezeDuration))
                                return false;
                            canApplyFreeze = true;
                        }
                        else
                            getDurationFromConfig = true;
                    }
                }
            }

            // Check if duration needs to be retrieved from config
            if (getDurationFromConfig)
            {
                freezeDuration = WorldConfig.GetIntValue(WorldCfg.GmFreezeDuration);
                canApplyFreeze = true;
            }

            // Player and duration retrieval is over
            if (canApplyFreeze)
            {
                if (!player) // can be null if some previous selection failed
                {
                    handler.SendSysMessage(CypherStrings.CommandFreezeWrong);
                    return true;
                }
                else if (player == handler.GetSession().GetPlayer())
                {
                    // Can't freeze himself
                    handler.SendSysMessage(CypherStrings.CommandFreezeError);
                    return true;
                }
                else // Apply the effect
                {
                    // Add the freeze aura and set the proper duration
                    // Player combat status and flags are now handled
                    // in Freeze Spell AuraScript (OnApply)
                    Aura freeze = player.AddAura(9454, player);
                    if (freeze != null)
                    {
                        if (freezeDuration != 0)
                            freeze.SetDuration(freezeDuration * Time.InMilliseconds);
                        handler.SendSysMessage(CypherStrings.CommandFreeze, player.GetName());
                        // save player
                        player.SaveToDB();
                        return true;
                    }
                }
            }
            return false;
        }

        [CommandNonGroup("gps", RBACPermissions.CommandGps)]
        static bool HandleGPSCommand(StringArguments args, CommandHandler handler)
        {
            WorldObject obj;
            if (!args.Empty())
            {
                HighGuid guidHigh = 0;
                ulong guidLow = handler.ExtractLowGuidFromLink(args, ref guidHigh);
                if (guidLow == 0)
                    return false;
                switch (guidHigh)
                {
                    case HighGuid.Player:
                        {
                            obj = Global.ObjAccessor.FindPlayer(ObjectGuid.Create(HighGuid.Player, guidLow));
                            if (!obj)
                            {
                                handler.SendSysMessage(CypherStrings.PlayerNotFound);
                            }
                            break;
                        }
                    case HighGuid.Creature:
                        {
                            obj = handler.GetCreatureFromPlayerMapByDbGuid(guidLow);
                            if (!obj)
                            {
                                handler.SendSysMessage(CypherStrings.CommandNocreaturefound);
                            }
                            break;
                        }
                    case HighGuid.GameObject:
                        {
                            obj = handler.GetObjectFromPlayerMapByDbGuid(guidLow);
                            if (!obj)
                            {
                                handler.SendSysMessage(CypherStrings.CommandNogameobjectfound);
                            }
                            break;
                        }
                    default:
                        return false;
                }
                if (!obj)
                    return false;
            }
            else
            {
                obj = handler.GetSelectedUnit();

                if (!obj)
                {
                    handler.SendSysMessage(CypherStrings.SelectCharOrCreature);
                    return false;
                }
            }

            CellCoord cellCoord = GridDefines.ComputeCellCoord(obj.GetPositionX(), obj.GetPositionY());
            Cell cell = new(cellCoord);

            uint zoneId, areaId;
            obj.GetZoneAndAreaId(out zoneId, out areaId);
            uint mapId = obj.GetMapId();

            MapRecord mapEntry = CliDB.MapStorage.LookupByKey(mapId);
            AreaTableRecord zoneEntry = CliDB.AreaTableStorage.LookupByKey(zoneId);
            AreaTableRecord areaEntry = CliDB.AreaTableStorage.LookupByKey(areaId);

            float zoneX = obj.GetPositionX();
            float zoneY = obj.GetPositionY();

            Global.DB2Mgr.Map2ZoneCoordinates((int)zoneId, ref zoneX, ref zoneY);

            Map map = obj.GetMap();
            float groundZ = obj.GetMapHeight(obj.GetPositionX(), obj.GetPositionY(), MapConst.MaxHeight);
            float floorZ = obj.GetMapHeight(obj.GetPositionX(), obj.GetPositionY(), obj.GetPositionZ());

            GridCoord gridCoord = GridDefines.ComputeGridCoord(obj.GetPositionX(), obj.GetPositionY());

            // 63? WHY?
            uint gridX = (MapConst.MaxGrids - 1) - gridCoord.X_coord;
            uint gridY = (MapConst.MaxGrids - 1) - gridCoord.Y_coord;

            bool haveMap = Map.ExistMap(mapId, gridX, gridY);
            bool haveVMap = Map.ExistVMap(mapId, gridX, gridY);
            bool haveMMap = (Global.DisableMgr.IsPathfindingEnabled(mapId) && Global.MMapMgr.GetNavMesh(handler.GetSession().GetPlayer().GetMapId()) != null);

            if (haveVMap)
            {
                if (map.IsOutdoors(obj.GetPhaseShift(), obj.GetPositionX(), obj.GetPositionY(), obj.GetPositionZ()))
                    handler.SendSysMessage(CypherStrings.GpsPositionOutdoors);
                else
                    handler.SendSysMessage(CypherStrings.GpsPositionIndoors);
            }
            else
                handler.SendSysMessage(CypherStrings.GpsNoVmap);

            string unknown = handler.GetCypherString(CypherStrings.Unknown);

            handler.SendSysMessage(CypherStrings.MapPosition,
                mapId, (mapEntry != null ? mapEntry.MapName[handler.GetSessionDbcLocale()] : unknown),
                zoneId, (zoneEntry != null ? zoneEntry.AreaName[handler.GetSessionDbcLocale()] : unknown),
                areaId, (areaEntry != null ? areaEntry.AreaName[handler.GetSessionDbcLocale()] : unknown),
                obj.GetPositionX(), obj.GetPositionY(), obj.GetPositionZ(), obj.GetOrientation());

            Transport transport = obj.GetTransport();
            if (transport)
            {
                handler.SendSysMessage(CypherStrings.TransportPosition, transport.GetGoInfo().MoTransport.SpawnMap, obj.GetTransOffsetX(), obj.GetTransOffsetY(), obj.GetTransOffsetZ(), obj.GetTransOffsetO(),
                    transport.GetEntry(), transport.GetName());
            }

            handler.SendSysMessage(CypherStrings.GridPosition, cell.GetGridX(), cell.GetGridY(), cell.GetCellX(), cell.GetCellY(), obj.GetInstanceId(),
                zoneX, zoneY, groundZ, floorZ, haveMap, haveVMap, haveMMap);

            LiquidData liquidStatus;
            ZLiquidStatus status = map.GetLiquidStatus(obj.GetPhaseShift(), obj.GetPositionX(), obj.GetPositionY(), obj.GetPositionZ(), LiquidHeaderTypeFlags.AllLiquids, out liquidStatus);

            if (liquidStatus != null)
                handler.SendSysMessage(CypherStrings.LiquidStatus, liquidStatus.level, liquidStatus.depth_level, liquidStatus.entry, liquidStatus.type_flags, status);

            PhasingHandler.PrintToChat(handler, obj.GetPhaseShift());

            return true;
        }

        [CommandNonGroup("guid", RBACPermissions.CommandGuid)]
        static bool HandleGUIDCommand(StringArguments args, CommandHandler handler)
        {
            ObjectGuid guid = handler.GetSession().GetPlayer().GetTarget();

            if (guid.IsEmpty())
            {
                handler.SendSysMessage(CypherStrings.NoSelection);
                return false;
            }

            handler.SendSysMessage(CypherStrings.ObjectGuid, guid.ToString(), guid.GetHigh());
            return true;
        }

        [CommandNonGroup("help", RBACPermissions.CommandHelp, true)]
        static bool HandleHelpCommand(StringArguments args, CommandHandler handler)
        {
            string cmd = args.NextString("");
            if (cmd.IsEmpty())
            {
                handler.ShowHelpForCommand(CommandManager.GetCommands(), "help");
                handler.ShowHelpForCommand(CommandManager.GetCommands(), "");
            }
            else
            {
                if (!handler.ShowHelpForCommand(CommandManager.GetCommands(), cmd))
                    handler.SendSysMessage(CypherStrings.NoHelpCmd);
            }

            return true;
        }

        [CommandNonGroup("hidearea", RBACPermissions.CommandHidearea)]
        static bool HandleHideAreaCommand(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            Player playerTarget = handler.GetSelectedPlayer();
            if (!playerTarget)
            {
                handler.SendSysMessage(CypherStrings.NoCharSelected);
                return false;
            }

            AreaTableRecord area = CliDB.AreaTableStorage.LookupByKey(args.NextUInt32());
            if (area == null)
            {
                handler.SendSysMessage(CypherStrings.BadValue);
                return false;
            }

            if (area.AreaBit < 0)
            {
                handler.SendSysMessage(CypherStrings.BadValue);
                return false;
            }

            uint offset = (uint)area.AreaBit / 64;
            if (offset >= PlayerConst.ExploredZonesSize)
            {
                handler.SendSysMessage(CypherStrings.BadValue);
                return false;
            }

            uint val = (1u << (area.AreaBit % 64));
            playerTarget.RemoveExploredZones(offset, val);

            handler.SendSysMessage(CypherStrings.UnexploreArea);
            return true;
        }

        // move item to other slot
        [CommandNonGroup("itemmove", RBACPermissions.CommandItemmove)]
        static bool HandleItemMoveCommand(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            byte srcSlot = args.NextByte();
            byte dstSlot = args.NextByte();

            if (srcSlot == dstSlot)
                return true;

            if (handler.GetSession().GetPlayer().IsValidPos(InventorySlots.Bag0, srcSlot, true))
                return false;

            if (handler.GetSession().GetPlayer().IsValidPos(InventorySlots.Bag0, dstSlot, false))
                return false;

            ushort src = (ushort)((InventorySlots.Bag0 << 8) | srcSlot);
            ushort dst = (ushort)((InventorySlots.Bag0 << 8) | dstSlot);

            handler.GetSession().GetPlayer().SwapItem(src, dst);

            return true;
        }

        // kick player
        [CommandNonGroup("kick", RBACPermissions.CommandKick, true)]
        static bool HandleKickPlayerCommand(StringArguments args, CommandHandler handler)
        {
            Player target;
            string playerName;
            if (!handler.ExtractPlayerTarget(args, out target, out _, out playerName))
                return false;

            if (handler.GetSession() != null && target == handler.GetSession().GetPlayer())
            {
                handler.SendSysMessage(CypherStrings.CommandKickself);
                return false;
            }

            // check online security
            if (handler.HasLowerSecurity(target, ObjectGuid.Empty))
                return false;

            string kickReason = args.NextString("");
            string kickReasonStr = "No reason";
            if (kickReason != null)
                kickReasonStr = kickReason;

            if (WorldConfig.GetBoolValue(WorldCfg.ShowKickInWorld))
                Global.WorldMgr.SendWorldText(CypherStrings.CommandKickmessageWorld, (handler.GetSession() != null ? handler.GetSession().GetPlayerName() : "Server"), playerName, kickReasonStr);
            else
                handler.SendSysMessage(CypherStrings.CommandKickmessage, playerName);

            target.GetSession().KickPlayer();

            return true;
        }

        [CommandNonGroup("linkgrave", RBACPermissions.CommandLinkgrave)]
        static bool HandleLinkGraveCommand(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            uint graveyardId = args.NextUInt32();
            if (graveyardId == 0)
                return false;

            string px2 = args.NextString();
            Team team;
            if (string.IsNullOrEmpty(px2))
                team = 0;
            else if (px2 == "horde")
                team = Team.Horde;
            else if (px2 == "alliance")
                team = Team.Alliance;
            else
                return false;

            WorldSafeLocsEntry graveyard = Global.ObjectMgr.GetWorldSafeLoc(graveyardId);
            if (graveyard == null)
            {
                handler.SendSysMessage(CypherStrings.CommandGraveyardnoexist, graveyardId);
                return false;
            }

            Player player = handler.GetSession().GetPlayer();

            uint zoneId = player.GetZoneId();

            AreaTableRecord areaEntry = CliDB.AreaTableStorage.LookupByKey(zoneId);
            if (areaEntry == null || areaEntry.ParentAreaID != 0)
            {
                handler.SendSysMessage(CypherStrings.CommandGraveyardwrongzone, graveyardId, zoneId);
                return false;
            }

            if (Global.ObjectMgr.AddGraveYardLink(graveyardId, zoneId, team))
                handler.SendSysMessage(CypherStrings.CommandGraveyardlinked, graveyardId, zoneId);
            else
                handler.SendSysMessage(CypherStrings.CommandGraveyardalrlinked, graveyardId, zoneId);

            return true;
        }

        [CommandNonGroup("listfreeze", RBACPermissions.CommandListfreeze)]
        static bool HandleListFreezeCommand(StringArguments args, CommandHandler handler)
        {
            // Get names from DB
            PreparedStatement stmt = DB.Characters.GetPreparedStatement(CharStatements.SEL_CHARACTER_AURA_FROZEN);
            SQLResult result = DB.Characters.Query(stmt);
            if (result.IsEmpty())
            {
                handler.SendSysMessage(CypherStrings.CommandNoFrozenPlayers);
                return true;
            }

            // Header of the names
            handler.SendSysMessage(CypherStrings.CommandListFreeze);

            // Output of the results
            do
            {
                string player = result.Read<string>(0);
                int remaintime = result.Read<int>(1);
                // Save the frozen player to update remaining time in case of future .listfreeze uses
                // before the frozen state expires
                Player frozen = Global.ObjAccessor.FindPlayerByName(player);
                if (frozen)
                    frozen.SaveToDB();
                // Notify the freeze duration
                if (remaintime == -1) // Permanent duration
                    handler.SendSysMessage(CypherStrings.CommandPermaFrozenPlayer, player);
                else
                    // show time left (seconds)
                    handler.SendSysMessage(CypherStrings.CommandTempFrozenPlayer, player, remaintime / Time.InMilliseconds);
            }
            while (result.NextRow());

            return true;
        }

        [CommandNonGroup("mailbox", RBACPermissions.CommandMailbox)]
        static bool HandleMailBoxCommand(StringArguments args, CommandHandler handler)
        {
            Player player = handler.GetSession().GetPlayer();

            handler.GetSession().SendShowMailBox(player.GetGUID());
            return true;
        }

        [CommandNonGroup("movegens", RBACPermissions.CommandMovegens)]
        static bool HandleMovegensCommand(StringArguments args, CommandHandler handler)
        {
            Unit unit = handler.GetSelectedUnit();
            if (!unit)
            {
                handler.SendSysMessage(CypherStrings.SelectCharOrCreature);

                return false;
            }

            handler.SendSysMessage(CypherStrings.MovegensList, (unit.IsTypeId(TypeId.Player) ? "Player" : "Creature"), unit.GetGUID().ToString());

            MotionMaster motionMaster = unit.GetMotionMaster();
            float x, y, z;
            motionMaster.GetDestination(out x, out y, out z);

            for (byte i = 0; i < (int)MovementSlot.Max; ++i)
            {
                IMovementGenerator movementGenerator = motionMaster.GetMotionSlot(i);
                if (movementGenerator == null)
                {
                    handler.SendSysMessage("Empty");
                    continue;
                }

                switch (movementGenerator.GetMovementGeneratorType())
                {
                    case MovementGeneratorType.Idle:
                        handler.SendSysMessage(CypherStrings.MovegensIdle);
                        break;
                    case MovementGeneratorType.Random:
                        handler.SendSysMessage(CypherStrings.MovegensRandom);
                        break;
                    case MovementGeneratorType.Waypoint:
                        handler.SendSysMessage(CypherStrings.MovegensWaypoint);
                        break;
                    case MovementGeneratorType.Confused:
                        handler.SendSysMessage(CypherStrings.MovegensConfused);
                        break;
                    case MovementGeneratorType.Chase:
                        {
                            Unit target;
                            if (unit.IsTypeId(TypeId.Player))
                                target = ((ChaseMovementGenerator<Player>)movementGenerator).GetTarget();
                            else
                                target = ((ChaseMovementGenerator<Creature>)movementGenerator).GetTarget();

                            if (!target)
                                handler.SendSysMessage(CypherStrings.MovegensChaseNull);
                            else if (target.IsTypeId(TypeId.Player))
                                handler.SendSysMessage(CypherStrings.MovegensChasePlayer, target.GetName(), target.GetGUID().ToString());
                            else
                                handler.SendSysMessage(CypherStrings.MovegensChaseCreature, target.GetName(), target.GetGUID().ToString());
                            break;
                        }
                    case MovementGeneratorType.Follow:
                        {
                            Unit target;
                            if (unit.IsTypeId(TypeId.Player))
                                target = ((FollowMovementGenerator<Player>)movementGenerator).GetTarget();
                            else
                                target = ((FollowMovementGenerator<Creature>)movementGenerator).GetTarget();

                            if (!target)
                                handler.SendSysMessage(CypherStrings.MovegensFollowNull);
                            else if (target.IsTypeId(TypeId.Player))
                                handler.SendSysMessage(CypherStrings.MovegensFollowPlayer, target.GetName(), target.GetGUID().ToString());
                            else
                                handler.SendSysMessage(CypherStrings.MovegensFollowCreature, target.GetName(), target.GetGUID().ToString());
                            break;
                        }
                    case MovementGeneratorType.Home:
                        {
                            if (unit.IsTypeId(TypeId.Unit))
                                handler.SendSysMessage(CypherStrings.MovegensHomeCreature, x, y, z);
                            else
                                handler.SendSysMessage(CypherStrings.MovegensHomePlayer);
                            break;
                        }
                    case MovementGeneratorType.Flight:
                        handler.SendSysMessage(CypherStrings.MovegensFlight);
                        break;
                    case MovementGeneratorType.Point:
                        {
                            handler.SendSysMessage(CypherStrings.MovegensPoint, x, y, z);
                            break;
                        }
                    case MovementGeneratorType.Fleeing:
                        handler.SendSysMessage(CypherStrings.MovegensFear);
                        break;
                    case MovementGeneratorType.Distract:
                        handler.SendSysMessage(CypherStrings.MovegensDistract);
                        break;
                    case MovementGeneratorType.Effect:
                        handler.SendSysMessage(CypherStrings.MovegensEffect);
                        break;
                    default:
                        handler.SendSysMessage(CypherStrings.MovegensUnknown, movementGenerator.GetMovementGeneratorType());
                        break;
                }
            }
            return true;
        }

        // mute player for some times
        [CommandNonGroup("mute", RBACPermissions.CommandMute, true)]
        static bool HandleMuteCommand(StringArguments args, CommandHandler handler)
        {
            string nameStr;
            string delayStr;
            handler.ExtractOptFirstArg(args, out nameStr, out delayStr);
            if (string.IsNullOrEmpty(delayStr))
                return false;

            string muteReason = args.NextString();
            string muteReasonStr = "No reason";
            if (muteReason != null)
                muteReasonStr = muteReason;

            Player target;
            ObjectGuid targetGuid;
            string targetName;
            if (!handler.ExtractPlayerTarget(new StringArguments(nameStr), out target, out targetGuid, out targetName))
                return false;

            uint accountId = target ? target.GetSession().GetAccountId() : Global.CharacterCacheStorage.GetCharacterAccountIdByGuid(targetGuid);

            // find only player from same account if any
            if (!target)
            {
                WorldSession session = Global.WorldMgr.FindSession(accountId);
                if (session != null)
                    target = session.GetPlayer();
            }

            if (!uint.TryParse(delayStr, out uint notSpeakTime))
                return false;

            // must have strong lesser security level
            if (handler.HasLowerSecurity(target, targetGuid, true))
                return false;

            PreparedStatement stmt = DB.Login.GetPreparedStatement(LoginStatements.UPD_MUTE_TIME);
            string muteBy;
            if (handler.GetSession() != null)
                muteBy = handler.GetSession().GetPlayerName();
            else
                muteBy = "Console";

            if (target)
            {
                // Target is online, mute will be in effect right away.
                long muteTime = GameTime.GetGameTime() + notSpeakTime * Time.Minute;
                target.GetSession().m_muteTime = muteTime;
                stmt.AddValue(0, muteTime);
                string nameLink = handler.PlayerLink(targetName);

                if (WorldConfig.GetBoolValue(WorldCfg.ShowMuteInWorld))
                {
                    Global.WorldMgr.SendWorldText(CypherStrings.CommandMutemessageWorld, (handler.GetSession() != null ? handler.GetSession().GetPlayerName() : "Server"), nameLink, notSpeakTime, muteReasonStr);
                    target.SendSysMessage(CypherStrings.YourChatDisabled, notSpeakTime, muteBy, muteReasonStr);
                }
                else
                {
                    target.SendSysMessage(CypherStrings.YourChatDisabled, notSpeakTime, muteBy, muteReasonStr);
                }
            }
            else
            {
                // Target is offline, mute will be in effect starting from the next login.
                int muteTime = -(int)(notSpeakTime * Time.Minute);
                stmt.AddValue(0, muteTime);
            }

            stmt.AddValue(1, muteReasonStr);
            stmt.AddValue(2, muteBy);
            stmt.AddValue(3, accountId);
            DB.Login.Execute(stmt);
            string nameLink_ = handler.PlayerLink(targetName);

            if (WorldConfig.GetBoolValue(WorldCfg.ShowMuteInWorld) && !target)
                Global.WorldMgr.SendWorldText(CypherStrings.CommandMutemessageWorld, handler.GetSession().GetPlayerName(), nameLink_, notSpeakTime, muteReasonStr);
            else
                handler.SendSysMessage(target ? CypherStrings.YouDisableChat : CypherStrings.CommandDisableChatDelayed, nameLink_, notSpeakTime, muteReasonStr);
            return true;
        }

        // mutehistory command
        [CommandNonGroup("mutehistory", RBACPermissions.CommandMutehistory, true)]
        static bool HandleMuteInfoCommand(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            string accountName = args.NextString("");
            if (accountName.IsEmpty())
                return false;

            uint accountId = Global.AccountMgr.GetId(accountName);
            if (accountId == 0)
            {
                handler.SendSysMessage(CypherStrings.AccountNotExist, accountName);
                return false;
            }

            PreparedStatement stmt = DB.Login.GetPreparedStatement(LoginStatements.SEL_ACCOUNT_MUTE_INFO);
            stmt.AddValue(0, accountId);

            SQLResult result = DB.Login.Query(stmt);
            if (result.IsEmpty())
            {
                handler.SendSysMessage(CypherStrings.CommandMutehistoryEmpty, accountName);
                return true;
            }

            handler.SendSysMessage(CypherStrings.CommandMutehistory, accountName);
            do
            {
                // we have to manually set the string for mutedate
                long sqlTime = result.Read<uint>(0);

                // set it to string
                string buffer = Time.UnixTimeToDateTime(sqlTime).ToShortTimeString();

                handler.SendSysMessage(CypherStrings.CommandMutehistoryOutput, buffer, result.Read<uint>(1), result.Read<string>(2), result.Read<string>(3));
            } while (result.NextRow());

            return true;
        }

        [CommandNonGroup("neargrave", RBACPermissions.CommandNeargrave)]
        static bool HandleNearGraveCommand(StringArguments args, CommandHandler handler)
        {
            string px2 = args.NextString();
            Team team;
            if (string.IsNullOrEmpty(px2))
                team = 0;
            else if (px2 == "horde")
                team = Team.Horde;
            else if (px2 == "alliance")
                team = Team.Alliance;
            else
                return false;

            Player player = handler.GetSession().GetPlayer();
            uint zone_id = player.GetZoneId();

            WorldSafeLocsEntry graveyard = Global.ObjectMgr.GetClosestGraveYard(player, team, null);
            if (graveyard != null)
            {
                uint graveyardId = graveyard.Id;

                GraveYardData data = Global.ObjectMgr.FindGraveYardData(graveyardId, zone_id);
                if (data == null)
                {
                    handler.SendSysMessage(CypherStrings.CommandGraveyarderror, graveyardId);
                    return false;
                }

                team = (Team)data.team;

                string team_name = handler.GetCypherString(CypherStrings.CommandGraveyardNoteam);

                if (team == 0)
                    team_name = handler.GetCypherString(CypherStrings.CommandGraveyardAny);
                else if (team == Team.Horde)
                    team_name = handler.GetCypherString(CypherStrings.CommandGraveyardHorde);
                else if (team == Team.Alliance)
                    team_name = handler.GetCypherString(CypherStrings.CommandGraveyardAlliance);

                handler.SendSysMessage(CypherStrings.CommandGraveyardnearest, graveyardId, team_name, zone_id);
            }
            else
            {
                string team_name = "";

                if (team == Team.Horde)
                    team_name = handler.GetCypherString(CypherStrings.CommandGraveyardHorde);
                else if (team == Team.Alliance)
                    team_name = handler.GetCypherString(CypherStrings.CommandGraveyardAlliance);

                if (team == 0)
                    handler.SendSysMessage(CypherStrings.CommandZonenograveyards, zone_id);
                else
                    handler.SendSysMessage(CypherStrings.CommandZonenografaction, zone_id, team_name);
            }

            return true;
        }

        [CommandNonGroup("pinfo", RBACPermissions.CommandPinfo, true)]
        static bool HandlePInfoCommand(StringArguments args, CommandHandler handler)
        {            
            // Define ALL the player variables!
            Player target;
            ObjectGuid targetGuid;
            string targetName;
            PreparedStatement stmt;

            // To make sure we get a target, we convert our guid to an omniversal...
            ObjectGuid parseGUID = ObjectGuid.Create(HighGuid.Player, args.NextUInt64());

            // ... and make sure we get a target, somehow.
            if (Global.CharacterCacheStorage.GetCharacterNameByGuid(parseGUID, out targetName))
            {
                target = Global.ObjAccessor.FindPlayer(parseGUID);
                targetGuid = parseGUID;
            }
            // if not, then return false. Which shouldn't happen, now should it ?
            else if (!handler.ExtractPlayerTarget(args, out target, out targetGuid, out targetName))
                return false;

            /* The variables we extract for the command. They are
             * default as "does not exist" to prevent problems
             * The output is printed in the follow manner:
             *
             * Player %s %s (guid: %u)                   - I.    LANG_PINFO_PLAYER
             * ** GM Mode active, Phase: -1              - II.   LANG_PINFO_GM_ACTIVE (if GM)
             * ** Banned: (Type, Reason, Time, By)       - III.  LANG_PINFO_BANNED (if banned)
             * ** Muted: (Reason, Time, By)              - IV.   LANG_PINFO_MUTED (if muted)
             * * Account: %s (id: %u), GM Level: %u      - V.    LANG_PINFO_ACC_ACCOUNT
             * * Last Login: %u (Failed Logins: %u)      - VI.   LANG_PINFO_ACC_LASTLOGIN
             * * Uses OS: %s - Latency: %u ms            - VII.  LANG_PINFO_ACC_OS
             * * Registration Email: %s - Email: %s      - VIII. LANG_PINFO_ACC_REGMAILS
             * * Last IP: %u (Locked: %s)                - IX.   LANG_PINFO_ACC_IP
             * * Level: %u (%u/%u XP (%u XP left)        - X.    LANG_PINFO_CHR_LEVEL
             * * Race: %s %s, Class %s                   - XI.   LANG_PINFO_CHR_RACE
             * * Alive ?: %s                             - XII.  LANG_PINFO_CHR_ALIVE
             * * Phase: %s                               - XIII. LANG_PINFO_CHR_PHASE (if not GM)
             * * Money: %ug%us%uc                        - XIV.  LANG_PINFO_CHR_MONEY
             * * Map: %s, Area: %s                       - XV.   LANG_PINFO_CHR_MAP
             * * Guild: %s (Id: %u)                      - XVI.  LANG_PINFO_CHR_GUILD (if in guild)
             * ** Rank: %s                               - XVII. LANG_PINFO_CHR_GUILD_RANK (if in guild)
             * ** Note: %s                               - XVIII.LANG_PINFO_CHR_GUILD_NOTE (if in guild and has note)
             * ** O. Note: %s                            - XVIX. LANG_PINFO_CHR_GUILD_ONOTE (if in guild and has officer note)
             * * Played time: %s                         - XX.   LANG_PINFO_CHR_PLAYEDTIME
             * * Mails: %u Read/%u Total                 - XXI.  LANG_PINFO_CHR_MAILS (if has mails)
             *
             * Not all of them can be moved to the top. These should
             * place the most important ones to the head, though.
             *
             * For a cleaner overview, I segment each output in Roman numerals
             */

            // Account data print variables
            string userName = handler.GetCypherString(CypherStrings.Error);
            uint accId;
            ulong lowguid = targetGuid.GetCounter();
            string eMail = handler.GetCypherString(CypherStrings.Error);
            string regMail = handler.GetCypherString(CypherStrings.Error);
            uint security = 0;
            string lastIp = handler.GetCypherString(CypherStrings.Error);
            byte locked = 0;
            string lastLogin = handler.GetCypherString(CypherStrings.Error);
            uint failedLogins = 0;
            uint latency = 0;
            string OS = handler.GetCypherString(CypherStrings.Unknown);

            // Mute data print variables
            long muteTime = -1;
            string muteReason = handler.GetCypherString(CypherStrings.NoReason);
            string muteBy = handler.GetCypherString(CypherStrings.Unknown);

            // Ban data print variables
            long banTime = -1;
            string banType = handler.GetCypherString(CypherStrings.Unknown);
            string banReason = handler.GetCypherString(CypherStrings.NoReason);
            string bannedBy = handler.GetCypherString(CypherStrings.Unknown);

            // Character data print variables
            Race raceid;
            Class classid;
            Gender gender;
            Locale locale = handler.GetSessionDbcLocale();
            uint totalPlayerTime;
            uint level;
            string alive;
            ulong money;
            uint xp = 0;
            uint xptotal = 0;

            // Position data print
            uint mapId;
            uint areaId;
            string areaName = handler.GetCypherString(CypherStrings.Unknown);
            string zoneName = handler.GetCypherString(CypherStrings.Unknown);

            // Guild data print variables defined so that they exist, but are not necessarily used
            ulong guildId = 0;
            byte guildRankId = 0;
            string guildName = "";
            string guildRank = "";
            string note = "";
            string officeNote = "";

            // Mail data print is only defined if you have a mail

            if (target)
            {
                // check online security
                if (handler.HasLowerSecurity(target, ObjectGuid.Empty))
                    return false;

                accId = target.GetSession().GetAccountId();
                money = target.GetMoney();
                totalPlayerTime = target.GetTotalPlayedTime();
                level = target.GetLevel();
                latency = target.GetSession().GetLatency();
                raceid = target.GetRace();
                classid = target.GetClass();
                muteTime = target.GetSession().m_muteTime;
                mapId = target.GetMapId();
                areaId = target.GetAreaId();
                alive = target.IsAlive() ? handler.GetCypherString(CypherStrings.Yes) : handler.GetCypherString(CypherStrings.No);
                gender = target.GetNativeSex();
            }
            // get additional information from DB
            else
            {
                // check offline security
                if (handler.HasLowerSecurity(null, targetGuid))
                    return false;

                // Query informations from the DB
                stmt = DB.Characters.GetPreparedStatement(CharStatements.SEL_CHAR_PINFO);
                stmt.AddValue(0, lowguid);
                SQLResult result = DB.Characters.Query(stmt);

                if (result.IsEmpty())
                    return false;

                totalPlayerTime = result.Read<uint>(0);
                level = result.Read<byte>(1);
                money = result.Read<ulong>(2);
                accId = result.Read<uint>(3);
                raceid = (Race)result.Read<byte>(4);
                classid = (Class)result.Read<byte>(5);
                mapId = result.Read<ushort>(6);
                areaId = result.Read<ushort>(7);
                gender = (Gender)result.Read<byte>(8);
                uint health = result.Read<uint>(9);
                PlayerFlags playerFlags = (PlayerFlags)result.Read<uint>(10);

                if (health == 0 || playerFlags.HasAnyFlag(PlayerFlags.Ghost))
                    alive = handler.GetCypherString(CypherStrings.No);
                else
                    alive = handler.GetCypherString(CypherStrings.Yes);
            }

            // Query the prepared statement for login data
            stmt = DB.Login.GetPreparedStatement(LoginStatements.SEL_PINFO);
            stmt.AddValue(0, Global.WorldMgr.GetRealm().Id.Index);
            stmt.AddValue(1, accId);
            SQLResult result0 = DB.Login.Query(stmt);

            if (!result0.IsEmpty())
            {
                userName = result0.Read<string>(0);
                security = result0.Read<byte>(1);

                // Only fetch these fields if commander has sufficient rights)
                if (handler.HasPermission(RBACPermissions.CommandsPinfoCheckPersonalData) && // RBAC Perm. 48, Role 39
                    (!handler.GetSession() || handler.GetSession().GetSecurity() >= (AccountTypes)security))
                {
                    eMail = result0.Read <string>(2);
                    regMail = result0.Read <string>(3);
                    lastIp = result0.Read <string>(4);
                    lastLogin = result0.Read <string>(5);
                }
                else
                {
                    eMail = handler.GetCypherString(CypherStrings.Unauthorized);
                    regMail = handler.GetCypherString(CypherStrings.Unauthorized);
                    lastIp = handler.GetCypherString(CypherStrings.Unauthorized);
                    lastLogin = handler.GetCypherString(CypherStrings.Unauthorized);
                }
                muteTime = (long)result0.Read <ulong>(6);
                muteReason = result0.Read <string>(7);
                muteBy = result0.Read <string>(8);
                failedLogins = result0.Read <uint>(9);
                locked = result0.Read <byte>(10);
                OS = result0.Read <string>(11);
            }

            // Creates a chat link to the character. Returns nameLink
            string nameLink = handler.PlayerLink(targetName);

            // Returns banType, banTime, bannedBy, banreason
            PreparedStatement stmt2 = DB.Login.GetPreparedStatement(LoginStatements.SEL_PINFO_BANS);
            stmt2.AddValue(0, accId);
            SQLResult result2 = DB.Login.Query(stmt2);
            if (result2.IsEmpty())
            {
                banType = handler.GetCypherString(CypherStrings.Character);
                stmt = DB.Characters.GetPreparedStatement(CharStatements.SEL_PINFO_BANS);
                stmt.AddValue(0, lowguid);
                result2 = DB.Characters.Query(stmt);
            }

            if (!result2.IsEmpty())
            {
                banTime = (result2.Read<ulong>(1) != 0 ? 0 : result2.Read<uint>(0));
                bannedBy = result2.Read<string>(2);
                banReason = result2.Read<string>(3);
            }

            // Can be used to query data from Characters database
            stmt2 = DB.Characters.GetPreparedStatement(CharStatements.SEL_PINFO_XP);
            stmt2.AddValue(0, lowguid);
            SQLResult result4 = DB.Characters.Query(stmt2);

            if (!result4.IsEmpty())
            {
                xp = result4.Read<uint>(0); // Used for "current xp" output and "%u XP Left" calculation
                ulong gguid = result4.Read<ulong>(1); // We check if have a guild for the person, so we might not require to query it at all
                xptotal = Global.ObjectMgr.GetXPForLevel(level);

                if (gguid != 0)
                {
                    // Guild Data - an own query, because it may not happen.
                    PreparedStatement stmt3 = DB.Characters.GetPreparedStatement(CharStatements.SEL_GUILD_MEMBER_EXTENDED);
                    stmt3.AddValue(0, lowguid);
                    SQLResult result5 = DB.Characters.Query(stmt3);
                    if (!result5.IsEmpty())
                    {
                        guildId = result5.Read<ulong>(0);
                        guildName = result5.Read<string>(1);
                        guildRank = result5.Read<string>(2);
                        guildRankId = result5.Read<byte>(3);
                        note = result5.Read<string>(4);
                        officeNote = result5.Read<string>(5);
                    }
                }
            }

            // Initiate output
            // Output I. LANG_PINFO_PLAYER
            handler.SendSysMessage(CypherStrings.PinfoPlayer, target ? "" : handler.GetCypherString(CypherStrings.Offline), nameLink, targetGuid.ToString());

            // Output II. LANG_PINFO_GM_ACTIVE if character is gamemaster
            if (target && target.IsGameMaster())
                handler.SendSysMessage(CypherStrings.PinfoGmActive);

            // Output III. LANG_PINFO_BANNED if ban exists and is applied
            if (banTime >= 0)
                handler.SendSysMessage(CypherStrings.PinfoBanned, banType, banReason, banTime > 0 ? Time.secsToTimeString((ulong)(banTime - GameTime.GetGameTime()), true) : handler.GetCypherString(CypherStrings.Permanently), bannedBy);

            // Output IV. LANG_PINFO_MUTED if mute is applied
            if (muteTime > 0)
                handler.SendSysMessage(CypherStrings.PinfoMuted, muteReason, Time.secsToTimeString((ulong)(muteTime - GameTime.GetGameTime()), true), muteBy);

            // Output V. LANG_PINFO_ACC_ACCOUNT
            handler.SendSysMessage(CypherStrings.PinfoAccAccount, userName, accId, security);

            // Output VI. LANG_PINFO_ACC_LASTLOGIN
            handler.SendSysMessage(CypherStrings.PinfoAccLastlogin, lastLogin, failedLogins);

            // Output VII. LANG_PINFO_ACC_OS
            handler.SendSysMessage(CypherStrings.PinfoAccOs, OS, latency);

            // Output VIII. LANG_PINFO_ACC_REGMAILS
            handler.SendSysMessage(CypherStrings.PinfoAccRegmails, regMail, eMail);

            // Output IX. LANG_PINFO_ACC_IP
            handler.SendSysMessage(CypherStrings.PinfoAccIp, lastIp, locked != 0 ? handler.GetCypherString(CypherStrings.Yes) : handler.GetCypherString(CypherStrings.No));

            // Output X. LANG_PINFO_CHR_LEVEL
            if (level != WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                handler.SendSysMessage(CypherStrings.PinfoChrLevelLow, level, xp, xptotal, (xptotal - xp));
            else
                handler.SendSysMessage(CypherStrings.PinfoChrLevelHigh, level);

            // Output XI. LANG_PINFO_CHR_RACE
            handler.SendSysMessage(CypherStrings.PinfoChrRace, (gender == 0 ? handler.GetCypherString(CypherStrings.CharacterGenderMale) : handler.GetCypherString(CypherStrings.CharacterGenderFemale)),
                Global.DB2Mgr.GetChrRaceName(raceid, locale), Global.DB2Mgr.GetClassName(classid, locale));

            // Output XII. LANG_PINFO_CHR_ALIVE
            handler.SendSysMessage(CypherStrings.PinfoChrAlive, alive);

            // Output XIII. phases
            if (target)
                PhasingHandler.PrintToChat(handler, target.GetPhaseShift());

            // Output XIV. LANG_PINFO_CHR_MONEY
            ulong gold = money / MoneyConstants.Gold;
            ulong silv = (money % MoneyConstants.Gold) / MoneyConstants.Silver;
            ulong copp = (money % MoneyConstants.Gold) % MoneyConstants.Silver;
            handler.SendSysMessage(CypherStrings.PinfoChrMoney, gold, silv, copp);

            // Position data
            MapRecord map = CliDB.MapStorage.LookupByKey(mapId);
            AreaTableRecord area = CliDB.AreaTableStorage.LookupByKey(areaId);
            if (area != null)
            {
                areaName = area.AreaName[locale];

                AreaTableRecord zone = CliDB.AreaTableStorage.LookupByKey(area.ParentAreaID);
                if (zone != null)
                    zoneName = zone.AreaName[locale];
            }

            if (target)
                handler.SendSysMessage(CypherStrings.PinfoChrMap, map.MapName[locale],
                    (!zoneName.IsEmpty() ? zoneName : handler.GetCypherString(CypherStrings.Unknown)),
                    (!areaName.IsEmpty() ? areaName : handler.GetCypherString(CypherStrings.Unknown)));

            // Output XVII. - XVIX. if they are not empty
            if (!guildName.IsEmpty())
            {
                handler.SendSysMessage(CypherStrings.PinfoChrGuild, guildName, guildId);
                handler.SendSysMessage(CypherStrings.PinfoChrGuildRank, guildRank, guildRankId);
                if (!note.IsEmpty())
                    handler.SendSysMessage(CypherStrings.PinfoChrGuildNote, note);
                if (!officeNote.IsEmpty())
                    handler.SendSysMessage(CypherStrings.PinfoChrGuildOnote, officeNote);
            }

            // Output XX. LANG_PINFO_CHR_PLAYEDTIME
            handler.SendSysMessage(CypherStrings.PinfoChrPlayedtime, (Time.secsToTimeString(totalPlayerTime, true, true)));

            // Mail Data - an own query, because it may or may not be useful.
            // SQL: "SELECT SUM(CASE WHEN (checked & 1) THEN 1 ELSE 0 END) AS 'readmail', COUNT(*) AS 'totalmail' FROM mail WHERE `receiver` = ?"
            PreparedStatement stmt4 = DB.Characters.GetPreparedStatement(CharStatements.SEL_PINFO_MAILS);
            stmt4.AddValue(0, lowguid);
            SQLResult result6 = DB.Characters.Query(stmt4);
            if (!result6.IsEmpty())
            {
                uint readmail = (uint)result6.Read<double>(0);
                uint totalmail = (uint)result6.Read<ulong>(1);

                // Output XXI. LANG_INFO_CHR_MAILS if at least one mail is given
                if (totalmail >= 1)
                    handler.SendSysMessage(CypherStrings.PinfoChrMails, readmail, totalmail);
            }

            return true;
        }

        [CommandNonGroup("playall", RBACPermissions.CommandPlayall)]
        static bool HandlePlayAllCommand(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            uint soundId = args.NextUInt32();

            if (!CliDB.SoundKitStorage.ContainsKey(soundId))
            {
                handler.SendSysMessage(CypherStrings.SoundNotExist, soundId);
                return false;
            }

            uint broadcastTextId = args.NextUInt32();

            Global.WorldMgr.SendGlobalMessage(new PlaySound(handler.GetSession().GetPlayer().GetGUID(), soundId, broadcastTextId));

            handler.SendSysMessage(CypherStrings.CommandPlayedToAll, soundId);
            return true;
        }

        [CommandNonGroup("possess", RBACPermissions.CommandPossess)]
        static bool HandlePossessCommand(StringArguments args, CommandHandler handler)
        {
            Unit unit = handler.GetSelectedUnit();
            if (!unit)
                return false;

            handler.GetSession().GetPlayer().CastSpell(unit, 530, true);
            return true;
        }

        [CommandNonGroup("pvpstats", RBACPermissions.CommandPvpstats, true)]
        static bool HandlePvPstatsCommand(StringArguments args, CommandHandler handler)
        {
            if (WorldConfig.GetBoolValue(WorldCfg.BattlegroundStoreStatisticsEnable))
            {
                PreparedStatement stmt = DB.Characters.GetPreparedStatement(CharStatements.SEL_PVPSTATS_FACTIONS_OVERALL);
                SQLResult result = DB.Characters.Query(stmt);

                if (!result.IsEmpty())
                {
                    uint horde_victories = result.Read<uint>(1);

                    if (!(result.NextRow()))
                        return false;

                    uint alliance_victories = result.Read<uint>(1);

                    handler.SendSysMessage(CypherStrings.Pvpstats, alliance_victories, horde_victories);
                }
                else
                    return false;
            }
            else
                handler.SendSysMessage(CypherStrings.PvpstatsDisabled);

            return true;
        }

        // Teleport player to last position
        [CommandNonGroup("recall", RBACPermissions.CommandRecall)]
        static bool HandleRecallCommand(StringArguments args, CommandHandler handler)
        {
            Player target;
            if (!handler.ExtractPlayerTarget(args, out target))
                return false;

            // check online security
            if (handler.HasLowerSecurity(target, ObjectGuid.Empty))
                return false;

            if (target.IsBeingTeleported())
            {
                handler.SendSysMessage(CypherStrings.IsTeleported, handler.GetNameLink(target));

                return false;
            }

            // stop flight if need
            if (target.IsInFlight())
            {
                target.GetMotionMaster().MovementExpired();
                target.CleanupAfterTaxiFlight();
            }

            target.Recall();
            return true;
        }

        [CommandNonGroup("repairitems", RBACPermissions.CommandRepairitems, true)]
        static bool HandleRepairitemsCommand(StringArguments args, CommandHandler handler)
        {
            Player target;
            if (!handler.ExtractPlayerTarget(args, out target))
                return false;

            // check online security
            if (handler.HasLowerSecurity(target, ObjectGuid.Empty))
                return false;

            // Repair items
            target.DurabilityRepairAll(false, 0, false);

            handler.SendSysMessage(CypherStrings.YouRepairItems, handler.GetNameLink(target));
            if (handler.NeedReportToTarget(target))
                target.SendSysMessage(CypherStrings.YourItemsRepaired, handler.GetNameLink());

            return true;
        }

        [CommandNonGroup("respawn", RBACPermissions.CommandRespawn)]
        static bool HandleRespawnCommand(StringArguments args, CommandHandler handler)
        {
            Player player = handler.GetSession().GetPlayer();

            // accept only explicitly selected target (not implicitly self targeting case)
            Creature target = !player.GetTarget().IsEmpty() ? handler.GetSelectedCreature() : null;
            if (target)
            {
                if (target.IsPet())
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    return false;
                }

                if (target.IsDead())
                    target.Respawn();
                return true;
            }

            // First handle any creatures that still have a corpse around
            var worker = new WorldObjectWorker(player, new RespawnDo());
            Cell.VisitGridObjects(player, worker, player.GetGridActivationRange());

            // Now handle any that had despawned, but had respawn time logged.
            List<RespawnInfo> data = new();
            player.GetMap().GetRespawnInfo(data, SpawnObjectTypeMask.All, 0);
            if (!data.Empty())
            {
                uint gridId = GridDefines.ComputeGridCoord(player.GetPositionX(), player.GetPositionY()).GetId();
                foreach (RespawnInfo info in data)
                    if (info.gridId == gridId)
                        player.GetMap().RemoveRespawnTime(info, true);
            }

            return true;
        }

        [CommandNonGroup("revive", RBACPermissions.CommandRevive, true)]
        static bool HandleReviveCommand(StringArguments args, CommandHandler handler)
        {
            Player target;
            ObjectGuid targetGuid;
            if (!handler.ExtractPlayerTarget(args, out target, out targetGuid))
                return false;

            if (target != null)
            {
                target.ResurrectPlayer(0.5f);
                target.SpawnCorpseBones();
                target.SaveToDB();
            }
            else
                Player.OfflineResurrect(targetGuid, null);

            return true;
        }

        // Save all players in the world
        [CommandNonGroup("saveall", RBACPermissions.CommandSaveall, true)]
        static bool HandleSaveAllCommand(StringArguments args, CommandHandler handler)
        {
            Global.ObjAccessor.SaveAllPlayers();
            handler.SendSysMessage(CypherStrings.PlayersSaved);
            return true;
        }

        [CommandNonGroup("save", RBACPermissions.CommandSave)]
        static bool HandleSaveCommand(StringArguments args, CommandHandler handler)
        {
            Player player = handler.GetSession().GetPlayer();

            // save GM account without delay and output message
            if (handler.GetSession().HasPermission(RBACPermissions.CommandsSaveWithoutDelay))
            {
                Player target = handler.GetSelectedPlayer();
                if (target)
                    target.SaveToDB();
                else
                    player.SaveToDB();
                handler.SendSysMessage(CypherStrings.PlayerSaved);
                return true;
            }

            // save if the player has last been saved over 20 seconds ago
            uint saveInterval = WorldConfig.GetUIntValue(WorldCfg.IntervalSave);
            if (saveInterval == 0 || (saveInterval > 20 * Time.InMilliseconds && player.GetSaveTimer() <= saveInterval - 20 * Time.InMilliseconds))
                player.SaveToDB();

            return true;
        }

        [CommandNonGroup("showarea", RBACPermissions.CommandShowarea)]
        static bool HandleShowAreaCommand(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            Player playerTarget = handler.GetSelectedPlayer();
            if (!playerTarget)
            {
                handler.SendSysMessage(CypherStrings.NoCharSelected);
                return false;
            }

            AreaTableRecord area = CliDB.AreaTableStorage.LookupByKey(args.NextUInt32());
            if (area == null)
            {
                handler.SendSysMessage(CypherStrings.BadValue);
                return false;
            }

            if (area.AreaBit < 0)
            {
                handler.SendSysMessage(CypherStrings.BadValue);
                return false;
            }

            uint offset = (uint)area.AreaBit / 64;
            if (offset >= PlayerConst.ExploredZonesSize)
            {
                handler.SendSysMessage(CypherStrings.BadValue);
                return false;
            }

            ulong val = 1ul << (area.AreaBit % 64);
            playerTarget.AddExploredZones(offset, val);

            handler.SendSysMessage(CypherStrings.ExploreArea);
            return true;
        }

        // Summon Player
        [CommandNonGroup("summon", RBACPermissions.CommandSummon)]
        static bool HandleSummonCommand(StringArguments args, CommandHandler handler)
        {
            Player target;
            ObjectGuid targetGuid;
            string targetName;
            if (!handler.ExtractPlayerTarget(args, out target, out targetGuid, out targetName))
                return false;

            Player _player = handler.GetSession().GetPlayer();
            if (target == _player || targetGuid == _player.GetGUID())
            {
                handler.SendSysMessage(CypherStrings.CantTeleportSelf);

                return false;
            }

            if (target)
            {
                string nameLink = handler.PlayerLink(targetName);
                // check online security
                if (handler.HasLowerSecurity(target, ObjectGuid.Empty))
                    return false;

                if (target.IsBeingTeleported())
                {
                    handler.SendSysMessage(CypherStrings.IsTeleported, nameLink);

                    return false;
                }

                Map map = _player.GetMap();
                if (map.IsBattlegroundOrArena())
                {
                    // only allow if gm mode is on
                    if (!_player.IsGameMaster())
                    {
                        handler.SendSysMessage(CypherStrings.CannotGoToBgGm, nameLink);
                        return false;
                    }
                    // if both players are in different bgs
                    else if (target.GetBattlegroundId() != 0 && _player.GetBattlegroundId() != target.GetBattlegroundId())
                        target.LeaveBattleground(false); // Note: should be changed so target gets no Deserter debuff

                    // all's well, set bg id
                    // when porting out from the bg, it will be reset to 0
                    target.SetBattlegroundId(_player.GetBattlegroundId(), _player.GetBattlegroundTypeId());
                    // remember current position as entry point for return at bg end teleportation
                    if (!target.GetMap().IsBattlegroundOrArena())
                        target.SetBattlegroundEntryPoint();
                }
                else if (map.Instanceable())
                {
                    Map targetMap = target.GetMap();

                    Player targetGroupLeader = null;
                    Group targetGroup = target.GetGroup();
                    if (targetGroup != null)
                        targetGroupLeader = Global.ObjAccessor.GetPlayer(map, targetGroup.GetLeaderGUID());

                    // check if far teleport is allowed
                    if (targetGroupLeader == null || (targetGroupLeader.GetMapId() != map.GetId()) || (targetGroupLeader.GetInstanceId() != map.GetInstanceId()))
                    {
                        if ((targetMap.GetId() != map.GetId()) || (targetMap.GetInstanceId() != map.GetInstanceId()))
                        {
                            handler.SendSysMessage(CypherStrings.CannotSummonToInst);
                            return false;
                        }
                    }

                    // check if we're already in a different instance of the same map
                    if ((targetMap.GetId() == map.GetId()) && (targetMap.GetInstanceId() != map.GetInstanceId()))
                    {
                        handler.SendSysMessage(CypherStrings.CannotSummonInstInst, nameLink);
                        return false;
                    }
                }

                handler.SendSysMessage(CypherStrings.Summoning, nameLink, "");
                if (handler.NeedReportToTarget(target))
                    target.SendSysMessage(CypherStrings.SummonedBy, handler.PlayerLink(_player.GetName()));

                // stop flight if need
                if (target.IsInFlight())
                {
                    target.GetMotionMaster().MovementExpired();
                    target.CleanupAfterTaxiFlight();
                }
                // save only in non-flight case
                else
                    target.SaveRecallPosition();

                // before GM
                float x, y, z;
                _player.GetClosePoint(out x, out y, out z, target.GetCombatReach());
                target.TeleportTo(_player.GetMapId(), x, y, z, target.GetOrientation());
                PhasingHandler.InheritPhaseShift(target, _player);
                target.UpdateObjectVisibility();
            }
            else
            {
                // check offline security
                if (handler.HasLowerSecurity(null, targetGuid))
                    return false;

                string nameLink = handler.PlayerLink(targetName);

                handler.SendSysMessage(CypherStrings.Summoning, nameLink, handler.GetCypherString(CypherStrings.Offline));

                // in point where GM stay
                Player.SavePositionInDB(new WorldLocation(_player.GetMapId(), _player.GetPositionX(), _player.GetPositionY(), _player.GetPositionZ(), _player.GetOrientation()), _player.GetZoneId(), targetGuid);
            }

            return true;
        }

        [CommandNonGroup("unbindsight", RBACPermissions.CommandUnbindsight)]
        static bool HandleUnbindSightCommand(StringArguments args, CommandHandler handler)
        {
            Player player = handler.GetSession().GetPlayer();

            if (player.IsPossessing())
                return false;

            player.StopCastingBindSight();
            return true;
        }

        [CommandNonGroup("unfreeze", RBACPermissions.CommandUnfreeze)]
        static bool HandleUnFreezeCommand(StringArguments args, CommandHandler handler)
        {
            string name = "";
            Player player;
            string targetName = args.NextString(); // Get entered name

            if (!string.IsNullOrEmpty(targetName))
            {
                name = targetName;
                ObjectManager.NormalizePlayerName(ref name);
                player = Global.ObjAccessor.FindPlayerByName(name);
            }
            else // If no name was entered - use target
            {
                player = handler.GetSelectedPlayer();
                if (player)
                    name = player.GetName();
            }

            if (player)
            {
                handler.SendSysMessage(CypherStrings.CommandUnfreeze, name);

                // Remove Freeze spell (allowing movement and spells)
                // Player Flags + Neutral faction removal is now
                // handled on the Freeze Spell AuraScript (OnRemove)
                player.RemoveAurasDueToSpell(9454);
            }
            else
            {
                if (!string.IsNullOrEmpty(targetName))
                {
                    // Check for offline players
                    ObjectGuid guid = Global.CharacterCacheStorage.GetCharacterGuidByName(name);
                    if (guid.IsEmpty())
                    {
                        handler.SendSysMessage(CypherStrings.CommandFreezeWrong);
                        return true;
                    }

                    // If player found: delete his freeze aura    
                    PreparedStatement stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_CHAR_AURA_FROZEN);
                    stmt.AddValue(0, guid.GetCounter());
                    DB.Characters.Execute(stmt);

                    handler.SendSysMessage(CypherStrings.CommandUnfreeze, name);
                    return true;
                }
                else
                {
                    handler.SendSysMessage(CypherStrings.CommandFreezeWrong);
                    return true;
                }
            }

            return true;
        }

        // unmute player
        [CommandNonGroup("unmute", RBACPermissions.CommandUnmute, true)]
        static bool HandleUnmuteCommand(StringArguments args, CommandHandler handler)
        {
            Player target;
            ObjectGuid targetGuid;
            string targetName;
            if (!handler.ExtractPlayerTarget(args, out target, out targetGuid, out targetName))
                return false;

            uint accountId = target ? target.GetSession().GetAccountId() : Global.CharacterCacheStorage.GetCharacterAccountIdByGuid(targetGuid);

            // find only player from same account if any
            if (!target)
            {
                WorldSession session = Global.WorldMgr.FindSession(accountId);
                if (session != null)
                    target = session.GetPlayer();
            }

            // must have strong lesser security level
            if (handler.HasLowerSecurity(target, targetGuid, true))
                return false;

            if (target)
            {
                if (target.GetSession().CanSpeak())
                {
                    handler.SendSysMessage(CypherStrings.ChatAlreadyEnabled);
                    return false;
                }

                target.GetSession().m_muteTime = 0;
            }

            PreparedStatement stmt = DB.Login.GetPreparedStatement(LoginStatements.UPD_MUTE_TIME);
            stmt.AddValue(0, 0);
            stmt.AddValue(1, "");
            stmt.AddValue(2, "");
            stmt.AddValue(3, accountId);
            DB.Login.Execute(stmt);

            if (target)
                target.SendSysMessage(CypherStrings.YourChatEnabled);

            string nameLink = handler.PlayerLink(targetName);

            handler.SendSysMessage(CypherStrings.YouEnableChat, nameLink);

            return true;
        }

        [CommandNonGroup("unpossess", RBACPermissions.CommandUnpossess)]
        static bool HandleUnPossessCommand(StringArguments args, CommandHandler handler)
        {
            Unit unit = handler.GetSelectedUnit();
            if (!unit)
                unit = handler.GetSession().GetPlayer();

            unit.RemoveCharmAuras();

            return true;
        }

        [CommandNonGroup("unstuck", RBACPermissions.CommandUnstuck, true)]
        static bool HandleUnstuckCommand(StringArguments args, CommandHandler handler)
        {
            uint SPELL_UNSTUCK_ID = 7355;
            uint SPELL_UNSTUCK_VISUAL = 2683;

            // No args required for players
            if (handler.GetSession() != null && handler.GetSession().HasPermission(RBACPermissions.CommandsUseUnstuckWithArgs))
            {
                // 7355: "Stuck"
                var player1 = handler.GetSession().GetPlayer();
                if (player1)
                    player1.CastSpell(player1, SPELL_UNSTUCK_ID, false);
                return true;
            }

            if (args.Empty())
                return false;

            string location_str = "inn";
            string loc = args.NextString();
            if (string.IsNullOrEmpty(loc))
                location_str = loc;

            Player player;
            ObjectGuid targetGUID;
            if (!handler.ExtractPlayerTarget(args, out player, out targetGUID))
                return false;

            if (!player)
            {
                PreparedStatement stmt = DB.Characters.GetPreparedStatement(CharStatements.SEL_CHAR_HOMEBIND);
                stmt.AddValue(0, targetGUID.GetCounter());
                SQLResult result = DB.Characters.Query(stmt);
                if (!result.IsEmpty())
                {
                    Player.SavePositionInDB(new WorldLocation(result.Read<ushort>(0), result.Read<float>(2), result.Read<float>(3), result.Read<float>(4), 0.0f), result.Read<ushort>(1), targetGUID);
                    return true;
                }

                return false;
            }

            if (player.IsInFlight() || player.IsInCombat())
            {
                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(SPELL_UNSTUCK_ID, Difficulty.None);
                if (spellInfo == null)
                    return false;

                Player caster = handler.GetSession().GetPlayer();
                if (caster)
                {
                    ObjectGuid castId = ObjectGuid.Create(HighGuid.Cast, SpellCastSource.Normal, player.GetMapId(), SPELL_UNSTUCK_ID, player.GetMap().GenerateLowGuid(HighGuid.Cast));
                    Spell.SendCastResult(caster, spellInfo, new Networking.Packets.SpellCastVisual(SPELL_UNSTUCK_VISUAL, 0), castId, SpellCastResult.CantDoThatRightNow);
                }

                return false;
            }

            if (location_str == "inn")
            {
                var home = player.GetHomebind();
                player.TeleportTo(home.GetMapId(), home.GetPositionX(), home.GetPositionY(), home.GetPositionZ(), player.GetOrientation());
                return true;
            }

            if (location_str == "graveyard")
            {
                player.RepopAtGraveyard();
                return true;
            }

            if (location_str == "startzone")
            {
                player.TeleportTo(player.GetStartPosition());
                return true;
            }

            //Not a supported argument
            return false;
        }

        [CommandNonGroup("wchange", RBACPermissions.CommandWchange)]
        static bool HandleChangeWeather(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            // Weather is OFF
            if (!WorldConfig.GetBoolValue(WorldCfg.Weather))
            {
                handler.SendSysMessage(CypherStrings.WeatherDisabled);
                return false;
            }

            // *Change the weather of a cell            
            //0 to 3, 0: fine, 1: rain, 2: snow, 3: sand
            if (!uint.TryParse(args.NextString(), out uint type))
                return false; 
            
            //0 to 1, sending -1 is instand good weather
            if (!float.TryParse(args.NextString(), out float grade))
                return false;                       

            Player player = handler.GetSession().GetPlayer();
            uint zoneid = player.GetZoneId();

            Weather weather = player.GetMap().GetOrGenerateZoneDefaultWeather(zoneid);
            if (weather == null)
            {
                handler.SendSysMessage(CypherStrings.NoWeather);
                return false;
            }

            weather.SetWeather((WeatherType)type, grade);
            return true;
        }
    }

    [CommandGroup("achievement", RBACPermissions.CommandAchievement)]
    class AchievementCommand
    {
        [Command("add", RBACPermissions.CommandAchievementAdd)]
        static bool Add(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            string idStr = handler.ExtractKeyFromLink(args, "Hachievement");
            if (string.IsNullOrEmpty(idStr))
                return false;

            if (!uint.TryParse(idStr, out uint achievementId) || achievementId == 0)
                return false;

            Player target = handler.GetSelectedPlayer();
            if (!target)
            {
                handler.SendSysMessage(CypherStrings.NoCharSelected);
                return false;
            }
            AchievementRecord achievementEntry = CliDB.AchievementStorage.LookupByKey(achievementId);
            if (achievementEntry != null)
                target.CompletedAchievement(achievementEntry);

            return true;
        }
    }
}