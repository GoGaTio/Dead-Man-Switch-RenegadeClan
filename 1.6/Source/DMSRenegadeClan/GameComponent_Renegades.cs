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

		public float playerOpinion = 0;

		public FactionRelationKind playerRep = FactionRelationKind.Neutral;

		private Faction ofRenegades;

		private Faction ofDMS;

		public List<Thing> things = new List<Thing>();

		public RenegadesRequest MakeRequest(RenegadesRequestDef def)
		{
			RenegadesRequest obj = (RenegadesRequest)Activator.CreateInstance(def.requestClass);
			lastID++;
			obj.ID = lastID;
			obj.def = def;
			return obj;
		}

		public static GameComponent_Renegades Find => Current.Game.GetComponent<GameComponent_Renegades>();

		/*public float Interest
        {
            get
            {
				Faction.OfPlayer.GoodwillWith(DMS.QuestKindDefOf.DMS_Officer_Ceremonist.defaultFactionType)
            }
        }*/

		public Faction RenegadesFaction
        {
            get
            {
				if(ofRenegades == null)
				{
					ofRenegades = Verse.Find.FactionManager.FirstFactionOfDef(RCDefOf.DMSRC_RenegadeClan);
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
					FactionDef def = DefDatabase<FactionDef>.GetNamed("DMS_Army");
					if(def != null)
					{
						ofDMS = Verse.Find.FactionManager.FirstFactionOfDef(def);
					}
					else
					{
						Log.Error("For some reason DMS_Army FactionDef is null");
					}
				}
				return ofDMS;
			}
		}

		public FactionRelation RelationWithPlayer(Faction faction = null)
		{
			return new FactionRelation(faction, playerRep) { baseGoodwill = playerRep == FactionRelationKind.Neutral ? 0 : (playerRep == FactionRelationKind.Hostile ? -75 : 75) };
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
			for(int i = 0; i < requests.Count; i++)
			{
				requests[i].Tick();
			}
		}

		public float RaidCommonality(float points)
        {
			if(playerRep != FactionRelationKind.Hostile)
			{
				return 0f;
			}
			return 1f;
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
			things = ThingSetMakerDefOf.TraderStock.root.Generate(parms);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref requests, "requests", LookMode.Deep);
			Scribe_Values.Look(ref active, "active", true);
			Scribe_Collections.Look(ref things, "things", LookMode.Deep);
			ofRenegades = null;
			ofDMS = null;
		}
	}
}