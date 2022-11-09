﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AWBWApp.Game.API.Replay;
using AWBWApp.Game.API.Replay.Actions;
using AWBWApp.Game.Game.Building;
using AWBWApp.Game.Game.Country;
using AWBWApp.Game.Game.Tile;
using AWBWApp.Game.Game.Units;
using AWBWApp.Game.Helpers;
using AWBWApp.Game.Input;
using AWBWApp.Game.UI;
using AWBWApp.Game.UI.Components;
using AWBWApp.Game.UI.Replay;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Threading;
using osuTK;
using osuTK.Graphics;

namespace AWBWApp.Game.Game.Logic
{
    public class GameMap : Container, IKeyBindingHandler<AWBWGlobalAction>
    {
        public Vector2I MapSize { get; private set; }

        public Bindable<WeatherType> CurrentWeather = new Bindable<WeatherType>();

        private readonly TileGridContainer<DrawableTile> tileGrid;
        private readonly TileGridContainer<DrawableBuilding> buildingGrid;

        private readonly Container<DrawableUnit> unitsDrawable;
        private Dictionary<long, DrawableUnit> units;

        private readonly UnitRangeIndicator rangeIndicator;
        private readonly TileCursor tileCursor;

        [Resolved]
        private TerrainTileStorage terrainTileStorage { get; set; }

        [Resolved]
        private BuildingStorage buildingStorage { get; set; }

        [Resolved]
        private UnitStorage unitStorage { get; set; }

        [Resolved]
        private CountryStorage countryStorage { get; set; }

        private CustomShoalGenerator shoalGenerator { get; set; }

        private FogOfWarGenerator fogOfWarGenerator;

        private readonly EffectAnimationController effectAnimationController;

        private ReplayController replayController;

        private readonly MovingGrid grid;

        private bool animatingMapStart;

        private Bindable<bool> revealUnknownInformation;

        public IBindable<bool> RevealUnknownInformation => revealUnknownInformation;

        private Bindable<bool> showGridlines;
        private Bindable<bool> showTileCursor;

        private DetailedInformationPopup infoPopup;

        private const int unit_deselect_delay = 500;
        private ScheduledDelegate unitDeselectDelegate;
        private bool hasLoadedMap = false;
        private bool hasShownMapOutdatedWarning;

        public Sample soundBomb, soundCannon, soundCaptured, soundCapturing, soundCash, soundComplete, soundExplosion,
            soundMoveCar, soundMoveHeli, soundMoveMan, soundMoveJet, soundMovePipe, soundMoveSea, soundMoveSub,
            soundMoveTank, soundStep, soundMachineGun, soundMissile, soundPower, soundSuperPower, soundUsePower,
            soundUseSuper, soundTrap, soundRocket, soundSubMissile;

        public Track trackAdder, trackAndy, trackColin, trackDrake, trackEagle, trackFlak, trackGrimm, trackGrit, 
            trackHachi, trackHawke, trackJake, trackJavier, trackJess, trackJugger, trackKanbei, trackKindle, trackKoal, 
            trackLash, trackMax, trackNell, trackOlaf, trackPower, trackRachel, trackSami, trackSasha, trackSensei, 
            trackSonja, trackSuperPower, trackVictory, trackVonBolt;

        [Resolved]
        private AWBWAppUserInputManager inputManager { get; set; }

        public GameMap(ReplayController controller)
        {
            replayController = controller;

            AddRange(new Drawable[]
            {
                tileGrid = new TileGridContainer<DrawableTile>(DrawableTile.BASE_SIZE)
                {
                    Position = new Vector2(0, DrawableTile.BASE_SIZE.Y)
                },
                buildingGrid = new TileGridContainer<DrawableBuilding>(DrawableTile.BASE_SIZE)
                {
                    Position = new Vector2(0, DrawableTile.BASE_SIZE.Y)
                },
                grid = new MovingGrid
                {
                    Position = new Vector2(-1, DrawableTile.BASE_SIZE.Y - 2),
                    Velocity = Vector2.Zero,
                    Spacing = new Vector2(16),
                    LineSize = new Vector2(2),
                    GridColor = new Color4(15, 15, 15, 255),
                },
                unitsDrawable = new Container<DrawableUnit>(),
                tileCursor = new TileCursor()
                {
                    Alpha = 0f
                },
                rangeIndicator = new UnitRangeIndicator(),
                effectAnimationController = new EffectAnimationController
                {
                    Origin = Anchor.TopLeft,
                    Anchor = Anchor.TopLeft
                }
            });
        }

        private DependencyContainer dependencies;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IBindable<WeatherType>>(CurrentWeather);
            return dependencies;
        }

        public void ScheduleSetToLoading() => Schedule(setToLoading);

