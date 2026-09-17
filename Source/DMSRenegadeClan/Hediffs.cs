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
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
using Verse.Grammar;
using Verse.Sound;
using static HarmonyLib.Code;
using static RimWorld.MechClusterSketch;
using static System.Net.Mime.MediaTypeNames;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
using static UnityEngine.Scripting.GarbageCollector;

namespace DMSRC
{
	/*public abstract class PawnRenderNodeWorker_Overlay : PawnRenderNodeWorker
	{
		protected abstract PawnOverlayDrawer OverlayDrawer(Pawn pawn);

		public override bool ShouldListOnGraph(PawnRenderNode node, PawnDrawParms parms)
		{
			return false;
		}

		public override void AppendDrawRequests(PawnRenderNode node, PawnDrawParms parms, List<PawnGraphicDrawRequest> requests)
		{
			Mesh mesh = node.GetMesh(parms);
			if (mesh != null)
			{
				requests.Add(new PawnGraphicDrawRequest(node, mesh));
			}
		}

		public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
		{
			return base.CanDrawNow(node, parms);
		}

		public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
		{
			return base.OffsetFor(node, parms, out pivot);
		}

		public override void PostDraw(PawnRenderNode node, PawnDrawParms parms, Mesh mesh, Matrix4x4 matrix)
		{
			OverlayDrawer(parms.pawn)?.RenderPawnOverlay(matrix, mesh, node.Props.overlayLayer, parms, node.Props.overlayOverApparel);
		}
	}

	public class Hediff_Needle : HediffWithComps
	{
		public void GetAnchor(Hediff injury, Rot4 pawnRot, Mesh bodyMesh)
		{
			if (pawn.story?.bodyType?.woundAnchors != null)
			{
				List<BodyTypeDef.WoundAnchor> tmpAnchors = PawnDrawUtility.FindAnchors(pawn, injury.Part).ToList();
				if (tmpAnchors.Count > 0)
				{
					for (int num = tmpAnchors.Count - 1; num >= 0; num--)
					{
						if (tmpAnchors[num].layer != layer || !PawnDrawUtility.AnchorUsable(pawn, tmpAnchors[num], pawnRot))
						{
							tmpAnchors.RemoveAt(num);
						}
					}
					if (tmpAnchors.Count == 0)
					{
						continue;
					}
					BodyTypeDef.WoundAnchor woundAnchor = tmpAnchors.RandomElement();
					PawnDrawUtility.CalcAnchorData(pawn, woundAnchor, pawnRot, out anchorOffset, out range);
					range = hediff.def.woundAnchorRange ?? range;
					text = woundAnchor.tag;
				}
				tmpAnchors.Clear();
			}
			else
			{
				GetDefaultAnchor(bodyMesh, out anchorOffset, out range);
			}
			FleshTypeDef.ResolvedWound resolvedWound = pawn.RaceProps.FleshType.ChooseWoundOverlay(injury);
			Vector3 vector = resolvedWound.wound.drawOffsetSouth;
			if (pawnRot.IsHorizontal)
			{
				vector = resolvedWound.wound.drawOffsetEastWest.ScaledBy((pawnRot == Rot4.East) ? Vector3.one : VecOneFlipped);
			}
			Vector3 vector2 = bodyMesh.bounds.center + vector;
			bool flip;
			Material material = resolvedWound.GetMaterial(pawnRot, out flip);
			if (resolvedWound.wound.flipOnWoundAnchorTag != null && resolvedWound.wound.flipOnWoundAnchorTag == text && resolvedWound.wound.flipOnRotation == pawnRot)
			{
				flip = !flip;
			}
			if (!pawn.def.race.Humanlike)
			{
				Vector3 vector3 = Rand.InsideUnitCircleVec3 * (Mathf.Min(bodyMesh.bounds.extents.x, bodyMesh.bounds.extents.z) / 2f);
				if (flip)
				{
					vector3.x *= -1f;
				}
				vector2 += vector3;
			}
		}
	}*/

	public class HediffCompProperties_NeuroControl : HediffCompProperties
	{
		public ThingDef customMote;
		public HediffCompProperties_NeuroControl()
		{
			compClass = typeof(HediffComp_NeuroControl);
		}
	}
	public class HediffComp_NeuroControl : HediffComp
	{
		public Pawn controller;

		private MoteDualAttached mote;

		public HediffCompProperties_NeuroControl Props => props as HediffCompProperties_NeuroControl;

		public override string CompLabelInBracketsExtra => controller?.Name?.ToStringShort ?? controller?.LabelCap ?? null;

		public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
		{
			base.Notify_PawnDied(dinfo, culprit);
			Hediff_NeuroControlChip h = controller.health.hediffSet.GetFirstHediff<Hediff_NeuroControlChip>();
			if(h != null)
			{
				h.RemovePawn(parent.pawn);
			}
			base.Pawn.health.RemoveHediff(parent);
		}

		public override void CompPostPostRemoved()
		{
			Hediff_NeuroControlChip h = controller.health.hediffSet.GetFirstHediff<Hediff_NeuroControlChip>();
			if (h != null)
			{
				h.RemovePawn(parent.pawn);
			}
			base.CompPostPostRemoved();
		}

		public override void CompPostTick(ref float severityAdjustment)
		{
			if (controller.MapHeld == parent.pawn.MapHeld)
			{
				ThingDef moteDef = Props.customMote ?? ThingDefOf.Mote_PsychicLinkLine;
				if (mote == null)
				{
					mote = MoteMaker.MakeInteractionOverlay(moteDef, parent.pawn, controller);
				}
				mote.Maintain();
			}
		}

