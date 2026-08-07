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
using CombatExtended;

namespace DMSRC.CE
{
	[StaticConstructorOnStartup]
	public static class FirecrackerPatch
	{
		static FirecrackerPatch()
		{
			if (DMSRenegadeClan.harmonyInstance == null)
			{
				DMSRenegadeClan.harmonyInstance = new Harmony("DMSRenegadeClan_Patch");
			}
			DMSRenegadeClan.CEIsActive = true;
			MethodInfo method = typeof(AmmoThing).Method("TryLaunchCookOffProjectile");
			MethodInfo prefix = typeof(FirecrackerPatch).GetMethod("TryLaunchCookOffProjectile");
			DMSRenegadeClan.harmonyInstance.Patch(method, new HarmonyMethod(prefix));
		}

		public static bool TryLaunchCookOffProjectile(ref bool __result, AmmoThing __instance)
		{
			if (__instance.AmmoDef?.ammoClass?.label == "FSR")
			{
				Map map = __instance.MapHeld;
				if (map == null)
				{
					__result = false;
					return false;
				}
				IntVec3 pos = __instance.PositionHeld;
				float rand = Rand.Value;
				if(rand < 0.25f)
				{
					ProjectileCE projectile = (ProjectileCE)ThingMaker.MakeThing(__instance.AmmoDef.cookOffProjectile);
					GenSpawn.Spawn(projectile, pos, map);
					FloatRange angleSinRange = new FloatRange(Mathf.Sin(-10 * ((float)Math.PI / 180f)), 0);
					projectile.canTargetSelf = true;
					projectile.minCollisionDistance = 0f;
					projectile.logMisses = false;
					projectile.Launch(__instance,
									  __instance.DrawPos.ToVector2(),
									  Mathf.Asin(angleSinRange.RandomInRange),
									  Rand.Range(0, 360),
									  0.1f,
									  __instance.AmmoDef.cookOffProjectile.projectile.speed * __instance.AmmoDef.cookOffSpeed,
									  __instance);
				}
				else if (rand < 0.15f)
				{
					List<IntVec3> hitCells = CellRect.FromCell(pos).ExpandedBy(1).ClipInsideMap(map).Cells.ToList();
					DamageInfo dinfo = new DamageInfo(RCDefOf.DMSRC_Firecracker, RCDefOf.DMSRC_Firecracker.defaultDamage, RCDefOf.DMSRC_Firecracker.defaultArmorPenetration, instigator: __instance, instigatorGuilty: false);
					foreach (IntVec3 c in hitCells)
					{
						foreach (Thing t in c.GetThingList(map).ToList())
						{
							if (t == __instance) continue;
							t.TakeDamage(dinfo);
						}
						if (Rand.Chance(FireUtility.ChanceToStartFireIn(c, map) * 2f))
						{
							FireUtility.TryStartFireIn(c, map, Rand.Range(0.2f, 0.6f), __instance);
						}
					}
					if ((double)__instance.AmmoDef.cookOffFlashScale > 0.01)
					{
						FleckMakerCE.Static(pos, map, FleckDefOf.ShotFlash, __instance.AmmoDef.cookOffFlashScale);
					}
					if (__instance.AmmoDef.cookOffProjectile.projectile.landedEffecter != null)
					{
						__instance.AmmoDef.cookOffProjectile.projectile.landedEffecter.Spawn(pos, map);
					}
				}
				if (__instance.AmmoDef.cookOffSound != null)
				{
					__instance.AmmoDef.cookOffSound.PlayOneShot(new TargetInfo(pos, map));
				}
				if (__instance.AmmoDef.cookOffTailSound != null)
				{
					__instance.AmmoDef.cookOffTailSound.PlayOneShotOnCamera();
				}
				__result = true;
				return false;
			}
			return true;
		}
	}
}