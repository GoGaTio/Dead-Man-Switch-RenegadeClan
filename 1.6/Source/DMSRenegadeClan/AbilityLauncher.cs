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
	public class Verb_ControlMech : Verb_CastAbility
	{
		public override float WarmupTime => base.WarmupTime * WarmupTimeFactor;

		public float WarmupTimeFactor
		{
			get
			{
				LocalTargetInfo target = base.CurrentTarget;
				if(target != null && target.Pawn != null)
				{
					return target.Pawn.BodySize;
				}
				return 1f;
			}
		}
	}

	public class CompProperties_InterceptControl : CompProperties_AbilityEffect
	{
		public CompProperties_InterceptControl()
		{
			compClass = typeof(CompAbilityEffect_InterceptControl);
		}
	}
	public class CompAbilityEffect_InterceptControl : CompAbilityEffect
	{
		public new CompProperties_InterceptControl Props => (CompProperties_InterceptControl)props;

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
			Pawn pawn = target.Pawn;
			if (pawn == null)
			{
				return;
			}
			pawn.GetOverseer()?.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, pawn);
			pawn.SetFaction(parent.pawn.Faction);
			if (MechanitorUtility.IsMechanitor(parent.pawn))
			{
				parent.pawn.relations.AddDirectRelation(PawnRelationDefOf.Overseer, pawn);
			}
		}

		public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
		{
			return Valid(target);
		}

		public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
		{
			Pawn pawn = target.Pawn;
			if (pawn == null)
			{
				return false;
			}
			if (!pawn.RaceProps.IsMechanoid || pawn.Faction == parent.pawn.Faction || pawn.kindDef.isBoss)
			{
				return false;
			}
			if (pawn.Faction == Faction.OfMechanoids && !Faction.OfMechanoids.deactivated)
			{
				if (throwMessages)
				{
					Messages.Message("DMSRC_MessageCantInterceptMechhive".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
				}
				return false;
			}
			return true;
		}
	}

	public class CompProperties_NeuroControl : CompProperties_AbilityEffect
	{
		public CompProperties_NeuroControl()
		{
			compClass = typeof(CompAbilityEffect_NeuroControl);
		}
	}
	public class CompAbilityEffect_NeuroControl : CompAbilityEffect
	{
		public new CompProperties_NeuroControl Props => (CompProperties_NeuroControl)props;

		public Hediff_NeuroControlChip Chip => parent.pawn.health.hediffSet.GetFirstHediff<Hediff_NeuroControlChip>();

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
			Pawn pawn = target.Pawn;
			if(pawn == null)
			{
				return;
			}
			Hediff_NeuroControlChip chip = Chip;
			if(chip == null)
			{
				return;
			}
			if (chip.controlledPawns.Contains(pawn))
			{
				chip.RemovePawn(pawn);
				return;
			}
			if(chip.Capacity <= chip.controlledPawns.Count)
			{
				chip.RemovePawn(chip.controlledPawns.First());
			}
			chip.ControlPawn(pawn);
		}
	}
	public class CompProperties_AbilityProjectileWithMissRadius : CompProperties_AbilityEffect
	{
		public ThingDef projectileDef;

		public float forcedMissRadius;

		public float previewRadius = -1f;

		public bool preventLaunchUnderRoof;

		public CompProperties_AbilityProjectileWithMissRadius()
		{
			compClass = typeof(CompAbilityEffect_ProjectileWithMissRadius);
		}
	}
	public class CompAbilityEffect_ProjectileWithMissRadius : CompAbilityEffect
	{
		public new CompProperties_AbilityProjectileWithMissRadius Props => (CompProperties_AbilityProjectileWithMissRadius)props;

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
			LaunchProjectile(target);
		}
		public override void DrawEffectPreview(LocalTargetInfo target)
		{
			base.DrawEffectPreview(target);
			if (ShouldSkipCell(target))
			{
				return;
			}
			if (Props.previewRadius > 0)
			{
				GenDraw.DrawRadiusRing(target.Cell, Props.previewRadius, Color.white);
			}
			else
			{
				GenDraw.DrawRadiusRing(target.Cell, Props.projectileDef.projectile.explosionRadius + Props.forcedMissRadius, Color.white);
			}
		}
		private void LaunchProjectile(LocalTargetInfo target)
		{
			Pawn pawn = parent.pawn;
			int num = Rand.Range(0, GenRadial.NumCellsInRadius(Props.forcedMissRadius));
			IntVec3 c = target.Cell + GenRadial.RadialPattern[num];
			((Projectile)GenSpawn.Spawn(Props.projectileDef, pawn.Position, pawn.Map)).Launch(pawn, pawn.DrawPos, c, c, ProjectileHitFlags.All);
		}

		public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
		{
			return base.Valid(target, throwMessages) && !ShouldSkipCell(target);
		}
		public override bool AICanTargetNow(LocalTargetInfo target)
		{
			return target.Pawn != null && !ShouldSkipCell(target);
		}

		public override bool GizmoDisabled(out string reason)
		{
			if (base.GizmoDisabled(out reason))
			{
				return true;
			}
			if (Props.preventLaunchUnderRoof && parent.pawn.Map.roofGrid.RoofAt(parent.pawn.Position) != null)
			{
				reason = "DMSRC_CannotLaunchUnderRoof".Translate();
				return true;
			}
			return false;
		}

		private bool ShouldSkipCell(LocalTargetInfo target)
		{
			if (Props.projectileDef.projectile.flyOverhead)
			{
				IntVec3 cell = target.Pawn?.Position ?? target.Cell;
				return parent.pawn.Map.roofGrid.RoofAt(cell)?.isThickRoof == true;
			}
			return false;
		}
	}

	public class CompProperties_EmergencyBattery : CompProperties_AbilityEffect
	{
		public float amount;

		public CompProperties_EmergencyBattery()
		{
			compClass = typeof(CompAbilityEffect_EmergencyBattery);
		}
	}
	public class CompAbilityEffect_EmergencyBattery : CompAbilityEffect
	{
		public new CompProperties_EmergencyBattery Props => (CompProperties_EmergencyBattery)props;

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
			if(parent.pawn.needs?.energy != null)
			{
				parent.pawn.needs.energy.CurLevel += Props.amount;
			}
		}
		public override bool ShouldHideGizmo => base.ShouldHideGizmo || parent.pawn.needs?.energy?.IsLowEnergySelfShutdown != true;

		public override bool CanCast => base.CanCast && parent.pawn.needs?.energy?.IsLowEnergySelfShutdown == true;
	}
}
