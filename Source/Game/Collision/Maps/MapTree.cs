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
using System.Collections.Generic;
using System.IO;

namespace Game.Collision
{
    public class LocationInfo
    {
        public int RootId;
        public ModelInstance HitInstance;
        public GroupModel HitModel;
        public float GroundZ = float.NegativeInfinity;
    }

    public class AreaInfo
    {
        public bool Result;
        public float GroundZ = float.NegativeInfinity;
        public uint Flags;
        public int AdtId;
        public int RootId;
        public int GroupId;
    }

    public class StaticMapTree
    {
        uint mapId;
        BIH tree = new();
        ModelInstance[] treeValues;
        uint treeValueCount;

        Dictionary<uint, uint> spawnIndices = new();
        Dictionary<uint, bool> loadedTiles = new();
        Dictionary<uint, uint> loadedSpawns = new();

        public StaticMapTree(uint mapId)
        {
            this.mapId = mapId;
        }

        public LoadResult InitMap(string mapTreeName)
        {
            Log.outDebug(LogFilter.Maps, $"StaticMapTree.InitMap() : Initializing StaticMapTree '{mapTreeName}'");
            if (!File.Exists(mapTreeName))
                return LoadResult.FileNotFound;

            using (BinaryReader reader = new(new FileStream(mapTreeName, FileMode.Open, FileAccess.Read)))
            {
                var magic = reader.ReadStringFromChars(8);
                if (magic != MapConst.VMapMagic)
                    return LoadResult.VersionMismatch;

                var node = reader.ReadStringFromChars(4);
                if (node != "NODE")
                    return LoadResult.ReadFromFileFailed;

                if (!tree.ReadFromFile(reader))
                    return LoadResult.ReadFromFileFailed;

                treeValueCount = tree.PrimCount();
                treeValues = new ModelInstance[treeValueCount];

                if (reader.ReadStringFromChars(4) != "SIDX")
                    return LoadResult.ReadFromFileFailed;

                uint spawnIndicesSize = reader.ReadUInt32();
                for (uint i = 0; i < spawnIndicesSize; ++i)
                {
                    uint spawnId = reader.ReadUInt32();
                    spawnIndices[spawnId] = i;
                }
            }

            return LoadResult.Success;
        }

        public void UnloadMap(VMapManager vm)
        {
            foreach (var id in loadedSpawns)
            {
                for (uint refCount = 0; refCount < id.Key; ++refCount)
                    vm.ReleaseModelInstance(treeValues[id.Key].Name);

                treeValues[id.Key].SetUnloaded();
            }
            loadedSpawns.Clear();
            loadedTiles.Clear();
        }

        public LoadResult LoadMapTile(uint tileX, uint tileY, VMapManager vm)
        {
            if (treeValues == null)
            {
                Log.outError(LogFilter.Server, "StaticMapTree.LoadMapTile() : tree has not been initialized [{0}, {1}]", tileX, tileY);
                return LoadResult.ReadFromFileFailed;
            }

            LoadResult result = LoadResult.FileNotFound;

            TileFileOpenResult fileResult = OpenMapTileFile(VMapManager.VMapPath, mapId, tileX, tileY, vm);
            if (fileResult.File != null)
            {
                result = LoadResult.Success;
                using BinaryReader reader = new(fileResult.File);
                if (reader.ReadStringFromChars(8) != MapConst.VMapMagic)
                    result = LoadResult.VersionMismatch;

                if (result == LoadResult.Success)
                {
                    uint numSpawns = reader.ReadUInt32();
                    for (uint i = 0; i < numSpawns && result == LoadResult.Success; ++i)
                    {
                        // read model spawns
                        if (ModelSpawn.ReadFromFile(reader, out ModelSpawn spawn))
                        {
                            // acquire model instance
                            WorldModel model = vm.AcquireModelInstance(spawn.Name, spawn.Flags);
                            if (model == null)
                                Log.outError(LogFilter.Server, "StaticMapTree.LoadMapTile() : could not acquire WorldModel [{0}, {1}]", tileX, tileY);

                            // update tree
                            if (spawnIndices.ContainsKey(spawn.Id))
                            {
                                uint referencedVal = spawnIndices[spawn.Id];
                                if (!loadedSpawns.ContainsKey(referencedVal))
                                {
                                    if (referencedVal >= treeValueCount)
                                    {
                                        Log.outError(LogFilter.Maps, "StaticMapTree.LoadMapTile() : invalid tree element ({0}/{1}) referenced in tile {2}", referencedVal, treeValueCount, fileResult.Name);
                                        continue;
                                    }

                                    treeValues[referencedVal] = new ModelInstance(spawn, model);
                                    loadedSpawns[referencedVal] = 1;
                                }
                                else
                                    ++loadedSpawns[referencedVal];
                            }
                            else if (mapId == fileResult.UsedMapId)
                            {
                                // unknown parent spawn might appear in because it overlaps multiple tiles
                                // in case the original tile is swapped but its neighbour is now (adding this spawn)
                                // we want to not mark it as loading error and just skip that model
                                Log.outError(LogFilter.Maps, $"StaticMapTree.LoadMapTile() : invalid tree element (spawn {spawn.Id}) referenced in tile fileResult.Name{fileResult.Name} by map {mapId}");
                                result = LoadResult.ReadFromFileFailed;
                            }
                        }
                        else
                        {
                            Log.outError(LogFilter.Maps, $"StaticMapTree.LoadMapTile() : cannot read model from file (spawn index {i}) referenced in tile {fileResult.Name} by map {mapId}");
                            result = LoadResult.ReadFromFileFailed;
                        }
                    }
                }
                loadedTiles[PackTileID(tileX, tileY)] = true;
            }
            else
            {
                loadedTiles[PackTileID(tileX, tileY)] = false;
            }

            return result;
        }

