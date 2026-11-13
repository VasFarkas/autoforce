#region Copyright & License Information
/*
 * AutoForce mod for OpenRA.
 * This file is free software, made available under the terms of the
 * GNU General Public License v3 or later, the same as OpenRA itself.
 */
#endregion

using OpenRA.Traits;

namespace OpenRA.Mods.AutoForce.Traits
{
	public enum HelixSide { Quantity, Quality }

	public enum HelixEffect
	{
		// Percentage bonuses applied to every unit the building has produced.
		Speed,
		FireRate,
		Damage,

		// Squad composition changes.
		AddUnit, // Add Count of Unit to the squad.
		AddElite, // Add Count of the most recently unlocked elite unit (or the first roster unit).
		Replace, // Swap Count of From for Count of To (To becomes the new elite unit).

		Armor, // Value% less damage taken by the building's units.
		Storage, // Value extra resource storage for the owner while this building stands.
		Condition, // Grants the named Condition on the building (rules enable traits with it).
	}

	[Desc("One node of a building's helix upgrade tree. Add one of these traits per node. " +
		"Only one node per tier can ever be bought, and you may only change strands directly after a major node.")]
	public class HelixNodeInfo : TraitInfo<HelixNode>
	{
		[FieldLoader.Require]
		[Desc("Unique id of this node within the actor, e.g. ql3.")]
		public readonly string Id = null;

		[Desc("Which strand of the helix the node sits on.")]
		public readonly HelixSide Side = HelixSide.Quality;

		[Desc("1-based tier (row) of the node.")]
		public readonly int Tier = 1;

		[Desc("Major nodes allow switching to the other strand for the next tier.")]
		public readonly bool Major = false;

		[Desc("Display name.")]
		public readonly string Name = "Upgrade";

		[Desc("One-line description shown in the tooltip.")]
		public readonly string Description = "";

		[Desc("Cash cost, paid when research starts.")]
		public readonly int Cost = 500;

		[Desc("Research time in ticks.")]
		public readonly int Duration = 750;

		public readonly HelixEffect Effect = HelixEffect.Speed;

		[Desc("Percent for Speed/FireRate/Damage.")]
		public readonly int Value = 0;

		[Desc("Actor name for AddUnit, or the replacement actor for Replace.")]
		public readonly string Unit = null;

		[Desc("Actor to be replaced (Replace only).")]
		public readonly string From = null;

		[Desc("Only available to this side (Allies or Soviet). Empty = both sides.")]
		public readonly string Faction = "";

		[Desc("Condition granted by the Condition effect.")]
		public readonly string Condition = null;

		[Desc("Number of units added or replaced.")]
		public readonly int Count = 1;
	}

	public class HelixNode { }
}
