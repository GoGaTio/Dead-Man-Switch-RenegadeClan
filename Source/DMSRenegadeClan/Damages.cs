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
			if (explosion.intendedTarget != t && t.Faction != null && !t.Faction.HostileTo(explosion.instigator?.Faction))
			{
				return;
			}
			base.ExplosionDamageThing(explosion, t, damagedThings, ignoredThings, cell);
		}
		public override DamageResult Apply(DamageInfo dinfo, Thing victim)
		{
			Pawn pawn = victim as Pawn;
			if (pawn != null)
			{
				Find.TickManager.slower.SignalForceNormalSpeedShort();
				if (pawn.RaceProps.IsFlesh)
				{
					Hediff hediff = pawn.health.GetOrAddHediff(RCDefOf.DMSRC_BeamIllness);
					hediff.Severity += 0.003f * dinfo.Amount;
				}
				if (pawn.Faction == Faction.OfPlayer)
				{
					Find.TickManager.slower.SignalForceNormalSpeedShort();
				}
			}
			else if (victim.def.CountAsResource)
			{
				dinfo.SetAmount(dinfo.Amount * 0.1f);
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

	public class DamageWorker_TimedBomb : DamageWorker_AddInjury
	{
		private static List<IntVec3> openCells = new List<IntVec3>();

		private static List<IntVec3> adjWallCells = new List<IntVec3>();

		public override IEnumerable<IntVec3> ExplosionCellsToHit(IntVec3 center, Map map, float radius, IntVec3? needLOSToCell1 = null, IntVec3? needLOSToCell2 = null, FloatRange? affectedAngle = null)
		{
			openCells.Clear();
			adjWallCells.Clear();
			float num = affectedAngle?.min ?? 0f;
			float num2 = affectedAngle?.max ?? 0f;
			int num3 = GenRadial.NumCellsInRadius(radius);
			for (int i = 0; i < num3; i++)
			{
				IntVec3 intVec = center + GenRadial.RadialPattern[i];
				if (!intVec.InBounds(map))
				{
					continue;
				}
				if (affectedAngle.HasValue)
				{
					float lengthHorizontal = (intVec - center).LengthHorizontal;
					float num4 = lengthHorizontal / radius;
					if (!(lengthHorizontal > 0.5f))
					{
						continue;
					}
					float num5 = Mathf.Atan2(-(intVec.z - center.z), intVec.x - center.x) * 57.29578f;
					float num6 = num;
					float num7 = num2;
					if (num5 - num6 < -0.5f * num4 || num5 - num7 > 0.5f * num4)
					{
						continue;
					}
				}
				openCells.Add(intVec);
			}
			/*for (int j = 0; j < openCells.Count; j++)
			{
				IntVec3 intVec2 = openCells[j];
				Building edifice = intVec2.GetEdifice(map);
				if (!intVec2.Walkable(map) || (edifice != null && edifice.def.Fillage == FillCategory.Full && !(edifice is Building_Door { Open: not false })))
				{
					continue;
				}
				for (int k = 0; k < 4; k++)
				{
					IntVec3 intVec3 = intVec2 + GenAdj.CardinalDirections[k];
					if (intVec3.InHorDistOf(center, radius) && intVec3.InBounds(map) && !intVec3.Standable(map) && intVec3.GetEdifice(map) != null && !openCells.Contains(intVec3) && !adjWallCells.Contains(intVec3))
					{
						adjWallCells.Add(intVec3);
					}
				}
			}*/
			return openCells.Concat(adjWallCells);
		}

		public override void ExplosionAffectCell(Explosion explosion, IntVec3 c, List<Thing> damagedThings, List<Thing> ignoredThings, bool canThrowMotes)
		{
			explosion.Map.roofGrid.SetRoof(c, null);
			base.ExplosionAffectCell(explosion, c, damagedThings, ignoredThings, canThrowMotes);
		}
	}

	public class DamageWorker_Firecracker : DamageWorker_AddInjury
	{
		protected override void ExplosionDamageThing(Explosion explosion, Thing t, List<Thing> damagedThings, List<Thing> ignoredThings, IntVec3 cell)
		{
			if(explosion.intendedTarget != t && t.Faction != null && !t.Faction.HostileTo(explosion.instigator?.Faction))
			{
				return;
			}
			base.ExplosionDamageThing(explosion, t, damagedThings, ignoredThings, cell);
		}
		public override DamageResult Apply(DamageInfo dinfo, Thing victim)
		{
			Pawn pawn = victim as Pawn;
			if (pawn != null)
			{
				if (pawn.Faction == Faction.OfPlayer)
				{
					Find.TickManager.slower.SignalForceNormalSpeedShort();
				}
			}
			else if (victim.def.CountAsResource)
			{
				dinfo.SetAmount(dinfo.Amount * 0.1f);
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

	public class DamageWorker_CumulativeBurst : DamageWorker_AddInjury
	{
		protected override void ExplosionDamageThing(Explosion explosion, Thing t, List<Thing> damagedThings, List<Thing> ignoredThings, IntVec3 cell)
		{
			base.ExplosionDamageThing(explosion, t, damagedThings, ignoredThings, cell);
			if (t.Destroyed)
			{
				return;
			}
			if(t is Pawn pawn)
			{
				if(pawn.RaceProps.IsMechanoid || pawn.RaceProps.IsDrone)
				{
					CumulativeEffect(pawn, explosion.instigator, explosion.weapon, explosion.projectile, true);
				}
				else if(pawn.apparel != null)
				{
					Apparel core = pawn.apparel.WornApparel.FirstOrDefault((a) => a.def.thingClass == AccessTools.TypeByName("Exosuit.Exosuit_Core"));
					if(core != null)
					{
						CumulativeEffect(pawn, explosion.instigator, explosion.weapon, explosion.projectile);
					}
				}
			}
		}

		public void CumulativeEffect(Pawn pawn, Thing initiator, ThingDef weaponDef, ThingDef projectileDef, bool onlyInternal = false)
		{
			List<BodyPartRecord> list = new List<BodyPartRecord>();
			List<Hediff> hediffs = new List<Hediff>();
			BattleLogEntry_CumulativeEffect battleLogEntry = null;
			if (pawn != null)
			{
				battleLogEntry = new BattleLogEntry_CumulativeEffect(initiator, pawn, weaponDef, projectileDef, def);
				Find.BattleLog.Add(battleLogEntry);
			}
			for(int i = 0; i < 3; i++)
			{
				BodyPartRecord part = pawn.health.hediffSet.GetRandomNotMissingPart(def, BodyPartHeight.Undefined, onlyInternal ? BodyPartDepth.Inside : BodyPartDepth.Undefined);
				if (list.Contains(part))
				{
					continue;
				}
				if(part != null)
				{
					Hediff hediff = ApplyDamageToPart(pawn, 10f * pawn.BodySize, initiator, part, weaponDef);
					if(hediff == null)
					{
						continue;
					}
					hediffs.Add(hediff);
					list.Add(part);
				}
			}
			if (pawn != null)
			{
				List<bool> recipientPartsDestroyed = null;
				if (!list.NullOrEmpty())
				{
					recipientPartsDestroyed = list.Select((BodyPartRecord part) => pawn.health.hediffSet.GetPartHealth(part) <= 0f).ToList();
				}
				battleLogEntry.FillTargets(list, recipientPartsDestroyed, false);
			}
			if (hediffs != null)
			{
				for (int num = 0; num < hediffs.Count; num++)
				{
					hediffs[num].combatLogEntry = new Verse.WeakReference<LogEntry>(battleLogEntry);
					hediffs[num].combatLogText = battleLogEntry.ToGameStringFromPOV(null);
				}
			}
		}

		protected Hediff ApplyDamageToPart(Pawn pawn, float amount, Thing initiator, BodyPartRecord part, ThingDef weaponDef)
		{
			Pawn pawn2 = initiator as Pawn;
			HediffDef hediffDefFromDamage = HealthUtility.GetHediffDefFromDamage(DamageDefOf.Burn, pawn, part);
			Hediff_Injury hediff_Injury = (Hediff_Injury)HediffMaker.MakeHediff(hediffDefFromDamage, pawn);
			hediff_Injury.Part = part;
			hediff_Injury.sourceDef = weaponDef;
			hediff_Injury.sourceLabel = weaponDef?.label ?? "";
			hediff_Injury.Severity = amount;
			hediff_Injury.TryGetComp<HediffComp_GetsPermanent>()?.PreFinalizeInjury();
			pawn.health.AddHediff(hediff_Injury, null, new DamageInfo(DamageDefOf.Burn, 30f, 999f));
			return hediff_Injury;
		}
	}

	public class BattleLogEntry_CumulativeEffect : LogEntry_DamageResult
	{
		private Pawn initiatorPawn;

		private ThingDef initiatorThing;

		private Pawn recipientPawn;

		private ThingDef weaponDef;

		private ThingDef projectileDef;

		private DamageDef damageDef;

		private string InitiatorName
		{
			get
			{
				if (initiatorPawn != null)
				{
					return initiatorPawn.LabelShort;
				}
				if (initiatorThing != null)
				{
					return initiatorThing.defName;
				}
				return "null";
			}
		}

		private string RecipientName
		{
			get
			{
				if (recipientPawn != null)
				{
					return recipientPawn.LabelShort;
				}
				return "null";
			}
		}

		public BattleLogEntry_CumulativeEffect()
		{
		}

		public BattleLogEntry_CumulativeEffect(Thing initiator, Pawn recipient, ThingDef weaponDef, ThingDef projectileDef, DamageDef damageDef)
		{
			if (initiator is Pawn)
			{
				initiatorPawn = initiator as Pawn;
			}
			else if (initiator != null)
			{
				initiatorThing = initiator.def;
			}
			recipientPawn = recipient as Pawn;
			this.weaponDef = weaponDef;
			this.projectileDef = projectileDef;
			this.damageDef = damageDef;
		}

		public override bool Concerns(Thing t)
		{
			if (t != initiatorPawn)
			{
				return t == recipientPawn;
			}
			return true;
		}

		public override IEnumerable<Thing> GetConcerns()
		{
			if (initiatorPawn != null)
			{
				yield return initiatorPawn;
			}
			if (recipientPawn != null)
			{
				yield return recipientPawn;
			}
		}

		public override bool CanBeClickedFromPOV(Thing pov)
		{
			if (pov != initiatorPawn || recipientPawn == null || !CameraJumper.CanJump(recipientPawn))
			{
				if (pov == recipientPawn)
				{
					return CameraJumper.CanJump(initiatorPawn);
				}
				return false;
			}
			return true;
		}

		public override void ClickedFromPOV(Thing pov)
		{
			if (recipientPawn == null)
			{
				return;
			}
			if (pov == initiatorPawn)
			{
				CameraJumper.TryJumpAndSelect(recipientPawn);
				return;
			}
			if (pov == recipientPawn)
			{
				CameraJumper.TryJumpAndSelect(initiatorPawn);
				return;
			}
			throw new NotImplementedException();
		}

		public override Texture2D IconFromPOV(Thing pov)
		{
			if (damagedParts.NullOrEmpty())
			{
				return null;
			}
			if (pov == null || pov == recipientPawn)
			{
				return LogEntry.Blood;
			}
			if (pov == initiatorPawn)
			{
				return LogEntry.BloodTarget;
			}
			return null;
		}

		protected override BodyDef DamagedBody()
		{
			if (recipientPawn == null)
			{
				return null;
			}
			return recipientPawn.RaceProps.body;
		}

		protected override GrammarRequest GenerateGrammarRequest()
		{
			GrammarRequest result = base.GenerateGrammarRequest();
			result.Includes.Add(RCDefOf.DMSRC_Combat_CumulativeEffect);
			if (initiatorPawn != null)
			{
				result.Rules.AddRange(GrammarUtility.RulesForPawn("INITIATOR", initiatorPawn, result.Constants));
			}
			else if (initiatorThing != null)
			{
				result.Rules.AddRange(GrammarUtility.RulesForDef("INITIATOR", initiatorThing));
			}
			else
			{
				result.Constants["INITIATOR_missing"] = "True";
			}
			if (recipientPawn != null)
			{
				result.Rules.AddRange(GrammarUtility.RulesForPawn("RECIPIENT", recipientPawn, result.Constants));
			}
			else
			{
				result.Constants["RECIPIENT_missing"] = "True";
			}
			result.Rules.AddRange(PlayLogEntryUtility.RulesForOptionalWeapon("WEAPON", weaponDef, projectileDef));
			if (projectileDef != null)
			{
				result.Rules.AddRange(GrammarUtility.RulesForDef("PROJECTILE", projectileDef));
			}
			if (damageDef != null && damageDef.combatLogRules != null)
			{
				result.Includes.Add(damageDef.combatLogRules);
			}
			return result;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref initiatorPawn, "initiatorPawn", saveDestroyedThings: true);
			Scribe_Defs.Look(ref initiatorThing, "initiatorThing");
			Scribe_References.Look(ref recipientPawn, "recipientPawn", saveDestroyedThings: true);
			Scribe_Defs.Look(ref weaponDef, "weaponDef");
			Scribe_Defs.Look(ref projectileDef, "projectileDef");
			Scribe_Defs.Look(ref damageDef, "damageDef");
		}

		public override string ToString()
		{
			return "DMSRC.BattleLogEntry_CumulativeEffect: " + InitiatorName + "->" + RecipientName;
		}
	}
}
