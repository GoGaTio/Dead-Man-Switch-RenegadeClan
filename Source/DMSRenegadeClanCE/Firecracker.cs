using CombatExtended;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace DMSRC.CE
{
	public class Firecracker : BulletCE
	{
		private IntVec3? lastPosition;

		private IntVec3? tickPosition;
		public override void Tick()
		{
			if (tickPosition != Position)
			{
				lastPosition = tickPosition;
			}
			tickPosition = Position;
			if (Spawned)
			{
				ThrowSparks(DrawPos);
			}
			base.Tick();
		}

		public void ThrowSparks(Vector3 drawPos)
		{
			if (Position.ShouldSpawnMotesAt(MapHeld))
			{
				for (int i = 0; i < 3; i++)
				{
					FleckCreationData dataStatic = FleckMaker.GetDataStatic(drawPos, MapHeld, RCDefOf.DMSRC_Fleck_SparksFast);
					dataStatic.scale = new FloatRange(0.3f, 0.6f).RandomInRange;
					dataStatic.rotationRate = 0;
					dataStatic.velocityAngle = new FloatRange(0, 360).RandomInRange;
					dataStatic.velocitySpeed = new FloatRange(2, 3).RandomInRange;
					MapHeld.flecks.CreateFleck(dataStatic);
				}
			}
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			tickPosition = Position;
			lastPosition = Position;
		}

		public override void Impact(Thing hitThing)
		{
			Map map = base.Map;
			IntVec3 position = base.Position;
			base.Impact(hitThing);
			if (hitThing?.FireBulwark == true)
			{
				position = lastPosition ?? position;
			}
			GenExplosion.DoExplosion(position, map, def.projectile.explosionRadius, RCDefOf.DMSRC_Firecracker, base.launcher, RCDefOf.DMSRC_Firecracker.defaultDamage, RCDefOf.DMSRC_Firecracker.defaultArmorPenetration, doVisualEffects: false, doSoundEffects: false, projectile: def, weapon: this.equipmentDef, intendedTarget: this.intendedTarget.Thing ?? null);
		}
	}
}
