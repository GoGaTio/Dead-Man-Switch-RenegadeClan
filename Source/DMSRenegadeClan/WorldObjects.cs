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
using System.Runtime.Remoting.Lifetime;
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
using Verse.Noise;

namespace DMSRC
{
	[StaticConstructorOnStartup]
	public class RequestSite : WorldObject
	{
		private static readonly Texture2D TradeCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/FulfillTradeRequest");

		private Material cachedMat;

		public override Material Material
		{
			get
			{
				if (cachedMat == null)
				{
					cachedMat = MaterialPool.MatFrom(color: (base.Faction == null) ? Color.white : base.Faction.Color, texPath: def.texture, shader: ShaderDatabase.WorldOverlayTransparentLit, renderQueue: 3550);
				}
				return cachedMat;
			}
		}
	}

	public class CaravanArrivalAction_VisitRequestSite : CaravanArrivalAction
	{
		private RequestSite site;

		public override string Label => "Visit".Translate(site.Label);

		public override string ReportString => "CaravanVisiting".Translate(site.Label);

		public CaravanArrivalAction_VisitRequestSite()
		{
		}

		public CaravanArrivalAction_VisitRequestSite(RequestSite site)
		{
			this.site = site;
		}

		public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
		{
			FloatMenuAcceptanceReport floatMenuAcceptanceReport = base.StillValid(caravan, destinationTile);
			if (!floatMenuAcceptanceReport)
			{
				return floatMenuAcceptanceReport;
			}
			if (site != null && site.Tile != destinationTile)
			{
				return false;
			}
			return CanVisit(caravan, site);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref site, "site");
		}

		public override void Arrived(Caravan caravan)
		{
			
		}

		public static FloatMenuAcceptanceReport CanVisit(Caravan caravan, RequestSite site)
		{
			return site != null && site.Spawned;
		}

		public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan, RequestSite site)
		{
			return CaravanArrivalActionUtility.GetFloatMenuOptions(() => CanVisit(caravan, site), () => new CaravanArrivalAction_VisitRequestSite(site), "Visit".Translate(site.Label), caravan, site.Tile, site);
		}
	}

	public class TransportersArrivalAction_GiveToRenegades : TransportersArrivalAction
	{
		private RequestSite site;

		private static readonly List<Thing> tmpContainedThings = new List<Thing>();

		public override bool GeneratesMap => false;

		public TransportersArrivalAction_GiveToRenegades()
		{
		}

		public TransportersArrivalAction_GiveToRenegades(RequestSite site)
		{
			this.site = site;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref site, "site");
		}

		public override FloatMenuAcceptanceReport StillValid(IEnumerable<IThingHolder> pods, PlanetTile destinationTile)
		{
			FloatMenuAcceptanceReport floatMenuAcceptanceReport = base.StillValid(pods, destinationTile);
			if (!floatMenuAcceptanceReport)
			{
				return floatMenuAcceptanceReport;
			}
			if (site != null && !Find.WorldGrid.IsNeighborOrSame(site.Tile, destinationTile))
			{
				return false;
			}
			return CanGiveTo(pods, site);
		}

		public override void Arrived(List<ActiveTransporterInfo> transporters, PlanetTile tile)
		{
			tmpContainedThings.Clear();
			for (int i = 0; i < transporters.Count; i++)
			{
				tmpContainedThings.AddRange(transporters[i].innerContainer);
			}
			//site.DropItems(tmpContainedThings);
			tmpContainedThings.Clear();
			//Messages.Message("MessageTransportPodsArrivedAndAddedToCaravan".Translate(site.Name), site, MessageTypeDefOf.TaskCompletion);
		}

		public static FloatMenuAcceptanceReport CanGiveTo(IEnumerable<IThingHolder> pods, RequestSite site)
		{
			return site != null && site.Spawned;
		}

		public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(Action<PlanetTile, TransportersArrivalAction> launchAction, IEnumerable<IThingHolder> pods, RequestSite site)
		{
			return TransportersArrivalActionUtility.GetFloatMenuOptions(() => CanGiveTo(pods, site), () => new TransportersArrivalAction_GiveToRenegades(site), "GiveToCaravan".Translate(site.Label), launchAction, site.Tile);
		}
	}
}