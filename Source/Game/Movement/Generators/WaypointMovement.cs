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
using System.Collections.Generic;
using System.Linq;

namespace Game.Movement
{
    public class WaypointMovementGenerator : MovementGeneratorMedium<Creature>
    {
        public WaypointMovementGenerator(uint pathId = 0, bool repeating = true)
        {
            _nextMoveTime = new TimeTrackerSmall(0);
            _pathId = pathId;
            _repeating = repeating;
            _loadedFromDB = true;
        }

        public WaypointMovementGenerator(WaypointPath path, bool repeating = true)
        {
            _nextMoveTime = new TimeTrackerSmall(0);
            _repeating = repeating;            
            _path = path;
        }

        public override void DoInitialize(Creature creature)
        {
            _done = false;

            if (_loadedFromDB)
            {
                if (_pathId == 0)
                    _pathId = creature.GetWaypointPath();

                _path = Global.WaypointMgr.GetPath(_pathId);
            }

            if (_path == null)
            {
                // No path id found for entry
                Log.outError(LogFilter.Sql, $"WaypointMovementGenerator.DoInitialize: creature {creature.GetName()} ({creature.GetGUID()} DB GUID: {creature.GetSpawnId()}) doesn't have waypoint path id: {_pathId}");
                return;
            }

            _nextMoveTime.Reset(1000);

            // inform AI
            if (creature.IsAIEnabled)
                creature.GetAI().WaypointPathStarted(_path.id);
        }

        public override void DoFinalize(Creature creature)
        {
            creature.ClearUnitState(UnitState.Roaming | UnitState.RoamingMove);
            creature.SetWalk(false);
        }

        public override void DoReset(Creature creature)
        {
            if (!_done && _nextMoveTime.Passed() && CanMove(creature))
                StartMove(creature);
            else if (_done)
            {
                // mimic IdleMovementGenerator
                if (!creature.IsStopped())
                    creature.StopMoving();
            }
        }

        void OnArrived(Creature creature)
        {
            if (_path == null || _path.nodes.Empty())
                return;

            Cypher.Assert(_currentNode < _path.nodes.Count, $"WaypointMovementGenerator.OnArrived: tried to reference a node id ({_currentNode}) which is not included in path ({_path.id})");
            WaypointNode waypoint = _path.nodes.ElementAt((int)_currentNode);
            if (waypoint.delay != 0)
            {
                creature.ClearUnitState(UnitState.RoamingMove);
                _nextMoveTime.Reset((int)waypoint.delay);
            }

            if (waypoint.eventId != 0 && RandomHelper.URand(0, 99) < waypoint.eventChance)
            {
                Log.outDebug(LogFilter.MapsScript, $"Creature movement start script {waypoint.eventId} at point {_currentNode} for {creature.GetGUID()}.");
                creature.ClearUnitState(UnitState.RoamingMove);
                creature.GetMap().ScriptsStart(ScriptsType.Waypoint, waypoint.eventId, creature, null);
            }

            // inform AI
            if (creature.IsAIEnabled)
            {
                creature.GetAI().MovementInform(MovementGeneratorType.Waypoint, (uint)_currentNode);
                creature.GetAI().WaypointReached(waypoint.id, _path.id);
            }

            creature.UpdateCurrentWaypointInfo(waypoint.id, _path.id);
        }

        void StartMove(Creature creature, bool relaunch = false)
        {
            // sanity checks
            if (creature == null || !creature.IsAlive() || _done || _path == null || _path.nodes.Empty() || (relaunch && _isArrivalDone))
                return;

            if (!relaunch)  // on relaunch, can avoid this since its only called on valid movement
            {
                if (!CanMove(creature) || (creature.IsFormationLeader() && !creature.IsFormationLeaderMoveAllowed())) // if cannot move OR cannot move because of formation
                {
                    _nextMoveTime.Reset(1000); // delay 1s
                    return;
                }
            }

            bool transportPath = creature.GetTransport() != null;

            if (_isArrivalDone)
            {
                Cypher.Assert(_currentNode < _path.nodes.Count, $"WaypointMovementGenerator.StartMove: tried to reference a node id ({_currentNode}) which is not included in path ({_path.id})");
                WaypointNode lastWaypoint = _path.nodes.ElementAt(_currentNode);
                if ((_currentNode == _path.nodes.Count - 1) && !_repeating) // If that's our last waypoint
                {
                    float x = lastWaypoint.x;
                    float y = lastWaypoint.y;
                    float z = lastWaypoint.z;
                    float o = creature.GetOrientation();

                    if (!transportPath)
                        creature.SetHomePosition(x, y, z, o);
                    else
                    {
                        Transport trans = creature.GetTransport();
                        if (trans)
                        {
                            o -= trans.GetOrientation();
                            creature.SetTransportHomePosition(x, y, z, o);
                            trans.CalculatePassengerPosition(ref x, ref y, ref z, ref o);
                            creature.SetHomePosition(x, y, z, o);
                        }
                        else
                            transportPath = false;
                        // else if (vehicle) - this should never happen, vehicle offsets are const
                    }
                    _done = true;
                    creature.UpdateCurrentWaypointInfo(0, 0);

                    // inform AI
                    if (creature.IsAIEnabled)
                        creature.GetAI().WaypointPathEnded(lastWaypoint.id, _path.id);
                    return;
                }

                _currentNode = (_currentNode + 1) % _path.nodes.Count;

                // inform AI
                if (creature.IsAIEnabled)
                    creature.GetAI().WaypointStarted(lastWaypoint.id, _path.id);
            }

            Cypher.Assert(_currentNode < _path.nodes.Count, $"WaypointMovementGenerator.StartMove: tried to reference a node id ({_currentNode}) which is not included in path ({_path.id})");
            WaypointNode waypoint = _path.nodes[_currentNode];
            Position formationDest = new(waypoint.x, waypoint.y, waypoint.z, (waypoint.orientation != 0 && waypoint.delay != 0) ? waypoint.orientation : 0.0f);

            _isArrivalDone = false;
            _recalculateSpeed = false;

            creature.AddUnitState(UnitState.RoamingMove);

            MoveSplineInit init = new(creature);

            //! If creature is on transport, we assume waypoints set in DB are already transport offsets
            if (transportPath)
            {
                init.DisableTransportPathTransformations();
                ITransport trans = creature.GetDirectTransport();
                if (trans != null)
                {
                    float orientation = formationDest.GetOrientation();
                    trans.CalculatePassengerPosition(ref formationDest.posX, ref formationDest.posY, ref formationDest.posZ, ref orientation);
                    formationDest.SetOrientation(orientation);
                }
            }

            //! Do not use formationDest here, MoveTo requires transport offsets due to DisableTransportPathTransformations() call
            //! but formationDest contains global coordinates
            init.MoveTo(waypoint.x, waypoint.y, waypoint.z);

            //! Accepts angles such as 0.00001 and -0.00001, 0 must be ignored, default value in waypoint table
            if (waypoint.orientation != 0 && waypoint.delay != 0)
                init.SetFacing(waypoint.orientation);

            switch (waypoint.moveType)
            {
                case WaypointMoveType.Land:
                    init.SetAnimation(AnimType.ToGround);
                    break;
                case WaypointMoveType.Takeoff:
                    init.SetAnimation(AnimType.ToFly);
                    break;
                case WaypointMoveType.Run:
                    init.SetWalk(false);
                    break;
                case WaypointMoveType.Walk:
                    init.SetWalk(true);
                    break;
            }

            init.Launch();

            // inform formation
            creature.SignalFormationMovement(formationDest, waypoint.id, waypoint.moveType, (waypoint.orientation != 0 && waypoint.delay != 0));
        }

