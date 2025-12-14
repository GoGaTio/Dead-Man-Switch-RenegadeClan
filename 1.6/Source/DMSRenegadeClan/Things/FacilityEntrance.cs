using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace DMSRC
{
	public class FacilityEntrance : MapPortal
	{
		public string rPrefabTag = "";

		public bool facilityDestroyed = false;

		public int countdown = -1;
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref rPrefabTag, "rPrefabTag");
			Scribe_Values.Look(ref facilityDestroyed, "facilityDestroyed");
			Scribe_Values.Look(ref countdown, "countdown");
		}

		public override bool IsEnterable(out string reason)
		{
			if (facilityDestroyed)
			{
				reason = "DMSRC_FacilityDestroyed".Translate();
				return false;
			}
			return base.IsEnterable(out reason);
		}

		protected override void Tick()
		{
			if(countdown > 0)
			{
				countdown--;
				if (countdown == 0) DestroyMap();
			}
			base.Tick();
		}

		public void DestroyMap()
		{
			if (base.PocketMapExists)
			{
				DamageInfo damageInfo = new DamageInfo(DamageDefOf.Crush, 99999f, 9999f);
				for (int num = pocketMap.mapPawns.AllPawns.Count - 1; num >= 0; num--)
				{
					Pawn pawn = pocketMap.mapPawns.AllPawns[num];
					pawn.TakeDamage(damageInfo);
					if (!pawn.Dead)
					{
						pawn.Kill(damageInfo);
					}
				}
				PocketMapUtility.DestroyPocketMap(pocketMap);
			}
			facilityDestroyed = true;
		}

		protected override IEnumerable<GenStepWithParams> GetExtraGenSteps()
		{

			yield return new GenStepWithParams(GenStepDefOf.AncientStockpile, default(GenStepParams));
		}

		public void StartCountdown(int ticks)
		{
			if(ticks < countdown)
			{
				countdown = ticks;
			}
		}
	}
}
