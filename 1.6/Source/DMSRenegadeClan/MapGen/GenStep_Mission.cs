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
			IntVec3 center = map.Center;
			Faction faction = map.IsPocketMap ? map.PocketMapParent.sourceMap.ParentFaction : map.ParentFaction;
			Rot4 rot = Rot4.Random;
			if (useStaticPrefab)
			{
				staticPrefab.Generate(center, rot, map, faction);
			}
			else if (!tag.NullOrEmpty() && RPrefabUtility.TryGetByTag(tag, out var result))
			{
				result.Generate(center, rot, map, faction);
			}
			else prefabs.RandomElement().Generate(center, rot, map, faction);
		}
	}
}