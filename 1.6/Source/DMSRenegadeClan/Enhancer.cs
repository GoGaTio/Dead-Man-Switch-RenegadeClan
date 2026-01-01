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
using Verse.Grammar;
using static HarmonyLib.Code;
using static RimWorld.MechClusterSketch;
using static RimWorld.PsychicRitualRoleDef;
using static UnityEngine.Scripting.GarbageCollector;

namespace DMSRC
{
	public class EnhanceDef : Def
	{
		public class ThingDefWeight
		{
			public ThingDef thingDef;

			public float weight;

			public string tag;

			public void LoadDataFromXmlCustom(XmlNode xmlRoot)
			{
				XmlHelper.ParseElements(this, xmlRoot, "thingDef", "weight");
			}
		}

		public float chanceToEnhanceWeapon = 0.2f;

		public float chanceToEnhanceApparel = 0.1f;

		public List<FactionDef> factions = new List<FactionDef>();

		public string tag;

		public List<ThingDefWeight> apparelDefs = new List<ThingDefWeight>();

		public List<ThingDefWeight> weaponDefs = new List<ThingDefWeight>();

		public List<ThingDef> stuffDefs = new List<ThingDef>();

		public List<PawnGenOption> reinforcementOptions = new List<PawnGenOption>();

		public SimpleCurve minReinforcementPointsCurve = new SimpleCurve(new CurvePoint[3]
		{
			new CurvePoint(6000f, 0f),
			new CurvePoint(8000f, 100f),
			new CurvePoint(10000f, 200f)
		});

		public SimpleCurve maxReinforcementPointsCurve = new SimpleCurve(new CurvePoint[3]
		{
			new CurvePoint(1000f, 0f),
			new CurvePoint(5000f, 400f),
			new CurvePoint(10000f, 1000f)
		});

		public bool AllowFaction(Faction faction)
		{
			if (faction.def.categoryTag == tag)
			{
				return true;
			}
			if (factions.Contains(faction.def))
			{
				return true;
			}
			return false;
		}

