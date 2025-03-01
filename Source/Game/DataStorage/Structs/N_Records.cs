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

namespace Game.DataStorage
{
    public sealed class NameGenRecord
    {
        public uint Id;
        public string Name;
        public byte RaceID;
        public byte Sex;
    }

    public sealed class NamesProfanityRecord
    {
        public uint Id;
        public string Name;
        public sbyte Language;
    }

    public sealed class NamesReservedRecord
    {
        public uint Id;
        public string Name;
    }

    public sealed class NamesReservedLocaleRecord
    {
        public uint Id;
        public string Name;
        public byte LocaleMask;
    }

    public sealed class NumTalentsAtLevelRecord
    {
        public uint Id;
        public uint NumTalents;
        public uint NumTalentsDeathKnight;
        public uint NumTalentsDemonHunter;
    }
}
