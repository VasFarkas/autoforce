#region Copyright & License Information
/*
 * AutoForce mod for OpenRA.
 * This file is free software, made available under the terms of the
 * GNU General Public License v3 or later, the same as OpenRA itself.
 */
#endregion

using OpenRA.Graphics;
using OpenRA.Mods.AutoForce.Widgets;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.AutoForce.Traits
{
	[Desc("Attach to the World actor. Adds the helix upgrade panel to the in-game UI.")]
	public class AutoForceUIInfo : TraitInfo<AutoForceUI> { }

	public class AutoForceUI : IWorldLoaded
	{
		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			if (w.Type != WorldType.Regular)
				return;

			// The in-game UI is (re)built while the world loads, so add ours once that has finished.
			Game.RunAfterTick(() => Ui.Root.AddChild(new HelixTreeWidget(w, wr)));
		}
	}
}