        [BackgroundDependencyLoader]
        private void load(AWBWConfigManager settings, ISampleStore samples, ITrackStore tracks)
        {
            revealUnknownInformation = settings.GetBindable<bool>(AWBWSetting.ReplayOnlyShownKnownInfo);
            revealUnknownInformation.BindValueChanged(x => UpdateDiscoveredBuildings());
            showGridlines = settings.GetBindable<bool>(AWBWSetting.ReplayShowGridOverMap);
            showGridlines.BindValueChanged(x => grid.FadeTo(x.NewValue ? 1 : 0, 400, Easing.OutQuint), true);
            showTileCursor = settings.GetBindable<bool>(AWBWSetting.ShowTileCursor);
            // Loads Sound Effects
            soundBomb = samples.Get("Bomb");
            soundCannon = samples.Get("Cannon");
            soundCaptured = samples.Get("Captured");
            soundCapturing = samples.Get("Capturing");
            soundCash = samples.Get("Cash");
            soundComplete = samples.Get("Complete");
            soundExplosion = samples.Get("Explosion");
            soundMachineGun = samples.Get("MachineGun");
            soundMissile = samples.Get("Missile");
            soundMoveCar = samples.Get("MoveCar");
            soundMoveHeli = samples.Get("MoveHeli");
            soundMoveMan = samples.Get("MoveMan");
            soundMoveJet = samples.Get("MoveJet");
            soundMovePipe = samples.Get("MovePipe");
            soundMoveSea = samples.Get("MoveSea");
            soundMoveSub = samples.Get("MoveSub");
            soundMoveTank = samples.Get("MoveTank");
            soundPower = samples.Get("Power");
            soundRocket = samples.Get("Rocket");
            soundStep = samples.Get("Step");
            soundSubMissile = samples.Get("SubMissile");
            soundSuperPower = samples.Get("SuperPower");
            soundTrap = samples.Get("Trap");
            soundUsePower = samples.Get("UsePower");
            soundUseSuper = samples.Get("UseSuper");
            samples.Volume.Value = 0.2;
            // Loads Soundtrack
            trackAdder = tracks.Get("Adder.ogg");
            trackAndy = tracks.Get("Andy.ogg");
            trackColin = tracks.Get("Colin.ogg");
            trackDrake = tracks.Get("Drake.ogg");
            trackEagle = tracks.Get("Eagle.ogg");
            trackFlak = tracks.Get("Flak.ogg");
            trackGrimm = tracks.Get("Grimm.ogg");
            trackGrit = tracks.Get("Grit.ogg");
            trackHachi = tracks.Get("Hachi.ogg");
            trackHawke = tracks.Get("Hawke.ogg");
            trackJake = tracks.Get("Jake.ogg");
            trackJavier = tracks.Get("Javier.ogg");
            trackJess = tracks.Get("Jess.ogg");
            trackJugger = tracks.Get("Jugger.ogg");
            trackKanbei = tracks.Get("Kanbei.ogg");
            trackKindle = tracks.Get("Kindle.ogg");
            trackKoal = tracks.Get("Koal.ogg");
            trackLash = tracks.Get("Lash.ogg");
            trackMax = tracks.Get("Max.ogg");
            trackNell = tracks.Get("Nell.ogg");
            trackOlaf = tracks.Get("Olaf.ogg");
            trackPower = tracks.Get("Power.ogg");
            trackRachel = tracks.Get("Rachel.ogg");
            trackSami = tracks.Get("Sami.ogg");
            trackSasha = tracks.Get("Sasha.ogg");
            trackSensei = tracks.Get("Sensei.ogg");
            trackSonja = tracks.Get("Sonja.ogg");
            trackSuperPower = tracks.Get("SuperPower.ogg");
            trackVictory = tracks.Get("Victory.ogg");
            trackVonBolt = tracks.Get("VonBolt.ogg");
            tracks.Volume.Value = 0.6;
            // Loops CO songs
            trackAdder.Looping = true;
            trackAndy.Looping = true;
            trackColin.Looping = true;
            trackDrake.Looping = true;
            trackEagle.Looping = true;
            trackFlak.Looping = true;
            trackGrimm.Looping = true;
            trackGrit.Looping = true;
            trackHachi.Looping = true;
            trackHawke.Looping = true;
            trackJake.Looping = true;
            trackJavier.Looping = true;
            trackJess.Looping = true;
            trackJugger.Looping = true;
            trackKanbei.Looping = true;
            trackKindle.Looping = true;
            trackKoal.Looping = true;
            trackLash.Looping = true;
            trackMax.Looping = true;
            trackNell.Looping = true;
            trackOlaf.Looping = true;
            trackRachel.Looping = true;
            trackSami.Looping = true;
            trackSasha.Looping = true;
            trackSensei.Looping = true;
            trackSonja.Looping = true;
            trackVonBolt.Looping = true;
        }

        private void setToLoading()
        {
            //Todo: Fix this hardcoded map
            var loadingMap = new ReplayMap
            {
                Size = new Vector2I(33, 11),
                TerrainName = "Loading",
                Ids = new short[]
                {
                    28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
                    28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
                    28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
                    28, 28, 28, 01, 28, 28, 28, 01, 01, 01, 28, 28, 01, 28, 28, 01, 01, 28, 28, 01, 01, 01, 28, 01, 01, 01, 28, 01, 01, 01, 28, 28, 28,
                    28, 28, 28, 01, 28, 28, 28, 01, 28, 01, 28, 01, 28, 01, 28, 01, 28, 01, 28, 28, 01, 28, 28, 01, 28, 01, 28, 01, 28, 28, 28, 28, 28,
                    28, 28, 28, 01, 28, 28, 28, 01, 28, 01, 28, 01, 01, 01, 28, 01, 28, 01, 28, 28, 01, 28, 28, 01, 28, 01, 28, 01, 28, 01, 28, 28, 28,
                    28, 28, 28, 01, 28, 28, 28, 01, 28, 01, 28, 01, 28, 01, 28, 01, 28, 01, 28, 28, 01, 28, 28, 01, 28, 01, 28, 01, 28, 01, 28, 28, 28,
                    28, 28, 28, 01, 01, 01, 28, 01, 01, 01, 28, 01, 28, 01, 28, 01, 01, 28, 28, 01, 01, 01, 28, 01, 28, 01, 28, 01, 01, 01, 28, 28, 28,
                    28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
                    28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
                    28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
                }
            };

            if (shoalGenerator == null)
                shoalGenerator = new CustomShoalGenerator(terrainTileStorage, buildingStorage);
            loadingMap = shoalGenerator.CreateCustomShoalVersion(loadingMap);

            MapSize = loadingMap.Size;

            tileGrid.ClearToSize(MapSize);
            buildingGrid.ClearToSize(MapSize);
            unitsDrawable.Clear();

            units = new Dictionary<long, DrawableUnit>();

            var mapIdx = 0;

            for (int y = 0; y < MapSize.Y; y++)
            {
                for (int x = 0; x < MapSize.X; x++)
                {
                    var terrainId = loadingMap.Ids[mapIdx++];

                    var terrainTile = terrainTileStorage.GetTileByAWBWId(terrainId);
                    var tile = new DrawableTile(terrainTile);
                    tileGrid.AddTile(tile, new Vector2I(x, y));
                }
            }

            AutoSizeAxes = Axes.Both;
            setSize(new Vector2(MapSize.X * DrawableTile.BASE_SIZE.X, (MapSize.Y + 1) * DrawableTile.BASE_SIZE.Y));

            tileGrid.FadeOut().FadeIn(250);
            animateStart(4);
        }

        private void setSize(Vector2 size)
        {
            grid.Size = size;
            tileGrid.Size = size;
            buildingGrid.Size = size;
            unitsDrawable.Size = size;
            effectAnimationController.Size = size;
        }

        public void SetInfoPopup(DetailedInformationPopup popup)
        {
            infoPopup = popup;
        }

