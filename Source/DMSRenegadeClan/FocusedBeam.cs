using Fortified;
using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
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
using static RimWorld.PsychicRitualRoleDef;
using static System.Net.Mime.MediaTypeNames;
using static Verse.DamageWorker;

namespace DMSRC
{
	public class Verb_FocusedBeam : Verb
	{
		private List<Vector3> path = new List<Vector3>();

		private List<Vector3> tmpPath = new List<Vector3>();

		private int ticksToNextPathStep;

		private Vector3 initialTargetPosition;

		private MoteDualAttached mote;

		private Thing thingTarget;

		private Effecter endEffecter;

		private Sustainer sustainer;

		private HashSet<IntVec3> pathCells = new HashSet<IntVec3>();

		private HashSet<IntVec3> tmpPathCells = new HashSet<IntVec3>();

		private HashSet<IntVec3> tmpHighlightCells = new HashSet<IntVec3>();

		private HashSet<IntVec3> tmpSecondaryHighlightCells = new HashSet<IntVec3>();

		private HashSet<IntVec3> hitCells = new HashSet<IntVec3>();

		private List<Vector3> offsetsByRots = null;

		private ShootLine resultingLine;

		private const int NumSubdivisionsPerUnitLength = 1;

		protected override int ShotsPerBurst => base.BurstShotCount;

		public float ShotProgress => (float)ticksToNextPathStep / (float)base.TicksBetweenBurstShots;

		public Vector3 InterpolatedPosition
		{
			get
			{
				Vector3 vector = base.CurrentTarget.CenterVector3 - initialTargetPosition;
				return Vector3.Lerp(path[burstShotsLeft], path[Mathf.Min(burstShotsLeft + 1, path.Count - 1)], ShotProgress) + vector;
			}
		}

		private CompVehicleWeapon vehicleWeaponInt = null;

		public CompVehicleWeapon VehicleWeapon
		{
			get
			{
				if (vehicleWeaponInt == null)
				{
					vehicleWeaponInt = Caster.TryGetComp<CompVehicleWeapon>();
				}
				return vehicleWeaponInt;
			}
		}

		private CompMultipleTurretGun multipleTurretGunInt = null;

		public CompMultipleTurretGun MultipleTurretGun
		{
			get
			{
				if (multipleTurretGunInt == null)
				{
					multipleTurretGunInt = Caster.TryGetComp<CompMultipleTurretGun>();
				}
				return multipleTurretGunInt;
			}
		}

		public override float? AimAngleOverride
		{
			get
			{
				if (state != VerbState.Bursting)
				{
					return null;
				}
				return (InterpolatedPosition - caster.DrawPos).AngleFlat();
			}
		}

		public void UpdateOffsets()
		{
			offsetsByRots = new List<Vector3>(4);
			if (CasterIsPawn)
			{
				if (EquipmentSource == CasterPawn.equipment.Primary)
				{
					if (VehicleWeapon != null)
					{
						AddOffsets(VehicleWeapon.Props.drawData);
					}
					else if (CasterPawn.apparel != null)
					{
						Type type = AccessTools.TypeByName("Exosuit.Exosuit_Core");
						Apparel core = CasterPawn.apparel.WornApparel.FirstOrDefault((a) => a.def.thingClass == type);
						if (core != null)
						{
							DefModExtension ext = core.def.modExtensions.FirstOrDefault((x) => x.GetType().Name == "ApparelRenderOffsets");
							if (ext != null)
							{
								DrawData data = equipmentOffsetData.GetValue(ext) as DrawData;
								if (data != null)
								{
									AddOffsets(data);
								}
							}
						}
					}
				}
				else
				{
					SubTurret turret = MultipleTurretGun?.turrets?.FirstOrDefault((x) => x.turret == EquipmentSource);
					if (turret != null)
					{
						AddOffsets(turret.TurretProp.renderNodeProperties.First().drawData);
					}
				}
			}
			void AddOffsets(DrawData drawData)
			{
				for (int i = 0; i < 4 ; i++)
				{
					offsetsByRots.Add(drawData.OffsetForRot(new Rot4(i)));
				}
			}
		}

		public Vector3 OffsetByRot(Rot4 rot)
		{
			if (offsetsByRots == null)
			{
				UpdateOffsets();
			}
			if (offsetsByRots.NullOrEmpty())
			{
				return Vector3.zero;
			}
			return offsetsByRots[rot.AsInt];
		}

