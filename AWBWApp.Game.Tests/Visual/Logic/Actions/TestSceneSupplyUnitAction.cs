﻿using System.Collections.Generic;
using AWBWApp.Game.API.Replay;
using AWBWApp.Game.API.Replay.Actions;
using NUnit.Framework;
using osu.Framework.Graphics.Primitives;

namespace AWBWApp.Game.Tests.Visual.Logic.Actions
{
    [TestFixture]
    public class TestSceneSupplyUnitAction : BaseActionsTestScene
    {
        [Test]
        public void TestSupplyUnit()
        {
            AddStep("Setup", supplyTest);
            AddStep("Supply Units", ReplayController.GoToNextAction);
            AddUntilStep("Supplied", () => !ReplayController.HasOngoingAction());
            AddAssert("All units supplied", () => DoesUnitPassTest(1, x => x.Fuel.Value == 99) && DoesUnitPassTest(2, x => x.Fuel.Value == 99) && DoesUnitPassTest(1, x => x.Fuel.Value == 99) && DoesUnitPassTest(4, x => x.Fuel.Value == 99));
            AddStep("Undo", ReplayController.GoToPreviousAction);
            AddAssert("All units have no fuel", () => DoesUnitPassTest(1, x => x.Fuel.Value == 0) && DoesUnitPassTest(2, x => x.Fuel.Value == 0) && DoesUnitPassTest(1, x => x.Fuel.Value == 0) && DoesUnitPassTest(4, x => x.Fuel.Value == 0));
        }

        [Test]
        public void TestSupplyUnitWithMove()
        {
            AddStep("Setup", supplyTestWithMove);
            AddStep("Supply Units", ReplayController.GoToNextAction);
            AddUntilStep("Supplied", () => !ReplayController.HasOngoingAction());
            AddAssert("All units supplied", () => DoesUnitPassTest(1, x => x.Fuel.Value == 99) && DoesUnitPassTest(2, x => x.Fuel.Value == 99) && DoesUnitPassTest(1, x => x.Fuel.Value == 99));
            AddStep("Undo", ReplayController.GoToPreviousAction);
            AddAssert("All units have no fuel", () => DoesUnitPassTest(1, x => x.Fuel.Value == 0) && DoesUnitPassTest(2, x => x.Fuel.Value == 0) && DoesUnitPassTest(1, x => x.Fuel.Value == 0));
        }

        private void supplyTest()
        {
            var replayData = CreateBasicReplayData(2);
            var turn = CreateBasicTurnData(replayData);
            replayData.TurnData.Add(turn);

            var blackBoat = CreateBasicReplayUnit(0, 0, "APC", new Vector2I(2, 2));
            turn.ReplayUnit.Add(blackBoat.ID, blackBoat);

            var suppliedUnit = CreateBasicReplayUnit(1, 0, "Infantry", new Vector2I(2, 1));
            suppliedUnit.Fuel = 0;
            turn.ReplayUnit.Add(suppliedUnit.ID, suppliedUnit);
            suppliedUnit = CreateBasicReplayUnit(2, 0, "Infantry", new Vector2I(1, 2));
            suppliedUnit.Fuel = 0;
            turn.ReplayUnit.Add(suppliedUnit.ID, suppliedUnit);
            suppliedUnit = CreateBasicReplayUnit(3, 0, "Infantry", new Vector2I(3, 2));
            suppliedUnit.Fuel = 0;
            turn.ReplayUnit.Add(suppliedUnit.ID, suppliedUnit);
            suppliedUnit = CreateBasicReplayUnit(4, 0, "Infantry", new Vector2I(2, 3));
            suppliedUnit.Fuel = 0;
            turn.ReplayUnit.Add(suppliedUnit.ID, suppliedUnit);

            var supplyUnitAction = new SupplyUnitAction
            {
                SupplyingUnitId = blackBoat.ID,
                SuppliedUnitIds = new List<long> { 1, 2, 3, 4 }
            };
            turn.Actions.Add(supplyUnitAction);

            var map = CreateBasicMap(5, 5);
            map.Ids[2 * 5 + 2] = 16;

            ReplayController.LoadReplay(replayData, map);
        }

        private void supplyTestWithMove()
        {
            var replayData = CreateBasicReplayData(2);
            var turn = CreateBasicTurnData(replayData);
            replayData.TurnData.Add(turn);

            var blackBoat = CreateBasicReplayUnit(0, 0, "APC", new Vector2I(2, 3));
            turn.ReplayUnit.Add(blackBoat.ID, blackBoat);

            var suppliedUnit = CreateBasicReplayUnit(1, 0, "Infantry", new Vector2I(2, 1));
            suppliedUnit.Fuel = 0;
            turn.ReplayUnit.Add(suppliedUnit.ID, suppliedUnit);
            suppliedUnit = CreateBasicReplayUnit(2, 0, "Infantry", new Vector2I(1, 2));
            suppliedUnit.Fuel = 0;
            turn.ReplayUnit.Add(suppliedUnit.ID, suppliedUnit);
            suppliedUnit = CreateBasicReplayUnit(3, 0, "Infantry", new Vector2I(3, 2));
            suppliedUnit.Fuel = 0;
            turn.ReplayUnit.Add(suppliedUnit.ID, suppliedUnit);

            var supplyUnitAction = new SupplyUnitAction
            {
                SupplyingUnitId = blackBoat.ID,
                SuppliedUnitIds = new List<long> { 1, 2, 3 },

                MoveUnit = new MoveUnitAction
                {
                    Distance = 1,
                    Trapped = false,
                    Unit = new ReplayUnit { ID = blackBoat.ID, Position = new Vector2I(2, 2) },
                    Path = new[]
                    {
                        new UnitPosition(new Vector2I(2, 3)),
                        new UnitPosition(new Vector2I(2, 2))
                    }
                }
            };
            turn.Actions.Add(supplyUnitAction);

            var map = CreateBasicMap(5, 5);
            map.Ids[2 * 5 + 2] = 16;
            map.Ids[3 * 5 + 2] = 16;

            ReplayController.LoadReplay(replayData, map);
        }
    }
}