        public void SetToInitialGameState(ReplayData gameState, ReplayMap map)
        {
            Assert.IsTrue(ThreadSafety.IsUpdateThread, "SetToInitialGameState was called off update thread.");
            hasLoadedMap = false;

            if (shoalGenerator == null)
                shoalGenerator = new CustomShoalGenerator(terrainTileStorage, buildingStorage);

            map = shoalGenerator.CreateCustomShoalVersion(map);

            MapSize = map.Size;

            //Calculate the map size as this isn't given by the api
            //Todo: Check buildings
            if (AutoSizeAxes == Axes.None)
                Size = Vec2IHelper.ScalarMultiply(MapSize + new Vector2I(0, 1), DrawableTile.BASE_SIZE);

            tileGrid.ClearToSize(MapSize);
            buildingGrid.ClearToSize(MapSize);
            unitsDrawable.Clear();

            units = new Dictionary<long, DrawableUnit>();

            var replayBuildings = gameState.TurnData[0].Buildings;

            for (int y = 0; y < MapSize.Y; y++)
            {
                for (int x = 0; x < MapSize.X; x++)
                {
                    var terrainId = map.Ids[y * MapSize.X + x];

                    TerrainTile terrainTile;

                    if (buildingStorage.ContainsBuildingWithAWBWId(terrainId))
                    {
                        terrainTile = terrainTileStorage.GetTileByCode("Plain");

                        if (!replayBuildings.TryGetValue(new Vector2I(x, y), out _))
                        {
                            if (!hasShownMapOutdatedWarning)
                            {
                                replayController.ShowError(new Exception("Buildings do not match replay due to a map update. This can cause further errors, proceed with caution."), false);
                                hasShownMapOutdatedWarning = true;
                            }
                        }
                    }
                    else
                        terrainTile = terrainTileStorage.GetTileByAWBWId(terrainId);
                    var tile = new DrawableTile(terrainTile);
                    tileGrid.AddTile(tile, new Vector2I(x, y));
                }
            }

            foreach (var awbwBuilding in replayBuildings)
            {
                if (!awbwBuilding.Value.TerrainID.HasValue)
                    throw new Exception("Invalid building encountered: Missing terrain id.");

                if (!buildingStorage.TryGetBuildingByAWBWId(awbwBuilding.Value.TerrainID.Value, out var building))
                {
                    //This is probably a terrain tile that get building properties. This can happen with pipes.
                    if (terrainTileStorage.TryGetTileByAWBWId(awbwBuilding.Value.TerrainID.Value, out _))
                        continue;

                    throw new Exception("Unknown Building ID: " + awbwBuilding.Value.TerrainID.Value);
                }
                var position = awbwBuilding.Value.Position;

                var playerID = getPlayerIDFromCountryID(building.CountryID);
                var country = playerID.HasValue ? replayController.Players[playerID.Value].Country : null;
                var drawableBuilding = new DrawableBuilding(building, position, playerID, country);

                foreach (var player in gameState.ReplayInfo.Players)
                {
                    if (drawableBuilding.TeamToTile.ContainsKey(player.Value.TeamName))
                        continue;

                    drawableBuilding.TeamToTile[player.Value.TeamName] = drawableBuilding.BuildingTile;
                }

                //For testing purposes. Likely not used in any game.
                if (awbwBuilding.Value.Capture.HasValue && awbwBuilding.Value.Capture != 0)
                    drawableBuilding.CaptureHealth.Value = awbwBuilding.Value.Capture.Value;

                buildingGrid.AddTile(drawableBuilding, position);
            }

            var replayUnits = gameState.TurnData[0].ReplayUnit;

            if (replayUnits != null)
            {
                foreach (var unit in replayUnits)
                {
                    var drawableUnit = AddUnit(unit.Value, false);

                    //For testing purposes. Likely not used in any game.
                    if (!drawableUnit.BeingCarried.Value && TryGetDrawableBuilding(drawableUnit.MapPosition, out var awbwBuilding))
                        drawableUnit.IsCapturing.Value = awbwBuilding.CaptureHealth.Value != 20 && awbwBuilding.CaptureHealth.Value != 0;
                }
            }

            fogOfWarGenerator = new FogOfWarGenerator(this);
            fogOfWarGenerator.FogOfWar.BindValueChanged(x => updateFog(x.NewValue));

            AutoSizeAxes = Axes.Both;
            setSize(new Vector2(MapSize.X * DrawableTile.BASE_SIZE.X, (MapSize.Y + 1) * DrawableTile.BASE_SIZE.Y));

            CurrentWeather.Value = gameState.TurnData[0].StartWeather.Type;
            tileGrid.FadeIn();
            hasLoadedMap = true;
            animateStart(1.5f);
        }

