using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace DRC
{
	public class RenegadeClanModSettings : ModSettings
	{
		public bool allowSabotage = true;

		public override void ExposeData()
		{
			Scribe_Values.Look(ref allowSabotage, "allowSabotage", true);
			base.ExposeData();
		}
	}

	public class RenegadeClanMod : Mod
	{
		public RenegadeClanModSettings settings;

		public RenegadeClanMod(ModContentPack content) : base(content)
		{
			this.settings = GetSettings<RenegadeClanModSettings>();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(inRect);
			listingStandard.CheckboxLabeled("Sabotage".Translate(), ref settings.allowSabotage, "Allow Sabotage event".Translate());
			listingStandard.End();
			base.DoSettingsWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			return "DMS - Renegade Clan";
		}
	}
}
