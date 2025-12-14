using DelaunatorSharp;
using DMSRC;
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
using System.Reflection.Emit;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
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
using static HarmonyLib.Code;

namespace EliteRaid
{
	
	public class Dialog_Renegades : Window
	{
		private GameComponent_Renegades renegades;

		private Map map;

		private RenegadesRequest request = null;

		private float viewHeight = 1000f;

		private Vector2 scrollPosition;

		public override Vector2 InitialSize => new Vector2(960f, 660f);

		public Dialog_Renegades(GameComponent_Renegades renegades, Map map)
		{
			soundAppear = SoundDefOf.TabOpen;
			soundClose = SoundDefOf.TabClose;
			this.renegades = renegades;
			this.doCloseX = true;
			this.closeOnClickedOutside = true;
			this.forcePause = true;
			this.map = map;
		}

		public override void PreOpen()
		{
			if (renegades.things.NullOrEmpty())
			{
				renegades.GenerateThings();
			}
			base.PreOpen();
		}

		public override void DoWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Medium;
			Text.Anchor = TextAnchor.MiddleCenter;
			Rect labelRect = new Rect(inRect.x, inRect.y, inRect.width, 24f);
			Widgets.Label(labelRect, "DMSRC_RenegadesDialod".Translate());
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
			Rect leftRect = new Rect(inRect.x, inRect.y + 24f, 250f, inRect.height - 24f).ContractedBy(10f);
			Rect rightRect = new Rect(inRect.x + 250f, inRect.y + 24f, inRect.width - 250f, inRect.height - 24f).ContractedBy(10f);
			Widgets.DrawLineVertical(250f, inRect.y + 10f, inRect.height - 20f);
			DoLeftRect(leftRect);
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
			DoRightRect(rightRect);
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
		}

		public void DoLeftRect(Rect rect)
		{
			if (Widgets.ButtonText(rect, "Add".Translate().CapitalizeFirst()))
			{
				renegades.playerRep = FactionRelationKind.Ally;
			}
		}

		public void DoRightRect(Rect rect)
		{
			if (renegades.playerRep != FactionRelationKind.Ally)
			{
				Text.Anchor = TextAnchor.MiddleCenter;
				GUI.color = Color.gray;
				Widgets.Label(rect, "DMSRC_RenegadesDialod_RequestsDisabled".Translate());
				return;
			}
			Widgets.BeginGroup(rect);
			if(request == null)
			{
				if (Widgets.ButtonText(new Rect(rect.width - 101f, 5f, 96f, 48f), "Add".Translate().CapitalizeFirst()))
				{
					List<FloatMenuOption> list = new List<FloatMenuOption>();
					foreach(RenegadesRequestDef def in DefDatabase<RenegadesRequestDef>.AllDefs)
					{
						list.Add(new FloatMenuOption(def.label, delegate
						{
							request = renegades.MakeRequest(def);
							request.tile = map.Tile;
						}, extraPartWidth: 29f, extraPartOnGUI: (Rect r) => Widgets.InfoCardButton(r.x + 5f, r.y + (r.height - 24f) / 2f,def)));
					}
					Find.WindowStack.Add(new FloatMenu(list));
				}
				Rect outRect = new Rect(0f, 58f, rect.width, rect.height - 58f);
				Rect viewRect = new Rect(0f, 58f, outRect.width - 16f, renegades.requests.Count * 106);
				Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
				float num = 58f;
				if (!renegades.requests.NullOrEmpty())
				{
					foreach (RenegadesRequest r in renegades.requests.ToList())
					{
						Rect rect1 = r.DoInterface(0f, num, viewRect.width);
						num += rect1.height + 6f;
					}
				}
				Widgets.EndScrollView();
			}
			else
			{
				request.DrawTab(new Rect(0f, 58f, rect.width, rect.height - 58f), ref scrollPosition, viewHeight, renegades);
				if (Widgets.ButtonText(new Rect(rect.width - 202f, 5f, 96f, 48f), "Save".Translate().CapitalizeFirst()))
				{
					AcceptanceReport report = request.TrySave(renegades, map);
					if (report.Accepted)
					{
						renegades.requests.Add(request);
						request = null;
					}
					else
					{
						Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
					}
				}
				if (Widgets.ButtonText(new Rect(rect.width - 101f, 5f, 96f, 48f), "Cancel".Translate().CapitalizeFirst()))
				{
					request = null;
				}
			}
			Widgets.EndGroup();
		}

