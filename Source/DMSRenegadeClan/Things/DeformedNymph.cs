using Fortified;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace DMSRC
{
	public class DeformedNymph : ArtificialOrganismHumanlike
	{
		public static readonly List<string> HairDefName = new List<string>()
		{
			"Curly",
			"Junkie",
			"Savage",
			"Scrapper",
			"Sticky",
			"Snazzy",
			"Warden"
		};

		public override void PostMake()
		{
			base.PostMake();
			if(story == null)
			{
				return;
			}
			story.HairColor = new Color(0.65f, 0.65f, 0.65f, 1f);
			story.hairDef = GetHair();
			Name = new NameSingle(NameGenerator.GenerateName(RCDefOf.DMSRC_DeformedNymph));
		}

		public HairDef GetHair()
		{
			List<string> list = HairDefName.ToList();
			while (!list.NullOrEmpty())
			{
				string s = list.RandomElement();
				HairDef def = DefDatabase<HairDef>.GetNamedSilentFail(s);
				if(def != null)
				{
					return def;
				}
				list.Remove(s);
			}
			return HairDefOf.Bald;
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo g in base.GetGizmos())
			{
				if(g is Command_Action command && command.defaultLabel == "CommandSelectOverseer".Translate())
				{
					continue;
				}
				yield return g;
			}
		}
	}
}
