using Verse;

namespace ApexMechs
{
	public class Verb_ShootMultiple : Verb_Shoot
	{
		protected override int ShotsPerBurst => verbProps.burstShotCount + (int)((1 - CasterPawn.health.summaryHealth.SummaryHealthPercent) * 10);

		protected override bool TryCastShot()
		{
			bool num = base.TryCastShot();
			for (int i = 0; i < (int)((1 - CasterPawn.health.summaryHealth.SummaryHealthPercent) * 10); i++)
			{
                if (Rand.Chance(0.5f)) break;
                LocalTargetInfo secondaryTarget;

				do
				{
					secondaryTarget = new LocalTargetInfo(CurrentTarget.Cell + new IntVec3(Rand.Range(-3, 3), 0, Rand.Range(-3, 3)));
				}
				while (secondaryTarget.Cell.DistanceTo(CurrentTarget.Cell) <= 0f);
				((Projectile)GenSpawn.Spawn(Projectile, CasterPawn.Position, CasterPawn.Map)).Launch(CasterPawn, secondaryTarget, secondaryTarget, ProjectileHitFlags.NonTargetPawns);
			}
            return num;
		}
	}
}