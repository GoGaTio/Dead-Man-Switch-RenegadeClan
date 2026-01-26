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
using HarmonyLib;

namespace DMSRC
{
	[StaticConstructorOnStartup]
	public class InactiveMech : ThingWithComps, IThingHolder
	{
		public static readonly Texture2D Icon = ContentFinder<Texture2D>.Get("UI/Icons/WorkMode/SelfShutdown");

		public ThingOwner<Thing> innerContainer;

		[Unsaved(false)]
		private string cachedLabel;

		public InactiveMech()
		{
			innerContainer = new ThingOwner<Thing>(this, oneStackOnly: true, LookMode.Deep, removeContentsIfDestroyed: false);
		}

		public override Graphic Graphic => BaseContent.BadGraphic;

		public Pawn InnerPawn
		{
			get
			{
				if (innerContainer.Count <= 0)
				{
					return null;
				}
				if (innerContainer.First() is Corpse c)
				{
					return c.InnerPawn;
				}
				if (innerContainer.First() is Pawn p)
				{
					return p;
				}
				return null;
			}
		}

		public override string LabelNoCount
		{
			get
			{
				if (cachedLabel == null)
				{
					if(InnerPawn == null)
					{
						cachedLabel = base.LabelNoCount;
					}
					cachedLabel = "DMSRC_DeactivatedLabel".Translate(InnerPawn.KindLabel, InnerPawn);
				}
				return cachedLabel;
			}
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return innerContainer;
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
		{
			if(InnerPawn == null)
			{
				base.DynamicDrawPhaseAt(phase, drawLoc, flip);
			}
			else InnerPawn.DynamicDrawPhaseAt(phase, drawLoc);
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			if(InnerPawn != null)
			{
				InnerPawn.Rotation = Rot4.Random;
			}
			else
			{
				Log.Message(innerContainer.ContentsString);
			}
		}

		public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
			absorbed = true;
			if(InnerPawn != null)
			{
				InnerPawn.TakeDamage(dinfo);
				if (InnerPawn.Dead || InnerPawn.Destroyed)
				{
					if (InnerPawn.Corpse != null)
					{
						GenSpawn.Spawn(InnerPawn.Corpse, Position, Map);
					}
					innerContainer.Clear();
					Destroy();
					return;
				}
			}
			else
			{
				Destroy();
			}
			base.PreApplyDamage(ref dinfo, out absorbed);
			absorbed = true;
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			if(Faction != Faction.OfPlayerSilentFail)
			{
				yield break;
			}
			yield return new Command_Action
			{
				defaultLabel = "Activate".Translate(),
				defaultDesc = "Activate".Translate(),
				icon = Icon,
				action = delegate
				{
					Map map = Map;
					IntVec3 pos = Position;
					bool flag = this.DeSpawnOrDeselect();
					Thing t = GenSpawn.Spawn(InnerPawn, pos, map);
					Destroy();
					if (flag)
					{
						Find.Selector.Select(t);
					}
				}
			};
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Deep.Look(ref innerContainer, saveDestroyedThings: true, "DMSRC_innerContainer", this);
			if (Scribe.mode == LoadSaveMode.PostLoadInit && innerContainer.removeContentsIfDestroyed)
			{
				innerContainer.removeContentsIfDestroyed = false;
			}
		}

		/*public override string GetInspectString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (InnerPawn.Faction != null && !InnerPawn.Faction.Hidden)
			{
				stringBuilder.AppendLineTagged("Faction".Translate() + ": " + InnerPawn.Faction.NameColored);
			}
			//stringBuilder.AppendLine("DeadTime".Translate(Age.ToStringTicksToPeriodVague(vagueMin: true, vagueMax: false)));
			float num = 1f - InnerPawn.health.hediffSet.GetCoverageOfNotMissingNaturalParts(InnerPawn.RaceProps.body.corePart);
			if (num >= 0.01f)
			{
				stringBuilder.AppendLine("CorpsePercentMissing".Translate() + ": " + num.ToStringPercent());
			}
			Hediff_DeathRefusal firstHediff = InnerPawn.health.hediffSet.GetFirstHediff<Hediff_DeathRefusal>();
			if (firstHediff != null && firstHediff.InProgress)
			{
				stringBuilder.AppendLine("SelfResurrecting".Translate());
			}
			stringBuilder.AppendLine(base.GetInspectString());
			return stringBuilder.ToString().TrimEndNewlines();
		}*/
	}
}