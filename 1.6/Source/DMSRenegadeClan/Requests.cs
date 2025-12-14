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
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.NetworkInformation;
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
using Verse.Sound;
using static DMSRC.TradeRequest;

namespace DMSRC
{
	public class RenegadesRequestDef : Def
	{
		public Type requestClass = typeof(RenegadesRequest);

		public SitePartDef sitePartDef;
	}

	public class RenegadesRequest : IExposable, ILoadReferenceable
	{
		public int ID;

		public RenegadesRequestDef def;

		public int silver = -1;

		public int ticksRequested = -1;

		public int ticksPayed = -1;

		public int ticksBeforeArrival = -1;

		public bool paySiteCreated = false;

		public bool arrived = false;

		public bool payedWithRep = false;

		public PlanetTile tile = PlanetTile.Invalid;

		public RenegadesRequest()
		{

		}
		public virtual void ExposeData()
		{
			Scribe_Values.Look(ref ID, "ID", -1);
			Scribe_Defs.Look(ref def, "def");
			Scribe_Values.Look(ref silver, "silver", -1);
			Scribe_Values.Look(ref ticksRequested, "ticksRequested", -1);
			Scribe_Values.Look(ref ticksPayed, "ticksPayed", -1);
			Scribe_Values.Look(ref ticksBeforeArrival, "ticksBeforeArrival", -1);
			Scribe_Values.Look(ref paySiteCreated, "paySiteCreated", defaultValue: false);
			Scribe_Values.Look(ref payedWithRep, "payedWithRep", defaultValue: false);
			Scribe_Values.Look(ref tile, "tile");
		}

		public float ChanceToFire
		{
			get
			{
				int ticks = Find.TickManager.TicksGame - ticksRequested;
				if (ticks < 30000)
				{
					return 0f;
				}
				if (ticks > 120000)
				{
					return 1f;
				}
				return Mathf.Lerp(0f, 1f, ticks / 180000f);
			}
		}

		public virtual void Tick()
		{
			if (!payedWithRep && !paySiteCreated)
			{
				if (Rand.Chance(ChanceToFire))
				{
					RequestSite worldObject = (RequestSite)WorldObjectMaker.MakeWorldObject(RCDefOf.DMSRC_RequestSite);
					TileFinder.TryFindNewSiteTile(out var tile, Find.RandomSurfacePlayerHomeMap.Tile, 2, 7, false, null, 0f, false, TileFinderMode.Random, exitOnFirstTileFound: false, false);
					worldObject.Tile = tile;
					worldObject.request = this;
					worldObject.SetFaction(GameComponent_Renegades.Find.RenegadesFaction);
					Find.WorldObjects.Add(worldObject);
					paySiteCreated = true;
				}
			}
			else if (!arrived && (payedWithRep || ticksPayed > 0))
			{
				ticksBeforeArrival--;
				if (ticksBeforeArrival < 0)
				{
					Arrive();
					arrived = true;
				}
			}
		}

		public virtual void Arrive()
		{

		}

		public string GetUniqueLoadID()
		{
			return GetType().Name + "_" + ID + "N";
		}

		public virtual void Complete()
		{
			GameComponent_Renegades.Find.requests.Remove(this);
		}

		public virtual void DrawTab(Rect rect, ref Vector2 scrollPosition, float viewHeight, GameComponent_Renegades renegades)
		{

		}

		public virtual AcceptanceReport TrySave(GameComponent_Renegades renegades, Map map)
		{
			return true;
		}

		public virtual Rect DoInterface(float x, float y, float width)
		{
			Rect rect = new Rect(x, y, width, 100f);
			Color color = Color.white;
			Widgets.DrawAltRect(rect);
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.BeginGroup(rect);
			Rect rect1 = new Rect(0, 0, width - 25f, 25f);
			Widgets.Label(rect1, GenDate.DateFullStringAt(ticksRequested, Find.WorldGrid.LongLatOf(tile)));
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.InfoCardButton(new Rect(width - 25f, 0, 25f, 25f), def);
			float f = 25f;
			int ticks = Find.TickManager.TicksGame - ticksRequested;
			if (!payedWithRep && ticksPayed == -1)
			{
				Widgets.Label(new Rect(0, f, width, 25f), "Requires".Translate() + ": " + ThingDefOf.Silver.LabelCap + " x" + silver);
				f += 25f;
				Widgets.Label(new Rect(0, f, width, 25f), ticks < 30000 ? "DMSRC_PaySiteAfter_Range".Translate((30000 - ticks).ToStringTicksToDays(), (120000 - ticks).ToStringTicksToDays()) : "DMSRC_PaySiteAfter_Single".Translate((180000 - ticks).ToStringTicksToDays()));
				f += 25f;
			}
			if (!arrived && (payedWithRep || ticksPayed > 0))
			{
				Widgets.Label(new Rect(0, f, width, 25f), "DMSRC_ArrivalAfter_Single".Translate((ticksBeforeArrival * 2500).ToStringTicksToDays()));
				f += 25f;
			}
			Widgets.EndGroup();
			return rect;
		}
	}

