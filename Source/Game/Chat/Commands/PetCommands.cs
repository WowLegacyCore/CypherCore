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
using Framework.IO;
using Game.Entities;
using Game.Spells;

namespace Game.Chat
{
    [CommandGroup("pet", RBACPermissions.CommandPet)]
    class PetCommands
    {
        [Command("create", RBACPermissions.CommandPetCreate)]
        static bool HandlePetCreateCommand(StringArguments args, CommandHandler handler)
        {
            Player player = handler.GetSession().GetPlayer();
            Creature creatureTarget = handler.GetSelectedCreature();

            if (!creatureTarget || creatureTarget.IsPet() || creatureTarget.IsTypeId(TypeId.Player))
            {
                handler.SendSysMessage(CypherStrings.SelectCreature);
                return false;
            }

            CreatureTemplate creatureTemplate = creatureTarget.GetCreatureTemplate();
            // Creatures with family CreatureFamily.None crashes the server
            if (creatureTemplate.Family == CreatureFamily.None)
            {
                handler.SendSysMessage("This creature cannot be tamed. (Family id: 0).");
                return false;
            }

            if (!player.GetPetGUID().IsEmpty())
            {
                handler.SendSysMessage("You already have a pet");
                return false;
            }

            // Everything looks OK, create new pet
            Pet pet = new(player, PetType.Hunter);
            if (!pet.CreateBaseAtCreature(creatureTarget))
            {
                handler.SendSysMessage("Error 1");
                return false;
            }

            creatureTarget.SetDeathState(DeathState.JustDied);
            creatureTarget.RemoveCorpse();
            creatureTarget.SetHealth(0); // just for nice GM-mode view

            pet.SetCreatorGUID(player.GetGUID());
            pet.SetFaction(player.GetFaction());

            if (!pet.InitStatsForLevel(creatureTarget.GetLevel()))
            {
                Log.outError(LogFilter.ChatSystem, "InitStatsForLevel() in EffectTameCreature failed! Pet deleted.");
                handler.SendSysMessage("Error 2");
                return false;
            }

            // prepare visual effect for levelup
            pet.SetLevel(creatureTarget.GetLevel() - 1);

            pet.GetCharmInfo().SetPetNumber(Global.ObjectMgr.GeneratePetNumber(), true);
            // this enables pet details window (Shift+P)
            pet.InitPetCreateSpells();
            pet.SetFullHealth();

            pet.GetMap().AddToMap(pet.ToCreature());

            // visual effect for levelup
            pet.SetLevel(creatureTarget.GetLevel());

            player.SetMinion(pet, true);
            pet.SavePetToDB(PetSaveMode.AsCurrent);
            player.PetSpellInitialize();

            return true;
        }

        [Command("learn", RBACPermissions.CommandPetLearn)]
        static bool HandlePetLearnCommand(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            Pet pet = GetSelectedPlayerPetOrOwn(handler);
            if (!pet)
            {
                handler.SendSysMessage(CypherStrings.SelectPlayerOrPet);
                return false;
            }

            uint spellId = handler.ExtractSpellIdFromLink(args);
            if (spellId == 0 || !Global.SpellMgr.HasSpellInfo(spellId, Difficulty.None))
                return false;

            // Check if pet already has it
            if (pet.HasSpell(spellId))
            {
                handler.SendSysMessage("Pet already has spell: {0}", spellId);
                return false;
            }

            // Check if spell is valid
            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
            if (spellInfo == null || !Global.SpellMgr.IsSpellValid(spellInfo))
            {
                handler.SendSysMessage(CypherStrings.CommandSpellBroken, spellId);
                return false;
            }

            pet.LearnSpell(spellId);

            handler.SendSysMessage("Pet has learned spell {0}", spellId);
            return true;
        }

        [Command("unlearn", RBACPermissions.CommandPetUnlearn)]
        static bool HandlePetUnlearnCommand(StringArguments args, CommandHandler handler)
        {
            if (args.Empty())
                return false;

            Pet pet = GetSelectedPlayerPetOrOwn(handler);
            if (!pet)
            {
                handler.SendSysMessage(CypherStrings.SelectPlayerOrPet);
                return false;
            }

            uint spellId = handler.ExtractSpellIdFromLink(args);

            if (pet.HasSpell(spellId))
                pet.RemoveSpell(spellId, false);
            else
                handler.SendSysMessage("Pet doesn't have that spell");

            return true;
        }

        [Command("level", RBACPermissions.CommandPetLevel)]
        static bool HandlePetLevelCommand(StringArguments args, CommandHandler handler)
        {
            Pet pet = GetSelectedPlayerPetOrOwn(handler);
            Player owner = pet ? pet.GetOwner() : null;
            if (!pet || !owner)
            {
                handler.SendSysMessage(CypherStrings.SelectPlayerOrPet);
                return false;
            }

            int level = args.NextInt32();
            if (level == 0)
                level = (int)(owner.GetLevel() - pet.GetLevel());
            if (level == 0 || level < -SharedConst.StrongMaxLevel || level > SharedConst.StrongMaxLevel)
            {
                handler.SendSysMessage(CypherStrings.BadValue);
                return false;
            }

            int newLevel = (int)pet.GetLevel() + level;
            if (newLevel < 1)
                newLevel = 1;
            else if (newLevel > owner.GetLevel())
                newLevel = (int)owner.GetLevel();

            pet.GivePetLevel(newLevel);
            return true;
        }

        static Pet GetSelectedPlayerPetOrOwn(CommandHandler handler)
        {
            Unit target = handler.GetSelectedUnit();
            if (target)
            {
                if (target.IsTypeId(TypeId.Player))
                    return target.ToPlayer().GetPet();
                if (target.IsPet())
                    return target.ToPet();
                return null;
            }

            Player player = handler.GetSession().GetPlayer();
            return player ? player.GetPet() : null;
        }
    }
}
