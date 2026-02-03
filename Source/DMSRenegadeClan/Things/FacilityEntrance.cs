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
			if (this.IsHashIntervalTick(250) && PocketMap != null)
			{
				foreach(Lord lord in Map.lordManager.lords.ToList())
				{
					if(lord.faction.IsPlayer == false && !GenHostility.AnyHostileActiveThreatTo(Map, lord.faction))
					{
						List<Pawn> list = lord.ownedPawns.ToList();
						foreach(Pawn p in list)
						{
							Job job = JobMaker.MakeJob(JobDefOf.EnterPortal, this);
							job.checkOverrideOnExpire = false;
							job.expiryInterval = 99999;
							job.expireOnEnemiesNearby = false;
							job.playerForced = true;
							p.jobs.TryTakeOrderedJob(job, JobTag.Misc);
						}
					}
				}
			}
			base.Tick();
		}

		public override void OnEntered(Pawn pawn)
		{
			base.OnEntered(pawn);
			if(pawn.Faction?.IsPlayer == false)
			{
				Lord prevlord = pawn.GetLord();
				if(prevlord != null)
				{
					prevlord.RemovePawn(pawn);
				}
				Lord lord = PocketMap.lordManager.lords.FirstOrDefault((x) => x.faction == pawn.Faction && typeof(LordJob_AssaultColony).IsAssignableFrom(x.LordJob.GetType()));
				if (lord == null)
				{
					lord = LordMaker.MakeNewLord(pawn.Faction, new LordJob_AssaultColony(pawn.Faction, false, false, false, true, false, false, true), PocketMap);
				}
				lord.AddPawn(pawn);
			}
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

		protected override Map GeneratePocketMapInt()
		{
			if(RPrefabUtility.TryGetByTag(rPrefabTag, out var result))
			{
				GenStep_RPrefab.staticPrefab = result;
			}
			return base.GeneratePocketMapInt();
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
