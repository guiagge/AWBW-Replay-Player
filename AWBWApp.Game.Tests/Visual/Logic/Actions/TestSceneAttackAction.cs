﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using AWBWApp.Game.API.Replay;
using AWBWApp.Game.API.Replay.Actions;
using NUnit.Framework;
using osu.Framework.Graphics.Primitives;

namespace AWBWApp.Game.Tests.Visual.Logic.Actions
{
    [TestFixture]
    public class TestSceneAttackAction : BaseActionsTestScene
    {
        [Test]
        public void TestDestroyUnits()
        {
            AddStep("Setup", destroyTest);
            AddStep("Destroy Land", () => ReplayController.GoToNextAction());
            AddUntilStep("Land Destroyed", () => !HasUnit(1));
            AddStep("Destroy Sea", () => ReplayController.GoToNextAction());
            AddUntilStep("Sea Destroyed", () => !HasUnit(2));
            AddStep("Destroy Air", () => ReplayController.GoToNextAction());
            AddUntilStep("Air Destroyed", () => !HasUnit(3));
            AddStep("Destroy Transport carrying Unit", () => ReplayController.GoToNextAction());
            AddUntilStep("Transport Destroyed", () => !HasUnit(4));
            AddAssert("Transport Cargo Destroyed", () => !HasUnit(5));
            AddStep("Undo", ReplayController.GoToPreviousAction);
            AddUntilStep("Transport Exists", () => HasUnit(4));
            AddUntilStep("Transport Cargo Exists", () => HasUnit(5));
            AddStep("Undo", ReplayController.GoToPreviousAction);
            AddUntilStep("Air Exists", () => HasUnit(3));
            AddStep("Undo", ReplayController.GoToPreviousAction);
            AddUntilStep("Sea Exists", () => HasUnit(2));
            AddStep("Undo", ReplayController.GoToPreviousAction);
            AddUntilStep("Land Exists", () => HasUnit(1));
        }

        [TestCase(1)]
        [TestCase(4)]
        public void TestAttackWithRange(int range)
        {
            AddStep("Setup", () => attackTest(range));
            AddStep("Attack Left", () => ReplayController.GoToNextAction());
            AddUntilStep("Defender HP is 8", () => DoesUnitPassTest(0, x => x.HealthPoints.Value == 8));
            AddUntilStep("Attacker HP is 9", () => DoesUnitPassTest(1, x => x.HealthPoints.Value == 9));
            AddStep("Attack Up", () => ReplayController.GoToNextAction());
            AddUntilStep("Defender HP is 6", () => DoesUnitPassTest(0, x => x.HealthPoints.Value == 6));
            AddUntilStep("Attacker HP is 9", () => DoesUnitPassTest(2, x => x.HealthPoints.Value == 9));
            AddStep("Attack Right", () => ReplayController.GoToNextAction());
            AddUntilStep("Defender HP is 4", () => DoesUnitPassTest(0, x => x.HealthPoints.Value == 4));
            AddUntilStep("Attacker HP is 9", () => DoesUnitPassTest(3, x => x.HealthPoints.Value == 9));
            AddStep("Attack Down", () => ReplayController.GoToNextAction());
            AddUntilStep("Defender HP is 2", () => DoesUnitPassTest(0, x => x.HealthPoints.Value == 2));
            AddUntilStep("Attacker HP is 9", () => DoesUnitPassTest(4, x => x.HealthPoints.Value == 9));

            AddStep("Undo", ReplayController.GoToPreviousAction);
            AddAssert("Defender HP is 4", () => DoesUnitPassTest(0, x => x.HealthPoints.Value == 4));
            AddAssert("Attacker HP is 10", () => DoesUnitPassTest(4, x => x.HealthPoints.Value == 10));
            AddStep("Undo", ReplayController.GoToPreviousAction);
            AddAssert("Defender HP is 6", () => DoesUnitPassTest(0, x => x.HealthPoints.Value == 6));
            AddAssert("Attacker HP is 10", () => DoesUnitPassTest(3, x => x.HealthPoints.Value == 10));
            AddStep("Undo", ReplayController.GoToPreviousAction);
            AddAssert("Defender HP is 8", () => DoesUnitPassTest(0, x => x.HealthPoints.Value == 8));
            AddAssert("Attacker HP is 10", () => DoesUnitPassTest(2, x => x.HealthPoints.Value == 10));
            AddStep("Undo", ReplayController.GoToPreviousAction);
            AddAssert("Defender HP is 10", () => DoesUnitPassTest(0, x => x.HealthPoints.Value == 10));
            AddAssert("Attacker HP is 10", () => DoesUnitPassTest(1, x => x.HealthPoints.Value == 10));
        }