		public override void DrawHighlight(LocalTargetInfo target)
		{
			tmpHighlightCells.Clear();
			tmpSecondaryHighlightCells.Clear();
			verbProps.DrawRadiusRing(caster.Position, this);
			if (!target.IsValid)
			{
				return;
			}
			GenDraw.DrawTargetHighlight(target);
			DrawHighlightFieldRadiusAroundTarget(target);
			CellRect map = CellRect.WholeMap(Caster.Map);
			CalculatePath(target.CenterVector3, tmpPath, tmpPathCells, false);
			foreach (IntVec3 tmpPathCell in tmpPathCells)
			{
				if (!TryGetHitCell(tmpPathCell, out var hitCell))
				{
					continue;
				}
				tmpHighlightCells.Add(hitCell);
				foreach (IntVec3 beamHitNeighbourCell in GetNeighbours(hitCell))
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
				GenDraw.DrawFieldEdges(tmpSecondaryHighlightCells.ToList(), verbProps.secondaryHighlightColor.Value);
			}
		}

		private List<IntVec3> neighboursCached = new List<IntVec3>();

		private IEnumerable<IntVec3> GetNeighbours(IntVec3 cell)
		{
			if (neighboursCached.NullOrEmpty())
			{
				neighboursCached = new List<IntVec3>();
				float range = verbProps.sprayWidth;
				IntVec3 zero = IntVec3.Zero;
				foreach (IntVec3 c in CellRect.FromCell(zero).ExpandedBy(Mathf.CeilToInt(range)))
				{
					if (c.DistanceTo(zero) <= range)
					{
						neighboursCached.Add(c);
					}
				}
			}
			foreach (IntVec3 neighbour in neighboursCached)
			{
				yield return neighbour + cell;
			}
		}

		/*protected override bool TryCastShot()
		{
			if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
			{
				return false;
			}
			ShootLine resultingLine;
			bool flag = TryFindShootLineFromTo(caster.Position, currentTarget, out resultingLine);
			if (verbProps.stopBurstWithoutLos && !flag)
			{
				return false;
			}
			
			if (!TryGetHitCell(resultingLine.Source, targetCell, out var hitCell))
			{
				return true;
			}
			HitCell(hitCell, resultingLine.Source);
			
			return true;
		}*/

		protected bool TryGetHitCell(IntVec3 targetCell, out IntVec3 hitCell)
		{
			IntVec3 root = Caster.Position;
			LocalTargetInfo targetInfo = currentTarget.Cell == targetCell ? currentTarget : targetCell;
			if (CanHitTarget(targetInfo))
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
			if (base.EquipmentSource != null)
			{
				base.EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
				base.EquipmentSource.GetComp<CompApparelReloadable>()?.UsedOnce();
			}
			lastShotTick = Find.TickManager.TicksGame;
			ticksToNextPathStep = base.TicksBetweenBurstShots;
			ShootLine resultingLine;
			bool flag = TryFindShootLineFromTo(caster.Position, currentTarget, out resultingLine);
			IntVec3 intVec = InterpolatedPosition.Yto0().ToIntVec3();
			if (TryGetHitCell(intVec, out var hitCell))
			{
				float damageFactor = EquipmentSource?.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) ?? 1f;
				hitCells.Clear();
				HitCell(hitCell, damageFactor);
				FleckMaker.ThrowSmoke(hitCell.ToVector3Shifted() + Gen.RandomHorizontalVector(verbProps.sprayWidth * 0.7f), caster.Map, verbProps.sprayWidth * 0.6f);
				hitCells.Add(hitCell);
				foreach (IntVec3 beamHitNeighbourCell in GetNeighbours(hitCell))
				{
					if (!hitCells.Contains(beamHitNeighbourCell))
					{
						float t = beamHitNeighbourCell.DistanceTo(hitCell) / verbProps.sprayWidth;
						HitCell(beamHitNeighbourCell, damageFactor * Mathf.Lerp(1f, 0.2f, t));
						hitCells.Add(beamHitNeighbourCell);
					}
				}
				return true;
			}
			return false;
		}