        public void UnloadMapTile(uint tileX, uint tileY, VMapManager vm)
        {
            uint tileID = PackTileID(tileX, tileY);
            if (!loadedTiles.ContainsKey(tileID))
            {
                Log.outError(LogFilter.Server, "StaticMapTree.UnloadMapTile() : trying to unload non-loaded tile - Map:{0} X:{1} Y:{2}", mapId, tileX, tileY);
                return;
            }
            if (loadedTiles[tileID]) // file associated with tile
            {
                TileFileOpenResult fileResult = OpenMapTileFile(VMapManager.VMapPath, mapId, tileX, tileY, vm);
                if (fileResult.File != null)
                {
                    using BinaryReader reader = new(fileResult.File);
                    bool result = true;
                    if (reader.ReadStringFromChars(8) != MapConst.VMapMagic)
                        result = false;

                    uint numSpawns = reader.ReadUInt32();
                    for (uint i = 0; i < numSpawns && result; ++i)
                    {
                        // read model spawns
                        result = ModelSpawn.ReadFromFile(reader, out ModelSpawn spawn);
                        if (result)
                        {
                            // release model instance
                            vm.ReleaseModelInstance(spawn.Name);

                            // update tree
                            if (spawnIndices.ContainsKey(spawn.Id))
                            {
                                uint referencedNode = spawnIndices[spawn.Id];
                                if (!loadedSpawns.ContainsKey(referencedNode))
                                    Log.outError(LogFilter.Server, "StaticMapTree.UnloadMapTile() : trying to unload non-referenced model '{0}' (ID:{1})", spawn.Name, spawn.Id);
                                else if (--loadedSpawns[referencedNode] == 0)
                                {
                                    treeValues[referencedNode].SetUnloaded();
                                    loadedSpawns.Remove(referencedNode);
                                }
                            }
                            else if (mapId == fileResult.UsedMapId) // logic documented in StaticMapTree::LoadMapTile
                                result = false;
                        }
                    }
                }
            }
            loadedTiles.Remove(tileID);
        }
        
        static uint PackTileID(uint tileX, uint tileY) { return tileX << 16 | tileY; }
        static void UnpackTileID(uint ID, ref uint tileX, ref uint tileY) { tileX = ID >> 16; tileY = ID & 0xFF; }

        static TileFileOpenResult OpenMapTileFile(string vmapPath, uint mapID, uint tileX, uint tileY, VMapManager vm)
        {
            TileFileOpenResult result = new();
            result.Name = vmapPath + GetTileFileName(mapID, tileX, tileY);

            if (File.Exists(result.Name))
            {
                result.UsedMapId = mapID;
                result.File = new FileStream(result.Name, FileMode.Open, FileAccess.Read);
                return result;
            }

            int parentMapId = vm.GetParentMapId(mapID);
            while (parentMapId != -1)
            {
                result.Name = vmapPath + GetTileFileName((uint)parentMapId, tileX, tileY);
                if (File.Exists(result.Name))
                {
                    result.File = new FileStream(result.Name, FileMode.Open, FileAccess.Read);
                    result.UsedMapId = (uint)parentMapId;
                    return result;
                }

                parentMapId = vm.GetParentMapId((uint)parentMapId);
            }

            return result;
        }

        public static LoadResult CanLoadMap(string vmapPath, uint mapID, uint tileX, uint tileY, VMapManager vm)
        {
            string fullname = vmapPath + VMapManager.GetMapFileName(mapID);
            if (!File.Exists(fullname))
                return LoadResult.FileNotFound;

            using (BinaryReader reader = new(new FileStream(fullname, FileMode.Open, FileAccess.Read)))
            {
                if (reader.ReadStringFromChars(8) != MapConst.VMapMagic)
                    return LoadResult.VersionMismatch;
            }

            FileStream stream = OpenMapTileFile(vmapPath, mapID, tileX, tileY, vm).File;
            if (stream == null)
                return LoadResult.FileNotFound;

            using (BinaryReader reader = new(stream))
            {
                if (reader.ReadStringFromChars(8) != MapConst.VMapMagic)
                    return LoadResult.VersionMismatch;
            }

            return LoadResult.Success;
        }

