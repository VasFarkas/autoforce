#region Copyright & License Information
/*
 * AutoForce mod for OpenRA.
 * This file is free software, made available under the terms of the
 * GNU General Public License v3 or later, the same as OpenRA itself.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.AutoForce.Traits
{
	public enum NodeState { Locked, Available, Researching, Done, Excluded }

	[Desc("Makes the building train and maintain a squad automatically, and (when HelixNode traits are present) " +
		"exposes the helix upgrade tree. Needs a Production trait and at least one Exit.")]
	public class AutoSquadInfo : TraitInfo, Requires<ProductionInfo>
	{
		[FieldLoader.Require]
		[Desc("Squad at the start: actor name -> number of units to maintain.")]
		public readonly Dictionary<string, int> Roster = new Dictionary<string, int>();

		[Desc("Production type used to choose an Exit (must match an Exit's ProductionTypes, or the Exit must have none).")]
		public readonly string ProductionType = "AutoInfantry";

		[Desc("Ticks between two spawns (25 ticks are roughly one second at normal game speed).")]
		public readonly int SpawnInterval = 150;

		[Desc("Ticks before the first spawn after the building is created.")]
		public readonly int InitialDelay = 50;

		[Desc("Charge the unit's Valued cost from the owner's cash for every spawned unit. Squads are free by default; " +
			"the squad size is the limit.")]
		public readonly bool PayForUnits = false;

		[Desc("Title shown on top of the helix panel. Empty = use the actor's tooltip.")]
		public readonly string PanelTitle = "";

		public override object Create(ActorInitializer init) { return new AutoSquad(init.Self, this); }
	}

	public class AutoSquad : ITick, IResolveOrder, INotifyProduction
	{
		public const string ResearchOrder = "AutoForceResearch";
		public const string BuildStamp = "AutoForce build 3";

		public readonly AutoSquadInfo Info;
		public readonly List<HelixNodeInfo> Nodes;

		readonly Actor self;
		readonly List<string> unitOrder = new List<string>();
		readonly Dictionary<string, int> target = new Dictionary<string, int>();
		readonly List<Actor> members = new List<Actor>();
		readonly HashSet<string> done = new HashSet<string>();
		readonly Dictionary<int, string> chosenByTier = new Dictionary<int, string>();

		Production production;
		string lastElite;
		int spawnTimer;
		int speedBonus, damageBonus, fireRateBonus;

		/// <summary>What the building is doing right now, shown in the helix panel.</summary>
		public string Status { get; private set; }
		public bool StatusIsProblem { get; private set; }

		string lastLogged;

		public HelixNodeInfo Researching { get; private set; }
		public int ResearchTicksLeft { get; private set; }
		public int ResearchTicksTotal { get; private set; }

		public AutoSquad(Actor self, AutoSquadInfo info)
		{
			this.self = self;
			Info = info;
			spawnTimer = info.InitialDelay;

			// Actor names are lowercase inside the ruleset (Actors["e1"], ActorInfo.Name == "e1"),
			// while YAML spells them E1, so everything is normalized here.
			foreach (var kv in info.Roster)
			{
				var name = kv.Key.ToLowerInvariant();
				unitOrder.Add(name);
				target[name] = kv.Value;
			}

			Nodes = self.Info.TraitInfos<HelixNodeInfo>().OrderBy(n => n.Tier).ThenBy(n => n.Side).ToList();
		}

		public bool HasTree { get { return Nodes.Count > 0; } }

		public int MaxTier { get { return Nodes.Count == 0 ? 0 : Nodes.Max(n => n.Tier); } }

		public string Title
		{
			get
			{
				if (!string.IsNullOrEmpty(Info.PanelTitle))
					return Info.PanelTitle;

				var tooltip = self.Info.TraitInfoOrDefault<TooltipInfo>();
				return tooltip != null ? FluentProvider.GetMessage(tooltip.Name) : self.Info.Name;
			}
		}

		public int SpeedBonus { get { return speedBonus; } }
		public int DamageBonus { get { return damageBonus; } }
		public int FireRateBonus { get { return fireRateBonus; } }

		/// <summary>Current squad plan as (actor name, wanted, alive) in spawn priority order.</summary>
		public IEnumerable<(string Unit, int Wanted, int Alive)> Composition()
		{
			foreach (var u in unitOrder)
				if (target[u] > 0 || CountAlive(u) > 0)
					yield return (u, target[u], CountAlive(u));
		}

		public int CountAlive(string unit)
		{
			var n = 0;
			foreach (var m in members)
				if (!m.IsDead && m.Info.Name == unit)
					n++;

			return n;
		}

		public static string UnitDisplayName(Actor self, string unit)
		{
			ActorInfo ai;
			if (self.World.Map.Rules.Actors.TryGetValue(unit.ToLowerInvariant(), out ai))
			{
				var t = ai.TraitInfoOrDefault<TooltipInfo>();
				if (t != null)
					return FluentProvider.GetMessage(t.Name);
			}

			return unit;
		}

		public NodeState GetState(HelixNodeInfo n)
		{
			if (done.Contains(n.Id))
				return NodeState.Done;

			if (Researching != null && Researching.Id == n.Id)
				return NodeState.Researching;

			string chosen;
			if (chosenByTier.TryGetValue(n.Tier, out chosen) && chosen != n.Id)
				return NodeState.Excluded;

			return PrerequisitesMet(n) ? NodeState.Available : NodeState.Locked;
		}

		bool PrerequisitesMet(HelixNodeInfo n)
		{
			if (n.Tier <= 1)
				return true;

			string prevId;
			if (!chosenByTier.TryGetValue(n.Tier - 1, out prevId) || !done.Contains(prevId))
				return false;

			var prev = Nodes.FirstOrDefault(x => x.Id == prevId);
			if (prev == null)
				return false;

			// Stay on your strand, or cross over right after a major upgrade.
			return prev.Side == n.Side || prev.Major;
		}

		public string ExplainLocked(HelixNodeInfo n)
		{
			if (n.Tier <= 1)
				return "";

			string prevId;
			if (!chosenByTier.TryGetValue(n.Tier - 1, out prevId) || !done.Contains(prevId))
				return "Finish tier " + (n.Tier - 1) + " first";

			return "Switching strands needs a major upgrade first";
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == ResearchOrder && order.TargetString != null)
				StartResearch(order.TargetString);
		}

		void StartResearch(string id)
		{
			if (Researching != null)
				return;

			var node = Nodes.FirstOrDefault(n => n.Id == id);
			if (node == null || GetState(node) != NodeState.Available)
				return;

			var resources = self.Owner.PlayerActor.Trait<PlayerResources>();
			if (!resources.TakeCash(node.Cost, true))
				return;

			Researching = node;
			ResearchTicksTotal = Math.Max(1, node.Duration);
			ResearchTicksLeft = ResearchTicksTotal;
			chosenByTier[node.Tier] = node.Id;
		}

		void FinishResearch()
		{
			var node = Researching;
			Researching = null;
			done.Add(node.Id);

			switch (node.Effect)
			{
				case HelixEffect.Speed:
					speedBonus += node.Value;
					break;
				case HelixEffect.FireRate:
					fireRateBonus += node.Value;
					break;
				case HelixEffect.Damage:
					damageBonus += node.Value;
					break;
				case HelixEffect.AddUnit:
					AddWanted(node.Unit, node.Count);
					if (node.Major && node.Unit != null)
						lastElite = node.Unit.ToLowerInvariant(); // later "reinforce" nodes grow this unit
					break;
				case HelixEffect.AddElite:
					AddWanted(lastElite ?? unitOrder.FirstOrDefault(), node.Count);
					break;
				case HelixEffect.Replace:
					var fromUnit = node.From != null ? node.From.ToLowerInvariant() : null;
					if (fromUnit != null && target.ContainsKey(fromUnit))
						target[fromUnit] = Math.Max(0, target[fromUnit] - node.Count);

					AddWanted(node.Unit, node.Count);
					if (node.Unit != null)
						lastElite = node.Unit.ToLowerInvariant();
					break;
			}

			RefreshBonuses();
			Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", "NewOptions", self.Owner.Faction.InternalName);
			TextNotificationsManager.AddTransientLine(self.Owner, node.Name + " complete.");
		}

		void AddWanted(string unit, int count)
		{
			if (string.IsNullOrEmpty(unit))
				return;

			unit = unit.ToLowerInvariant();
			if (!target.ContainsKey(unit))
			{
				unitOrder.Insert(0, unit); // newest units are trained first
				target[unit] = 0;
			}

			target[unit] += count;
		}

		void RefreshBonuses()
		{
			foreach (var m in members)
				if (!m.IsDead)
					ApplyBonuses(m);
		}

		void ApplyBonuses(Actor unit)
		{
			var sm = unit.TraitOrDefault<SquadMember>();
			if (sm == null)
				return;

			// Fire rate: +20% means 100 / 1.2 of the original reload delay.
			var reload = Math.Max(25, 10000 / (100 + fireRateBonus));
			sm.SetBonuses(100 + speedBonus, 100 + damageBonus, reload);
		}

		void INotifyProduction.UnitProduced(Actor self, Actor other, CPos exit)
		{
			members.Add(other);
			ApplyBonuses(other);
		}

		void Block(string reason)
		{
			Status = reason;
			StatusIsProblem = true;
			if (reason != lastLogged)
			{
				lastLogged = reason;
				Log.Write("debug", "AutoForce " + self.Info.Name + " (" + self.ActorID + "): " + reason);
			}
		}

		string NextWanted()
		{
			foreach (var u in unitOrder)
				if (target[u] > CountAlive(u))
					return u;

			return null;
		}

		void ITick.Tick(Actor self)
		{
			if (Researching != null && --ResearchTicksLeft <= 0)
				FinishResearch();

			members.RemoveAll(m => m.IsDead || m.Owner != self.Owner);

			if (spawnTimer > 0)
				spawnTimer--;

			var next = NextWanted();
			if (next == null)
			{
				Status = "Squad is at full strength";
				StatusIsProblem = false;
				return;
			}

			if (spawnTimer > 0)
			{
				Status = "Next: " + UnitDisplayName(self, next) + " in " + ((spawnTimer + 24) / 25) + "s";
				StatusIsProblem = false;
				return;
			}

			if (production == null)
				production = self.TraitOrDefault<Production>();

			if (production == null)
			{
				Block("this building has no Production trait");
				spawnTimer = 25;
				return;
			}

			ActorInfo actorInfo;
			if (!self.World.Map.Rules.Actors.TryGetValue(next, out actorInfo))
			{
				Block("unknown actor '" + next + "' in the roster, dropped it");
				target[next] = 0;
				return;
			}

			var cost = 0;
			if (Info.PayForUnits)
			{
				var valued = actorInfo.TraitInfoOrDefault<ValuedInfo>();
				cost = valued != null ? valued.Cost : 0;
			}

			var resources = self.Owner.PlayerActor.Trait<PlayerResources>();
			if (resources.GetCashAndResources() < cost)
			{
				Block("waiting for funds ($" + cost + ")");
				spawnTimer = 25;
				return;
			}

			var inits = new TypeDictionary
			{
				new OwnerInit(self.Owner),
				new FactionInit(production.Faction)
			};

			if (production.Produce(self, actorInfo, Info.ProductionType, inits, 0))
			{
				if (cost > 0)
					resources.TakeCash(cost);

				Status = "Trained " + UnitDisplayName(self, next);
				StatusIsProblem = false;
				lastLogged = null;
				spawnTimer = Info.SpawnInterval;
			}
			else
			{
				if (production.IsTraitDisabled)
					Block("production is disabled (low power or condition)");
				else if (production.IsTraitPaused)
					Block("production is paused");
				else
					Block("no free exit: the cells outside the building are blocked");

				spawnTimer = 25;
			}
		}
	}
}
