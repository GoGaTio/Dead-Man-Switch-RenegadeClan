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
using static HarmonyLib.Code;

namespace DMSRC
{
	public class GenStep_RPrefab : GenStep
	{
		public List<RPrefabDef> prefabs;

		public string tag;

		public bool useStaticPrefab = false;

		public static RPrefabDef staticPrefab;

		public override int SeedPart => 913431596;

		public override void Generate(Map map, GenStepParams parms)
		{
			RPrefabDef prefab;
			if (useStaticPrefab)
			{
				prefab = staticPrefab;
			}
			else if (tag.NullOrEmpty() || !RPrefabUtility.TryGetByTag(tag, out prefab))
			{
				prefab = prefabs.RandomElement();
			}
			Rot4 rot = Rot4.Random;
			IntVec2 size = prefab.size;
			if(rot == Rot4.East ||  rot == Rot4.West)
			{
				size = size.Rotated();
			}
			CellRect rect = CellRect.WholeMap(map).ContractedBy(10);
			List<CellRect> largestClearRects = MapGenUtility.GetLargestClearRects(map, size, size * 2, true, -1f, 0.7f, TerrainAffordanceDefOf.Light);
			largestClearRects.RemoveAll(r => !r.FullyContainedWithin(rect));
			List<CellRect> orGenerateVar = MapGenerator.GetOrGenerateVar<List<CellRect>>("UsedRects");
			IntVec3 center = largestClearRects.NullOrEmpty() ? map.Center : largestClearRects.RandomElement().CenterCell;
			Faction faction = map.IsPocketMap ? map.PocketMapParent.sourceMap.ParentFaction : map.ParentFaction;
			var list = new List<Thing>();
			prefab.Generate(center, rot, map, faction, ref list);
			map.powerNetManager.UpdatePowerNetsAndConnections_First();
			UpdateDesiredPowerOutputForAllGenerators(map);
		}

		public static void UpdateDesiredPowerOutputForAllGenerators(Map map)
		{
			List<Thing> tmpThings = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
			for (int i = 0; i < tmpThings.Count; i++)
			{
				if (IsPowerGenerator(tmpThings[i]))
				{
					tmpThings[i].TryGetComp<CompPowerPlant>()?.UpdateDesiredPowerOutput();
				}
			}
		}

		private static bool IsPowerGenerator(Thing thing)
		{
			if (thing.TryGetComp<CompPowerPlant>() != null)
			{
				return true;
			}
			CompPowerTrader compPowerTrader = thing.TryGetComp<CompPowerTrader>();
			if (compPowerTrader != null)
			{
				if (!(compPowerTrader.PowerOutput > 0f))
				{
					if (!compPowerTrader.PowerOn)
					{
						return compPowerTrader.Props.PowerConsumption < 0f;
					}
					return false;
				}
				return true;
			}
			return false;
		}
	}
}