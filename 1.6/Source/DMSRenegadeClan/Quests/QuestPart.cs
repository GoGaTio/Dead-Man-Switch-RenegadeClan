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
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using static System.Collections.Specialized.BitVector32;
using static UnityEngine.GraphicsBuffer;

namespace DMSRC
{
	public class SiteMissionProps : IExposable
	{
		public Type missionClass;

		public ThingDef targetDef;

		public PawnKindDef targetKind;

		public IntRange wavesCooldown;

		public virtual void ExposeData()
		{
			Scribe_Defs.Look(ref targetDef, "targetDef");
			Scribe_Defs.Look(ref targetKind, "targetKind");
			Scribe_Values.Look(ref wavesCooldown, "wavesCooldown");
		}
	}
	public class QuestPart_Mission : QuestPartActivable
	{
		public Site site;

		public SiteMissionProps mission;

		public string inSignalFail;

		public string inSignalSuccess;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref site, "site");
			Scribe_Deep.Look(ref mission, "mission");
			Scribe_Values.Look(ref inSignalFail, "inSignalFail");
			Scribe_Values.Look(ref inSignalSuccess, "inSignalSuccess");
		}

		public virtual void Fail()
		{
			if (!inSignalFail.NullOrEmpty())
			{
				Find.SignalManager.SendSignal(new Signal(inSignalFail, true));
			}
		}

		protected override void Complete(SignalArgs signalArgs)
		{
			base.Complete(signalArgs);
			Find.SignalManager.SendSignal(new Signal(inSignalSuccess, signalArgs));
		}
	}

	public class QuestPart_Mission_Destroy : QuestPart_Mission
	{
		public List<Thing> targets;

		protected override void Enable(SignalArgs receivedArgs)
		{
			base.Enable(receivedArgs);
			targets = site.Map.listerThings.ThingsOfDef(mission.targetDef);
		}
		public override IEnumerable<GlobalTargetInfo> QuestLookTargets
		{
			get
			{
				foreach (GlobalTargetInfo questLookTarget in base.QuestLookTargets)
				{
					yield return questLookTarget;
				}
				if (targets.NullOrEmpty())
				{
					yield break;
				}
				for (int i = 0; i < targets.Count; i++)
				{
					yield return targets[i];
				}
			}
		}

		public override void QuestPartTick()
		{
			base.QuestPartTick();
			if (Find.TickManager.TicksGame % 60 != 0 || targets == null)
			{
				return;
			}
			foreach (Thing thing in targets.ToList())
			{
				if (thing == null || thing.Destroyed || thing.MapHeld != site.Map)
				{
					targets.Remove(thing);
					if (thing != null && targets.Count > 0)
					{
						Messages.Message("DMSRC_Mission_Destroyed".Translate(mission.targetDef.LabelCap, targets.Count), MessageTypeDefOf.TaskCompletion);
					}
				}
			}
			if (targets.Count <= 0)
			{
				Complete();
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref targets, "targets", LookMode.Reference);
		}
	}

	public class QuestPart_Mission_Defend : QuestPart_Mission
	{
		public List<Thing> targets;

		public int wavesLeft;

		public int ticksTillNextWaves;

		public override AlertReport AlertReport => new AlertReport() { active = true, culpritsThings = targets };

		public override bool AlertCritical => ticksTillNextWaves < Mathf.Max(2500f, mission.wavesCooldown.min / 2f);

		public override string AlertExplanation => "DMSRC_Mission_DefendDesc".Translate(ticksTillNextWaves.ToStringTicksToPeriod(), mission.targetDef.label);

		public override string AlertLabel => "DMSRC_Mission_DefendLabel".Translate(ticksTillNextWaves.ToStringTicksToPeriod(), mission.targetDef.label);

		protected override void Enable(SignalArgs receivedArgs)
		{
			base.Enable(receivedArgs);
			targets = site.Map.listerThings.ThingsOfDef(mission.targetDef);
		}
		public override IEnumerable<GlobalTargetInfo> QuestLookTargets
		{
			get
			{
				foreach (GlobalTargetInfo questLookTarget in base.QuestLookTargets)
				{
					yield return questLookTarget;
				}
				if (targets.NullOrEmpty())
				{
					yield break;
				}
				for (int i = 0; i < targets.Count; i++)
				{
					yield return targets[i];
				}
			}
		}

		public void SendWave()
		{
			Map map = site.Map;
			ticksTillNextWaves = mission.wavesCooldown.RandomInRange;
			Faction faction = (site.Faction?.def == RCDefOf.DMSRC_RenegadeClan) ? GameComponent_Renegades.Find.DMSFaction : GameComponent_Renegades.Find.RenegadesFaction;
			List<Pawn> list = new List<Pawn>();
			foreach (Pawn item in GeneratePawns(faction))
			{
				if (CellFinder.TryFindRandomEdgeCellWith((IntVec3 x) => x.Standable(map) && !x.Fogged(map) && map.reachability.CanReachColony(x), map, CellFinder.EdgeRoadChance_Ignore, out var result))
				{
					GenSpawn.Spawn(item, result, map);
				}
				list.Add(item);
			}
			if (!list.Any())
			{
				return;
			}
			LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction), map, list);
			Find.LetterStack.ReceiveLetter("DMSRC_Mission_DefendRaidLabel".Translate(faction.Name), "DMSRC_Mission_DefendRaidDesc".Translate(faction.Name, mission.targetDef.LabelCap), LetterDefOf.ThreatBig, list);
		}

		private IEnumerable<Pawn> GeneratePawns(Faction faction)
		{
			return PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
			{
				groupKind = PawnGroupKindDefOf.Combat,
				tile = site.Map.Tile,
				faction = faction,
				points = Mathf.Max(site.ActualThreatPoints, faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat))
			});
		}

		public override void QuestPartTick()
		{
			base.QuestPartTick();
			ticksTillNextWaves--;
			if(ticksTillNextWaves <= 0)
			{
				SendWave();
			}
			if (Find.TickManager.TicksGame % 60 != 0 || targets == null)
			{
				return;
			}
			foreach (Thing thing in targets.ToList())
			{
				if (thing == null || thing.Destroyed || thing.MapHeld != site.Map)
				{
					targets.Remove(thing);
					if (thing != null)
					{
						if(targets.Count > 0)
						{
							Messages.Message("DMSRC_Mission_Destroyed".Translate(mission.targetDef.LabelCap, targets.Count), MessageTypeDefOf.ThreatBig);
						}
						else
						{
							Fail();
						}
					}
				}
			}
			if (targets.Count <= 0)
			{
				Complete();
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref targets, "targets", LookMode.Reference);
			Scribe_Values.Look(ref wavesLeft, "wavesLeft");
			Scribe_Values.Look(ref ticksTillNextWaves, "ticksTillNextWaves");
		}
	}

	public class QuestPart_Mission_Kill : QuestPart_Mission
	{
		public List<Pawn> targets;

		protected override void Enable(SignalArgs receivedArgs)
		{
			base.Enable(receivedArgs);
			targets = site.Map.mapPawns.PawnsInFaction(site.Faction);
		}
		public override IEnumerable<GlobalTargetInfo> QuestLookTargets
		{
			get
			{
				foreach (GlobalTargetInfo questLookTarget in base.QuestLookTargets)
				{
					yield return questLookTarget;
				}
				if (targets.NullOrEmpty())
				{
					yield break;
				}
				for (int i = 0; i < targets.Count; i++)
				{
					yield return targets[i];
				}
			}
		}

		public override void QuestPartTick()
		{
			base.QuestPartTick();
			if (Find.TickManager.TicksGame % 60 != 0 || targets == null)
			{
				return;
			}
			foreach (Pawn pawn in targets.ToList())
			{
				if (pawn == null)
				{
					targets.Remove(pawn);
				}
				else if(pawn.MapHeld != site.Map)
				{
					Fail();
				}
				if (targets.Count <= 0)
				{
					Complete();
				}
			}
		}

		public override void Notify_PawnKilled(Pawn pawn, DamageInfo? dinfo)
		{
			base.Notify_PawnKilled(pawn, dinfo);
			if (targets.Contains(pawn))
			{
				targets.Remove(pawn);
				Messages.Message("DMSRC_Mission_Killed".Translate(pawn.Name?.ToStringShort ?? pawn.LabelCap, targets.Count), MessageTypeDefOf.TaskCompletion);
				if(targets.Count <= 0)
				{
					Complete();
				}
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref targets, "targets", LookMode.Reference);
		}
	}
}