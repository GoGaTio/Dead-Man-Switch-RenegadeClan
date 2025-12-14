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
using Fortified;

namespace DMSRC
{
	public class CompProperties_NeuroApparel : CompProperties
	{
		public HediffDef hediff;

		public BodyPartDef part;

		public CompProperties_NeuroApparel()
		{
			compClass = typeof(CompNeuroApparel);
		}
	}
	public class CompNeuroApparel : ThingComp
	{
		private CompProperties_NeuroApparel Props => (CompProperties_NeuroApparel)props;

		private Faction faction;

		public override void Notify_Equipped(Pawn pawn)
		{
			if (pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediff) == null)
			{
				HediffComp_RemoveIfApparelDropped hediffComp_RemoveIfApparelDropped = pawn.health.AddHediff(Props.hediff, pawn.health.hediffSet.GetNotMissingParts().FirstOrFallback((BodyPartRecord p) => p.def == Props.part)).TryGetComp<HediffComp_RemoveIfApparelDropped>();
				if (hediffComp_RemoveIfApparelDropped != null)
				{
					hediffComp_RemoveIfApparelDropped.wornApparel = (Apparel)parent;
				}
			}
		}

		public virtual void ExposeData()
		{
			Scribe_References.Look(ref faction, "faction");
		}
	}
}
