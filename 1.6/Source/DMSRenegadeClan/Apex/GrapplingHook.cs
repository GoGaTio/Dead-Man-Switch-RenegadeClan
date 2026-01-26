using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Verse;

namespace GhoulProsthetics
{
    [StaticConstructorOnStartup]
    public class Projectile_GrapplingHook : Projectile
    {
        private static readonly Material RopeLineMat = MaterialPool.MatFrom("Things/Chain", ShaderDatabase.Transparent, GenColor.FromBytes(255, 255, 255));
        private int ticksTillPull = -1;
        private bool pullingTarget = false;
        private bool pullingCaster = false;
        private PawnFlyer_Pulled flyer;

        public Vector3 Origin => launcher.Spawned
            ? launcher.DrawPos
            : ThingOwnerUtility.SpawnedParentOrMe(launcher.ParentHolder)?.DrawPos ?? origin;

        public void UpdateDest()
        {
            destination = usedTarget.Cell.ToVector3Shifted();
        }

        protected override void Tick()
        {
            base.Tick();
            UpdateDest();
            if (ticksTillPull <= 0) return;
            ticksTillPull--;
            if (ticksTillPull <= 0) Pull();
        }

        public void Pull()
        {
            ticksTillPull = -2;
            Pawn caster = launcher as Pawn;
            Pawn victim = usedTarget.Thing as Pawn;

            if (caster == null || victim == null || caster.Dead || victim.Dead)
            {
                Destroy();
                return;
            }
            float casterMass = caster.GetStatValue(StatDefOf.Mass) + MassUtility.GearAndInventoryMass(caster);
            float victimMass = victim.GetStatValue(StatDefOf.Mass) + MassUtility.GearAndInventoryMass(victim);

            if (victimMass > casterMass * 2) //caster flies to victim
            {
                var destCell = usedTarget.Thing.OccupiedRect().AdjacentCells.MinBy(cell => cell.DistanceTo(launcher.Position));
                var selected = Find.Selector.IsSelected(caster);
                caster.rotationTracker.FaceTarget(destCell);
                flyer = (PawnFlyer_Pulled)PawnFlyer.MakeFlyer(GhoulProstheticsDefOf.VFEP_GrapplingPawn, caster, destCell, null, null);
                flyer.Hook = this;
                GenSpawn.Spawn(flyer, destCell, Map);
                if (selected)
                    Find.Selector.Select(caster);
                pullingCaster = true;
            }
            else
            {
                var destCell = caster.OccupiedRect().AdjacentCells.MinBy(cell => cell.DistanceTo(victim.Position));
                caster.rotationTracker.FaceTarget(destCell);
                var flyer = (PawnFlyer_Pulled)PawnFlyer.MakeFlyer(GhoulProstheticsDefOf.VFEP_GrapplingPawn, victim, destCell, null, null);
                flyer.Hook = this;
                GenSpawn.Spawn(flyer, destCell, Map);
                pullingTarget = true;
                int ticks = (int)Mathf.Min(flyer.def.pawnFlyer.flightSpeed * flyer.Position.DistanceTo(destCell), flyer.def.pawnFlyer.flightDurationMin);
                caster.stances.stunner.StunFor(ticks, caster);
            }
            
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Vector3 drawHookAt = drawLoc;
            Vector3 ropeStart = launcher.DrawPos;
            Vector3 ropeEnd = drawLoc;
            if (pullingCaster) 
            {
                if (flyer != null) ropeStart = flyer.DrawPos;
            }
            else if (pullingTarget)
            {
                ropeStart = launcher.DrawPos;
                if (flyer != null) ropeEnd = flyer.DrawPos;
                drawHookAt = ropeEnd;
            }
            GenDraw.DrawLineBetween(ropeStart, ropeEnd, AltitudeLayer.PawnRope.AltitudeFor(), RopeLineMat, 0.3f);
            base.DrawAt(drawHookAt, flip);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            GenClamor.DoClamor(this, 12f, ClamorDefOf.Impact);
            ticksTillPull = 10;
            landed = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksTillPull, "ticksTillPull");
        }
    }

    public class PawnFlyer_Pulled : PawnFlyer
    {
        public Projectile_GrapplingHook Hook;

        protected override void RespawnPawn()
        {
            base.RespawnPawn();
            Hook.Destroy();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref Hook, "hook");
        }
    }

    public class PawnFlyerWorker_Pulled : PawnFlyerWorker
    {
        public PawnFlyerWorker_Pulled(PawnFlyerProperties properties) : base(properties)
        {
        }

        public override float GetHeight(float t) => 0f;
    }
}