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

namespace DMSRC
{
	public class SitePartWorker_RenegadesRequest : SitePartWorker
	{
		public override string GetPostProcessedThreatLabel(Site site, SitePart sitePart)
		{
			string text = base.GetPostProcessedThreatLabel(site, sitePart);
			if (site.HasWorldObjectTimeout)
			{
				text += " (" + "DurationLeft".Translate(site.WorldObjectTimeoutTicksLeft.ToStringTicksToPeriod()) + ")";
			}
			return text;
		}
	}

	public class GenStep_RenegadesRequest : GenStep_Scatterer
	{
		public override int SeedPart => 913432597;

		protected override bool CanScatterAt(IntVec3 c, Map map)
		{
			if (!base.CanScatterAt(c, map))
			{
				return false;
			}
			if (!c.SupportsStructureType(map, TerrainAffordanceDefOf.Heavy))
			{
				return false;
			}
			if (!map.reachability.CanReachMapEdge(c, TraverseParms.For(TraverseMode.PassDoors)))
			{
				return false;
			}
			CellRect rect = CellRect.CenteredOn(c, 5, 5);
			if (MapGenerator.TryGetVar<List<CellRect>>("UsedRects", out var var) && var.Any((CellRect x) => x.Overlaps(rect)))
			{
				return false;
			}
			foreach (IntVec3 item in rect)
			{
				if (!item.InBounds(map) || item.GetEdifice(map) != null)
				{
					return false;
				}
			}
			return true;
		}

		protected override void ScatterAt(IntVec3 loc, Map map, GenStepParams parms, int count = 1)
		{
			if(map.Parent is Site site && site.parts.Count > 0)
			{
				SitePart part = site.parts.FirstOrDefault((x)=> x.parms is ContainerSitePartParams);
				if(part != null && part.parms is ContainerSitePartParams conparms && conparms.request != null && conparms.request is RenegadesRequestWithSite rrws)
				{
					rrws.FillSite(loc, map, parms);
					rrws.Complete();
				}
			}
		}
	}
}