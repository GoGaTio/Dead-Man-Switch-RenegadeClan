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
    public class CompProperties_CompOverseerMech : CompProperties
    {
        public float commandRange = 34.9f;

        public int controlGroups = 2;

        public int bandwidth = 6;

        public bool canRepair = true;

        public int ticksPerHeal = 120;

		public bool instantControl = false;

		[NoTranslate]
        public string selectOverseerIconPath = "UI/Icons/DMSRC_SelectOverseer";

        public CompProperties_CompOverseerMech()
        {
            compClass = typeof(CompOverseerMech);
        }
    }

    public class CompOverseerMech : ThingComp, ITargetingSource
    {
        public bool CasterIsPawn => true;
        public bool IsMeleeAttack => false;
        public bool Targetable => true;
        public bool MultiSelect => false;
        public bool HidePawnTooltips => false;
        public Thing Caster => Parent;
        public Pawn CasterPawn => null;
        public Verb GetVerb => null;
        public TargetingParameters targetParams => new TargetingParameters()
        {
            canTargetPawns = true,
            canTargetLocations = false
        };
        public Texture2D UIIcon => TexCommand.Install;
        public ITargetingSource DestinationSelector => null;
        public bool CanHitTarget(LocalTargetInfo target)
        {
            return ValidateTarget(target, showMessages: false);
        }
        public bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (target.IsValid && target.HasThing && target.Thing is Pawn pawn)
            {
                AcceptanceReport acceptanceReport = MechanitorUtility.CanControlMech(dummyPawn, pawn);
                if (!acceptanceReport.Accepted)
                {
                    if (showMessages && !acceptanceReport.Reason.NullOrEmpty())
                    {
						Messages.Message(acceptanceReport.Reason.CapitalizeFirst(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                    }
                    return false;
                }
                return true;
            }
            return false;
        }
        public void DrawHighlight(LocalTargetInfo target)
        {
            if (target.IsValid)
            {
                GenDraw.DrawTargetHighlight(target);
            }
        }

        public virtual void OrderForceTarget(LocalTargetInfo target)
        {
            if (target.IsValid && target.HasThing && target.Thing is Pawn pawn)
            {
                if (MechanitorUtility.CanControlMech(dummyPawn, pawn))
                {
                    Connect(pawn, dummyPawn);
                }
            }
        }
        public void OnGUI(LocalTargetInfo target)
        {
            if (ValidateTarget(target, showMessages: false))
            {
                GenUI.DrawMouseAttachment(UIIcon);
            }
            else
            {
                GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
            }
        }
        public CompProperties_CompOverseerMech Props => (CompProperties_CompOverseerMech)props;
        private MechWorkModeDef workMode;
        protected Effecter connectMechEffecter;
        protected Effecter connectProgressBarEffecter;
        public int connectTick = -1;
        public LocalTargetInfo curTarget = LocalTargetInfo.Invalid;
        public Pawn dummyPawn;
        public int hackCooldownTicks;
        public bool MechanitorActive => dummyPawn != null && Parent.IsColonyMech;

        public OverseerMech Parent => parent as OverseerMech;

        public int CurrentBandwidth
        {
            get
            {
                int num = Props.bandwidth;
                num += (int)dummyPawn.GetStatValue(StatDefOf.MechBandwidth);
                num -= 6;
				return num;
            }
        }

        public MechWorkModeDef WorkMode
        {
            get
            {
                if(workMode == null)
                {
                    workMode = MechWorkModeDefOf.Work;
                }
                return workMode;
            }
        }

        private Texture2D selectIcon;
		public Texture2D SelectIcon
		{
			get
			{
				if (selectIcon == null)
				{
					selectIcon = ContentFinder<Texture2D>.Get(Props.selectOverseerIconPath);
				}
				return selectIcon;
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (Parent.IsColonyMech)
            {
                UpdateDummy();
            }
            Parent.GetOverseer()?.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, Parent);
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (MechanitorActive && parent.Spawned && dummyPawn.mechanitor.AnySelectedDraftedMechs)
            {
                GenDraw.DrawRadiusRing(parent.Position, Props.commandRange, Color.white, (IntVec3 c) => c.InBounds(parent.MapHeld));
            }
        }
        public void UpdateDummy()
        {
            if (dummyPawn is null)
            {
                PawnGenerationRequest req = new PawnGenerationRequest(RCDefOf.DMSRC_DummyMechanitor, Faction.OfAncients, forcedXenotype: XenotypeDefOf.Baseliner, forceGenerateNewPawn: true);
                dummyPawn = PawnGenerator.GeneratePawn(req);
                dummyPawn.SetFactionDirect(parent.Faction);
                dummyPawn.Name = new NameSingle(parent.LabelCap);
                for (int num = dummyPawn.health.hediffSet.hediffs.Count - 1; num >= 0; num--)
                {
                    Hediff h = dummyPawn.health.hediffSet.hediffs.FirstOrDefault((Hediff x) => x.def != RCDefOf.DMSRC_DummyPawn && !(x is Hediff_Mechlink) && !x.def.HasModExtension<ForceNotRemoveExtension>());
                    if(h == null)
                    {
                        break;
                    }
                    dummyPawn.health.RemoveHediff(h);
                }
            }
            Hediff_DummyPawn hediff = (Hediff_DummyPawn)dummyPawn.health.GetOrAddHediff(RCDefOf.DMSRC_DummyPawn);
            hediff.overseer = Parent;
            hediff.Severity = Mathf.Max(Props.controlGroups - 2, 0.5f);
			PawnComponentsUtility.AddComponentsForSpawn(dummyPawn);
            PawnComponentsUtility.AddAndRemoveDynamicComponents(dummyPawn);
            dummyPawn.mechanitor.Notify_BandwidthChanged();
            dummyPawn.gender = Gender.None;
            dummyPawn.equipment.DestroyAllEquipment();
            dummyPawn.story.title = "";
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            if (dummyPawn != null)
            {
                dummyPawn.mechanitor.UndraftAllMechs();
                List<Pawn> list = dummyPawn.mechanitor.OverseenPawns.ToList();
                foreach (Pawn p in list)
                {
                    dummyPawn.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, p);
                }
                dummyPawn.mechanitor.Notify_BandwidthChanged();
            }
            
            base.Notify_Killed(prevMap, dinfo);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra())
            {
                yield return g;
            }
            if (parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }
            if (dummyPawn == null)
            {
                UpdateDummy();
                yield break;
            }
            foreach (var m in dummyPawn.mechanitor.GetGizmos())
            {
                if (m is Command_CallBossgroup)
                {
                    continue;
                }
                yield return m;
            }
            Command_Action command_Action = new Command_Action();
            command_Action.defaultLabel = "CurrentMechWorkMode".Translate() + ": " + WorkMode.LabelCap;
            command_Action.defaultDesc = "CurrentMechWorkMode".Translate() + ": " + WorkMode.LabelCap + "\n\n" + WorkMode.description + "\n\n" + "ClickToChangeWorkMode".Translate();
            command_Action.icon = WorkMode.uiIcon;
            command_Action.action = delegate
            {
                List<FloatMenuOption> list = new List<FloatMenuOption>();
                List<MechWorkModeDef> list2 = DefDatabase<MechWorkModeDef>.AllDefs.ToList();
                for (int i = 0; i < list2.Count; i++)
                {
                    MechWorkModeDef mode = list2[i];
                    list.Add(new FloatMenuOption(mode.LabelCap, delegate
                    {
                        workMode = mode;
                        PawnComponentsUtility.AddAndRemoveDynamicComponents(Parent, actAsIfSpawned: true);
                        if (workMode != MechWorkModeDefOf.Recharge && Parent.CurJobDef == JobDefOf.MechCharge && Parent.IsCharging())
                        {
                            Parent.jobs.EndCurrentJob(JobCondition.InterruptForced);
                        }
                        Parent.TryGetComp<CompCanBeDormant>()?.WakeUp();
                        Parent.jobs?.CheckForJobOverride();
                    }));
                }
                if (list.Any())
                {
                    Find.WindowStack.Add(new FloatMenu(list));
                }
            };
            yield return command_Action;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.IsHashIntervalTick(240) || !MechanitorActive)
            {
                return;
            }
            if (dummyPawn.Faction != parent.Faction)
            {
                dummyPawn.SetFaction(parent.Faction);
            }
            Pawn pawn = Parent.GetOverseer();
            if (pawn != null)
            {
                pawn.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, Parent);
                pawn.mechanitor.Notify_BandwidthChanged();
            }
        }

        public void Connect(Pawn mech, Pawn pawn)
        {
            if (mech.Faction != pawn.Faction)
            {
                mech.SetFaction(pawn.Faction);
            }

            mech.GetOverseer()?.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, mech);
            pawn.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
            pawn.mechanitor.Notify_BandwidthChanged();
        }

        private void Highlight(LocalTargetInfo target)
        {
            if (target.IsValid)
            {
                GenDraw.DrawTargetHighlight(target);
            }
        }

        public override void Notify_Downed()
        {
            base.Notify_Downed();
            dummyPawn?.mechanitor?.UndraftAllMechs();
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref dummyPawn, "DMSRC_dummyPawn");
        }
    }
}