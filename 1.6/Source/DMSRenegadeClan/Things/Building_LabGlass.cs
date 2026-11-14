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
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.Scripting.GarbageCollector;

namespace DMSRC
{
	public class Building_LabGlass : Building, IThingHolder, IThingHolderWithDrawnPawn
	{
		public Vector3 PawnDrawOffset => new Vector3(0, 0, 0.15f) + CompBiosculpterPod.FloatingOffset(Find.TickManager.TicksGame);

		public ThingOwner innerContainer;

		public bool broken = false;

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return innerContainer;
		}

		public Building_LabGlass()
		{
			innerContainer = new ThingOwner<Thing>(this);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref broken, "broken");
			Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
		}

		//IThingHolderWithDrawnPawn
		public float HeldPawnDrawPos_Y => DrawPos.y + 0.03658537f;

		public float HeldPawnBodyAngle => Rot4.North.AsAngle;

		public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;

		//Graphic
		private Graphic GlassGraphic
		{
			get
			{
				if (cachedGlassGraphic == null)
				{
					cachedGlassGraphic = GraphicDatabase.Get<Graphic_Single>(def.graphicData.texPath + "_Glass", ShaderDatabase.Transparent, def.graphicData.drawSize, Color.white);
				}
				return cachedGlassGraphic;
			}
		}

		[Unsaved(false)]
		private Graphic cachedGlassGraphic;

		private Graphic BrokenGlassGraphic
		{
			get
			{
				if (cachedBrokenGlassGraphic == null)
				{
					cachedBrokenGlassGraphic = GraphicDatabase.Get<Graphic_Single>(def.graphicData.texPath + "_BrokenGlass", ShaderDatabase.Transparent, def.graphicData.drawSize, Color.white);
				}
				return cachedBrokenGlassGraphic;
			}
		}

		[Unsaved(false)]
		private Graphic cachedBrokenGlassGraphic;

		private Graphic TopGraphic
		{
			get
			{
				if (cachedTopGraphic == null)
				{
					cachedTopGraphic = GraphicDatabase.Get<Graphic_Single>(def.graphicData.texPath + "_Top", ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white);
				}
				return cachedTopGraphic;
			}
		}

		[Unsaved(false)]
		private Graphic cachedTopGraphic;

		public override void PostMake()
		{
			base.PostMake();
			innerContainer.TryAddOrTransfer(PawnGenerator.GeneratePawn(RCDefOf.DMSRC_AMO_DeformedNymph, GameComponent_Renegades.Find.RenegadesFaction));
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			if (!innerContainer.dontTickContents)
			{
				innerContainer.dontTickContents = true;
			}
		}

		public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
		{
			base.PostApplyDamage(dinfo, totalDamageDealt);
			innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
		}

		public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
		{
			if (!broken)
			{
				Pawn pawn = innerContainer.FirstOrDefault(x => x is Pawn) as Pawn;
				if(pawn != null)
				{
					pawn.Drawer.renderer.DynamicDrawPhaseAt(phase, drawLoc + PawnDrawOffset, null, neverAimWeapon: true);
				}
			}
			base.DynamicDrawPhaseAt(phase, drawLoc, flip);
		}

		protected override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			base.DrawAt(drawLoc, flip);
			if (broken)
			{
				BrokenGlassGraphic.Draw(DrawPos + Altitudes.AltIncVect * 2f, Rot4.North, this);
			}
			else
			{
				GlassGraphic.Draw(DrawPos + Altitudes.AltIncVect * 2f, Rot4.North, this);
			}
			TopGraphic.Draw(DrawPos + Altitudes.AltIncVect * 3f, Rot4.North, this);
		}
	}
}