		public override void WarmupComplete()
		{
			UpdateOffsets();
			burstShotsLeft = ShotsPerBurst;
			state = VerbState.Bursting;
			initialTargetPosition = currentTarget.CenterVector3;
			CalculatePath(currentTarget.CenterVector3, path, pathCells);
			hitCells.Clear();
			if (verbProps.beamMoteDef != null)
			{
				mote = MoteMaker.MakeInteractionOverlay(verbProps.beamMoteDef, caster, new TargetInfo(path[0].ToIntVec3(), caster.Map));
			}
			TryCastNextBurstShot();
			ticksToNextPathStep = base.TicksBetweenBurstShots;
			endEffecter?.Cleanup();
			if (verbProps.soundCastBeam != null)
			{
				sustainer = verbProps.soundCastBeam.TrySpawnSustainer(SoundInfo.InMap(caster, MaintenanceType.PerTick));
			}
			thingTarget = null;
			if (base.currentTarget.HasThing && !base.currentTarget.Thing.DestroyedOrNull())
			{
				thingTarget = base.currentTarget.Thing;
				currentTarget = thingTarget.PositionHeld;
				currentDestination = LocalTargetInfo.Invalid;
			}
		}

		private bool CanHit(Thing thing)
		{
			return thing.Spawned;
		}

		private void HitCell(IntVec3 cell, float damageFactor = 1f)
		{
			if (cell.InBounds(caster.Map))
			{
				if (verbProps.beamDamageDef.explosionCellFleck != null)
				{
					FleckMaker.ThrowExplosionCell(cell, caster.Map, verbProps.beamDamageDef.explosionCellFleck, verbProps.beamDamageDef.explosionColorEdge);
				}
				ApplyDamage(VerbUtility.ThingsToHit(cell, caster.Map, CanHit).RandomElementWithFallback(), damageFactor);
				if (Rand.Chance(verbProps.beamChanceToStartFire))
				{
					FireUtility.TryStartFireIn(cell, caster.Map, 1f, caster);
				}
			}
		}

