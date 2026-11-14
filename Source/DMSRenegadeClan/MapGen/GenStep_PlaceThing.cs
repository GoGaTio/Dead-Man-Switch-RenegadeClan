using DelaunatorSharp;
using Fortified.Structures;
using Gilzoide.ManagedJobs;
using HarmonyLib;
using Ionic.Crc;
using Ionic.Zlib;
using JetBrains.Annotations;
using KTrie;
using LudeonTK;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
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
using static HarmonyLib.Code;

namespace DMSRC
{
	public class GenStep_PlaceThing : GenStep
	{
		public GenStep_PlaceThing() { }

		public override int SeedPart => 394817398;

		public List<ThingDef> thingDefs;

		public FactionDef forcedFaction;

		public RotEnum allowedRotation = RotEnum.All;

		public override void Generate(Map map, GenStepParams parms)
		{
			if (thingDefs.NullOrEmpty()) return;
			Faction faction = null;
			if (forcedFaction != null)
			{
				faction = Find.FactionManager.FirstFactionOfDef(forcedFaction);
			}
			if (faction == null)
			{
				faction = map.ParentFaction ?? parms.sitePart?.site?.Faction;
			}
            ThingDef def = thingDefs.RandomElement();
			Rot4 rot = allowedRotation.Random();
			IntVec2 size = def.Size;
			if (rot.IsHorizontal)
			{
                size = size.Rotated();
			}
			if (CellRect.WholeMap(map).ContractedBy(5).TryFindRandomInnerRect(size, out var rect, r => !r.Cells.Any(c => c.Fogged(map) || !GenSpawn.CanSpawnAt(def, c, map, rot))) || CellRect.WholeMap(map).TryFindRandomInnerRect(size, out rect, r => GenSpawn.CanSpawnAt(def, r.CenterCell, map, rot)))
			{
				Thing.allowDestroyNonDestroyable = true;
				try
				{
					foreach (IntVec3 c in rect)
					{
						map.roofGrid.SetRoof(c, null);
						foreach (Thing t in c.GetThingList(map).ToList())
						{
							if (t is Plant || t.def.building?.isNaturalRock == true)
							{
								continue;
							}
							t.Destroy();
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error("Exception while clearing area: " + ex);
				}
				finally
				{
					Thing.allowDestroyNonDestroyable = false;
				}
				GenSpawn.Spawn(def, rect.CenterCell, map, rot);
			}
		}
	}
}