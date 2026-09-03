using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI.Group;

namespace DMSRC
{
	public class GenStep_BossHunt : GenStep
	{
		public override int SeedPart => 371433025;

		public PawnKindDef bossKind;

		public List<ThingDef> bossApparel;

		public List<PawnGenOption> escorts = new List<PawnGenOption>();

		public float pointsOffset;

		public override void Generate(Map map, GenStepParams parms)
		{
			List<Pawn> pawns = new List<Pawn>();
			Faction faction = Find.FactionManager.FirstFactionOfDef(bossKind.defaultFactionDef);
			Pawn boss = PawnGenerator.GeneratePawn(bossKind, faction, map.Tile);
			if(boss == null)
			{
				return;
			}
			if (!bossApparel.NullOrEmpty())
			{
				if (boss.apparel == null)
				{
					boss.apparel = new Pawn_ApparelTracker(boss);
				}
				for (int i = 0; i < bossApparel.Count; i++)
				{
					Apparel newApparel = (Apparel)ThingMaker.MakeThing(bossApparel[i]);
					boss.apparel.Wear(newApparel, dropReplacedApparel: true, locked: true);
				}
			}
			if (!escorts.NullOrEmpty())
			{
				float points = parms.sitePart.parms.threatPoints + pointsOffset;
				Log.Message(points);
				while (points > 0)
				{
					if (escorts.TryRandomElementByWeight((x) => x.selectionWeight, out var result))
					{
						Pawn pawn = PawnGenerator.GeneratePawn(result.kind, faction, map.Tile);
						Log.Message(pawn);
						pawns.Add(pawn);
						points -= result.Cost;
					}
					else
					{
						break;
					}
				}
			}
			IntVec3 center = IntVec3.Invalid;
			if (!RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(c => c.Standable(map), map, out center))
			{
				if(!CellRect.WholeMap(map).TryFindRandomCell(out center, c => c.Standable(map)))
				{
					return;
				}
			}
			Lord lord = LordMaker.MakeNewLord(faction, new LordJob_BossgroupAssaultColony(faction, center, Gen.YieldSingle(boss)), map, pawns);
			lord.AddPawn(boss);
			GenSpawn.Spawn(boss, center, map);
			foreach (Pawn p in pawns)
			{
				IntVec3 cell = CellFinder.RandomClosewalkCellNear(center, map, 15, c => c.Standable(map));
				Log.Message(cell);
				if (cell.IsValid)
				{
					Log.Message(GenSpawn.Spawn(p, cell, map));
				}
			}
		}
	}
}
