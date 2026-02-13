using DelaunatorSharp;
using Gilzoide.ManagedJobs;
using HarmonyLib;
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
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
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
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
using static UnityEngine.Scripting.GarbageCollector;

namespace DMSRC
{
	public class PlaceWorker_WetwareProducer : PlaceWorker
	{
		public override void DrawGhost(ThingDef def, IntVec3 loc, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			rot = Rot4.North;
			GhostUtility.GhostGraphicFor(GraphicDatabase.Get<Graphic_Single>("Things/Building/DMSRC_WetwareProducer_Glass", ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white), def, ghostCol).DrawFromDef(GenThing.TrueCenter(loc, rot, def.Size, AltitudeLayer.MetaOverlays.AltitudeFor()), rot, def);
			GhostUtility.GhostGraphicFor(GraphicDatabase.Get<Graphic_Single>("Things/Building/DMSRC_WetwareProducer_Top", ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white), def, ghostCol).DrawFromDef(GenThing.TrueCenter(loc, rot, def.Size, AltitudeLayer.MetaOverlays.AltitudeFor()), rot, def);
		}

		public override void PostPlace(Map map, BuildableDef def, IntVec3 loc, Rot4 rot)
		{
			base.PostPlace(map, def, loc, rot);
			GameComponent_Renegades.Find?.ForbiddenTechMessage();
		}
	}
	public class ProducerExtension : DefModExtension
	{
		public List<ThingDefCountClass> options = new List<ThingDefCountClass>();
	}
	public class Hediff_WetwarePregnancy : HediffWithComps
	{
		public int notWorkingTicks = -1;

		public Building_WetwareProducer Holder => pawn?.ParentHolder as Building_WetwareProducer;

		public float tickOffset = 0f;

		public override bool ShouldRemove => Holder == null;

		public override void Tick()
		{
			if(notWorkingTicks >= 0)
			{
				notWorkingTicks++;
			}
			if (pawn.IsHashIntervalTick(250))
			{
				tickOffset = PawnUtility.BodyResourceGrowthSpeed(pawn) * Mathf.Max(pawn.GetStatValue(StatDefOf.Fertility), 0.5f) / (pawn.RaceProps.gestationPeriodDays * 30000f);
			}
			base.Tick();
		}
		public override void TickInterval(int delta)
		{
			base.TickInterval(delta);
			if(notWorkingTicks >= 0)
			{
				return;
			}
			float num = tickOffset;
			Severity += num * (float)delta;
			if (Severity < 1f)
			{
				return;
			}
			Holder.Notify_PregnancyEnded();
			pawn.health.RemoveHediff(this);
		}

		public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
		{
			base.Notify_PawnDied(dinfo, culprit);
			Building_WetwareProducer holder = pawn.SpawnedParentOrMe as Building_WetwareProducer;
			if(holder != null)
			{
				holder.Notify_PawnDied();
			}
		}

