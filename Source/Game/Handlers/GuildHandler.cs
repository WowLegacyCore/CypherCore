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
using Game.Entities;
using Game.Guilds;
using Game.Networking;
using Game.Networking.Packets;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.QueryGuildInfo, Status = SessionStatus.Authed)]
        void HandleGuildQuery(QueryGuildInfo query)
        {
            Guild guild = Global.GuildMgr.GetGuildByGuid(query.GuildGuid);
            if (guild)
            {
                if (guild.IsMember(query.PlayerGuid))
                {
                    guild.SendQueryResponse(this, query.PlayerGuid);
                    return;
                }
            }

            QueryGuildInfoResponse response = new();
            response.GuildGUID = query.GuildGuid;
            response.PlayerGuid = query.PlayerGuid;
            SendPacket(response);
        }

        [WorldPacketHandler(ClientOpcodes.GuildInviteByName)]
        void HandleGuildInviteByName(GuildInviteByName packet)
        {
            if (!ObjectManager.NormalizePlayerName(ref packet.Name))
                return;

            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleInviteMember(this, packet.Name);
        }

        [WorldPacketHandler(ClientOpcodes.GuildOfficerRemoveMember)]
        void HandleGuildOfficerRemoveMember(GuildOfficerRemoveMember packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleRemoveMember(this, packet.Removee);
        }

        [WorldPacketHandler(ClientOpcodes.AcceptGuildInvite)]
        void HandleGuildAcceptInvite(AcceptGuildInvite packet)
        {
            if (GetPlayer().GetGuildId() == 0)
            {
                Guild guild = Global.GuildMgr.GetGuildById(GetPlayer().GetGuildIdInvited());
                if (guild)
                    guild.HandleAcceptMember(this);
            }
        }

        [WorldPacketHandler(ClientOpcodes.GuildDeclineInvitation)]
        void HandleGuildDeclineInvitation(GuildDeclineInvitation packet)
        {
            GetPlayer().SetGuildIdInvited(0);
            GetPlayer().SetInGuild(0);
        }

        [WorldPacketHandler(ClientOpcodes.GuildGetRoster)]
        void HandleGuildGetRoster(GuildGetRoster packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleRoster(this);
            else
                Guild.SendCommandResult(this, GuildCommandType.GetRoster, GuildCommandError.PlayerNotInGuild);
        }

        [WorldPacketHandler(ClientOpcodes.GuildPromoteMember)]
        void HandleGuildPromoteMember(GuildPromoteMember packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleUpdateMemberRank(this, packet.Promotee, false);
        }

        [WorldPacketHandler(ClientOpcodes.GuildDemoteMember)]
        void HandleGuildDemoteMember(GuildDemoteMember packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleUpdateMemberRank(this, packet.Demotee, true);
        }

        [WorldPacketHandler(ClientOpcodes.GuildAssignMemberRank)]
        void HandleGuildAssignRank(GuildAssignMemberRank packet)
        {
            ObjectGuid setterGuid = GetPlayer().GetGUID();

            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleSetMemberRank(this, packet.Member, setterGuid, (byte)packet.RankOrder);
        }

        [WorldPacketHandler(ClientOpcodes.GuildLeave)]
        void HandleGuildLeave(GuildLeave packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleLeaveMember(this);
        }

        [WorldPacketHandler(ClientOpcodes.GuildDelete)]
        void HandleGuildDisband(GuildDelete packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleDelete(this);
        }

        [WorldPacketHandler(ClientOpcodes.GuildUpdateMotdText)]
        void HandleGuildUpdateMotdText(GuildUpdateMotdText packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleSetMOTD(this, packet.MotdText);
        }

        [WorldPacketHandler(ClientOpcodes.GuildSetMemberNote)]
        void HandleGuildSetMemberNote(GuildSetMemberNote packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleSetMemberNote(this, packet.Note, packet.NoteeGUID, packet.IsPublic);
        }

        [WorldPacketHandler(ClientOpcodes.GuildGetRanks)]
        void HandleGuildGetRanks(GuildGetRanks packet)
        {
            Guild guild = Global.GuildMgr.GetGuildByGuid(packet.GuildGUID);
            if (guild)
                if (guild.IsMember(GetPlayer().GetGUID()))
                    guild.SendGuildRankInfo(this);
        }

        [WorldPacketHandler(ClientOpcodes.GuildAddRank)]
        void HandleGuildAddRank(GuildAddRank packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleAddNewRank(this, packet.Name);
        }

        [WorldPacketHandler(ClientOpcodes.GuildDeleteRank)]
        void HandleGuildDeleteRank(GuildDeleteRank packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleRemoveRank(this, (byte)packet.RankOrder);
        }

        [WorldPacketHandler(ClientOpcodes.GuildUpdateInfoText)]
        void HandleGuildUpdateInfoText(GuildUpdateInfoText packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleSetInfo(this, packet.InfoText);
        }

        [WorldPacketHandler(ClientOpcodes.SaveGuildEmblem)]
        void HandleSaveGuildEmblem(SaveGuildEmblem packet)
        {
            Guild.EmblemInfo emblemInfo = new();
            emblemInfo.ReadPacket(packet);

            if (GetPlayer().GetNPCIfCanInteractWith(packet.Vendor, NPCFlags.TabardDesigner, NPCFlags2.None))
            {
                // Remove fake death
                if (GetPlayer().HasUnitState(UnitState.Died))
                    GetPlayer().RemoveAurasByType(AuraType.FeignDeath);

                if (!emblemInfo.ValidateEmblemColors())
                {
                    Guild.SendSaveEmblemResult(this, GuildEmblemError.InvalidTabardColors);
                    return;
                }

                Guild guild = GetPlayer().GetGuild();
                if (guild)
                    guild.HandleSetEmblem(this, emblemInfo);
                else
                    Guild.SendSaveEmblemResult(this, GuildEmblemError.NoGuild); // "You are not part of a guild!";
            }
            else
                Guild.SendSaveEmblemResult(this, GuildEmblemError.InvalidVendor); // "That's not an emblem vendor!"
        }


        [WorldPacketHandler(ClientOpcodes.GuildBankRemainingWithdrawMoneyQuery)]
        void HandleGuildBankMoneyWithdrawn(GuildBankRemainingWithdrawMoneyQuery packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.SendMoneyInfo(this);
        }

        [WorldPacketHandler(ClientOpcodes.GuildPermissionsQuery)]
        void HandleGuildPermissionsQuery(GuildPermissionsQuery packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.SendPermissions(this);
        }

        [WorldPacketHandler(ClientOpcodes.GuildBankActivate)]
        void HandleGuildBankActivate(GuildBankActivate packet)
        {
            GameObject go = GetPlayer().GetGameObjectIfCanInteractWith(packet.Banker, GameObjectTypes.GuildBank);
            if (go == null)
                return;

            Guild guild = GetPlayer().GetGuild();
            if (guild == null)
            {
                Guild.SendCommandResult(this, GuildCommandType.ViewTab, GuildCommandError.PlayerNotInGuild);
                return;
            }

            guild.SendBankList(this, 0, packet.FullUpdate);
        }

        [WorldPacketHandler(ClientOpcodes.GuildBankQueryTab)]
        void HandleGuildBankQueryTab(GuildBankQueryTab packet)
        {
            if (GetPlayer().GetGameObjectIfCanInteractWith(packet.Banker, GameObjectTypes.GuildBank))
            {
                Guild guild = GetPlayer().GetGuild();
                if (guild)
                    guild.SendBankList(this, packet.Tab, packet.FullUpdate);
            }
        }

        [WorldPacketHandler(ClientOpcodes.GuildBankDepositMoney)]
        void HandleGuildBankDepositMoney(GuildBankDepositMoney packet)
        {
            if (GetPlayer().GetGameObjectIfCanInteractWith(packet.Banker, GameObjectTypes.GuildBank))
            {
                if (packet.Money != 0 && GetPlayer().HasEnoughMoney(packet.Money))
                {
                    Guild guild = GetPlayer().GetGuild();
                    if (guild)
                        guild.HandleMemberDepositMoney(this, packet.Money);
                }
            }
        }

        [WorldPacketHandler(ClientOpcodes.GuildBankWithdrawMoney)]
        void HandleGuildBankWithdrawMoney(GuildBankWithdrawMoney packet)
        {
            if (packet.Money != 0 && GetPlayer().GetGameObjectIfCanInteractWith(packet.Banker, GameObjectTypes.GuildBank))
            {
                Guild guild = GetPlayer().GetGuild();
                if (guild)
                    guild.HandleMemberWithdrawMoney(this, packet.Money);
            }
        }

        // For now we don't want to handle this packet, I have no clue what this packet even is now.
        // Will see when testing world.
        void HandleDepositGuildBankItem(DepositGuildBankItem depositGuildBankItem)
        {
            if (!GetPlayer().GetGameObjectIfCanInteractWith(depositGuildBankItem.Banker, GameObjectTypes.GuildBank))
                return;

            Guild guild = GetPlayer().GetGuild();
            if (guild == null)
                return;

            if (!Player.IsInventoryPos(depositGuildBankItem.ContainerSlot.ValueOr(InventorySlots.Bag0), depositGuildBankItem.ContainerItemSlot))
                GetPlayer().SendEquipError(InventoryResult.InternalBagError, null);
            else
                guild.SwapItemsWithInventory(GetPlayer(), false, depositGuildBankItem.BankTab, depositGuildBankItem.BankSlot,
                    depositGuildBankItem.ContainerSlot.ValueOr(InventorySlots.Bag0), depositGuildBankItem.ContainerItemSlot, 0);
        }

        [WorldPacketHandler(ClientOpcodes.StoreGuildBankItem)]
        void HandleStoreGuildBankItem(StoreGuildBankItem storeGuildBankItem)
        {
            if (!GetPlayer().GetGameObjectIfCanInteractWith(storeGuildBankItem.Banker, GameObjectTypes.GuildBank))
                return;

            Guild guild = GetPlayer().GetGuild();
            if (guild == null)
                return;

            if (!Player.IsInventoryPos(storeGuildBankItem.ContainerSlot.ValueOr(InventorySlots.Bag0), storeGuildBankItem.ContainerItemSlot))
                GetPlayer().SendEquipError(InventoryResult.InternalBagError, null);
            else
                guild.SwapItemsWithInventory(GetPlayer(), true, storeGuildBankItem.BankTab, storeGuildBankItem.BankSlot,
                    storeGuildBankItem.ContainerSlot.ValueOr(InventorySlots.Bag0), storeGuildBankItem.ContainerItemSlot, 0);
        }

        [WorldPacketHandler(ClientOpcodes.SwapItemWithGuildBankItem)]
        void HandleSwapItemWithGuildBankItem(SwapItemWithGuildBankItem swapItemWithGuildBankItem)
        {
            if (!GetPlayer().GetGameObjectIfCanInteractWith(swapItemWithGuildBankItem.Banker, GameObjectTypes.GuildBank))
                return;

            Guild guild = GetPlayer().GetGuild();
            if (guild == null)
                return;

            if (!Player.IsInventoryPos(swapItemWithGuildBankItem.ContainerSlot.ValueOr(InventorySlots.Bag0), swapItemWithGuildBankItem.ContainerItemSlot))
                GetPlayer().SendEquipError(InventoryResult.InternalBagError, null);
            else
                guild.SwapItemsWithInventory(GetPlayer(), false, swapItemWithGuildBankItem.BankTab, swapItemWithGuildBankItem.BankSlot,
                    swapItemWithGuildBankItem.ContainerSlot.ValueOr(InventorySlots.Bag0), swapItemWithGuildBankItem.ContainerItemSlot, 0);
        }

        [WorldPacketHandler(ClientOpcodes.SwapGuildBankItemWithGuildBankItem)]
        void HandleSwapGuildBankItemWithGuildBankItem(SwapGuildBankItemWithGuildBankItem swapGuildBankItemWithGuildBankItem)
        {
            if (!GetPlayer().GetGameObjectIfCanInteractWith(swapGuildBankItemWithGuildBankItem.Banker, GameObjectTypes.GuildBank))
                return;

            Guild guild = GetPlayer().GetGuild();
            if (guild == null)
                return;

            guild.SwapItems(GetPlayer(), swapGuildBankItemWithGuildBankItem.BankTab[0], swapGuildBankItemWithGuildBankItem.BankSlot[0],
                swapGuildBankItemWithGuildBankItem.BankTab[1], swapGuildBankItemWithGuildBankItem.BankSlot[1], 0);
        }

        [WorldPacketHandler(ClientOpcodes.MoveGuildBankItem)]
        void HandleMoveGuildBankItem(MoveGuildBankItem moveGuildBankItem)
        {
            if (!GetPlayer().GetGameObjectIfCanInteractWith(moveGuildBankItem.Banker, GameObjectTypes.GuildBank))
                return;

            Guild guild = GetPlayer().GetGuild();
            if (guild == null)
                return;

            guild.SwapItems(GetPlayer(), moveGuildBankItem.BankTab, moveGuildBankItem.BankSlot, moveGuildBankItem.BankTab1, moveGuildBankItem.BankSlot1, 0);
        }

        [WorldPacketHandler(ClientOpcodes.MergeItemWithGuildBankItem)]
        void HandleMergeItemWithGuildBankItem(MergeItemWithGuildBankItem mergeItemWithGuildBankItem)
        {
            if (!GetPlayer().GetGameObjectIfCanInteractWith(mergeItemWithGuildBankItem.Banker, GameObjectTypes.GuildBank))
                return;

            Guild guild = GetPlayer().GetGuild();
            if (guild == null)
                return;

            if (!Player.IsInventoryPos(mergeItemWithGuildBankItem.ContainerSlot.ValueOr(InventorySlots.Bag0), mergeItemWithGuildBankItem.ContainerItemSlot))
                GetPlayer().SendEquipError(InventoryResult.InternalBagError, null);
            else
                guild.SwapItemsWithInventory(GetPlayer(), false, mergeItemWithGuildBankItem.BankTab, mergeItemWithGuildBankItem.BankSlot,
                    mergeItemWithGuildBankItem.ContainerSlot.ValueOr(InventorySlots.Bag0), mergeItemWithGuildBankItem.ContainerItemSlot, mergeItemWithGuildBankItem.StackCount);
        }

        [WorldPacketHandler(ClientOpcodes.SplitItemToGuildBank)]
        void HandleSplitItemToGuildBank(SplitItemToGuildBank splitItemToGuildBank)
        {
            if (!GetPlayer().GetGameObjectIfCanInteractWith(splitItemToGuildBank.Banker, GameObjectTypes.GuildBank))
                return;

            Guild guild = GetPlayer().GetGuild();
            if (guild == null)
                return;

            if (!Player.IsInventoryPos(splitItemToGuildBank.ContainerSlot.ValueOr(InventorySlots.Bag0), splitItemToGuildBank.ContainerItemSlot))
                GetPlayer().SendEquipError(InventoryResult.InternalBagError, null);
            else
                guild.SwapItemsWithInventory(GetPlayer(), false, splitItemToGuildBank.BankTab, splitItemToGuildBank.BankSlot,
                    splitItemToGuildBank.ContainerSlot.ValueOr(InventorySlots.Bag0), splitItemToGuildBank.ContainerItemSlot, splitItemToGuildBank.StackCount);
        }

        [WorldPacketHandler(ClientOpcodes.MergeGuildBankItemWithItem)]
        void HandleMergeGuildBankItemWithItem(MergeGuildBankItemWithItem mergeGuildBankItemWithItem)
        {
            if (!GetPlayer().GetGameObjectIfCanInteractWith(mergeGuildBankItemWithItem.Banker, GameObjectTypes.GuildBank))
                return;

            Guild guild = GetPlayer().GetGuild();
            if (guild == null)
                return;

            if (!Player.IsInventoryPos(mergeGuildBankItemWithItem.ContainerSlot.ValueOr(InventorySlots.Bag0), mergeGuildBankItemWithItem.ContainerItemSlot))
                GetPlayer().SendEquipError(InventoryResult.InternalBagError, null);
            else
                guild.SwapItemsWithInventory(GetPlayer(), true, mergeGuildBankItemWithItem.BankTab, mergeGuildBankItemWithItem.BankSlot,
                    mergeGuildBankItemWithItem.ContainerSlot.ValueOr(InventorySlots.Bag0), mergeGuildBankItemWithItem.ContainerItemSlot, mergeGuildBankItemWithItem.StackCount);
        }

        [WorldPacketHandler(ClientOpcodes.SplitGuildBankItemToInventory)]
        void HandleSplitGuildBankItemToInventory(SplitGuildBankItemToInventory splitGuildBankItemToInventory)
        {
            if (!GetPlayer().GetGameObjectIfCanInteractWith(splitGuildBankItemToInventory.Banker, GameObjectTypes.GuildBank))
                return;

            Guild guild = GetPlayer().GetGuild();
            if (guild == null)
                return;

            if (!Player.IsInventoryPos(splitGuildBankItemToInventory.ContainerSlot.ValueOr(InventorySlots.Bag0), splitGuildBankItemToInventory.ContainerItemSlot))
                GetPlayer().SendEquipError(InventoryResult.InternalBagError, null);
            else
                guild.SwapItemsWithInventory(GetPlayer(), true, splitGuildBankItemToInventory.BankTab, splitGuildBankItemToInventory.BankSlot,
                    splitGuildBankItemToInventory.ContainerSlot.ValueOr(InventorySlots.Bag0), splitGuildBankItemToInventory.ContainerItemSlot, splitGuildBankItemToInventory.StackCount);
        }

        [WorldPacketHandler(ClientOpcodes.AutoStoreGuildBankItem)]
        void HandleAutoStoreGuildBankItem(AutoStoreGuildBankItem autoStoreGuildBankItem)
        {
            if (!GetPlayer().GetGameObjectIfCanInteractWith(autoStoreGuildBankItem.Banker, GameObjectTypes.GuildBank))
                return;

            Guild guild = GetPlayer().GetGuild();
            if (guild == null)
                return;

            guild.SwapItemsWithInventory(GetPlayer(), true, autoStoreGuildBankItem.BankTab, autoStoreGuildBankItem.BankSlot, InventorySlots.Bag0, ItemConst.NullSlot, 0);
        }

        [WorldPacketHandler(ClientOpcodes.MergeGuildBankItemWithGuildBankItem)]
        void HandleMergeGuildBankItemWithGuildBankItem(MergeGuildBankItemWithGuildBankItem mergeGuildBankItemWithGuildBankItem)
        {
            if (!GetPlayer().GetGameObjectIfCanInteractWith(mergeGuildBankItemWithGuildBankItem.Banker, GameObjectTypes.GuildBank))
                return;

            Guild guild = GetPlayer().GetGuild();
            if (guild == null)
                return;

            guild.SwapItems(GetPlayer(), mergeGuildBankItemWithGuildBankItem.BankTab, mergeGuildBankItemWithGuildBankItem.BankSlot,
                mergeGuildBankItemWithGuildBankItem.BankTab1, mergeGuildBankItemWithGuildBankItem.BankSlot1, mergeGuildBankItemWithGuildBankItem.StackCount);
        }

        [WorldPacketHandler(ClientOpcodes.SplitGuildBankItem)]
        void HandleSplitGuildBankItem(SplitGuildBankItem splitGuildBankItem)
        {
            if (!GetPlayer().GetGameObjectIfCanInteractWith(splitGuildBankItem.Banker, GameObjectTypes.GuildBank))
                return;

            Guild guild = GetPlayer().GetGuild();
            if (guild == null)
                return;

            guild.SwapItems(GetPlayer(), splitGuildBankItem.BankTab, splitGuildBankItem.BankSlot,
                splitGuildBankItem.BankTab1, splitGuildBankItem.BankSlot1, splitGuildBankItem.StackCount);
        }

        [WorldPacketHandler(ClientOpcodes.GuildBankBuyTab)]
        void HandleGuildBankBuyTab(GuildBankBuyTab packet)
        {
            if (packet.Banker.IsEmpty() || GetPlayer().GetGameObjectIfCanInteractWith(packet.Banker, GameObjectTypes.GuildBank))
            {
                Guild guild = GetPlayer().GetGuild();
                if (guild)
                    guild.HandleBuyBankTab(this, packet.BankTab);
            }
        }

        [WorldPacketHandler(ClientOpcodes.GuildBankUpdateTab)]
        void HandleGuildBankUpdateTab(GuildBankUpdateTab packet)
        {
            if (!string.IsNullOrEmpty(packet.Name) && !string.IsNullOrEmpty(packet.Icon))
            {
                if (GetPlayer().GetGameObjectIfCanInteractWith(packet.Banker, GameObjectTypes.GuildBank))
                {
                    Guild guild = GetPlayer().GetGuild();
                    if (guild)
                        guild.HandleSetBankTabInfo(this, packet.BankTab, packet.Name, packet.Icon);
                }
            }
        }

        [WorldPacketHandler(ClientOpcodes.GuildBankLogQuery)]
        void HandleGuildBankLogQuery(GuildBankLogQuery packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.SendBankLog(this, (byte)packet.Tab);
        }

        [WorldPacketHandler(ClientOpcodes.GuildBankTextQuery)]
        void HandleGuildBankTextQuery(GuildBankTextQuery packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.SendBankTabText(this, (byte)packet.Tab);
        }

        [WorldPacketHandler(ClientOpcodes.GuildBankSetTabText)]
        void HandleGuildBankSetTabText(GuildBankSetTabText packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.SetBankTabText((byte)packet.Tab, packet.TabText);
        }

        [WorldPacketHandler(ClientOpcodes.GuildSetRankPermissions)]
        void HandleGuildSetRankPermissions(GuildSetRankPermissions packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild == null)
                return;

            Guild.GuildBankRightsAndSlots[] rightsAndSlots = new Guild.GuildBankRightsAndSlots[GuildConst.MaxBankTabs];
            for (byte tabId = 0; tabId < GuildConst.MaxBankTabs; ++tabId)
                rightsAndSlots[tabId] = new Guild.GuildBankRightsAndSlots(tabId, (sbyte)packet.TabFlags[tabId], (int)packet.TabWithdrawItemLimit[tabId]);

            guild.HandleSetRankInfo(this, (byte)packet.RankOrder, packet.RankName, (GuildRankRights)packet.Flags, packet.WithdrawGoldLimit, rightsAndSlots);
        }

        [WorldPacketHandler(ClientOpcodes.RequestGuildPartyState)]
        void HandleGuildRequestPartyState(RequestGuildPartyState packet)
        {
            Guild guild = Global.GuildMgr.GetGuildByGuid(packet.GuildGUID);
            if (guild)
                guild.HandleGuildPartyRequest(this);
        }

        [WorldPacketHandler(ClientOpcodes.GuildChangeNameRequest, Processing = PacketProcessing.Inplace)]
        void HandleGuildChallengeUpdateRequest(GuildChallengeUpdateRequest packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleGuildRequestChallengeUpdate(this);
        }

        [WorldPacketHandler(ClientOpcodes.DeclineGuildInvites)]
        void HandleDeclineGuildInvites(DeclineGuildInvites packet)
        {
            if (packet.Allow)
                GetPlayer().AddPlayerFlag(PlayerFlags.AutoDeclineGuild);
            else
                GetPlayer().RemovePlayerFlag(PlayerFlags.AutoDeclineGuild);
        }

        [WorldPacketHandler(ClientOpcodes.RequestGuildRewardsList)]
        void HandleRequestGuildRewardsList(RequestGuildRewardsList packet)
        {
            if (Global.GuildMgr.GetGuildById(GetPlayer().GetGuildId()))
            {
                var rewards = Global.GuildMgr.GetGuildRewards();

                GuildRewardList rewardList = new();
                rewardList.Version = GameTime.GetGameTime();

                for (int i = 0; i < rewards.Count; i++)
                {
                    GuildRewardItem rewardItem = new();
                    rewardItem.ItemID = rewards[i].ItemID;
                    rewardItem.RaceMask = (uint)rewards[i].RaceMask;
                    rewardItem.MinGuildLevel = 0;
                    rewardItem.MinGuildRep = rewards[i].MinGuildRep;
                    rewardItem.Cost = rewards[i].Cost;
                    rewardList.RewardItems.Add(rewardItem);
                }

                SendPacket(rewardList);
            }
        }

        [WorldPacketHandler(ClientOpcodes.GuildQueryNews)]
        void HandleGuildQueryNews(GuildQueryNews packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                if (guild.GetGUID() == packet.GuildGUID)
                    guild.SendNewsUpdate(this);
        }

        [WorldPacketHandler(ClientOpcodes.GuildReplaceGuildMaster)]
        void HandleGuildReplaceGuildMaster(GuildReplaceGuildMaster replaceGuildMaster)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleSetNewGuildMaster(this, "", true);
        }

        [WorldPacketHandler(ClientOpcodes.GuildSetGuildMaster)]
        void HandleGuildSetGuildMaster(GuildSetGuildMaster packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleSetNewGuildMaster(this, packet.NewMasterName, false);
        }

        [WorldPacketHandler(ClientOpcodes.GuildSetAchievementTracking)]
        void HandleGuildSetAchievementTracking(GuildSetAchievementTracking packet)
        {
            Guild guild = GetPlayer().GetGuild();
            if (guild)
                guild.HandleSetAchievementTracking(this, packet.AchievementIDs);
        }
    }
}
