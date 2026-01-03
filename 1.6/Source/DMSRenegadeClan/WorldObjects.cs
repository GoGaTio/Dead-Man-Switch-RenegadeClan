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

		public RenegadesRequest request;

		public int silver;

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

		public override string GetInspectString()
		{
			string s = base.GetInspectString();
			if (!s.NullOrEmpty())
			{
				s += "\n";
			}
			s += "Requires".Translate() + ": " + ThingDefOf.Silver.LabelCap + " x" + request.silver;
			return s;
		}

		public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
		{
			foreach(Gizmo g in base.GetCaravanGizmos(caravan))
			{
				yield return g;
			}
			if(!caravan.pather.Moving && caravan.Tile == this.Tile)
			{
				yield return FulfillRequestCommand(caravan);
			}
		}

		private Command FulfillRequestCommand(Caravan caravan)
		{
			Command_Action command_Action = new Command_Action();
			command_Action.defaultLabel = "CommandFulfillTradeOffer".Translate();
			command_Action.defaultDesc = "CommandFulfillTradeOfferDesc".Translate();
			command_Action.icon = TradeCommandTex;
			command_Action.action = delegate
			{
				if (!CaravanInventoryUtility.HasThings(caravan, ThingDefOf.Silver, request.silver))
				{
					Messages.Message("CommandFulfillTradeOfferFailInsufficient".Translate(TradeRequestUtility.RequestedThingLabel(ThingDefOf.Silver, request.silver - silver)), MessageTypeDefOf.RejectInput, historical: false);
				}
				else
				{
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CommandFulfillTradeOfferConfirm".Translate(GenLabel.ThingLabel(ThingDefOf.Silver, null, request.silver)), delegate
					{
						Fulfill(caravan);
					}));
				}
			};
			if (!CaravanInventoryUtility.HasThings(caravan, ThingDefOf.Silver, request.silver))
			{
				command_Action.Disable("CommandFulfillTradeOfferFailInsufficient".Translate(TradeRequestUtility.RequestedThingLabel(ThingDefOf.Silver, request.silver)));
			}
			return command_Action;
		}

		private void Fulfill(Caravan caravan)
		{
			int remaining = request.silver - silver;
			List<Thing> list = CaravanInventoryUtility.TakeThings(caravan, delegate (Thing thing)
			{
				if (ThingDefOf.Silver != thing.def)
				{
					return 0;
				}
				int num2 = Mathf.Min(remaining, thing.stackCount);
				remaining -= num2;
				return num2;
			});
			for (int num = 0; num < list.Count; num++)
			{
				list[num].Destroy();
			}
			Fulfilled();
		}

		public void DropItems(List<Thing> items)
		{
			foreach (Thing thing in items.ToList())
			{
				if(thing.def == ThingDefOf.Silver)
				{
					silver += thing.stackCount;
				}
				thing.Destroy();
			}
			if(silver >= request.silver)
			{
				Fulfilled();
			}
		}

		public void Fulfilled()
		{
			request.ticksPayed = Find.TickManager.TicksGame;
			request.ticksBeforeArrival = new IntRange(25, 80).RandomInRange;
			this.Destroy();
		}

		public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
		{
			foreach (FloatMenuOption floatMenuOption in base.GetFloatMenuOptions(caravan))
			{
				yield return floatMenuOption;
			}
			foreach (FloatMenuOption floatMenuOption2 in CaravanArrivalAction_VisitRequestSite.GetFloatMenuOptions(caravan, this))
			{
				yield return floatMenuOption2;
			}
		}

		public override IEnumerable<FloatMenuOption> GetTransportersFloatMenuOptions(IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction)
		{
			foreach (FloatMenuOption transportersFloatMenuOption in base.GetTransportersFloatMenuOptions(pods, launchAction))
			{
				yield return transportersFloatMenuOption;
			}
			foreach (FloatMenuOption floatMenuOption in TransportersArrivalAction_GiveToRenegades.GetFloatMenuOptions(launchAction, pods, this))
			{
				yield return floatMenuOption;
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref request, "DMSRC_renegadesRequest");
			Scribe_Values.Look(ref silver, "DMSRC_silver");
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
			site.DropItems(tmpContainedThings);
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