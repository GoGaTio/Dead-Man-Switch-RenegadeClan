using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DMSRC
{
	public class FacilityEntrance : Fortified.FacilityEntrance
	{
		public bool facilityDestroyed = false;

		public int countdown = -1;
		public override void ExposeData()
		{
			base.ExposeData();
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

		public void DestroyMap()
		{
			if (base.PocketMapExists)
			{
				try
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
				catch (Exception arg)
				{
					Log.Error($"Error destroying PocketMap: {arg}");
				}
			}
			facilityDestroyed = true;
			QuestUtility.SendQuestTargetSignals(questTags, "FacilityDestroyed", this.Named("SUBJECT"));
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
