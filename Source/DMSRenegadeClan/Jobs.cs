using DelaunatorSharp;
using DMS;
using Fortified;
using Gilzoide.ManagedJobs;
using Ionic.Crc;
using Ionic.Zlib;
using JetBrains.Annotations;
using KTrie;
using LudeonTK;
using NVorbis.NAudioSupport;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using RuntimeAudioClipLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
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

namespace DMSRC
{
	public class JobDriver_PlantBomb : JobDriver
	{
		private IntVec3 Cell => job.GetTarget(TargetIndex.A).Cell;

		private Thing Item => job.GetTarget(TargetIndex.B).Thing;

		private bool usingFromInventory;

		private IntVec3 cellInt;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref usingFromInventory, "usingFromInventory", defaultValue: false);
			Scribe_Values.Look(ref cellInt, "DMSRC_cellInt");
		}

		public override void Notify_Starting()
		{
			base.Notify_Starting();
			cellInt = new IntVec3(Cell.x, 0, Cell.z);
			job.count = 1;
			usingFromInventory = pawn.inventory != null && pawn.inventory.Contains(Item);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			foreach (Toil item in PrepareToUseToils())
			{
				yield return item;
			}
			yield return Toils_Goto.GotoCell(Cell, PathEndMode.Touch);
			Toil toil = Toils_General.Wait(120);
			toil.WithProgressBarToilDelay(TargetIndex.A);
			toil.FailOnCannotReach(TargetIndex.A, PathEndMode.Touch);
			yield return toil;
			yield return Toils_General.Do(Plant);
		}

		private IEnumerable<Toil> PrepareToUseToils()
		{
			if (usingFromInventory)
			{
				yield return Toils_Misc.TakeItemFromInventoryToCarrier(pawn, TargetIndex.B);
			}
			else
			{
				yield return ReserveItem();
				yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch).FailOnDespawnedOrNull(TargetIndex.B);
				Toil toil = ToilMaker.MakeToil("PickupItem");
				toil.initAction = delegate
				{
					Pawn actor = toil.actor;
					Job curJob = actor.jobs.curJob;
					Thing thing = Item;
					actor.carryTracker.TryStartCarry(thing, 1);
					if (thing != actor.carryTracker.CarriedThing && actor.Map.reservationManager.ReservedBy(thing, actor, curJob))
					{
						actor.Map.reservationManager.Release(thing, actor, curJob);
					}
					actor.jobs.curJob.targetA = actor.carryTracker.CarriedThing;
				};
				toil.defaultCompleteMode = ToilCompleteMode.Instant;
				yield return toil;
			}
		}

		private Toil ReserveItem()
		{
			Toil toil = ToilMaker.MakeToil("ReserveItem");
			toil.initAction = delegate
			{
				if (pawn.Faction != null)
				{
					Thing thing = Item;
					if (pawn.carryTracker.CarriedThing != thing)
					{
						if (!pawn.Reserve(thing, job, 1, -1))
						{
							Log.Error(string.Concat("DMSRC reservation for ", pawn, " on job ", this, " failed, because it could not register item from ", thing));
							pawn.jobs.EndCurrentJob(JobCondition.Errored);
						}
						job.count = 1;
					}
				}
			};
			toil.defaultCompleteMode = ToilCompleteMode.Instant;
			toil.atomicWithPrevious = true;
			return toil;
		}

		private void Plant()
		{
			CompUseEffectTargetablePlant comp = Item.TryGetComp<CompUseEffectTargetablePlant>();
			if(comp != null)
			{
				Thing t = GenSpawn.Spawn(ThingMaker.MakeThing(comp.Props.plantDef), cellInt, pawn.MapHeld, WipeMode.Vanish);
				if (t.def.CanHaveFaction)
				{
					t.SetFaction(pawn.Faction);
				}
				int ticks = pawn.Faction == Faction.OfPlayerSilentFail ? Mathf.RoundToInt(comp.hoursCooldownSelected * 2500) : comp.Props.timerRange.RandomInRange * 2500;
				t.TryGetComp<CompTimedBomb>().timerTicks = ticks;
			}
			Item.SplitOff(1).Destroy();
		}
	}

	public class JobGiver_RepairMechs : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			if (!(pawn is OverseerMech mech) || !mech.Comp.Props.canRepair)
			{
				return null;
			}
            if (MechRepairUtility.CanRepair(mech) && mech.GetComp<CompMechRepairable>()?.autoRepair == true)
            {
				return JobMaker.MakeJob(RCDefOf.DMSRC_RepairMech, mech);
            }
			Thing thing = GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction), PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, (Thing x) => CanRepair(mech, x));
			if(thing == null)
            {
				return null;
            }
			return JobMaker.MakeJob(RCDefOf.DMSRC_RepairMech, thing);
		}

		private static bool CanRepair(Pawn pawn, Thing thing)
		{
			Pawn target = (Pawn)thing;
			if (!target.RaceProps.IsMechanoid)
			{
				return false;
			}
			CompMechRepairable compMechRepairable = thing.TryGetComp<CompMechRepairable>();
			if (compMechRepairable == null)
			{
				return false;
			}
			if (target.InAggroMentalState || target.HostileTo(pawn))
			{
				return false;
			}
			if (thing.IsForbidden(pawn))
			{
				return false;
			}
			if (!pawn.CanReserve(target, 1, -1, null, false))
			{
				return false;
			}
			if (target.IsBurning())
			{
				return false;
			}
			if (target.IsAttacking())
			{
				return false;
			}
			if (target.needs.energy == null)
			{
				return false;
			}
			if (!MechRepairUtility.CanRepair(target))
			{
				return false;
			}
			return compMechRepairable.autoRepair;
		}
	}

	public class JobDriver_RepairMech : JobDriver
	{
		private const TargetIndex MechInd = TargetIndex.A;

		protected int ticksToNextRepair;

		protected Pawn Mech => (Pawn)job.GetTarget(TargetIndex.A).Thing;

		private OverseerMech Overseer => pawn as OverseerMech;

		protected int TicksPerHeal => Mathf.RoundToInt(Overseer.Comp.Props.ticksPerHeal);

		private bool selfRepair;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn == Mech || pawn.Reserve(Mech, job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
            if (pawn == Mech)
            {
				selfRepair = true;
			}
			this.FailOnDestroyedOrNull(TargetIndex.A);
			this.FailOnForbidden(TargetIndex.A);
			this.FailOn(() => Mech.IsAttacking());
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			Toil toil = selfRepair ? Toils_General.Wait(int.MaxValue, TargetIndex.None) : Toils_General.WaitWith(TargetIndex.A, int.MaxValue, useProgressBar: false, maintainPosture: true, maintainSleep: true);
			toil.WithEffect(EffecterDefOf.MechRepairing, TargetIndex.A);
			toil.PlaySustainerOrSound(SoundDefOf.RepairMech_Touch);
			toil.AddPreInitAction(delegate
			{
				ticksToNextRepair = TicksPerHeal;
			});
			toil.handlingFacing = true;
			toil.tickIntervalAction = delegate (int delta)
			{
				ticksToNextRepair -= delta;
				if (ticksToNextRepair <= 0)
				{
					Mech.needs.energy.CurLevel -= Mech.GetStatValue(StatDefOf.MechEnergyLossPerHP) * (float)delta;
					MechRepairUtility.RepairTick(Mech, delta);
					ticksToNextRepair = TicksPerHeal;
				}
				if(!selfRepair)
                {
					pawn.rotationTracker.FaceTarget(Mech);
				}
			};
			toil.AddFinishAction(delegate
			{
				if (!selfRepair && Mech.jobs?.curJob != null)
				{
					Mech.jobs.EndCurrentJob(JobCondition.InterruptForced);
				}
			});
			toil.AddEndCondition(() => MechRepairUtility.CanRepair(Mech) ? JobCondition.Ongoing : JobCondition.Succeeded);
			yield return toil;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref ticksToNextRepair, "ticksToNextRepair", 0);
			Scribe_Values.Look(ref selfRepair, "selfRepair");
		}
	}

	public class JobDriver_ControlMech : JobDriver
	{
		private const TargetIndex MechInd = TargetIndex.A;

		private Pawn Mech => (Pawn)job.GetTarget(TargetIndex.A).Thing;

		private OverseerMech Overseer => pawn as OverseerMech;

		private int MechControlTime => Mathf.RoundToInt(Mech.GetStatValue(StatDefOf.ControlTakingTime) * 120f);

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(Mech, job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
			this.FailOn(() => Overseer == null || !MechanitorUtility.CanControlMech(Overseer.Comp.dummyPawn, Mech));
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			yield return Toils_General.WaitWith(TargetIndex.A, MechControlTime, useProgressBar: true, maintainPosture: true, maintainSleep: false, TargetIndex.A).WithEffect(EffecterDefOf.ControlMech, TargetIndex.A);
			Toil toil = ToilMaker.MakeToil("MakeNewToils");
			toil.initAction = delegate
			{
				Overseer.Comp.Connect(Mech, Overseer.Comp.dummyPawn);
			};
			toil.PlaySoundAtEnd(SoundDefOf.ControlMech_Complete);
			yield return toil;
		}
	}

	public class JobDriver_OpenContainer : JobDriver
	{
		private Building_SecurityContainer Container => (Building_SecurityContainer)job.GetTarget(TargetIndex.A).Thing;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(Container, job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
			this.FailOn(() => Container.opened);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
			yield return Toils_General.WaitWith(TargetIndex.A, Container.Comp.Props.openTime, useProgressBar: true, face: TargetIndex.A);
			Toil toil = ToilMaker.MakeToil("MakeNewToils");
			toil.initAction = delegate
			{
				Container.Open(pawn);
			};
			toil.PlaySoundAtEnd(SoundDefOf.Door_OpenManual);
			yield return toil;
		}
	}

	public class JobDriver_TalkToRenegades : JobDriver
	{
		private Building Target => (Building)job.GetTarget(TargetIndex.A).Thing;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(Target, job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
			this.FailOn(() => Target.GetComp<CompCallRenegades>()?.powerTraderComp?.PowerOn == false);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
			Toil toil = ToilMaker.MakeToil("MakeNewToils");
			toil.initAction = delegate
			{
				Find.WindowStack.Add(new Dialog_Renegades(GameComponent_Renegades.Find, Target.Map));
			};
			yield return toil;
		}
	}
}
