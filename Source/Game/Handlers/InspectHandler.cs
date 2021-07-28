
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
using Game.Entities;
using Game.Guilds;
using Game.Networking;
using Game.Networking.Packets;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.Inspect, Processing = PacketProcessing.Inplace)]
        void HandleInspect(Inspect inspect)
        {
            Player player = Global.ObjAccessor.GetPlayer(_player, inspect.Target);
            if (!player)
            {
                Log.outDebug(LogFilter.Network, "WorldSession.HandleInspectOpcode: Target {0} not found.", inspect.Target.ToString());
                return;
            }

            if (!GetPlayer().IsWithinDistInMap(player, SharedConst.InspectDistance, false))
                return;

            if (GetPlayer().IsValidAttackTarget(player))
                return;

            InspectResult inspectResult = new();
            inspectResult.DisplayInfo.Initialize(player);

            if (GetPlayer().CanBeGameMaster() || WorldConfig.GetIntValue(WorldCfg.TalentsInspecting) + (GetPlayer().GetTeamId() == player.GetTeamId() ? 1 : 0) > 1)
            {
                var talents = player.GetTalentMap(player.GetActiveTalentGroup());
                foreach (var v in talents)
                {
                    if (v.Value != PlayerSpellState.Removed)
                        inspectResult.Talents.Add((ushort)v.Key);
                }
            }

            Guild guild = Global.GuildMgr.GetGuildById(player.GetGuildId());
            if (guild)
            {
                inspectResult.GuildData.HasValue = true;

                InspectGuildData guildData = new();
                guildData.GuildGUID = guild.GetGUID();
                guildData.NumGuildMembers = guild.GetMembersCount();
                inspectResult.GuildData.Set(guildData);
            }

            inspectResult.ItemLevel = (int)player.GetAverageItemLevel();
            inspectResult.LifetimeMaxRank = player.GetUpdateField<byte>(ActivePlayerFields.Bytes1, (byte)ActivePlayerBytes1Offset.LifetimeMaxRank);
            inspectResult.TodayHK = player.GetUpdateField<byte>(ActivePlayerFields.Bytes2, (byte)ActivePlayerBytes2Offset.TodayHonorableKills);
            inspectResult.YesterdayHK = player.GetUpdateField<byte>(ActivePlayerFields.Bytes2, (byte)ActivePlayerBytes2Offset.YesterdayHonorableKills);
            inspectResult.LifetimeHK = player.GetUpdateField<uint>(ActivePlayerFields.LifetimeHonorableKills);
            inspectResult.HonorLevel = player.GetUpdateField<uint>(PlayerFields.HonorLevel);

            SendPacket(inspectResult);
        }
    }
}
