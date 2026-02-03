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
    public class CompProperties_SecurityContainer : CompProperties
    {
        public ThingSetMakerDef defaultLoot;

        public FactionDef faction;

		public GraphicData graphicData;

		public int openTime = 120;

		public SoundDef completedSound;

		public IntRange defenceFactorRange = IntRange.One;

		public CompProperties_SecurityContainer()
        {
            compClass = typeof(CompSecurityContainer);
        }
    }

    public class CompSecurityContainer : ThingComp
    {
        public CompProperties_SecurityContainer Props => (CompProperties_SecurityContainer)props;

        public Building_SecurityContainer Parent => parent as Building_SecurityContainer;

        public override void PostDraw()
		{
			base.PostDraw();
			if (Parent.opened)
			{
				Mesh mesh = Props.graphicData.Graphic.MeshAt(parent.Rotation);
				Vector3 drawPos = parent.DrawPos;
				drawPos.y = AltitudeLayer.BuildingOnTop.AltitudeFor() + parent.def.graphicData.drawOffset.y;
				Graphics.DrawMesh(mesh, drawPos + Props.graphicData.drawOffset.RotatedBy(parent.Rotation), Quaternion.identity, Props.graphicData.Graphic.MatAt(parent.Rotation), 0);
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			if (!respawningAfterLoad && !parent.BeingTransportedOnGravship && parent.TryGetComp<CompHackable>(out var comp) && !comp.IsHacked)
			{
				comp.defence *= Props.defenceFactorRange.RandomInRange;
			}
			base.PostSpawnSetup(respawningAfterLoad);
		}

		public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            foreach(var op in base.CompFloatMenuOptions(selPawn))
            {
				yield return op;
            }
			if (Parent.opened)
			{
				yield break;
			}
			if (selPawn.RaceProps.intelligence < Intelligence.ToolUser)
			{
				yield break;
			}
			if(parent.TryGetComp<CompHackable>(out var comp) && !comp.IsHacked)
			{
				yield break;
			}
			bool flag = true;
			string reason = "";
			if (!selPawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
			{
				flag = false;
				reason = "Incapable".Translate().CapitalizeFirst();
			}
			if (!selPawn.CanReach(parent, PathEndMode.InteractionCell, Danger.Deadly))
			{
				flag = false;
				reason = "NoPath".Translate().CapitalizeFirst();
			}
			if (flag)
			{
				yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Open".Translate(parent), delegate
				{
					Job job = JobMaker.MakeJob(RCDefOf.DMSRC_OpenContainer, parent);
					job.ignoreDesignations = true;
					selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
				}, MenuOptionPriority.High), selPawn, parent);
			}
			else
			{
				yield return new FloatMenuOption("CannotOpen".Translate(parent) + ": " + reason, null);
			}
		}

		public override void PostPostMake()
        {
            base.PostPostMake();
			if(Props.defaultLoot == null)
			{
				return;
			}
            ThingSetMakerParams parms = default(ThingSetMakerParams);
			if(Props.faction != null)
			{
				parms.makingFaction = Find.FactionManager.FirstFactionOfDef(Props.faction);
			}
            Parent.innerContainer.TryAddRangeOrTransfer(Props.defaultLoot.root.Generate(parms));
        }
    }
}