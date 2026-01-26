using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CoolPsycasts
{
	public class DamageExtension : DefModExtension
    {
        public FloatRange pushBackDistance = new FloatRange(3, 5);
		public bool pull = false;
		public bool sourceIsExplosionCenter = false;
		public bool affectNonPawns = false;
		public float massProrata = 0;
        public SoundDef soundOnDamage;
        public FleckDef fleckOnDamage;
        public float fleckRadius;
        public bool fleckOnInstigator;
    }
    public class DamageWorker_Push : DamageWorker_Blunt
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            if (dinfo.Instigator != null)
            {
                var extension = this.def.GetModExtension<DamageExtension>();
                IntVec3 source = dinfo.Def.isExplosive && extension.sourceIsExplosionCenter ? dinfo.IntendedTarget.Position : dinfo.Instigator.Position;
                float coefficient = extension.massProrata != 0 ? extension.massProrata / thing.GetStatValue(StatDefOf.Mass) : 1f;
                if (extension.affectNonPawns || thing is Pawn) { TryToKnockBack(dinfo.Instigator, thing, extension.pushBackDistance.RandomInRange * coefficient, source, extension.pull, extension); }
            }
            return base.Apply(dinfo, thing);
        }

        private void TryToKnockBack(Thing attacker, Thing thing, float knockBackDistance, IntVec3 source, bool pull, DamageExtension extension)
        {
            Predicate<IntVec3> validator = delegate (IntVec3 x)
            {
                if (x.DistanceTo(thing.Position) < knockBackDistance)
                { return false; }
                if (!x.Walkable(thing.Map) || !GenSight.LineOfSight(thing.Position, x, thing.Map))
                { return false; }
                if (x.GetFirstPawn(thing.Map) != null) 
                { return false; }
                return true;
            };
            var cells = GenRadial.RadialCellsAround(thing.Position, knockBackDistance + 1, true).Where(x => validator(x)).OrderByDescending(x => x.DistanceTo(source)).ToList();
            if (cells.Any())
            {
                var cell = pull ? cells.Last() : cells.First();
                thing.Position = cell;
                if (thing is Pawn victim)
                {
                    victim.pather.StopDead();
                    victim.jobs.StopAll();
                }
                if (extension.fleckOnDamage != null)
                {
                    var target = extension.fleckOnInstigator ? attacker : thing;
                    FleckMaker.Static(target.Position, target.Map, extension.fleckOnDamage, extension.fleckRadius);
                }
            }
        }
    }
}