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
using Game.Cache;
using Game.Entities;
using Game.Guilds;
using Game.Networking;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.LfGuildAddRecruit)]
        void HandleGuildFinderAddRecruit(LFGuildAddRecruit lfGuildAddRecruit)
        {
            if (Global.GuildFinderMgr.GetAllMembershipRequestsForPlayer(GetPlayer().GetGUID()).Count >= 10)
                return;

            if (!lfGuildAddRecruit.GuildGUID.IsGuild())
                return;
            if (!lfGuildAddRecruit.ClassRoles.HasAnyFlag((uint)GuildFinderOptionsRoles.All) || lfGuildAddRecruit.ClassRoles > (uint)GuildFinderOptionsRoles.All)
                return;
            if (!lfGuildAddRecruit.Availability.HasAnyFlag((uint)GuildFinderOptionsAvailability.Always) || lfGuildAddRecruit.Availability > (uint)GuildFinderOptionsAvailability.Always)
                return;
            if (!lfGuildAddRecruit.PlayStyle.HasAnyFlag((uint)GuildFinderOptionsInterest.All) || lfGuildAddRecruit.PlayStyle > (uint)GuildFinderOptionsInterest.All)
                return;

            MembershipRequest request = new(GetPlayer().GetGUID(), lfGuildAddRecruit.GuildGUID, lfGuildAddRecruit.Availability,
                lfGuildAddRecruit.ClassRoles, lfGuildAddRecruit.PlayStyle, lfGuildAddRecruit.Comment, GameTime.GetGameTime());
            Global.GuildFinderMgr.AddMembershipRequest(lfGuildAddRecruit.GuildGUID, request);
        }

        [WorldPacketHandler(ClientOpcodes.LfGuildBrowse)]
        void HandleGuildFinderBrowse(LFGuildBrowse lfGuildBrowse)
        {
            if (!lfGuildBrowse.ClassRoles.HasAnyFlag((uint)GuildFinderOptionsRoles.All) || lfGuildBrowse.ClassRoles > (uint)GuildFinderOptionsRoles.All)
                return;
            if (!lfGuildBrowse.Availability.HasAnyFlag((uint)GuildFinderOptionsAvailability.Always) || lfGuildBrowse.Availability > (uint)GuildFinderOptionsAvailability.Always)
                return;
            if (!lfGuildBrowse.PlayStyle.HasAnyFlag((uint)GuildFinderOptionsInterest.All) || lfGuildBrowse.PlayStyle > (uint)GuildFinderOptionsInterest.All)
                return;
            if (lfGuildBrowse.CharacterLevel > WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel) || lfGuildBrowse.CharacterLevel < 1)
                return;

            Player player = GetPlayer();

            LFGuildPlayer settings = new(player.GetGUID(), lfGuildBrowse.ClassRoles, lfGuildBrowse.Availability, lfGuildBrowse.PlayStyle, (uint)GuildFinderOptionsLevel.Any);
            var guildList = Global.GuildFinderMgr.GetGuildsMatchingSetting(settings, (uint)player.GetTeam());

            LFGuildBrowseResult lfGuildBrowseResult = new();
            for (var i = 0; i < guildList.Count; ++i)
            {
                LFGuildSettings guildSettings = guildList[i];
                LFGuildBrowseData guildData = new();
                Guild guild = Global.GuildMgr.GetGuildByGuid(guildSettings.GetGUID());

                guildData.GuildName = guild.GetName();
                guildData.GuildGUID = guild.GetGUID();
                guildData.GuildVirtualRealm = Global.WorldMgr.GetVirtualRealmAddress();
                guildData.GuildMembers = guild.GetMembersCount();
                guildData.GuildAchievementPoints = guild.GetAchievementMgr().GetAchievementPoints();
                guildData.PlayStyle = guildSettings.GetInterests();
                guildData.Availability = guildSettings.GetAvailability();
                guildData.ClassRoles = guildSettings.GetClassRoles();
                guildData.LevelRange = guildSettings.GetLevel();
                guildData.EmblemStyle = guild.GetEmblemInfo().GetStyle();
                guildData.EmblemColor = guild.GetEmblemInfo().GetColor();
                guildData.BorderStyle = guild.GetEmblemInfo().GetBorderStyle();
                guildData.BorderColor = guild.GetEmblemInfo().GetBorderColor();
                guildData.Background = guild.GetEmblemInfo().GetBackgroundColor();
                guildData.Comment = guildSettings.GetComment();
                guildData.Cached = 0;
                guildData.MembershipRequested = (sbyte)(Global.GuildFinderMgr.HasRequest(player.GetGUID(), guild.GetGUID()) ? 1 : 0);

                lfGuildBrowseResult.Post.Add(guildData);
            }

            player.SendPacket(lfGuildBrowseResult);
        }

        [WorldPacketHandler(ClientOpcodes.LfGuildDeclineRecruit)]
        void HandleGuildFinderDeclineRecruit(LFGuildDeclineRecruit lfGuildDeclineRecruit)
        {
            if (!GetPlayer().GetGuild())
                return;

            if (!lfGuildDeclineRecruit.RecruitGUID.IsPlayer())
                return;

            Global.GuildFinderMgr.RemoveMembershipRequest(lfGuildDeclineRecruit.RecruitGUID, GetPlayer().GetGuild().GetGUID());
        }

        [WorldPacketHandler(ClientOpcodes.LfGuildGetApplications)]
        void HandleGuildFinderGetApplications(LFGuildGetApplications lfGuildGetApplications)
        {
            List<MembershipRequest> applicatedGuilds = Global.GuildFinderMgr.GetAllMembershipRequestsForPlayer(GetPlayer().GetGUID());
            LFGuildApplications lfGuildApplications = new();
            lfGuildApplications.NumRemaining = 10 - Global.GuildFinderMgr.CountRequestsFromPlayer(GetPlayer().GetGUID());

            for (var i = 0; i < applicatedGuilds.Count; ++i)
            {
                MembershipRequest application = applicatedGuilds[i];
                LFGuildApplicationData applicationData = new();

                Guild guild = Global.GuildMgr.GetGuildByGuid(application.GetGuildGuid());
                LFGuildSettings guildSettings = Global.GuildFinderMgr.GetGuildSettings(application.GetGuildGuid());

                applicationData.GuildGUID = application.GetGuildGuid();
                applicationData.GuildVirtualRealm = Global.WorldMgr.GetVirtualRealmAddress();
                applicationData.GuildName = guild.GetName();
                applicationData.ClassRoles = guildSettings.GetClassRoles();
                applicationData.PlayStyle = guildSettings.GetInterests();
                applicationData.Availability = guildSettings.GetAvailability();
                applicationData.SecondsSinceCreated = (uint)(GameTime.GetGameTime() - application.GetSubmitTime());
                applicationData.SecondsUntilExpiration = (uint)(application.GetExpiryTime() - GameTime.GetGameTime());
                applicationData.Comment = application.GetComment();

                lfGuildApplications.Application.Add(applicationData);
            }

            GetPlayer().SendPacket(lfGuildApplications);
        }

        [WorldPacketHandler(ClientOpcodes.LfGuildGetGuildPost)]
        void HandleGuildFinderGetGuildPost(LFGuildGetGuildPost lfGuildGetGuildPost)
        {
            Player player = GetPlayer();

            Guild guild = player.GetGuild();
            if (!guild) // Player must be in guild
                return;

            LFGuildPost lfGuildPost = new();
            if (guild.GetLeaderGUID() == player.GetGUID())
            {
                LFGuildSettings settings = Global.GuildFinderMgr.GetGuildSettings(guild.GetGUID());
                if (settings == null)
                    return;

                lfGuildPost.Post.HasValue = true;
                lfGuildPost.Post.Value.Active = settings.IsListed();
                lfGuildPost.Post.Value.PlayStyle = settings.GetInterests();
                lfGuildPost.Post.Value.Availability = settings.GetAvailability();
                lfGuildPost.Post.Value.ClassRoles = settings.GetClassRoles();
                lfGuildPost.Post.Value.LevelRange = settings.GetLevel();
                lfGuildPost.Post.Value.Comment = settings.GetComment();
            }

            player.SendPacket(lfGuildPost);
        }

        // Lists all recruits for a guild - Misses times
        [WorldPacketHandler(ClientOpcodes.LfGuildGetRecruits)]
        void HandleGuildFinderGetRecruits(LFGuildGetRecruits lfGuildGetRecruits)
        {
            Player player = GetPlayer();
            Guild guild = player.GetGuild();
            if (!guild)
                return;

            long now = GameTime.GetGameTime();
            LFGuildRecruits lfGuildRecruits = new();
            lfGuildRecruits.UpdateTime = now;
            var recruitsList = Global.GuildFinderMgr.GetAllMembershipRequestsForGuild(guild.GetGUID());
            if (recruitsList != null)
            {
                foreach (var recruitRequestPair in recruitsList)
                {
                    LFGuildRecruitData recruitData = new();
                    recruitData.RecruitGUID = recruitRequestPair.Key;
                    recruitData.RecruitVirtualRealm = Global.WorldMgr.GetVirtualRealmAddress();
                    recruitData.Comment = recruitRequestPair.Value.GetComment();
                    recruitData.ClassRoles = recruitRequestPair.Value.GetClassRoles();
                    recruitData.PlayStyle = recruitRequestPair.Value.GetInterests();
                    recruitData.Availability = recruitRequestPair.Value.GetAvailability();
                    recruitData.SecondsSinceCreated = (uint)(now - recruitRequestPair.Value.GetSubmitTime());
                    recruitData.SecondsUntilExpiration = (uint)(recruitRequestPair.Value.GetExpiryTime() - now);

                    CharacterCacheEntry charInfo = Global.CharacterCacheStorage.GetCharacterCacheByGuid(recruitRequestPair.Key);
                    if (charInfo != null)
                    {
                        recruitData.Name = charInfo.Name;
                        recruitData.CharacterClass = (byte)charInfo.ClassId;
                        recruitData.CharacterGender = (byte)charInfo.Sex;
                        recruitData.CharacterLevel = charInfo.Level;
                    }

                    lfGuildRecruits.Recruits.Add(recruitData);
                }
            }

            player.SendPacket(lfGuildRecruits);
        }

        [WorldPacketHandler(ClientOpcodes.LfGuildRemoveRecruit)]
        void HandleGuildFinderRemoveRecruit(LFGuildRemoveRecruit lfGuildRemoveRecruit)
        {
            if (!lfGuildRemoveRecruit.GuildGUID.IsGuild())
                return;

            Global.GuildFinderMgr.RemoveMembershipRequest(GetPlayer().GetGUID(), lfGuildRemoveRecruit.GuildGUID);
        }

        // Sent any time a guild master sets an option in the interface and when listing / unlisting his guild
        [WorldPacketHandler(ClientOpcodes.LfGuildSetGuildPost)]
        void HandleGuildFinderSetGuildPost(LFGuildSetGuildPost lfGuildSetGuildPost)
        {
            // Level sent is zero if untouched, force to any (from interface). Idk why
            if (lfGuildSetGuildPost.LevelRange == 0)
                lfGuildSetGuildPost.LevelRange = (uint)GuildFinderOptionsLevel.Any;

            if (!lfGuildSetGuildPost.ClassRoles.HasAnyFlag((uint)GuildFinderOptionsRoles.All) || lfGuildSetGuildPost.ClassRoles > (uint)GuildFinderOptionsRoles.All)
                return;
            if (!lfGuildSetGuildPost.Availability.HasAnyFlag((uint)GuildFinderOptionsAvailability.Always) || lfGuildSetGuildPost.Availability > (uint)GuildFinderOptionsAvailability.Always)
                return;
            if (!lfGuildSetGuildPost.PlayStyle.HasAnyFlag((uint)GuildFinderOptionsInterest.All) || lfGuildSetGuildPost.PlayStyle > (uint)GuildFinderOptionsInterest.All)
                return;
            if (!lfGuildSetGuildPost.LevelRange.HasAnyFlag((uint)GuildFinderOptionsLevel.All) || lfGuildSetGuildPost.LevelRange > (uint)GuildFinderOptionsLevel.All)
                return;

            Player player = GetPlayer();

            if (player.GetGuildId() == 0) // Player must be in guild
                return;

            Guild guild = Global.GuildMgr.GetGuildById(player.GetGuildId());
            if (guild == null)
                return;

            if (guild.GetLeaderGUID() != player.GetGUID())
                    return;

            LFGuildSettings settings = new(lfGuildSetGuildPost.Active, (uint)player.GetTeam(), player.GetGuild().GetGUID(), lfGuildSetGuildPost.ClassRoles,
                lfGuildSetGuildPost.Availability, lfGuildSetGuildPost.PlayStyle, lfGuildSetGuildPost.LevelRange, lfGuildSetGuildPost.Comment);
            Global.GuildFinderMgr.SetGuildSettings(player.GetGuild().GetGUID(), settings);
        }
    }
}
