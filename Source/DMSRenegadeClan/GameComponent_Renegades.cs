using DelaunatorSharp;
using DMS;
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
using static DMSRC.TradeRequest;
using static RimWorld.FleshTypeDef;
using static RimWorld.PsychicRitualRoleDef;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;

namespace DMSRC
{
	public class ContainerSitePartParams : SitePartParams, IExposable
	{
		public RenegadesRequest request;

		public new void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref request, "request");
		}
	}

	public class GameComponent_Renegades : GameComponent
	{
		private readonly IntRange TicksContactInteral = new IntRange(100, 200);

		private readonly IntRange TicksContactInteralHostile = new IntRange(600, 1200);

		private readonly IntRange TicksContactInteralInintial = new IntRange(400, 800);

		public List<RenegadesRequest> requests = new List<RenegadesRequest>();

		public bool active = true;

		public int lastID = -1;

		public int playerGoodwill = 0;

		private FactionRelationKind playerRelation = FactionRelationKind.Neutral;

		public FactionRelationKind PlayerRelation
		{
			get
			{
				return playerRelation;
			}
			set
			{
				if(playerRelation != value)
				{
					FactionRelationKind prev = playerRelation;
					playerRelation = value;
					if(playerRelation == FactionRelationKind.Hostile)
					{
						requests.Clear();
					}
					ColoredText.ClearCache();
					Faction.OfPlayerSilentFail?.Notify_RelationKindChanged(RenegadesFaction, prev, false, null, GlobalTargetInfo.Invalid, out var _);
				}
			}
		}

		private Faction ofRenegades;

		private Faction ofDMS;

		public List<Thing> things = new List<Thing>();

		public bool contact = false;

		public int ticksTillContact = -1;

		public int ticksTillRefresh = -1;

		public bool enemyWithFleet = false;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref requests, "requests", LookMode.Deep);
			Scribe_Values.Look(ref playerRelation, "playerRelation", FactionRelationKind.Neutral);
			Scribe_Values.Look(ref playerGoodwill, "playerGoodwill", 0);
			Scribe_Values.Look(ref active, "active", true);
			Scribe_Values.Look(ref lastID, "lastID", -1);
			Scribe_Values.Look(ref contact, "contact", true);
			Scribe_Values.Look(ref ticksTillContact, "ticksTillContact", -1);
			Scribe_Values.Look(ref ticksTillRefresh, "ticksTillRefresh", -1);
			Scribe_Values.Look(ref enemyWithFleet, "enemyWithFleet", false);
			Scribe_Collections.Look(ref things, "things", LookMode.Deep);
			ofRenegades = null;
			ofDMS = null;
			if(Scribe.mode == LoadSaveMode.PostLoadInit && !things.NullOrEmpty())
			{
				things.RemoveAll(x => x == null || x.def == null);
			}
		}

		public void OffsetGoodwill(int offset, bool notifyPlayer = false)
		{
			ChangeGoodwill(playerGoodwill + offset, notifyPlayer);
		}

		public void ChangeGoodwill(int newValue, bool notifyPlayer = false)
		{
			int goodwillPrev = playerGoodwill;
			playerGoodwill = newValue;
			if (goodwillPrev < 50 && playerGoodwill >= 50)
			{
				bool flag = notifyPlayer && PlayerRelation != FactionRelationKind.Ally;
				PlayerRelation = FactionRelationKind.Ally;
				if (flag)
				{
					Verse.Find.LetterStack.ReceiveLetter("LetterLabelRelationsChange_Ally".Translate(RenegadesFaction.Name), RelationChangeLetter(FactionRelationKind.Ally), LetterDefOf.PositiveEvent, null, RenegadesFaction);
				}
			}
			else if (goodwillPrev > 0 && playerGoodwill <= 0)
			{
				bool flag = notifyPlayer && PlayerRelation != FactionRelationKind.Neutral;
				PlayerRelation = FactionRelationKind.Neutral;
				if (flag)
				{
					Verse.Find.LetterStack.ReceiveLetter("LetterLabelRelationsChange_NeutralFromAlly".Translate(RenegadesFaction.Name), RelationChangeLetter(FactionRelationKind.Neutral), LetterDefOf.NegativeEvent, null, RenegadesFaction);
				}
			}
			else if (goodwillPrev > -50 && playerGoodwill <= -50)
			{
				bool flag = notifyPlayer && PlayerRelation != FactionRelationKind.Hostile;
				PlayerRelation = FactionRelationKind.Hostile;
				if (flag)
				{
					Verse.Find.LetterStack.ReceiveLetter("LetterLabelRelationsChange_Hostile".Translate(RenegadesFaction.Name), RelationChangeLetter(FactionRelationKind.Hostile), LetterDefOf.NegativeEvent, null, RenegadesFaction);
				}
			}

		}
		public TaggedString RelationChangeLetter(FactionRelationKind newKind)
		{
			TaggedString text = "";
			switch (newKind)
			{
				case FactionRelationKind.Hostile:
					text += "LetterRelationsChange_Hostile".Translate(RenegadesFaction.NameColored);
					text += "\n\n" + "LetterRelationsChange_HostileGoodwillDescription_NoGifting".Translate(playerGoodwill.ToStringWithSign(), (-50).ToStringWithSign(), 0.ToStringWithSign());
					break;
				case FactionRelationKind.Ally:
					text += "LetterRelationsChange_Ally".Translate(RenegadesFaction.NameColored);
					text += "\n\n" + "LetterRelationsChange_AllyGoodwillDescription".Translate(playerGoodwill.ToStringWithSign(), 50.ToStringWithSign(), 0.ToStringWithSign());
					break;
				case FactionRelationKind.Neutral:
					text += "LetterRelationsChange_NeutralFromAlly".Translate(RenegadesFaction.NameColored);
					text += "\n\n" + "LetterRelationsChange_NeutralFromAllyGoodwillDescription".Translate(RenegadesFaction.NameColored, playerGoodwill.ToStringWithSign(), 0.ToStringWithSign(), (-50).ToStringWithSign(), 50.ToStringWithSign());
					break;
			}
			return text;
		}

		public RenegadesRequest MakeRequest(RenegadesRequestDef def)
		{
			RenegadesRequest obj = (RenegadesRequest)Activator.CreateInstance(def.requestClass);
			lastID++;
			obj.ID = lastID;
			obj.def = def;
			return obj;
		}

		public static GameComponent_Renegades Find => Current.Game.GetComponent<GameComponent_Renegades>();

		public Faction RenegadesFaction
        {
            get
            {
				if(ofRenegades == null)
				{
					ofRenegades = Verse.Find.FactionManager.FirstFactionOfDef(RCDefOf.DMSRC_RenegadeClan);
					if(ofRenegades == null)
					{
						FactionGenerator.CreateFactionAndAddToManager(RCDefOf.DMSRC_RenegadeClan);
						ofRenegades = Verse.Find.FactionManager.FirstFactionOfDef(RCDefOf.DMSRC_RenegadeClan);
						if (ModsConfig.IdeologyActive && ofRenegades?.ideos?.PrimaryIdeo != null)
						{
							if (ofRenegades.ideos.PrimaryIdeo.PreferredXenotypes.NullOrEmpty())
							{
								Precept_Xenotype precept_Xenotype = (Precept_Xenotype)PreceptMaker.MakePrecept(PreceptDefOf.PreferredXenotype);
								precept_Xenotype.xenotype = XenotypeDefOf.Baseliner;
								ofRenegades.ideos.PrimaryIdeo.AddPrecept(precept_Xenotype);
							}
						}
					}
				}
				return ofRenegades;
			}
        }

		public Faction DMSFaction
		{
			get
			{
				if (ofDMS == null)
				{
					ofDMS = Verse.Find.FactionManager.FirstFactionOfDef(RCDefOf.DMS_Army);
				}
				return ofDMS;
			}
		}

		public FactionRelation RelationWithPlayer(Faction faction = null)
		{
			return new FactionRelation(faction, PlayerRelation) { baseGoodwill = playerGoodwill };
		}

		public GameComponent_Renegades(Game game)
		{
			
		}

		public void ForbiddenTechMessage()
		{
			if (DMSFaction?.defeated != false || DMSFaction.HostileTo(Faction.OfPlayerSilentFail))
			{
				return;
			}
			Messages.Message("DMSRC_ForbiddenTech".Translate(), MessageTypeDefOf.NegativeEvent);
		}

		public void UsedForbiddenTech()
		{
			if (DMSFaction?.defeated != false || DMSFaction.HostileTo(Faction.OfPlayerSilentFail))
			{
				return;
			}
			Faction.OfPlayerSilentFail?.TryAffectGoodwillWith(DMSFaction, -50, canSendMessage: true, canSendHostilityLetter: true, RCDefOf.DMSRC_UsedForbiddenTech);
		}

		public override void GameComponentTick()
		{
			if (Verse.Find.TickManager.TicksGame % 6000 != 0)
			{
				return;
			}
			FactionRelationKind kind = DMSFaction?.RelationKindWith(Faction.OfPlayerSilentFail) ?? FactionRelationKind.Neutral;
			if (kind == FactionRelationKind.Ally)
			{
				if(playerRelation != FactionRelationKind.Hostile)
				{
					PlayerRelation = FactionRelationKind.Hostile;
					playerGoodwill = -200;
					contact = false;
					ticksTillContact = TicksContactInteralHostile.RandomInRange;
				}
				return;
			}
			if (contact)
			{
				ticksTillRefresh--;
				if (ticksTillRefresh < 0)
				{
					ticksTillRefresh = TicksContactInteral.RandomInRange;
					GenerateThings();
				}
				foreach (RenegadesRequest req in requests.ToList())
				{
					req.Tick();
				}
				if (PlayerRelation == FactionRelationKind.Ally)
				{
					DMSFaction?.TryAffectGoodwillWith(Faction.OfPlayerSilentFail, -200, canSendMessage: false, canSendHostilityLetter: false, RCDefOf.DMSRC_AllyWithRenegades);
				}
			}
			else
			{
				ticksTillContact--;
				if (kind == FactionRelationKind.Hostile)
				{
					ticksTillContact -= 2;
				}
				if (ticksTillContact <= 0)
				{
					if (kind == FactionRelationKind.Ally)
					{
						contact = false;
						ticksTillContact = TicksContactInteralHostile.RandomInRange;
						return;
					}
					if (!ResearchProjectDefOf.MicroelectronicsBasics.IsFinished)
					{
						contact = false;
						ticksTillContact = TicksContactInteral.RandomInRange;
						return;
					}
					ContactPlayer();
				}
			}
		}

		public override void StartedNewGame()
		{
			base.StartedNewGame();
			if (ticksTillContact < 0 && !contact)
			{
				ticksTillContact = TicksContactInteralInintial.RandomInRange;
			}
			if (ModsConfig.IdeologyActive && RenegadesFaction?.ideos?.PrimaryIdeo != null)
			{
				if (RenegadesFaction.ideos.PrimaryIdeo.PreferredXenotypes.NullOrEmpty())
				{
					Precept_Xenotype precept_Xenotype = (Precept_Xenotype)PreceptMaker.MakePrecept(PreceptDefOf.PreferredXenotype);
					precept_Xenotype.xenotype = XenotypeDefOf.Baseliner;
					RenegadesFaction.ideos.PrimaryIdeo.AddPrecept(precept_Xenotype);
				}
			}
			TestOnStartUp();
		}

		public override void LoadedGame()
		{
			base.LoadedGame();
			TestOnStartUp();
		}

		public void TestOnStartUp()
		{
			Faction faction = RenegadesFaction;
			if (faction == null)
			{
				Log.Warning("DMSRC Renegades clan faction was null on startup, that is unacceptable!!! Fixing...");
			}
			else if (DMSFaction != null && Faction.OfPlayerSilentFail?.RelationKindWith(DMSFaction) == FactionRelationKind.Ally)
			{
				PlayerRelation = FactionRelationKind.Hostile;
				playerGoodwill = -200;
			}
		}

		public void ContactPlayer()
		{
			if(PlayerRelation == FactionRelationKind.Ally)
			{
				contact = true;
				return;
			}
			Map map = Verse.Find.CurrentMap;
			if(map == null || !map.IsPlayerHome)
			{
				map = Verse.Find.AnyPlayerHomeMap;
			}
			ChoiceLetter choiceLetter = (ChoiceLetter)LetterMaker.MakeLetter("DMSRC_RenegadesContactsLetter_Label".Translate(), "DMSRC_RenegadesContactsLetter_Text".Translate(), RCDefOf.DMSRC_ContactEvent);
			choiceLetter.StartTimeout(180000);
			Verse.Find.LetterStack.ReceiveLetter(choiceLetter);
			contact = true;
		}

		public float RaidCommonality(float points)
        {
			if(PlayerRelation == FactionRelationKind.Hostile)
			{
				return 0.5f;
			}
			return 0f;
        }

		private TraderKindDef def;

		public void GenerateThings()
		{
			if (def == null)
			{
				def = DefDatabase<TraderKindDef>.AllDefs.FirstOrDefault((TraderKindDef x) => x.category == "DMSRC_RenegadesMarket");
			}
			if (def == null)
			{
				string s = "DMSRC Cannot find trader def for renegade marker. Defs available:";
				foreach(TraderKindDef x in DefDatabase<TraderKindDef>.AllDefs)
				{
					s += "\n" + x.defName + " - " + x.category;
				}
				Log.Error(s);
				return;
			}
			if (things.NullOrEmpty())
			{
				things = new List<Thing>();
			}
			else
			{
				foreach (Thing t in things.ToList())
				{
					t?.Destroy();//For some reason things could be null after loading game
				}
				things.Clear();
			}
			ThingSetMakerParams parms = default(ThingSetMakerParams);
			parms.traderDef = def;
			parms.makingFaction = RenegadesFaction;
			if (ThingSetMakerDefOf.TraderStock == null)
			{
				Log.Error("DMSRC Cannot generate market stock: ThingSetMakerDefOf.TraderStock is null for some reason.");
				return;
			}
			if (ThingSetMakerDefOf.TraderStock.root == null)
			{
				Log.Error("DMSRC Cannot generate market stock: ThingSetMakerDefOf.TraderStock.root is null for some reason.");
				return;
			}
			List<Thing> list = ThingSetMakerDefOf.TraderStock.root.Generate(parms);
			if (list == null)
			{
				Log.Error("DMSRC Cannot generate market stock: ThinkSetMaker returned null.");
				return;
			}
			if (list.NullOrEmpty())
			{
				Log.Error("DMSRC Cannot generate market stock: ThinkSetMaker returned empty list.");
				return;
			}
			foreach (Thing item in list.ToList())
			{
				if(item == null || item.stackCount < 1)
				{
					continue;
				}
				if (item.def.stackLimit <= 1)
				{
					things.Add(item);
				}
				else
				{
					Thing t = things.FirstOrDefault((x) => x.CanStackWith(item));
					if (t == null)
					{
						things.Add(item);
					}
					else
					{
						t.TryAbsorbStack(item, false);
					}
				}
			}
		}
	}
}