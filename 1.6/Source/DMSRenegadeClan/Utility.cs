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
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using static DMSRC.GenStep_RPrefab;

namespace DMSRC
{
    public static class OverseerMechUtility
    {
        public static CompOverseerMech GetOverseerMech(Pawn dummy)
        {
            if(dummy == null || dummy.kindDef != RCDefOf.DMSRC_DummyMechanitor || dummy.mechanitor == null || dummy.health?.hediffSet == null)
            {
                return null;
            }
            return dummy.health.hediffSet.GetFirstHediff<Hediff_DummyPawn>()?.overseer.Comp;
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

		[DebugAction("DMSRC", "Contact renegades", false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static void Contact()
		{
			//Log.Message("Hours till contact" + GameComponent_Renegades.Find.hoursTillContact);
			GameComponent_Renegades.Find.contacted = true;
		}
	}

	public static class RPrefabUtility
	{
        private static List<RPrefabDef> defs;

		public static List<RPrefabDef> Defs
        {
            get
            {
                if(defs == null)
                {
                    defs = DefDatabase<RPrefabDef>.AllDefsListForReading;
                }
                return defs;
            }
        }

        public static RPrefabDef GetByTag(string tag)
        {
            if(Defs.TryRandomElement((x) => x.tags.Contains(tag), out var result))
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