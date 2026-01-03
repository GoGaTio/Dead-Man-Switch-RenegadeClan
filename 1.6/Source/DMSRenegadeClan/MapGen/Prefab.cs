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
using static System.Collections.Specialized.BitVector32;

namespace DMSRC
{
	public class RPrefabDef : PrefabDef
	{
		public class SubPrefabProps
		{
			public IntVec3 pos = IntVec3.Invalid;

			public List<IntVec3> positions = new List<IntVec3>();

			public RotEnum rotations = RotEnum.All;

			public List<string> tags = new List<string>();

			public string tag;
		}

		public List<string> tags = new List<string>();

		public List<CellRect> clearRects = new List<CellRect>();

		public List<CellRect> roofRects = new List<CellRect>();

		public List<CellRect> clearRoofRects = new List<CellRect>();

		public List<SubPrefabProps> subPrefabs = new List<SubPrefabProps>();

		public override void PostLoad()
		{
			base.PostLoad();
		}

		public void Generate(IntVec3 pos, Rot4 rot, Map map, Faction faction, ref List<Thing> things)
		{
			IntVec3 root = PrefabUtility.GetRoot(this, pos, rot);
			Thing.allowDestroyNonDestroyable = true;
			try
			{
				foreach (CellRect r in clearRects)
				{
					foreach (IntVec3 c in r.Cells)
					{
						IntVec3 adjustedLocalPosition = PrefabUtility.GetAdjustedLocalPosition(c, rot);
						foreach (Thing t in (root + adjustedLocalPosition).GetThingList(map).ToList())
						{
							t.Destroy();
						}
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
			List<Thing> generated = new List<Thing>();
			PrefabUtility.SpawnPrefab(this, map, pos, rot, faction, generated);
			foreach(Thing t in generated)
			{
				if (t.TryGetComp<CompRefuelable>(out var comp))
				{
					comp.Refuel(comp.Props.fuelCapacity - comp.Fuel);
				}
			}
			things.AddRange(generated);
			foreach (CellRect r in roofRects)
			{
				foreach (IntVec3 c in r.Cells)
				{
					IntVec3 adjustedLocalPosition = PrefabUtility.GetAdjustedLocalPosition(c, rot);
					IntVec3 cell = root + adjustedLocalPosition;
					if (!cell.Roofed(map))
					{
						map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
					}
				}
			}
			foreach (CellRect r in clearRoofRects)
			{
				foreach (IntVec3 c in r.Cells)
				{
					IntVec3 adjustedLocalPosition = PrefabUtility.GetAdjustedLocalPosition(c, rot);
					IntVec3 cell = root + adjustedLocalPosition;
					map.roofGrid.SetRoof(cell, null);
				}
			}
			foreach (SubPrefabProps sub in subPrefabs)
			{
				if (sub.pos.IsValid)
				{
					if (RPrefabUtility.Defs.TryRandomElement((x) => x.tags.Any((y) => sub.tags.Contains(y) || sub.tag == y), out var result))
					{
						IntVec3 adjustedLocalPosition = PrefabUtility.GetAdjustedLocalPosition(sub.pos, rot);
						int num = rot.AsInt + sub.rotations.Random().AsInt;
						if (num > 3)
						{
							num -= 4;
						}
						result.Generate(root + adjustedLocalPosition, new Rot4(num), map, faction, ref things);
					}
					
				}
				for (int i = 0; i < sub.positions.Count; i++)
				{
					if (RPrefabUtility.Defs.TryRandomElement((x) => x.tags.Any((y) => sub.tags.Contains(y) || sub.tag == y), out var result2))
					{
						IntVec3 adjustedLocalPosition2 = PrefabUtility.GetAdjustedLocalPosition(sub.positions[i], rot);
						int num2 = rot.AsInt + sub.rotations.Random().AsInt;
						if (num2 > 3)
						{
							num2 -= 4;
						}
						result2.Generate(root + adjustedLocalPosition2, new Rot4(num2), map, faction, ref things);
					}
				}
			}
		}
	}
}