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
using System.Security.Cryptography;
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
using static DMSRC.GenStep_RPrefab;
using static HarmonyLib.Code;

namespace DMSRC
{
	public static class OverseerMechUtility
	{
		public static CompOverseerMech GetOverseerMechComp(this Pawn dummy)
		{
			return GetOverseerMech(dummy)?.Comp;
		}

		public static OverseerMech GetOverseerMech(this Pawn dummy)
		{
			if (dummy == null || dummy.kindDef != RCDefOf.DMSRC_DummyMechanitor || dummy.mechanitor == null || dummy.health?.hediffSet == null)
			{
				return null;
			}
			return dummy.health.hediffSet.GetFirstHediff<Hediff_DummyPawn>()?.overseer;
		}
	}

	public static class RCToolsUtility
	{
		[DebugAction("DMSRC", "Trackers report", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void TrackersReport(Pawn p)
		{
			if (p != null)
			{
				string s = "Needs report:" + "\n";
				if (p.ageTracker == null)
				{
					s += "\n" + "ageTracker" + " is null;";
				}
				if (p.health == null)
				{
					s += "\n" + "health" + " is null;";
				}
				if (p.records == null)
				{
					s += "\n" + "records" + " is null;";
				}
				if (p.inventory == null)
				{
					s += "\n" + "inventory" + " is null;";
				}
				if (p.meleeVerbs == null)
				{
					s += "\n" + "meleeVerbs" + " is null;";
				}
				if (p.ownership == null)
				{
					s += "\n" + "ownership" + " is null;";
				}
				if (p.carryTracker == null)
				{
					s += "\n" + "carryTracker" + " is null;";
				}
				if (p.needs == null)
				{
					s += "\n" + "needs" + " is null;";
				}
				if (p.mindState == null)
				{
					s += "\n" + "mindState" + " is null;";
				}
				if (p.surroundings == null)
				{
					s += "\n" + "surroundings" + " is null;";
				}
				if (p.thinker == null)
				{
					s += "\n" + "thinker" + " is null;";
				}
				if (p.jobs == null)
				{
					s += "\n" + "jobs" + " is null;";
				}
				if (p.stances == null)
				{
					s += "\n" + "stances" + " is stances;";
				}
				if (p.rotationTracker == null)
				{
					s += "\n" + "rotationTracker" + " is null;";
				}
				if (p.pather == null)
				{
					s += "\n" + "pather" + " is null;";
				}
				if (p.equipment == null)
				{
					s += "\n" + "equipment" + " is null;";
				}
				if (p.apparel == null)
				{
					s += "\n" + "apparel" + " is null;";
				}
				if (p.skills == null)
				{
					s += "\n" + "skills" + " is null;";
				}
				Log.Message(s);
			}

		}

		[DebugAction("DMSRC", "Deactivate mech", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void Deactivate(Pawn p)
		{
			if (p != null)
			{
				InactiveMech b = (InactiveMech)ThingMaker.MakeThing(RCDefOf.DMSRC_InactiveMech);
				IntVec3 cell = p.Position;
				Map map = p.Map;
				p.DeSpawn();
				b.innerContainer.TryAddOrTransfer(p);
				GenSpawn.Spawn(b, cell, map);
			}
		}

		[DebugAction("DMSRC", "Check Apparel", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static void OpenMarket(Pawn p)
		{
			if (p != null && p.apparel != null)
			{
				string s = "";
				foreach (Apparel ap in p.apparel.WornApparel)
				{
					s += "\n" + ap.def.defName + ", Wearer: " + ap.Wearer?.LabelCap ?? "null";
					if (ap.TryGetComp<CompShield>(out var comp))
					{
						s += ", Shield energy:" + comp.Energy + ", IsApparel: " + comp.IsApparel + ", State: " + comp.ShieldState.ToString();
						s += ", (" + comp.parent.GetStatValue(StatDefOf.EnergyShieldRechargeRate) + "/" + comp.parent.GetStatValue(StatDefOf.EnergyShieldEnergyMax) + ")";

					}
				}
				Log.Message(s);
			}
		}

		[DebugAction("DMSRC", "Spawn all weapons", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static void SpawnAllWeapons()
		{
			IntVec3 cell = UI.MouseCell();
			foreach (ThingDef def in from d in DefDatabase<ThingDef>.AllDefs
									 where d.equipmentType == EquipmentType.Primary
									 select d)
			{
				Thing t = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
				GenPlace.TryPlaceThing(t, cell, Find.CurrentMap, ThingPlaceMode.Near);
			}
		}

		[DebugAction("DMSRC", "Spawn things by trade tag", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static List<DebugActionNode> SpawnThingsByTradeTag()
		{
			List<DebugActionNode> nodes = new List<DebugActionNode>();
			List<string> tags = new List<string>();
			foreach (ThingDef t in DefDatabase<ThingDef>.AllDefs)
			{
				if (t.tradeTags.NullOrEmpty()) continue;
				foreach (string s in t.tradeTags)
				{
					if (tags.Contains(s)) continue;
					tags.Add(s);
				}
			}
			//tags.Sort();
			foreach (string tag in tags)
			{
				string localTag = tag;
				nodes.Add(new DebugActionNode(localTag, DebugActionType.ToolMap)
				{
					action = delegate
					{
						foreach (ThingDef def in from d in DefDatabase<ThingDef>.AllDefs
												 where !d.tradeTags.NullOrEmpty() && d.tradeTags.Contains(localTag)
												 select d)
						{
							Thing t = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
							GenPlace.TryPlaceThing(t, UI.MouseCell(), Find.CurrentMap, ThingPlaceMode.Near);
						}
					}
				});
			}
			return nodes;
		}

		[DebugAction("DMSRC", "Spawn things by weapon tag", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static List<DebugActionNode> SpawnThingsByWeaponTag()
		{
			List<DebugActionNode> nodes = new List<DebugActionNode>();
			List<string> tags = new List<string>();
			foreach (ThingDef t in DefDatabase<ThingDef>.AllDefs)
			{
				if (t.weaponTags.NullOrEmpty()) continue;
				foreach (string s in t.weaponTags)
				{
					if (tags.Contains(s)) continue;
					tags.Add(s);
				}
			}
			//tags.Sort();
			foreach (string tag in tags)
			{
				string localTag = tag;
				nodes.Add(new DebugActionNode(localTag, DebugActionType.ToolMap)
				{
					action = delegate
					{
						foreach (ThingDef def in from d in DefDatabase<ThingDef>.AllDefs
												 where !d.weaponTags.NullOrEmpty() && d.weaponTags.Contains(localTag)
												 select d)
						{
							Thing t = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
							GenPlace.TryPlaceThing(t, UI.MouseCell(), Find.CurrentMap, ThingPlaceMode.Near);
						}
					}
				});
			}
			return nodes;
		}

		private static Rot4 Rotation = Rot4.North;

		[DebugAction("DMSRC", "Rotate RPrefab", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static void RotateRPrefab()
		{
			Rotation.Rotate(RotationDirection.Clockwise);
			Messages.Message("RPrefab rotation: " + Rotation.ToStringHuman(), MessageTypeDefOf.NeutralEvent, historical: false);
		}

		[DebugAction("DMSRC", "Spawn RPrefab", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static List<DebugActionNode> SpawnRPrefab()
		{
			List<DebugActionNode> list = new List<DebugActionNode>();
			foreach (RPrefabDef def in DefDatabase<RPrefabDef>.AllDefsListForReading)
			{
				list.Add(new DebugActionNode(def.defName ?? "", DebugActionType.ToolMap)
				{
					action = delegate
					{
						SpawnAtMouseCell(def);
					}
				});
			}
			return list;
		}
		private static void SpawnAtMouseCell(RPrefabDef def)
		{
			IntVec3 intVec = UI.MouseCell();
			Map currentMap = Find.CurrentMap;
			if (!intVec.InBounds(currentMap))
			{
				return;
			}
			Rot4 rotation = Rotation;
			List<Thing> list = new List<Thing>();
			def.Generate(intVec, rotation, currentMap, null, ref list);
		}

		[DebugAction("DMSRC", "Get faction", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static void GetFaction()
		{
			IntVec3 cell = UI.MouseCell();
			int count = cell.GetThingList(Find.CurrentMap).Count;
			foreach (Thing t in cell.GetThingList(Find.CurrentMap).ToList())
			{
				MoteMaker.ThrowText(cell.ToVector3(), Find.CurrentMap, Throw(t), Color.white);
			}
			string Throw(Thing t)
			{
				string s = "";
				if (t.Faction == null)
				{
					s = "null";
				}
				else
				{
					s = t.Faction.def.defName;
				}
				if (count == 1)
				{
					return s;
				}
				return t.LabelCap + ": " + s;
			}
		}

		[DebugAction("DMSRC", "Get market value", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static void GetMarketValue()
		{
			IntVec3 cell = UI.MouseCell();
			int count = cell.GetThingList(Find.CurrentMap).Count;
			foreach (Thing t in cell.GetThingList(Find.CurrentMap).ToList())
			{
				float num = t.MarketValue;
				if (num > 0)
				{
					MoteMaker.ThrowText(cell.ToVector3(), Find.CurrentMap, t.LabelCap + ": " + num.ToStringMoney(), Color.white);
				}
			}
		}

		[DebugAction("DMSRC", "Get region type", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static void GetRegionType()
		{
			IntVec3 intVec = UI.MouseCell();
			Map map = Find.CurrentMap;
			if (intVec.InBounds(map))
			{
				Region r = map.regionGrid.GetRegionAt_NoRebuild_InvalidAllowed(intVec);
				string s = "null";
				if (r != null)
				{
					s = r.type.ToString();
				}
				MoteMaker.ThrowText(intVec.ToVector3(), map, s, Color.white);
			}
		}

		[DebugAction("DMSRC", "Get region count", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static void GetRegionCount()
		{
			IntVec3 intVec = UI.MouseCell();
			Map map = Find.CurrentMap;
			if (intVec.InBounds(map))
			{
				Room r = intVec.GetRoom(map);
				string s = "null";
				if (r != null)
				{
					s = r.RegionCount.ToString();
				}
				MoteMaker.ThrowText(intVec.ToVector3(), map, s, Color.white);
			}
		}

		[DebugAction("DMSRC", "Contact renegades", false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static void Contact()
		{
			//Log.Message("Hours till contact" + GameComponent_Renegades.Find.hoursTillContact);
			GameComponent_Renegades.Find.ContactPlayer();
		}

		[DebugAction("DMSRC", "Tile covered by broadcast", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
		public static void TileCoveredByBroadcast()
		{
			PlanetTile planetTile = GenWorld.MouseTile();
			if (planetTile.Valid)
			{
				Messages.Message(CompBroadcastAntenna.affectedTiles.Contains(planetTile).ToString(), MessageTypeDefOf.NeutralEvent, false);
			}
		}

		[DebugAction("Pawns", "10 damage until dead or downed", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap, hideInSubMenu = true)]
		private static void Do10DamageUntilDeadOrDowned()
		{
			foreach (Thing item in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).ToList())
			{
				for (int i = 0; i < 1000; i++)
				{
					DamageInfo dinfo = new DamageInfo(DamageDefOf.Bullet, 10f);
					dinfo.SetIgnoreInstantKillProtection(ignore: true);
					item.TakeDamage(dinfo);
					Pawn pawn = item as Pawn;
					if (!item.Destroyed && (pawn == null || !pawn.DeadOrDowned))
					{
						continue;
					}
					string text = "Took " + (i + 1) + " hits";
					if (pawn != null)
					{
						if (pawn.health.ShouldBeDeadFromLethalDamageThreshold())
						{
							text = text + " (reached lethal damage threshold of " + pawn.health.LethalDamageThreshold.ToString("0.#") + ")";
						}
						else if (PawnCapacityUtility.CalculatePartEfficiency(pawn.health.hediffSet, pawn.RaceProps.body.corePart) <= 0.0001f)
						{
							text += " (core part hp reached 0)";
						}
						else if (!pawn.Dead && pawn.health.ShouldBeDowned())
						{
							text = text + " (downed)";
						}
						else
						{
							PawnCapacityDef pawnCapacityDef = pawn.health.ShouldBeDeadFromRequiredCapacity();
							if (pawnCapacityDef != null)
							{
								text = text + " (incapable of " + pawnCapacityDef.defName + ")";
							}
						}
					}
					Log.Message(text + ".");
					break;
				}
			}
		}

		[DebugAction("Autotests", null, false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static void GeneratePawnsOfAllKinds()
		{
			LongEventHandler.QueueLongEvent(delegate
			{
				Map map = Find.CurrentMap;
				Thing.allowDestroyNonDestroyable = true;
				try
				{
					foreach (IntVec3 c in CellRect.WholeMap(map).Cells)
					{
						foreach (Thing t in (c).GetThingList(map).ToList())
						{
							t.Destroy();
						}
						map.terrainGrid.SetTerrain(c, TerrainDefOf.Concrete);
					}
				}
				catch (Exception ex)
				{
					Log.Error("Exception while clearing area: " + ex);
				}
				int workingX = 2;
				int workingZ = 2;
				Pawn pawn = null;
				foreach (PawnKindDef allDef in DefDatabase<PawnKindDef>.AllDefs)
				{
					if(allDef is CreepJoinerFormKindDef)
					{
						continue;
					}
					IntVec3 intVec = new IntVec3(workingX, 0, workingZ);
					workingX += 2;
					if (map.Size.x <= workingX + 1)
					{
						workingX = 2;
						workingZ += 2;
						if (map.Size.z <= workingZ + 1)
						{
							break;
						}
					}
					try
					{
						Faction faction = FactionUtility.DefaultFactionFrom(allDef.defaultFactionDef);
						pawn = PawnGenerator.GeneratePawn(allDef, faction);
						GenSpawn.Spawn(pawn, intVec, map);
					}
					catch (Exception ex)
					{
						Log.Error("Exception while generating pawn of " + allDef.defName + " kind : " + ex);
						pawn?.Destroy();
						continue;
					}
					if(pawn != null)
					{
						
						if(pawn.Name != null && pawn.Name.IsValid)
						{
							if(pawn.Name is NameSingle)
							{
								pawn.Name = new NameSingle(allDef.defName);
							}
							else if (pawn.Name is NameTriple)
							{
								pawn.Name = new NameTriple(allDef.defName, allDef.defName, allDef.defName);
							}
						}
					}
				}

			}, "DMSRC_RecalculatingTiles", doAsynchronously: false, null);

		}

	}

	public static class RPrefabUtility
	{
		private static List<RPrefabDef> defs;

		public static List<RPrefabDef> Defs
		{
			get
			{
				if (defs == null)
				{
					defs = DefDatabase<RPrefabDef>.AllDefsListForReading;
				}
				return defs;
			}
		}

		public static RPrefabDef GetByTag(string tag)
		{
			if (Defs.TryRandomElement((x) => x.tags.Contains(tag), out var result))
			{
				return result;
			}
			return null;
		}

		public static bool TryGetByTag(string tag, out RPrefabDef result)
		{
			result = null;
			if (Defs.TryRandomElement((x) => x.tags.Contains(tag), out result))
			{
				return true;
			}
			return false;
		}

		public static CellRect Clear(this CellRect rect, Map map)
		{
			Thing.allowDestroyNonDestroyable = true;
			try
			{
				foreach (IntVec3 c in rect.Cells)
				{
					foreach (Thing t in (c).GetThingList(map).ToList())
					{
						t.Destroy();
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Exception while clearing area: " + ex);
			}
			return rect;
		}
	}
}