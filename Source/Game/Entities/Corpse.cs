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
using Framework.Database;
using Game.DataStorage;
using Game.Loots;
using Game.Maps;
using System.Collections.Generic;
using System.Text;
using Framework.Collections;

namespace Game.Entities
{
    public class Corpse : WorldObject
    {
        public Corpse(CorpseType type = CorpseType.Bones) : base(type != CorpseType.Bones)
        {
            m_type = type;
            ObjectTypeId = TypeId.Corpse;
            ObjectTypeMask |= TypeMask.Corpse;

            m_updateFlag.Stationary = true;

            ValuesCount = (int)CorpseFields.End;

            m_time = GameTime.GetGameTime();
        }

        public override void AddToWorld()
        {
            // Register the corpse for guid lookup
            if (!IsInWorld)
                GetMap().GetObjectsStore().Add(GetGUID(), this);

            base.AddToWorld();
        }

        public override void RemoveFromWorld()
        {
            // Remove the corpse from the accessor
            if (IsInWorld)
                GetMap().GetObjectsStore().Remove(GetGUID());

            base.RemoveFromWorld();
        }

        public bool Create(ulong guidlow, Map map)
        {
            _Create(ObjectGuid.Create(HighGuid.Corpse, map.GetId(), 0, guidlow));
            return true;
        }

        public bool Create(ulong guidlow, Player owner)
        {
            Cypher.Assert(owner != null);

            Relocate(owner.GetPositionX(), owner.GetPositionY(), owner.GetPositionZ(), owner.GetOrientation());

            if (!IsPositionValid())
            {
                Log.outError(LogFilter.Player, "Corpse (guidlow {0}, owner {1}) not created. Suggested coordinates isn't valid (X: {2} Y: {3})",
                    guidlow, owner.GetName(), owner.GetPositionX(), owner.GetPositionY());
                return false;
            }

            _Create(ObjectGuid.Create(HighGuid.Corpse, owner.GetMapId(), 0, guidlow));

            SetObjectScale(1);
            SetOwnerGUID(owner.GetGUID());

            _cellCoord = GridDefines.ComputeCellCoord(GetPositionX(), GetPositionY());

            PhasingHandler.InheritPhaseShift(this, owner);

            return true;
        }

        public void SaveToDB()
        {
            // prevent DB data inconsistence problems and duplicates
            SQLTransaction trans = new();
            DeleteFromDB(trans);

            StringBuilder items = new();
            for (var i = 0; i < EquipmentSlot.End; ++i)
                items.Append($"{GetUpdateField<uint>(CorpseFields.Items + i)} ");

            byte index = 0;
            PreparedStatement stmt = DB.Characters.GetPreparedStatement(CharStatements.INS_CORPSE);
            stmt.AddValue(index++, GetOwnerGUID().GetCounter());                            // guid
            stmt.AddValue(index++, GetPositionX());                                         // posX
            stmt.AddValue(index++, GetPositionY());                                         // posY
            stmt.AddValue(index++, GetPositionZ());                                         // posZ
            stmt.AddValue(index++, GetOrientation());                                       // orientation
            stmt.AddValue(index++, GetMapId());                                             // mapId
            stmt.AddValue(index++, GetDisplayId());                                         // displayId
            stmt.AddValue(index++, items.ToString());                                       // itemCache
            stmt.AddValue(index++, (byte)GetRace());                                        // race
            stmt.AddValue(index++, (byte)GetClass());                                       // class
            stmt.AddValue(index++, (byte)GetSex());                                         // gender
            stmt.AddValue(index++, GetUpdateField<uint>(CorpseFields.Flags));               // flags
            stmt.AddValue(index++, GetUpdateField<uint>(CorpseFields.DynamicFlags));        // dynFlags
            stmt.AddValue(index++, (uint)m_time);                                           // time
            stmt.AddValue(index++, (uint)GetCorpseType());                                  // corpseType
            stmt.AddValue(index++, GetInstanceId());                                        // instanceId
            trans.Append(stmt);

            foreach (var phaseId in GetPhaseShift().GetPhases().Keys)
            {
                index = 0;
                stmt = DB.Characters.GetPreparedStatement(CharStatements.INS_CORPSE_PHASES);
                stmt.AddValue(index++, GetOwnerGUID().GetCounter());                        // OwnerGuid
                stmt.AddValue(index++, phaseId);                                            // PhaseId
                trans.Append(stmt);
            }

            foreach (var customization in GetCustomizationChoices())
            {
                index = 0;
                stmt = DB.Characters.GetPreparedStatement(CharStatements.INS_CORPSE_CUSTOMIZATIONS);
                stmt.AddValue(index++, GetOwnerGUID().GetCounter());                        // OwnerGuid
                stmt.AddValue(index++, customization.ChrCustomizationOptionID);
                stmt.AddValue(index++, customization.ChrCustomizationChoiceID);
                trans.Append(stmt);
            }

            DB.Characters.CommitTransaction(trans);
        }

