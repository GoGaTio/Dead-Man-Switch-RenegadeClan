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

		protected override bool TryCastShot()
		{
			IntVec3 intVec = InterpolatedPosition.Yto0().ToIntVec3();
			IntVec3 intVec2 = GenSight.LastPointOnLineOfSight(resultingLine.Source, intVec, (IntVec3 c) => c.InBounds(caster.Map) && c.WalkableByAny(caster.Map) && !c.Impassable(caster.Map) && c.CanBeSeenOverFast(caster.Map), skipFirstCell: true);
			IntVec3 intVec3 = (intVec2.IsValid ? intVec2 : intVec);
			HitCell(intVec3);
			return base.TryCastShot();
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
			if (target != null && !target.Position.IsValid && target.Map == Caster.Map)
			{
				currentTarget = target.Position;
			}
			base.BurstingTick();
			TryFindShootLineFromTo(caster.Position, currentTarget, out resultingLine);
			Vector3 vector = InterpolatedPosition;
			IntVec3 intVec = vector.ToIntVec3();
			Vector3 vector2 = InterpolatedPosition - caster.Position.ToVector3Shifted();
			float num = vector2.MagnitudeHorizontal();
			Vector3 normalized = vector2.Yto0().normalized;
			IntVec3 intVec2 = GenSight.LastPointOnLineOfSight(resultingLine.Source, intVec, (IntVec3 c) => c.InBounds(caster.Map) && !c.Impassable(caster.Map) && c.CanBeSeenOverFast(caster.Map), skipFirstCell: true);
			if (intVec2.IsValid)
			{
				num -= (intVec - intVec2).LengthHorizontal;
				vector = caster.Position.ToVector3Shifted() + normalized * num;
				intVec = vector.ToIntVec3();
			}
			Vector3 offsetA = normalized * verbProps.beamStartOffset;
			Vector3 vector3 = vector - intVec.ToVector3Shifted();
			if (mote != null)
			{
				mote.UpdateTargets(new TargetInfo(caster.Position, caster.Map), new TargetInfo(intVec, caster.Map), offsetA, vector3);
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
			if (WarmingUp || Bursting)
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
