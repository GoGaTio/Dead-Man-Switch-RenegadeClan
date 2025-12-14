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

namespace DMSRC
{
	public interface IAbilityNotifyable
	{
		void Notify_AbilityUsed(Ability ability);
	}

	public class CompProperties_AbilityNotify : CompProperties_AbilityEffect
	{
		public CompProperties_AbilityNotify()
		{
			compClass = typeof(CompAbilityEffect_Notify);
		}
	}
	public class CompAbilityEffect_Notify : CompAbilityEffect
	{
		public new CompProperties_AbilityNotify Props => (CompProperties_AbilityNotify)props;

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
			if(parent.pawn is IAbilityNotifyable pawn)
            {
				pawn.Notify_AbilityUsed(parent);
            }
			foreach(ThingComp comp in parent.pawn.AllComps)
            {
				if(comp is IAbilityNotifyable item)
                {
					item.Notify_AbilityUsed(parent);
                }
            }
		}
	}
}
