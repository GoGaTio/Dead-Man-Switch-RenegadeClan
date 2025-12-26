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
		public bool wavesActive = true;

		public int ticksTillNextWave = -1;

		public int wavesSent = 0;

		public int wavesDefeated = 0;
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

		public virtual void SendWave(List<Thing> attackTargets = null)
		{
			wavesSent++;
			Map map = site.Map;
			List<Pawn> list = new List<Pawn>();
			IncidentParms incidentParms = new IncidentParms();
			incidentParms.target = map;
			incidentParms.points = mission.wavesPointsFromMap.Evaluate(StorytellerUtility.DefaultThreatPointsNow(map)) + mission.wavesPointsFromQuest.Evaluate(points);
			incidentParms.faction = WavesFaction;
			incidentParms.attackTargets = attackTargets;
			incidentParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
			incidentParms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeDrop;
			incidentParms.questTag = this.quest.id + "_WaveNo" + wavesSent + "Sent";
			Log.Message(incidentParms.questTag);
			if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(incidentParms))
			{
				Log.Error("BRUH");
			}
		}

		public override void Notify_QuestSignalReceived(Signal signal)
		{
			Log.Message("Notify_QuestSignalReceived: " + signal.tag);
			if (signal.tag == this.quest.id + "_WaveNo" + wavesSent + "Sent" + ".Fleeing" || signal.tag == this.quest.id + "_WaveNo" + wavesSent + "Sent" + ".AllEnemiesDefeated")
			{
				if(wavesDefeated < wavesSent)
				{
					wavesDefeated = wavesSent;
				}
			}
			base.Notify_QuestSignalReceived(signal);
		}

		protected override void ProcessQuestSignal(Signal signal)
		{
			Log.Message("ProcessQuestSignal: " + signal.tag);
			base.ProcessQuestSignal(signal);
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

	public class QuestPart_Mission_Defend : QuestPart_MissionWithWaves
	{
		public List<Thing> targets;

		public int wavesLeft;

		public override AlertReport AlertReport => new AlertReport() { active = true, culpritsThings = targets };

		public override bool AlertCritical => ticksTillNextWave < Mathf.Max(2500f, mission.wavesCooldown.min / 2f);

		public override string AlertExplanation => "DMSRC_Mission_DefendDesc".Translate(ticksTillNextWave.ToStringTicksToPeriod(), mission.targetDef.label);

		public override string AlertLabel => "DMSRC_Mission_DefendLabel".Translate(ticksTillNextWave.ToStringTicksToPeriod(), mission.targetDef.label);

		protected override void Enable(SignalArgs receivedArgs)
		{
			base.Enable(receivedArgs);
			targets = site.Map.listerThings.ThingsOfDef(mission.targetDef);
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
			if (wavesDefeated >= wavesLeft)
			{
				Complete();
			}
		}

		public override void SendWave(List<Thing> attackTargets = null)
		{
			base.SendWave(targets);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref targets, "targets", LookMode.Reference);
			Scribe_Values.Look(ref wavesLeft, "wavesLeft");
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