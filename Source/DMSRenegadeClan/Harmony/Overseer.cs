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
using static HarmonyLib.Code;
using static RimWorld.MechClusterSketch;
using static System.Net.WebRequestMethods;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
using Fortified;

namespace DMSRC
{
    [HarmonyPatch(typeof(JobGiver_WanderOverseer), "Target")]
    public static class Patch_JobGiver_WanderOverseer
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref GlobalTargetInfo __result)
        {
			if (pawn is IOverseer)
			{
                __result = pawn;
                return;
			}
			if (__result.Pawn == null) return;
            Pawn mech = OverseerMechUtility.GetOverseerPawn(__result.Pawn);
            if (mech != null)
            {
                __result = mech;
            }
        }
    }

    [HarmonyPatch(typeof(JobGiver_AIDefendOverseer), "GetDefendee")]
    public static class Patch_JobGiver_AIDefendOverseer
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref Pawn __result)
        {
			if (pawn is IOverseer)
			{
				__result = pawn;
				return;
			}
			if (__result == null) return;
			Pawn mech = OverseerMechUtility.GetOverseerPawn(__result);
			if (mech != null)
			{
				__result = mech;
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
			Pawn mech = OverseerMechUtility.GetOverseerPawn(__result);
			if (mech != null)
			{
				__result = mech;
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
                if (transferable.HasAnyThing && transferable.AnyThing is IOverseer)
                {
                    __result = false;
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Interval))]
    public static class Patch_SkillRecord
    {
        [HarmonyPrefix]
        [HarmonyPriority(501)]
        public static bool Interval(Pawn ___pawn)
        {
            return ___pawn != null && !(___pawn is IOverseer);
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
			Pawn mech = OverseerMechUtility.GetOverseerPawn(overseer);
            if (mech == null)
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
					if (caravan.PawnsListForReading.Contains(mech))
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
            if (pawn is IOverseer mech && mech.Comp.WorkMode == __instance.workMode)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(CompOverseerSubject), "State", MethodType.Getter)]
    public static class Patch_Overseer
    {
        [HarmonyPostfix]
        public static void Postfix(ref OverseerSubjectState __result, CompOverseerSubject __instance)
        {
            if (__result != OverseerSubjectState.Overseen && __instance.parent is IOverseer)
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
            if(__instance is IOverseer mech)
            {
                mech.Notify_NameChanged();
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.TotalBandwidth), MethodType.Getter)]
    public static class Pawn_MechanitorTracker_TotalBandwidth
    {
        public static void Postfix(ref int __result, Pawn_MechanitorTracker __instance)
        {
            CompOverseerMech comp = OverseerMechUtility.GetOverseerMechComp(__instance.Pawn);
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
            if (CompBroadcastAntenna.Affects(mech.Map))
            {
                __result = true;
                return;
            }
            if (mech is IOverseer)
            {
                __result = true;
                return;
            }
            Pawn overseer = mech.GetOverseer();
            if(overseer == null)
            {
                return;
            }
			Pawn overlord = overseer.GetOverseerPawn(out var overseerInt);
            if (overlord != null && overlord.MapHeld == mech.Map && overlord.PositionHeld.DistanceTo(target.Cell) <= overseerInt.Comp.Props.commandRange)
            {
                __result = true;
            }
        }
    }

	[HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.GetMechGizmos))]
    public static class MechanitorUtility_GetMechGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn mech)
        {
            foreach (var gizmo in __result)
            {
                if (gizmo is Command_Action command && command.defaultLabel == "CommandSelectOverseer".Translate())
                {
                    if (mech is IOverseer)
                    {
                        continue;
                    }
                    var overseer = mech.GetOverseer();
                    if (overseer != null)
                    {
						Pawn overlord = overseer.GetOverseerPawn(out var overseerInt);
                        if (overlord != null)
                        { 
                            command.defaultDesc = "CommandSelectOverseerDesc".Translate();
                            command.icon = overseerInt.Comp.SelectIcon;
                            command.action = delegate
                            {
                                Find.Selector.ClearSelection();
                                Find.Selector.Select(overlord);
                            };
                            command.Disabled = !overlord.Spawned;
                            command.onHover = delegate
                            {
                                if (overseer != null)
                                {
                                    if (overlord.Spawned)
                                    {
										GenDraw.DrawArrowPointingAt(overlord.TrueCenter());
									}
                                    else if (overlord.SpawnedOrAnyParentSpawned)
                                    {
										GenDraw.DrawArrowPointingAt(overlord.PositionHeld.ToVector3());
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
			Pawn mech = OverseerMechUtility.GetOverseerPawn(__instance.Pawn);
            if (mech != null && mech.Spawned)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.CanControlMech))]
    public static class Pawn_MechanitorUtility_CanControlMech
    {
        public static void Postfix(Pawn pawn, Pawn mech, ref AcceptanceReport __result)
        {
            if (!__result.Accepted) return;
            if (mech is IOverseer) __result = false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.KindLabel), MethodType.Getter)]
    public static class Pawn_KindLabel
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
			Pawn mech = OverseerMechUtility.GetOverseerPawn(__instance);
            if (mech != null)
            {
                __result = mech.KindLabel;
            }
        }
    }

    [HarmonyPatch(typeof(JobGiver_GetEnergy), nameof(JobGiver_GetEnergy.GetMinAutorechargeThreshold))]
    public static class JobGiver_GetEnergy_Min
	{
		[HarmonyPrefix]
		public static bool Prefix(Pawn pawn, ref int __result)
        {
            if(pawn is IOverseer mech)
            {
				int num = pawn.RaceProps.maxMechEnergy;
				__result = Mathf.RoundToInt((float)num * mech.MinCharge);
				return false;
			}
            return true;
        }
    }

	[HarmonyPatch(typeof(PawnColumnWorker_AllowedArea), nameof(PawnColumnWorker_AllowedArea.DoCell))]
	public static class PawnColumnWorker_AllowedArea_DoCell
	{
		[HarmonyPrefix]
		public static bool Prefix(Rect rect, Pawn pawn, PawnTable table)
		{
			if (pawn is IOverseer mech)
			{
				if (pawn.playerSettings.SupportsAllowedAreas)
				{
					AreaAllowedGUI.DoAllowedAreaSelectors(rect, pawn);
				}
				else if (AnimalPenUtility.NeedsToBeManagedByRope(pawn))
				{
					AnimalPenGUI.DoAllowedAreaMessage(rect, pawn);
				}
				else if (pawn.RaceProps.Dryad)
				{
					Text.Anchor = TextAnchor.MiddleCenter;
					Text.Font = GameFont.Tiny;
					GUI.color = Color.gray;
					Widgets.Label(rect, "CannotAssignAllowedAreaToDryad".Translate());
					GUI.color = Color.white;
					Text.Font = GameFont.Small;
					Text.Anchor = TextAnchor.UpperLeft;
				}
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Frame), "GetIdeoForStyle")]
	public static class Frame_GetIdeoForStyle
	{
		[HarmonyPrefix]
		public static bool Prefix(Pawn worker, ref Ideo __result)
		{
			if (worker is IOverseer)
			{
                __result = worker.Faction?.ideos?.PrimaryIdeo;
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
			if (pawn is IOverseer mech)
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
				Pawn mech = OverseerMechUtility.GetOverseerPawn(pawn);
				if (mech != null)
				{
					target = mech;
				}
			}
		}
	}

	[HarmonyPatch(typeof(HealthUtility), nameof(HealthUtility.GetGeneralConditionLabel))]
    public static class HealthUtility_GetGeneralConditionLabel
    {
        public static void Postfix(ref string __result, Pawn pawn)
        {
			Pawn mech = OverseerMechUtility.GetOverseerPawn(pawn);
            if (mech != null)
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
            Pawn mech = OverseerMechUtility.GetOverseerPawn(overseer);
            if (mech == null)
            {
                return true;
            }
            GUI.DrawTexture(rect, mech.def.uiIcon);
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
            if (mech is IOverseer)
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
            if (__instance.pawn is IOverseer)
            {
                __result = true;
            }
        }
    }

}

