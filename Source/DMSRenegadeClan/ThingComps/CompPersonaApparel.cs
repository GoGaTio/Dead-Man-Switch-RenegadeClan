using DelaunatorSharp;
using Fortified;
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

namespace DMSRC
{
	public class CompProperties_ShieldHelmet : CompProperties_Shield
	{
		public PawnRenderNodeTagDef arrarelTag;

		public CompProperties_ShieldHelmet()
		{
			compClass = typeof(CompShieldHelmet);
		}
	}
	public class CompShieldHelmet : CompShield
	{
		public bool disabledByOtherApparel = false;

		public bool check = true;

		public new CompProperties_ShieldHelmet Props => (CompProperties_ShieldHelmet)props;

		public override void CompTick()
		{
			base.CompTick();
			if (check && parent.IsHashIntervalTick(300))
			{
				if (disabledByOtherApparel)
				{
					if (PawnOwner.apparel.WornApparel.FirstOrDefault(x => x.def.apparel.parentTagDef == Props.arrarelTag) == null)
					{
						disabledByOtherApparel = false;
					}
				}
				else if (PawnOwner.apparel.WornApparel.FirstOrDefault(x => x.def.apparel.parentTagDef == Props.arrarelTag) != null)
				{
					disabledByOtherApparel = true;
				}
			}
		}

		public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
			absorbed = false;
			if (disabledByOtherApparel) return;
			base.PostPreApplyDamage(ref dinfo, out absorbed);
		}

		public override void CompDrawWornExtras()
		{
			if (disabledByOtherApparel) return;
			base.CompDrawWornExtras();
		}

		public override void PostDraw()
		{
			if (disabledByOtherApparel) return;
			base.PostDraw();
		}

		public override void Initialize(CompProperties props)
		{
			base.Initialize(props);
			check = ((CompProperties_ShieldHelmet)props)?.arrarelTag != null;
		}
	}
}
