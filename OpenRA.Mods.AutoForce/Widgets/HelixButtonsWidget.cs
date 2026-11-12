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
using OpenRA.Graphics;
using OpenRA.Mods.AutoForce.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.AutoForce.Widgets
{
	/// <summary>
	/// One button per upgradable building type, in a column left of the sidebar. Greyed out when you own none of
	/// that type; click to open the upgrade tree of one (click again to cycle through several).
	/// </summary>
	public class HelixButtonsWidget : Widget
	{
		const int ButtonWidth = 62;
		const int ButtonHeight = 22;
		const int Gap = 3;
		const int SidebarWidth = 250;
		const int Top = 300;

		static readonly (string Actor, string Label, int Rgb)[] Entries =
		{
			("AFINF", "INF", 0x5CD65C), ("AFAT", "AT", 0xE8604C), ("AFDEMO", "DEMO", 0xF0A030),
			("AFMAIN", "MAIN", 0x5AA0F0), ("AFUTIL", "UTIL", 0x4CD6D6), ("AFSIEGE", "SIEGE", 0xC070E0),
			("HPAD", "HELIPAD", 0xE0E0E0), ("AFLD", "AIRFIELD", 0xE0E0E0), ("SYRD", "NAVAL", 0x70B8F0),
			("SPEN", "SUB PEN", 0x70B8F0), ("PROC", "REFINERY", 0xE8C050), ("FACT", "YARD", 0xE8C050),
			("FIX", "DEPOT", 0xA0D080),
		};

		readonly World world;
		readonly List<Actor>[] owned = new List<Actor>[Entries.Length];
		int hover = -1;

		public HelixButtonsWidget(World world)
		{
			this.world = world;
			for (var i = 0; i < owned.Length; i++)
				owned[i] = new List<Actor>();

			IsVisible = () => true;
			Bounds = new WidgetBounds(0, 0, ButtonWidth, Entries.Length * (ButtonHeight + Gap));
		}

		public override void Tick()
		{
			var screen = Game.Renderer.Resolution;
			Bounds = new WidgetBounds(screen.Width - SidebarWidth - ButtonWidth - 4, Top, ButtonWidth, Entries.Length * (ButtonHeight + Gap));

			foreach (var l in owned)
				l.Clear();

			if (world.LocalPlayer == null)
				return;

			foreach (var a in world.ActorsHavingTrait<AutoSquad>())
				Add(a);
			foreach (var a in world.ActorsHavingTrait<OnDemandBuild>())
				Add(a);

			hover = IndexAt(Viewport.LastMousePos);
		}

		void Add(Actor a)
		{
			if (a.IsDead || !a.IsInWorld || a.Owner != world.LocalPlayer)
				return;

			var name = a.Info.Name.ToUpperInvariant();
			for (var i = 0; i < Entries.Length; i++)
			{
				if (Entries[i].Actor != name || owned[i].Contains(a))
					continue;

				var s = a.TraitOrDefault<AutoSquad>();
				if (s != null && !s.HasTree && a.TraitOrDefault<OnDemandBuild>() == null)
					continue;

				owned[i].Add(a);
			}
		}

		Rectangle Rect(int i)
		{
			var o = RenderOrigin;
			return new Rectangle(o.X, o.Y + i * (ButtonHeight + Gap), ButtonWidth, ButtonHeight);
		}

		int IndexAt(int2 p)
		{
			for (var i = 0; i < Entries.Length; i++)
				if (Rect(i).Contains(p))
					return i;

			return -1;
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			var i = IndexAt(mi.Location);
			if (i < 0)
				return false;

			if (mi.Event == MouseInputEvent.Down && mi.Button == MouseButton.Left && owned[i].Count > 0)
			{
				var list = owned[i];
				var at = list.IndexOf(HelixTreeWidget.Pinned);
				HelixTreeWidget.Pinned = list[(at + 1) % list.Count];
				HelixTreeWidget.ClearSuppression = true;
			}

			return mi.Event != MouseInputEvent.Move;
		}

		public override void Draw()
		{
			for (var i = 0; i < Entries.Length; i++)
			{
				var r = Rect(i);
				var count = owned[i].Count;
				var active = count > 0 && owned[i].Contains(HelixTreeWidget.Pinned);
				var c = Color.FromArgb(255, (Entries[i].Rgb >> 16) & 255, (Entries[i].Rgb >> 8) & 255, Entries[i].Rgb & 255);

				var bg = count == 0 ? Color.FromArgb(255, 30, 32, 36)
					: active ? Color.FromArgb(255, 70, 80, 96)
					: hover == i ? Color.FromArgb(255, 52, 60, 72) : Color.FromArgb(255, 20, 26, 34);
				var border = count == 0 ? Color.FromArgb(255, 54, 58, 64) : c;
				var text = count == 0 ? Color.FromArgb(255, 96, 100, 108) : Color.FromArgb(255, 240, 242, 246);

				WidgetUtils.FillRectWithColor(r, border);
				WidgetUtils.FillRectWithColor(new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), bg);

				var label = count > 1 ? Entries[i].Label + " x" + count : Entries[i].Label;
				var font = Game.Renderer.Fonts["TinyBold"];
				var w = font.Measure(label).X;
				font.DrawTextWithShadow(label, new float2(r.X + (r.Width - w) / 2, r.Y + 6), text, Color.Black, 1);
			}
		}
	}
}
