using Fortified.Structures;
using RimWorld;
using RimWorld.SketchGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace DMSRC
{
	public class ScenPart_PlayerArrivesRenegadesStructure : ScenPart
	{
		public List<FFF_StructureDef> structures = new List<FFF_StructureDef>();

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref structures, "structures", LookMode.Def);
		}

		public override void GenerateIntoMap(Map map)
		{
			if (Find.GameInitData == null || structures.NullOrEmpty())
			{
				return;
			}
			RPrefabDef prefab = null;
			List<Thing> things = new List<Thing>();
			List<Pawn> pawns = new List<Pawn>();
			List<Pawn> mechs = new List<Pawn>();
			foreach (ScenPart allPart in Find.Scenario.AllParts)
			{
				things.AddRange(allPart.PlayerStartingThings());
			}
			foreach (Pawn startingPawn in Find.GameInitData.startingAndOptionalPawns)
			{
				pawns.Add(startingPawn);
				foreach (ThingDefCount item in Find.GameInitData.startingPossessions[startingPawn])
				{
					startingPawn.inventory.GetDirectlyHeldThings().TryAdd(StartingPawnUtility.GenerateStartingPossession(item));
				}
			}
			foreach (Thing t in things.ToList())
			{
				if (t is Pawn p)
				{
					if (p.RaceProps.IsMechanoid)
					{
						mechs.Add(p);
						p.equipment.DestroyAllEquipment();
						p.apparel.DestroyAll();
						p.inventory.DestroyAll();
					}
					else
					{
						pawns.Add(p);
					}
					things.Remove(t);
				}
			}
			IOverseer overseer = mechs.FirstOrDefault((x) => x is IOverseer) as IOverseer;
			overseer.Comp.UpdateDummy();
			if (overseer != null)
			{
				foreach (Pawn p in mechs)
				{
					if (p != overseer)
					{
						overseer.Comp.Connect(p, overseer.Comp.dummyPawn);
					}
				}
			}
			IntVec3 spot = MapGenerator.PlayerStartSpot;
			List<Thing> generated = new List<Thing>();
			Rot4 rot = Rot4.Random;
			IntVec3 root = PrefabUtility.GetRoot(prefab, spot, rot);
			Thing.allowDestroyNonDestroyable = true;
			prefab.Generate(spot, rot, map, Faction.OfPlayerSilentFail, ref generated);
			List<IntVec3> itemCells = new List<IntVec3>();
			List<IntVec3> spawnCells = new List<IntVec3>();
			List<Thing> beacons = new List<Thing>();
			beacons.AddRange(generated.Where((x) => x is Building_OrbitalTradeBeacon));
			foreach (Thing t in generated.ToList().InRandomOrder())
			{
				CellRect cellRect = new CellRect(t.Position.x - t.RotatedSize.x / 2 - 4, t.Position.z - t.RotatedSize.z / 2 - 4, t.RotatedSize.x + 8, t.RotatedSize.z + 8);
				cellRect.ClipInsideMap(t.Map);
				foreach (IntVec3 item in cellRect)
				{
					t.Map.areaManager.Home[item] = true;
				}
				if (t is Building_CryptosleepCasket)
				{
					spawnCells.Add(t.InteractionCell);
				}
				if (t.TryGetComp<CompRefuelable>(out var comp))
				{
					if (comp.Props.fuelIsMortarBarrel)
					{
						comp.Refuel(comp.Props.fuelCapacity - comp.Fuel);
					}
					else
					{
						comp.ConsumeFuel(comp.Fuel);
					}
				}
				else if (t is InactiveMech m)
				{
					if (!mechs.NullOrEmpty())
					{
						Pawn mech = mechs.RandomElement();
						mech.Rotation = Rot4.Random;
						m.innerContainer.Clear();
						m.innerContainer.TryAddOrTransfer(mech);
						mechs.Remove(mech);
					}
					else
					{
						m.Destroy();
					}
				}
				else if (t.def.building.maxItemsInCell > 1 && !t.def.preventDroppingThingsOn && beacons.Any((b) => b.Position.InHorDistOf(t.Position, 7.9f)))
				{
					itemCells.AddRange(t.OccupiedRect().Cells);
				}
			}
			foreach (Thing thing in things)
			{
				GenPlace.TryPlaceThing(thing, itemCells.RandomElement(), map, ThingPlaceMode.Near);
			}
			if (!mechs.NullOrEmpty())
			{
				pawns.AddRange(mechs);
			}
			foreach (Pawn p in pawns)
			{
				IntVec3 c = spawnCells.RandomElement();
				GenSpawn.Spawn(p, c, map);
				spawnCells.Remove(c);
			}
			map.powerNetManager.UpdatePowerNetsAndConnections_First();
			GenStep_RPrefab.UpdateDesiredPowerOutputForAllGenerators(map);
		}
	}

	public class ScenPart_PlayerArrivesPrefab : ScenPart
	{
		public List<RPrefabDef> prefabOptions = new List<RPrefabDef>();

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref prefabOptions, "prefabOptions", LookMode.Def);
		}

		public override void GenerateIntoMap(Map map)
		{
			if (Find.GameInitData == null || prefabOptions.NullOrEmpty())
			{
				return;
			}
			RPrefabDef prefab = prefabOptions.RandomElement();
			List<Thing> things = new List<Thing>();
			List<Pawn> pawns = new List<Pawn>();
			List<Pawn> mechs = new List<Pawn>();
			foreach (ScenPart allPart in Find.Scenario.AllParts)
			{
				things.AddRange(allPart.PlayerStartingThings());
			}
			foreach (Pawn startingPawn in Find.GameInitData.startingAndOptionalPawns)
			{
				pawns.Add(startingPawn);
				foreach (ThingDefCount item in Find.GameInitData.startingPossessions[startingPawn])
				{
					startingPawn.inventory.GetDirectlyHeldThings().TryAdd(StartingPawnUtility.GenerateStartingPossession(item));
				}
			}
			foreach (Thing t in things.ToList())
			{
				if (t is Pawn p)
				{
					if (p.RaceProps.IsMechanoid)
					{
						mechs.Add(p);
						p.equipment.DestroyAllEquipment();
						p.apparel.DestroyAll();
						p.inventory.DestroyAll();
					}
					else
					{
						pawns.Add(p);
					}
					things.Remove(t);
				}
			}
			IOverseer overseer = mechs.FirstOrDefault((x) => x is IOverseer) as IOverseer;
			overseer.Comp.UpdateDummy();
			if (overseer != null)
			{
				foreach (Pawn p in mechs)
				{
					if (p != overseer)
					{
						overseer.Comp.Connect(p, overseer.Comp.dummyPawn);
					}
				}
			}
			IntVec3 spot = MapGenerator.PlayerStartSpot;
			List<Thing> generated = new List<Thing>();
			Rot4 rot = Rot4.Random;
			IntVec3 root = PrefabUtility.GetRoot(prefab, spot, rot);
			Thing.allowDestroyNonDestroyable = true;
			prefab.Generate(spot, rot, map, Faction.OfPlayerSilentFail, ref generated);
			List<IntVec3> itemCells = new List<IntVec3>();
			List<IntVec3> spawnCells = new List<IntVec3>();
			List<Thing> beacons = new List<Thing>();
			beacons.AddRange(generated.Where((x) => x is Building_OrbitalTradeBeacon));
			foreach (Thing t in generated.ToList().InRandomOrder())
			{
				CellRect cellRect = new CellRect(t.Position.x - t.RotatedSize.x / 2 - 4, t.Position.z - t.RotatedSize.z / 2 - 4, t.RotatedSize.x + 8, t.RotatedSize.z + 8);
				cellRect.ClipInsideMap(t.Map);
				foreach (IntVec3 item in cellRect)
				{
					t.Map.areaManager.Home[item] = true;
				}
				if (t is Building_CryptosleepCasket)
				{
					spawnCells.Add(t.InteractionCell);
				}
				if (t.TryGetComp<CompRefuelable>(out var comp))
				{
					if (comp.Props.fuelIsMortarBarrel)
					{
						comp.Refuel(comp.Props.fuelCapacity - comp.Fuel);
					}
					else
					{
						comp.ConsumeFuel(comp.Fuel);
					}
				}
				else if (t is InactiveMech m)
				{
					if (!mechs.NullOrEmpty())
					{
						Pawn mech = mechs.RandomElement();
						mech.Rotation = Rot4.Random;
						m.innerContainer.Clear();
						m.innerContainer.TryAddOrTransfer(mech);
						mechs.Remove(mech);
					}
					else
					{
						m.Destroy();
					}
				}
				else if (t.def.building.maxItemsInCell > 1 && !t.def.preventDroppingThingsOn && beacons.Any((b) => b.Position.InHorDistOf(t.Position, 7.9f)))
				{
					itemCells.AddRange(t.OccupiedRect().Cells);
				}
			}
			foreach (Thing thing in things)
			{
				GenPlace.TryPlaceThing(thing, itemCells.RandomElement(), map, ThingPlaceMode.Near);
			}
			if (!mechs.NullOrEmpty())
			{
				pawns.AddRange(mechs);
			}
			foreach (Pawn p in pawns)
			{
				IntVec3 c = spawnCells.RandomElement();
				GenSpawn.Spawn(p, c, map);
				spawnCells.Remove(c);
			}
			map.powerNetManager.UpdatePowerNetsAndConnections_First();
			GenStep_RPrefab.UpdateDesiredPowerOutputForAllGenerators(map);
		}
	}

	public class ScenPart_Renegades : ScenPart
	{
		public bool startContacted = false;

		public int goodwill = 0;

		public FactionRelationKind relations = FactionRelationKind.Neutral;

		public bool enemyWithFleet;

		public IntRange contactInDaysRange = IntRange.Invalid;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref startContacted, "startContacted");
			Scribe_Values.Look(ref goodwill, "goodwill");
			Scribe_Values.Look(ref relations, "relations");
			Scribe_Values.Look(ref enemyWithFleet, "enemyWithFleet", defaultValue: false);
			Scribe_Values.Look(ref contactInDaysRange, "contactInDaysRange", defaultValue: IntRange.Invalid);
		}

		public override void PostWorldGenerate()
		{
			base.PostWorldGenerate();
			Apply();
		}

		public void Apply()
		{
			GameComponent_Renegades comp = GameComponent_Renegades.Find;
			if (comp == null)
			{
				return;
			}
			Faction fleet = comp.DMSFaction;
			Faction player = Faction.OfPlayerSilentFail;
			if (enemyWithFleet && fleet != null)
			{
				fleet.SetRelation(new FactionRelation(player, FactionRelationKind.Hostile) { baseGoodwill = -200 });
				Faction.OfPlayerSilentFail?.TryAffectGoodwillWith(fleet, -200, canSendMessage: false, canSendHostilityLetter: false, RCDefOf.DMSRC_AllyWithRenegades);
				comp.enemyWithFleet = true;
			}
			comp.PlayerRelation = relations;
			comp.playerGoodwill = goodwill;
			if (startContacted)
			{
				comp.contact = true;
			}
			else if (contactInDaysRange != IntRange.Invalid)
			{
				comp.ticksTillContact = contactInDaysRange.RandomInRange * 24;
			}
		}
	}
}