#region Copyright & License Information
/*
 * AutoForce mod for OpenRA.
 * This file is free software, made available under the terms of the
 * GNU General Public License v3 or later, the same as OpenRA itself.
 */
#endregion

using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AutoForce.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Put this on the Player actor. Lets skirmish bots buy helix upgrades for their auto-training buildings.")]
	public class HelixResearchBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between two research decisions.")]
		public readonly int Interval = 150;

		[Desc("Cash the bot keeps for buildings; it only researches with money beyond this.")]
		public readonly int CashReserve = 600;

		public override object Create(ActorInitializer init) { return new HelixResearchBotModule(init.Self, this); }
	}

	public class HelixResearchBotModule : ConditionalTrait<HelixResearchBotModuleInfo>, IBotTick
	{
		readonly Player player;
		int ticks;

		public HelixResearchBotModule(Actor self, HelixResearchBotModuleInfo info)
			: base(info)
		{
			player = self.Owner;
			ticks = info.Interval;
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (IsTraitDisabled || --ticks > 0)
				return;

			ticks = Info.Interval;

			var cash = player.PlayerActor.Trait<PlayerResources>().GetCashAndResources() - Info.CashReserve;
			if (cash <= 0)
				return;

			Actor bestActor = null;
			HelixNodeInfo best = null;

			foreach (var a in player.World.ActorsHavingTrait<AutoSquad>())
			{
				if (a.Owner != player || a.IsDead || !a.IsInWorld)
					continue;

				var squad = a.Trait<AutoSquad>();
				if (!squad.HasTree || squad.Researching != null)
					continue;

				// Each building sticks to one strand (decided by its id) and only changes when that strand is blocked.
				var preferred = a.ActorID % 2 == 0 ? HelixSide.Quality : HelixSide.Quantity;
				var options = squad.Nodes.Where(n => squad.GetState(n) == NodeState.Available && n.Cost <= cash).ToList();
				var pick = options.Where(n => n.Side == preferred).OrderBy(n => n.Cost).FirstOrDefault()
					?? options.OrderBy(n => n.Cost).FirstOrDefault();

				if (pick != null && (best == null || pick.Cost < best.Cost))
				{
					best = pick;
					bestActor = a;
				}
			}

			if (best != null)
				bot.QueueOrder(new Order(AutoSquad.ResearchOrder, bestActor, false) { TargetString = best.Id });
		}
	}
}
