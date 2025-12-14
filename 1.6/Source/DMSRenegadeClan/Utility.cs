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
		[DebugAction("DMSRC", "Trackers report", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void TrackersReport(Pawn p)
		{
            if(p != null)
            {
                string s = "Needs report:" + "\n";
                if(p.ageTracker == null)
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

        [DebugAction("DMSRC", "Add module...", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddModule(Pawn p)
        {
            if (p != null)
            {
                CompModuleNodes comp = p.GetComp<CompModuleNodes>();
                if(comp != null)
                {
                    List<DebugMenuOption> list = new List<DebugMenuOption>();
                    foreach (ModuleNode node in comp.Props.nodes)
                    {
                        ModuleNode localNode = node;
                        list.Add(new DebugMenuOption(localNode.name, DebugMenuOptionMode.Action, delegate
                        {
                            List<DebugMenuOption> list2 = new List<DebugMenuOption>(); 
                            foreach (ThingDef item in Modules)
                            {
                                ThingDef item2 = item;
                                if(item2.GetCompProperties<CompProperties_Module>().tags.Any((string x)=> localNode.tags.Contains(x)))
                                {
                                    list2.Add(new DebugMenuOption(item2.defName, DebugMenuOptionMode.Action, delegate
                                    {
                                        comp.TryAddModule(ThingMaker.MakeThing(item2) as ThingWithComps, localNode.name);
                                    }));
                                }
                            }
                            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list2));
                        }));
                    }
                    Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
                }
            }
        }

        [DebugAction("DMSRC", "Deactivate mech", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OpenMarket(Pawn p)
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

		/*[DebugAction("Generation", null, false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void CreatePrefab()
		{
			DebugToolsGeneral.GenericRectTool("Create", delegate (CellRect rect)
			{
				PrefabDef prefabDef = PrefabUtility.CreatePrefab(rect, DebugGenerationSettings.prefabCopyAllThings, DebugGenerationSettings.prefabCopyTerrain);
				StringBuilder stringBuilder = new StringBuilder();
				string text = "  ";
				stringBuilder.AppendLine("\n<DMSRC.RPrefabDef>");
				stringBuilder.AppendLine(text + "<defName></defName>");
				stringBuilder.AppendLine($"{text}<size>({rect.Size.x},{rect.Size.z})</size>");
				if (prefabDef.things.CountAllowNull() > 0)
				{
					stringBuilder.AppendLine(text + "<things>");
					for (int i = 0; i < prefabDef.things.Count; i++)
					{
						PrefabThingData prefabThingData = prefabDef.things[i];
						stringBuilder.AppendLine(text + text + "<" + prefabThingData.def.defName + ">");
						if (prefabThingData.rects != null)
						{
							stringBuilder.AppendLine(text + text + text + "<rects>");
							foreach (CellRect rect in prefabThingData.rects)
							{
								stringBuilder.AppendLine($"{text}{text}{text}{text}<li>{rect}</li>");
							}
							stringBuilder.AppendLine(text + text + text + "</rects>");
						}
						else if (prefabThingData.positions != null)
						{
							stringBuilder.AppendLine(text + text + text + "<positions>");
							foreach (IntVec3 position in prefabThingData.positions)
							{
								stringBuilder.AppendLine($"{text}{text}{text}{text}<li>{position}</li>");
							}
							stringBuilder.AppendLine(text + text + text + "</positions>");
						}
						else
						{
							stringBuilder.AppendLine($"{text}{text}{text}<position>{prefabThingData.position}</position>");
						}
						if (prefabThingData.relativeRotation != RotationDirection.None)
						{
							stringBuilder.AppendLine(text + text + text + "<relativeRotation>" + Enum.GetName(typeof(RotationDirection), prefabThingData.relativeRotation) + "</relativeRotation>");
						}
						if (prefabThingData.stuff != null)
						{
							stringBuilder.AppendLine(text + text + text + "<stuff>" + prefabThingData.stuff.defName + "</stuff>");
						}
						if (prefabThingData.quality.HasValue)
						{
							stringBuilder.AppendLine($"{text}{text}{text}<quality>{prefabThingData.quality}</quality>");
						}
						if (prefabThingData.hp != 0)
						{
							stringBuilder.AppendLine($"{text}{text}{text}<hp>{prefabThingData.hp}</hp>");
						}
						if (prefabThingData.stackCountRange != IntRange.One)
						{
							stringBuilder.AppendLine($"{text}{text}{text}<stackCountRange>{prefabThingData.stackCountRange.min}~{prefabThingData.stackCountRange.max}</stackCountRange>");
						}
						if (prefabThingData.colorDef != null)
						{
							stringBuilder.AppendLine($"{text}{text}{text}<colorDef>{prefabThingData.colorDef}</colorDef>");
						}
						if (prefabThingData.color != default(Color))
						{
							stringBuilder.AppendLine($"{text}{text}{text}<color>{prefabThingData.color}</color>");
						}
						stringBuilder.AppendLine(text + text + "</" + prefabThingData.def.defName + ">");
					}
					stringBuilder.AppendLine(text + "</things>");
				}
				if (prefabDef.terrain.CountAllowNull() > 0)
				{
					stringBuilder.AppendLine(text + "<terrain>");
					foreach (PrefabTerrainData item in prefabDef.terrain)
					{
						stringBuilder.AppendLine(text + text + "<" + item.def.defName + ">");
						if (item.color != null)
						{
							stringBuilder.AppendLine($"{text}{text}{text}<color>{item.color}</color>");
						}
						stringBuilder.AppendLine(text + text + text + "<rects>");
						foreach (CellRect rect2 in item.rects)
						{
							stringBuilder.AppendLine($"{text}{text}{text}{text}<li>{rect2}</li>");
						}
						stringBuilder.AppendLine(text + text + text + "</rects>");
						stringBuilder.AppendLine(text + text + "</" + item.def.defName + ">");
					}
					stringBuilder.AppendLine(text + "</terrain>");
				}
				stringBuilder.AppendLine("</DMSRC.RPrefabDef>");
				GUIUtility.systemCopyBuffer = stringBuilder.ToString();
				Messages.Message("Copied to clipboard", MessageTypeDefOf.NeutralEvent, historical: false);
			}, closeOnComplete: true);
		}*/

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
                if(t.Faction == null)
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

		private static List<ThingDef> modules = new List<ThingDef>();
        public static List<ThingDef> Modules
        {
            get
            {
                if (modules.NullOrEmpty())
                {
                    modules = new List<ThingDef>();
                    List<ThingDef> list = DefDatabase<ThingDef>.AllDefs.ToList();
                    foreach(ThingDef def in list)
                    {
                        if (def.comps.Any() && def.comps.Any((CompProperties x) => typeof(CompModule).IsAssignableFrom(x.compClass)))
                        {
                            modules.Add(def);
                        }
                    }
                }
                return modules;
            }
        }
        public static CompOverseerMech GetOverseerMech(Pawn dummy)
        {
            if(dummy == null || dummy.kindDef != RCDefOf.DMSRC_DummyMechanitor || dummy.mechanitor == null || dummy.health?.hediffSet == null)
            {
                return null;
            }
            return dummy.health.hediffSet.GetFirstHediff<Hediff_DummyPawn>()?.overseer.Comp;
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