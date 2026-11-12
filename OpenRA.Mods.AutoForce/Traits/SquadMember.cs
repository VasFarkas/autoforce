#region Copyright & License Information
/*
 * AutoForce mod for OpenRA.
 * This file is free software, made available under the terms of the
 * GNU General Public License v3 or later, the same as OpenRA itself.
 */
#endregion

using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AutoForce.Traits
{
	[Desc("Attach to units that can be produced by an AutoSquad building. " +
		"The producing building pushes its upgrade bonuses into this trait.")]
	public class SquadMemberInfo : TraitInfo<SquadMember> { }

	public class SquadMember : ISpeedModifier, IFirepowerModifier, IReloadModifier, IDamageModifier
	{
		int speed = 100;
		int firepower = 100;
		int reload = 100;
		int damageTaken = 100;

		public void SetBonuses(int speedPercent, int firepowerPercent, int reloadPercent, int damageTakenPercent)
		{
			speed = speedPercent;
			firepower = firepowerPercent;
			reload = reloadPercent;
			damageTaken = damageTakenPercent;
		}

		int ISpeedModifier.GetSpeedModifier() { return speed; }
		int IFirepowerModifier.GetFirepowerModifier() { return firepower; }
		int IReloadModifier.GetReloadModifier() { return reload; }
		int IDamageModifier.GetDamageModifier(Actor attacker, Damage damage) { return damageTaken; }
	}
}
