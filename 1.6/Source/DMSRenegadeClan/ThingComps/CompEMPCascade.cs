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
    public class CompProperties_EMPCascade : CompProperties
    {
		public float effectRadius;

		public int effectDamage;

		public IntRange effectsCountRange;

		public IntRange initialDelayRange;

		public IntRange delayRange;

		public bool useRandomAngle = false;

		public FloatRange effectAngleRange = new FloatRange();

		public CompProperties_EMPCascade()
        {
            compClass = typeof(CompEMPCascade);
        }
    }

    public class CompEMPCascade : ThingComp
    {
        public CompProperties_EMPCascade Props => (CompProperties_EMPCascade)props;

		public bool active = false;

		public int ticksTillEffect;

		public int effectsLeft;

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref active, "active", false);
			Scribe_Values.Look(ref ticksTillEffect, "ticksTillEffect");
			Scribe_Values.Look(ref effectsLeft, "effectsLeft");
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
		{
			base.PostDeSpawn(map, mode);
			active = false;
		}

		public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
		{
			base.PostPostApplyDamage(dinfo, totalDamageDealt);
			if(!active && dinfo.Def == DamageDefOf.EMP && parent.Spawned)
			{
				active = true;
				ticksTillEffect = Props.initialDelayRange.RandomInRange;
				effectsLeft = Props.effectsCountRange.RandomInRange;
			}
		}

		public override void CompTick()
		{
			if (!active)
			{
				return;
			}
			ticksTillEffect--;
			if(ticksTillEffect > 0)
			{
				return;
			}
			effectsLeft--;
			if (effectsLeft <= 0)
			{
				active = false;
				return;
			}
			ticksTillEffect = Props.delayRange.RandomInRange;
			List<IntVec3> cells = new List<IntVec3>();
			Map map = parent.Map;
			IntVec3 center = parent.Position;
			float num1 = Rand.Value * 360f;
			float num2 = num1 + Props.effectAngleRange.RandomInRange;
			if (num2 > 360)
			{
				num2 -= 360;
			}
			int num3 = GenRadial.NumCellsInRadius(Props.effectRadius);
			for (int i = 0; i < num3; i++)
			{
				IntVec3 intVec = center + GenRadial.RadialPattern[i];
				if (!intVec.InBounds(map))
				{
					continue;
				}
				if (Props.useRandomAngle)
				{
					float lengthHorizontal = (intVec - center).LengthHorizontal;
					float num4 = lengthHorizontal / Props.effectRadius;
					if (!(lengthHorizontal > 0.5f))
					{
						continue;
					}
					float num5 = Mathf.Atan2(-(intVec.z - center.z), intVec.x - center.x) * 57.29578f;
					float num6 = num1;
					float num7 = num2;
					if (num5 - num6 < -0.5f * num4 || num5 - num7 > 0.5f * num4)
					{
						continue;
					}
				}
				cells.Add(intVec);
			}
			EffecterDefOf.DisabledByEMPLarge.SpawnMaintained(parent, map, Props.effectRadius / 10f);
			GenExplosion.DoExplosion(center, map, Props.effectRadius, DamageDefOf.EMP, null, Props.effectDamage, intendedTarget: parent, ignoredThings: new List<Thing>() { parent }, screenShakeFactor: 0, overrideCells: cells, doVisualEffects: false);
		}
	}
}