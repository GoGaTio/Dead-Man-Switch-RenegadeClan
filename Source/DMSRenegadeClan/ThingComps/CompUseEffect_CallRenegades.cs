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
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using static HarmonyLib.Code;

namespace DMSRC
{
	public class CompProperties_CallRenegades : CompProperties
	{
		public JobDef useJob;

		public MenuOptionPriority floatMenuOptionPriority;

		public CompProperties_CallRenegades()
		{
			compClass = typeof(CompCallRenegades);
		}
	}
	public class CompCallRenegades : ThingComp
	{
		public CompPowerTrader powerTraderComp;

		private Texture2D icon;

		private Texture2D Icon
		{
			get
			{
				if (icon == null && GameComponent_Renegades.Find.RenegadesFaction != null)
				{
					icon = GameComponent_Renegades.Find.RenegadesFaction.def?.FactionIcon;
				}
				return icon;
			}
		}
		public CompProperties_CallRenegades Props => (CompProperties_CallRenegades)props;

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			powerTraderComp = parent.GetComp<CompPowerTrader>();
		}

		public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn myPawn)
		{
			if (Props.useJob == null || powerTraderComp?.PowerOn == false || parent.Map?.Tile.Valid != true || parent.Map.IsPocketMap)
			{
				yield break;
			}
			GameComponent_Renegades renegades = GameComponent_Renegades.Find;
			if(renegades == null || !renegades.contacted || renegades.RenegadesFaction == null)
			{
				yield break;
			}
			string text = "CallOnRadio".Translate(renegades.RenegadesFaction.Name ?? renegades.RenegadesFaction.def.label);
			text = text + " (" + renegades.PlayerRelation.GetLabelCap() + ", " + renegades.playerGoodwill.ToStringWithSign() + ")";
			FloatMenuOption floatMenuOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, delegate
			{
				foreach (CompUseEffect comp in parent.GetComps<CompUseEffect>())
				{
					if (comp.SelectedUseOption(myPawn))
					{
						return;
					}
				}
				Job job = JobMaker.MakeJob(Props.useJob, parent);
				job.count = 1;
				myPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			}, priority: Props.floatMenuOptionPriority, iconTex: Icon, iconColor: renegades.RenegadesFaction.Color), myPawn, parent);
			yield return floatMenuOption;
		}
	}
}