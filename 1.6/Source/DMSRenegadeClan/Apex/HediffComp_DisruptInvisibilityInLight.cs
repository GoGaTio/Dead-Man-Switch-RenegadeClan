using RimWorld;
using Verse;
using Verse.AI;

namespace CultBuffs
	{
	public class HediffComp_DisruptInvisibilityInLight : HediffComp
	{
		public HediffCompProperties_DisruptInvisibilityInLight Props => (HediffCompProperties_DisruptInvisibilityInLight)props;

        public override void CompPostTick(ref float severityAdjustment)
		{
            if (parent?.pawn?.Map != null) 
            {
                float lightLevel = parent.pawn.Map.glowGrid.GroundGlowAt(parent.pawn.Position);
				if (lightLevel > 0.29) parent.TryGetComp<HediffComp_Invisibility>().DisruptInvisibility();
            }
		}
	}

	public class HediffCompProperties_DisruptInvisibilityInLight : HediffCompProperties
	{
		public HediffCompProperties_DisruptInvisibilityInLight()
		{
			compClass = typeof(HediffComp_DisruptInvisibilityInLight);
		}
	}
}
