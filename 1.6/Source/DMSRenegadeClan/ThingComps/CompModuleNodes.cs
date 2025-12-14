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
    public class ModuleNode
    {
        public ModuleNode()
        {

        }

        public DrawData drawData;

        public float baseLayer;

        public string name;

        public List<string> tags;
    }

    public class CompProperties_ModuleNodes : CompProperties
    {
        public List<ModuleNode> nodes = new List<ModuleNode>();

        public CompProperties_ModuleNodes()
        {
            compClass = typeof(CompModuleNodes);
        }
    }

    public class CompModuleNodes : ThingComp, IAbilityNotifyable
    {
        public CompProperties_ModuleNodes Props => (CompProperties_ModuleNodes)props;
        public Pawn Parent => parent as Pawn;

        public List<ThingWithComps> modules = new List<ThingWithComps>();

        public bool TryAddModule(ThingWithComps module, string nodeName)
        {
            if(module.HasComp<CompModule>() && Props.nodes.Any((ModuleNode x)=> x.name == nodeName))
            {
                modules.Add(module);
                module.GetComp<CompModule>().nodeName = nodeName;
                ModulesChanged();
                Parent.Drawer.renderer.SetAllGraphicsDirty();
                return true;
            }
            return false;
        }

        public void Notify_AbilityUsed(Ability ability)
        {
            ThingWithComps module = modules.Where((ThingWithComps x) => (x.GetComp<CompModule>() is CompModule comp) && comp.abilityCharges > 0 && comp.Props.ability == ability.def).RandomElementByWeightWithFallback((ThingWithComps y) => 1f / (float)(y.GetComp<CompModule>().abilityCharges), null);
            if (module != null)
            {
                module.GetComp<CompModule>().abilityCharges--;
            }
            Parent.Drawer.renderer.SetAllGraphicsDirty();
        }

        public void ModulesChanged()
        {
            List<AbilityDef> list = new List<AbilityDef>();
            foreach (ThingWithComps module in modules)
            {
                AbilityDef ad = module.GetComp<CompModule>().Props.ability;
                if (ad != null && !list.Contains(ad))
                {
                    list.Add(ad);
                }
            }
            foreach (AbilityDef abilityDef in list)
            {
                Ability ability = Parent.abilities.GetAbility(abilityDef);
                if (ability == null)
                {
                    this.Parent.abilities.GainAbility(abilityDef);
                    ability = Parent.abilities.GetAbility(abilityDef);
                }
                ability.maxCharges = 1;
                ability.RemainingCharges = 0;
                foreach (ThingWithComps module in modules)
                {
                    CompModule comp = module.GetComp<CompModule>();
                    if (comp.Props.ability == abilityDef)
                    {
                        ability.maxCharges += comp.Props.charges;
                        ability.RemainingCharges += comp.abilityCharges;
                    }
                }
                ability.maxCharges--;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra())
            {
                yield return g;
            }
            if (parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref modules, "DMSRC_modules");
        }

        public override List<PawnRenderNode> CompRenderNodes()
        {
            List<PawnRenderNode> list = new List<PawnRenderNode>();
            foreach (ThingWithComps module in modules)
            {  
                CompModule comp = module.GetComp<CompModule>();
                ModuleNode node = Props.nodes.FirstOrDefault((ModuleNode n) => n.name == comp.nodeName); 
                PawnRenderNodeProperties renderNodeProperty = new PawnRenderNodeProperties();
                renderNodeProperty.drawData = node.drawData;
                renderNodeProperty.baseLayer = node.baseLayer;
                renderNodeProperty.drawSize = comp.Props.drawSize;
                renderNodeProperty.pawnType = PawnRenderNodeProperties.RenderNodePawnType.Any;
                renderNodeProperty.parentTagDef = PawnRenderNodeTagDefOf.Body;
                renderNodeProperty.texPath = comp.TexPath;
				PawnRenderNode_Module renderNode = (PawnRenderNode_Module)Activator.CreateInstance(renderNodeProperty.nodeClass, Parent, renderNodeProperty, Parent.Drawer.renderer.renderTree);
                renderNode.moduleComp = comp;
				list.Add(renderNode);
            }
            return list;
        }
    }

	public class PawnRenderNode_Module : PawnRenderNode
	{
		public CompModule moduleComp;

		public PawnRenderNode_Module(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
			: base(pawn, props, tree)
		{
		}

		public override Graphic GraphicFor(Pawn pawn)
		{
			return GraphicDatabase.Get<Graphic_Multi>(moduleComp.TexPath, ShaderDatabase.Cutout);
		}
	}
}