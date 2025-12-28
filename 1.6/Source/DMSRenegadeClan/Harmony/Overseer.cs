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
using System.Reflection;
using System.Reflection.Emit;
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
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;

namespace DMSRC
{
    [HarmonyPatch(typeof(FloatMenuOptionProvider_DraftedMove), nameof(FloatMenuOptionProvider_DraftedMove.PawnGotoAction))]
    public static class Patch_PawnGotoAction
    {
        [HarmonyPrefix]
        public static bool Prefix(IntVec3 clickCell, Pawn pawn, IntVec3 gotoLoc)
        {
            if (pawn is OverseerMech && pawn.IsColonyMech)
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

    [HarmonyPatch(typeof(JobGiver_WanderOverseer), "Target")]
    public static class Patch_JobGiver_WanderOverseer
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref GlobalTargetInfo __result)
        {
            if (__result.Pawn == null) return;
            CompOverseerMech comp = OverseerMechUtility.GetOverseerMech(__result.Pawn);
            if (comp != null)
            {
                __result = comp.Parent;
            }
        }
    }

    [HarmonyPatch(typeof(JobGiver_AIDefendOverseer), "GetDefendee")]
    public static class Patch_JobGiver_AIDefendOverseer
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref Pawn __result)
        {
            if (__result == null) return;
            CompOverseerMech comp = OverseerMechUtility.GetOverseerMech(__result);
            if (comp != null)
            {
                __result = comp.Parent;
            }
        }
    }

    [HarmonyPatch(typeof(JobGiver_AIFollowOverseer), "GetFollowee")]
    public static class Patch_JobGiver_AIFollowOverseer
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref Pawn __result)
        {
            if (__result == null) return;
            CompOverseerMech comp = OverseerMechUtility.GetOverseerMech(__result);
            if (comp != null)
            {
                __result = comp.Parent;
            }
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
                if (item is OverseerMech)
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
            if (pawn is OverseerMech && pawn.IsColonyMech)
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
                    int directionTile = (int)findRandomStartingTileBasedOnExitDir.Invoke(null, new object[2] { map.Tile, exitDir });
                    Caravan caravan2 = CaravanExitMapUtility.ExitMapAndCreateCaravan(Gen.YieldSingle(pawn), pawn.Faction, map.Tile, directionTile, -1, sendMessage: false);
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

        public static MethodInfo findRandomStartingTileBasedOnExitDir = AccessTools.Method(typeof(CaravanExitMapUtility), "FindRandomStartingTileBasedOnExitDir", new Type[2] { typeof(int), typeof(Rot4) }, (Type[])null);
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
            if (pawn is OverseerMech && pawn.IsColonyMech)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Dialog_FormCaravan), "ShouldShowWarningForMechWithoutMechanitor")]
    public static class Patch_ShouldShowWarningForMechWithoutMechanitor
    {

        private static List<Pawn> tmpPawnsToTransfer = new List<Pawn>();

        [HarmonyPrefix]
        public static bool Prefix(ref bool __result, List<TransferableOneWay> ___transferables)
        {
            foreach (TransferableOneWay transferable in ___transferables)
            {
                if (transferable.HasAnyThing && transferable.AnyThing is OverseerMech)
                {
                    __result = false;
                    return false;
                }
            }
            return true;
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
            if (allowColonists && p is OverseerMech && p.IsColonyMech)
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
                __result = x is OverseerMech;
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
				if (lord.ownedPawns[i] is OverseerMech pawn && pawn.mindState.lastJobTag != JobTag.WaitingForOthersToFinishGatheringItems)
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
                if (pawn is OverseerMech)
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
                if (pawn is OverseerMech)
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.PrepareCaravan_GatherDownedPawns, (IntVec3)meetingPoint.GetValue(__instance), (IntVec3)exitSpot.GetValue(__instance));
                }
            }
        }
    }

    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Interval))]
    public static class Patch_SkillRecord
    {
        [HarmonyPrefix]
        [HarmonyPriority(501)]
        public static bool Interval(Pawn ___pawn)
        {
            return ___pawn != null && ___pawn.GetComp<CompOverseerMech>() == null;
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
            if (pawn is OverseerMech && pawn.Faction == caravanFaction && pawn.HostFaction == null)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(CaravanExitMapUtility), "FindCaravanToJoinFor")]
    public static class Patch_FindCaravan
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref Caravan __result)
        {
            if (__result != null)
            {
                return;
            }
            if (!pawn.IsColonyMech)
            {
                return;
            }
            Pawn overseer = pawn.GetOverseer();
            if (overseer == null || overseer.kindDef != RCDefOf.DMSRC_DummyMechanitor)
            {
                return;
            }
            CompOverseerMech comp = OverseerMechUtility.GetOverseerMech(overseer);
            if (comp == null)
            {
                return;
            }
            if (!pawn.Spawned || !pawn.CanReachMapEdge() || pawn.Map.IsPocketMap)
            {
                return;
            }
			List<PlanetTile> tmpNeighbors = new List<PlanetTile>();
			PlanetTile tile = pawn.Map.Tile;
			Find.WorldGrid.GetTileNeighbors(tile, tmpNeighbors);
			tmpNeighbors.Add(tile);
			List<Caravan> caravans = Find.WorldObjects.Caravans;
			for (int i = 0; i < caravans.Count; i++)
			{
				Caravan caravan = caravans[i];
				if (!tmpNeighbors.Contains(caravan.Tile) || !caravan.autoJoinable)
				{
					continue;
				}
				if (pawn.GetMechWorkMode() == MechWorkModeDefOf.Escort)
				{
					if (caravan.PawnsListForReading.Contains(comp.Parent))
					{
						__result = caravan;
					}
				}
			}
		}
    }

    [HarmonyPatch(typeof(ThinkNode_ConditionalWorkMode), "Satisfied")]
    public static class Patch_ThinkNode
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref bool __result, ThinkNode_ConditionalWorkMode __instance)
        {
            if (pawn is OverseerMech mech && mech.Comp.WorkMode == __instance.workMode)
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
            if (__result.disabledReason == "CommandSettleFailNoColonists".Translate() && map.mapPawns.SpawnedColonyMechs.Any((Pawn x) => x is OverseerMech && !x.Downed))
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

    [HarmonyPatch(typeof(CompOverseerSubject), "State", MethodType.Getter)]
    public static class Patch_Overseer
    {
        [HarmonyPostfix]
        public static void Postfix(ref OverseerSubjectState __result, CompOverseerSubject __instance)
        {
            if (__instance.parent.HasComp<CompOverseerMech>())
            {
                __result = OverseerSubjectState.Overseen;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Name), MethodType.Setter)]
    public static class Patch_Name
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance)
        {
            if(__instance is OverseerMech mech && mech.Name?.IsValid == true && mech.Comp?.dummyPawn != null)
            {
                mech.Comp.dummyPawn.Name = mech.Name;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.TotalBandwidth), MethodType.Getter)]
    public static class Pawn_MechanitorTracker_TotalBandwidth
    {
        public static void Postfix(ref int __result, Pawn_MechanitorTracker __instance)
        {
            CompOverseerMech comp = OverseerMechUtility.GetOverseerMech(__instance.Pawn);
            if (comp == null)
            {
                return;
            }
            if (comp.MechanitorActive)
            {
                __result = comp.CurrentBandwidth;
            }
            else
            {
                __result = 0;
            }
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange))]
    public static class MechanitorUtility_InMechanitorCommandRange
    {
        public static void Postfix(Pawn mech, LocalTargetInfo target, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            if (mech.HasComp<CompOverseerMech>())
            {
                __result = true;
                return;
            }
            Pawn overseer = mech.GetOverseer();
            if(overseer == null)
            {
                return;
            }
            CompOverseerMech comp = OverseerMechUtility.GetOverseerMech(overseer);
            if (overseer != null && comp != null && comp.parent.MapHeld == mech.Map && comp.parent.PositionHeld.DistanceTo(target.Cell) <= comp.Props.commandRange)
            {
                __result = true;
            }
        }
    }

    /*[HarmonyPatch(typeof(PawnComponentsUtility), "AddAndRemoveDynamicComponents")]
    public class Patch_PawnComponentsUtility
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, bool actAsIfSpawned)
        {
            if (pawn.HasComp<CompOverseerMech>())
            {
                pawn.skills = null;
            }
        }
    }*/

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.GetMechGizmos))]
    public static class MechanitorUtility_GetMechGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn mech)
        {
            foreach (var gizmo in __result)
            {
                if (gizmo is Command_Action command && command.defaultLabel == "CommandSelectOverseer".Translate())
                {
                    if (mech.HasComp<CompOverseerMech>())
                    {
                        continue;
                    }
                    var overseer = mech.GetOverseer();
                    if (overseer != null)
                    {
                        CompOverseerMech comp = OverseerMechUtility.GetOverseerMech(overseer);
                        if (comp != null)
                        { 
                            command.defaultDesc = "CommandSelectOverseerDesc".Translate();
                            command.icon = ContentFinder<Texture2D>.Get(comp.Props.selectOverseerIconPath);
                            command.action = delegate
                            {
                                Find.Selector.ClearSelection();
                                Find.Selector.Select(comp.parent);
                            };
                            command.Disabled = !comp.parent.Spawned;
                            command.onHover = delegate
                            {
                                if (overseer != null)
                                {
                                    if (comp.parent.Spawned)
                                    {
										GenDraw.DrawArrowPointingAt(comp.parent.TrueCenter());
									}
                                    else if (comp.parent.SpawnedOrAnyParentSpawned)
                                    {
										GenDraw.DrawArrowPointingAt(comp.parent.PositionHeld.ToVector3());
									}
                                }
                            };
                        }
                    }
                }
                yield return gizmo;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.CanControlMechs), MethodType.Getter)]
    public static class Pawn_MechanitorTracker_CanControlMechs
    {
        public static void Postfix(ref AcceptanceReport __result, Pawn_MechanitorTracker __instance)
        {
            CompOverseerMech comp = OverseerMechUtility.GetOverseerMech(__instance.Pawn);
            if (comp != null && comp.parent.Spawned)
            {
                __result = true;
            }
        }
    }

    /*[HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.TotalAvailableControlGroups),
    MethodType.Getter)]
    public static class Pawn_MechanitorTracker_TotalAvailableControlGroups
    {
        public static void Postfix(ref int __result, Pawn_MechanitorTracker __instance)
        {
            CompOverseerMech comp = OverseerMechUtility.GetOverseerMech(__instance.Pawn);
            if (comp != null)
            {
                __result = comp.Props.controlGroups;
            }
        }
    }*/

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.CanControlMech))]
    public static class Pawn_MechanitorUtility_CanControlMech
    {
        public static void Postfix(Pawn pawn, Pawn mech, ref AcceptanceReport __result)
        {
            if (!__result.Accepted) return;
            if (mech.HasComp<CompOverseerMech>()) __result = false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.KindLabel), MethodType.Getter)]
    public static class Pawn_KindLabel
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            CompOverseerMech comp = OverseerMechUtility.GetOverseerMech(__instance);
            if (comp != null)
            {
                __result = comp.Parent.KindLabel;
            }
        }
    }

    [HarmonyPatch(typeof(JobGiver_GetEnergy), nameof(JobGiver_GetEnergy.GetMinAutorechargeThreshold))]
    public static class JobGiver_GetEnergy_Min
	{
		[HarmonyPrefix]
		public static bool Prefix(Pawn pawn, ref int __result)
        {
            if(pawn is OverseerMech mech)
            {
				int num = pawn.RaceProps.maxMechEnergy;
				__result = Mathf.RoundToInt((float)num * mech.MinCharge);
				return false;
			}
            return true;
        }
    }

	[HarmonyPatch(typeof(JobGiver_GetEnergy), nameof(JobGiver_GetEnergy.GetMaxRechargeLimit))]
	public static class JobGiver_GetEnergy_Max
	{
		[HarmonyPrefix]
		public static bool Prefix(Pawn pawn, ref float __result)
		{
			if (pawn is OverseerMech mech)
			{
				int num = pawn.RaceProps.maxMechEnergy;
				__result = Mathf.RoundToInt((float)num * mech.MaxCharge);
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(CameraJumper), nameof(CameraJumper.TryJumpAndSelect))]
	public static class CameraJumper_TryJumpAndSelect
	{
		public static void Prefix(ref GlobalTargetInfo target)
		{
			if (target.Thing is Pawn pawn)
			{
				CompOverseerMech comp = OverseerMechUtility.GetOverseerMech(pawn);
				if (comp != null)
				{
					target = comp.parent;
				}
			}
		}
	}

	[HarmonyPatch(typeof(HealthUtility), nameof(HealthUtility.GetGeneralConditionLabel))]
    public static class HealthUtility_GetGeneralConditionLabel
    {
        public static void Postfix(ref string __result, Pawn pawn)
        {
            CompOverseerMech comp = OverseerMechUtility.GetOverseerMech(pawn);
            if (comp != null)
            {
                __result = "";
            }
        }
    }

    [HarmonyPatch(typeof(TransferableUIUtility), "DrawOverseerIcon")]
    public static class TransferableUIUtility_DrawOverseerIcon
    {
        public static bool Prefix(Pawn overseer, Rect rect)
        {
            CompOverseerMech comp = OverseerMechUtility.GetOverseerMech(overseer);
            if (comp == null)
            {
                return true;
            }

            GUI.DrawTexture(rect, comp.parent.def.uiIcon);
            if (!Mouse.IsOver(rect))
            {
                return false;
            }

            Widgets.DrawHighlight(rect);
            TooltipHandler.TipRegion(rect, "MechOverseer".Translate(overseer));

            return false;
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), "CanDraftMech")]
    public static class Patch_MechanitorDraft
    {
        [HarmonyPostfix]
        public static void Postfix(ref AcceptanceReport __result, Pawn mech)
        {
            if (mech.HasComp<CompOverseerMech>())
            {
                __result = true;
            }
        }
    }
    [HarmonyPatch(typeof(Pawn_DraftController), "ShowDraftGizmo", MethodType.Getter)]
    public static class Patch_MechDraft
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, Pawn_DraftController __instance)
        {
            if (__instance.pawn.HasComp<CompOverseerMech>())
            {
                __result = true;
            }
        }
    }

}

