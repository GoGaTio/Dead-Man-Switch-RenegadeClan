using System;
using System.Collections;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Fortified
{
	public class CompProperties_Camouflage : CompProperties
	{
		public float baseDetectRange = 14f;

		public int ticksUntilRecover = 1200;

		[MustTranslate]
		public string detectedMessage;

		[MustTranslate]
		public string detectedLetterLabel;

		[MustTranslate]
		public string detectedLetterDesc;

		public float detectChanceOnCast = 0.05f;

		[NoTranslate]
		public string texPath;

		[NoTranslate]
		public string commandKey;

		public CompProperties_Camouflage()
		{
			compClass = typeof(CompCamouflage);
		}
	}
	public class CompCamouflage : ThingComp
	{
		public CompProperties_Camouflage Props => (CompProperties_Camouflage)props;

		[Unsaved(false)]
		private HediffComp_Invisibility invisibility;

		private int lastDetectedTick = -99999;

		private static float lastNotified = -99999f;

		public bool active = true;

		[Unsaved(false)]
		private Texture2D iconTex;

		private Pawn Parent => (Pawn)parent;

		public Texture2D Icon
		{
			get
			{
				if (!(iconTex != null))
				{
					return iconTex = ContentFinder<Texture2D>.Get(Props.texPath);
				}
				return iconTex;
			}
		}

		private HediffComp_Invisibility Invisibility => invisibility ?? (invisibility = Parent.health.hediffSet.GetFirstHediffOfDef(FFF_DefOf.FFF_Camouflage)?.TryGetComp<HediffComp_Invisibility>());

		public override void PostExposeData()
		{
			Scribe_Values.Look(ref lastDetectedTick, "lastDetectedTick", 0);
			Scribe_Values.Look(ref active, "active", defaultValue: true);
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			if (Invisibility == null)
			{
				Parent.health.AddHediff(FFF_DefOf.FFF_Camouflage);
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (parent.Faction == Faction.OfPlayerSilentFail)
			{
				Command_Toggle command_Toggle = new Command_Toggle();
				command_Toggle.hotKey = KeyBindingDefOf.Command_TogglePower;
				command_Toggle.icon = Icon;
				command_Toggle.defaultLabel = (Props.commandKey + "_Label").Translate();
				command_Toggle.defaultDesc = (Props.commandKey + "_Desc").Translate();
				command_Toggle.isActive = () => active;
				command_Toggle.toggleAction = delegate
				{
					active = !active;
					if (active)
					{
						lastDetectedTick = Find.TickManager.TicksGame;
					}
					else
					{
						Invisibility.BecomeVisible();
					}
				};
				yield return command_Toggle;
			}
		}

		public override void CompTick()
		{
			base.CompTick();
			
			if (!parent.Spawned || Parent.Downed)
			{
				return;
			}
			if (active && parent.IsHashIntervalTick(7))
			{
				if (Find.TickManager.TicksGame > lastDetectedTick + Props.ticksUntilRecover)
				{
					CheckDetected();
				}
				if (Find.TickManager.TicksGame > lastDetectedTick + Props.ticksUntilRecover)
				{
					Invisibility.BecomeInvisible();
				}
			}
			if (!parent.IsHashIntervalTick(60))
			{
				return;
			}
			if (parent.Faction != Faction.OfPlayerSilentFail)
			{
				Verb currentEffectiveVerb = Parent.CurrentEffectiveVerb;
				if (currentEffectiveVerb != null && !currentEffectiveVerb.verbProps.IsMeleeAttack)
				{
					TargetScanFlags targetScanFlags = TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
					if (currentEffectiveVerb.IsIncendiary_Ranged())
					{
						targetScanFlags |= TargetScanFlags.NeedNonBurning;
					}
					Thing thing = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(Parent, targetScanFlags);
					if (thing != null)
					{
						Parent.TryStartAttack(thing);
					}
				}
			}
			if (Invisibility == null)
			{
				Parent.health.AddHediff(FFF_DefOf.FFF_Camouflage);
			}
		}

		private void CheckDetected()
		{
			foreach (Pawn item in parent.Map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn))
			{
				if (PawnCanDetect(item))
				{
					if (!Invisibility.PsychologicallyVisible)
					{
						Invisibility.BecomeVisible();
					}
					lastDetectedTick = Find.TickManager.TicksGame;
				}
			}
		}

		private bool PawnCanDetect(Pawn pawn)
		{
			if (pawn.Downed || !parent.HostileTo(pawn) || !pawn.Awake())
			{
				return false;
			}
			if (pawn.IsAnimal)
			{
				return false;
			}
			if (!parent.Position.InHorDistOf(pawn.Position, GetPawnSightRadius(pawn, Parent)))
			{
				return false;
			}
			return GenSight.LineOfSightToThing(pawn.Position, Parent, parent.Map);
		}

		private float GetPawnSightRadius(Pawn pawn, Pawn saboteur)
		{
			float num = Props.baseDetectRange;
			if (pawn.genes == null || pawn.genes.AffectedByDarkness)
			{
				float t = saboteur.Map.glowGrid.GroundGlowAt(saboteur.Position);
				num *= Mathf.Lerp(0.33f, 1f, t);
			}
			return num * pawn.health.capacities.GetLevel(PawnCapacityDefOf.Sight);
		}

		public override void Notify_UsedVerb(Pawn pawn, Verb verb)
		{
			base.Notify_UsedVerb(pawn, verb);
			if (verb.IsMeleeAttack || Rand.Chance(Props.detectChanceOnCast))
			{
				Invisibility.BecomeVisible();
				lastDetectedTick = Find.TickManager.TicksGame;
			}
		}

		public override void Notify_BecameVisible()
		{
			if (parent.HostileTo(Faction.OfPlayerSilentFail))
			{
				foreach (Pawn item in parent.MapHeld.listerThings.ThingsInGroup(ThingRequestGroup.Pawn))
				{
					if (item.kindDef == Parent.kindDef && item != Parent && item.Position.InHorDistOf(parent.Position, 30f) && !item.IsPsychologicallyInvisible() && GenSight.LineOfSight(item.Position, parent.Position, item.Map))
					{
						return;
					}
				}
				if (RealTime.LastRealTime > lastNotified + 60f)
				{
					Find.LetterStack.ReceiveLetter(Props.detectedLetterLabel, Props.detectedLetterDesc, LetterDefOf.ThreatBig, parent, null, null, null, null, 6);
				}
				else
				{
					Messages.Message(Props.detectedMessage, parent, MessageTypeDefOf.ThreatBig);
				}
			}
			lastDetectedTick = Find.TickManager.TicksGame;
			lastNotified = RealTime.LastRealTime;
		}
	}
}