		public override void PostAdd(DamageInfo? dinfo)
		{
			tickOffset = PawnUtility.BodyResourceGrowthSpeed(pawn) * pawn.GetStatValue(StatDefOf.Fertility) / (pawn.RaceProps.gestationPeriodDays * 30000f);
			base.PostAdd(dinfo);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref notWorkingTicks, "notWorkingTicks", -1);
		}
	}

	[StaticConstructorOnStartup]
	public class Building_WetwareProducer : Building_Enterable, IStoreSettingsParent, IThingHolderWithDrawnPawn, IThingHolder
	{
		public int cooldownTicks = -1;

		[Unsaved(false)]
		private Effecter bubbleEffecter;

		private float containedNutrition;

		public ThingDef product;

		public override bool IsContentsSuspended => false;

		public Hediff_WetwarePregnancy Pregnancy => SelectedPawn.health.hediffSet.GetFirstHediff<Hediff_WetwarePregnancy>();

		public List<ThingDefCountClass> Options => def.GetModExtension<ProducerExtension>()?.options;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref containedNutrition, "containedNutrition", 0f);
			Scribe_Deep.Look(ref allowedNutritionSettings, "allowedNutritionSettings", this);
			Scribe_Defs.Look(ref product, "product");
			if (allowedNutritionSettings == null)
			{
				allowedNutritionSettings = new StorageSettings(this);
				if (def.building.defaultStorageSettings != null)
				{
					allowedNutritionSettings.CopyFrom(def.building.defaultStorageSettings);
				}
			}
		}

		//IThingHolderWithDrawnPawn
		public float HeldPawnDrawPos_Y => DrawPos.y + 0.03658537f;

		public float HeldPawnBodyAngle => Rot4.North.AsAngle;

		public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;

		public override Vector3 PawnDrawOffset => new Vector3(0, 0, 0.15f) + CompBiosculpterPod.FloatingOffset(Find.TickManager.TicksGame);

		//IStoreSettingsParent
		public bool StorageTabVisible => true;

		private StorageSettings allowedNutritionSettings;

		public StorageSettings GetStoreSettings()
		{
			return allowedNutritionSettings;
		}

		public StorageSettings GetParentStoreSettings()
		{
			return def.building.fixedStorageSettings;
		}

		public void Notify_SettingsChanged()
		{
		}

		//UiIcons
		private static readonly Texture2D CancelLoadingIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

		private static readonly CachedTexture InsertPawnIcon = new CachedTexture("UI/Gizmos/InsertPawn");

		//Graphic
		private Graphic GlassGraphic
		{
			get
			{
				if (cachedGlassGraphic == null)
				{
					cachedGlassGraphic = GraphicDatabase.Get<Graphic_Single>("Things/Building/DMSRC_WetwareProducer_Glass", ShaderDatabase.Transparent, def.graphicData.drawSize, Color.white);
				}
				return cachedGlassGraphic;
			}
		}

		[Unsaved(false)]
		private Graphic cachedGlassGraphic;

		private Graphic TopGraphic
		{
			get
			{
				if (cachedTopGraphic == null)
				{
					cachedTopGraphic = GraphicDatabase.Get<Graphic_Single>("Things/Building/DMSRC_WetwareProducer_Top", ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white);
				}
				return cachedTopGraphic;
			}
		}

		[Unsaved(false)]
		private Graphic cachedTopGraphic;

		//Power
		[Unsaved(false)]
		private CompPowerTrader cachedPowerComp;
		
		public bool PowerOn => PowerTraderComp.PowerOn;

		private CompPowerTrader PowerTraderComp
		{
			get
			{
				if (cachedPowerComp == null)
				{
					cachedPowerComp = this.TryGetComp<CompPowerTrader>();
				}
				return cachedPowerComp;
			}
		}

		//Refuel
		[Unsaved(false)]
		private CompRefuelable cachedRefuelableComp;

		public bool Fueled => RefuelableComp.HasFuel;

		private CompRefuelable RefuelableComp
		{
			get
			{
				if (cachedRefuelableComp == null)
				{
					cachedRefuelableComp = this.TryGetComp<CompRefuelable>();
				}
				return cachedRefuelableComp;
			}
		}

		public float NutritionStored
		{
			get
			{
				float num = containedNutrition;
				for (int i = 0; i < innerContainer.Count; i++)
				{
					Thing thing = innerContainer[i];
					num += (float)thing.stackCount * thing.GetStatValue(StatDefOf.Nutrition);
				}
				return num;
			}
		}

		public float NutritionConsumedPerDay
		{
			get
			{
				Need_Food need = selectedPawn?.needs?.food;
				if (need == null)
				{
					return 0;
				}
				if (need.Starving)
				{
					return Need_Food.BaseHungerRate(selectedPawn.ageTracker.CurLifeStage, selectedPawn.def) * selectedPawn.health.hediffSet.GetHungerRateFactor(HediffDefOf.Malnutrition) * (selectedPawn.story?.traits?.HungerRateFactor ?? 1f);
				}
				return need.FoodFallPerTickAssumingCategory(need.CurCategory) * 60000;
			}
		}

		public float NutritionNeeded => NutritionConsumedPerDay * 3f - NutritionStored;

		public bool CanAcceptNutrition(Thing thing)
		{
			return allowedNutritionSettings.AllowedToAccept(thing);
		}

		public override AcceptanceReport CanAcceptPawn(Pawn pawn)
		{
			if (!pawn.RaceProps.Humanlike || pawn.BodySize > 1.5f)
			{
				return false;
			}
			if (selectedPawn != null && innerContainer.Contains(selectedPawn))
			{
				return "Occupied".Translate();
			}
			if (!PowerOn)
			{
				return "NoPower".Translate().CapitalizeFirst();
			}
			if (!Fueled)
			{
				return "NoFuel".Translate().CapitalizeFirst();
			}
			if (pawn.gender != Gender.Female)
			{
				return "DMSRC_NotAFemale".Translate(pawn.Named("PAWN")).Resolve();
			}
			if (!pawn.ageTracker.CurLifeStage.reproductive)
			{
				return "PawnIsTooYoung".Translate(pawn.Named("PAWN")).Resolve();
			}
			if (pawn.GetStatValue(StatDefOf.Fertility) <= 0f)
			{
				return "PawnIsInfertile".Translate(pawn.Named("PAWN")).Resolve();
			}
			if (pawn.Sterile())
			{
				return "PawnIsSterile".Translate(pawn.Named("PAWN")).Resolve();
			}
			if (selectedPawn != null && selectedPawn != pawn)
			{
				return "WaitingForPawn".Translate(selectedPawn.Named("PAWN"));
			}
			if (pawn.health.hediffSet.HasHediff(HediffDefOf.BioStarvation))
			{
				return "PawnBiostarving".Translate(pawn.Named("PAWN"));
			}
			return !pawn.IsSubhuman && (this.Faction != Faction.OfPlayerSilentFail || (!pawn.IsQuestLodger() && (pawn.IsColonist || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony)));
		}

		public override void TryAcceptPawn(Pawn pawn)
		{
			if (selectedPawn == null || !CanAcceptPawn(pawn))
			{
				return;
			}
			selectedPawn = pawn;
			bool num = pawn.DeSpawnOrDeselect();
			if (innerContainer.TryAddOrTransfer(pawn))
			{
				SoundDefOf.GrowthVat_Close.PlayOneShot(SoundInfo.InMap(this));
				Start();
			}
			if (num)
			{
				Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
			}
		}

		public void Notify_PregnancyEnded()
		{
			startTick = -1;
			cooldownTicks = 60000;
			Thing result = ThingMaker.MakeThing(product);
			result.stackCount = Options.FirstOrDefault((x) => x.thingDef == product).count;
			if (GenDrop.TryDropSpawn(result, InteractionCell, Map, ThingPlaceMode.Near, out var _) && Faction == Faction.OfPlayerSilentFail)
			{
				Find.LetterStack.ReceiveLetter("DMSRC_WetwareProducedLabel".Translate(result.Label).CapitalizeFirst(), "DMSRC_WetwareProducedDesc".Translate(result.Label, selectedPawn).CapitalizeFirst(), LetterDefOf.PositiveEvent, result, hyperlinkThingDefs: new List<ThingDef>() { result.def }, delayTicks: 2);
				if (Rand.Chance(0.35f))
				{
					GameComponent_Renegades.Find?.UsedForbiddenTech();
				}
			}
		}

		public void Notify_PawnDied()
		{
			innerContainer.TryDropAll(InteractionCell, base.Map, ThingPlaceMode.Near);
			startTick = -1;
			selectedPawn = null;
		}

		public override void PostMake()
		{
			base.PostMake();
			allowedNutritionSettings = new StorageSettings(this);
			if (def.building.defaultStorageSettings != null)
			{
				allowedNutritionSettings.CopyFrom(def.building.defaultStorageSettings);
			}
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			if (innerContainer.dontTickContents)
			{
				innerContainer.dontTickContents = false;
			}
			if(product == null)
			{
				product = Options?.First()?.thingDef;
				if (product == null)
				{
					product = DefDatabase<ThingDef>.GetNamed("Neurocomputer");
				}
			}
		}

		public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
		{
			if (selectedPawn != null && innerContainer.Contains(selectedPawn))
			{
				selectedPawn.Drawer.renderer.DynamicDrawPhaseAt(phase, drawLoc + PawnDrawOffset, null, neverAimWeapon: true);
			}
			base.DynamicDrawPhaseAt(phase, drawLoc, flip);
		}

		protected override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			base.DrawAt(drawLoc, flip);
			GlassGraphic.Draw(DrawPos + Altitudes.AltIncVect * 2f, Rot4.North, this);
			TopGraphic.Draw(DrawPos + Altitudes.AltIncVect * 3f, Rot4.North, this);
		}

		protected override void Tick()
		{
			base.Tick();
			if (this.IsHashIntervalTick(250))
			{
				PowerTraderComp.PowerOutput = (base.Working ? (0f - base.PowerComp.Props.PowerConsumption) : (0f - base.PowerComp.Props.idlePowerDraw));
			}
			Pawn pawn = selectedPawn;
			if (pawn == null || innerContainer == null || !innerContainer.Contains(pawn))
			{
				cooldownTicks = -1;
				startTick = -1;
				bubbleEffecter?.Cleanup();
				bubbleEffecter = null;
				return;
			}
			if (this.IsHashIntervalTick(250))
			{
				Need_Food need = pawn.needs?.food;
				if(need != null)
				{
					if(Pregnancy != null)
					{
						if (Pregnancy.notWorkingTicks > -1)
						{
							if (!need.Starving && PowerOn && Fueled)
							{
								Pregnancy.notWorkingTicks = -1;
								if(Faction == Faction.OfPlayerSilentFail)
								{
									Messages.Message("DMSRC_MessagePregnancyReenabled".Translate(pawn.Named("PAWN"), this.Named("BUILDING")), this, MessageTypeDefOf.NeutralEvent, historical: true);
								}
							}
						}
						else if (need.Starving || !PowerOn || !Fueled)
						{
							Pregnancy.notWorkingTicks = 0;
							if (Faction == Faction.OfPlayerSilentFail)
							{
								Messages.Message("DMSRC_MessagePregnancyDisabled".Translate(pawn.Named("PAWN"), this.Named("BUILDING")), this, MessageTypeDefOf.NegativeHealthEvent, historical: true);
							}
						}
					}
					float needed = need.NutritionWanted;
					if(needed > 0f)
					{
						if (needed <= containedNutrition)
						{
							need.CurLevel = need.MaxLevel;
							containedNutrition -= needed;
						}
						else
						{
							need.CurLevel += containedNutrition;
							containedNutrition = 0;
						}
						if (containedNutrition <= 0)
						{
							TryAbsorbNutritiousThing();
						}
					}
				}
			}
			if (bubbleEffecter == null)
			{
				bubbleEffecter = RCDefOf.DMSRC_WetwareProducer_Bubbles.SpawnAttached(this, base.MapHeld);
			}
			bubbleEffecter.EffectTick(this, this);
			if (base.Working)
			{
				RefuelableComp.ConsumeFuel(RefuelableComp.Props.fuelConsumptionRate / 60000f);
			}
			else
			{
				cooldownTicks--;
				if (cooldownTicks <= 0)
				{
					Start();
				}
			}
		}

		public void Start()
		{
			startTick = Find.TickManager.TicksGame;
			Hediff_WetwarePregnancy hediff = SelectedPawn.health.GetOrAddHediff(RCDefOf.DMSRC_ArtificalPregnancy) as Hediff_WetwarePregnancy;
			hediff.Severity = 0;
		}

		private void TryAbsorbNutritiousThing()
		{
			for (int i = 0; i < innerContainer.Count; i++)
			{
				if (innerContainer[i] != selectedPawn && innerContainer[i].def != ThingDefOf.Xenogerm && innerContainer[i].def != ThingDefOf.HumanEmbryo && !Options.Any((x)=> x.thingDef == innerContainer[i].def))
				{
					float statValue = innerContainer[i].GetStatValue(StatDefOf.Nutrition);
					if (statValue > 0f)
					{
						containedNutrition += statValue;
						innerContainer[i].SplitOff(1).Destroy();
						break;
					}
				}
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo in base.GetGizmos())
			{
				yield return gizmo;
			}
			if (Faction != Faction.OfPlayerSilentFail)
			{
				yield break;
			}
			foreach (Gizmo item in StorageSettingsClipboard.CopyPasteGizmosFor(allowedNutritionSettings))
			{
				yield return item;
			}
			if (base.Working)
			{
				if (DebugSettings.ShowDevGizmos)
				{
					if (selectedPawn != null && innerContainer.Contains(selectedPawn))
					{
						yield return new Command_Action
						{
							defaultLabel = "DEV: 99%",
							action = delegate
							{
								Pregnancy.Severity = 0.99f;
							}
						};
					}
				}
			}
			else
			{
				if (selectedPawn != null)
				{
					Command_Action command_Action1 = new Command_Action();
					command_Action1.defaultLabel = "CommandCancelLoad".Translate();
					command_Action1.defaultDesc = "CommandCancelLoadDesc".Translate();
					command_Action1.icon = CancelLoadingIcon;
					command_Action1.activateSound = SoundDefOf.Designate_Cancel;
					command_Action1.action = delegate
					{
						innerContainer.TryDropAll(InteractionCell, base.Map, ThingPlaceMode.Near);
						if (selectedPawn?.CurJobDef == JobDefOf.EnterBuilding)
						{
							selectedPawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
						}
						selectedPawn = null;
					};
					yield return command_Action1;
				}
				if (selectedPawn == null)
				{
					Command_Action command_Action2 = new Command_Action();
					command_Action2.defaultLabel = "InsertPerson".Translate() + "...";
					command_Action2.defaultDesc = "DMSRC_InsertPersonProducerDesc".Translate(this.def.label);
					command_Action2.icon = InsertPawnIcon.Texture;
					command_Action2.action = delegate
					{
						List<FloatMenuOption> list = new List<FloatMenuOption>();
						foreach (Pawn item1 in base.Map.mapPawns.AllPawnsSpawned)
						{
							Pawn pawn = item1;
							AcceptanceReport report = CanAcceptPawn(item1);
							if (report.Accepted)
							{
								list.Add(new FloatMenuOption(pawn.LabelCap + "(" + StatDefOf.Fertility.LabelCap + ": " + pawn.GetStatValue(StatDefOf.Fertility).ToStringByStyle(StatDefOf.Fertility.toStringStyle) + ")", delegate
								{
									SelectPawn(pawn);
									GameComponent_Renegades.Find?.ForbiddenTechMessage();
								}, pawn, Color.white));
							}
							else if (!report.Reason.NullOrEmpty())
							{
								list.Add(new FloatMenuOption(item1.LabelCap + ": " + report.Reason, null, pawn, Color.white));
							}
						}
						if (!list.Any())
						{
							list.Add(new FloatMenuOption("NoViablePawns".Translate(), null));
						}
						Find.WindowStack.Add(new FloatMenu(list));
					};
					if (!PowerOn)
					{
						command_Action2.Disable("NoPower".Translate().CapitalizeFirst());
					}
					if (!Fueled)
					{
						command_Action2.Disable("NoFuel".Translate().CapitalizeFirst());
					}
					yield return command_Action2;
				}
			}
			Command_Action command_Action3 = new Command_Action();
			ThingDefCountClass option = Options.FirstOrDefault((x) => x.thingDef == product);
			command_Action3.defaultLabel = option.Label;
			command_Action3.defaultDesc = "DMSRC_ProducerResultDesc".Translate(option.Label);
			command_Action3.icon = option.thingDef.uiIcon;
			command_Action3.defaultIconColor = option.thingDef.uiIconColor;
			command_Action3.action = delegate
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				foreach (ThingDefCountClass item2 in Options)
				{
					ThingDefCountClass tdcc = item2;
					list.Add(new FloatMenuOption(tdcc.Label, delegate
					{
						product = tdcc.thingDef;
						foreach(object o in Find.Selector.SelectedObjects)
						{
							if(o is Building_WetwareProducer b && b != this)
							{
								b.product = product;
							}
						}
						
					}, tdcc.thingDef));
				}
				Find.WindowStack.Add(new FloatMenu(list));
			};
			if (Working)
			{
				command_Action3.Disable("NoPower".Translate().CapitalizeFirst());
			}
			yield return command_Action3;
			if (DebugSettings.ShowDevGizmos)
			{
				yield return new Command_Action
				{
					defaultLabel = "DEV: Fill nutrition",
					action = delegate
					{
						containedNutrition = 10f;
					}
				};
				yield return new Command_Action
				{
					defaultLabel = "DEV: Empty nutrition",
					action = delegate
					{
						containedNutrition = 0f;
					}
				};
			}
		}

		public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
		{
			foreach (FloatMenuOption floatMenuOption in base.GetFloatMenuOptions(selPawn))
			{
				yield return floatMenuOption;
			}
			if(Faction != Faction.OfPlayerSilentFail)
			{
				yield break;
			}
			if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
			{
				yield return new FloatMenuOption("CannotEnterBuilding".Translate(this) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
				yield break;
			}
			AcceptanceReport acceptanceReport = CanAcceptPawn(selPawn);
			if (acceptanceReport.Accepted)
			{
				yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("EnterBuilding".Translate(this), delegate
				{
					SelectPawn(selPawn);
				}), selPawn, this);
			}
			else if (!acceptanceReport.Reason.NullOrEmpty())
			{
				yield return new FloatMenuOption("CannotEnterBuilding".Translate(this) + ": " + acceptanceReport.Reason.CapitalizeFirst(), null);
			}
		}

		public override string GetInspectString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(base.GetInspectString());
			if (selectedPawn != null)
			{
				if (innerContainer.Contains(selectedPawn))
				{
					stringBuilder.AppendLineIfNotEmpty().Append(string.Format("{0}: {1}", "CasketContains".Translate().ToString(), selectedPawn.NameShortColored.Resolve()));
					if (Working && PowerOn && Fueled && containedNutrition > 0)
					{
						float severity = Pregnancy.Severity;
						stringBuilder.AppendLineIfNotEmpty().Append("Progress".Translate() + ": " + severity.ToStringByStyle(ToStringStyle.PercentZero));
						stringBuilder.Append(" (" + "TimeLeft".Translate().CapitalizeFirst() + ": " + Mathf.RoundToInt((1f - severity) / Pregnancy.tickOffset).ToStringTicksToPeriod().Colorize(ColoredText.DateTimeColor) + ")");
					}
				}
				else
				{
					stringBuilder.AppendLineIfNotEmpty().Append("WaitingForPawn".Translate(selectedPawn.Named("PAWN")).Resolve());
				}
			}
			stringBuilder.AppendLineIfNotEmpty().Append("Nutrition".Translate()).Append(": ").Append(NutritionStored.ToStringByStyle(ToStringStyle.FloatMaxOne));
			if (selectedPawn != null && innerContainer.Contains(selectedPawn))
			{
				stringBuilder.Append(" (-").Append("PerDay".Translate(NutritionConsumedPerDay.ToString("F1"))).Append(")");
			}
			if(cooldownTicks > 0)
			{
				stringBuilder.AppendLineIfNotEmpty().Append("Cooldown".Translate() + ": " + cooldownTicks.ToStringTicksToPeriod());
			}
			return stringBuilder.ToString();
		}
	}
}