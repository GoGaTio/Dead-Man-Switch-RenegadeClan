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
	public class FacilityDestroyer : ThingWithComps
	{
		public int ticksLeft = 0;

		private static readonly SimpleCurve TicksToShakeMTBTicksCurve = new SimpleCurve
		{
			new CurvePoint(2500f, 45f),
			new CurvePoint(300f, 30f),
			new CurvePoint(60f, 5f)
		};

		protected override void Tick()
		{
			base.Tick();
			ticksLeft--;
			Map map = Map;
			if (ticksLeft < 0)
			{
				if (map.IsPocketMap && map.PocketMapParent.sourceMap?.spawnedThings.FirstOrDefault(t => t is FacilityEntrance) is FacilityEntrance entrance)
				{
					entrance.DestroyMap();
				}
				else
				{
					try
					{
						Thing.allowDestroyNonDestroyable = true;
						Destroy();
					}
					finally
					{
						Thing.allowDestroyNonDestroyable = false;
					}
				}
			}
			else if(Find.CurrentMap == map && Rand.MTBEventOccurs(TicksToShakeMTBTicksCurve.Evaluate(ticksLeft), 1f, 1f))
			{
				Find.CameraDriver.shaker.DoShake(0.2f);
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref ticksLeft, "ticksLeft");
		}
	}
}