		/*public Rect DoInterface(float x, float y, RenegadesRequest request, float width)
		{
			Rect rect = new Rect(x, y, width, 288f);
			float num = 0f;
			rect.height += num;
			Color color = Color.white;
			Widgets.DrawAltRect(rect);
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.BeginGroup(rect);
			Rect rect2 = new Rect(48f, 0f, width - 96f, 48f);
			Rect rect3 = new Rect(0f, 0f, 48f, 48f);
			Rect rect4 = new Rect(0, 0f, width, 48f);
			Rect rect5 = new Rect(width - 48f, 0f, 48, 48f);
			GUI.color = Color.white;
			if (isDefault)
			{
				Widgets.Label(rect4, "Default".Translate());
			}
			else
			{
				if(elc.faction != null)
				{
					Widgets.DrawTextureFitted(rect3, elc.faction.FactionIcon, 1);
					Widgets.Label(rect2, elc.faction.LabelCap);
				}
				if (Widgets.ButtonImage(rect5, TexButton.Delete))
				{
					options.Remove(elc);
					Text.Anchor = TextAnchor.UpperLeft;
					return rect;
				}
			}
			FloatRange f = new FloatRange(0f, 10f);
			rect4.y += 48f;
			Widgets.HorizontalSlider(rect4, ref elc.weaponMoneyFactor, f, "Стоимость оружия: x" + elc.weaponMoneyFactor.ToStringPercent(), 0.1f);
			rect4.y += 48f;
			Widgets.HorizontalSlider(rect4, ref elc.apparelMoneyFactor, f, "Стоимость снаряжения: x" + elc.apparelMoneyFactor.ToStringPercent(), 0.1f);
			rect4.y += 48f;
			Widgets.HorizontalSlider(rect4, ref elc.hediffMoneyFactor, f , "Стоимость имплантов: x" + elc.hediffMoneyFactor.ToStringPercent(), 0.1f);
			rect4.y += 48f;
			Widgets.Label(new Rect(0, rect4.y, width, 24f), "Quality".Translate());
			Widgets.Label(new Rect(0, rect4.y + 24f, width, 24f), " - ");
			if (Widgets.ButtonText(new Rect(50f, rect4.y + 24f, 100f, 24f), elc.minQuality.GetLabel()))
			{
				List<FloatMenuOption> list1 = new List<FloatMenuOption>();
				foreach (QualityCategory e1 in Enum.GetValues(typeof(QualityCategory)))
				{
					QualityCategory elocal1 = e1;
					list1.Add(new FloatMenuOption(e1.GetLabel(), delegate
					{
						elc.minQuality = elocal1;
					}));
				}
				Find.WindowStack.Add(new FloatMenu(list1));
			}
			if (Widgets.ButtonText(new Rect(width - 150f, rect4.y + 24f, 100f, 24f), elc.maxQuality.GetLabel()))
			{
				List<FloatMenuOption> list2 = new List<FloatMenuOption>();
				foreach (QualityCategory e2 in Enum.GetValues(typeof(QualityCategory)))
				{
					QualityCategory elocal2 = e2;
					list2.Add(new FloatMenuOption(e2.GetLabel(), delegate
					{
						elc.maxQuality = elocal2;
					}));
				}
				Find.WindowStack.Add(new FloatMenu(list2));
			}
			rect4.y += 48f;
			Widgets.Label(new Rect(0, rect4.y, width, 24f), "Требования");
			if (Widgets.ButtonText(new Rect(0f, rect4.y + 24f, 160, 24f), elc.requiredResearch?.LabelCap ?? "нет"))
			{
				List<DebugMenuOption> list3 = new List<DebugMenuOption>();
				list3.Add(new DebugMenuOption("нет", DebugMenuOptionMode.Action, delegate
				{
					elc.requiredResearch = null;
				}));
				foreach (ResearchProjectDef research in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
				{
					ResearchProjectDef localResearch = research;
					list3.Add(new DebugMenuOption(localResearch.LabelCap, DebugMenuOptionMode.Action, delegate
					{
						elc.requiredResearch = localResearch;
					}));
				}
				Find.WindowStack.Add(new Dialog_DebugOptionListLister(list3));
			}
			if (Widgets.ButtonText(new Rect(width - 150f, rect4.y + 24f, 150f, 24f), elc.requiredBuilding?.LabelCap ?? "нет"))
			{
				List<DebugMenuOption> list4 = new List<DebugMenuOption>();
				list4.Add(new DebugMenuOption("нет", DebugMenuOptionMode.Action, delegate
				{
					elc.requiredBuilding = null;
				}));
				foreach (ThingDef thing in DefDatabase<ThingDef>.AllDefsListForReading)
				{
					if (thing.category == ThingCategory.Building && thing.BuildableByPlayer)
					{
						ThingDef localThing = thing;
						list4.Add(new DebugMenuOption(localThing.LabelCap, DebugMenuOptionMode.Action, delegate
						{
							elc.requiredBuilding = localThing;
						}));
					}
				}
				Find.WindowStack.Add(new Dialog_DebugOptionListLister(list4));
			}
			Widgets.EndGroup();
			Text.Font = GameFont.Small;
			GUI.color = Color.white;
			Text.Anchor = TextAnchor.UpperLeft;
			return rect;
		}

		public static void DoRow(Rect rect, Tradeable trad, int index)
		{
			if (index % 2 == 1)
			{
				Widgets.DrawLightHighlight(rect);
			}
			Text.Font = GameFont.Small;
			Widgets.BeginGroup(rect);
			float width = rect.width;
			int num = trad.CountHeldBy(Transactor.Trader);
			if (num != 0 && trad.IsThing)
			{
				Rect rect2 = new Rect(width - 75f, 0f, 75f, rect.height);
				if (Mouse.IsOver(rect2))
				{
					Widgets.DrawHighlight(rect2);
				}
				Text.Anchor = TextAnchor.MiddleRight;
				Rect rect3 = rect2;
				rect3.xMin += 5f;
				rect3.xMax -= 5f;
				Widgets.Label(rect3, num.ToStringCached());
				TooltipHandler.TipRegionByKey(rect2, "TraderCount");
				Rect rect4 = new Rect(rect2.x - 100f, 0f, 100f, rect.height);
				Text.Anchor = TextAnchor.MiddleRight;
				DrawPrice(rect4, trad, TradeAction.PlayerBuys);
			}
			width -= 175f;
			Rect rect5 = new Rect(width - 240f, 0f, 240f, rect.height);
			if (!trad.TraderWillTrade)
			{
				DrawWillNotTradeText(rect5, "TraderWillNotTrade".Translate());
			}
			else if (ModsConfig.IdeologyActive && TransferableUIUtility.TradeIsPlayerSellingToSlavery(trad, TradeSession.trader.Faction) && !new HistoryEvent(HistoryEventDefOf.SoldSlave, TradeSession.playerNegotiator.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
			{
				DrawWillNotTradeText(rect5, "NegotiatorWillNotTradeSlaves".Translate(TradeSession.playerNegotiator));
				if (Mouse.IsOver(rect5))
				{
					Widgets.DrawHighlight(rect5);
					TooltipHandler.TipRegion(rect5, "NegotiatorWillNotTradeSlavesTip".Translate(TradeSession.playerNegotiator, TradeSession.playerNegotiator.Ideo.name));
				}
			}
			else
			{
				bool flash = Time.time - Dialog_Trade.lastCurrencyFlashTime < 1f && trad.IsCurrency;
				TransferableUIUtility.DoCountAdjustInterface(rect5, trad, index, trad.GetMinimumToTransfer(), trad.GetMaximumToTransfer(), flash);
			}
			width -= 240f;
			int num2 = trad.CountHeldBy(Transactor.Colony);
			if (num2 != 0 || trad.IsCurrency)
			{
				Rect rect6 = new Rect(width - 100f, 0f, 100f, rect.height);
				Text.Anchor = TextAnchor.MiddleLeft;
				DrawPrice(rect6, trad, TradeAction.PlayerSells);
				Rect rect7 = new Rect(rect6.x - 75f, 0f, 75f, rect.height);
				if (Mouse.IsOver(rect7))
				{
					Widgets.DrawHighlight(rect7);
				}
				Text.Anchor = TextAnchor.MiddleLeft;
				Rect rect8 = rect7;
				rect8.xMin += 5f;
				rect8.xMax -= 5f;
				Widgets.Label(rect8, num2.ToStringCached());
				TooltipHandler.TipRegionByKey(rect7, "ColonyCount");
			}
			width -= 175f;
			TransferableUIUtility.DoExtraIcons(trad, rect, ref width);
			if (ModsConfig.IdeologyActive)
			{
				TransferableUIUtility.DrawCaptiveTradeInfo(trad, TradeSession.trader, rect, ref width);
			}
			Rect idRect = new Rect(0f, 0f, width, rect.height);
			TransferableUIUtility.DrawTransferableInfo(trad, idRect, trad.TraderWillTrade ? Color.white : NoTradeColor);
			GenUI.ResetLabelAlign();
			Widgets.EndGroup();
		}*/
	}
}
