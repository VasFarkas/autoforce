#region Copyright & License Information
/*
 * AutoForce mod for OpenRA.
 * This file is free software, made available under the terms of the
 * GNU General Public License v3 or later, the same as OpenRA itself.
 */
#endregion

using System;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.AutoForce.Traits
{
	[Desc("Lets the building build one unit on demand (e.g. an MCV at the Service Depot) with a fixed build time. " +
		"A button in the sidebar panel starts it. Needs a Production trait with an Exit.")]
	public class OnDemandBuildInfo : TraitInfo, Requires<ProductionInfo>
	{
		[FieldLoader.Require]
		[Desc("Actor to build.")]
		public readonly string Unit = null;

		[Desc("Production type used to choose an Exit.")]
		public readonly string ProductionType = "AutoMcv";

		[Desc("Build time in ticks (25 ticks = 1 second). 3000 = 2 minutes.")]
		public readonly int Duration = 3000;

		[Desc("Charge the unit's normal Valued cost when the build starts.")]
		public readonly bool PayCost = true;

		public override object Create(ActorInitializer init) { return new OnDemandBuild(init.Self, this); }
	}

	public class OnDemandBuild : ITick, IResolveOrder
	{
		public const string BuildOrder = "AutoForceBuildOnDemand";

		public readonly OnDemandBuildInfo Info;
		readonly Actor self;
		Production production;
		int cost;
		bool costKnown;

		public bool Building { get; private set; }
		public int TicksLeft { get; private set; }
		public int TicksTotal { get { return Math.Max(1, Info.Duration); } }
		public string Status { get; private set; }
		public bool StatusIsProblem { get; private set; }

		public OnDemandBuild(Actor self, OnDemandBuildInfo info)
		{
			this.self = self;
			Info = info;
			Status = "Ready";
		}

		public string UnitName { get { return AutoSquad.UnitDisplayName(self, Info.Unit); } }

		public int Cost
		{
			get
			{
				if (!costKnown)
				{
					costKnown = true;
					ActorInfo ai;
					if (Info.PayCost && self.World.Map.Rules.Actors.TryGetValue(Info.Unit.ToLowerInvariant(), out ai))
					{
						var v = ai.TraitInfoOrDefault<ValuedInfo>();
						cost = v != null ? v.Cost : 0;
					}
				}

				return cost;
			}
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != BuildOrder || Building)
				return;

			var resources = self.Owner.PlayerActor.Trait<PlayerResources>();
			if (Cost > 0 && !resources.TakeCash(Cost, true))
				return;

			Building = true;
			TicksLeft = TicksTotal;
		}

		void ITick.Tick(Actor self)
		{
			if (!Building)
				return;

			if (TicksLeft > 0)
			{
				TicksLeft--;
				Status = UnitName + " ready in " + ((TicksLeft + 24) / 25) + "s";
				StatusIsProblem = false;
				return;
			}

			if (production == null)
				production = self.TraitOrDefault<Production>();

			ActorInfo actorInfo;
			if (production == null || !self.World.Map.Rules.Actors.TryGetValue(Info.Unit.ToLowerInvariant(), out actorInfo))
			{
				Status = "cannot build: missing Production trait or unknown actor";
				StatusIsProblem = true;
				Building = false;
				return;
			}

			var inits = new TypeDictionary { new OwnerInit(self.Owner), new FactionInit(production.Faction) };
			if (production.Produce(self, actorInfo, Info.ProductionType, inits, 0))
			{
				Building = false;
				Status = UnitName + " delivered";
				StatusIsProblem = false;
			}
			else
			{
				Status = "waiting: exit blocked (move units away from the depot)";
				StatusIsProblem = true;
				TicksLeft = 0; // retried every tick; Produce is cheap when the exit is blocked
			}
		}
	}
}
