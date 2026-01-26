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
using static System.Net.Mime.MediaTypeNames;

namespace DMSRC
{
    public class CompProperties_TimedBomb : CompProperties_Explosive
	{
		public FleckDef fleck;

		public Vector3 offset;

		public SoundDef soundOnEmission;

		public SimpleCurve emissionIntervalCurve = new SimpleCurve()
		{
			new CurvePoint(0f, 0f)
		};

		public IntRange defenceFactorRange = IntRange.One;

		public CompProperties_TimedBomb()
        {
            compClass = typeof(CompTimedBomb);
			explodeOnDestroyed = true;
		}
    }

    public class CompTimedBomb : CompExplosive
    {
        public new CompProperties_TimedBomb Props => (CompProperties_TimedBomb)props;

        public bool defused = false;

		public int timerTicks = 10000;

		public int ticksTillEmit = 360;

		protected override bool CanEverExplodeFromDamage => !defused;

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			if (!respawningAfterLoad && !parent.BeingTransportedOnGravship && parent.TryGetComp<CompHackable>(out var comp) && !comp.IsHacked)
			{
				comp.defence *= Props.defenceFactorRange.RandomInRange;
			}
			base.PostSpawnSetup(respawningAfterLoad);
		}

		public override void ReceiveCompSignal(string signal)
		{
			if (!defused && signal == "Hacked")
			{
				defused = true;
			}
		}

		public override void CompTick()
		{
			if (defused)
			{
				return;
			}
			ticksTillEmit--;
			if (ticksTillEmit <= 0)
			{
				Emit();
				ticksTillEmit = Mathf.RoundToInt(Props.emissionIntervalCurve.Evaluate(timerTicks));
			}
			timerTicks--;
			if (timerTicks <= 0)
			{
				Map map = parent.MapHeld;
				defused = true;
				base.Detonate(map, true);
			}
		}

		protected void Emit()
		{
			if (Props.fleck == null)
			{
				return;
			}
			FleckMaker.Static(parent.DrawPos + Props.offset, parent.MapHeld, Props.fleck);
			if (!Props.soundOnEmission.NullOrUndefined())
			{
				Props.soundOnEmission.PlayOneShot(SoundInfo.InMap(parent));
			}
		}
		public override void PostDestroy(DestroyMode mode, Map previousMap)
		{
			if(defused) destroyedThroughDetonation = true;
			base.PostDestroy(mode, previousMap);
		}

		public override bool CompPreventClaimingBy(Faction faction)
		{
			if (!defused)
			{
				return true;
			}
			return base.CompPreventClaimingBy(faction);
		}

		public override void PostExposeData()
        {
            base.PostExposeData();
			Scribe_Values.Look(ref defused, "DMSRC_defused");
			Scribe_Values.Look(ref timerTicks, "DMSRC_timerTicks");
			Scribe_Values.Look(ref ticksTillEmit, "DMSRC_ticksTillEmit", 0);
		}

		public override string CompInspectStringExtra()
		{
			string s = base.CompInspectStringExtra();
			if(parent.Faction?.HostileTo(Faction.OfPlayerSilentFail) == false || DebugSettings.ShowDevGizmos)
			{
				s += "DMSRC_DetonationCountdown".Translate(timerTicks.ToStringTicksToPeriod());
				//s += timerTicks.ToStringTicksToPeriod()
			}
			else if(parent.Faction != null)
			{
				s += "DMSRC_DetonationInstigator".Translate(parent.Faction.NameColored.Resolve());
			}
			return s;
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo g in base.CompGetGizmosExtra())
			{
				yield return g;
			}
			if (DebugSettings.ShowDevGizmos)
			{
				yield return new Command_Action
				{
					defaultLabel = "DEV: 10 seconds",
					action = delegate
					{
						timerTicks = 600;
					}
				};
			}
		}
    }
}