using DelaunatorSharp;
using Gilzoide.ManagedJobs;
using HarmonyLib;
using Ionic.Crc;
using Ionic.Zlib;
using JetBrains.Annotations;
using KTrie;
using LudeonTK;
using NVorbis.NAudioSupport;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using RuntimeAudioClipLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using static System.Collections.Specialized.BitVector32;
using static UnityEngine.GraphicsBuffer;

namespace DMSRC
{
	public class IncidentWorker_Sabotage : IncidentWorker
	{
		private static readonly SimpleCurve SaboteursCountFactorByPointsCurve = new SimpleCurve
		{
			new CurvePoint(2000f, 1f),
			new CurvePoint(4000f, 1.2f),
			new CurvePoint(6000f, 1.5f),
			new CurvePoint(8000f, 1.8f),
			new CurvePoint(10000f, 2f)
		};

		private static readonly SimpleCurve SaboteursCountByColonistsCurve = new SimpleCurve
		{
			new CurvePoint(2f, 1f),
			new CurvePoint(5f, 3f),
			new CurvePoint(10f, 5f)
		};

		protected override bool CanFireNowSub(IncidentParms parms)
		{
			if (base.CanFireNowSub(parms))
			{
				return GameComponent_Renegades.Find.PlayerRelation == FactionRelationKind.Hostile && (parms.target as Map).regionGrid.AllRooms?.Count > 2;
			}
			return false;
		}

		public override float ChanceFactorNow(IIncidentTarget target)
		{
			return base.ChanceFactorNow(target) * GameComponent_Renegades.Find.RaidCommonality(-1);
		}
		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			GameComponent_Renegades comp = GameComponent_Renegades.Find;
			if (comp == null || comp.PlayerRelation != FactionRelationKind.Hostile || comp.RenegadesFaction == null)
			{
				return false;
			}
			Map map = (Map)parms.target;
			parms.faction = comp.RenegadesFaction;
			int count = Mathf.Max(3, Mathf.RoundToInt(SaboteursCountFactorByPointsCurve.Evaluate(parms.points) * SaboteursCountByColonistsCurve.Evaluate(map.mapPawns.FreeColonistsSpawnedOrInPlayerEjectablePodsCount)));
			List<Pawn> list = GenerateSaboteurs(parms, count).ToList();
			LordMaker.MakeNewLord(parms.faction, new LordJob_Sabotage(), map, list);
			return true;
		}

