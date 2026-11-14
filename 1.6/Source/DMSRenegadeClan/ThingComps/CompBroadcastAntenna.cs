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
using System.Reflection.Emit;
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
using static System.Net.Mime.MediaTypeNames;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
using static UnityEngine.GraphicsBuffer;

namespace DMSRC
{
	public class CompProperties_BroadcastAntenna : CompProperties
	{
		public float worldRange;

		public float worldRangeOtherLayer;

		public CompProperties_BroadcastAntenna()
		{
			compClass = typeof(CompBroadcastAntenna);
		}
	}

	[StaticConstructorOnStartup]
	public class CompBroadcastAntenna : ThingComp
	{
		public static List<CompBroadcastAntenna> broadcastAntennas = new List<CompBroadcastAntenna>();

		public static List<PlanetTile> affectedTiles = new List<PlanetTile>();

		public static readonly Material RadiusMat = MaterialPool.MatFrom(GenDraw.OneSidedLineOpaqueTexPath, ShaderDatabase.WorldOverlayAdditiveTwoSided, new Color(0.24f, 0.53f, 0.21f), 3580);

		public CompProperties_BroadcastAntenna Props => (CompProperties_BroadcastAntenna)props;

		public static bool Affects(Map map)
		{
			if (affectedTiles.Contains(map.Tile))
			{
				return true;
			}
			return false;
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			if(parent.Faction == Faction.OfPlayerSilentFail && parent.GetComp<CompPowerTrader>()?.PowerOn != false)
			{
				broadcastAntennas.Add(this);
				Update();
			}
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
		{
			base.PostDeSpawn(map, mode);
			if (parent.Faction == Faction.OfPlayerSilentFail && broadcastAntennas.Remove(this))
			{
				Update();
			}
		}

		public override void ReceiveCompSignal(string signal)
		{
			if (parent.Faction == Faction.OfPlayerSilentFail)
			{
				if (signal == "PowerTurnedOff" && broadcastAntennas.Remove(this))
				{
					Update();
				}
				if (signal == "PowerTurnedOn")
				{
					broadcastAntennas.Add(this);
					Update();
				}
			}
		}

		public void Update()
		{
			broadcastAntennas.RemoveDuplicates();
			affectedTiles.Clear();
			foreach(CompBroadcastAntenna comp in broadcastAntennas)
			{
				comp.RecalculateTiles();
			}
		}

		public void RecalculateTiles()
		{
			LongEventHandler.QueueLongEvent(delegate
			{
				try
				{
					PlanetTile rootTile = parent.Tile;
					affectedTiles.Add(rootTile);
					WorldGrid grid = Find.WorldGrid;
					foreach (PlanetLayer l in grid.PlanetLayers.Values)
					{
						float distance;
						PlanetTile tile = rootTile;
						if (l == rootTile.Layer)
						{
							distance = Props.worldRange;
						}
						else
						{
							distance = Props.worldRangeOtherLayer;
							tile = l.GetClosestTile_NewTemp(rootTile);
						}
						distance /= l.Def.rangeDistanceFactor;
						float distanceApprox = distance * 1.5f;
						foreach (Tile t in l.Tiles)
						{
							if (!affectedTiles.Contains(t.tile) && t.PrimaryBiome?.impassable == false && Find.WorldGrid.ApproxDistanceInTiles(tile, t.tile) <= distanceApprox && Find.WorldGrid.TraversalDistanceBetween(tile, t.tile, passImpassable: true, int.MaxValue, canTraverseLayers: true) <= distance)
							{
								affectedTiles.Add(t.tile);
							}
						}
					}
				}
				catch(Exception ex)
				{
					DelayedErrorWindowRequest.Add("DMSRC_ErrorWhileRecalculatingTiles".Translate() + ": " + ex, "DMSRC_ErrorWhileRecalculatingTilesTitle".Translate());
				}
				
			}, "DMSRC_RecalculatingTiles", doAsynchronously: false, null);
		}
	}
}