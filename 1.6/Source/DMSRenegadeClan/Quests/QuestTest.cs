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
using UnityEngine.Assertions;
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
using static RimWorld.QuestPart;
using static System.Net.Mime.MediaTypeNames;

namespace DMSRC
{
	public class QuestNode_Root_Site : QuestNode
	{
		public SimpleCurve rewardValueCurve = new SimpleCurve
		{
			new CurvePoint(200f, 550f),
			new CurvePoint(400f, 1100f),
			new CurvePoint(800f, 1600f),
			new CurvePoint(1600f, 2600f),
			new CurvePoint(3200f, 3600f),
			new CurvePoint(30000f, 10000f)
		};

		public SiteMissionProps mission;

		public int timeoutTicks = 60000;

		public enum GiverType
		{
			Undefined,
			Renegades,
			ColonialFleet,
			Any
		}

		public GiverType giver = GiverType.Undefined;

		public int rewardValue = 1;

		public float minPoints = 400f;

		public SitePartDef sitePart;

		public bool oppositeFactionForSite = true;

		public Faction SiteFaction(Faction giver)
		{
			if (oppositeFactionForSite)
			{
				if (giver.def == RCDefOf.DMSRC_RenegadeClan)
				{
					return GameComponent_Renegades.Find.DMSFaction;
				}
				else return GameComponent_Renegades.Find.RenegadesFaction;
			}
			return giver;
		}

		protected override void RunInt()
		{
			Quest quest = QuestGen.quest;
			Slate slate = QuestGen.slate;
			//QuestGenUtility.RunAdjustPointsForDistantFight();
			float points = slate.Get("points", 0f);
			if (points < minPoints)
			{
				points = minPoints;
			}
			bool fromRenegades = (giver == GiverType.Renegades) || (giver == GiverType.Any && Rand.Bool);
			int rep = rewardValue * 10;
			GameComponent_Renegades comp = GameComponent_Renegades.Find;
			Map map = Find.Maps.Where((Map m) => m.IsPlayerHome).RandomElement();
			slate.Set("map", map);
			Faction faction = fromRenegades ? comp.RenegadesFaction : comp.DMSFaction;
			slate.Set("faction", faction); slate.Set("askerIsNull", true);
			slate.Set<Pawn>("asker", null);
			Site site = null;
			if (TileFinder.TryFindNewSiteTile(out var tile, 0, validator: (x) => x.Tile.hilliness == Hilliness.Flat))
			{
				site = SiteMaker.MakeSite(sitePart, tile, faction, false);
				site.SetFaction(SiteFaction(faction));
			}
			else
			{
				return;
			}
			Log.Message("1");
			Log.Message(site == null ? "null" : site.ToString());
			Log.Message(faction == null ? "null" : faction.ToString());
			Log.Message(site?.Faction == null ? "null" : site.Faction.ToString());
			quest.SpawnWorldObject(site);
			slate.Set("site", site);
			quest.ReserveFaction(site.Faction);
			string inSignalFail = QuestGenUtility.HardcodedSignalWithQuestID("DMSRC_FailMission");
			string inSignalSuccess = QuestGenUtility.HardcodedSignalWithQuestID("DMSRC_SuccessMission");
			string inSignalEnable = QuestGenUtility.HardcodedSignalWithQuestID("site.MapGenerated");
			string inSignalRemoved = QuestGenUtility.HardcodedSignalWithQuestID("site.MapRemoved");
			Log.Message("2");
			quest.WorldObjectTimeout(site, timeoutTicks);
			quest.Delay(timeoutTicks, delegate
			{
				QuestGen_End.End(quest, QuestEndOutcome.Fail);
			}, inSignalDisable: inSignalEnable);
			Log.Message("3");
			QuestPart_Mission questPart_Mission = (QuestPart_Mission)Activator.CreateInstance(mission.missionClass);
			questPart_Mission.inSignalEnable = inSignalEnable;
			questPart_Mission.inSignalFail = inSignalFail;
			questPart_Mission.site = site;
			questPart_Mission.inSignalSuccess = inSignalSuccess;
			questPart_Mission.mission = mission;
			Log.Message("4");
			QuestGenUtility.RunInner(delegate
			{
				Quest quest2 = quest;
				RewardsGeneratorParams parms = new RewardsGeneratorParams
				{
					rewardValue = rewardValueCurve.Evaluate(points),
					thingRewardItemsOnly = true
				};
				quest2.GiveRewards(parms, null, null, null, null, null, null, null, null, false);
				if (quest2.PartsListForReading.FirstOrDefault((QuestPart x) => x is QuestPart_Choice) is QuestPart_Choice questPart_Choice)
				{
					foreach (var choice in questPart_Choice.choices)
					{
						if (choice.rewards.FirstOrDefault((y) => y is Reward_Goodwill) is Reward_Goodwill reward_Goodwill)
						{
							if (fromRenegades)
							{
								choice.rewards.Remove(reward_Goodwill);
								choice.rewards.Insert(0, (Reward)(new Reward_RenegadesRep() { amount = rep * 0.01f }));
							}
							else reward_Goodwill.amount += rep;
						}
						else choice.rewards.Insert(0, fromRenegades ? (Reward)(new Reward_RenegadesRep() { amount = rep * 0.01f }) : (Reward)(new Reward_Goodwill() { amount = rep, faction = comp.DMSFaction }));
					}
				}
				quest.End(QuestEndOutcome.Success, 0, null, null, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
			}, inSignalSuccess);
			Log.Message("5");
			questPart_Mission.signalListenMode = QuestPart.SignalListenMode.OngoingOnly;
			quest.AddPart(questPart_Mission);
			quest.End(QuestEndOutcome.Fail, sendStandardLetter: true, inSignal: inSignalRemoved);
			Log.Message("6");
			QuestGen.AddQuestDescriptionRules(new List<Rule>
			{
				new Rule_String("siteLabel", site.Label)
			});
		}

		protected override bool TestRunInt(Slate slate)
		{
			if (!Find.Storyteller.difficulty.allowViolentQuests)
			{
				return false;
			}
			QuestGenUtility.TestRunAdjustPointsForDistantFight(slate);
			if (slate.Get("points", 0f) < 40f)
			{
				return false;
			}
			foreach (Map map in Find.Maps)
			{
				if (map.IsPlayerHome)
				{
					return true;
				}
			}
			return false;
		}
	}

}