#region Copyright & License Information
/*
 * AutoForce mod for OpenRA.
 * This file is free software, made available under the terms of the
 * GNU General Public License v3 or later, the same as OpenRA itself.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Mods.Common;
using OpenRA.Traits;

namespace OpenRA.Mods.AutoForce.Traits
{
	/// <summary>Research progress of one upgrade tree. Shared by every building of the same tree group.</summary>
	public class HelixGroupState
	{
		public readonly HashSet<string> Done = new HashSet<string>();
		public readonly List<string> DoneOrder = new List<string>();
		public readonly Dictionary<int, string> ChosenByTier = new Dictionary<int, string>();

		public string ResearchingId;
		public string ResearchingName;
		public int TicksLeft;
		public int TicksTotal;

		public void Start(string id, int tier, string name, int ticks)
		{
			ResearchingId = id;
			ResearchingName = name;
			ChosenByTier[tier] = id;
			TicksTotal = ticks;
			TicksLeft = ticks;
		}
	}

	[Desc("Put this on the Player actor. Keeps the research state of every helix tree (shared trees such as the universal " +
		"infantry and factory trees are researched once for all their buildings).")]
	public class HelixTreesInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new HelixTrees(init.Self); }
	}

	public class HelixTrees : ITick
	{
		readonly Actor self;
		readonly Dictionary<string, HelixGroupState> groups = new Dictionary<string, HelixGroupState>();

		public HelixTrees(Actor self) { this.self = self; }

		public HelixGroupState Get(string group, Actor building)
		{
			var key = string.IsNullOrEmpty(group) ? "type:" + building.Info.Name : group;
			HelixGroupState s;
			if (!groups.TryGetValue(key, out s))
			{
				s = new HelixGroupState();
				groups[key] = s;
			}

			return s;
		}

		void ITick.Tick(Actor self)
		{
			foreach (var s in groups.Values)
			{
				if (s.ResearchingId == null || --s.TicksLeft > 0)
					continue;

				s.Done.Add(s.ResearchingId);
				s.DoneOrder.Add(s.ResearchingId);
				s.ResearchingId = null;

				Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", "NewOptions", self.Owner.Faction.InternalName);
				TextNotificationsManager.AddTransientLine(self.Owner, s.ResearchingName + " complete.");
			}
		}
	}
}
