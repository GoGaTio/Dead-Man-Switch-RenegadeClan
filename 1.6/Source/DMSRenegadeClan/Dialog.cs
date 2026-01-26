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
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;

namespace DMSRC
{
	public class Dialog_Renegades : Window
	{
		private GameComponent_Renegades renegades;

		private Map map;

		private RenegadesRequest request = null;

		private float viewHeight = 1000f;

		private Vector2 scrollPosition;

		private Faction faction;

		protected override float Margin => 0f;

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
			if (faction == null)
			{
				faction = renegades.RenegadesFaction;
			}
			Text.Font = GameFont.Medium;
			Text.Anchor = TextAnchor.MiddleCenter;
			Rect labelRect = new Rect(inRect.x, inRect.y, inRect.width, 24f);
			Widgets.Label(labelRect, "DMSRC_RenegadesDialod".Translate());
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
			Rect leftRect = new Rect(inRect.x, inRect.y + 24f, 240f, inRect.height - 24f).ContractedBy(10f);
			Rect rightRect = new Rect(inRect.x + 240f, inRect.y + 24f, inRect.width - 250f, inRect.height - 24f).ContractedBy(10f);
			Widgets.DrawLineVertical(250f, inRect.y + 24f, inRect.height - 24f);
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
			Widgets.BeginGroup(rect);
			Text.Anchor = TextAnchor.MiddleCenter;
			Rect rect1 = new Rect(60f, 10f, 120f, 120f);
			Rect rect2 = new Rect(20f, 130f, 200f, 30f);
			Rect rect3 = new Rect(20f, 10f, 200f, 150f);
			DrawIcon(rect1);
			Widgets.Label(rect2, faction.NameColored);
			if (Mouse.IsOver(rect3))
			{
				Widgets.DrawHighlight(rect3);
				TooltipHandler.TipRegion(rect3, faction.NameColored + "\n\n" + faction.def.Description);
			}
			if (DebugSettings.ShowDevGizmos && Widgets.ButtonText(new Rect(0, 0, 48f, 24f), "DEV"))
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				list.Add(new FloatMenuOption("Make Ally", delegate
				{
					renegades.PlayerRelation = FactionRelationKind.Ally;
				}));
				list.Add(new FloatMenuOption("Make Neutral", delegate
				{
					renegades.PlayerRelation = FactionRelationKind.Neutral;
				}));
				list.Add(new FloatMenuOption("Make Hostile", delegate
				{
					renegades.PlayerRelation = FactionRelationKind.Hostile;
				}));
				list.Add(new FloatMenuOption("Reset stock", delegate
				{
					renegades.GenerateThings();
				}));
				Find.WindowStack.Add(new FloatMenu(list));
				
			}
			Widgets.EndGroup();
		}

		private void DrawIcon(Rect rect)
		{
			Color color = GUI.color;
			GUI.color = faction.Color;
			Widgets.DrawTextureFitted(rect, faction.def.FactionIcon, 1f, 1f);
			GUI.color = color;
		}

		public void DoRightRect(Rect rect)
		{
			if (renegades.PlayerRelation != FactionRelationKind.Ally)
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
							if(map.IsPocketMap || map.generatorDef.isUnderground)
							{
								request.tile = Find.RandomPlayerHomeMap.Tile;
							}
							else
							{
								request.tile = map.Tile;
							}
							request.Maps = null;
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
						float f = 0;
						Rect rect1 = r.DoInterface(10f, num, viewRect.width, ref f);
						num += rect1.height + 6f;
					}
				}
				Widgets.EndScrollView();
			}
			else
			{
				request.DrawTab(new Rect(0f, 58f, rect.width, rect.height - 58f), ref scrollPosition, viewHeight, renegades);
				
				if (Widgets.ButtonText(new Rect(rect.width - 303f, 5f, 96f, 48f), request.Map.Parent.LabelCap))
				{
					List<FloatMenuOption> list = new List<FloatMenuOption>();
					foreach (Map map in request.Maps)
					{
						list.Add(new FloatMenuOption(map.Parent.LabelCap, delegate
						{
							request.tile = map.Tile;
						}));
					}
					Find.WindowStack.Add(new FloatMenu(list));
				}
				if (Widgets.ButtonText(new Rect(rect.width - 202f, 5f, 96f, 48f), "Save".Translate().CapitalizeFirst()))
				{
					AcceptanceReport report = request.TrySave(renegades);
					if (report.Accepted)
					{
						renegades.requests.Add(request);
						request.Saved();
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

		
	}
}
