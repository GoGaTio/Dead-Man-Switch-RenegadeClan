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

		public IntRange? initialWavesCooldown;

		public SimpleCurve wavesPointsFromQuest = new SimpleCurve
		{
			new CurvePoint(200f, 550f),
			new CurvePoint(400f, 1100f),
			new CurvePoint(800f, 1600f),
			new CurvePoint(1600f, 2600f),
			new CurvePoint(3200f, 3600f),
			new CurvePoint(30000f, 10000f)
		};

		public SimpleCurve wavesPointsFromMap = new SimpleCurve
		{
			new CurvePoint(1f, 2f)
		};

		public bool wavesFromOppositeFaction;

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

		public float points;

		public string inSignalFail;

		public string inSignalSuccess;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref site, "site");
			Scribe_Deep.Look(ref mission, "mission");
			Scribe_Values.Look(ref inSignalFail, "inSignalFail");
			Scribe_Values.Look(ref points, "points");
			Scribe_Values.Look(ref inSignalSuccess, "inSignalSuccess");
		}

		protected override void Enable(SignalArgs receivedArgs)
		{
			base.Enable(receivedArgs);

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

	public class QuestPart_MissionWithWaves : QuestPart_Mission
	{
		public override AlertReport AlertReport => new AlertReport() { active = wavesActive && wavesSent < 1 && wavesSent == wavesDefeated };

		public override bool AlertCritical => ticksTillNextWave < Mathf.Max(2500f, mission.wavesCooldown.min / 2f);

		public override string AlertExplanation => "DMSRC_Mission_WaveDesc".Translate();

		public override string AlertLabel => "DMSRC_Mission_WaveLabel".Translate(ticksTillNextWave.ToStringTicksToPeriod());

		public bool wavesActive = true;

		public int ticksTillNextWave = -1;

		public int wavesSent;

		public int wavesDefeated;

		public virtual bool CanSendWave
		{
			get
			{
				if (wavesActive)
				{
					return wavesSent <= wavesDefeated;
				}
				return false;
			}
		}

		protected override void Enable(SignalArgs receivedArgs)
		{
			ticksTillNextWave = (mission.initialWavesCooldown ?? mission.wavesCooldown).RandomInRange;
			base.Enable(receivedArgs);
			GameComponent_Renegades comp = GameComponent_Renegades.Find;
			if (WavesFaction.def == RCDefOf.DMSRC_RenegadeClan)
			{
				comp.PlayerRelation = FactionRelationKind.Hostile;
				comp.playerGoodwill = -200;
			}
			else if(!comp.DMSFaction.HostileTo(Faction.OfPlayerSilentFail))
			{
				comp.DMSFaction.SetRelation(new FactionRelation(Faction.OfPlayerSilentFail, FactionRelationKind.Hostile) { baseGoodwill = -200});
				comp.enemyWithFleet = true;
			}
		}

		public virtual Faction WavesFaction
		{
			get
			{
				if (mission.wavesFromOppositeFaction)
				{
					if(site.Faction.def == RCDefOf.DMSRC_RenegadeClan)
					{
						return GameComponent_Renegades.Find.DMSFaction;
					}
					else return GameComponent_Renegades.Find.RenegadesFaction;
				}
				return site.Faction;
			}
		}

		public override void QuestPartTick()
		{
			base.QuestPartTick();
			if (CanSendWave)
			{
				ticksTillNextWave--;
				if(ticksTillNextWave < 0)
				{
					ticksTillNextWave = mission.wavesCooldown.RandomInRange;
					SendWave();
				}
			}
		}

		protected string WaveTag => "Quest" + this.quest.id + ".WaveNo" + wavesSent + "Sent";

		public virtual void SendWave(List<Thing> attackTargets = null)
		{
			wavesSent++;
			Log.Message(wavesSent);
			Map map = site.Map;
			List<Pawn> list = new List<Pawn>();
			IncidentParms incidentParms = new IncidentParms();
			incidentParms.target = map;
			incidentParms.points = mission.wavesPointsFromMap.Evaluate(StorytellerUtility.DefaultThreatPointsNow(map)) + mission.wavesPointsFromQuest.Evaluate(points);
			incidentParms.faction = WavesFaction;
			incidentParms.attackTargets = attackTargets;
			incidentParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
			incidentParms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeDrop;
			incidentParms.questTag = WaveTag;
			incidentParms.raidNeverFleeIndividual = true;
			incidentParms.canTimeoutOrFlee = false;
			incidentParms.canSteal = false;
			if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(incidentParms))
			{
				if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(incidentParms))
				{
					WaveDefeated();
				}
			}
		}

		public override void Notify_QuestSignalReceived(Signal signal)
		{
			if (wavesDefeated < wavesSent && (signal.tag == WaveTag + ".Fleeing" || signal.tag == WaveTag + ".AllEnemiesDefeated"))
			{
				WaveDefeated();
			}
			base.Notify_QuestSignalReceived(signal);
		}

		public virtual void WaveDefeated()
		{
			wavesDefeated++;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref wavesActive, "wavesActive");
			Scribe_Values.Look(ref wavesSent, "wavesSent");
			Scribe_Values.Look(ref wavesDefeated, "wavesDefeated");
			Scribe_Values.Look(ref ticksTillNextWave, "ticksTillNextWave", defaultValue: -1);
		}
	}

	public class QuestPart_Mission_Defend : QuestPart_MissionWithWaves
	{
		public List<Thing> targets;

		public int wavesLeft;

		public override AlertReport AlertReport => new AlertReport() { active = wavesLeft > 0, culpritsThings = targets };

		public override string AlertExplanation => "DMSRC_Mission_DefendDesc".Translate(mission.targetDef.label);

		public override string AlertLabel => wavesSent == wavesDefeated ? "DMSRC_Mission_DefendWaveLabel".Translate(ticksTillNextWave.ToStringTicksToPeriod()).Resolve() : "DMSRC_Mission_DefendLabel".Translate(mission.targetDef.label).Resolve();

		private string Tag => "Quest" + quest.id + ".DMSRCMission." + mission.targetDef.defName;

		protected override void Enable(SignalArgs receivedArgs)
		{
			base.Enable(receivedArgs);
			targets = site.Map.listerThings.ThingsOfDef(mission.targetDef);
			foreach (Thing thing in targets.ToList())
			{
				if(thing.questTags == null)
				{
					thing.questTags = new List<string>();
				}
				thing.questTags.Add(Tag);
			}
			wavesLeft = 3;
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

		public override void SendWave(List<Thing> attackTargets = null)
		{
			base.SendWave(targets);
		}

		public override void WaveDefeated()
		{
			base.WaveDefeated();
			wavesLeft--;
			if(wavesLeft <= 0)
			{
				Complete();
			}
		}

		public override void Notify_QuestSignalReceived(Signal signal)
		{
			base.Notify_QuestSignalReceived(signal);
			if(signal.tag == Tag + ".Destroyed")
			{
				Thing t = signal.args.GetArg("SUBJECT").arg as Thing;
				if(t != null)
				{
					targets.Remove(t);
				}
				if (targets.NullOrEmpty())
				{
					Fail();
				}
				else
				{
					Messages.Message("DMSRC_Mission_Destroyed".Translate(mission.targetDef.LabelCap, targets.Count), MessageTypeDefOf.ThreatBig);
				}
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref targets, "targets", LookMode.Reference);
			Scribe_Values.Look(ref wavesLeft, "wavesLeft");
		}
	}

	public class QuestPart_Mission_Kill : QuestPart_MissionWithWaves
	{
		public List<Pawn> targets = new List<Pawn>();

		public override AlertReport AlertReport
		{
			get
			{
				AlertReport report = base.AlertReport;
				report.culpritsPawns = targets;
				return report;
			}
		}

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
				Messages.Message("DMSRC_Mission_Killed".Translate(pawn.Name?.ToStringShort ?? pawn.LabelCap, targets.Count == 1 ? pawn.kindDef.label : pawn.kindDef.labelPlural, targets.Count), MessageTypeDefOf.TaskCompletion);
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

	public class QuestPart_Mission_Destroy : QuestPart_MissionWithWaves
	{
		public List<Thing> targets;

		private string Tag => "Quest" + quest.id + ".DMSRCMission." + mission.targetDef.defName;

		public override AlertReport AlertReport
		{
			get
			{
				AlertReport report = base.AlertReport;
				report.culpritsThings = targets;
				return report;
			}
		}

		protected override void Enable(SignalArgs receivedArgs)
		{
			base.Enable(receivedArgs);
			targets = site.Map.listerThings.ThingsOfDef(mission.targetDef);
			foreach (Thing thing in targets.ToList())
			{
				if (thing.questTags == null)
				{
					thing.questTags = new List<string>();
				}
				thing.questTags.Add(Tag);
			}
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

		public override void Notify_QuestSignalReceived(Signal signal)
		{
			base.Notify_QuestSignalReceived(signal);
			if (signal.tag == Tag + ".Destroyed")
			{
				Thing t = signal.args.GetArg("SUBJECT").arg as Thing;
				if (t != null)
				{
					targets.Remove(t);
				}
				if (targets.NullOrEmpty())
				{
					Complete();
				}
				else
				{
					Messages.Message("DMSRC_Mission_Destroyed".Translate(mission.targetDef.LabelCap, targets.Count), MessageTypeDefOf.ThreatBig);
				}
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref targets, "targets", LookMode.Reference);
		}
	}

	public class QuestPart_Mission_DestroyFacility : QuestPart_MissionWithWaves
	{
		private FacilityEntrance target;

		private string Tag => "Quest" + quest.id + ".DMSRCMission.Facility";

		public override AlertReport AlertReport
		{
			get
			{
				AlertReport report = base.AlertReport;
				report.culpritsThings = new List<Thing>() { target };
				return report;
			}
		}

		protected override void Enable(SignalArgs receivedArgs)
		{
			base.Enable(receivedArgs);
			target = site.Map.listerThings.AllThings.FirstOrDefault((Thing t) => t is FacilityEntrance) as FacilityEntrance;
			if(target != null)
			{
				if (target.questTags == null)
				{
					target.questTags = new List<string>();
				}
				target.questTags.Add(Tag);
			}
		}

		public override IEnumerable<GlobalTargetInfo> QuestLookTargets
		{
			get
			{
				foreach (GlobalTargetInfo questLookTarget in base.QuestLookTargets)
				{
					yield return questLookTarget;
				}
				if(site == null || site.Map == null)
				{
					yield break;
				}
				if (target == null)
				{
					target = site.Map.listerThings.AllThings.FirstOrDefault((Thing t) => t is FacilityEntrance) as FacilityEntrance;
					yield break;
				}
				yield return target;
			}
		}

		public override void Notify_QuestSignalReceived(Signal signal)
		{
			base.Notify_QuestSignalReceived(signal);
			if (signal.tag == Tag + ".FacilityDestroyed")
			{
				Complete();
			}
		}
	}
}