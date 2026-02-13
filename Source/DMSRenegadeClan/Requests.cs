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
using System.Security.Policy;
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
using static HarmonyLib.Code;
using static UnityEngine.GraphicsBuffer;

namespace DMSRC
{
	public class RenegadesRequestDef : Def
	{
		public Type requestClass = typeof(RenegadesRequest);

		public IntRange hoursBeforeArrival = new IntRange(15, 30);

		public int minGoodwill = 0;

		public bool Available(GameComponent_Renegades comp, out string reason)
		{
			if(minGoodwill > comp.playerGoodwill)
			{
				reason = "DMSRC_NeedMoreGoodwill".Translate(minGoodwill);
				return false;
			}
			reason = null;
			return true;
		}
	}

	public class RenegadesRequest : IExposable, ILoadReferenceable
	{
		public int ID;

		public RenegadesRequestDef def;

		public int ticksRequested = -1;

		public int ticksBeforeArrival = -1;

		public PlanetTile tile = PlanetTile.Invalid;

		public bool arrived = false;

		protected List<Map> mapsTmp = new List<Map>();

		public List<Map> Maps
		{
			get
			{
				if (mapsTmp.NullOrEmpty())
				{
					mapsTmp = Find.Maps;
					mapsTmp.RemoveWhere((m) => m.IsPocketMap || m.generatorDef.isUnderground);
				}
				return mapsTmp;
			}
			set
			{
				mapsTmp = value;
			}
		}

		public Map Map
		{
			get
			{
				var maps = Maps;
				return Maps.FirstOrDefault((m)=>m.Tile == tile);
			}
		}

		public RenegadesRequest()
		{

		}
		public virtual void ExposeData()
		{
			Scribe_Values.Look(ref ID, "ID", -1);
			Scribe_Defs.Look(ref def, "def");
			Scribe_Values.Look(ref arrived, "arrived", false);
			Scribe_Values.Look(ref ticksRequested, "ticksRequested", -1);
			Scribe_Values.Look(ref ticksBeforeArrival, "ticksBeforeArrival", -1);
			Scribe_Values.Look(ref tile, "tile");
		}

		public virtual void Saved()
		{

		}

		public virtual void Tick()
		{
			if (!arrived && CanArrive())
			{
				ticksBeforeArrival--;
				if (ticksBeforeArrival < 0)
				{
					if(Map == null)
					{
						tile = Find.AnyPlayerHomeMap?.Tile ?? Maps.RandomElement().Tile;
					}
					Arrive();
					arrived = true;
				}
			}
		}

		public virtual bool CanArrive() => true;

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

		public virtual AcceptanceReport TrySave(GameComponent_Renegades renegades)
		{
			ticksBeforeArrival = def.hoursBeforeArrival.RandomInRange;
			ticksRequested = Find.TickManager.TicksGame;
			return true;
		}

		public virtual Rect DoInterface(float x, float y, float width, ref float f)
		{
			Rect rect = new Rect(x, y, width, 100f);
			Color color = Color.white;
			Widgets.DrawAltRect(rect);
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.BeginGroup(rect);
			Rect rect1 = new Rect(0, 0, width - 25f, 25f);
			Widgets.Label(rect1, GenDate.DateFullStringAt(ticksRequested, Find.WorldGrid.LongLatOf(tile)));
			Widgets.InfoCardButton(new Rect(width - 25f, 0, 25f, 25f), def);
			f = 25f;
			if (!arrived && CanArrive())
			{
				Widgets.Label(new Rect(0, f, width, 25f), "DMSRC_ArrivalAfter_Single".Translate((ticksBeforeArrival * 2500).ToStringTicksToDays()));
				f += 25f;
			}
			Widgets.EndGroup();
			Text.Anchor = TextAnchor.MiddleLeft;
			return rect;
		}
	}

	public class AidRequest : RenegadesRequest
	{
		public float points = 1000;

		public override void Arrive()
		{
			base.Arrive();
			IncidentParms incidentParms = new IncidentParms();
			incidentParms.target = Find.WorldObjects.MapParentAt(tile).Map;
			incidentParms.faction = GameComponent_Renegades.Find.RenegadesFaction;
			incidentParms.raidArrivalModeForQuickMilitaryAid = true;
			incidentParms.pawnGroupKind = RCDefOf.DMSRC_MilitaryAid;
			incidentParms.podOpenDelay = 10;
			incidentParms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeDrop;
			incidentParms.points = points;
			if (!IncidentDefOf.RaidFriendly.Worker.TryExecute(incidentParms))
			{
				Log.Error("Failed to request military aid");
			}
			Complete();
		}

		public override void DrawTab(Rect rect, ref Vector2 scrollPosition, float viewHeight, GameComponent_Renegades renegades)
		{
			Widgets.BeginGroup(rect);
			Rect first = new Rect(5f, 0, rect.width, 60f);
			Widgets.DrawLightHighlight(first);
			float width = rect.width;
			Text.Anchor = TextAnchor.MiddleLeft;
			Text.Font = GameFont.Small;
			Rect rectSlider = new Rect(first.width - 300, 27, 300, 33);
			Rect rectLabel = new Rect(20, 0, rectSlider.x - 20, 60);
			Widgets.Label(rectLabel, "DMSRC_MilitaryAid".Translate(Mathf.RoundToInt(points), Mathf.RoundToInt(points * 0.01f)));
			points = Widgets.HorizontalSlider(rectSlider, points, 1000f, 10000f, roundTo: 100f);
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.EndGroup();
		}

