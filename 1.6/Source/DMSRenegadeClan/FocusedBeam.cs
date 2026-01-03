using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using HarmonyLib;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using UnityEngine;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DMSRC
{
	public class CompProperties_FocusedBeam : CompProperties
	{
		public float explosionRange = 0.3f;

		public int explosionDamage = 20;

		public DamageDef damageDef;

		public ThingDef beamMoteDef;

		public CompProperties_FocusedBeam()
		{
			compClass = typeof(CompFocusedBeam);
		}
	}
	public class CompFocusedBeam : ThingComp
	{
		public CompProperties_FocusedBeam Props => (CompProperties_FocusedBeam)props;
	}

	public class Verb_FocusedBeam : Verb_ShootBeam
	{
		private MoteDualAttached mote;

		private Thing target;

		private List<Vector3> tmpPath = new List<Vector3>();

		private HashSet<IntVec3> tmpPathCells = new HashSet<IntVec3>();

		private HashSet<IntVec3> tmpHighlightCells = new HashSet<IntVec3>();

		private HashSet<IntVec3> tmpSecondaryHighlightCells = new HashSet<IntVec3>();

		public static Color secondaryHighliteColor = new Color(0.56f, 0.44f, 0.65f);

		public static MethodInfo calculatePath = AccessTools.Method(typeof(Verb_ShootBeam), "CalculatePath", new Type[4] { typeof(Vector3), typeof(List<Vector3>) , typeof(HashSet<IntVec3>), typeof(bool) }, (Type[])null);

		public static FieldInfo ticksToNextPathStep = AccessTools.Field(typeof(Verb_ShootBeam), "ticksToNextPathStep");

		private CompFocusedBeam comp = null;

		public CompFocusedBeam Comp
        {
            get
            {
				if(comp == null)
                {
					comp = EquipmentSource.TryGetComp<CompFocusedBeam>();
				}
				return comp;
            }
        }

		public override void DrawHighlight(LocalTargetInfo target)
		{
			tmpHighlightCells.Clear();
			tmpSecondaryHighlightCells.Clear();
			verbProps.DrawRadiusRing(caster.Position, this);
			if (target.IsValid)
			{
				GenDraw.DrawTargetHighlight(target);
				DrawHighlightFieldRadiusAroundTarget(target);
			}
			CellRect map = CellRect.WholeMap(Caster.Map);
			calculatePath.Invoke(this, new object[4] { target.CenterVector3, tmpPath, tmpPathCells, false });
			foreach (IntVec3 tmpPathCell in tmpPathCells)
			{
				if (!TryGetHitCell(tmpPathCell, out var hitCell))
				{
					continue;
				}
				tmpHighlightCells.Add(hitCell);
				foreach (IntVec3 beamHitNeighbourCell in CellRect.FromCell(hitCell).ExpandedBy(1).Cells)
				{
					if (!tmpSecondaryHighlightCells.Contains(beamHitNeighbourCell) && map.Contains(beamHitNeighbourCell))
					{
						tmpSecondaryHighlightCells.Add(beamHitNeighbourCell);
					}
				}
			}
			if (tmpHighlightCells.Any())
			{
				GenDraw.DrawFieldEdges(tmpHighlightCells.ToList(), verbProps.highlightColor ?? Color.white);
			}
			if (tmpSecondaryHighlightCells.Any())
			{
				GenDraw.DrawFieldEdges(tmpSecondaryHighlightCells.ToList(), secondaryHighliteColor);
			}
		}

		protected bool TryGetHitCell(IntVec3 targetCell, out IntVec3 hitCell)
		{
			IntVec3 root = Caster.Position;
			LocalTargetInfo targetInfo = currentTarget.Cell == targetCell ? currentTarget : targetCell;
			if (/*TryFindShootLineFromTo(root, targetInfo, out var line) && */CanHitTarget(targetInfo))
			{
				hitCell = targetCell;
				return true;
			}
			IntVec3 intVec = GenSight.LastPointOnLineOfSight(root, targetCell, (IntVec3 c) => root.DistanceTo(c) <= verbProps.minRange || (c.InBounds(caster.Map) && c.CanBeSeenOverFast(caster.Map)), skipFirstCell: true);
			hitCell = (intVec.IsValid ? intVec : targetCell);
			return true;
		}

		protected override bool TryCastShot()
		{
			lastShotTick = Find.TickManager.TicksGame;
			ticksToNextPathStep.SetValue(this, TicksBetweenBurstShots);
			if (base.EquipmentSource != null)
			{
				base.EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
				base.EquipmentSource.GetComp<CompApparelReloadable>()?.UsedOnce();
			}
			IntVec3 intVec = InterpolatedPosition.Yto0().ToIntVec3();
			if (TryGetHitCell(intVec, out var hitCell))
			{
				HitCell(hitCell);
				return true;
			}
			return false;
		}
		public override void WarmupComplete()
		{
			base.WarmupComplete();
			target = null;
			if (base.currentTarget.HasThing && !base.currentTarget.Thing.DestroyedOrNull())
            {
				target = base.currentTarget.Thing;
				currentTarget = target.PositionHeld;
				currentDestination = LocalTargetInfo.Invalid;
			}
			mote = MoteMaker.MakeInteractionOverlay(Comp.Props.beamMoteDef, caster, new TargetInfo(base.InterpolatedPosition.ToIntVec3(), caster.Map));
		}

		private ShootLine resultingLine;
		public override void BurstingTick()
		{
			IntVec3 root = Caster.Position;
			if (target != null && !target.Position.IsValid && target.Map == Caster.Map)
			{
				currentTarget = target.Position;
			}
			base.BurstingTick();
			TryFindShootLineFromTo(root, currentTarget, out resultingLine);
			Vector3 vector = InterpolatedPosition;
			IntVec3 intVec = vector.ToIntVec3();
			Vector3 vector2 = InterpolatedPosition - root.ToVector3Shifted();
			float num = vector2.MagnitudeHorizontal();
			Vector3 normalized = vector2.Yto0().normalized;
			IntVec3 intVec2 = GenSight.LastPointOnLineOfSight(resultingLine.Source, intVec, (IntVec3 c) => root.DistanceTo(c) <= verbProps.minRange || (c.InBounds(caster.Map) && c.WalkableByAny(caster.Map) && !c.Impassable(caster.Map) && c.CanBeSeenOverFast(caster.Map)), skipFirstCell: true);
			if (intVec2.IsValid)
			{
				num -= (intVec - intVec2).LengthHorizontal;
				vector = root.ToVector3Shifted() + normalized * num;
				intVec = vector.ToIntVec3();
			}
			Vector3 offsetA = normalized * verbProps.beamStartOffset;
			Vector3 vector3 = vector - intVec.ToVector3Shifted();
			if (mote != null)
			{
				mote.UpdateTargets(new TargetInfo(root, caster.Map), new TargetInfo(intVec, caster.Map), offsetA, vector3);
				mote.Maintain();
			}
		}

		private void HitCell(IntVec3 cell)
		{
			if (cell.InBounds(caster.Map))
			{
				float explosionRange = Comp?.Props?.explosionRange ?? 2.3f;
				int damage = Comp?.Props.explosionDamage ?? 10;
				if (cell.DistanceTo(caster.Position) < explosionRange)
				{
					explosionRange = 0.1f;
					damage *= 2;
				}
				GenExplosion.DoExplosion(cell, Caster.MapHeld, explosionRange, Comp?.Props.damageDef, Caster, damage, 999f, damageFalloff: true, screenShakeFactor: 0f, weapon: this.EquipmentSource?.def, ignoredThings: new List<Thing>() { Caster }, doSoundEffects: false);
			}
		}

		public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
		{
			if (WarmingUp)
			{
				verbProps.requireLineOfSight = false;
				return base.CanHitTargetFrom(root, targ);
			}
			bool b = false;
			try
			{
				verbProps.requireLineOfSight = true;
				b = base.CanHitTargetFrom(root, targ);
				
			}
			catch (Exception ex)
			{
				Log.Error("Could not instantiate Verb (directOwner): " + ex);
			}
			finally
			{
				verbProps.requireLineOfSight = false;
			}
			return b;
		}
	}
}