		public List<Pawn> Reinforcements(PawnGroupMakerParms parms)
		{
			List<Pawn> list = new List<Pawn>();
			try
			{
				float points = new FloatRange(minReinforcementPointsCurve.Evaluate(parms.points), maxReinforcementPointsCurve.Evaluate(parms.points)).RandomInRange;
				while (points > 0)
				{
					if (reinforcementOptions.Where((x) => x.Cost <= points).TryRandomElementByWeight((y) => y.selectionWeight, out var result))
					{
						PawnKindDef kind = result.kind;
						Faction faction = parms.faction;
						Ideo ideo = parms.ideo;
						PlanetTile? tile = parms.tile;
						bool allowFood = false;
						PawnGenerationRequest request = new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, tile, forceGenerateNewPawn: false, allowDead: false, parms.faction.deactivated, canGeneratePawnRelations: true, mustBeCapableOfViolence: true, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: true, allowFood, allowAddictions: true, false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, ideo, forceNoIdeo: false, forceNoBackstory: false, forbidAnyTitle: false, forceDead: false, null, null, null);
						request.AllowedDevelopmentalStages = DevelopmentalStage.Adult;
						Pawn pawn = PawnGenerator.GeneratePawn(request);
						points -= result.Cost;
						list.Add(pawn);
					}
					else
					{
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Error while applying DMSRC.EnhanceDef(DeadManSwitchRenegadeClan): " + ex);
			}
			return list;
		}

		public bool TryEnhanceWeapon(Pawn pawn)
		{
			if (!weaponDefs.NullOrEmpty() && pawn.equipment != null)
			{
				ThingWithComps weapon = RandomWeapon(pawn.equipment.Primary?.def.IsMeleeWeapon == true);
				if (pawn.equipment.Primary != null)
				{
					if (WeaponScore(weapon) * 2 > WeaponScore(pawn.equipment.Primary))
					{
						pawn.equipment.DestroyEquipment(pawn.equipment.Primary);
					}
					else return false;
				}
				pawn.equipment.AddEquipment(weapon);
				Log.Message(weapon.def.defName);
				return true;
			}
			return false;
		}

		private float WeaponScore(ThingWithComps weapon)
		{
			if (weapon.def.IsMeleeWeapon)
			{
				return weapon.GetStatValue(RCDefOf.MeleeWeapon_AverageDPS);
			}
			if (weapon.def.IsRangedWeapon)
			{
				VerbProperties verb = weapon.def.Verbs.First((VerbProperties x) => x.isPrimary);
				float time = ((verb.warmupTime * weapon.GetStatValue(StatDefOf.RangedWeapon_WarmupMultiplier) + weapon.GetStatValue(StatDefOf.RangedWeapon_Cooldown)) * 60) + verb.ticksBetweenBurstShots;
				float damage = verb.defaultProjectile.projectile.GetDamageAmount(weapon) * verb.burstShotCount;
				float accuracy = verb.ForcedMissRadius > 0.01f ? (1 / verb.ForcedMissRadius) : Accuracy(weapon, verb.range);
				return damage * accuracy / time;
			}
			return 0f;
		}

		private float Accuracy(ThingWithComps gun, float range)
		{
			float num = 0;
			if (range <= 3)
			{
				num += gun.GetStatValue(StatDefOf.AccuracyTouch) * range;
			}
			else
			{
				num += gun.GetStatValue(StatDefOf.AccuracyTouch) * 3;
				if (range <= 12)
				{
					num += gun.GetStatValue(StatDefOf.AccuracyShort) * (range - 3);
				}
				else
				{
					num += gun.GetStatValue(StatDefOf.AccuracyShort) * 12;
					if (range <= 25)
					{
						num += gun.GetStatValue(StatDefOf.AccuracyMedium) * (range - 12);
					}
					else
					{
						num += gun.GetStatValue(StatDefOf.AccuracyMedium) * 25;
						num += gun.GetStatValue(StatDefOf.AccuracyLong) * (range - 40);
					}
				}
			}
			return num / range;
		}

		private ThingWithComps RandomWeapon(bool melee = false)
		{
			ThingDefWeight tdw = weaponDefs.Where((x)=>melee ? x.thingDef.IsMeleeWeapon : x.thingDef.IsRangedWeapon).RandomElementByWeight((y)=> y.weight);
			ThingDef def = null;
			ThingDef stuff = null;
			if (!tdw.tag.NullOrEmpty())
			{
				def = DefDatabase<ThingDef>.AllDefs.Where((x) => x.IsRangedWeapon && x.weaponTags != null && x.weaponTags.Contains(tdw.tag)).RandomElement();
			}
			if(def == null)
			{
				def = tdw.thingDef;
			}
			if (def.MadeFromStuff)
			{
				if (!GenStuff.TryRandomStuffFor(def, out stuff, TechLevel.Undefined, (x) => stuffDefs.Contains(x)))
				{
					stuff = GenStuff.DefaultStuffFor(def);
				}
			}
			return ThingMaker.MakeThing(def, stuff) as ThingWithComps;
		}

		public bool TryEnhanceApparel(Pawn pawn)
		{
			if (!apparelDefs.NullOrEmpty() && pawn.apparel != null)
			{
				Apparel apparel = RandomApparel();
				if (apparel != null)
				{
					List<Apparel> list = new List<Apparel>();
					foreach (Apparel ap in pawn.apparel.WornApparel)
					{
						if (!ApparelUtility.CanWearTogether(apparel.def, ap.def, pawn.RaceProps.body))
						{
							list.Add(ap);
						}
					}
					if (list.Count > 0)
					{
						if (ApparelScore(apparel) > list.Sum((a) => ApparelScore(a)))
						{
							foreach (Apparel ap in list.ToList())
							{
								pawn.apparel.Remove(ap);
								ap.Destroy();
							}
						}
						else return false;
					}
					pawn.apparel.Wear(apparel);
					Log.Message(apparel.def.defName);
					return true;
				}
			}
			return false;
		}

		private float ApparelScore(Apparel apparel)
		{
			return ((apparel.GetStatValue(StatDefOf.ArmorRating_Sharp) + apparel.GetStatValue(StatDefOf.ArmorRating_Blunt)) / apparel.def.apparel.layers.Count) / apparel.def.apparel.bodyPartGroups.Count;
		}

		private Apparel RandomApparel(bool secondTry = false)
		{
			ThingDefWeight tdw = apparelDefs.RandomElementByWeight((y) => y.weight);
			ThingDef def = tdw.thingDef;
			ThingDef stuff = null;
			if (def == null)
			{
				DefDatabase<ThingDef>.AllDefs.Where((x) => x.apparel != null && x.apparel.tags.Contains(tdw.tag)).RandomElement();
			}
			if (def == null && !secondTry)
			{
				if (secondTry) return RandomApparel(true);
				return null;
			}
			if (def.MadeFromStuff)
			{
				if (!GenStuff.TryRandomStuffFor(def, out stuff, TechLevel.Undefined, (x) => stuffDefs.Contains(x)))
				{
					stuff = GenStuff.DefaultStuffFor(def);
				}
			}
			return ThingMaker.MakeThing(def, stuff) as Apparel;
		}
	}
}