	public class RenegadesRequestWithSite : RenegadesRequest
	{
		public override void Arrive()
		{
			SitePartDefWithParams parms = new SitePartDefWithParams(def.sitePartDef, new ContainerSitePartParams
			{
				request = this
			});
			TileFinder.TryFindNewSiteTile(out var tile, Find.RandomSurfacePlayerHomeMap.Tile, 2, 7, false, null, 0f, false, TileFinderMode.Random, exitOnFirstTileFound: false, false);
			Site site = SiteMaker.MakeSite(Gen.YieldSingle(parms), tile, GameComponent_Renegades.Find.RenegadesFaction);
			Find.WorldObjects.Add(site);
		}

		public virtual void FillSite(IntVec3 loc, Map map, GenStepParams parms)
		{

		}
	}

	public class TradeRequest : RenegadesRequestWithSite
	{
		public List<TradeRow> tradeRows = null;

		public class TradeRow
		{
			public int count;

			public Thing thing;

			public int countSelected;

			public string editBuffer;

			public TradeRow(Thing thing, int count)
			{
				this.thing = thing;
				this.count = count;
			}
			public void DrawTradeableRow(Rect rect, bool drawHighlight, out bool changed)
			{
				changed = false;
				if (Mouse.IsOver(rect))
				{
					Widgets.DrawHighlightSelected(rect);
				}
				else if (drawHighlight)
				{
					Widgets.DrawLightHighlight(rect);
				}
				Text.Anchor = TextAnchor.MiddleLeft;
				Text.Font = GameFont.Small;
				Widgets.BeginGroup(rect);
				float width = rect.width;
				float height = rect.height;
				Rect rectBP = new Rect(width - height, 0, height, height);
				Rect rectNum = new Rect(rectBP.x - (height * 2), 0, height * 2, height);
				Rect rectBM = new Rect(rectNum.x - height, 0, height, height);
				Rect rectPrice = new Rect(rectBM.x - (height * 2), 0, (height * 2), height);
				Rect rectCount = new Rect(rectPrice.x - (height * 2), 0, (height * 2), height);
				Rect rectInfo = new Rect(0, 0, height, height);
				Rect rectIcon = new Rect(24f, 0, height, height);
				Rect rectLabel = new Rect(height * 2, 0, rectCount.x - (height * 2), height);
				Widgets.InfoCardButton(3f, 3f, thing);
				Widgets.Label(rectLabel, thing.LabelCapNoCount);
				Widgets.Label(rectCount, count.ToString());
				Widgets.Label(rectPrice, thing.MarketValue.ToStringMoney());
				int c = countSelected;
				Widgets.TextFieldNumeric(rectNum, ref countSelected, ref editBuffer, 0, count);
				if(countSelected != c)
				{
					changed = true;
				}
				Text.Anchor = TextAnchor.MiddleCenter;
				int num = GenUI.CurrentAdjustmentMultiplier();
				if (Widgets.ButtonText(rectBM, "-") && countSelected > 0)
				{
					countSelected = Mathf.Max(countSelected - num, 0);
					SoundDefOf.Tick_High.PlayOneShotOnCamera();
					editBuffer = countSelected.ToStringCached();
					changed = true;
				}
				if (Widgets.ButtonText(rectBP, "+") && countSelected < count)
				{
					countSelected = Mathf.Min(countSelected + num, count);
					SoundDefOf.Tick_High.PlayOneShotOnCamera();
					editBuffer = countSelected.ToStringCached();
					changed = true;
				}
				try
				{
					Widgets.ThingIcon(rectIcon, thing);
				}
				catch (Exception ex)
				{
					Log.Error("Exception drawing thing icon for " + thing.def.defName + ": " + ex.ToString());
				}
				TooltipHandler.TipRegion(rectLabel, thing.DescriptionDetailed);
				GenUI.ResetLabelAlign();
				Widgets.EndGroup();
			}
		}

		public List<Thing> things = new List<Thing>();

		public TradeRequest()
		{

		}

		public override void Tick()
		{
			base.Tick();
		}

		public override void FillSite(IntVec3 loc, Map map, GenStepParams parms)
		{
			CellRect cellRect = CellRect.CenteredOn(loc, 17, 17).ClipInsideMap(map);
			if (!MapGenerator.TryGetVar<List<CellRect>>("UsedRects", out var var))
			{
				var = new List<CellRect>();
				MapGenerator.SetVar("UsedRects", var);
			}
			Building_SecurityContainer container = (Building_SecurityContainer)ThingMaker.MakeThing(RCDefOf.DMSRC_SecurityContainer_Renegades);
			container.innerContainer.ClearAndDestroyContents();
			container.innerContainer.TryAddRangeOrTransfer(things.ToList());
			things.Clear();
			RimWorld.BaseGen.ResolveParams resolveParams = default(RimWorld.BaseGen.ResolveParams);
			resolveParams.rect = cellRect;
			resolveParams.faction = map.ParentFaction;
			resolveParams.singleThingToSpawn = container;
			RimWorld.BaseGen.BaseGen.globalSettings.map = map;
			RimWorld.BaseGen.BaseGen.symbolStack.Push("thing", resolveParams);
			RimWorld.BaseGen.BaseGen.Generate();
			MapGenerator.SetVar("RectOfInterest", cellRect);
			var.Add(cellRect);
		}