		private void ApplyDamage(Thing thing, float damageFactor = 1f)
		{
			Map map = caster.Map;
			if (thing == null || verbProps.beamDamageDef == null)
			{
				return;
			}
			float angleFlat = (currentTarget.Cell - caster.Position).AngleFlat;

			/*BattleLogEntry_ExplosionImpact battleLogEntry_ExplosionImpact = null;
			Pawn pawn = t as Pawn;
			if (pawn != null)
			{
				battleLogEntry_ExplosionImpact = new BattleLogEntry_ExplosionImpact(explosion.instigator, t, explosion.weapon, explosion.projectile, def);
				Find.BattleLog.Add(battleLogEntry_ExplosionImpact);
			}*/


			//BattleLogEntry_RangedImpact log = new BattleLogEntry_RangedImpact(caster, thing, currentTarget.Thing, base.EquipmentSource.def, null, null);
			BattleLogEntry_RangedImpact log = null;
			DamageInfo dinfo;
			if (verbProps.beamTotalDamage > 0f)
			{
				float num = verbProps.beamTotalDamage;
				num *= damageFactor;
				dinfo = new DamageInfo(verbProps.beamDamageDef, num, 99f, angleFlat, caster, null, base.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, currentTarget.Thing);
			}
			else
			{
				float amount = (float)verbProps.beamDamageDef.defaultDamage * damageFactor;
				dinfo = new DamageInfo(verbProps.beamDamageDef, amount, 99f, angleFlat, caster, null, base.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, currentTarget.Thing);
			}
			dinfo.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);
			if (thing is Pawn)
			{
				log = new BattleLogEntry_RangedImpact(caster, thing, thingTarget ?? currentTarget.Thing, base.EquipmentSource.def, null, null);
				Find.BattleLog.Add(log);
			}
			if (thingTarget != null || currentTarget.HasThing)
			{
				dinfo.intendedTargetInt = thingTarget ?? currentTarget.Thing;
			}
			DamageResult damageResult = thing.TakeDamage(dinfo);
			damageResult.AssociateWithLog(log);
		}

		public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
		{
			return base.TryStartCastOn(verbProps.beamTargetsGround ? ((LocalTargetInfo)castTarg.Cell) : castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
		}

		public override void BurstingTick()
		{
			IntVec3 root = Caster.Position;
			if (thingTarget != null && !thingTarget.Position.IsValid && thingTarget.Map == Caster.Map)
			{
				currentTarget = thingTarget.Position;
			}
			ticksToNextPathStep--;
			base.BurstingTick();
			TryFindShootLineFromTo(root, currentTarget, out resultingLine);
			Vector3 vector = InterpolatedPosition;
			IntVec3 intVec = vector.ToIntVec3();
			Vector3 vector2 = InterpolatedPosition - root.ToVector3Shifted();
			float num = vector2.MagnitudeHorizontal();
			Vector3 normalized = vector2.Yto0().normalized;
			if (TryGetHitCell(intVec, out var intVec2))
			{
				num -= (intVec - intVec2).LengthHorizontal;
				vector = root.ToVector3Shifted() + normalized * num;
				intVec = vector.ToIntVec3();
			}
			Vector3 offsetA = OffsetByRot(Caster.Rotation);
			Vector3 offsetB = normalized * verbProps.beamStartOffset;
			Vector3 vector3 = vector - intVec.ToVector3Shifted();
			if (mote != null)
			{
				mote.UpdateTargets(new TargetInfo(root, caster.Map), new TargetInfo(intVec, caster.Map), offsetA + offsetB, vector3);
				mote.Maintain();
			}
			if (verbProps.beamGroundFleckDef != null && Rand.Chance(verbProps.beamFleckChancePerTick))
			{
				FleckMaker.Static(vector, caster.Map, verbProps.beamGroundFleckDef);
			}
			if (endEffecter == null && verbProps.beamEndEffecterDef != null)
			{
				endEffecter = verbProps.beamEndEffecterDef.Spawn(intVec, caster.Map, vector3);
			}
			if (endEffecter != null)
			{
				endEffecter.offset = vector3;
				endEffecter.EffectTick(new TargetInfo(intVec, caster.Map), TargetInfo.Invalid);
				endEffecter.ticksLeft--;
			}
			if (verbProps.beamLineFleckDef != null)
			{
				float num2 = 1f * num;
				for (int num3 = 0; (float)num3 < num2; num3++)
				{
					if (Rand.Chance(verbProps.beamLineFleckChanceCurve.Evaluate((float)num3 / num2)))
					{
						Vector3 vector4 = num3 * normalized - normalized * Rand.Value + normalized / 2f;
						FleckMaker.Static(caster.Position.ToVector3Shifted() + vector4, caster.Map, verbProps.beamLineFleckDef);
					}
				}
			}
			sustainer?.Maintain();
		}
		private void CalculatePath(Vector3 target, List<Vector3> pathList, HashSet<IntVec3> pathCellsList, bool addRandomOffset = true)
		{
			pathList.Clear();
			Vector3 vector = (target - caster.Position.ToVector3Shifted()).Yto0();
			float magnitude = vector.magnitude;
			Vector3 normalized = vector.normalized;
			Vector3 vector2 = normalized.RotatedBy(-90f);
			float num = ((verbProps.beamFullWidthRange > 0f) ? Mathf.Min(magnitude / verbProps.beamFullWidthRange, 1f) : 1f);
			float num2 = (verbProps.beamWidth + 1f) * num / (float)ShotsPerBurst;
			Vector3 vector3 = target.Yto0() - vector2 * verbProps.beamWidth / 2f * num;
			pathList.Add(vector3);
			for (int i = 0; i < ShotsPerBurst; i++)
			{
				Vector3 vector4 = normalized * (Rand.Value * verbProps.beamMaxDeviation) - normalized / 2f;
				Vector3 vector5 = Mathf.Sin(((float)i / (float)ShotsPerBurst + 0.5f) * Mathf.PI * 57.29578f) * verbProps.beamCurvature * -normalized - normalized * verbProps.beamMaxDeviation / 2f;
				if (addRandomOffset)
				{
					pathList.Add(vector3 + (vector4 + vector5) * num);
				}
				else
				{
					pathList.Add(vector3 + vector5 * num);
				}
				vector3 += vector2 * num2;
			}
			pathCellsList.Clear();
			foreach (Vector3 path in pathList)
			{
				pathCellsList.Add(path.ToIntVec3());
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref path, "path", LookMode.Value);
			Scribe_Values.Look(ref ticksToNextPathStep, "ticksToNextPathStep", 0);
			Scribe_Values.Look(ref initialTargetPosition, "initialTargetPosition");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if(path == null)
				{
					path = new List<Vector3>();
				}
				neighboursCached = null;
			}
		}

		private static readonly FieldInfo equipmentOffsetData = AccessTools.Field(AccessTools.TypeByName("Exosuit.ApparelRenderOffsets"), "equipmentOffsetData");

		public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
		{
			if (!WarmingUp)
			{
				verbProps.requireLineOfSight = true;
				return base.CanHitTargetFrom(root, targ);
			}
			bool b = false;
			try
			{
				verbProps.requireLineOfSight = false;
				b = base.CanHitTargetFrom(root, targ);
			}
			catch (Exception ex)
			{
				Log.Error("Could not get hit target from" + root.ToString() + ": "  + ex);
			}
			finally
			{
				verbProps.requireLineOfSight = true;
			}
			return b;
		}
	}
}
