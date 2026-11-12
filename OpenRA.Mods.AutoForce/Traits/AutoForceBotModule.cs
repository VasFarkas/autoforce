#region Copyright & License Information
/*
 * AutoForce mod for OpenRA.
 * This file is free software, made available under the terms of the
 * GNU General Public License v3 or later, the same as OpenRA itself.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AutoForce.Traits
{
	[Desc("Lets skirmish AIs use AutoForce: researches helix upgrades with spare cash and rebuilds a lost " +
		"Construction Yard through the Service Depot's MCV button.")]
	public class AutoForceBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between two decisions.")]
		public readonly int Interval = 125;

		[Desc("Cash the AI keeps for building before it spends on upgrades.")]
		public readonly int ReserveCash = 900;

		[Desc("Actor types that count as a Construction Yard.")]
		public readonly HashSet<string> ConstructionYardTypes = new HashSet<string> { "fact" };

		[Desc("Actor types that count as an MCV.")]
		public readonly HashSet<string> McvTypes = new HashSet<string> { "mcv" };

		public override object Create(ActorInitializer init) { return new AutoForceBotModule(init.Self, this); }
	}

	public class AutoForceBotModule : ConditionalTrait<AutoForceBotModuleInfo>, IBotTick
	{
		readonly Actor self;
		readonly Dictionary<uint, HelixSide> preferredSide = new Dictionary<uint, HelixSide>();
		int ticks;

		public AutoForceBotModule(Actor self, AutoForceBotModuleInfo info)
			: base(info)
		{
			this.self = self;
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (IsTraitDisabled || ++ticks % Info.Interval != 0)
				return;

			var player = self.Owner;
			var resources = player.PlayerActor.Trait<PlayerResources>();
			var cash = resources.GetCashAndResources();

			if (TryRebuildMcv(bot, player, cash))
				return;

			foreach (var actor in self.World.ActorsHavingTrait<AutoSquad>())
			{
				if (actor.IsDead || !actor.IsInWorld || actor.Owner != player)
					continue;

				var squad = actor.Trait<AutoSquad>();
				if (!squad.HasTree || squad.Researching != null)
					continue;

				var node = Choose(actor, squad, cash - Info.ReserveCash);
				if (node == null)
					continue;

				bot.QueueOrder(new Order(AutoSquad.ResearchOrder, actor, false) { TargetString = node.Id });
				return; // one purchase per decision, so the AI never drains its cash at once
			}
		}

		HelixNodeInfo Choose(Actor actor, AutoSquad squad, int budget)
		{
			var options = squad.Nodes.Where(n => squad.GetState(n) == NodeState.Available && n.Cost <= budget).ToList();
			if (options.Count == 0)
				return null;

			HelixSide side;
			if (!preferredSide.TryGetValue(actor.ActorID, out side))
			{
				side = actor.ActorID % 2 == 0 ? HelixSide.Quality : HelixSide.Quantity;
				preferredSide[actor.ActorID] = side;
			}

			var onSide = options.Where(n => n.Side == side).ToList();
			if (onSide.Count > 0)
				return onSide[0];

			// Only the other strand is reachable (or affordable): follow it.
			var other = options[0];
			preferredSide[actor.ActorID] = other.Side;
			return other;
		}

		bool TryRebuildMcv(IBot bot, Player player, int cash)
		{
			var hasYardOrMcv = self.World.Actors.Any(a =>
				!a.IsDead && a.IsInWorld && a.Owner == player &&
				(Info.ConstructionYardTypes.Contains(a.Info.Name) || Info.McvTypes.Contains(a.Info.Name)));

			if (hasYardOrMcv)
				return false;

			foreach (var depot in self.World.ActorsHavingTrait<OnDemandBuild>())
			{
				if (depot.IsDead || !depot.IsInWorld || depot.Owner != player)
					continue;

				var build = depot.Trait<OnDemandBuild>();
				if (build.Building)
					return true; // one is already on its way

				if (cash < build.Cost)
					continue;

				bot.QueueOrder(new Order(OnDemandBuild.BuildOrder, depot, false));
				return true;
			}

			return false;
		}
	}
}
