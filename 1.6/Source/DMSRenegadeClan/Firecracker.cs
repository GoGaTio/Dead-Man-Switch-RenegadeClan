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
	public class Firecracker : Bullet
	{
		private IntVec3? lastPosition;

		private IntVec3? tickPosition;
		protected override void Tick()
        {
            base.Tick();
			if(tickPosition != Position)
            {
				lastPosition = tickPosition;
            }
			tickPosition = Position;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
			tickPosition = Position;
			lastPosition = Position;
		}
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
		{
			Map map = base.Map;
			IntVec3 position = base.Position;
			base.Impact(hitThing, blockedByShield);
			if (hitThing?.FireBulwark == true)
			{
				position = lastPosition ?? position;
			}
			GenExplosion.DoExplosion(position, map, def.projectile.explosionRadius, RCDefOf.DMSRC_Firecracker, base.launcher, RCDefOf.DMSRC_Firecracker.defaultDamage, RCDefOf.DMSRC_Firecracker.defaultArmorPenetration, doVisualEffects: false, doSoundEffects: false, projectile: def, weapon: this.equipmentDef, intendedTarget: this.intendedTarget.Thing ?? null, ignoredThings: launcher ==  null ? null : new List<Thing>() { launcher });
		}
	}
}
