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

namespace DMSRC
{
    public class CompProperties_UsableNeuroChip : CompProperties_Usable
    {
		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			foreach (StatDrawEntry item1 in base.SpecialDisplayStats(req))
			{
				yield return item1;
			}
			foreach (StatDrawEntry item2 in Implant(req.Thing?.def ?? req.Def as ThingDef).stages[0].SpecialDisplayStats())
			{
				yield return item2;
			}
		}

		public override void ResolveReferences(ThingDef parentDef)
		{
			base.ResolveReferences(parentDef);
			if(parentDef.descriptionHyperlinks == null)
			{
				parentDef.descriptionHyperlinks = new List<DefHyperlink>();
			}
			parentDef.descriptionHyperlinks.Add(new DefHyperlink(Implant(parentDef)));
		}

		public HediffDef Implant(ThingDef parentDef) => parentDef?.GetCompProperties<CompProperties_UseEffectInstallImplant>()?.hediffDef;

		public CompProperties_UsableNeuroChip()
		{
			compClass = typeof(CompUsableNeuroChip);
		}
	}
	public class CompUsableNeuroChip : CompUsableImplant
	{
        public override AcceptanceReport CanBeUsedBy(Pawn p, bool forced = false, bool ignoreReserveAndReachable = false)
        {
            AcceptanceReport report = base.CanBeUsedBy(p, forced, ignoreReserveAndReachable);
            if (!report.Accepted)
            {
                return report;
            }
            if (p.health.hediffSet.GetFirstHediff<Hediff_Neurointerface>() is Hediff_Neurointerface inter && inter != null && (parent.TryGetComp<CompUseEffect_InstallImplant>().Props.hediffDef.stages.Any((HediffStage x)=> !x.statOffsets.NullOrEmpty() && x.statOffsets.Any((StatModifier y) => y.stat == RCDefOf.DMSRC_Neurocapacity)) || inter.UsedCapacity < inter.Capacity))
            {
                return true;
            }
            return false;
        }

		public override void UsedBy(Pawn p)
		{
			base.UsedBy(p);
			Hediff_NeuroChip.Recalculate(p);
		}
	}
}
