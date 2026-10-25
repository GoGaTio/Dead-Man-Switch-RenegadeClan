using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace DMSRC
{
	public class PawnsArrivalModeWorker_EmergeFromRealityRift : PawnsArrivalModeWorker
	{
		public static readonly FloatRange StunRange = new FloatRange(2f, 4f);

		public override bool CanUseWith(IncidentParms parms)
		{
			return parms.faction.def == RCDefOf.DMSRC_RenegadeClan;
		}

		public override bool CanUseOnTile(PlanetTile tile)
		{
			return true;
		}

		public override void Arrive(List<Pawn> pawns, IncidentParms parms)
		{
			if (!pawns.Empty())
			{
				Map map = (Map)parms.target;
				List<IntVec3> list = new List<IntVec3>();
				for (int i = 0; i < pawns.Count; i++)
				{
					list.Add(CellRect.FromCell(parms.spawnCenter).ExpandedBy(1).EdgeCells.RandomElement());
					pawns[i].stances.stunner.StunFor(StunRange.RandomInRangeSeeded(pawns[i].ThingID.GetHashCode()).SecondsToTicks(), pawns[i], addBattleLog: true, showMote: false);
				}
				float num = 8f / (float)pawns.Count;
				SpawnRequest spawnRequest = new SpawnRequest(pawns.Cast<Thing>().ToList(), list, 1, num);
				spawnRequest.spawnSound = SoundDefOf.Psycast_Skip_Entry;
				spawnRequest.spawnEffect = EffecterDefOf.Skip_Exit;
				spawnRequest.initialDelay = 300;
				spawnRequest.lord = parms.lord;
				map.deferredSpawner.AddRequest(spawnRequest, false);
				GenSpawn.Spawn(RCDefOf.DMSRC_RealityRift, parms.spawnCenter, map);
			}
		}

		public override bool TryResolveRaidSpawnCenter(IncidentParms parms)
		{
			Map map = (Map)parms.target;
			int num = 20;
			while (true)
			{
				if (parms.spawnCenter != IntVec3.Invalid || num <= 0)
				{
					break;
				}
				parms.spawnCenter = TryFindRandomSpotInsideColony(map);
				num--;
			}
			return parms.spawnCenter.IsValid;
		}

		public static IntVec3 TryFindRandomSpotInsideColony(Map map, Predicate<IntVec3> extraValidator = null)
		{
			IEnumerable<Building> source = map.listerBuildings.allBuildingsColonist.Where((Building b) => b.def.building.ai_chillDestination);
			bool desperate;
			int walkRadius;
			int walkRadiusMaxImpassable;
			int minColonyBuildingsLOS;
			for (int num = 0; num < 120; num++)
			{
				if (!source.TryRandomElement(out var result2))
				{
					break;
				}
				desperate = num > 60;
				walkRadius = 6 - num / 20;
				walkRadiusMaxImpassable = 6 - num / 20;
				minColonyBuildingsLOS = 5 - num / 30;
				if (CellFinder.TryFindRandomCellNear(result2.Position, map, 10, FinalValidator, out var result, 50))
				{
					return result;
				}
			}
			List<Building> allBuildingsColonist = map.listerBuildings.allBuildingsColonist;
			for (int num2 = 0; num2 < 120; num2++)
			{
				if (!allBuildingsColonist.TryRandomElement(out var result3))
				{
					break;
				}
				desperate = num2 > 60;
				walkRadius = 6 - num2 / 20;
				walkRadiusMaxImpassable = 6 - num2 / 20;
				minColonyBuildingsLOS = 4 - num2 / 30;
				if (CellFinder.TryFindRandomCellNear(result3.Position, map, 15, FinalValidator, out var result6, 50))
				{
					return result6;
				}
			}
			for (int num3 = 0; num3 < 50; num3++)
			{
				if (!map.mapPawns.FreeColonistsAndPrisonersSpawned.TryRandomElement(out var result4))
				{
					break;
				}
				desperate = num3 > 25;
				walkRadius = 3;
				walkRadiusMaxImpassable = 6;
				minColonyBuildingsLOS = 0;
				if (CellFinder.TryFindRandomCellNear(result4.Position, map, 15, FinalValidator, out var result5, 50))
				{
					return result5;
				}
			}
			desperate = true;
			walkRadius = 3;
			walkRadiusMaxImpassable = 6;
			minColonyBuildingsLOS = 0;
			if (CellFinderLoose.TryGetRandomCellWith(FinalValidator, map, 1000, out var result7))
			{
				return result7;
			}
			return IntVec3.Invalid;
			bool FinalValidator(IntVec3 c)
			{
				if (!c.Standable(map))
				{
					return false;
				}
				District district = c.GetDistrict(map);
				if (district.TouchesMapEdge)
				{
					return false;
				}
				if (district.CellCount < 60)
				{
					return false;
				}
				if (!desperate && !map.reachability.CanReachColony(c))
				{
					return false;
				}
				if (extraValidator != null && !extraValidator(c))
				{
					return false;
				}
				int num4 = 0;
				foreach (IntVec3 item in CellRect.CenteredOn(c, walkRadius))
				{
					District district2 = item.GetDistrict(map);
					if (district2 != district)
					{
						num4++;
						if (!desperate && district2 != null && district2.IsDoorway)
						{
							return false;
						}
					}
					if (num4 > walkRadiusMaxImpassable)
					{
						return false;
					}
				}
				if (minColonyBuildingsLOS > 0)
				{
					int colonyBuildingsLOSFound = 0;
					tmpBuildings.Clear();
					RegionTraverser.BreadthFirstTraverse(c, map, (Region from, Region to) => true, delegate (Region reg)
					{
						Faction ofPlayer = Faction.OfPlayer;
						List<Thing> list = reg.ListerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
						for (int i = 0; i < list.Count; i++)
						{
							Thing thing = list[i];
							if (thing.Faction == ofPlayer && thing.Position.InHorDistOf(c, 16f) && GenSight.LineOfSight(thing.Position, c, map, skipFirstCell: true) && tmpBuildings.Add(thing))
							{
								colonyBuildingsLOSFound++;
								if (colonyBuildingsLOSFound >= minColonyBuildingsLOS)
								{
									return true;
								}
							}
						}
						return false;
					}, 12);
					tmpBuildings.Clear();
					if (colonyBuildingsLOSFound < minColonyBuildingsLOS)
					{
						return false;
					}
				}
				return true;
			}
		}

		private static HashSet<Thing> tmpBuildings = new HashSet<Thing>();
	}
}