        [Test]
        public void TestAttackWithMovement()
        {
            AddStep("Setup", attackWithMoveTest);
            AddStep("Perform", () => ReplayController.GoToNextAction());
            AddUntilStep("Defender HP is 5", () => DoesUnitPassTest(0, x => x.HealthPoints.Value == 5));
            AddUntilStep("Attacker HP is 8", () => DoesUnitPassTest(1, x => x.HealthPoints.Value == 8));
            AddStep("Undo", ReplayController.GoToPreviousAction);
            AddAssert("Defender HP is 10", () => DoesUnitPassTest(0, x => x.HealthPoints.Value == 10));
            AddAssert("Attacker HP is 10", () => DoesUnitPassTest(1, x => x.HealthPoints.Value == 10));
            AddAssert("Attacker Position is start", () => DoesUnitPassTest(1, x => x.MapPosition == Vector2I.Zero));
        }

        [Test]
        public void CounterAttackTest()
        {
            AddStep("Setup", counterAttackTest);
            AddStep("Attack Unit with Ammo", () => ReplayController.GoToNextAction());
            AddStep("Transport", () => ReplayController.GoToNextAction());
            AddStep("No ammo", () => ReplayController.GoToNextAction());
            AddStep("No ammo but has secondary", () => ReplayController.GoToNextAction());
            AddStep("Too close", () => ReplayController.GoToNextAction());
            AddStep("Too far", () => ReplayController.GoToNextAction());
            AddStep("Hidden", () => ReplayController.GoToNextAction());
        }

        [Test]
        public void SonjaTest()
        {
            AddStep("Setup", sonjaCounterattackTest);
            AddStep("Use Power", () => ReplayController.GoToNextAction());
            AddStep("Next Turn", () => ReplayController.GoToNextAction());

            AddStep("Attack Unit with Ammo", () => ReplayController.GoToNextAction());
            AddStep("Transport", () => ReplayController.GoToNextAction());
            AddStep("No ammo", () => ReplayController.GoToNextAction());
            AddStep("No ammo but has secondary", () => ReplayController.GoToNextAction());
            AddStep("Too close", () => ReplayController.GoToNextAction());
            AddStep("Too far", () => ReplayController.GoToNextAction());
            AddStep("Hidden", () => ReplayController.GoToNextAction());
        }

        private void destroyTest()
        {
            var replayData = CreateBasicReplayData(2);

            var turn = CreateBasicTurnData(replayData);
            replayData.TurnData.Add(turn);

            var attackerUnit = CreateBasicReplayUnit(0, 0, "Artillery", new Vector2I(2, 2));
            attackerUnit.Ammo = 4;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);