        public static string GetTileFileName(uint mapID, uint tileX, uint tileY)
        {
            return $"{mapID:D4}_{tileY:D2}_{tileX:D2}.vmtile";
        }

        public bool GetAreaInfo(ref Vector3 pos, out uint flags, out int adtId, out int rootId, out int groupId)
        {
            flags = 0;
            adtId = 0;
            rootId = 0;
            groupId = 0;

            AreaInfoCallback intersectionCallBack = new(treeValues);
            tree.IntersectPoint(pos, intersectionCallBack);
            if (intersectionCallBack.aInfo.Result)
            {
                flags = intersectionCallBack.aInfo.Flags;
                adtId = intersectionCallBack.aInfo.AdtId;
                rootId = intersectionCallBack.aInfo.RootId;
                groupId = intersectionCallBack.aInfo.GroupId;
                pos.Z = intersectionCallBack.aInfo.GroundZ;
                return true;
            }
            return false;
        }

        public bool GetLocationInfo(Vector3 pos, LocationInfo info)
        {
            LocationInfoCallback intersectionCallBack = new(treeValues, info);
            tree.IntersectPoint(pos, intersectionCallBack);
            return intersectionCallBack.result;
        }

        public float GetHeight(Vector3 pPos, float maxSearchDist)
        {
            float height = float.PositiveInfinity;
            Vector3 dir = new(0, 0, -1);
            Ray ray = new(pPos, dir);   // direction with length of 1
            float maxDist = maxSearchDist;
            if (GetIntersectionTime(ray, ref maxDist, false, ModelIgnoreFlags.Nothing))
                height = pPos.Z - maxDist;

            return height;
        }
        bool GetIntersectionTime(Ray pRay, ref float pMaxDist, bool pStopAtFirstHit, ModelIgnoreFlags ignoreFlags)
        {
            float distance = pMaxDist;
            MapRayCallback intersectionCallBack = new(treeValues, ignoreFlags);
            tree.IntersectRay(pRay, intersectionCallBack, ref distance, pStopAtFirstHit);
            if (intersectionCallBack.DidHit())
                pMaxDist = distance;
            return intersectionCallBack.DidHit();
        }

        public bool GetObjectHitPos(Vector3 pPos1, Vector3 pPos2, out Vector3 pResultHitPos, float pModifyDist)
        {
            bool result;
            float maxDist = (pPos2 - pPos1).magnitude();
            // valid map coords should *never ever* produce float overflow, but this would produce NaNs too
            Cypher.Assert(maxDist < float.MaxValue);
            // prevent NaN values which can cause BIH intersection to enter infinite loop
            if (maxDist < 1e-10f)
            {
                pResultHitPos = pPos2;
                return false;
            }
            Vector3 dir = (pPos2 - pPos1) / maxDist;              // direction with length of 1
            Ray ray = new(pPos1, dir);
            float dist = maxDist;
            if (GetIntersectionTime(ray, ref dist, false, ModelIgnoreFlags.Nothing))
            {
                pResultHitPos = pPos1 + dir * dist;
                if (pModifyDist < 0)
                {
                    if ((pResultHitPos - pPos1).magnitude() > -pModifyDist)
                    {
                        pResultHitPos += dir * pModifyDist;
                    }
                    else
                    {
                        pResultHitPos = pPos1;
                    }
                }
                else
                {
                    pResultHitPos += dir * pModifyDist;
                }
                result = true;
            }
            else
            {
                pResultHitPos = pPos2;
                result = false;
            }
            return result;
        }

        public bool IsInLineOfSight(Vector3 pos1, Vector3 pos2, ModelIgnoreFlags ignoreFlags)
        {
            float maxDist = (pos2 - pos1).magnitude();
            // return false if distance is over max float, in case of cheater teleporting to the end of the universe
            if (maxDist == float.MaxValue ||
                maxDist == float.PositiveInfinity)
                return false;

            // valid map coords should *never ever* produce float overflow, but this would produce NaNs too
            Cypher.Assert(maxDist < float.MaxValue);
            // prevent NaN values which can cause BIH intersection to enter infinite loop
            if (maxDist < 1e-10f)
                return true;
            // direction with length of 1
            Ray ray = new(pos1, (pos2 - pos1) / maxDist);
            if (GetIntersectionTime(ray, ref maxDist, true, ignoreFlags))
                return false;

            return true;
        }

        public int NumLoadedTiles() { return loadedTiles.Count; }
    }

    class TileFileOpenResult
    {
        public string Name;
        public FileStream File;
        public uint UsedMapId;
    }
}
