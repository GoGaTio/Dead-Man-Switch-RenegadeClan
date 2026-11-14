using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace DMSRC
{
	public class QuestNode_DefinedThingDefs : QuestNode
	{
		public List<ThingDef> thingDefs = new List<ThingDef>();

		protected override bool TestRunInt(Slate slate)
		{
			return true;
		}

		protected override void RunInt()
		{
			QuestPart_Choice questPart_Choice = QuestGen.quest.RewardChoice();
			QuestPart_Choice.Choice item = new QuestPart_Choice.Choice
			{
				rewards = new List<Reward>()
			};
			foreach(ThingDef thingDef in thingDefs)
			{
				item.rewards.Add(new Reward_DefinedThingDef(thingDef));
			}
			questPart_Choice.choices.Add(item);
		}
	}
}
