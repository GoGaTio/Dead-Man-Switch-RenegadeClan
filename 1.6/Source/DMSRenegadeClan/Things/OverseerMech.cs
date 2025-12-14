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
using DMS;
using Fortified;

namespace DMSRC
{
    public class Hediff_DummyPawn : Hediff
    {
        public OverseerMech overseer;
    }
    public class OverseerMech : WeaponUsableMech
    {
		private CompOverseerMech comp;

        public CompOverseerMech Comp
        {
            get
            {
                if(comp == null)
                {
                    comp = GetComp<CompOverseerMech>();
                }
                return comp;
            }
        }

		public float MinCharge => 0.05f;

		public float MaxCharge => 1f;

        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            base.SetFaction(newFaction, recruiter);
			if (newFaction != null && newFaction.IsPlayer)
			{
				Comp?.UpdateDummy();
			}
		}
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode != DestroyMode.WillReplace)
            {
                Comp.dummyPawn?.mechanitor?.UndraftAllMechs();
            }
            base.DeSpawn(mode);
        }

        public override IEnumerable<FloatMenuOption> GetExtraFloatMenuOptionsFor(IntVec3 sq)
        {
			foreach(FloatMenuOption item in base.GetExtraFloatMenuOptionsFor(sq))
            {
				yield return item;
            }
			List<Thing> thingList = sq.GetThingList(Map);
			foreach (Thing thing in thingList)
			{
				Pawn mech;
				if ((mech = thing as Pawn) == null || mech == this || !mech.IsColonyMech)
				{
					continue;
				}
				if (mech.GetOverseer() != Comp.dummyPawn)
				{
					if (!this.CanReach(mech, PathEndMode.Touch, Danger.Deadly))
					{
						yield return new FloatMenuOption("CannotControlMech".Translate(mech.LabelShort) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
					}
					else if (!MechanitorUtility.CanControlMech(Comp.dummyPawn, mech))
					{
						AcceptanceReport acceptanceReport = MechanitorUtility.CanControlMech(Comp.dummyPawn, mech);
						if (!acceptanceReport.Reason.NullOrEmpty())
						{
							yield return new FloatMenuOption("CannotControlMech".Translate(mech.LabelShort) + ": " + acceptanceReport.Reason, null);
						}
					}
					else
					{
						yield return RimWorld.FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("ControlMech".Translate(mech.LabelShort), delegate
						{
							Job job = JobMaker.MakeJob(RCDefOf.DMSRC_ControlMech, thing);
							jobs.TryTakeOrderedJob(job, JobTag.Misc);
						}), this, new LocalTargetInfo(thing));
					}
					yield return new FloatMenuOption("CannotDisassembleMech".Translate(mech.LabelCap) + ": " + "MustBeOverseer".Translate().CapitalizeFirst(), null);
				}
				else
				{
					yield return RimWorld.FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("DisconnectMech".Translate(mech.LabelShort), delegate
					{
						MechanitorUtility.ForceDisconnectMechFromOverseer(mech);
					}, MenuOptionPriority.Low, null, null, 0f, null, null, playSelectionSound: true, -10), this, new LocalTargetInfo(thing));
					if (!mech.IsFighting())
					{
						yield return (RimWorld.FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("DisassembleMech".Translate(mech.LabelCap), delegate
						{
							Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmDisassemblingMech".Translate(mech.LabelCap) + ":\n" + (from x in MechanitorUtility.IngredientsFromDisassembly(mech.def)
																																					 select x.Summary).ToLineList("  - "), delegate
																																					 {
																																						 this.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.DisassembleMech, thing), JobTag.Misc);
																																					 }, destructive: true));
						}, MenuOptionPriority.Low, null, null, 0f, null, null, playSelectionSound: true, -20), this, new LocalTargetInfo(thing)));
					}
				}
				if (!Comp.Props.canRepair || !MechRepairUtility.CanRepair(mech))
				{
					continue;
				}
				if (!this.CanReach(mech, PathEndMode.Touch, Danger.Deadly))
				{
					yield return new FloatMenuOption("CannotRepairMech".Translate(mech.LabelShort) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
					continue;
				}
				yield return (RimWorld.FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("RepairThing".Translate(mech.LabelShort), delegate
				{
					Job job2 = JobMaker.MakeJob(JobDefOf.RepairMech, mech);
					jobs.TryTakeOrderedJob(job2, JobTag.Misc);
				}), this, new LocalTargetInfo(thing)));
			}
		}
    }
}