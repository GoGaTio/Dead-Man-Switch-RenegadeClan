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
using System.Security.Cryptography;
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
using Verse.Sound;
using static HarmonyLib.Code;
using static RimWorld.MechClusterSketch;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
using static UnityEngine.Scripting.GarbageCollector;

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
				fleet.SetRelation(new FactionRelation(player, FactionRelationKind.Hostile) { baseGoodwill = -200});
				Faction.OfPlayerSilentFail?.TryAffectGoodwillWith(fleet, -200, canSendMessage: false, canSendHostilityLetter: false, RCDefOf.DMSRC_AllyWithRenegades);
				comp.enemyWithFleet = true;
			}
			comp.PlayerRelation = relations;
			comp.playerGoodwill = goodwill;
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
						p.apparel.DestroyAll();
						p.inventory.DestroyAll();
					}
					else
					{
						pawns.Add(p);
					}
					things.Remove(t);
				}
			}
			IOverseer overseer = mechs.FirstOrDefault((x)=>x is IOverseer) as IOverseer;
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
			List<Thing> beacons = new List<Thing>();
			beacons.AddRange(generated.Where((x)=>x is Building_OrbitalTradeBeacon));
			foreach (Thing t in generated.ToList().InRandomOrder())
			{
				CellRect cellRect = new CellRect(t.Position.x - t.RotatedSize.x / 2 - 4, t.Position.z - t.RotatedSize.z / 2 - 4, t.RotatedSize.x + 8, t.RotatedSize.z + 8);
				cellRect.ClipInsideMap(t.Map);
				foreach (IntVec3 item in cellRect)
				{
					t.Map.areaManager.Home[item] = true;
				}
				if (t is Building_CryptosleepCasket)
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
				else if(t.def.building.maxItemsInCell > 1 && !t.def.preventDroppingThingsOn && beacons.Any((b)=> b.Position.InHorDistOf(t.Position, 7.9f)))
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
			GenStep_RPrefab.UpdateDesiredPowerOutputForAllGenerators(map);
		}
	}

	public class Reward_RenegadesGoodwill : Reward
	{
		public int amount;

		public override IEnumerable<GenUI.AnonymousStackElement> StackElements
		{
			get
			{
				GameComponent_Renegades comp = GameComponent_Renegades.Find;
				Faction faction = comp.RenegadesFaction;
				yield return QuestPartUtility.GetStandardRewardStackElement("Goodwill".Translate() + " " + amount.ToStringWithSign(), delegate (Rect r)
				{
					GUI.color = faction.Color;
					GUI.DrawTexture(r, faction.def.FactionIcon);
					GUI.color = Color.white;
				}, () => "GoodwillTip".Translate(faction, amount, -75, 75, comp.playerGoodwill, comp.PlayerRelation.GetLabelCap()).Resolve(), delegate
				{
					Find.WindowStack.Add(new Dialog_InfoCard(faction));
				});
			}
		}

		public override void InitFromValue(float rewardValue, RewardsGeneratorParams parms, out float valueActuallyUsed)
		{
			GameComponent_Renegades comp = GameComponent_Renegades.Find;
			amount = GenMath.RoundRandom(RewardsGenerator.RewardValueToGoodwillCurve.Evaluate(rewardValue));
			amount = Mathf.Min(amount, 100 - comp.playerGoodwill);
			amount = Mathf.Max(amount, 1);
			valueActuallyUsed = RewardsGenerator.RewardValueToGoodwillCurve.EvaluateInverted(amount);
			if (comp.RenegadesFaction.HostileTo(Faction.OfPlayer))
			{
				amount += Mathf.Clamp(-comp.playerGoodwill / 2, 0, amount);
				amount = Mathf.Min(amount, 100 - comp.playerGoodwill);
				if (amount < 1)
				{
					Log.Warning("Tried to use " + amount + " goodwill in Reward_Goodwill. A different reward type should have been chosen in this case.");
					amount = 1;
				}
			}
		}

		public override IEnumerable<QuestPart> GenerateQuestParts(int index, RewardsGeneratorParams parms, string customLetterLabel, string customLetterText, RulePack customLetterLabelRules, RulePack customLetterTextRules)
		{
			QuestPart_RenegadesGoodwillChange questPart_FactionGoodwillChange = new QuestPart_RenegadesGoodwillChange();
			questPart_FactionGoodwillChange.change = amount;
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

	public class QuestPart_RenegadesGoodwillChange : QuestPart
	{
		public int change;

		public string inSignal;

		public override void Notify_QuestSignalReceived(Signal signal)
		{
			base.Notify_QuestSignalReceived(signal);
			if (!(signal.tag == inSignal))
			{
				return;
			}
			Log.Message("test");
			GameComponent_Renegades.Find.OffsetGoodwill(change);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			//Scribe_Values.Look(ref inSignal, "inSignal");
			Scribe_Values.Look(ref change, "change", 0);
		}
	}

	public class PlaceWorker_ShowTurretRadius : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			VerbProperties verbProperties = ((ThingDef)checkingDef).building.turretGunDef.Verbs.Find((VerbProperties v) => typeof(Verb_FocusedBeam).IsAssignableFrom(v.verbClass));
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

	public class StockGenerator_ThingSetMaker : StockGenerator
	{
		public ThingSetMakerDef thingMakerDef;
		public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
		{
			ThingSetMakerParams parms = default(ThingSetMakerParams);
			parms.tile = forTile;
			parms.makingFaction = faction;
			return thingMakerDef.root.Generate(parms);
		}

		public override bool HandlesThingDef(ThingDef thingDef)
		{
			return thingMakerDef.root.AllGeneratableThingsDebug().Contains(thingDef);
		}
	}

	public class ChoiceLetter_RenegadesOffer : ChoiceLetter
	{
		public override bool CanDismissWithRightClick => false;

		public override IEnumerable<DiaOption> Choices
		{
			get
			{
				GameComponent_Renegades comp = GameComponent_Renegades.Find;
				if (base.ArchivedOnly || comp?.RenegadesFaction == null || comp.PlayerRelation == FactionRelationKind.Hostile || Faction.OfPlayerSilentFail == null)
				{
					yield return base.Option_Close;
					yield break;
				}
				DiaOption optionAccept = new DiaOption("Accept".Translate().CapitalizeFirst());
				optionAccept.action = delegate
				{
					comp.DMSFaction.SetRelation(new FactionRelation(Faction.OfPlayerSilentFail, comp.DMSFaction.PlayerRelationKind) { baseGoodwill = -200 });
					Faction.OfPlayerSilentFail?.TryAffectGoodwillWith(comp.DMSFaction, -200, canSendMessage: true, canSendHostilityLetter: true, RCDefOf.DMSRC_AllyWithRenegades);
					comp.PlayerRelation = FactionRelationKind.Ally;
					comp.ChangeGoodwill(15);
					comp.enemyWithFleet = true;
					Find.LetterStack.RemoveLetter(this);
				};
				optionAccept.resolveTree = true;
				yield return optionAccept;
				yield return Option_Reject;
				yield return Option_Postpone;
			}
		}
	}

	public class OverseerMechGizmo : Gizmo
	{
		public const int InRectPadding = 6;

		private const float Width = 130f;

		private const int IconButtonSize = 26;

		private const float BaseSelectedTexJump = 20f;

		private const float BaseSelectedTextScale = 0.8f;

		private static readonly CachedTexture PowerIcon = new CachedTexture("UI/Icons/MechRechargeSettings");

		private static readonly Color UncontrolledMechBackgroundColor = new Color32(byte.MaxValue, 25, 25, 55);

		private CompOverseerMech comp;

		public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions => GetWorkModeOptions(comp);

		public override bool Visible
		{
			get
			{
				return Find.Selector.SelectedPawns.Count == 1;
			}
		}

		public override float Order
		{
			get
			{
				return -90f;
			}
		}

		public OverseerMechGizmo(CompOverseerMech comp)
		{
			this.comp = comp;
			Order = -90f;
		}

		public static IEnumerable<FloatMenuOption> GetWorkModeOptions(CompOverseerMech comp)
		{
			foreach (MechWorkModeDef wm in DefDatabase<MechWorkModeDef>.AllDefsListForReading.OrderBy((MechWorkModeDef d) => d.uiOrder))
			{
				MechWorkModeDef wmLocal = wm;
				FloatMenuOption floatMenuOption = new FloatMenuOption(wmLocal.LabelCap, delegate
				{
					comp.SetWorkMode(wmLocal);
				}, wmLocal.uiIcon, Color.white);
				floatMenuOption.tooltip = new TipSignal(wmLocal.description, wmLocal.index ^ 0xDFE8661);
				yield return floatMenuOption;
			}
		}

		public override bool GroupsWith(Gizmo other)
		{
			return false;
		}

		public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
		{
			Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
			Rect inRect = rect.ContractedBy(6f);
			Widgets.DrawWindowBackground(rect);
			Rect rect1 = new Rect(inRect.x, inRect.y, 26f, 26f);
			Widgets.DrawTextureFitted(rect1, PowerIcon.Texture, 1f);
			if (!disabled && Mouse.IsOver(rect1))
			{
				Widgets.DrawHighlight(rect1);
				if (Widgets.ButtonInvisible(rect1))
				{
					Find.WindowStack.Add(new Dialog_OverseerRechargeSettings(comp));
				}
			}
			Rect rect2 = new Rect(inRect.x, inRect.yMax - 26f, 26f, 26f);
			Widgets.DrawTextureFitted(rect2, comp.WorkMode.uiIcon, 1f);
			if (!disabled && Mouse.IsOver(rect2))
			{
				Widgets.DrawHighlight(rect2);
				if (Widgets.ButtonInvisible(rect2))
				{
					Find.WindowStack.Add(new FloatMenu(GetWorkModeOptions(comp).ToList()));
				}
				if(Find.WindowStack.FloatMenu == null)
				{
					TooltipHandler.TipRegion(rect2, new TipSignal(("CurrentMechWorkMode".Translate() + ": " + comp.WorkMode.LabelCap).Colorize(ColoredText.TipSectionTitleColor) + "\n" + comp.WorkMode.description + "\n\n" + "ClickToChangeWorkMode".Translate()));
				}
			}
			return new GizmoResult(GizmoState.Clear);
		}

		public override float GetWidth(float maxWidth)
		{
			return 38f;
		}
	}
}