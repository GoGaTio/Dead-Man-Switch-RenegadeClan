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
	public class DamageWorker_BeamExplosion : DamageWorker_AddInjury
	{
		protected override void ExplosionDamageThing(Explosion explosion, Thing t, List<Thing> damagedThings, List<Thing> ignoredThings, IntVec3 cell)
		{
			if (explosion.intendedTarget != t && t.Faction != null && t.Faction == explosion.instigator?.Faction)
			{
				return;
			}
			base.ExplosionDamageThing(explosion, t, damagedThings, ignoredThings, cell);
		}
		public override DamageResult Apply(DamageInfo dinfo, Thing victim)
		{
			Pawn pawn = victim as Pawn;
			if (pawn != null && pawn.Faction == Faction.OfPlayer)
			{
				Find.TickManager.slower.SignalForceNormalSpeedShort();
				if (pawn.RaceProps.IsFlesh)
				{
					Hediff hediff = pawn.health.GetOrAddHediff(RCDefOf.DMSRC_BeamIllness);
					hediff.Severity += 0.005f * dinfo.Amount;
				}
			}
			Map map = victim.Map;
			DamageResult damageResult = base.Apply(dinfo, victim);
			if (map == null)
			{
				return damageResult;
			}
			if (!damageResult.deflected && !dinfo.InstantPermanentInjury && FireUtility.ChanceToAttachFireFromEvent(victim) > 0f)
			{
				victim.TryAttachFire(Rand.Range(0.35f, 0.65f), dinfo.Instigator);
			}
			if (victim.Destroyed && pawn == null)
			{
				foreach (IntVec3 item in victim.OccupiedRect())
				{
					FilthMaker.TryMakeFilth(item, map, ThingDefOf.Filth_Ash);
				}
			}
			return damageResult;
		}

		public override void ExplosionAffectCell(Explosion explosion, IntVec3 c, List<Thing> damagedThings, List<Thing> ignoredThings, bool canThrowMotes)
		{
			base.ExplosionAffectCell(explosion, c, damagedThings, ignoredThings, canThrowMotes);
			if (Rand.Chance(FireUtility.ChanceToStartFireIn(c, explosion.Map)))
			{
				FireUtility.TryStartFireIn(c, explosion.Map, Rand.Range(0.4f, 0.9f), explosion.instigator);
			}
		}
	}

	public class DamageWorker_Firecracker : DamageWorker_AddInjury
	{
		protected override void ExplosionDamageThing(Explosion explosion, Thing t, List<Thing> damagedThings, List<Thing> ignoredThings, IntVec3 cell)
		{
			if(explosion.intendedTarget != t && t.Faction != null && t.Faction == explosion.instigator?.Faction)
			{
				return;
			}
			base.ExplosionDamageThing(explosion, t, damagedThings, ignoredThings, cell);
		}
		public override DamageResult Apply(DamageInfo dinfo, Thing victim)
		{
			Pawn pawn = victim as Pawn;
			if (pawn != null && pawn.Faction == Faction.OfPlayer)
			{
				Find.TickManager.slower.SignalForceNormalSpeedShort();
			}
			Map map = victim.Map;
			DamageResult damageResult = base.Apply(dinfo, victim);
			if (map == null)
			{
				return damageResult;
			}
			if (!damageResult.deflected && !dinfo.InstantPermanentInjury && Rand.Chance(FireUtility.ChanceToAttachFireFromEvent(victim) * 2f))
			{
				victim.TryAttachFire(Rand.Range(0.15f, 0.55f), dinfo.Instigator);
			}
			return damageResult;
		}

		public override void ExplosionAffectCell(Explosion explosion, IntVec3 c, List<Thing> damagedThings, List<Thing> ignoredThings, bool canThrowMotes)
		{
			base.ExplosionAffectCell(explosion, c, damagedThings, ignoredThings, canThrowMotes);
			if (Rand.Chance(FireUtility.ChanceToStartFireIn(c, explosion.Map) * 2f))
			{
				FireUtility.TryStartFireIn(c, explosion.Map, Rand.Range(0.2f, 0.6f), explosion.instigator);
			}
		}
	}
}