		public override AcceptanceReport TrySave(GameComponent_Renegades renegades)
		{
			renegades.OffsetGoodwill(-Mathf.RoundToInt(points * 0.01f));
			return base.TrySave(renegades);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref points, "points");
		}
	}

	/*public class WasteDumpRequest : RenegadesRequest
	{
		public int count;

		public WasteDumpRequest()
		{

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
				tradeRows.Sort((TradeRow ltr, TradeRow rtr) => Comparer(ltr, rtr));
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
				TooltipHandler.TipRegion(first, ThingDefOf.Silver.LabelCap.Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + ThingDefOf.Silver.description);
			}
			try
			{
				Widgets.ThingIcon(rectIcon, ThingDefOf.Silver);
			}
			catch (Exception ex)
			{
				Log.Error("Exception drawing thing icon for " + ThingDefOf.Silver.defName + ": " + ex.ToString());
			}
			Rect outRect = new Rect(0, 30f, rect.width, Mathf.FloorToInt((rect.height - 42f) / 30f) * 30f);
			Rect viewRect = new Rect(0, 30f, outRect.width - 16f, (tradeRows.Count) * 30f);
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

		public override void Arrive()
		{
			base.Arrive();
			List<Thing> things = TradeUtility.AllLaunchableThingsForTrade(Map).ToList();
			while (count > 0)
			{

			}
			Find.LetterStack.ReceiveLetter("DMSRC_RenegadesTradeLetter_Label".Translate(), "DMSRC_RenegadesTradeLetter_Text".Translate(), LetterDefOf.PositiveEvent, things);
			Complete();
		}

		public override AcceptanceReport TrySave(GameComponent_Renegades renegades)
		{
			if (!tradeRows.Any((t)=>t.countSelected > 0))
			{
				return "DMSRC_NoThingsSelected".Translate();
			}
			Map map = Map;
			float num = 0f;
			things = new List<Thing>();
			foreach (TradeRow row in tradeRows.ToList())
			{
				if (row.countSelected > 0)
				{
					num += row.thing.MarketValue * row.countSelected;
				}
			}
			int silver = Mathf.RoundToInt(num);
			if(!TradeUtility.ColonyHasEnoughSilver(map, silver))
			{
				return "NeedSilverLaunchable".Translate(silver.ToString());
			}
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
					Thing last = things.Last();
				}
			}
			TradeUtility.LaunchSilver(map, silver);
			tradeRows.Clear();
			tradeRows = null;
			if(renegades.hoursTillRefresh < 0)
			{
				renegades.hoursTillRefresh = new IntRange(240, 480).RandomInRange;
			}
			return base.TrySave(renegades);
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
	}*/

	public class TradeRequest : RenegadesRequest
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
				if (countSelected != c)
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
				tradeRows.Sort((TradeRow ltr, TradeRow rtr) => Comparer(ltr, rtr));
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
				TooltipHandler.TipRegion(first, ThingDefOf.Silver.LabelCap.Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + ThingDefOf.Silver.description);
			}
			try
			{
				Widgets.ThingIcon(rectIcon, ThingDefOf.Silver);
			}
			catch (Exception ex)
			{
				Log.Error("Exception drawing thing icon for " + ThingDefOf.Silver.defName + ": " + ex.ToString());
			}
			Rect outRect = new Rect(0, 30f, rect.width, Mathf.FloorToInt((rect.height - 42f) / 30f) * 30f);
			Rect viewRect = new Rect(0, 30f, outRect.width - 16f, (tradeRows.Count) * 30f);
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

		public static int Comparer(TradeRow ltr, TradeRow rtr)
		{
			int category = TransferableComparer_Category.Compare(ltr.thing.def, rtr.thing.def);
			if (category != 0)
			{
				return category;
			}
			int cost = ltr.thing.MarketValue.CompareTo(rtr.thing.MarketValue);
			if (cost != 0)
			{
				return cost;
			}
			return ltr.thing.LabelNoCount.CompareTo(rtr.thing.LabelNoCount);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref things, "things", LookMode.Deep);
		}

		public override void Arrive()
		{
			base.Arrive();
			foreach (Thing t in things.ToList())
			{
				TradeUtility.SpawnDropPod(DropCellFinder.TradeDropSpot(Map), Map, t);
			}
			Find.LetterStack.ReceiveLetter("DMSRC_RenegadesTradeLetter_Label".Translate(), "DMSRC_RenegadesTradeLetter_Text".Translate(), LetterDefOf.PositiveEvent, things);
			Complete();
		}

		public override AcceptanceReport TrySave(GameComponent_Renegades renegades)
		{
			if (!tradeRows.Any((t) => t.countSelected > 0))
			{
				return "DMSRC_NoThingsSelected".Translate();
			}
			Map map = Map;
			float num = 0f;
			things = new List<Thing>();
			foreach (TradeRow row in tradeRows.ToList())
			{
				if (row.countSelected > 0)
				{
					num += row.thing.MarketValue * row.countSelected;
				}
			}
			int silver = Mathf.RoundToInt(num);
			if (!TradeUtility.ColonyHasEnoughSilver(map, silver))
			{
				return "NeedSilverLaunchable".Translate(silver.ToString());
			}
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
					Thing last = things.Last();
				}
			}
			TradeUtility.LaunchSilver(map, silver);
			tradeRows.Clear();
			tradeRows = null;
			return base.TrySave(renegades);
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