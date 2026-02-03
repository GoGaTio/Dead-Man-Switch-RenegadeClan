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
    public class CompProperties_WarheadPlatform : CompProperties_Explosive
	{
		public GraphicData graphicData;

		public CompProperties_WarheadPlatform()
        {
            compClass = typeof(CompWarheadPlatform);
        }
    }

    public class CompWarheadPlatform : CompExplosive
    {
        public new CompProperties_WarheadPlatform Props => (CompProperties_WarheadPlatform)props;

        public bool hasWarhead = false;

		public override void PostDraw()
		{
			base.PostDraw();
			if (hasWarhead)
			{
				Mesh mesh = Props.graphicData.Graphic.MeshAt(parent.Rotation);
				Vector3 drawPos = parent.DrawPos;
				drawPos.y = AltitudeLayer.BuildingOnTop.AltitudeFor() + parent.def.graphicData.drawOffset.y;
				Graphics.DrawMesh(mesh, drawPos + Props.graphicData.drawOffset.RotatedBy(parent.Rotation), Quaternion.identity, Props.graphicData.Graphic.MatAt(parent.Rotation), 0);
			}
		}

		public override bool DontDrawParent()
		{
			if (hasWarhead)
			{
				return true;
			}
			return base.DontDrawParent();
		}

		protected override bool CanEverExplodeFromDamage => hasWarhead ? base.CanEverExplodeFromDamage : false;

		public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
		{
			if (destroyedThroughDetonation || (dinfo.HasValue && dinfo.Value.Def?.isExplosive == true))
			{
				DetonateWarhead(prevMap);
			}
			base.Notify_Killed(prevMap, dinfo);
		}

		public void DetonateWarhead(Map map)
		{
			if (hasWarhead)
			{
				if (map.IsPocketMap && map.PocketMapParent.sourceMap?.spawnedThings.FirstOrDefault(t=> t is FacilityEntrance) is FacilityEntrance entrance)
				{
					destroyedThroughDetonation = true;
					entrance.DestroyMap();
				}
			}
		}

		public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref hasWarhead, "DMSRC_hasWarhead");
        }

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo item in base.CompGetGizmosExtra())
			{
				yield return item;
			}
			if (DebugSettings.ShowDevGizmos)
			{
				Command_Action command_Action = new Command_Action();
				command_Action.defaultLabel = "DEV: Place warhead";
				command_Action.action = delegate
				{
					hasWarhead = !hasWarhead;
				};
				yield return command_Action;
			}
		}

		public override void PostPostMake()
		{
			base.PostPostMake();
			hasWarhead = true;
		}
    }
}