using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DMSRC
{
	[HarmonyPatch(typeof(FloatMenuOptionProvider_DraftedMove), nameof(FloatMenuOptionProvider_DraftedMove.PawnGotoAction))]
	public static class Patch_PawnGotoAction
	{
		[HarmonyPrefix]
		public static bool Prefix(IntVec3 clickCell, Pawn pawn, IntVec3 gotoLoc)
		{
			if (pawn is ICaravanOwner owner && owner.CanCaravan)
			{
				bool flag;
				if (pawn.Position == gotoLoc || (pawn.CurJobDef == JobDefOf.Goto && pawn.CurJob.targetA.Cell == gotoLoc))
				{
					flag = true;
				}
				else
				{
					Job job = JobMaker.MakeJob(JobDefOf.Goto, gotoLoc);
					if (pawn.Map.exitMapGrid.IsExitCell(clickCell))
					{
						job.exitMapOnArrival = true;
					}
					else if (!pawn.Map.IsPlayerHome && !pawn.Map.exitMapGrid.MapUsesExitGrid && CellRect.WholeMap(pawn.Map).IsOnEdge(clickCell, 3) && pawn.Map.Parent.GetComponent<FormCaravanComp>() != null && MessagesRepeatAvoider.MessageShowAllowed("MessagePlayerTriedToLeaveMapViaExitGrid-" + pawn.Map.uniqueID, 60f))
					{
						if (pawn.Map.Parent.GetComponent<FormCaravanComp>().CanFormOrReformCaravanNow)
						{
							Messages.Message("MessagePlayerTriedToLeaveMapViaExitGrid_CanReform".Translate(), pawn.Map.Parent, MessageTypeDefOf.RejectInput, historical: false);
						}
						else
						{
							Messages.Message("MessagePlayerTriedToLeaveMapViaExitGrid_CantReform".Translate(), pawn.Map.Parent, MessageTypeDefOf.RejectInput, historical: false);
						}
					}
					flag = pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
				}
				if (flag)
				{
					FleckMaker.Static(gotoLoc, pawn.Map, FleckDefOf.FeedbackGoto);
				}
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AnyPawnBlockingMapRemoval), MethodType.Getter)]
	public class Patch_AnyPawnBlockingMapRemoval
	{
		[HarmonyPostfix]
		public static void Postfix(ref bool __result, MapPawns __instance)
		{
			if (__result) return;
			foreach (Pawn item in __instance.AllPawns)
			{
				if (item is ICaravanOwner owner && owner.CanCaravan)
				{
					__result = true;
					return;
				}
			}
		}
	}


	[HarmonyPatch(typeof(CaravanExitMapUtility), nameof(CaravanExitMapUtility.ExitMapAndJoinOrCreateCaravan))]
	public static class Patch_ExitMapAndJoinOrCreateCaravan
	{
		[HarmonyPrefix]
		[HarmonyPriority(501)]
		public static bool Prefix(Pawn pawn, Rot4 exitDir)
		{
			if (pawn is ICaravanOwner owner && owner.CanCaravan)
			{
				Caravan caravan = CaravanExitMapUtility.FindCaravanToJoinFor(pawn);
				if (caravan != null)
				{
					//CaravanExitMapUtility.AddCaravanExitTaleIfShould(pawn);
					caravan.AddPawn(pawn, addCarriedPawnToWorldPawnsIfAny: true);
					pawn.ExitMap(allowedToJoinOrCreateCaravan: false, exitDir);
				}
				else
				{
					Map map = pawn.Map;
					PlanetTile directionTile = (PlanetTile)findRandomStartingTileBasedOnExitDir.Invoke(null, new object[2] { map.Tile, exitDir });
					Caravan caravan2 = CaravanExitMapUtility.ExitMapAndCreateCaravan(Gen.YieldSingle(pawn), pawn.Faction, map.Tile, directionTile, PlanetTile.Invalid, sendMessage: false);
					caravan2.autoJoinable = true;
					bool flag = false;
					IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
					for (int i = 0; i < allPawnsSpawned.Count; i++)
					{
						if (CaravanExitMapUtility.FindCaravanToJoinFor(allPawnsSpawned[i]) != null && !allPawnsSpawned[i].Downed && !allPawnsSpawned[i].Drafted)
						{
							if (allPawnsSpawned[i].IsAnimal)
							{
								flag = true;
							}
							RestUtility.WakeUp(allPawnsSpawned[i]);
							allPawnsSpawned[i].jobs.CheckForJobOverride();
						}
					}
					TaggedString taggedString = "MessagePawnLeftMapAndCreatedCaravan".Translate(pawn.LabelShort, pawn).CapitalizeFirst();
					if (flag)
					{
						taggedString += " " + "MessagePawnLeftMapAndCreatedCaravan_AnimalsWantToJoin".Translate();
					}
					Messages.Message(taggedString, caravan2, MessageTypeDefOf.TaskCompletion);
				}
				return false;
			}
			return true;
		}

		public static MethodInfo findRandomStartingTileBasedOnExitDir = AccessTools.Method(typeof(CaravanExitMapUtility), "FindRandomStartingTileBasedOnExitDir", new Type[2] { typeof(PlanetTile), typeof(Rot4) }, (Type[])null);
	}

	[HarmonyPatch(typeof(CaravanExitMapUtility), "CanExitMapAndJoinOrCreateCaravanNow")]
	public static class Patch_CanExitMapAndJoinOrCreateCaravanNow
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, ref bool __result)
		{
			if (__result || !pawn.Spawned)
			{
				return;
			}
			if (!pawn.Map.exitMapGrid.MapUsesExitGrid)
			{
				return;
			}
			if (pawn is ICaravanOwner owner && owner.CanCaravan)
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(JobDriver_PrepareCaravan_GatherItems), nameof(JobDriver_PrepareCaravan_GatherItems.IsUsableCarrier))]
	public static class Patch_IsUsableCarrier
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn p, Pawn forPawn, bool allowColonists, ref bool __result)
		{
			if (__result)
			{
				return;
			}
			if (!p.IsFormingCaravan())
			{
				return;
			}
			if (p.DestroyedOrNull() || !p.Spawned || p.inventory.UnloadEverything || !forPawn.CanReach(p, PathEndMode.Touch, Danger.Deadly))
			{
				return;
			}
			if (allowColonists && p is ICaravanOwner owner && owner.CanCaravan)
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch]
	public static class Patch_CheckForErrors
	{
		public static MethodBase TargetMethod()
		{
			return AccessTools.Method(AccessTools.Inner(typeof(Dialog_FormCaravan), "<>c__DisplayClass95_0"), "<CheckForErrors>b__1");
		}

		public static void Postfix(Pawn x, ref bool __result)
		{
			if (!__result)
			{
				__result = x is ICaravanOwner owner && owner.CanCaravan;
			}
		}
	}

	[HarmonyPatch(typeof(CaravanFormingUtility), nameof(CaravanFormingUtility.AllItemsLoadedOntoCaravan))]
	public static class Patch_LordToilTick_Patch
	{
		public static void Postfix(Lord lord, Map map, ref bool __result)
		{
			if (!__result)
			{
				return;
			}
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				if (lord.ownedPawns[i] is ICaravanOwner && lord.ownedPawns[i].mindState.lastJobTag != JobTag.WaitingForOthersToFinishGatheringItems)
				{
					__result = false;
					return;
				}
			}
			IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
			for (int j = 0; j < allPawnsSpawned.Count; j++)
			{
				if (allPawnsSpawned[j].CurJob != null && allPawnsSpawned[j].jobs.curDriver is JobDriver_PrepareCaravan_GatherItems && allPawnsSpawned[j].CurJob.lord == lord)
				{
					__result = false;
					return;
				}
			}
		}
	}

	[HarmonyPatch(typeof(LordToil_PrepareCaravan_GatherItems), "UpdateAllDuties")]
	public static class Patch_LordToil_PrepareCaravan_GatherItems
	{
		public static FieldInfo meetingPoint = AccessTools.Field(typeof(LordToil_PrepareCaravan_GatherItems), "meetingPoint");

		[HarmonyPostfix]
		public static void Postfix(LordToil_PrepareCaravan_GatherDownedPawns __instance)
		{
			for (int i = 0; i < __instance.lord.ownedPawns.Count; i++)
			{
				Pawn pawn = __instance.lord.ownedPawns[i];
				if (pawn is ICaravanOwner)
				{
					pawn.mindState.duty = new PawnDuty(DutyDefOf.PrepareCaravan_GatherItems, (IntVec3)meetingPoint.GetValue(__instance));
				}
			}
		}
	}

	[HarmonyPatch(typeof(LordToil_PrepareCaravan_GatherDownedPawns), "UpdateAllDuties")]
	public static class Patch_LordToil_PrepareCaravan_GatherDownedPawns
	{
		public static FieldInfo meetingPoint = AccessTools.Field(typeof(LordToil_PrepareCaravan_GatherDownedPawns), "meetingPoint");

		public static FieldInfo exitSpot = AccessTools.Field(typeof(LordToil_PrepareCaravan_GatherDownedPawns), "exitSpot");

		[HarmonyPostfix]
		public static void Postfix(LordToil_PrepareCaravan_GatherDownedPawns __instance)
		{
			for (int i = 0; i < __instance.lord.ownedPawns.Count; i++)
			{
				Pawn pawn = __instance.lord.ownedPawns[i];
				if (pawn is ICaravanOwner)
				{
					pawn.mindState.duty = new PawnDuty(DutyDefOf.PrepareCaravan_GatherDownedPawns, (IntVec3)meetingPoint.GetValue(__instance), (IntVec3)exitSpot.GetValue(__instance));
				}
			}
		}
	}

	[HarmonyPatch(typeof(CaravanUtility), "IsOwner")]
	public static class Patch_CaravanUtility
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, Faction caravanFaction, ref bool __result)
		{
			if (__result)
			{
				return;
			}
			if (caravanFaction == null)
			{
				return;
			}
			if (pawn is ICaravanOwner owner && owner.CanCaravan && pawn.Faction == caravanFaction && pawn.HostFaction == null)
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(SettleInExistingMapUtility), "SettleCommand")]
	public static class Patch_SettleInExistingMapUtility
	{
		[HarmonyPostfix]
		public static void Postfix(Map map, bool requiresNoEnemies, ref Command __result)
		{
			if (__result.disabledReason == "CommandSettleFailNoColonists".Translate() && map.mapPawns.SpawnedColonyMechs.Any((Pawn x) => x is ICaravanOwner owner && owner.CanCaravan && !x.Downed))
			{
				if (requiresNoEnemies)
				{
					foreach (IAttackTarget item in map.attackTargetsCache.TargetsHostileToColony)
					{
						if (GenHostility.IsActiveThreatToPlayer(item))
						{
							__result.Disable("CommandSettleFailEnemies".Translate());
							return;
						}
					}
				}
				__result.disabledReason = null;
				__result.Disabled = false;
			}
		}
	}
}