        public void DeleteFromDB(SQLTransaction trans) => DeleteFromDB(GetOwnerGUID(), trans);

        public static void DeleteFromDB(ObjectGuid ownerGuid, SQLTransaction trans)
        {
            PreparedStatement stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_CORPSE);
            stmt.AddValue(0, ownerGuid.GetCounter());
            DB.Characters.ExecuteOrAppend(trans, stmt);

            stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_CORPSE_PHASES);
            stmt.AddValue(0, ownerGuid.GetCounter());
            DB.Characters.ExecuteOrAppend(trans, stmt);

            stmt = DB.Characters.GetPreparedStatement(CharStatements.DEL_CORPSE_CUSTOMIZATIONS);
            stmt.AddValue(0, ownerGuid.GetCounter());
            DB.Characters.ExecuteOrAppend(trans, stmt);
        }

        public bool LoadCorpseFromDB(ulong guid, SQLFields field)
        {
            //        0     1     2     3            4      5          6          7     8      9       10     11        12    13          14          15
            // SELECT posX, posY, posZ, orientation, mapId, displayId, itemCache, race, class, gender, flags, dynFlags, time, corpseType, instanceId, guid FROM corpse WHERE mapId = ? AND instanceId = ?

            float posX = field.Read<float>(0);
            float posY = field.Read<float>(1);
            float posZ = field.Read<float>(2);
            float o = field.Read<float>(3);
            ushort mapId = field.Read<ushort>(4);

            _Create(ObjectGuid.Create(HighGuid.Corpse, mapId, 0, guid));

            SetObjectScale(1.0f);
            SetDisplayId(field.Read<uint>(5));
            StringArray items = new(field.Read<string>(6), ' ');
            for (int index = 0; index < EquipmentSlot.End; ++index)
                SetItem(index, uint.Parse(items[(int)index]));

            SetRace((Race)field.Read<byte>(7));
            SetClass((Class)field.Read<byte>(8));
            SetSex((Gender)field.Read<byte>(9));
            SetFlags((CorpseFlags)field.Read<byte>(10));
            SetCorpseDynamicFlags((CorpseDynFlags)field.Read<byte>(11));
            SetOwnerGUID(ObjectGuid.Create(HighGuid.Player, field.Read<ulong>(15)));
            SetFactionTemplate(CliDB.ChrRacesStorage.LookupByKey(GetRace()).FactionID);

            m_time = field.Read<uint>(12);

            uint instanceId = field.Read<uint>(14);

            // place
            SetLocationInstanceId(instanceId);
            SetMapId(mapId);
            Relocate(posX, posY, posZ, o);

            if (!IsPositionValid())
            {
                Log.outError(LogFilter.Player, "Corpse ({0}, owner: {1}) is not created, given coordinates are not valid (X: {2}, Y: {3}, Z: {4})",
                    GetGUID().ToString(), GetOwnerGUID().ToString(), posX, posY, posZ);
                return false;
            }

            _cellCoord = GridDefines.ComputeCellCoord(GetPositionX(), GetPositionY());
            return true;
        }

        public bool IsExpired(long t)
        {
            // Deleted character
            if (!Global.CharacterCacheStorage.HasCharacterCacheEntry(GetOwnerGUID()))
                return true;

            if (m_type == CorpseType.Bones)
                return m_time < t - 60 * Time.Minute;
            else
                return m_time < t - 3 * Time.Day;
        }

        public void AddCorpseDynamicFlag(CorpseDynFlags dynamicFlags) => AddFlag(CorpseFields.DynamicFlags, dynamicFlags);
        public void RemoveCorpseDynamicFlag(CorpseDynFlags dynamicFlags) => RemoveFlag(CorpseFields.DynamicFlags, dynamicFlags);
        public void SetCorpseDynamicFlags(CorpseDynFlags dynamicFlags) => SetUpdateField<uint>(CorpseFields.DynamicFlags, (uint)dynamicFlags);

        public ObjectGuid GetOwnerGUID() => GetUpdateField<ObjectGuid>(CorpseFields.Owner);
        public void SetOwnerGUID(ObjectGuid owner) => SetUpdateField<ObjectGuid>(CorpseFields.Owner, owner);

        public ObjectGuid GetPartyGUID() => GetUpdateField<ObjectGuid>(CorpseFields.PartyGUID);
        public void SetPartyGUID(ObjectGuid partyGuid) => SetUpdateField<ObjectGuid>(CorpseFields.PartyGUID, partyGuid);

        public ObjectGuid GetGuildGUID() => GetUpdateField<ObjectGuid>(CorpseFields.GuildGUID);
        public void SetGuildGUID(ObjectGuid guildGuid) => SetUpdateField<ObjectGuid>(CorpseFields.GuildGUID, guildGuid);

        public uint GetDisplayId() => GetUpdateField<uint>(CorpseFields.DisplayID);
        public void SetDisplayId(uint displayId) => SetUpdateField<uint>(CorpseFields.DisplayID, displayId);

        public Race GetRace() => (Race)GetUpdateField<byte>(CorpseFields.Bytes1);
        public void SetRace(Race race) => SetUpdateField<byte>(CorpseFields.Bytes1, (byte)race, 0);

        public Gender GetSex() => (Gender)GetUpdateField<byte>(CorpseFields.Bytes1, 1);
        public void SetSex(Gender sex) => SetUpdateField<byte>(CorpseFields.Bytes1, (byte)sex, 1);

        public Class GetClass() => (Class)GetUpdateField<byte>(CorpseFields.Bytes1, 2);
        public void SetClass(Class classId) => SetUpdateField<byte>(CorpseFields.Bytes1, (byte)classId, 2);

        public void SetFlags(CorpseFlags flags) => SetUpdateField<uint>(CorpseFields.Flags, (uint)flags);
        public void SetFactionTemplate(int factionTemplate) => SetUpdateField<int>(CorpseFields.FactionTemplate, factionTemplate);
        public void SetItem(int slot, uint item) => SetUpdateField<uint>(CorpseFields.Items + slot, item);

        public uint GetCustomizationChoiceId(uint chrCustomizationOptionId)
        {
            for (var i = 0; i < PlayerConst.MaxChrCustomizationChoices * 2; i += 2)
            {
                if (GetUpdateField<uint>(CorpseFields.CustomizationChoices + i) == chrCustomizationOptionId)
                    return GetUpdateField<uint>(CorpseFields.CustomizationChoices + i + 1);
            }

            return 0;
        }

        public ChrCustomizationChoice? GetCustomizationChoice(uint chrCustomizationOptionId)
        {
            for (var i = 0; i < PlayerConst.MaxChrCustomizationChoices * 2; i += 2)
            {
                if (GetUpdateField<uint>(CorpseFields.CustomizationChoices + i) == chrCustomizationOptionId)
                {
                    return new ChrCustomizationChoice
                    {
                        ChrCustomizationOptionID = GetUpdateField<uint>(CorpseFields.CustomizationChoices + i),
                        ChrCustomizationChoiceID = GetUpdateField<uint>(CorpseFields.CustomizationChoices + i + 1)
                    };
                }
            }

            return null;
        }

        public List<ChrCustomizationChoice> GetCustomizationChoices()
        {
            var characterCustomizations = new List<ChrCustomizationChoice>();
            for (var i = 0; i < PlayerConst.MaxChrCustomizationChoices; i += 2)
            {
                var optionId = GetUpdateField<uint>(CorpseFields.CustomizationChoices + i);
                if (optionId == 0)
                    continue;

                var choiceId = GetUpdateField<uint>(CorpseFields.CustomizationChoices + i + 1);
                characterCustomizations.Add(new()
                {
                    ChrCustomizationOptionID = optionId,
                    ChrCustomizationChoiceID = choiceId,
                });
            }

            return characterCustomizations;
        }

        void ClearCustomizations()
        {
            for (var i = 0; i < PlayerConst.MaxChrCustomizationChoices * 2; ++i)
                SetUpdateField<uint>(CorpseFields.CustomizationChoices + i, 0);
        }

        public void SetCustomizations(List<ChrCustomizationChoice> customizations)
        {
            ClearCustomizations();

            var index = 0;
            foreach (var customization in customizations)
            {
                SetUpdateField<uint>(CorpseFields.CustomizationChoices + index++, customization.ChrCustomizationOptionID);
                SetUpdateField<uint>(CorpseFields.CustomizationChoices + index++, customization.ChrCustomizationChoiceID);
            }
        }

        public long GetGhostTime() => m_time;
        public void ResetGhostTime() => m_time = GameTime.GetGameTime();
        public CorpseType GetCorpseType() => m_type;

        public CellCoord GetCellCoord() => _cellCoord;
        public void SetCellCoord(CellCoord cellCoord) => _cellCoord = cellCoord;

        public Loot loot = new();
        public Player lootRecipient;

        CorpseType m_type;
        long m_time;
        CellCoord _cellCoord;                                    // gride for corpse position for fast search
    }
}
