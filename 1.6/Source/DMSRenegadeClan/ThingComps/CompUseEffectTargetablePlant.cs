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
using static HarmonyLib.Code;
using static System.Net.Mime.MediaTypeNames;
using static UnityEngine.GraphicsBuffer;

namespace DMSRC
{
	public class CompUsableAllowTargetMechs : CompUsable
	{
		public override AcceptanceReport CanBeUsedBy(Pawn p, bool forced = false, bool ignoreReserveAndReachable = false)
		{
			if (p.IsMutant && !Props.allowedMutants.Contains(p.mutant.Def))
			{
				return false;
			}
			PlanetTile tile = p.MapHeld.Tile;
			if (tile.Valid && !Props.layerWhitelist.NullOrEmpty() && !Props.layerWhitelist.Contains(tile.LayerDef))
			{
				return "CannotPerformPlanetLayer".Translate(tile.LayerDef.gerundLabel.Named("GERUND"), tile.LayerDef.label.Named("LAYER")).Resolve();
			}
			if (tile.Valid && !Props.layerBlacklist.NullOrEmpty() && Props.layerBlacklist.Contains(tile.LayerDef))
			{
				return "CannotPerformPlanetLayer".Translate(tile.LayerDef.gerundLabel.Named("GERUND"), tile.LayerDef.label.Named("LAYER")).Resolve();
			}
			if (parent.TryGetComp<CompPowerTrader>(out var comp) && !comp.PowerOn)
			{
				return "NoPower".Translate();
			}
			if (!ignoreReserveAndReachable && !p.CanReach(parent, PathEndMode.Touch, Danger.Deadly))
			{
				return "NoPath".Translate();
			}
			if (!ignoreReserveAndReachable && !p.CanReserve(parent, 1, -1, null, forced))
			{
				Pawn pawn = p.Map.reservationManager.FirstRespectedReserver(parent, p) ?? p.Map.physicalInteractionReservationManager.FirstReserverOf(parent);
				if (pawn != null)
				{
					return "ReservedBy".Translate(pawn.LabelShort, pawn);
				}
				return "Reserved".Translate();
			}
			if (Props.userMustHaveHediff != null && !p.health.hediffSet.HasHediff(Props.userMustHaveHediff))
			{
				return "MustHaveHediff".Translate(Props.userMustHaveHediff);
			}
			List<ThingComp> allComps = parent.AllComps;
			for (int i = 0; i < allComps.Count; i++)
			{
				if (allComps[i] is CompUseEffect compUseEffect)
				{
					AcceptanceReport result = compUseEffect.CanBeUsedBy(p);
					if (!result.Accepted)
					{
						return result;
					}
				}
			}
			return true;
		}
	}
	public class CompProperties_UseEffectTargetablePlant : CompProperties_UseEffect
	{
		public ThingDef plantDef;

		public JobDef jobDef;

		public IntRange timerRange = new IntRange();

		public TargetingParameters targetingParameters = new TargetingParameters
		{
			canTargetPawns = false,
			canTargetBuildings = false,
			canTargetAnimals = false,
			canTargetHumans = false,
			canTargetMechs = false,
			canTargetLocations = true
		};

		public CompProperties_UseEffectTargetablePlant()
		{
			compClass = typeof(CompUseEffectTargetablePlant);
		}
	}
	public class CompUseEffectTargetablePlant : CompUseEffect, ITargetingSource
	{
		private IntVec3 selectedTarget;

		private Pawn caster;

		public float hoursCooldownSelected;

		public CompProperties_UseEffectTargetablePlant Props => (CompProperties_UseEffectTargetablePlant)props;

		public bool CasterIsPawn => true;

		public bool IsMeleeAttack => false;

		public bool Targetable => true;

		public bool MultiSelect => false;

		public bool HidePawnTooltips => false;

		public Thing Caster => parent;

		public Pawn CasterPawn => caster;

		public Verb GetVerb => null;

		public TargetingParameters targetParams => Props.targetingParameters;

		public virtual ITargetingSource DestinationSelector => null;

		public Texture2D UIIcon => null;

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref selectedTarget, "selectedTarget");
			Scribe_Values.Look(ref hoursCooldownSelected, "hoursCooldownSelected");
		}

		public override bool SelectedUseOption(Pawn p)
		{
			caster = p;
			Find.Targeter.BeginTargeting(this, null, allowNonSelectedTargetingSource: true);
			return true;
		}

		public override void DoEffect(Pawn usedBy)
		{
			if (!selectedTarget.IsValid || !ValidateTarget(new LocalTargetInfo(selectedTarget)))
			{
				return;
			}
			base.DoEffect(usedBy);
			Job job = JobMaker.MakeJob(Props.jobDef, new IntVec3(selectedTarget.x, 0, selectedTarget.z), parent);
			job.count = 1;
			job.playerForced = true;
			usedBy.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			selectedTarget = IntVec3.Invalid;
		}

		public bool CanHitTarget(LocalTargetInfo target)
		{
			return ValidateTarget(target, showMessages: false);
		}

		public void DrawHighlight(LocalTargetInfo target)
		{
			if (target.IsValid)
			{
				GenDraw.DrawTargetHighlight(target);
			}
		}

		public void OrderForceTarget(LocalTargetInfo target)
		{
			selectedTarget = target.Cell;
			if (parent.TryGetComp<CompUsable>(out var comp))
			{
				comp.TryStartUseJob(caster, target, comp.Props.ignoreOtherReservations);
			}
			caster = null;
		}

		public void OnGUI(LocalTargetInfo target)
		{
			Widgets.MouseAttachedLabel("TargetGizmoMouse".Translate());
			if (ValidateTarget(target, showMessages: false))
			{
				GenUI.DrawMouseAttachment(TexCommand.Attack);
			}
			else
			{
				GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
			}
		}

		public virtual bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
		{
			if (!target.Cell.IsValid)
			{
				return false;
			}
			IntVec3 cell = target.Cell;
			Map map = Find.CurrentMap;
			if (!cell.Standable(map))
			{
				return false;
			}
			if (cell.GetFirstBuilding(map) != null)
			{
				return false;
			}
			if (!cell.GetAffordances(map).Contains(TerrainAffordanceDefOf.Light))
			{
				return false;
			}
			return true;
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo item in base.CompGetGizmosExtra())
			{
				yield return item;
			}
			Command_Action command_Action = new Command_Action();
			command_Action.action = delegate
			{
				hoursCooldownSelected = Mathf.Max(hoursCooldownSelected - 0.1f, Props.timerRange.min);
			};
			command_Action.defaultLabel = "DMSRC_DecreaseTimer".Translate(hoursCooldownSelected);
			command_Action.defaultDesc = "DMSRC_DecreaseTimer".Translate(hoursCooldownSelected);
			command_Action.hotKey = KeyBindingDefOf.Misc5;
			command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/TempLower");
			yield return command_Action;
			Command_Action command_Action2 = new Command_Action();
			command_Action2.action = delegate
			{
				hoursCooldownSelected = Mathf.Min(hoursCooldownSelected + 0.1f, Props.timerRange.min);
			};
			command_Action2.defaultLabel = "DMSRC_IncreaseTimer".Translate(hoursCooldownSelected);
			command_Action2.defaultDesc = "DMSRC_IncreaseTimer".Translate(hoursCooldownSelected);
			command_Action2.hotKey = KeyBindingDefOf.Misc4;
			command_Action2.icon = ContentFinder<Texture2D>.Get("UI/Commands/TempLower");
			yield return command_Action2;
		}
	}
}