		public override void DrawTab(Rect rect, ref Vector2 scrollPosition, float viewHeight, GameComponent_Renegades renegades)
		{
			Widgets.BeginGroup(rect);
			if (tradeRows == null)
			{
				tradeRows = new List<TradeRow>();
				foreach (Thing t in renegades.things)
				{
					tradeRows.Add(new TradeRow(t, t.stackCount));
				}
			}
			Rect first = new Rect(0, 0, rect.width, 30f);
			Widgets.DrawLightHighlight(first);
			float width = rect.width;
			float height = 30f;
			Text.Anchor = TextAnchor.MiddleLeft;
			Text.Font = GameFont.Small;
			Rect rectBP = new Rect(width - height, 0, height, height);
			Rect rectNum = new Rect(rectBP.x - (height * 2), 0, height * 2, height);
			Rect rectBM = new Rect(rectNum.x - height, 0, height, height);
			Rect rectPrice = new Rect(rectBM.x - (height * 2), 0, (height * 2), height);
			Rect rectCount = new Rect(rectPrice.x - (height * 2), 0, (height * 2), height);
			Rect rectInfo = new Rect(0, 0, height, height);
			Rect rectIcon = new Rect(24f, 0, height, height);
			Rect rectLabel = new Rect(height * 2, 0, rectCount.x - (height * 2), height);
			Widgets.InfoCardButton(3f, 3f, ThingDefOf.Silver, null);
			Widgets.Label(rectLabel, ThingDefOf.Silver.LabelCap);
			Widgets.Label(rectPrice, CalculateSilver().ToStringMoney());
			if (Mouse.IsOver(first))
			{
				Widgets.DrawHighlight(first);
				TooltipHandler.TipRegionByKey(first, "DMSRC_Currency");
			}
			try
			{
				Widgets.ThingIcon(rectIcon, ThingDefOf.Silver);
			}
			catch (Exception ex)
			{
				Log.Error("Exception drawing thing icon for " + ThingDefOf.Silver.defName + ": " + ex.ToString());
			}
			Rect outRect = new Rect(0, 30f, rect.width, rect.height - 42f);
			Rect viewRect = new Rect(0, 30f, outRect.width - 16f, (tradeRows.Count + 2) * 30f);
			bool drawHighlight = false;
			float num = 30f;
			//Widgets.DrawLineHorizontal(0f, 32f, viewRect.width);
			Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
			foreach (TradeRow tradeRow in tradeRows)
			{
				Rect rowRect = new Rect(0f, num, viewRect.width, 30f);
				tradeRow.DrawTradeableRow(rowRect, drawHighlight, out var changed);
				if (changed)
				{
					calculated = false;
				}
				num += 30f;
				drawHighlight = !drawHighlight;
			}
			Widgets.EndScrollView();
			Widgets.EndGroup();
		}

		public override Rect DoInterface(float x, float y, float width)
		{
			return base.DoInterface(x, y, width);
		}

		
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref things, "things", LookMode.Deep);
		}

		public override AcceptanceReport TrySave(GameComponent_Renegades renegades, Map map)
		{
			float num = 0f;
			things = new List<Thing>();
			foreach (TradeRow row in tradeRows.ToList())
			{
				if (row.countSelected > 0)
				{
					if (row.countSelected >= row.count)
					{
						renegades.things.Remove(row.thing);
						things.Add(row.thing);
					}
					else things.Add(row.thing.SplitOff(row.countSelected));
					num += things.Last().MarketValue;
				}
			}
			if (things.NullOrEmpty())
			{
				return "DMSRC_NoThingsSelected".Translate();
			}
			silver = Mathf.RoundToInt(num);
			tradeRows.Clear();
			tradeRows = null;
			ticksRequested = Find.TickManager.TicksGame;
			tile = map.Tile;
			return base.TrySave(renegades, map);
		}

		private bool calculated = false;

		private float silverTemp;

		public float CalculateSilver()
		{
			if (!calculated)
			{
				silverTemp = 0f;
				foreach (TradeRow row in tradeRows)
				{
					if (row.countSelected > 0)
					{
						silverTemp += row.thing.MarketValue * row.countSelected;
					}
				}
				calculated = true;
				silverTemp = Mathf.RoundToInt(silverTemp);
			}
			return silverTemp;
		}
	}
}