        private void animateStart(float speed)
        {
            animatingMapStart = true;

            var inverseSpeed = 1 / speed;

            var offsetPosition = new Vector2(DrawableTile.HALF_BASE_SIZE.X, -3 * DrawableTile.BASE_SIZE.Y);

            for (int x = 0; x < MapSize.X; x++)
            {
                for (int y = 0; y < MapSize.Y; y++)
                {
                    var tile = tileGrid[x, y];
                    var tilePos = tile.Position;

                    tile.FadeOut().Delay((x + y) * 40 * inverseSpeed).FadeIn().MoveToOffset(offsetPosition).MoveTo(tilePos, 275 * inverseSpeed, Easing.OutCubic);

                    var coord = new Vector2I(x, y);

                    if (buildingGrid.TryGet(coord, out var building))
                    {
                        building.FadeOut().Delay(((x + y) * 40 + 25) * inverseSpeed).FadeIn().MoveToOffset(offsetPosition)
                                .MoveTo(building.Position, 275 * inverseSpeed, Easing.OutCubic);
                    }

                    if (TryGetDrawableUnit(coord, out var unit))
                    {
                        unit.UnitAnimatingIn = true;
                        unit.FadeOut().Delay(((x + y) * 40 + 50) * inverseSpeed).FadeInFromZero().MoveToOffset(offsetPosition)
                            .MoveTo(unit.Position, 275 * inverseSpeed, Easing.OutCubic).OnComplete(x => x.UnitAnimatingIn = false);
                    }
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!hasLoadedMap)
                return;

            var cursor = inputManager.CurrentState.Mouse.Position;

            if (getUnitAndTileFromMousePosition(ToLocalSpace(cursor), out var tilePosition, out var tile, out var building, out var unit) && IsHovered)
            {
                infoPopup.ShowDetails(tile, building, unit);

                tileCursor.TilePosition = tilePosition;
                if (showTileCursor.Value)
                    tileCursor.Show();
                else
                    tileCursor.Hide();
            }
            else
            {
                infoPopup.ShowDetails(null, null, null);
                tileCursor.Hide();
            }

            if (unit != selectedUnit)
            {
                if (unitDeselectDelegate == null)
                    unitDeselectDelegate = Scheduler.AddDelayed(() => SetUnitAsSelected(null), unit_deselect_delay);
            }
            else if (unitDeselectDelegate != null)
            {
                unitDeselectDelegate.Cancel();
                unitDeselectDelegate = null;
            }
        }

        private void updateFog(FogOfWarState[,] fogOfWar)
        {
            for (int x = 0; x < MapSize.X; x++)
            {
                for (int y = 0; y < MapSize.Y; y++)
                {
                    var fogState = fogOfWar[x, y];
                    tileGrid[x, y].FogOfWarActive.Value = fogState != FogOfWarState.AllVisible;

                    var coord = new Vector2I(x, y);
                    if (buildingGrid.TryGet(coord, out var building))
                        building.FogOfWarActive.Value = fogState != FogOfWarState.AllVisible;

                    //Replays without pipe attack actions can occasionally lead to having 2 units on the same tile.
                    foreach (var unit in GetAllDrawableUnitsOnTile(coord))
                        unit.FogOfWarActive.Value = unit.UnitData.MovementType == MovementType.Air ? fogState == FogOfWarState.Hidden : fogState != FogOfWarState.AllVisible;
                }
            }
        }

        public static Vector2 GetDrawablePositionForTopOfTile(Vector2I tilePos) => new Vector2(tilePos.X * DrawableTile.BASE_SIZE.X, tilePos.Y * DrawableTile.BASE_SIZE.Y - 1);
        public static Vector2 GetDrawablePositionForBottomOfTile(Vector2I tilePos) => new Vector2(tilePos.X * DrawableTile.BASE_SIZE.X, (tilePos.Y + 1) * DrawableTile.BASE_SIZE.Y - 1);

        public void ScheduleUpdateToGameState(TurnData gameState, Action postUpdateAction)
        {
            if (animatingMapStart)
            {
                FinishTransforms(true);
                animatingMapStart = false;
            }

            Schedule(() =>
            {
                updateToGameState(gameState);
                postUpdateAction?.Invoke();
            });
        }

        private void updateToGameState(TurnData gameState)
        {
            //Do units first, then buildings as buildings need to set capture status on units.

            foreach (var unit in gameState.ReplayUnit)
            {
                if (units.TryGetValue(unit.Value.ID, out DrawableUnit existingUnit))
                {
                    existingUnit.UpdateUnit(unit.Value);
                }
                else
                {
                    var unitData = unitStorage.GetUnitByCode(unit.Value.UnitName);

                    var player = replayController.Players[unit.Value.PlayerID!.Value];
                    var drawableUnit = new DrawableUnit(unitData, unit.Value, player.Country, player.UnitFaceDirection);
                    drawableUnit.FogOfWarActive.Value = IsTileFoggy(drawableUnit.MapPosition, drawableUnit.UnitData.MovementType == MovementType.Air);
                    units.Add(unit.Value.ID, drawableUnit);
                    unitsDrawable.Add(drawableUnit);
                }
            }

            foreach (var unit in units.Where(x => !gameState.ReplayUnit.ContainsKey(x.Value.UnitID)))
            {
                units.Remove(unit.Key);
                unitsDrawable.Remove(unit.Value, true);
            }

            for (int x = 0; x < MapSize.X; x++)
            {
                for (int y = 0; y < MapSize.Y; y++)
                {
                    var position = new Vector2I(x, y);

                    if (gameState.Buildings.TryGetValue(position, out var building))
                    {
                        UpdateBuilding(building, true);
                    }
                    else
                        buildingGrid.RemoveTile(new Vector2I(x, y));
                }
            }

            CurrentWeather.Value = gameState.StartWeather.Type;
        }

        public void ClearFog(bool makeFoggy, bool triggerChange) => fogOfWarGenerator?.ClearFog(makeFoggy, triggerChange);
        public void UpdateFogOfWar(long playerId, int rangeIncrease, bool canSeeIntoHiddenTiles, bool resetFog = true) => fogOfWarGenerator.GenerateFogForPlayer(playerId, rangeIncrease, canSeeIntoHiddenTiles, resetFog);

        public bool IsTileFoggy(Vector2I position, bool forAirUnit)
        {
            if (fogOfWarGenerator.FogOfWar.Value == null)
                return false;

            var fogState = fogOfWarGenerator.FogOfWar.Value[position.X, position.Y];

            return forAirUnit ? (fogState == FogOfWarState.Hidden) : (fogState != FogOfWarState.AllVisible);
        }

        public DrawableUnit AddUnit(ReplayUnit unit, bool schedule = true)
        {
            var unitData = unitStorage.GetUnitByCode(unit.UnitName);
            var player = replayController.Players[unit.PlayerID!.Value];
            var drawableUnit = new DrawableUnit(unitData, unit, player.Country, player.UnitFaceDirection);
            units.Add(unit.ID, drawableUnit);

            if (schedule)
                Schedule(() => unitsDrawable.Add(drawableUnit));
            else
                unitsDrawable.Add(drawableUnit);

            replayController.Players[unit.PlayerID!.Value].UnitCount.Value++;
            return drawableUnit;
        }

        public bool TryGetDrawableUnit(long unitId, out DrawableUnit drawableUnit) => units.TryGetValue(unitId, out drawableUnit);

        public DrawableUnit GetDrawableUnit(long unitId) => units[unitId];

        public bool TryGetDrawableBuilding(Vector2I position, out DrawableBuilding drawableBuilding) => buildingGrid.TryGet(position, out drawableBuilding);

        public DrawableUnit DeleteUnit(long unitId, bool explode)
        {
            if (!units.Remove(unitId, out DrawableUnit unit))
                return null;

            if (explode && !replayController.ShouldPlayerActionBeHidden(unit.MapPosition, unit.UnitData.MovementType == MovementType.Air))
                playExplosion(unit.UnitData.MovementType, unit.MapPosition);

            unitsDrawable.Remove(unit, true);
            if (unit.OwnerID.HasValue)
                replayController.Players[unit.OwnerID.Value].UnitCount.Value--;

            if (unit.Cargo != null)
            {
                foreach (var cargoId in unit.Cargo)
                {
                    if (cargoId == -1)
                        continue;

                    DeleteUnit(cargoId, false);
                }
            }

            return unit;
        }

        private void playExplosion(MovementType type, Vector2I unitPosition)
        {
            switch (type)
            {
                case MovementType.Air:
                    PlayEffect("Effects/Explosion/Explosion-Air", 450, unitPosition);
                    break;

                case MovementType.Sea:
                case MovementType.Lander:
                    PlayEffect("Effects/Explosion/Explosion-Sea", 350, unitPosition);
                    break;

                default:
                    PlayEffect("Effects/Explosion/Explosion-Land", 500, unitPosition);
                    break;
            }
        }

        public List<DrawableUnit> GetUnitsWithDistance(Vector2I position, int distance)
        {
            var unitsWithRange = new List<DrawableUnit>();

            foreach (var unit in units)
            {
                if ((unit.Value.MapPosition - position).ManhattonDistance() > distance)
                    continue;

                unitsWithRange.Add(unit.Value);
            }
            return unitsWithRange;
        }

        public EffectAnimation PlayEffect(string animation, double duration, Vector2I mapPosition, double startDelay = 0, Action<EffectAnimation> onLoaded = null) => effectAnimationController.PlayAnimation(animation, duration, mapPosition, startDelay, onLoaded);

        public EffectAnimation PlaySelectionAnimation(DrawableUnit unit)
        {
            var effect = PlayEffect("Effects/Select", 100, unit.MapPosition, 0,
                x => x.FadeTo(0.5f).ScaleTo(0.5f)
                      .FadeTo(1, 150, Easing.In).ScaleTo(1, 300, Easing.OutBounce).Then().Expire());
            return effect;
        }

        public EffectAnimation PlaySelectionAnimation(DrawableBuilding building)
        {
            var effect = PlayEffect("Effects/Select", 100, building.MapPosition, 0,
                x => x.FadeTo(0.5f).ScaleTo(0.5f)
                      .FadeTo(1, 150, Easing.In).ScaleTo(1, 300, Easing.OutBounce).Then().Expire());
            return effect;
        }

        public void ClearAllEffects()
        {
            effectAnimationController.Clear();
        }

        public DrawableUnit GetDrawableUnit(Vector2I unitPosition)
        {
            //Todo: query by position rather than iterate over everything
            foreach (var unit in units)
            {
                if (unit.Value.MapPosition == unitPosition && !unit.Value.BeingCarried.Value)
                    return unit.Value;
            }

            throw new Exception("Unable to find unit at position: " + unitPosition);
        }

        public bool TryGetDrawableUnit(Vector2I unitPosition, out DrawableUnit unit)
        {
            //Todo: query by position rather than iterate over everything
            foreach (var checkUnit in units)
            {
                if (checkUnit.Value.MapPosition == unitPosition && !checkUnit.Value.BeingCarried.Value)
                {
                    unit = checkUnit.Value;
                    return true;
                }
            }

            unit = null;
            return false;
        }

        public IEnumerable<DrawableUnit> GetAllDrawableUnitsOnTile(Vector2I unitPosition)
        {
            foreach (var checkUnit in units)
            {
                if (checkUnit.Value.MapPosition == unitPosition && !checkUnit.Value.BeingCarried.Value)
                    yield return checkUnit.Value;
            }
        }

        public IEnumerable<DrawableUnit> GetDrawableUnitsFromPlayer(long playerId)
        {
            return units.Values.Where(x => x.OwnerID.HasValue && x.OwnerID == playerId);
        }

        public IEnumerable<DrawableBuilding> GetDrawableBuildingsForPlayer(long playerId)
        {
            foreach (var building in buildingGrid)
            {
                if (building.OwnerID.HasValue && building.OwnerID == playerId)
                    yield return building;
            }
        }

        public DrawableTile GetDrawableTile(Vector2I position) => tileGrid[position.X, position.Y];

        public void UpdateBuilding(ReplayBuilding awbwBuilding, bool setBuildingToReady)
        {
            var tilePosition = awbwBuilding.Position;

            if (!buildingGrid.TryGet(tilePosition, out DrawableBuilding building))
            {
                if (!awbwBuilding.TerrainID.HasValue)
                    throw new Exception("Tried to update a missing building. But it didn't have a terrain id.");

                if (buildingStorage.TryGetBuildingByAWBWId(awbwBuilding.TerrainID.Value, out var buildingTile))
                {
                    var playerID = getPlayerIDFromCountryID(buildingTile.CountryID);
                    var country = playerID.HasValue ? replayController.Players[playerID.Value].Country : null;
                    var drawableBuilding = new DrawableBuilding(buildingTile, tilePosition, playerID, country);
                    drawableBuilding.FogOfWarActive.Value = IsTileFoggy(awbwBuilding.Position, false);
                    buildingGrid.AddTile(drawableBuilding, tilePosition);
                    return;
                }

                if (terrainTileStorage.TryGetTileByAWBWId(awbwBuilding.TerrainID.Value, out _))
                    return;

                throw new Exception("Unknown Building ID: " + awbwBuilding.TerrainID.Value);
            }

            var comparisonTerrainId = awbwBuilding.TerrainID ?? 0;

            if (comparisonTerrainId != 0 && building.BuildingTile.AWBWID != comparisonTerrainId)
            {
                buildingGrid.RemoveTile(tilePosition);

                if (awbwBuilding.TerrainID.HasValue && awbwBuilding.TerrainID != 0)
                {
                    if (buildingStorage.TryGetBuildingByAWBWId(awbwBuilding.TerrainID.Value, out var buildingTile))
                    {
                        var playerID = getPlayerIDFromCountryID(buildingTile.CountryID);
                        var country = playerID.HasValue ? replayController.Players[playerID.Value].Country : null;
                        var newBuilding = new DrawableBuilding(buildingTile, tilePosition, playerID, country);
                        transferDiscovery(building, newBuilding);
                        newBuilding.FogOfWarActive.Value = IsTileFoggy(awbwBuilding.Position, false);
                        buildingGrid.AddTile(newBuilding, tilePosition);
                        building = newBuilding;
                    }
                    else if (terrainTileStorage.TryGetTileByAWBWId(awbwBuilding.TerrainID.Value, out var terrainTile))
                    {
                        //Likely a blown up pipe. May need to change the tile underneath

                        var tile = tileGrid[tilePosition.X, tilePosition.Y];

                        if (tile.TerrainTile != terrainTile)
                        {
                            var newTile = new DrawableTile(terrainTile);

                            tileGrid.AddTile(newTile, tilePosition);
                            newTile.FogOfWarActive.Value = IsTileFoggy(tilePosition, false);
                        }
                    }
                }
            }

            //Todo: Is this always the case
            if (setBuildingToReady)
                building.HasDoneAction.Value = false;
            building.CaptureHealth.Value = awbwBuilding.Capture ?? 20;

            if (TryGetDrawableUnit(awbwBuilding.Position, out var unit))
                unit.IsCapturing.Value = awbwBuilding.Capture != awbwBuilding.LastCapture && awbwBuilding.Capture != 20 && awbwBuilding.Capture != 0;
        }

        public void RegisterDiscovery(DiscoveryCollection collection)
        {
            var team = getCurrentTeamVisibility();

            foreach (var id in collection.DiscoveryByID)
            {
                foreach (var discovered in id.Value.DiscoveredBuildings)
                {
                    if (!TryGetDrawableBuilding(discovered.Key, out var discBuilding))
                        continue;

                    if (buildingStorage.TryGetBuildingByAWBWId(discovered.Value.TerrainID!.Value, out var building))
                    {
                        discBuilding.TeamToTile[id.Key] = building;
                        discBuilding.UpdateFogOfWarBuilding(revealUnknownInformation.Value, team);
                    }
                    else
                    {
                        if (discovered.Value.TerrainID!.Value != 115 && discovered.Value.TerrainID!.Value != 116)
                            throw new Exception("A building was turned into a terrain tile, and it was not a pipe?");

                        //If a building is changed from a Pipe to a Pipe Seam, this will trigger.
                        //These actions are seen by everyone, even through fog.
                        //Todo: Does this cause other issues?

                        UpdateBuilding(discovered.Value, false);
                    }
                }
            }
        }

        public void UndoDiscovery(DiscoveryCollection collection)
        {
            var team = getCurrentTeamVisibility();

            foreach (var building in collection.OriginalDiscovery)
            {
                if (!TryGetDrawableBuilding(building.Key, out var discBuilding))
                    continue;

                discBuilding.TeamToTile.SetTo(building.Value);
                discBuilding.UpdateFogOfWarBuilding(revealUnknownInformation.Value, team);
            }
        }

        public void UpdateDiscoveredBuildings()
        {
            var team = getCurrentTeamVisibility();
            foreach (var building in buildingGrid)
                building.UpdateFogOfWarBuilding(revealUnknownInformation.Value, team);
        }

        private void transferDiscovery(DrawableBuilding originalBuilding, DrawableBuilding newBuilding)
        {
            newBuilding.TeamToTile.SetTo(originalBuilding.TeamToTile);

            if (originalBuilding.OwnerID.HasValue)
                newBuilding.TeamToTile[replayController.Players[originalBuilding.OwnerID.Value].Team] = newBuilding.BuildingTile;
            if (newBuilding.OwnerID.HasValue)
                newBuilding.TeamToTile[replayController.Players[newBuilding.OwnerID.Value].Team] = newBuilding.BuildingTile;
            newBuilding.UpdateFogOfWarBuilding(revealUnknownInformation.Value, getCurrentTeamVisibility());
        }

        private string getCurrentTeamVisibility()
        {
            if (replayController.CurrentFogView.Value is long id)
                return replayController.Players[id].Team;

            var team = replayController.CurrentFogView.Value as string;
            return team.IsNullOrEmpty() ? replayController.ActivePlayer.Team : team;
        }

        public bool OnPressed(KeyBindingPressEvent<AWBWGlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case AWBWGlobalAction.ShowUnitsAndBuildingsInFog:
                    revealUnknownInformation.Value = !revealUnknownInformation.Value;
                    return true;

                case AWBWGlobalAction.ShowGridLines:
                    showGridlines.Value = !showGridlines.Value;
                    return true;

                case AWBWGlobalAction.ShowTileCursor:
                    showTileCursor.Value = !showTileCursor.Value;
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<AWBWGlobalAction> e)
        {
        }

        private DrawableUnit selectedUnit;
        private int drawMode;

        public bool SetUnitAsSelected(DrawableUnit unit)
        {
            if (unit == null)
            {
                drawMode = 0;
                selectedUnit = null;
                rangeIndicator.FadeOut(500, Easing.OutQuint);
                return false;
            }

            if (unit.BeingCarried.Value)
                return false;
            if (!revealUnknownInformation.Value && unit.FogOfWarActive.Value)
                return false;

            if (unit != selectedUnit)
            {
                drawMode = 0;
                selectedUnit = unit;
            }
            else if (drawMode >= 2)
            {
                drawMode = 0;
                selectedUnit = null;
                rangeIndicator.FadeOut(500, Easing.OutCubic);
                return false;
            }
            else
                drawMode++;

            if (drawMode == 1 && unit.UnitData.AttackRange == Vector2I.Zero)
                drawMode++;

            var tileList = new List<Vector2I>();

            Color4 colour;
            Color4 outlineColour;

            switch (drawMode)
            {
                case 0:
                {
                    getMovementTiles(unit, tileList);
                    colour = new Color4(50, 200, 50, 100);
                    outlineColour = new Color4(100, 150, 100, 255);
                    break;
                }

                case 1:
                {
                    var range = unit.AttackRange.Value;

                    var action = replayController.GetActivePowerForPlayer(unit.OwnerID!.Value);
                    range.Y += action?.COPower.PowerIncreases?.FirstOrDefault(x => x.AffectedUnits.Contains("all") || x.AffectedUnits.Contains(unit.UnitData.Name))?.RangeIncrease ?? 0;

                    var dayToDay = replayController.Players[unit.OwnerID!.Value].ActiveCO.Value.CO.DayToDayPower;
                    range.Y += dayToDay.PowerIncreases?.FirstOrDefault(x => x.AffectedUnits.Contains("all") || x.AffectedUnits.Contains(unit.UnitData.Name))?.RangeIncrease ?? 0;

                    if (unit.UnitData.AttackRange != Vector2I.One)
                    {
                        for (int i = range.X; i <= range.Y; i++)
                        {
                            foreach (var tile in Vec2IHelper.GetAllTilesWithDistance(unit.MapPosition, i))
                            {
                                if (tile.X < 0 || tile.Y < 0 || tile.X >= MapSize.X || tile.Y >= MapSize.Y)
                                    continue;

                                tileList.Add(tile);
                            }
                        }
                    }
                    else
                        getPossibleAttackRange(unit, tileList, range);

                    colour = new Color4(200, 90, 90, 70);
                    outlineColour = new Color4(160, 82, 51, 255);
                    break;
                }

                case 2:
                {
                    var dayToDayPower = replayController.Players[unit.OwnerID!.Value].ActiveCO.Value.CO.DayToDayPower;
                    var action = replayController.GetActivePowerForPlayer(unit.OwnerID!.Value);
                    var sightRangeModifier = dayToDayPower.SightIncrease + (action?.SightRangeIncrease ?? 0);
                    sightRangeModifier += unit.UnitData.MovementType != MovementType.Air ? tileGrid[unit.MapPosition.X, unit.MapPosition.Y].TerrainTile.SightDistanceIncrease : 0;

                    if (CurrentWeather.Value == WeatherType.Rain)
                        sightRangeModifier -= 1;

                    var vision = Math.Max(1, unit.UnitData.Vision + sightRangeModifier);

                    for (int i = 0; i <= vision; i++)
                    {
                        foreach (var tile in Vec2IHelper.GetAllTilesWithDistance(unit.MapPosition, i))
                        {
                            if (tile.X < 0 || tile.Y < 0 || tile.X >= MapSize.X || tile.Y >= MapSize.Y)
                                continue;

                            var distance = tileGrid[tile.X, tile.Y].TerrainTile.LimitFogOfWarSightDistance;
                            if (distance > 0 && distance < i)
                                continue;

                            tileList.Add(tile);
                        }
                    }

                    colour = new Color4(50, 50, 200, 100);
                    outlineColour = new Color4(100, 100, 150, 255);
                    break;
                }

                default:
                    throw new ArgumentException("Out of range", nameof(drawMode));
            }

            rangeIndicator.ShowNewRange(tileList, unit.MapPosition, colour, outlineColour);

            return true;
        }

        private void getPossibleAttackRange(DrawableUnit unit, List<Vector2I> tileList, Vector2I range)
        {
            var movementList = new List<Vector2I>();

            getMovementTiles(unit, movementList);

            var tileSet = new HashSet<Vector2I>();

            foreach (var moveTile in movementList)
            {
                for (int i = range.X; i <= range.Y; i++)
                {
                    foreach (var tile in Vec2IHelper.GetAllTilesWithDistance(moveTile, i))
                    {
                        if (tile.X < 0 || tile.Y < 0 || tile.X >= MapSize.X || tile.Y >= MapSize.Y)
                            continue;

                        tileSet.Add(tile);
                    }
                }
            }

            tileList.AddRange(tileSet);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (!hasLoadedMap)
                return base.OnClick(e);

            if (getUnitAndTileFromMousePosition(e.MousePosition, out _, out _, out _, out var unit) && unit != null)
                return SetUnitAsSelected(unit);

            return base.OnClick(e);
        }

        private bool getUnitAndTileFromMousePosition(Vector2 cursor, out Vector2I tilePosition, out DrawableTile tile, out DrawableBuilding building, out DrawableUnit unit)
        {
            tile = null;
            building = null;
            unit = null;
            tilePosition = Vector2I.Zero;

            if (cursor.X < 0 || cursor.X >= DrawSize.X)
                return false;
            if (cursor.Y < DrawableTile.BASE_SIZE.Y || cursor.Y >= DrawSize.Y)
                return false;

            cursor.Y -= DrawableTile.BASE_SIZE.Y;

            //Doubly make sure that we aren't trying to get a tile outside of what we have.
            tilePosition = new Vector2I((int)(cursor.X / DrawableTile.BASE_SIZE.X), (int)(cursor.Y / DrawableTile.BASE_SIZE.Y));
            if (tilePosition.X < 0 || tilePosition.X >= MapSize.X || tilePosition.Y < 0 || tilePosition.Y >= MapSize.Y)
                return false;

            TryGetDrawableUnit(tilePosition, out unit);
            buildingGrid.TryGet(tilePosition, out building);
            tile = tileGrid[tilePosition.X, tilePosition.Y];
            Debug.Assert(tile != null);

            return true;
        }

        private void getMovementTiles(DrawableUnit unit, List<Vector2I> positions)
        {
            var visited = new HashSet<Vector2I>();
            var queue = new PriorityQueue<Vector2I, int>();

            queue.Enqueue(unit.MapPosition, 0);

            var movementRange = unit.MovementRange.Value;

            var action = replayController.GetActivePowerForPlayer(unit.OwnerID!.Value);
            var dayToDay = replayController.Players[unit.OwnerID!.Value].ActiveCO.Value.CO.DayToDayPower;

            movementRange += action?.MovementRangeIncrease ?? 0;

            void addTileIfCanMoveTo(Vector2I position, int movement)
            {
                Dictionary<MovementType, int> moveCosts;

                TerrainType terrainType;

                if (TryGetDrawableBuilding(position, out var building))
                {
                    moveCosts = building.BuildingTile.MovementCostsPerType;
                    terrainType = TerrainType.Building;
                }
                else
                {
                    var tile = tileGrid[position.X, position.Y].TerrainTile;
                    moveCosts = tile.MovementCostsPerType;
                    terrainType = tile.TerrainType;
                }

                if (moveCosts.TryGetValue(unit.UnitData.MovementType, out var cost))
                {
                    if (dayToDay.MoveCostPerTile != null && CurrentWeather.Value != WeatherType.Snow)
                        cost = dayToDay.MoveCostPerTile.Value;
                    else if (CurrentWeather.Value != WeatherType.Clear)
                        cost = movementForWeather(unit.UnitData.MovementType, dayToDay.WeatherWithNoMovementAffect, dayToDay.WeatherWithAdditionalMovementAffect, terrainType, cost);

                    if (movement + cost <= movementRange)
                        queue.Enqueue(position, movement + cost);
                }
            }

            while (queue.TryDequeue(out var tilePos, out var movement))
            {
                if (visited.Contains(tilePos))
                    continue;

                visited.Add(tilePos);
                positions.Add(tilePos);

                var nextTile = tilePos + new Vector2I(1, 0);
                if (nextTile.X < MapSize.X && !visited.Contains(nextTile))
                    addTileIfCanMoveTo(nextTile, movement);

                nextTile = tilePos + new Vector2I(-1, 0);
                if (nextTile.X >= 0 && !visited.Contains(nextTile))
                    addTileIfCanMoveTo(nextTile, movement);

                nextTile = tilePos + new Vector2I(0, 1);
                if (nextTile.Y < MapSize.Y && !visited.Contains(nextTile))
                    addTileIfCanMoveTo(nextTile, movement);

                nextTile = tilePos + new Vector2I(0, -1);
                if (nextTile.Y >= 0 && !visited.Contains(nextTile))
                    addTileIfCanMoveTo(nextTile, movement);
            }
        }

        private int movementForWeather(MovementType moveType, WeatherType noAffect, WeatherType additionalEffect, TerrainType type, int cost)
        {
            if (CurrentWeather.Value == WeatherType.Clear || CurrentWeather.Value == noAffect)
                return cost;

            if (CurrentWeather.Value == WeatherType.Rain && additionalEffect != WeatherType.Rain)
            {
                if ((moveType & (MovementType.Tread | MovementType.Tire)) == 0)
                    return cost;

                return (type & (TerrainType.Plain | TerrainType.Forest)) != 0 ? cost + 1 : cost;
            }

            switch (moveType)
            {
                default:
                    return cost;

                case MovementType.Air:
                    return cost * 2;

                case MovementType.LightInf:
                    return (type & (TerrainType.Plain | TerrainType.Forest | TerrainType.Mountain)) != 0 ? cost * 2 : cost;

                case MovementType.HeavyInf:
                    return type == TerrainType.Mountain ? cost * 2 : cost;

                case MovementType.Lander:
                case MovementType.Sea:
                    return (type & (TerrainType.Sea | TerrainType.Building)) != 0 ? cost * 2 : cost;

                case MovementType.Tire:
                case MovementType.Tread:
                    return (type & (TerrainType.Plain | TerrainType.Forest)) != 0 ? cost + 1 : cost;
            }
        }

        private long? getPlayerIDFromCountryID(int countryID) => replayController.Players.FirstOrDefault(x => x.Value.OriginalCountryID == countryID).Value?.ID;

        public UnitData GetUnitDataForUnitName(string unitName) => unitStorage.GetUnitByCode(unitName);

        public void playMusic(String coName) {
            switch(coName) {
                case "Adder": if (!trackAdder.IsRunning) stopAllMusic(); trackAdder.Start(); break;
                case "Andy": if (!trackAndy.IsRunning) stopAllMusic(); trackAndy.Start(); break;
                case "Colin": if (!trackColin.IsRunning) stopAllMusic(); trackColin.Start(); break;
                case "Drake": if (!trackDrake.IsRunning) stopAllMusic(); trackDrake.Start(); break;
                case "Eagle": if (!trackEagle.IsRunning) stopAllMusic(); trackEagle.Start(); break;
                case "Flak": if (!trackFlak.IsRunning) stopAllMusic(); trackFlak.Start(); break;
                case "Grimm": if (!trackGrimm.IsRunning) stopAllMusic(); trackGrimm.Start(); break;
                case "Grit": if (!trackGrit.IsRunning) stopAllMusic(); trackGrit.Start(); break;
                case "Hachi": if (!trackHachi.IsRunning) stopAllMusic(); trackHachi.Start(); break;
                case "Hawke": if (!trackHawke.IsRunning) stopAllMusic(); trackHawke.Start(); break;
                case "Jake": if (!trackJake.IsRunning) stopAllMusic(); trackJake.Start(); break;
                case "Javier": if (!trackJavier.IsRunning) stopAllMusic(); trackJavier.Start(); break;
                case "Jess": if (!trackJess.IsRunning) stopAllMusic(); trackJess.Start(); break;
                case "Jugger": if (!trackJugger.IsRunning) stopAllMusic(); trackJugger.Start(); break;
                case "Kanbei": if (!trackKanbei.IsRunning) stopAllMusic(); trackKanbei.Start(); break;
                case "Kindle": if (!trackKindle.IsRunning) stopAllMusic(); trackKindle.Start(); break;
                case "Koal": if (!trackKoal.IsRunning) stopAllMusic(); trackKoal.Start(); break;
                case "Lash": if (!trackLash.IsRunning) stopAllMusic(); trackLash.Start(); break;
                case "Max": if (!trackMax.IsRunning) stopAllMusic(); trackMax.Start(); break;
                case "Nell": if (!trackNell.IsRunning) stopAllMusic(); trackNell.Start(); break;
                case "Olaf": if (!trackOlaf.IsRunning) stopAllMusic(); trackOlaf.Start(); break;
                case "Rachel": if (!trackRachel.IsRunning) stopAllMusic(); trackRachel.Start(); break;
                case "Sami": if (!trackSami.IsRunning) stopAllMusic(); trackSami.Start(); break;
                case "Sasha": if (!trackSasha.IsRunning) stopAllMusic(); trackSasha.Start(); break;
                case "Sensei": if (!trackSensei.IsRunning) stopAllMusic(); trackSensei.Start(); break;
                case "Sonja": if (!trackSonja.IsRunning) stopAllMusic(); trackSonja.Start(); break;
                case "VonBolt": if (!trackVonBolt.IsRunning) stopAllMusic(); trackVonBolt.Start(); break;
                default: if (!trackSami.IsRunning) stopAllMusic(); trackSami.Start(); break;
            }
        }
        public void stopAllMusic() {
            trackAdder.Seek(0);
            trackAndy.Seek(0);
            trackColin.Seek(0);
            trackDrake.Seek(0);
            trackEagle.Seek(0);
            trackFlak.Seek(0);
            trackGrimm.Seek(0);
            trackGrit.Seek(0);
            trackHachi.Seek(0);
            trackHawke.Seek(0);
            trackJake.Seek(0);
            trackJavier.Seek(0);
            trackJess.Seek(0);
            trackJugger.Seek(0);
            trackKanbei.Seek(0);
            trackKindle.Seek(0);
            trackKoal.Seek(0);
            trackLash.Seek(0);
            trackMax.Seek(0);
            trackNell.Seek(0);
            trackOlaf.Seek(0);
            trackPower.Seek(0);
            trackRachel.Seek(0);
            trackSami.Seek(0);
            trackSasha.Seek(0);
            trackSensei.Seek(0);
            trackSonja.Seek(0);
            trackSuperPower.Seek(0);
            trackVictory.Seek(0);
            trackVonBolt.Seek(0);

            trackAdder.Stop();
            trackAndy.Stop();
            trackColin.Stop();
            trackDrake.Stop();
            trackEagle.Stop();
            trackFlak.Stop();
            trackGrimm.Stop();
            trackGrit.Stop();
            trackHachi.Stop();
            trackHawke.Stop();
            trackJake.Stop();
            trackJavier.Stop();
            trackJess.Stop();
            trackJugger.Stop();
            trackKanbei.Stop();
            trackKindle.Stop();
            trackKoal.Stop();
            trackLash.Stop();
            trackMax.Stop();
            trackNell.Stop();
            trackOlaf.Stop();
            trackPower.Stop();
            trackRachel.Stop();
            trackSami.Stop();
            trackSasha.Stop();
            trackSensei.Stop();
            trackSonja.Stop();
            trackSuperPower.Stop();
            trackVictory.Stop();
            trackVonBolt.Stop();
        }

        public void playAttackSound(string attackerUnit, MovementType defenderType) {
            if (attackerUnit == "Bomber") soundBomb.Play(); else 
            if (attackerUnit == "Sub") soundSubMissile.Play(); else 
            if (attackerUnit == "Rocket" || attackerUnit == "Missile") 
                soundRocket.Play(); else 
            if (attackerUnit == "Artillery" || attackerUnit == "Battleship") 
                soundRocket.Play(); else 
            if (attackerUnit == "Fighter" || (attackerUnit == "B-Copter" && 
                (defenderType == MovementType.Tread || defenderType == MovementType.Tire || 
                defenderType == MovementType.Lander || defenderType == MovementType.Sea))) 
                soundMissile.Play(); else 
            if (((attackerUnit == "Tank" || attackerUnit == "Md. Tank" || 
                attackerUnit == "Neotank" || attackerUnit == "Mega Tank") && 
                (defenderType == MovementType.Tread || defenderType == MovementType.Tire || 
                defenderType == MovementType.Lander)) || (attackerUnit == "Cruiser" && 
                defenderType == MovementType.Sea )) soundCannon.Play(); else 
            soundMachineGun.Play();
        }
    }
}
