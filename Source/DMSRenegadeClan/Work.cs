using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DMSRC
{
	public class WorkGiver_HaulToWetwareProducer : WorkGiver_Scanner
	{
		public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(RCDefOf.DMSRC_WetwareProducer);

		public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			if(t is Building_WetwareProducer producer)
			{
				if (!pawn.CanReserve(t, 1, -1, null, forced))
				{
					return false;
				}
				if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null)
				{
					return false;
				}
				if (t.IsBurning())
				{
					return false;
				}
				Pawn target = producer.SelectedPawn;
				if (target == null)
				{
					return false;
				}
				if (producer.innerContainer.Contains(target))
				{
					if (producer.NutritionNeeded > producer.NutritionConsumedPerDay || forced)
					{
						if (FindNutrition(pawn, producer).Thing == null)
						{
							JobFailReason.Is("NoFood".Translate());
							return false;
						}
						return true;
					}
				}
				else if (target.IsPrisonerOfColony || target.Downed || !target.health.capacities.CapableOf(PawnCapacityDefOf.Moving) || (def.workType != null && target.WorkTypeIsDisabled(def.workType)))
				{
					return pawn.CanReserveAndReach(target, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, forced);
				}
			}
			return false;
		}

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			if (!(t is Building_WetwareProducer producer))
			{
				return null;
			}
			Pawn target = producer.SelectedPawn;
			if (target == null)
			{
				return null;
			}
			if (!producer.innerContainer.Contains(target))
			{
				Job carryJob = JobMaker.MakeJob(JobDefOf.CarryToBuilding, producer, target);
				carryJob.count = 1;
				return carryJob;
			}
			if (producer.NutritionNeeded > 0f)
			{
				ThingCount thingCount = FindNutrition(pawn, producer);
				if (thingCount.Thing != null)
				{
					Job job = HaulAIUtility.HaulToContainerJob(pawn, thingCount.Thing, t);
					job.count = Mathf.Min(job.count, thingCount.Count);
					return job;
				}
			}
			return null;
		}

		private ThingCount FindNutrition(Pawn pawn, Building_WetwareProducer producer)
		{
			Thing thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.FoodSourceNotPlantOrTree), PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, Validator);
			if (thing == null)
			{
				return default(ThingCount);
			}
			int b = Mathf.CeilToInt(producer.NutritionNeeded / thing.GetStatValue(StatDefOf.Nutrition));
			return new ThingCount(thing, Mathf.Min(thing.stackCount, b));
			bool Validator(Thing x)
			{
				if (x.IsForbidden(pawn) || !pawn.CanReserve(x))
				{
					return false;
				}
				if (!producer.CanAcceptNutrition(x))
				{
					return false;
				}
				return true;
			}
		}
	}
}
