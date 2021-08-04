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

using Framework.GameMath;
using System;
using System.Collections.Generic;
using System.IO;
using Framework.Constants;

namespace Game.Collision
{
    public class StaticModelList
    {
        public static Dictionary<uint, GameobjectModelData> models = new();
    }

    public abstract class GameObjectModelOwnerBase
    {
        public abstract bool IsSpawned();
        public abstract uint GetDisplayId();
        public abstract byte GetNameSetId();
        public abstract bool IsInPhase(PhaseShift phaseShift);
        public abstract Vector3 GetPosition();
        public abstract float GetOrientation();
        public abstract float GetScale();
    }

    public class GameObjectModel : IModel
    {
        bool collisionEnabled;
        AxisAlignedBox boundingBox;
        Matrix3 invRotation;
        Vector3 position;
        float invScale;
        float scale;
        WorldModel model;
        GameObjectModelOwnerBase owner;
        bool isWmo;

        bool Initialize(GameObjectModelOwnerBase modelOwner)
        {
            var modelData = StaticModelList.models.LookupByKey(modelOwner.GetDisplayId());
            if (modelData == null)
                return false;

            AxisAlignedBox mdl_box = new(modelData.bound);
            // ignore models with no bounds
            if (mdl_box == AxisAlignedBox.Zero())
            {
                Log.outError(LogFilter.Server, "GameObject model {0} has zero bounds, loading skipped", modelData.name);
                return false;
            }

            model = Global.VMapMgr.AcquireModelInstance(modelData.name);
            if (model == null)
                return false;

            position = modelOwner.GetPosition();
            scale = modelOwner.GetScale();
            invScale = 1.0f / scale;

            Matrix3 iRotation = Matrix3.fromEulerAnglesZYX(modelOwner.GetOrientation(), 0, 0);
            invRotation = iRotation.inverse();
            // transform bounding box:
            mdl_box = new AxisAlignedBox(mdl_box.Lo * scale, mdl_box.Hi * scale);
            AxisAlignedBox rotated_bounds = new();
            for (int i = 0; i < 8; ++i)
                rotated_bounds.merge(iRotation * mdl_box.corner(i));

            boundingBox = rotated_bounds + position;
            owner = modelOwner;
            isWmo = modelData.isWmo;
            return true;
        }

        public static GameObjectModel Create(GameObjectModelOwnerBase modelOwner)
        {
            GameObjectModel mdl = new();
            if (!mdl.Initialize(modelOwner))
                return null;

            return mdl;
        }

        public override bool IntersectRay(Ray ray, ref float maxDist, bool stopAtFirstHit, PhaseShift phaseShift, ModelIgnoreFlags ignoreFlags)
        {
            if (!IsCollisionEnabled() || !owner.IsSpawned())
                return false;

            if (!owner.IsInPhase(phaseShift))
                return false;

            float time = ray.intersectionTime(boundingBox);
            if (time == float.PositiveInfinity)
                return false;

            // child bounds are defined in object space:
            Vector3 p = invRotation * (ray.Origin - position) * invScale;
            Ray modRay = new(p, invRotation * ray.Direction);
            float distance = maxDist * invScale;
            bool hit = model.IntersectRay(modRay, ref distance, stopAtFirstHit, ignoreFlags);
            if (hit)
            {
                distance *= scale;
                maxDist = distance;
            }
            return hit;
        }

        public override void IntersectPoint(Vector3 point, AreaInfo info, PhaseShift phaseShift)
        {
            if (!IsCollisionEnabled() || !owner.IsSpawned() || !IsMapObject())
                return;

            if (!owner.IsInPhase(phaseShift))
                return;

            if (!boundingBox.contains(point))
                return;

            // child bounds are defined in object space:
            Vector3 pModel = invRotation * (point - position) * invScale;
            Vector3 zDirModel = invRotation * new Vector3(0.0f, 0.0f, -1.0f);
            if (model.IntersectPoint(pModel, zDirModel, out float zDist, info))
            {
                Vector3 modelGround = pModel + zDist * zDirModel;
                float world_Z = ((modelGround * invRotation) * scale + position).Z;
                if (info.GroundZ < world_Z)
                {
                    info.GroundZ = world_Z;
                    info.AdtId = owner.GetNameSetId();
                }
            }
        }

        public bool UpdatePosition()
        {
            if (model == null)
                return false;

            var it = StaticModelList.models.LookupByKey(owner.GetDisplayId());
            if (it == null)
                return false;

            AxisAlignedBox mdl_box = new(it.bound);
            // ignore models with no bounds
            if (mdl_box == AxisAlignedBox.Zero())
            {
                Log.outError(LogFilter.Server, "GameObject model {0} has zero bounds, loading skipped", it.name);
                return false;
            }

            position = owner.GetPosition();

            Matrix3 iRotation = Matrix3.fromEulerAnglesZYX(owner.GetOrientation(), 0, 0);
            invRotation = iRotation.inverse();
            // transform bounding box:
            mdl_box = new AxisAlignedBox(mdl_box.Lo * scale, mdl_box.Hi * scale);
            AxisAlignedBox rotated_bounds = new();
            for (int i = 0; i < 8; ++i)
                rotated_bounds.merge(iRotation * mdl_box.corner(i));

            boundingBox = rotated_bounds + position;

            return true;
        }

        public override Vector3 GetPosition() { return position; }
        public override AxisAlignedBox GetBounds() { return boundingBox; }

        public void EnableCollision(bool enable) { collisionEnabled = enable; }
        bool IsCollisionEnabled() { return collisionEnabled; }
        public bool IsMapObject() { return isWmo; }

        public static void LoadGameObjectModelList()
        {
            uint oldMSTime = Time.GetMSTime();
            var filename = Global.WorldMgr.GetDataPath() + "/vmaps/GameObjectModels.dtree";
            if (!File.Exists(filename))
            {
                Log.outWarn(LogFilter.Server, "Unable to open '{0}' file.", filename);
                return;
            }
            try
            {
                using BinaryReader reader = new(new FileStream(filename, FileMode.Open, FileAccess.Read));
                string magic = reader.ReadStringFromChars(8);
                if (magic != MapConst.VMapMagic)
                {
                    Log.outError(LogFilter.Misc, $"File '{filename}' has wrong header, expected {MapConst.VMapMagic}.");
                    return;
                }

                long length = reader.BaseStream.Length;
                while (true)
                {
                    if (reader.BaseStream.Position >= length)
                        break;

                    uint displayId = reader.ReadUInt32();
                    bool isWmo = reader.ReadBoolean();
                    int nameLength = reader.ReadInt32();
                    string name = reader.ReadString(nameLength);
                    Vector3 v1 = reader.Read<Vector3>();
                    Vector3 v2 = reader.Read<Vector3>();

                    StaticModelList.models.Add(displayId, new GameobjectModelData(name, v1, v2, isWmo));
                }
            }
            catch (EndOfStreamException ex)
            {
                Log.outException(ex);
            }

            Log.outInfo(LogFilter.ServerLoading, "Loaded {0} GameObject models in {1} ms", StaticModelList.models.Count, Time.GetMSTimeDiffToNow(oldMSTime));
        }
    }
    public class GameobjectModelData
    {
        public GameobjectModelData(string name_, Vector3 lowBound, Vector3 highBound, bool isWmo_)
        {
            bound = new AxisAlignedBox(lowBound, highBound);
            name = name_;
            isWmo = isWmo_;
        }

        public AxisAlignedBox bound;
        public string name;
        public bool isWmo;
    }
}
    