		public override void CompExposeData()
		{
			base.CompExposeData();
			Scribe_References.Look(ref controller, "DMSRC_controller", saveDestroyedThings: true);
		}
	}
	public class Hediff_NeuroChip : Hediff_Level
	{
		public int disabledLevels = 0;
		public override HediffStage CurStage => disabledLevels > 0 ? def.stages[CurStageIndex - disabledLevels] : base.CurStage;

		public int Level => level - disabledLevels;

		public override string Label
		{
			get
			{
				if (!def.levelIsQuantity)
				{
					return def.label + " (" + "LevelNum".Translate(Level).ToString() + ")";
				}
				return def.label + " x" + Level;
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref disabledLevels, "disabledLevels");
		}

		public static void Recalculate(Pawn p)
		{
			if (p.health.hediffSet.GetFirstHediff<Hediff_Neurointerface>() is Hediff_Neurointerface inter && inter != null)
			{
				int usedCapacity = inter.UsedCapacity;
				int capacity = inter.Capacity;
				foreach (Hediff h in p.health.hediffSet.hediffs)
				{
					if (h is Hediff_NeuroChip chip && chip.disabledLevels > 0 && capacity - usedCapacity > 0)
					{
						if (capacity - usedCapacity > chip.disabledLevels)
						{
							usedCapacity += chip.disabledLevels;
							chip.disabledLevels = 0;
						}
						else
						{
							chip.disabledLevels -= capacity - usedCapacity;
							break;
						}
					}
				}
			}
		}

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			return base.SpecialDisplayStats(req);
		}
	}

	public class Hediff_NeuroControlChip : Hediff_NeuroChip
	{
		public override string Label
		{
			get
			{
				string s = base.Label;
				return s + " (" + "DMSRC_Controlled".Translate(controlledPawns.Count, Capacity) + ")";
			}
		}
		public int Capacity => Mathf.RoundToInt(pawn.GetStatValue(RCDefOf.DMSRC_NeuroControlPower));

		public List<Pawn> controlledPawns = new List<Pawn>();

		public void ControlPawn(Pawn pawn)
		{
			controlledPawns.Add(pawn);
			Hediff h = pawn.health.AddHediff(RCDefOf.DMSRC_NeuroControl, pawn.health.hediffSet.GetBrain());
			if (h.TryGetComp<HediffComp_NeuroControl>(out var comp))
			{
				comp.controller = this.pawn;
			}
		}

		public void RemovePawn(Pawn pawn)
		{
			if(pawn.health.hediffSet.TryGetHediff(RCDefOf.DMSRC_NeuroControl, out var h))
			{
				pawn.health.RemoveHediff(h);
			}
			controlledPawns.Remove(pawn);
		}

		public void RemoveAll()
		{
			foreach(Pawn p in controlledPawns.ToList())
			{
				RemovePawn(p);
			}
		}

		public override void Notify_PawnKilled()
		{
			RemoveAll();
			base.Notify_PawnKilled();
		}

		public override void Notify_Downed()
		{
			RemoveAll();
			base.Notify_Downed();
		}

		public override void PreRemoved()
		{
			RemoveAll();
			base.PreRemoved();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref controlledPawns, "DMSRC_controlledPawns", LookMode.Reference);
			if(Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				controlledPawns.RemoveAll((p) => p == null || p.Destroyed || p.Dead);
			}
		}
	}

	public class Hediff_Neurointerface : Hediff_Implant
	{
		public int Capacity => Mathf.RoundToInt(pawn.GetStatValue(RCDefOf.DMSRC_Neurocapacity));

		public int UsedCapacity
        {
            get
            {
				int num = 0;
				foreach(Hediff item in pawn.health.hediffSet.hediffs)
                {
					if(item is Hediff_NeuroChip chip)
                    {
						num += chip.Level;
                    }
                }
				return num;
            }
        }
        public override string LabelInBrackets => UsedCapacity + "/" + Capacity;
        public override void PostAdd(DamageInfo? dinfo)
		{
			base.PostAdd(dinfo);
			if (base.Part == null)
			{
				Log.Error(def.defName + " has null Part. It should be set before PostAdd.");
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			if (Scribe.mode == LoadSaveMode.PostLoadInit && base.Part == null)
			{
				Log.Error(GetType().Name + " has null part after loading.");
				pawn.health.hediffSet.hediffs.Remove(this);
			}
		}

		public override void PreRemoved()
		{
			base.PreRemoved();
			Hediff_ProcessorHelmet hediff = pawn.health.hediffSet.GetFirstHediff<Hediff_ProcessorHelmet>();
			if(hediff != null)
			{
				hediff.activeInt = null;
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
				if (activeInt == null)
				{
					activeInt = pawn?.health?.hediffSet?.HasHediff<Hediff_Neurointerface>();
				}
				return activeInt ?? false;
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
			if (wornApparel != null && wornApparel.TryGetComp<CompProcessorHelmet>(out var comp))
			{
				Hediff_Neurointerface hediff = pawn?.health?.hediffSet?.GetFirstHediff<Hediff_Neurointerface>();
				if (hediff == null)
				{
					return;
				}
				int count = hediff.UsedCapacity - (hediff.Capacity - Mathf.RoundToInt(CurStage.statOffsets.GetStatOffsetFromList(RCDefOf.DMSRC_Neurocapacity)));
				if (count > 0)
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