		private IEnumerable<Pawn> GenerateSaboteurs(IncidentParms parms, int count)
		{
			Map map = (Map)parms.target;
			PawnGenerationRequest request = new PawnGenerationRequest(RCDefOf.DMSRC_Mech_Saboteur, parms.faction);
			for (int i = 0; i < count; i++)
			{
				Pawn pawn = PawnGenerator.GeneratePawn(request);
				if (pawn.inventory != null)
				{
					pawn.inventory.TryAddAndUnforbid(ThingMaker.MakeThing(RCDefOf.DMSRC_TimedBomb));
				}
				if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Hostile))
				{
					cell = DropCellFinder.FindRaidDropCenterDistant(map);
				}
				cell = CellFinder.RandomClosewalkCellNear(cell, map, 8);
				GenSpawn.Spawn(pawn, cell, map);
				yield return pawn;
			}
		}
	}

	public class LordJob_Sabotage : LordJob
	{
		public override StateGraph CreateGraph()
		{
			StateGraph stateGraph = new StateGraph();
			LordToil toil = new LordToil_Sabotage();
			stateGraph.AddToil(toil);
			return stateGraph;
		}
	}

	public class LordToil_Sabotage : LordToil
	{
		public override void UpdateAllDuties()
		{
			foreach (Pawn ownedPawn in lord.ownedPawns)
			{
				ownedPawn.mindState.duty = new PawnDuty(RCDefOf.DMSRC_Sabotage);
				ownedPawn.mindState.forcedGotoPosition = JobGiver_TryPlantBomb.GetPlantPosition(ownedPawn);
			}
		}
	}

	public class JobGiver_TryPlantBomb : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			if (pawn.CurJobDef == RCDefOf.DMSRC_PlantBomb)
			{
				return null;
			}
			Thing bomb = pawn.inventory?.innerContainer?.FirstOrDefault((x) => x.def == RCDefOf.DMSRC_TimedBomb) ?? pawn.carryTracker?.innerContainer?.FirstOrDefault((x) => x.def == RCDefOf.DMSRC_TimedBomb);
			if (bomb == null)
			{
				bomb = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(RCDefOf.DMSRC_TimedBomb), PathEndMode.Touch, TraverseParms.For(pawn), 7f);
				if (bomb == null)
				{
					pawn.mindState.forcedGotoPosition = IntVec3.Invalid;
					return null;
				}
				Job job1 = JobMaker.MakeJob(JobDefOf.TakeCountToInventory, bomb);
				job1.locomotionUrgency = LocomotionUrgency.Sprint;
				job1.count = 1;
				return job1;
			}
			IntVec3 forcedGotoPosition = pawn.mindState.forcedGotoPosition;
			if (!forcedGotoPosition.IsValid || (forcedGotoPosition != pawn.Position && !pawn.CanReach(forcedGotoPosition, PathEndMode.Touch, Danger.Deadly)))
			{
				pawn.mindState.forcedGotoPosition = GetPlantPosition(pawn);
				return null;
			}
			if (forcedGotoPosition.DistanceTo(pawn.Position) > 15f)
			{
				return null;
			}
			Job job2 = JobMaker.MakeJob(RCDefOf.DMSRC_PlantBomb, forcedGotoPosition, bomb);
			job2.count = 1;
			job2.locomotionUrgency = LocomotionUrgency.Sprint;
			return job2;
		}

		public static IntVec3 GetPlantPosition(Pawn pawn)
		{
			Map map = pawn.Map;
			if (Rand.Bool)
			{
				if (map.listerBuildings.allBuildingsColonist.Where((x) => x.def.Minifiable && x.MarketValue > 1000).TryRandomElementByWeight((y)=> y.MarketValue, out var b) && b.OccupiedRect().ExpandedBy(1).EdgeCells.TryRandomElement((c)=> c.GetAffordances(map).Contains(TerrainAffordanceDefOf.Light) && c.Standable(map),out var result))
				{
					return result;
				}
			}
			Room room = map.regionGrid.AllRooms?.RandomElementByWeight((r) => r.GetStat(RoomStatDefOf.Wealth));
			if (room == null)
			{
				return IntVec3.Invalid;
			}
			if (room.Cells.TryRandomElement((c) => c.GetAffordances(map).Contains(TerrainAffordanceDefOf.Light) && c.Standable(map), out var cell))
			{
				return cell;
			}
			return IntVec3.Invalid;
		}
	}

	public class JobGiver_ForcedGotoSprint : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			IntVec3 forcedGotoPosition = pawn.mindState.forcedGotoPosition;
			if (!forcedGotoPosition.IsValid)
			{
				return null;
			}
			if (!pawn.CanReach(forcedGotoPosition, PathEndMode.ClosestTouch, Danger.Deadly))
			{
				return null;
			}
			Job job = JobMaker.MakeJob(RCDefOf.DSMRC_GotoSprint, forcedGotoPosition);
			job.locomotionUrgency = LocomotionUrgency.Sprint;
			return job;
		}
	}

	public class JobDriver_GotoNoReset : JobDriver_Goto
	{
		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			pawn.Map.pawnDestinationReservationManager.Reserve(pawn, job, job.targetA.Cell);
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			Toil toil = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
			toil.FailOn(() => job.GetTarget(TargetIndex.A).Thing is Pawn pawn && pawn.ParentHolder is Corpse);
			toil.FailOn(() => job.GetTarget(TargetIndex.A).Thing?.Destroyed ?? false);
			toil.tickAction = (Action)Delegate.Combine(toil.tickAction, (Action)delegate
			{
				if (!pawn.IsPsychologicallyInvisible() && pawn.GetRoom()?.GetStat(RoomStatDefOf.Wealth) > 500f)
				{
					pawn.mindState.forcedGotoPosition = pawn.Position;
					EndJobWith(JobCondition.InterruptForced);
				}
			});
			yield return toil;
		}
	}
}