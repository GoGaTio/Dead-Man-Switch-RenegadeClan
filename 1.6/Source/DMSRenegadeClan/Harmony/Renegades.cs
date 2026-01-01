using DelaunatorSharp;
using Fortified;
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
	[HarmonyPatch(typeof(StockGeneratorUtility), nameof(StockGeneratorUtility.TryMakeForStockSingle))]
	public class Patch_StockGeneratorUtility_TryMakeForStockSingle
	{
		[HarmonyPrefix]
		public static bool Prefix(ThingDef thingDef, int stackCount, Faction faction, ref Thing __result)
		{
			if (stackCount > 0 && faction?.def == RCDefOf.DMSRC_RenegadeClan && (thingDef.IsApparel || thingDef.IsWeapon))
			{
				ThingDef result = null;
				if (thingDef.MadeFromStuff && !(from x in GenStuff.AllowedStuffsFor(thingDef, TechLevel.Ultra, checkAllowedInStuffGeneration: true)
												where !PawnWeaponGenerator.IsDerpWeapon(thingDef, x) && faction.def.CanUseStuffForApparel(x)
												select x).TryRandomElementByWeight((ThingDef x) => x.stuffProps.commonality + 1f, out result))
				{
					result = GenStuff.RandomStuffByCommonalityFor(thingDef);
				}
				__result = ThingMaker.MakeThing(thingDef, result);
				__result.stackCount = stackCount;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(FactionDef), "RaidCommonalityFromPoints")]
	public class Patch_RaidCommonalityFromPoints
	{
		[HarmonyPostfix]
		public static void Postfix(float points, ref float __result, FactionDef __instance)
		{
			if (__instance == RCDefOf.DMSRC_RenegadeClan)
			{
				__result = GameComponent_Renegades.Find.RaidCommonality(points);
			}
		}
	}

	[HarmonyPatch(typeof(Faction), nameof(Faction.RelationWith))]
	public class Patch_RelationWith
	{
		[HarmonyPrefix]
		public static bool Prefix(Faction other, Faction __instance, ref FactionRelation __result)
		{
			if (__instance.def == RCDefOf.DMSRC_RenegadeClan && other.IsPlayer)
			{
				__result = GameComponent_Renegades.Find.RelationWithPlayer(other);
			}
			else if (__instance.IsPlayer && other.def == RCDefOf.DMSRC_RenegadeClan)
			{
				__result = GameComponent_Renegades.Find.RelationWithPlayer(other);
			}
			else
			{
				return true;
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(Widgets), nameof(Widgets.InfoCardButton), new Type[] { typeof(float), typeof(float), typeof(Thing) })]
	public class Patch_InfoCard
	{
		[HarmonyPrefix]
		public static bool Prefix(float x, float y, Thing thing, ref bool __result)
		{
			if (thing is InactiveMech mech && mech.InnerPawn != null)
			{
				__result = Widgets.InfoCardButton(x, y, mech.InnerPawn);
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Site), "ShouldRemoveMapNow")]
	public class Patch_ShouldRemoveMapNow
	{
		[HarmonyPostfix]
		public static void Postfix(ref bool alsoRemoveWorldObject, ref bool __result, Site __instance)
		{
			if (!__result) return;
			List<Building_SecurityContainer> list = new List<Building_SecurityContainer>();
			__instance.Map.listerThings.GetThingsOfType<DMSRC.Building_SecurityContainer>(list);
			if (list.NullOrEmpty())
			{
				return;
			}
			if (list.Any((Building_SecurityContainer x) => !x.innerContainer.NullOrEmpty()))
			{
				alsoRemoveWorldObject = false;
			}
			if (list.Any((Building_SecurityContainer x) => x.opened && !x.innerContainer.NullOrEmpty()))
			{
				__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(PawnGroupMakerUtility), nameof(PawnGroupMakerUtility.GeneratePawns))]
	public class Patch_Enhance
	{
		[HarmonyPostfix]
		public static IEnumerable<Pawn> Postfix(IEnumerable<Pawn> __result, PawnGroupMakerParms parms)
		{
			EnhanceDef def = null;
			int seed = Rand.Int;
			if (parms?.faction?.def?.humanlikeFaction == true && !__result.EnumerableNullOrEmpty())
			{
				Faction faction = parms.faction;
				GameComponent_Renegades comp = GameComponent_Renegades.Find;
				if (comp.active && comp.PlayerRelation == FactionRelationKind.Hostile && faction.HostileTo(Faction.OfPlayerSilentFail))
				{
					def = Defs.FirstOrDefault((x) => x.AllowFaction(faction));
				}
			}
			foreach (Pawn p in __result)
			{
				if(def != null && p.RaceProps.Humanlike)
				{
					if(Rand.ChanceSeeded(def.chanceToEnhanceWeapon, seed))
					{
						def.TryEnhanceWeapon(p);
					}
					if (Rand.ChanceSeeded(def.chanceToEnhanceApparel, seed))
					{
						def.TryEnhanceWeapon(p);
						if (def.TryEnhanceApparel(p))
						{
							def.TryEnhanceApparel(p);
						}
					}
				}
				seed = Gen.HashCombineInt(seed, p.GetHashCode() + p.LabelNoCount.GetHashCode());
				yield return p;
			}
			if(def != null)
			{
				foreach (Pawn p in def.Reinforcements(parms))
				{
					yield return p;
				}
			}
		}

		private static List<EnhanceDef> defs;

		public static List<EnhanceDef> Defs
		{
			get
			{
				if (defs == null)
				{
					defs = DefDatabase<EnhanceDef>.AllDefsListForReading;
				}
				return defs;
			}
		}
	}

	/*[HarmonyPatch(typeof(PawnGenerator), "GenerateRandomAge")]
	public class Patch_GenerateRandomAge
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, PawnGenerationRequest request)
		{
			float? chance = pawn.kindDef.GetModExtension<PawnGenExtension>()?.genderChanceOverride;
			if (chance != null && pawn.RaceProps.hasGenders)
			{
				if (Rand.Value < chance.Value)
				{
					pawn.gender = Gender.Male;
				}
				else
				{
					pawn.gender = Gender.Female;
				}
			}
		}
	}

	[HarmonyPatch(typeof(PawnSkinColors), "RandomSkinColorGene")]
	public class Patch_RandomSkinColorGene
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, ref GeneDef __result)
		{
			FloatRange? range = pawn.kindDef.GetModExtension<PawnGenExtension>()?.melaninRange;
			if (range != null)
			{
				__result = PawnSkinColors.GetSkinColorGene(range.Value.RandomInRange);
			}
		}
	}

	[HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new Type[] { typeof(PawnGenerationRequest)})]
	public class Patch_GeneratePawn
	{
		[HarmonyPostfix]
		public static void Postfix(PawnGenerationRequest request, ref Pawn __result)
		{
			if (__result?.guest != null)
			{
				if (request.ForceRecruitable)
				{
					__result.guest.Recruitable = true;
				}
				else
				{
					if (Rand.Chance(__result.kindDef.GetModExtension<PawnGenExtension>()?.unRecruitableChanceOverride ?? 0f))
					{
						__result.guest.Recruitable = false;
					}
				}
			}
		}
	}*/
}