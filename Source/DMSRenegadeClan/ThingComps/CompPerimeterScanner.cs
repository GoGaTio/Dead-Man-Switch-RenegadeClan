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
    public class CompProperties_PerimeterScanner : CompProperties_Glower
    {
        public int range;

        public EffecterDef effecter;

        public CompProperties_PerimeterScanner()
        {
            compClass = typeof(CompPerimeterScanner);
        }
    }

    public class CompPerimeterScanner : CompGlower
    {
        public new CompProperties_PerimeterScanner Props => (CompProperties_PerimeterScanner)props;

		private static float lastNotified = -99999f;

		private bool triggered;

		private Effecter detectionEffecter;


		private CompPowerTrader powerTraderComp;

		public CompPowerTrader PowerTraderComp => powerTraderComp ?? (powerTraderComp = parent.GetComp<CompPowerTrader>());

		private TargetInfo TgtInfo => new TargetInfo(parent.Position, parent.Map);

		protected override bool ShouldBeLitNow => triggered;

		public override void CompTick()
		{
			detectionEffecter?.EffectTick(TgtInfo, TgtInfo);
			if (parent.Spawned && parent.IsHashIntervalTick(30))
			{
				RunDetection();
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			if (respawningAfterLoad)
			{
				TickDetectionEffect();
			}
		}

		private void TickDetectionEffect()
		{
			if (triggered && detectionEffecter == null && Props.effecter != null)
			{
				detectionEffecter = Props.effecter.Spawn(parent.Position, parent.Map);
			}
			else if (!triggered && detectionEffecter != null)
			{
				detectionEffecter.Cleanup();
				detectionEffecter = null;
			}
		}

		private void RunDetection()
		{
			bool flag = false;
			if (PowerTraderComp?.PowerOn != false)
			{
				CellRect rect = parent.OccupiedRect().ExpandedBy(Props.range);
				IReadOnlyList<Pawn> allPawnsSpawned = parent.Map.mapPawns.AllPawnsSpawned;
				for (int i = 0; i < allPawnsSpawned.Count; i++)
				{
					if (rect.Contains(allPawnsSpawned[i].Position) && allPawnsSpawned[i].IsPsychologicallyInvisible() && allPawnsSpawned[i].HostileTo(Faction.OfPlayerSilentFail))
					{
						flag = true;
						break;
					}
				}
			}
			if (flag != triggered)
			{
				triggered = flag;
				UpdateLit(parent.Map);
				if (triggered)
				{
					if (RealTime.LastRealTime > lastNotified + 60f)
					{
						Find.LetterStack.ReceiveLetter("DMSRC_LadarDetectedInvisible".Translate(), "DMSRC_MessageLadarTriggered".Translate(), LetterDefOf.ThreatBig, parent, null, null, null, null, 6);
					}
					else
					{
						Messages.Message("DMSRC_MessageLadarTriggered".Translate(), parent, MessageTypeDefOf.ThreatSmall, historical: false);
					}
					lastNotified = RealTime.LastRealTime;
				}
			}
			TickDetectionEffect();
		}

		public override string CompInspectStringExtra()
		{
			if (triggered)
			{
				return "DMSRC_LadarDetectedInvisible".Translate().Colorize(ColorLibrary.RedReadable);
			}
			return base.CompInspectStringExtra();
		}

		public override void PostExposeData()
        {
            base.PostExposeData();
			Scribe_Values.Look(ref triggered, "triggered", defaultValue: false);
		}
    }

	public class PlaceWorker_PerimeterScanner : PlaceWorker
	{
		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			CompProperties_PerimeterScanner comp = def.GetCompProperties<CompProperties_PerimeterScanner>();
			if(comp != null)
			{
				GenDraw.DrawFieldEdges(GenAdj.OccupiedRect(center, rot, def.Size).ExpandedBy(comp.range).Cells.ToList(), Color.white);
			}
		}
	}
}