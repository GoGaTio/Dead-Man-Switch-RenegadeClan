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
using DelaunatorSharp;
using Gilzoide.ManagedJobs;
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
using HarmonyLib;

namespace DMSRC
{
	/*[HarmonyPatch(typeof(JobDriver_Wait), "CheckForAutoAttack")]
	public class Patch_SaboteurOpenDoor
	{
		[HarmonyPostfix]
		public static void Postfix(JobDriver_Wait __instance)
		{
			if (__instance.pawn.TryGetComp<CompSaboteur>(out var comp))
			{
				comp.Notify_TriedToShoot();
			}
		}
	}*/

	/*[HarmonyPatch(typeof(HediffComp_Invisibility), "ShouldBeVisible", MethodType.Getter)]
	[HarmonyPriority(555)]
	public class Patch_SaboteurAntiError
	{
		[HarmonyPrefix]
		[HarmonyPriority(555)]
		public static bool Prefix(ref bool __result, HediffComp_Invisibility __instance)
		{
			Pawn pawn = __instance.Pawn;
			if (pawn == null || pawn.HasComp<CompSaboteur>())
            {
				if(pawn.mindState == null)
                {
					return false;
                }
				if (pawn.mindState.lastBecameVisibleTick >= pawn.mindState.lastBecameInvisibleTick)
				{
					__result = true;
				}
				else
				{
					__result = false;
				}
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(HediffComp_Invisibility), "FadeOut", MethodType.Getter)]
	[HarmonyPriority(555)]
	public class Patch_SaboteurAntiErrorSecond
	{
		[HarmonyPrefix]
		[HarmonyPriority(555)]
		public static bool Prefix(ref float __result, HediffComp_Invisibility __instance)
		{
			Pawn pawn = __instance.Pawn;
			if (pawn == null || pawn.mindState == null)
			{
				__result = 1f;
				return false;
			}
			return true;
		}
	}*/
}