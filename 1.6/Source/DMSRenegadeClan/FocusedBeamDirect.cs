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
	public class Verb_FocusedBeamDirect : Verb
	{
		private int ticksToNextPathStep;

		private Vector3 beamFocusPosition;

		private MoteDualAttached mote;

		private Thing thingTarget;

		private Effecter endEffecter;

		private Sustainer sustainer;

		private HashSet<IntVec3> tmpHighlightCells = new HashSet<IntVec3>();

		private HashSet<IntVec3> tmpSecondaryHighlightCells = new HashSet<IntVec3>();

		private HashSet<IntVec3> hitCells = new HashSet<IntVec3>();

		private List<Vector3> offsetsByRots = null;

		public IntVec3 BeamFocus => new IntVec3(Mathf.RoundToInt(beamFocusPosition.x), 0, Mathf.RoundToInt(beamFocusPosition.z));

		public Vector3 InterpolatedPosition
		{
			get
			{
				return beamFocusPosition;
			}
		}

		protected override int ShotsPerBurst => base.BurstShotCount;

		public float ShotProgress => (float)ticksToNextPathStep / (float)base.TicksBetweenBurstShots;

		private CompVehicleWeapon vehicleWeaponInt = null;

		private bool vehicleWeaponResolved = false;

		public CompVehicleWeapon VehicleWeapon
		{
			get
			{
				if (vehicleWeaponResolved)
				{
					return vehicleWeaponInt;
				}
				if (vehicleWeaponInt == null)
				{
					vehicleWeaponInt = Caster.TryGetComp<CompVehicleWeapon>();
					vehicleWeaponResolved = true;
				}
				return vehicleWeaponInt;
			}
		}

		private CompMultipleTurretGun multipleTurretGunInt = null;

		private bool multipleTurretGunResolved = false;

		public CompMultipleTurretGun MultipleTurretGun
		{
			get
			{
				if (multipleTurretGunResolved)
				{
					return multipleTurretGunInt;
				}
				if (multipleTurretGunInt == null)
				{
					multipleTurretGunInt = Caster.TryGetComp<CompMultipleTurretGun>();
					multipleTurretGunResolved = true;
				}
				return multipleTurretGunInt;
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
			if (!TryGetHitCell(target.Cell, out var hitCell))
			{
				return;
			}
			tmpHighlightCells.Add(hitCell);
			foreach (IntVec3 beamHitNeighbourCell in GetNeighbours(hitCell))
			{
				if (!tmpSecondaryHighlightCells.Contains(beamHitNeighbourCell) && map.Contains(beamHitNeighbourCell))
				{
					tmpSecondaryHighlightCells.Add(beamHitNeighbourCell);
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
				float range = verbProps.beamWidth;
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
			IntVec3 intVec = beamFocusPosition.Yto0().ToIntVec3();
			if (TryGetHitCell(intVec, out var hitCell))
			{
				float damageFactor = EquipmentSource?.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) ?? 1f;
				hitCells.Clear();
				HitCell(hitCell, damageFactor);
				FleckMaker.ThrowSmoke(hitCell.ToVector3Shifted() + Gen.RandomHorizontalVector(verbProps.beamWidth * 0.7f), caster.Map, verbProps.beamWidth * 0.6f);
				hitCells.Add(hitCell);
				foreach (IntVec3 beamHitNeighbourCell in GetNeighbours(hitCell))
				{
					if (!hitCells.Contains(beamHitNeighbourCell))
					{
						float t = beamHitNeighbourCell.DistanceTo(hitCell) / verbProps.beamWidth;
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
			beamFocusPosition = currentTarget.CenterVector3;
			hitCells.Clear();
			if (verbProps.beamMoteDef != null)
			{
				mote = MoteMaker.MakeInteractionOverlay(verbProps.beamMoteDef, caster, new TargetInfo(BeamFocus, caster.Map));
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
			if (thingTarget != null && thingTarget.PositionHeld.IsValid && thingTarget.MapHeld == Caster.Map)
			{
				beamFocusPosition += (thingTarget.PositionHeld.ToVector3Shifted() - beamFocusPosition).Yto0().normalized * verbProps.beamCurvature;
				currentTarget = BeamFocus;
			}
			ticksToNextPathStep--;
			base.BurstingTick();
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

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref ticksToNextPathStep, "ticksToNextPathStep", 0);
			Scribe_Values.Look(ref beamFocusPosition, "beamFocusPosition");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
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
