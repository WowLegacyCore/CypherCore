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
using Framework.GameMath;
using System;
using System.IO;

namespace Game.Collision
{
    public enum ModelFlags
    {
        M2 = 1,
        HasBound = 1 << 1,
        ParentSpawn = 1 << 2
    }

    public class ModelMinimalData
    {
        public byte Flags;
        public byte AdtId;
        public uint Id;
        public Vector3 Position;
        public float Scale;
        public AxisAlignedBox BoundingBox;
        public string Name;
    }

    public class ModelSpawn : ModelMinimalData
    {
        public Vector3 Rotation;

        public ModelSpawn() { }

        public ModelSpawn(ModelSpawn spawn)
        {
            Flags = spawn.Flags;
            AdtId = spawn.AdtId;
            Id = spawn.Id;
            Position = spawn.Position;
            Rotation = spawn.Rotation;
            Scale = spawn.Scale;
            BoundingBox = spawn.BoundingBox;
            Name = spawn.Name;
        }

        public static bool ReadFromFile(BinaryReader reader, out ModelSpawn spawn)
        {
            spawn = new ModelSpawn
            {
                Flags       = (byte)reader.ReadInt32(),
                AdtId       = (byte)reader.ReadUInt16(),
                Id          = reader.ReadUInt32(),
                Position    = reader.Read<Vector3>(),
                Rotation    = reader.Read<Vector3>(),
                Scale       = reader.ReadSingle()
            };

            if ((spawn.Flags & (uint)ModelFlags.HasBound) != 0) // only WMOs have bound in MPQ, only available after computation
            {
                Vector3 bLow = reader.Read<Vector3>();
                Vector3 bHigh = reader.Read<Vector3>();
                spawn.BoundingBox = new AxisAlignedBox(bLow, bHigh);
            }

            uint nameLen = reader.ReadUInt32();
            spawn.Name = reader.ReadString((int)nameLen);
            return true;
        }
    }

    public class ModelInstance : ModelMinimalData
    {
        Matrix3 invRotation;
        float invScale;
        WorldModel model;

        public ModelInstance()
        {
            invScale = 0.0f;
            model = null;
        }

        public ModelInstance(ModelSpawn spawn, WorldModel model)
        {
            Flags = spawn.Flags;
            AdtId = spawn.AdtId;
            Id = spawn.Id;
            Position = spawn.Position;
            Scale = spawn.Scale;
            BoundingBox = spawn.BoundingBox;
            Name = spawn.Name;

            this.model = model;

            invRotation = Matrix3.fromEulerAnglesZYX(MathFunctions.PI * spawn.Rotation.Y / 180.0f, MathFunctions.PI * spawn.Rotation.X / 180.0f, MathFunctions.PI * spawn.Rotation.Z / 180.0f).inverse();
            invScale = 1.0f / Scale;
        }

        public bool IntersectRay(Ray pRay, ref float pMaxDist, bool pStopAtFirstHit, ModelIgnoreFlags ignoreFlags)
        {
            if (model == null)
                return false;

            float time = pRay.intersectionTime(BoundingBox);
            if (float.IsInfinity(time))
                return false;

            // child bounds are defined in object space:
            Vector3 p = invRotation * (pRay.Origin - Position) * invScale;
            Ray modRay = new(p, invRotation * pRay.Direction);
            float distance = pMaxDist * invScale;
            bool hit = model.IntersectRay(modRay, ref distance, pStopAtFirstHit, ignoreFlags);
            if (hit)
            {
                distance *= Scale;
                pMaxDist = distance;
            }
            return hit;
        }

        public void IntersectPoint(Vector3 p, AreaInfo info)
        {
            if (model == null)
                return;

            // M2 files don't contain area info, only WMO files
            if (Convert.ToBoolean(Flags & (uint)ModelFlags.M2))
                return;

            if (!BoundingBox.contains(p))
                return;

            // child bounds are defined in object space:
            Vector3 pModel = invRotation * (p - Position) * invScale;
            Vector3 zDirModel = invRotation * new Vector3(0.0f, 0.0f, -1.0f);
            if (model.IntersectPoint(pModel, zDirModel, out float zDist, info))
            {
                Vector3 modelGround = pModel + zDist * zDirModel;
                // Transform back to world space. Note that:
                // Mat * vec == vec * Mat.transpose()
                // and for rotation matrices: Mat.inverse() == Mat.transpose()
                float worldZ = ((modelGround * invRotation) * Scale + Position).Z;
                if (info.GroundZ < worldZ)
                {
                    info.GroundZ = worldZ;
                    info.AdtId = AdtId;
                }
            }
        }

        public bool GetLiquidLevel(Vector3 p, LocationInfo info, ref float liqHeight)
        {
            // child bounds are defined in object space:
            Vector3 pModel = invRotation * (p - Position) * invScale;
            //Vector3 zDirModel = iInvRot * Vector3(0.f, 0.f, -1.f);
            if (info.HitModel.GetLiquidLevel(pModel, out float zDist))
            {
                // calculate world height (zDist in model coords):
                // assume WMO not tilted (wouldn't make much sense anyway)
                liqHeight = zDist * Scale + Position.Z;
                return true;
            }
            return false;
        }

        public bool GetLocationInfo(Vector3 p, LocationInfo info)
        {
            if (model == null)
                return false;

            // M2 files don't contain area info, only WMO files
            if (Convert.ToBoolean(Flags & (uint)ModelFlags.M2))
                return false;
            if (!BoundingBox.contains(p))
                return false;
            // child bounds are defined in object space:
            Vector3 pModel = invRotation * (p - Position) * invScale;
            Vector3 zDirModel = invRotation * new Vector3(0.0f, 0.0f, -1.0f);
            if (model.GetLocationInfo(pModel, zDirModel, out float zDist, info))
            {
                Vector3 modelGround = pModel + zDist * zDirModel;
                // Transform back to world space. Note that:
                // Mat * vec == vec * Mat.transpose()
                // and for rotation matrices: Mat.inverse() == Mat.transpose()
                float world_Z = ((modelGround * invRotation) * Scale + Position).Z;
                if (info.GroundZ < world_Z) // hm...could it be handled automatically with zDist at intersection?
                {
                    info.GroundZ = world_Z;
                    info.HitInstance = this;
                    return true;
                }
            }
            return false;
        }

        public void SetUnloaded() { model = null; }
    }
}