            //Test Land Explosion
            var defendingLand = CreateBasicReplayUnit(1, 1, "Infantry", new Vector2I(2, 0));
            defendingLand.Ammo = 0;
            turn.ReplayUnit.Add(defendingLand.ID, defendingLand);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defendingLand, 10, 0, false));

            //Test Sea Explosion
            var defendingSea = CreateBasicReplayUnit(2, 1, "Lander", new Vector2I(4, 2));
            defendingSea.Ammo = 0;
            turn.ReplayUnit.Add(defendingSea.ID, defendingSea);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defendingSea, 10, 0, false));

            //Test Air Explosion
            var defendingAir = CreateBasicReplayUnit(3, 1, "Fighter", new Vector2I(2, 4));
            defendingAir.Ammo = 0;
            turn.ReplayUnit.Add(defendingAir.ID, defendingAir);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defendingAir, 10, 0, false));

            var defendingTransport = CreateBasicReplayUnit(4, 1, "APC", new Vector2I(0, 2));
            defendingTransport.Ammo = 0;
            defendingTransport.CargoUnits = new List<long> { 5 };
            turn.ReplayUnit.Add(defendingTransport.ID, defendingTransport);

            var defendingTransportCargo = CreateBasicReplayUnit(5, 1, "Infantry", new Vector2I(0, 2));
            defendingTransportCargo.Ammo = 0;
            defendingTransportCargo.BeingCarried = true;
            turn.ReplayUnit.Add(defendingTransportCargo.ID, defendingTransportCargo);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defendingTransport, 10, 0, false));

            Debug.Assert(defendingSea.Position.HasValue, "Sea Unit does not have position somehow.");

            var map = CreateBasicMap(5, 5);
            map.Ids[defendingSea.Position.Value.Y * 5 + defendingSea.Position.Value.X] = 28; //Set the tile under the lander to be sea just so it looks more correct.

            ReplayController.LoadReplay(replayData, map);
        }

        private void attackTest(int spaces)
        {
            var replayData = CreateBasicReplayData(2);

            var turn = CreateBasicTurnData(replayData);
            turn.ReplayUnit = new Dictionary<long, ReplayUnit>();
            turn.Buildings = new Dictionary<Vector2I, ReplayBuilding>();
            turn.Actions = new List<IReplayAction>();

            replayData.TurnData.Add(turn);

            var mapSize = 2 * spaces + 1;
            var middle = spaces;

            var unitType = spaces > 3 ? "Missile" : (spaces > 1 ? "Artillery" : "Infantry"); //Chose a unit that can counterattack
            var defenderUnit = CreateBasicReplayUnit(0, 1, unitType, new Vector2I(middle, middle));
            defenderUnit.Ammo = 4;

            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            for (int i = 0; i < 4; i++)
            {
                Vector2I position;

                switch (i)
                {
                    case 0:
                        position = new Vector2I(0, middle);
                        break;

                    case 1:
                        position = new Vector2I(middle, 0);
                        break;

                    case 2:
                        position = new Vector2I(mapSize - 1, middle);
                        break;

                    case 3:
                        position = new Vector2I(middle, mapSize - 1);
                        break;

                    default:
                        throw new Exception();
                }

                var attackerUnit = CreateBasicReplayUnit(i + 1, 0, unitType, position);
                attackerUnit.Ammo = 1;

                turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);

                var attackAction = createAttackUnitAction(attackerUnit, defenderUnit, 9, 10 - ((i + 1) * 2), spaces <= 1);
                turn.Actions.Add(attackAction);
            }

            ReplayController.LoadReplay(replayData, CreateBasicMap(mapSize, mapSize));
        }

        private void attackWithMoveTest()
        {
            var replayData = CreateBasicReplayData(2);

            var turn = CreateBasicTurnData(replayData);
            replayData.TurnData.Add(turn);

            const int map_size = 5;
            const int middle = 2;

            var defenderUnit = CreateBasicReplayUnit(0, 1, "Infantry", new Vector2I(middle, middle));
            defenderUnit.Ammo = 1;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            var attackerUnit = CreateBasicReplayUnit(1, 0, "Infantry", new Vector2I(0, 0));
            attackerUnit.Ammo = 1;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);

            var attackAction = createAttackUnitAction(attackerUnit, defenderUnit, 8, 5, true);
            attackAction.MoveUnit = new MoveUnitAction
            {
                Distance = 3,
                Path = new[]
                {
                    new UnitPosition(Vector2I.Zero),
                    new UnitPosition(new Vector2I(0, 1)),
                    new UnitPosition(new Vector2I(0, 2)),
                    new UnitPosition(new Vector2I(1, 2)),
                },
                Trapped = false,
                Unit = attackerUnit.Clone()
            };
            attackAction.MoveUnit.Unit.Position = new Vector2I(1, 2);
            attackAction.Attacker.Position = new Vector2I(1, 2);

            turn.Actions.Add(attackAction);

            ReplayController.LoadReplay(replayData, CreateBasicMap(map_size, map_size));
        }

        private void counterAttackTest()
        {
            var replayData = CreateBasicReplayData(2);

            var turn = CreateBasicTurnData(replayData);
            replayData.TurnData.Add(turn);

            // Normal Attack
            var attackerUnit = CreateBasicReplayUnit(0, 0, "Infantry", new Vector2I(1, 0));
            attackerUnit.Ammo = 1;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);
            var defenderUnit = CreateBasicReplayUnit(1, 1, "Fighter", new Vector2I(0, 0));
            defenderUnit.Ammo = 1;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defenderUnit, 8, 5, true));

            //Attack a Transport
            attackerUnit = CreateBasicReplayUnit(2, 0, "Infantry", new Vector2I(1, 1));
            attackerUnit.Ammo = 1;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);
            defenderUnit = CreateBasicReplayUnit(3, 1, "APC", new Vector2I(0, 1));
            defenderUnit.Ammo = 1;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defenderUnit, 10, 8, false));

            // No Ammo
            attackerUnit = CreateBasicReplayUnit(4, 0, "Infantry", new Vector2I(1, 2));
            attackerUnit.Ammo = 1;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);
            defenderUnit = CreateBasicReplayUnit(5, 1, "Fighter", new Vector2I(0, 2));
            defenderUnit.Ammo = 0;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defenderUnit, 10, 5, false));

            // No Ammo but has secondary
            attackerUnit = CreateBasicReplayUnit(6, 0, "Infantry", new Vector2I(1, 3));
            attackerUnit.Ammo = 1;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);
            defenderUnit = CreateBasicReplayUnit(7, 1, "Mega Tank", new Vector2I(0, 3));
            defenderUnit.Ammo = 0;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defenderUnit, 0, 9, true));

            // Too Close
            attackerUnit = CreateBasicReplayUnit(8, 0, "Infantry", new Vector2I(1, 4));
            attackerUnit.Ammo = 1;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);
            defenderUnit = CreateBasicReplayUnit(9, 1, "Artillery", new Vector2I(0, 4));
            defenderUnit.Ammo = 1;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defenderUnit, 10, 8, false));

            // Too Far
            attackerUnit = CreateBasicReplayUnit(10, 0, "Artillery", new Vector2I(2, 5));
            attackerUnit.Ammo = 1;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);
            defenderUnit = CreateBasicReplayUnit(11, 1, "Infantry", new Vector2I(0, 5));
            defenderUnit.Ammo = 1;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defenderUnit, 10, 1, false));

            // Hidden
            attackerUnit = CreateBasicReplayUnit(12, 0, "Stealth", new Vector2I(2, 6));
            attackerUnit.Ammo = 1;
            attackerUnit.SubHasDived = true;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);
            defenderUnit = CreateBasicReplayUnit(13, 1, "Infantry", new Vector2I(0, 6));
            defenderUnit.Ammo = 1;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defenderUnit, 10, 1, false));

            //Create map
            var map = CreateBasicMap(3, 7);
            ReplayController.LoadReplay(replayData, map);
        }

        private void sonjaCounterattackTest()
        {
            var replayData = CreateBasicReplayData(2);
            var firstTurn = CreateBasicTurnData(replayData);
            firstTurn.Players[0].ActiveCOID = 18;
            firstTurn.Players[0].RequiredPowerForNormal = 270000;
            firstTurn.Players[0].RequiredPowerForSuper = 450000;
            replayData.TurnData.Add(firstTurn);

            var powerAction = new PowerAction
            {
                CombatOfficerName = "Sonja",
                PowerName = "Counter Break",
                COPower = GetCOStorage().GetCOByName("Sonja").SuperPower,
                IsSuperPower = true,
                SightRangeIncrease = 1
            };

            firstTurn.Actions.Add(powerAction);

            var turn = CreateBasicTurnData(replayData, 1);
            turn.Players[0].ActiveCOID = 18;
            turn.Players[0].RequiredPowerForNormal = 324000;
            turn.Players[0].RequiredPowerForSuper = 540000;
            replayData.TurnData.Add(turn);

            // Normal Attack
            var attackerUnit = CreateBasicReplayUnit(0, 1, "Infantry", new Vector2I(1, 0));
            attackerUnit.Ammo = 1;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);
            var defenderUnit = CreateBasicReplayUnit(1, 0, "Fighter", new Vector2I(0, 0));
            defenderUnit.Ammo = 1;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defenderUnit, 8, 5, true));

            //Attack a Transport
            attackerUnit = CreateBasicReplayUnit(2, 1, "Infantry", new Vector2I(1, 1));
            attackerUnit.Ammo = 1;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);
            defenderUnit = CreateBasicReplayUnit(3, 0, "APC", new Vector2I(0, 1));
            defenderUnit.Ammo = 1;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defenderUnit, 10, 8, false));

            // No Ammo
            attackerUnit = CreateBasicReplayUnit(4, 1, "Infantry", new Vector2I(1, 2));
            attackerUnit.Ammo = 1;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);
            defenderUnit = CreateBasicReplayUnit(5, 0, "Fighter", new Vector2I(0, 2));
            defenderUnit.Ammo = 0;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defenderUnit, 10, 5, false));

            // No Ammo but has secondary
            attackerUnit = CreateBasicReplayUnit(6, 1, "Infantry", new Vector2I(1, 3));
            attackerUnit.Ammo = 1;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);
            defenderUnit = CreateBasicReplayUnit(7, 0, "Mega Tank", new Vector2I(0, 3));
            defenderUnit.Ammo = 0;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defenderUnit, 0, 10, true));

            // Too Close
            attackerUnit = CreateBasicReplayUnit(8, 1, "Infantry", new Vector2I(1, 4));
            attackerUnit.Ammo = 1;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);
            defenderUnit = CreateBasicReplayUnit(9, 0, "Artillery", new Vector2I(0, 4));
            defenderUnit.Ammo = 1;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defenderUnit, 10, 8, false));

            // Too Far
            attackerUnit = CreateBasicReplayUnit(10, 1, "Artillery", new Vector2I(2, 5));
            attackerUnit.Ammo = 1;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);
            defenderUnit = CreateBasicReplayUnit(11, 0, "Infantry", new Vector2I(0, 5));
            defenderUnit.Ammo = 1;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defenderUnit, 10, 1, false));

            // Hidden
            attackerUnit = CreateBasicReplayUnit(12, 1, "Stealth", new Vector2I(2, 6));
            attackerUnit.Ammo = 1;
            attackerUnit.SubHasDived = true;
            turn.ReplayUnit.Add(attackerUnit.ID, attackerUnit);
            defenderUnit = CreateBasicReplayUnit(13, 0, "Infantry", new Vector2I(0, 6));
            defenderUnit.Ammo = 1;
            turn.ReplayUnit.Add(defenderUnit.ID, defenderUnit);

            turn.Actions.Add(createAttackUnitAction(attackerUnit, defenderUnit, 10, 1, false));

            firstTurn.ReplayUnit = turn.ReplayUnit;

            //Create map
            var map = CreateBasicMap(3, 7);
            ReplayController.LoadReplay(replayData, map);
        }

        private AttackUnitAction createAttackUnitAction(ReplayUnit attacker, ReplayUnit defender, int attackerHealthAfter, int defenderHealthAfter, bool counterAttack)
        {
            var attackerAmmo = attacker.Ammo ?? 0;
            var defenderAmmo = defender.Ammo ?? 0;

            var attack = new AttackUnitAction
            {
                Attacker = attacker.Clone(),
                Defender = defender.Clone(),
                PowerChanges = new List<AttackUnitAction.COPowerChange>()
            };

            attack.Attacker.Ammo = Math.Max(0, attackerAmmo - 1);
            attack.Attacker.HitPoints = attackerHealthAfter;

            attack.Defender.Ammo = Math.Max(0, (counterAttack ? defenderAmmo - 1 : defenderAmmo));
            attack.Defender.HitPoints = defenderHealthAfter;

            return attack;
        }
    }
}
