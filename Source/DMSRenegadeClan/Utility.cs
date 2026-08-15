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

		public static IOverseer GetOverseerMech(this Pawn dummy)
		{
			if (dummy == null || dummy.kindDef != RCDefOf.DMSRC_DummyMechanitor || dummy.mechanitor == null || dummy.health?.hediffSet == null)
			{
				return null;
			}
			return dummy.health.hediffSet.GetFirstHediff<Hediff_DummyPawn>()?.overseer;
		}

		public static Pawn GetOverseerPawn(this Pawn dummy)
		{
			if (dummy == null || dummy.kindDef != RCDefOf.DMSRC_DummyMechanitor || dummy.mechanitor == null || dummy.health?.hediffSet == null)
			{
				return null;
			}
			return dummy.health.hediffSet.GetFirstHediff<Hediff_DummyPawn>()?.overseer as Pawn;
		}

		public static Pawn GetOverseerPawn(this Pawn dummy, out IOverseer overseer)
		{
			overseer = null;
			if (dummy == null || dummy.kindDef != RCDefOf.DMSRC_DummyMechanitor || dummy.mechanitor == null || dummy.health?.hediffSet == null)
			{
				return null;
			}
			overseer = dummy.health.hediffSet.GetFirstHediff<Hediff_DummyPawn>()?.overseer;
			return overseer as Pawn;
		}
	}

	public static class RCToolsUtility
	{
		[DebugAction("DMSRC", "Deactivate mech", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static void Deactivate(Pawn p)
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