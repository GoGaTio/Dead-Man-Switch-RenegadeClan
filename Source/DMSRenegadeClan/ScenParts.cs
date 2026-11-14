using Fortified.Structures;
using RimWorld;
using RimWorld.SketchGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Fortified;

namespace DMSRC
{
	public class ScenPart_Renegades : ScenPart
	{
		public bool startContacted = false;

		public int goodwill = 0;

		public FactionRelationKind relations = FactionRelationKind.Neutral;

		public bool enemyWithFleet;

		public IntRange contactInDaysRange = IntRange.Invalid;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref startContacted, "startContacted");
			Scribe_Values.Look(ref goodwill, "goodwill");
			Scribe_Values.Look(ref relations, "relations");
			Scribe_Values.Look(ref enemyWithFleet, "enemyWithFleet", defaultValue: false);
			Scribe_Values.Look(ref contactInDaysRange, "contactInDaysRange", defaultValue: IntRange.Invalid);
		}

		public override void PostWorldGenerate()
		{
			base.PostWorldGenerate();
			Apply();
		}

		public void Apply()
		{
			GameComponent_Renegades comp = GameComponent_Renegades.Find;
			if (comp == null)
			{
				return;
			}
			Faction fleet = comp.DMSFaction;
			Faction player = Faction.OfPlayerSilentFail;
			if (enemyWithFleet && fleet != null)
			{
				fleet.SetRelation(new FactionRelation(player, FactionRelationKind.Hostile) { baseGoodwill = -200 });
				Faction.OfPlayerSilentFail?.TryAffectGoodwillWith(fleet, -200, canSendMessage: false, canSendHostilityLetter: false, RCDefOf.DMSRC_AllyWithRenegades);
				comp.enemyWithFleet = true;
			}
			comp.PlayerRelation = relations;
			comp.playerGoodwill = goodwill;
			if (startContacted)
			{
				comp.contact = true;
			}
			else if (contactInDaysRange != IntRange.Invalid)
			{
				comp.ticksTillContact = contactInDaysRange.RandomInRange * 24;
			}
		}
	}
}