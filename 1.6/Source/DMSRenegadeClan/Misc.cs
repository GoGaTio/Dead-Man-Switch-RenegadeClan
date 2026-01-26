using DelaunatorSharp;
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
using Verse.Grammar;
using static HarmonyLib.Code;
using static RimWorld.MechClusterSketch;
using static UnityEngine.Scripting.GarbageCollector;

namespace DMSRC
{
	public class ScenPart_Renegades : ScenPart
	{
		public bool startContacted = false;

		public float will;

		public float opinion = 0;

		public FactionRelationKind relations = FactionRelationKind.Neutral;

		public bool enemyWithFleet;

		public IntRange contactInDaysRange = IntRange.Invalid;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref startContacted, "startContacted");
			Scribe_Values.Look(ref will, "will");
			Scribe_Values.Look(ref opinion, "opinion");
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
				Log.Error("Issue");
				return;
			}
			Faction fleet = comp.DMSFaction;
			Faction player = Faction.OfPlayerSilentFail;
			if(fleet == null)
			{
				Log.Message("fleet");
			}
			if (player == null)
			{
				Log.Message("player");
			}
			if (enemyWithFleet)
			{
				fleet.SetRelation(new FactionRelation(player, FactionRelationKind.Hostile) { baseGoodwill = -200});
				comp.enemyWithFleet = true;
			}
			comp.PlayerRelation = relations;
			comp.playerOpinion = opinion;
			if (startContacted)
			{
				comp.contacted = true;
			}
			else if(contactInDaysRange != IntRange.Invalid)
			{
				comp.hoursTillContact = contactInDaysRange.RandomInRange * 24;
			}
		}
	}

	public class GoodwillSituationWorker_Renegades : GoodwillSituationWorker
	{
		public override int GetNaturalGoodwillOffset(Faction other)
		{
			if (other.def == RCDefOf.DMS_Army && GameComponent_Renegades.Find.enemyWithFleet)
			{
				return -200;
			}
			return 0;
		}

		public override int GetMaxGoodwill(Faction other)
		{
			if (other.def == RCDefOf.DMS_Army && GameComponent_Renegades.Find.enemyWithFleet)
			{
				return -100;
			}
			return 100;
		}
	}

	public class ScenPart_PlayerArrivesPrefab : ScenPart
	{
		public List<RPrefabDef> prefabOptions = new List<RPrefabDef>();

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref prefabOptions, "prefabOptions", LookMode.Def);
		}

		public override void GenerateIntoMap(Map map)
		{
			if (Find.GameInitData == null || prefabOptions.NullOrEmpty())
			{
				return;
			}
			RPrefabDef prefab = prefabOptions.RandomElement();
			List<Thing> things = new List<Thing>();
			List<Pawn> pawns = new List<Pawn>();
			List<Pawn> mechs = new List<Pawn>();
			foreach (ScenPart allPart in Find.Scenario.AllParts)
			{
				things.AddRange(allPart.PlayerStartingThings());
			}
			foreach (Pawn startingPawn in Find.GameInitData.startingAndOptionalPawns)
			{
				pawns.Add(startingPawn);
				foreach (ThingDefCount item in Find.GameInitData.startingPossessions[startingPawn])
				{
					startingPawn.inventory.GetDirectlyHeldThings().TryAdd(StartingPawnUtility.GenerateStartingPossession(item));
				}
			}
			foreach(Thing t in things.ToList())
			{
				if(t is Pawn p)
				{
					if (p.RaceProps.IsMechanoid)
					{
						mechs.Add(p);
						p.equipment.DestroyAllEquipment();
					}
					else
					{
						pawns.Add(p);
					}
					things.Remove(t);
				}
			}
			OverseerMech overseer = mechs.FirstOrDefault((x)=>x is OverseerMech) as OverseerMech;
			overseer.Comp.UpdateDummy();
			if (overseer != null)
			{
				foreach(Pawn p in mechs)
				{
					if(p != overseer)
					{
						overseer.Comp.Connect(p, overseer.Comp.dummyPawn);
					}
				}
			}
			IntVec3 spot = MapGenerator.PlayerStartSpot;
			List<Thing> generated = new List<Thing>();
			Rot4 rot = Rot4.Random;
			IntVec3 root = PrefabUtility.GetRoot(prefab, spot, rot);
			Thing.allowDestroyNonDestroyable = true;
			prefab.Generate(spot, rot, map, Faction.OfPlayerSilentFail, ref generated);
			List<IntVec3> itemCells = new List<IntVec3>();
			List<IntVec3> spawnCells = new List<IntVec3>();
			foreach (Thing t in generated.ToList().InRandomOrder())
			{
				if(t is Building_AncientCryptosleepCasket)
				{
					spawnCells.Add(t.InteractionCell);
				}
				if (t.TryGetComp<CompRefuelable>(out var comp))
				{
					if (comp.Props.fuelIsMortarBarrel)
					{
						comp.Refuel(comp.Props.fuelCapacity - comp.Fuel);
					}
					else
					{
						comp.ConsumeFuel(comp.Fuel);
					}
				}
				else if(t is InactiveMech m)
				{
					if (!mechs.NullOrEmpty())
					{
						Pawn mech = mechs.RandomElement();
						mech.Rotation = Rot4.Random;
						m.innerContainer.Clear();
						m.innerContainer.TryAddOrTransfer(mech);
						mechs.Remove(mech);
					}
					else
					{
						m.Destroy();
					}
				}
				else if(t.def.building.maxItemsInCell > 1 && !t.def.preventDroppingThingsOn)
				{
					itemCells.AddRange(t.OccupiedRect().Cells);
				}
			}
			foreach (Thing thing in things)
			{
				GenPlace.TryPlaceThing(thing, itemCells.RandomElement(), map, ThingPlaceMode.Near);
			}
			if (!mechs.NullOrEmpty())
			{
				pawns.AddRange(mechs);
			}
			foreach(Pawn p in pawns)
			{
				IntVec3 c = spawnCells.RandomElement();
				GenSpawn.Spawn(p, c, map);
				spawnCells.Remove(c);
			}
			map.powerNetManager.UpdatePowerNetsAndConnections_First();
		}
	}

	public class Reward_RenegadesRep : Reward
	{
		public float amount;

		public override IEnumerable<GenUI.AnonymousStackElement> StackElements
		{
			get
			{
				Faction faction = GameComponent_Renegades.Find.RenegadesFaction;
				yield return QuestPartUtility.GetStandardRewardStackElement("Goodwill".Translate() + " " + amount.ToStringWithSign(), delegate (Rect r)
				{
					GUI.color = faction.Color;
					GUI.DrawTexture(r, faction.def.FactionIcon);
					GUI.color = Color.white;
				}, () => "GoodwillTip".Translate(faction, amount, -75, 75, faction.PlayerGoodwill, faction.PlayerRelationKind.GetLabelCap()).Resolve(), delegate
				{
					Find.WindowStack.Add(new Dialog_InfoCard(faction));
				});
			}
		}

		public override void InitFromValue(float rewardValue, RewardsGeneratorParams parms, out float valueActuallyUsed)
		{
			amount = GenMath.RoundRandom(RewardsGenerator.RewardValueToGoodwillCurve.Evaluate(rewardValue));
			amount = Mathf.Min(amount, 100 - parms.giverFaction.PlayerGoodwill);
			amount = Mathf.Max(amount, 1);
			valueActuallyUsed = RewardsGenerator.RewardValueToGoodwillCurve.EvaluateInverted(amount);
			if (parms.giverFaction.HostileTo(Faction.OfPlayer))
			{
				amount += Mathf.Clamp(-parms.giverFaction.PlayerGoodwill / 2, 0, amount);
				amount = Mathf.Min(amount, 100 - parms.giverFaction.PlayerGoodwill);
				if (amount < 1)
				{
					Log.Warning("Tried to use " + amount + " goodwill in Reward_Goodwill. A different reward type should have been chosen in this case.");
					amount = 1;
				}
			}
		}

		public override IEnumerable<QuestPart> GenerateQuestParts(int index, RewardsGeneratorParams parms, string customLetterLabel, string customLetterText, RulePack customLetterLabelRules, RulePack customLetterTextRules)
		{
			Faction faction = GameComponent_Renegades.Find.RenegadesFaction;
			QuestPart_FactionGoodwillChange questPart_FactionGoodwillChange = new QuestPart_FactionGoodwillChange();
			questPart_FactionGoodwillChange.change = 10;
			questPart_FactionGoodwillChange.faction = faction;
			questPart_FactionGoodwillChange.inSignal = RimWorld.QuestGen.QuestGen.slate.Get<string>("inSignal");
			yield return questPart_FactionGoodwillChange;
		}

		public override string GetDescription(RewardsGeneratorParams parms)
		{
			Faction faction = GameComponent_Renegades.Find.RenegadesFaction;
			return "Reward_Goodwill".Translate(faction, amount).Resolve();
		}

		public override string ToString()
		{
			return GetType().Name + " (faction=" + ", amount=" + amount + ")";
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref amount, "amount", 0);
		}
	}

	public class HediffCompProperties_NeuroControl : HediffCompProperties
	{
		public ThingDef customMote;
		public HediffCompProperties_NeuroControl()
		{
			compClass = typeof(HediffComp_NeuroControl);
		}
	}
	public class HediffComp_NeuroControl : HediffComp
	{
		public Pawn controller;

		private MoteDualAttached mote;

		public HediffCompProperties_NeuroControl Props => props as HediffCompProperties_NeuroControl;

		public override string CompLabelInBracketsExtra => controller?.Name?.ToStringShort ?? controller?.LabelCap ?? null;

		public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
		{
			base.Notify_PawnDied(dinfo, culprit);
			Hediff_NeuroControlChip h = controller.health.hediffSet.GetFirstHediff<Hediff_NeuroControlChip>();
			if(h != null)
			{
				h.RemovePawn(parent.pawn);
			}
			base.Pawn.health.RemoveHediff(parent);
		}

		public override void CompPostPostRemoved()
		{
			Hediff_NeuroControlChip h = controller.health.hediffSet.GetFirstHediff<Hediff_NeuroControlChip>();
			if (h != null)
			{
				h.RemovePawn(parent.pawn);
			}
			base.CompPostPostRemoved();
		}

		public override void CompPostTick(ref float severityAdjustment)
		{
			if (controller.MapHeld == parent.pawn.MapHeld)
			{
				ThingDef moteDef = Props.customMote ?? ThingDefOf.Mote_PsychicLinkLine;
				if (mote == null)
				{
					mote = MoteMaker.MakeInteractionOverlay(moteDef, parent.pawn, controller);
				}
				mote.Maintain();
			}
		}

		public override void CompExposeData()
		{
			base.CompExposeData();
			Scribe_References.Look(ref controller, "DMSRC_controller", saveDestroyedThings: true);
		}
	}
	public class Hediff_NeuroChip : Hediff_Level
	{
		public int disabledLevels = 0;
		public override HediffStage CurStage => disabledLevels > 0 ? def.stages[CurStageIndex - disabledLevels] : base.CurStage;

		public int Level => level - disabledLevels;

		public override string Label
		{
			get
			{
				if (!def.levelIsQuantity)
				{
					return def.label + " (" + "LevelNum".Translate(Level).ToString() + ")";
				}
				return def.label + " x" + Level;
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref disabledLevels, "disabledLevels");
		}

		public static void Recalculate(Pawn p)
		{
			if (p.health.hediffSet.GetFirstHediff<Hediff_Neurointerface>() is Hediff_Neurointerface inter && inter != null)
			{
				int usedCapacity = inter.UsedCapacity;
				int capacity = inter.Capacity;
				foreach (Hediff h in p.health.hediffSet.hediffs)
				{
					if (h is Hediff_NeuroChip chip && chip.disabledLevels > 0 && capacity - usedCapacity > 0)
					{
						if (capacity - usedCapacity > chip.disabledLevels)
						{
							usedCapacity += chip.disabledLevels;
							chip.disabledLevels = 0;
						}
						else
						{
							chip.disabledLevels -= capacity - usedCapacity;
							break;
						}
					}
				}
			}
		}

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			return base.SpecialDisplayStats(req);
		}
	}

	public class Hediff_NeuroControlChip : Hediff_NeuroChip
	{
		public override string Label
		{
			get
			{
				string s = base.Label;
				return s + " (" + "DMSRC_Controlled".Translate(controlledPawns.Count, Capacity) + ")";
			}
		}
		public int Capacity => Mathf.RoundToInt(pawn.GetStatValue(RCDefOf.DMSRC_NeuroControlPower));

		public List<Pawn> controlledPawns = new List<Pawn>();

		public void ControlPawn(Pawn pawn)
		{
			controlledPawns.Add(pawn);
			Hediff h = pawn.health.AddHediff(RCDefOf.DMSRC_NeuroControl, pawn.health.hediffSet.GetBrain());
			if (h.TryGetComp<HediffComp_NeuroControl>(out var comp))
			{
				comp.controller = this.pawn;
			}
		}

		public void RemovePawn(Pawn pawn)
		{
			if(pawn.health.hediffSet.TryGetHediff(RCDefOf.DMSRC_NeuroControl, out var h))
			{
				pawn.health.RemoveHediff(h);
			}
			controlledPawns.Remove(pawn);
		}

		public void RemoveAll()
		{
			foreach(Pawn p in controlledPawns.ToList())
			{
				RemovePawn(p);
			}
		}

		public override void Notify_PawnKilled()
		{
			RemoveAll();
			base.Notify_PawnKilled();
		}

		public override void Notify_Downed()
		{
			RemoveAll();
			base.Notify_Downed();
		}

		public override void PreRemoved()
		{
			RemoveAll();
			base.PreRemoved();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref controlledPawns, "DMSRC_controlledPawns", LookMode.Reference);
			if(Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				controlledPawns.RemoveAll((p) => p == null || p.Destroyed || p.Dead);
			}
		}
	}

	public class Hediff_Neurointerface : Hediff_Implant
	{
		public int Capacity => Mathf.RoundToInt(pawn.GetStatValue(RCDefOf.DMSRC_Neurocapacity));

		public int UsedCapacity
        {
            get
            {
				int num = 0;
				foreach(Hediff item in pawn.health.hediffSet.hediffs)
                {
					if(item is Hediff_NeuroChip chip)
                    {
						num += chip.Level;
                    }
                }
				return num;
            }
        }
        public override string LabelInBrackets => UsedCapacity + "/" + Capacity;
        public override void PostAdd(DamageInfo? dinfo)
		{
			base.PostAdd(dinfo);
			if (base.Part == null)
			{
				Log.Error(def.defName + " has null Part. It should be set before PostAdd.");
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			if (Scribe.mode == LoadSaveMode.PostLoadInit && base.Part == null)
			{
				Log.Error(GetType().Name + " has null part after loading.");
				pawn.health.hediffSet.hediffs.Remove(this);
			}
		}

		public override void PreRemoved()
		{
			base.PreRemoved();
			Hediff_ProcessorHelmet hediff = pawn.health.hediffSet.GetFirstHediff<Hediff_ProcessorHelmet>();
			if(hediff != null)
			{
				hediff.activeInt = null;
			}
		}
	}

	public class PlaceWorker_ShowTurretRadius : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			VerbProperties verbProperties = ((ThingDef)checkingDef).building.turretGunDef.Verbs.Find((VerbProperties v) => typeof(Verb_ShootBeam).IsAssignableFrom(v.verbClass));
			if (verbProperties.range > 0f)
			{
				GenDraw.DrawRadiusRing(loc, verbProperties.range);
			}
			if (verbProperties.minRange > 0f)
			{
				GenDraw.DrawRadiusRing(loc, verbProperties.minRange);
			}
			return true;
		}
	}

	public class ForceNotRemoveExtension : DefModExtension
	{

	}

	public class ThingSetMaker_CountDifferent : ThingSetMaker
	{
		protected override bool CanGenerateSub(ThingSetMakerParams parms)
		{
			if (!AllowedThingDefs(parms).Any())
			{
				return false;
			}
			if (parms.countRange.HasValue && parms.countRange.Value.max <= 0)
			{
				return false;
			}
			if (parms.maxTotalMass.HasValue && parms.maxTotalMass != float.MaxValue && !ThingSetMakerUtility.PossibleToWeighNoMoreThan(AllowedThingDefs(parms), parms.techLevel ?? TechLevel.Undefined, parms.maxTotalMass.Value, (!parms.countRange.HasValue) ? 1 : parms.countRange.Value.max))
			{
				return false;
			}
			return true;
		}

		protected override void Generate(ThingSetMakerParams parms, List<Thing> outThings)
		{
			IEnumerable<ThingDef> enumerable = AllowedThingDefs(parms);
			if (!enumerable.Any())
			{
				return;
			}
			TechLevel stuffTechLevel = parms.techLevel ?? TechLevel.Undefined;
			IntRange intRange = parms.countRange ?? IntRange.One;
			float num = parms.maxTotalMass ?? float.MaxValue;
			int num2 = Mathf.Max(intRange.RandomInRange, 1);
			float num3 = 0f;
			for (int i = 0; i < num2; i++)
			{
				if (!ThingSetMakerUtility.TryGetRandomThingWhichCanWeighNoMoreThan(enumerable, stuffTechLevel, (num == float.MaxValue) ? float.MaxValue : (num - num3), parms.qualityGenerator, out var thingStuffPair))
				{
					break;
				}
				Thing thing = ThingMaker.MakeThing(thingStuffPair.thing, thingStuffPair.stuff);
				ThingSetMakerUtility.AssignQuality(thing, parms.qualityGenerator);
				outThings.Add(thing);
				if (!(thing is Pawn))
				{
					num3 += thing.GetStatValue(StatDefOf.Mass) * (float)thing.stackCount;
				}
			}
		}

		protected virtual IEnumerable<ThingDef> AllowedThingDefs(ThingSetMakerParams parms)
		{
			return ThingSetMakerUtility.GetAllowedThingDefs(parms);
		}

		protected override IEnumerable<ThingDef> AllGeneratableThingsDebugSub(ThingSetMakerParams parms)
		{
			TechLevel techLevel = parms.techLevel ?? TechLevel.Undefined;
			foreach (ThingDef item in AllowedThingDefs(parms))
			{
				if (!parms.maxTotalMass.HasValue || parms.maxTotalMass == float.MaxValue || !(ThingSetMakerUtility.GetMinMass(item, techLevel) > parms.maxTotalMass))
				{
					yield return item;
				}
			}
		}
	}

	/*public class HediffCompProperties_Reveal : HediffCompProperties
	{
		public HediffCompProperties_Reveal()
		{
			compClass = typeof(HediffComp_Reveal);
		}
	}
	public class HediffComp_Reveal : HediffComp
	{
		public HediffCompProperties_Reveal Props => (HediffCompProperties_Reveal)props;

		[Unsaved(false)]
		private HediffComp_Invisibility invisibility;

		private int lastDetectedTick = -99999;

		private static float lastNotified = -99999f;

		private HediffComp_Invisibility Invisibility => invisibility ?? (invisibility = parent.TryGetComp<HediffComp_Invisibility>());


        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
			if (Invisibility == null)
			{
				return;
			}
			if (!Pawn.Spawned || Invisibility.PsychologicallyVisible)
			{
				return;
			}
			if (Pawn.IsHashIntervalTick(7))
			{
				if (Find.TickManager.TicksGame > lastDetectedTick + 1200)
				{
					CheckDetected();
				}
				if (Find.TickManager.TicksGame > lastDetectedTick + 1200)
				{
					Invisibility.BecomeInvisible();
				}
			}
		}

		private void CheckDetected()
		{
			foreach (Pawn item in Pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn))
			{
				if (PawnCanDetect(item))
				{
					BecomeVisible();
				}
			}
		}

		private bool PawnCanDetect(Pawn pawn)
		{
			if (pawn.Downed || !pawn.Awake())
			{
				return false;
			}
			if (pawn.Faction == Pawn.Faction || !GenHostility.HostileTo(pawn, Pawn))
			{
				return false;
			}
			if (!Pawn.Position.InHorDistOf(pawn.Position, GetPawnSightRadius(pawn, Pawn)))
			{
				return false;
			}
			return GenSight.LineOfSightToThing(pawn.Position, Pawn, Pawn.Map);
		}

		private static float GetPawnSightRadius(Pawn pawn, Pawn hidden)
		{
			float num = 14f;
			if (pawn.genes == null || pawn.genes.AffectedByDarkness)
			{
				float t = hidden.Map.glowGrid.GroundGlowAt(hidden.Position);
				num *= Mathf.Lerp(0.33f, 1f, t);
			}
			return num * pawn.health.capacities.GetLevel(PawnCapacityDefOf.Sight);
		}

        public override void Notify_PawnUsedVerb(Verb verb, LocalTargetInfo target)
        {
            base.Notify_PawnUsedVerb(verb, target);
            if (target != Pawn && !Invisibility.PsychologicallyVisible)
            {
				BecomeVisible();
			}
		}

		private void BecomeVisible()
		{
			Invisibility.BecomeVisible();
			if(Pawn.Faction != Faction.OfPlayer)
            {
				bool threat = GenHostility.HostileTo(Pawn, Faction.OfPlayer);
				if (RealTime.LastRealTime > lastNotified + 60f)
				{
					Find.LetterStack.ReceiveLetter("LetterLabelSightstealerRevealed".Translate(), "LetterSightstealerRevealed".Translate(), threat ? LetterDefOf.ThreatBig : LetterDefOf.NeutralEvent, Pawn, null, null, null, null, 6);
				}
				else
				{
					Messages.Message("MessageSightstealerRevealed".Translate(), Pawn, threat ? MessageTypeDefOf.ThreatBig : MessageTypeDefOf.NeutralEvent);
				}
			}
			lastNotified = RealTime.LastRealTime;
			lastDetectedTick = Find.TickManager.TicksGame;
		}
	}*/
}