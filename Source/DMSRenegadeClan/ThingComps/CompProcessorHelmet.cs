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
using DelaunatorSharp;
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
using HarmonyLib;

namespace DMSRC
{
	public class CompProperties_ProcessorHelmet : CompProperties
	{
		public HediffDef hediff;

		public CompProperties_ProcessorHelmet()
		{
			compClass = typeof(CompProcessorHelmet);
		}
	}
	public class CompProcessorHelmet : ThingComp
	{
		private CompProperties_ProcessorHelmet Props => (CompProperties_ProcessorHelmet)props;

		public override void Notify_Equipped(Pawn pawn)
		{
			if (pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediff) == null)
			{
				Hediff_ProcessorHelmet hediff = pawn.health.AddHediff(Props.hediff, pawn.health.hediffSet.GetBrain()) as Hediff_ProcessorHelmet;
				if (hediff != null)
				{
					hediff.wornApparel = (Apparel)parent;
				}
			}
		}
	}

	public class Hediff_ProcessorHelmet : HediffWithComps
	{
		public Apparel wornApparel;

		public bool? activeInt;

		public bool Active
		{
			get
			{
				if(activeInt == null)
				{
					activeInt = pawn.health.hediffSet.HasHediff<Hediff_Neurointerface>();
				}
				return activeInt.Value;
			}
		}

		public override HediffStage CurStage
		{
			get
			{
				if (Active)
				{
					return base.CurStage;
				}
				return new HediffStage() { becomeVisible = false };
			}
		}

		public override void PreRemoved()
		{
			base.PreRemoved();
			if(wornApparel != null && wornApparel.TryGetComp<CompProcessorHelmet>(out var comp))
			{
				Hediff_Neurointerface hediff = pawn.health.hediffSet.GetFirstHediff<Hediff_Neurointerface>();
				if(hediff == null)
				{
					return;
				}
				int count = hediff.UsedCapacity - (hediff.Capacity - Mathf.RoundToInt(CurStage.statOffsets.GetStatOffsetFromList(RCDefOf.DMSRC_Neurocapacity)));
				if(count > 0)
				{
					foreach (Hediff h in pawn.health.hediffSet.hediffs)
					{
						if (h is Hediff_NeuroChip hediff_Level && hediff_Level.Level > 1)
						{
							if (hediff_Level.level > count)
							{
								hediff_Level.disabledLevels = count;
								break;
							}
							else
							{
								hediff_Level.disabledLevels = hediff_Level.level - 1;
								count -= hediff_Level.disabledLevels;
							}
						}
					}
				}
			}
		}

		public override void PostAdd(DamageInfo? dinfo)
		{
			base.PostAdd(dinfo);
			Hediff_NeuroChip.Recalculate(pawn);
		}

		public override bool ShouldRemove => !pawn.apparel.Wearing(wornApparel);

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref wornApparel, "DMSRC_wornApparel");
		}
	}
}