        public override bool DoUpdate(Creature creature, uint diff)
        {
            if (!creature || !creature.IsAlive())
                return true;

            if (_done || _path == null || _path.nodes.Empty())
                return true;

            if (_stalled || creature.HasUnitState(UnitState.NotMove) || creature.IsMovementPreventedByCasting())
            {
                creature.StopMoving();
                return true;
            }

            // if it's moving
            if (!creature.MoveSpline.Finalized())
            {
                // set home position at place (every MotionMaster::UpdateMotion)
                if (creature.GetTransGUID().IsEmpty())
                    creature.SetHomePosition(creature.GetPosition());

                // relaunch movement if its speed has changed
                if (_recalculateSpeed)
                    StartMove(creature, true);
            }
            else
            {
                // check if there is a wait time for the next movement
                if (!_nextMoveTime.Passed())
                {
                    // dont update wait timer while moving
                    _nextMoveTime.Update((int)diff);
                    if (_nextMoveTime.Passed())
                    {
                        _nextMoveTime.Reset(0);
                        StartMove(creature); // check path status, get next point and move if necessary & can
                    }
                }
                else // if it's not moving and there is no timer, assume node is reached
                {
                    OnArrived(creature); // hooks and wait timer reset (if necessary)
                    _isArrivalDone = true; // signals that the next move will happen after reaching a node

                    if (_nextMoveTime.Passed())
                        StartMove(creature); // check path status, get next point and move if necessary & can
                }
            }

            return true;
        }

        void MovementInform(Creature creature)
        {
            if (creature.IsAIEnabled)
                creature.GetAI().MovementInform(MovementGeneratorType.Waypoint, (uint)_currentNode);
        }

        public override bool GetResetPosition(Unit u, out float x, out float y, out float z)
        {
            x = y = z = 0;
            // prevent a crash at empty waypoint path.
            // prevent a crash at empty waypoint path.
            if (_path == null || _path.nodes.Empty())
                return false;

            Cypher.Assert(_currentNode < _path.nodes.Count, $"WaypointMovementGenerator.GetResetPos: tried to reference a node id ({_currentNode}) which is not included in path ({_path.id})");
            WaypointNode waypoint = _path.nodes.ElementAt(_currentNode);

            x = waypoint.x;
            y = waypoint.y;
            z = waypoint.z;
            return true;
        }

        public override void Pause(uint timer = 0)
        {
            _stalled = timer == 0;
            _nextMoveTime.Reset(timer != 0 ? (int)timer : 1);
        }

        public override void Resume(uint overrideTimer = 0)
        {
            _stalled = false;
            if (overrideTimer != 0)
                _nextMoveTime.Reset((int)overrideTimer);
        }

        static bool CanMove(Creature creature)
        {
            return !creature.HasUnitState(UnitState.NotMove) && !creature.IsMovementPreventedByCasting();
        }

        public override MovementGeneratorType GetMovementGeneratorType() { return MovementGeneratorType.Waypoint; }

        public override void UnitSpeedChanged() { _recalculateSpeed = true; }

        TimeTrackerSmall _nextMoveTime;
        bool _recalculateSpeed;
        bool _isArrivalDone;
        uint _pathId;
        bool _repeating;
        bool _loadedFromDB;
        bool _stalled;
        bool _done;

        WaypointPath _path;
        int _currentNode;
    }
}
