using Fortified.Structures;
using RimWorld;
using RimWorld.SketchGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using UnityEngine;
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

    public class ScenPart_StartingAMO : ScenPart
    {
        private PawnKindDef AMOKind;

        private IEnumerable<PawnKindDef> PossibleAMOs => DefDatabase<PawnKindDef>.AllDefs.Where((PawnKindDef td) => typeof(Fortified.ArtificialOrganism).IsAssignableFrom(td.race.thingClass));

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref AMOKind, "AMOKind");
        }

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            Rect scenPartRect = listing.GetScenPartRect(this, ScenPart.RowHeight);
            if (Widgets.ButtonText(new Rect(scenPartRect.xMin, scenPartRect.yMin, scenPartRect.width, ScenPart.RowHeight), (AMOKind != null) ? AMOKind.LabelCap : "Random".Translate()))
            {
                List<FloatMenuOption> list = new List<FloatMenuOption>();
                list.Add(new FloatMenuOption("Random".Translate().CapitalizeFirst(), delegate
                {
                    AMOKind = null;
                }));
                foreach (PawnKindDef possibleMech in PossibleAMOs)
                {
                    PawnKindDef localKind = possibleMech;
                    list.Add(new FloatMenuOption(localKind.LabelCap, delegate
                    {
                        AMOKind = localKind;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(list));
            }
        }

        public override void Randomize()
        {
            AMOKind = PossibleAMOs.RandomElement();
        }

        public override string Summary(Scenario scen)
        {
            return ScenSummaryList.SummaryWithList(scen, "PlayerStartsWith", ScenPart_StartingThing_Defined.PlayerStartWithIntro);
        }

        public override IEnumerable<string> GetSummaryListEntries(string tag)
        {
            if (tag == "PlayerStartsWith")
            {
                yield return "AMO: " + AMOKind.LabelCap;
            }
        }

        public override IEnumerable<Thing> PlayerStartingThings()
        {
            if (AMOKind == null)
            {
                AMOKind = PossibleAMOs.RandomElement();
            }
            Faction faction = Find.FactionManager?.FirstFactionOfDef(AMOKind.defaultFactionDef);
            PawnGenerationRequest request = new PawnGenerationRequest(AMOKind, faction, PawnGenerationContext.NonPlayer, null, forceGenerateNewPawn: false, allowDead: false, allowDowned: false, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: false, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, forceNoIdeo: false, forceNoBackstory: false, forbidAnyTitle: false, forceDead: false, null, null, null, null, null, 0f, DevelopmentalStage.Adult);
            Pawn AMO = PawnGenerator.GeneratePawn(request);
            AMO.SetFaction(Faction.OfPlayer);
            yield return AMO;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() ^ ((AMOKind != null) ? AMOKind.GetHashCode() : 0);
        }
    }
}