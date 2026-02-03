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
		public List<RenegadesRequest> requests = new List<RenegadesRequest>();

		public bool active = true;

		public int lastID = -1;

		public int playerGoodwill = 0;

		private FactionRelationKind playerRelation = FactionRelationKind.Neutral;

		public FactionRelationKind PlayerRelation
		{
			get { return playerRelation; }
			set
			{
				if(playerRelation != value)
				{
					FactionRelationKind prev = playerRelation; 
					playerRelation = value;
					ColoredText.ClearCache();
					Faction.OfPlayerSilentFail?.Notify_RelationKindChanged(RenegadesFaction, prev, false, null, GlobalTargetInfo.Invalid, out var _);
				}
			}
		}

		private Faction ofRenegades;

		private Faction ofDMS;

		public List<Thing> things = new List<Thing>();

		public bool contacted = false;

		public int hoursTillContact = -1;

		public int hoursTillRefresh = -1;

		public bool enemyWithFleet = false;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref requests, "requests", LookMode.Deep);
			Scribe_Values.Look(ref playerRelation, "playerRelation", FactionRelationKind.Neutral);
			Scribe_Values.Look(ref playerGoodwill, "playerGoodwill", 0);
			Scribe_Values.Look(ref active, "active", true);
			Scribe_Values.Look(ref lastID, "lastID", -1);
			Scribe_Values.Look(ref contacted, "contacted", true);
			Scribe_Values.Look(ref hoursTillContact, "hoursTillContact", -1);
			Scribe_Values.Look(ref hoursTillRefresh, "hoursTillRefresh", -1);
			Scribe_Values.Look(ref enemyWithFleet, "enemyWithFleet", false);
			Scribe_Collections.Look(ref things, "things", LookMode.Deep);
			ofRenegades = null;
			ofDMS = null;
		}


		public void OffsetGoodwill(int offset)
		{
			ChangeGoodwill(playerGoodwill + offset);
		}

		public void ChangeGoodwill(int newValue)
		{
			int goodwillPrev = playerGoodwill;
			playerGoodwill = newValue;
			if (goodwillPrev < 50 && playerGoodwill >= 50)
			{
				PlayerRelation = FactionRelationKind.Ally;
			}
			else if (goodwillPrev > 0 && playerGoodwill <= 0)
			{
				PlayerRelation = FactionRelationKind.Neutral;
			}
			else if (goodwillPrev > -50 && playerGoodwill <= -50)
			{
				PlayerRelation = FactionRelationKind.Hostile;
			}
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
			return new FactionRelation(faction, playerRelation) { baseGoodwill = playerGoodwill };
		}

		public GameComponent_Renegades(Game game)
		{
			
		}

		public override void GameComponentTick()
		{
			if(Verse.Find.TickManager.TicksGame % 2500 != 0)
			{
				return;
			}
			if(playerRelation != FactionRelationKind.Hostile)
			{
				if (!contacted)
				{
					hoursTillContact--;
					if (hoursTillContact <= 0)
					{
						ContactPlayer();
					}
				}
				if (Faction.OfPlayerSilentFail?.RelationKindWith(DMSFaction) == FactionRelationKind.Ally)
				{
					PlayerRelation = FactionRelationKind.Hostile;
					playerGoodwill = -200;
				}
				if(hoursTillRefresh > 0)
				{
					hoursTillRefresh--;
					if(hoursTillRefresh <= 0)
					{
						GenerateThings();
					}
				}
			}
			for(int i = 0; i < requests.Count; i++)
			{
				requests[i].Tick();
			}
		}

		public override void StartedNewGame()
		{
			base.StartedNewGame();
			if (hoursTillContact == -1 && !contacted)
			{
				hoursTillContact = new IntRange(20, 40).RandomInRange * 24;
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
				Log.Message("DMSRC Renegades clan is null");
			}
			else if (Faction.OfPlayerSilentFail?.RelationKindWith(DMSFaction) == FactionRelationKind.Ally)
			{
				PlayerRelation = FactionRelationKind.Hostile;
				playerGoodwill = -200;
			}
		}

		public void ContactPlayer()
		{
			Map map = Verse.Find.CurrentMap;
			if(map == null || !map.IsPlayerHome)
			{
				map = Verse.Find.AnyPlayerHomeMap;
			}
			ChoiceLetter choiceLetter = (ChoiceLetter)LetterMaker.MakeLetter("DMSRC_RenegadesContactsLetter_Label".Translate(), "DMSRC_RenegadesContactsLetter_Text".Translate(), RCDefOf.DMSRC_ContactEvent);
			choiceLetter.StartTimeout(180000);
			Verse.Find.LetterStack.ReceiveLetter(choiceLetter);
			contacted = true;
		}

		public float RaidCommonality(float points)
        {
			if(playerRelation == FactionRelationKind.Hostile)
			{
				return 1f;
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
			foreach (Thing t in things)
			{
				t.Destroy();
			}
			things.Clear();
			ThingSetMakerParams parms = default(ThingSetMakerParams);
			parms.traderDef = def;
			parms.makingFaction = RenegadesFaction;
			List<Thing> list = ThingSetMakerDefOf.TraderStock.root.Generate(parms);
			foreach (Thing item in list.ToList())
			{
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