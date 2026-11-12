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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.AutoForce.Widgets
{
	/// <summary>
	/// The double-helix upgrade menu. It is docked over the right-hand sidebar's build area while one of
	/// your auto-training buildings is selected. Left strand = quantity, right strand = quality; the strands
	/// cross after every major node. The "Build" button flips back to the normal build palette.
	/// </summary>
	public class HelixTreeWidget : Widget
	{
		// Matches the sidebar's production container in mods/ra/chrome/ingame-player.yaml.
		const int SidebarRightMargin = 210; // 40px right of the sidebar edge: the build tabs stay uncovered
		const int SidebarTop = 300;
		const int PanelWidth = 198;

		const int HeaderHeight = 40;
		const int FooterHeight = 150;
		const int MinRowHeight = 28;
		const int MaxRowHeight = 44;
		const int MinorRadius = 11;
		const int MajorRadius = 15;

		static readonly Color QuantityColor = Color.FromArgb(255, 232, 163, 61);
		static readonly Color QualityColor = Color.FromArgb(255, 76, 201, 240);
		static readonly Color PanelColor = Color.FromArgb(255, 14, 18, 24);
		static readonly Color BorderColor = Color.FromArgb(255, 90, 100, 112);
		static readonly Color DimColor = Color.FromArgb(255, 70, 76, 84);
		static readonly Color TextColor = Color.FromArgb(255, 235, 238, 242);
		static readonly Color SubTextColor = Color.FromArgb(255, 160, 168, 178);
		static readonly Color GoodColor = Color.FromArgb(255, 120, 220, 120);
		static readonly Color BadColor = Color.FromArgb(255, 235, 100, 90);
		static readonly Color Shadow = Color.FromArgb(255, 0, 0, 0);

		readonly World world;
		AutoSquad squad;
		Actor squadActor;
		/// <summary>Building opened by one of the helix buttons; takes priority over the selection.</summary>
		public static Actor Pinned;
		public static bool ClearSuppression;

		Actor suppressedActor;
		OnDemandBuild demand;
		Actor lastFound;
		ProductionPaletteWidget palette;
		ProductionQueue queueWhenOpened;
		HelixNodeInfo hovered;
		bool buildButtonHovered;
		int maxTier;
		int rowHeight = MaxRowHeight;

		public HelixTreeWidget(World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			Bounds = new WidgetBounds(0, 0, 0, 0);
			IsVisible = () => true; // must stay visible so Tick keeps running; hidden state = zero-size Bounds
		}

		public override void Tick()
		{
			Actor found = null;
			AutoSquad foundSquad = null;
			OnDemandBuild foundDemand = null;

			if (world.LocalPlayer != null)
			{
				foreach (var a in world.Selection.Actors)
				{
					if (a.IsDead || !a.IsInWorld || a.Owner != world.LocalPlayer)
						continue;

					var s = a.TraitOrDefault<AutoSquad>();
					if (s != null && s.HasTree)
					{
						found = a;
						foundSquad = s;
						break;
					}

					var d = a.TraitOrDefault<OnDemandBuild>();
					if (d != null)
					{
						found = a;
						foundDemand = d;
						break;
					}
				}
			}

			if (ClearSuppression)
			{
				ClearSuppression = false;
				suppressedActor = null;
				lastFound = null; // re-read the palette queue when the panel opens
			}

			if (Pinned != null && (Pinned.IsDead || !Pinned.IsInWorld || Pinned.Owner != world.LocalPlayer))
				Pinned = null;

			if (Pinned != null)
			{
				found = Pinned;
				foundSquad = Pinned.TraitOrDefault<AutoSquad>();
				foundDemand = foundSquad != null && foundSquad.HasTree ? null : Pinned.TraitOrDefault<OnDemandBuild>();
				if (foundSquad != null && !foundSquad.HasTree)
					foundSquad = null;
			}

			// "Build" button: stay on the normal palette until the selection changes.
			if (suppressedActor != null && found != suppressedActor)
				suppressedActor = null;

			// Clicking one of the sidebar tabs (buildings / defenses ...) switches the palette's queue:
			// treat that as "I want the build menu" and step aside until the selection changes.
			if (palette == null)
				palette = Ui.Root.GetOrNull<ProductionPaletteWidget>("PRODUCTION_PALETTE");

			var queueNow = palette != null ? palette.CurrentQueue : null;
			if (found != lastFound)
			{
				lastFound = found;
				queueWhenOpened = queueNow;
			}
			else if (found != null && found != suppressedActor && queueNow != queueWhenOpened)
			{
				suppressedActor = found;
				Pinned = null;
			}

			if (found != null && found == suppressedActor)
			{
				found = null;
				foundSquad = null;
				foundDemand = null;
			}

			squadActor = found;
			squad = foundSquad;
			demand = foundDemand;

			if (demand != null)
			{
				var scr = Game.Renderer.Resolution;
				Bounds = new WidgetBounds(scr.Width - SidebarRightMargin, SidebarTop, PanelWidth, 128);
				var m = Viewport.LastMousePos;
				buildButtonHovered = BuildButtonRect().Contains(m);
				hovered = null;
				return;
			}

			if (squad == null)
			{
				hovered = null;
				buildButtonHovered = false;
				Bounds = new WidgetBounds(0, 0, 0, 0); // no hit area while nothing is selected
				return;
			}

			maxTier = squad.MaxTier;
			var screen = Game.Renderer.Resolution;
			var available = screen.Height - SidebarTop - 8;
			rowHeight = Math.Max(MinRowHeight, Math.Min(MaxRowHeight, (available - HeaderHeight - FooterHeight) / Math.Max(1, maxTier)));
			var height = HeaderHeight + maxTier * rowHeight + FooterHeight;
			Bounds = new WidgetBounds(screen.Width - SidebarRightMargin, SidebarTop, PanelWidth, height);

			var mouse = Viewport.LastMousePos;
			buildButtonHovered = BuildButtonRect().Contains(mouse);
			hovered = buildButtonHovered ? null : HitTest(mouse);
		}

		Rectangle BuildButtonRect()
		{
			var o = RenderOrigin;
			return new Rectangle(o.X + PanelWidth - 62, o.Y + 7, 54, 18);
		}

		float2 NodePos(HelixNodeInfo n)
		{
			var o = RenderOrigin;
			var cx = o.X + PanelWidth / 2f;
			var phase = (n.Tier - 1) % 3; // 0 = just after a major, 1 = widest, 2 = major (strands meet)
			var spread = phase == 0 ? 52f : phase == 1 ? 74f : 24f;
			var x = n.Side == HelixSide.Quantity ? cx - spread : cx + spread;
			var y = o.Y + HeaderHeight + (n.Tier - 1) * rowHeight + rowHeight / 2f;
			return new float2(x, y);
		}

		static int Radius(HelixNodeInfo n)
		{
			return n.Major ? MajorRadius : MinorRadius;
		}

		HelixNodeInfo HitTest(int2 p)
		{
			if (squad == null)
				return null;

			foreach (var n in squad.Nodes)
			{
				var c = NodePos(n);
				var dx = p.X - c.X;
				var dy = p.Y - c.Y;
				var r = Radius(n) + 3;
				if (dx * dx + dy * dy <= r * r)
					return n;
			}

			return null;
		}

		Rectangle DemandButtonRect()
		{
			var o = RenderOrigin;
			return new Rectangle(o.X + 10, o.Y + 56, PanelWidth - 20, 26);
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (demand != null)
			{
				if (mi.Event == MouseInputEvent.Down && mi.Button == MouseButton.Left)
				{
					if (BuildButtonRect().Contains(mi.Location))
					{
						suppressedActor = squadActor;
						Pinned = null;
					}
					else if (DemandButtonRect().Contains(mi.Location) && !demand.Building)
						world.IssueOrder(new Order(OnDemandBuild.BuildOrder, squadActor, false));
				}

				return mi.Event != MouseInputEvent.Move;
			}

			if (squad == null)
				return false;

			if (mi.Event == MouseInputEvent.Down && mi.Button == MouseButton.Left)
			{
				if (BuildButtonRect().Contains(mi.Location))
				{
					suppressedActor = squadActor;
					Pinned = null;
					return true;
				}

				var n = HitTest(mi.Location);
				if (n != null)
				{
					if (squad.GetState(n) == NodeState.Available && squad.Researching == null)
						world.IssueOrder(new Order(AutoSquad.ResearchOrder, squadActor, false) { TargetString = n.Id });

					return true;
				}
			}

			// Swallow clicks on the panel so they do not move or deselect units behind it.
			return mi.Event != MouseInputEvent.Move;
		}

		static Color SideColor(HelixNodeInfo n)
		{
			return n.Side == HelixSide.Quantity ? QuantityColor : QualityColor;
		}

		static Color Fade(Color c, int alpha)
		{
			return Color.FromArgb(alpha, c.R, c.G, c.B);
		}

		static float SmoothStep(float t)
		{
			return t * t * (3f - 2f * t);
		}

		void Line(float2 a, float2 b, float width, Color c)
		{
			Game.Renderer.RgbaColorRenderer.DrawLine(new float3(a.X, a.Y, 0), new float3(b.X, b.Y, 0), width, c);
		}

		// An S-shaped link between two nodes: the strands of the helix.
		void Strand(float2 a, float2 b, float width, Color c)
		{
			var prev = a;
			const int steps = 10;
			for (var i = 1; i <= steps; i++)
			{
				var t = i / (float)steps;
				var p = new float2(a.X + (b.X - a.X) * SmoothStep(t), a.Y + (b.Y - a.Y) * t);
				Line(prev, p, width, c);
				prev = p;
			}
		}

		void Text(string font, string text, float x, float y, Color c)
		{
			Game.Renderer.Fonts[font].DrawTextWithShadow(text, new float2((int)x, (int)y), c, Shadow, 1);
		}

		void TextCentered(string font, string text, float cx, float y, Color c)
		{
			var w = Game.Renderer.Fonts[font].Measure(text).X;
			Text(font, text, cx - w / 2f, y, c);
		}

		static List<string> Wrap(string font, string text, int maxWidth)
		{
			var lines = new List<string>();
			if (string.IsNullOrEmpty(text))
				return lines;

			var f = Game.Renderer.Fonts[font];
			var current = "";
			foreach (var word in text.Split(' '))
			{
				var candidate = current.Length == 0 ? word : current + " " + word;
				if (current.Length > 0 && f.Measure(candidate).X > maxWidth)
				{
					lines.Add(current);
					current = word;
				}
				else
					current = candidate;
			}

			if (current.Length > 0)
				lines.Add(current);

			return lines;
		}

		// Draws wrapped text and returns the y coordinate below it.
		float WrappedText(string font, string text, float x, float y, int maxWidth, int maxLines, int lineHeight, Color c)
		{
			var lines = Wrap(font, text, maxWidth);
			for (var i = 0; i < lines.Count && i < maxLines; i++)
			{
				Text(font, lines[i], x, y, c);
				y += lineHeight;
			}

			return y;
		}

		void DrawDemand()
		{
			var o = RenderOrigin;
			var panel = new Rectangle(o.X, o.Y, Bounds.Width, Bounds.Height);
			WidgetUtils.FillRectWithColor(panel, PanelColor);
			WidgetUtils.FillRectWithColor(new Rectangle(panel.X, panel.Y, panel.Width, 2), BorderColor);
			WidgetUtils.FillRectWithColor(new Rectangle(panel.X, panel.Bottom - 2, panel.Width, 2), BorderColor);
			WidgetUtils.FillRectWithColor(new Rectangle(panel.X, panel.Y, 2, panel.Height), BorderColor);
			WidgetUtils.FillRectWithColor(new Rectangle(panel.Right - 2, panel.Y, 2, panel.Height), BorderColor);

			Text("Bold", "Service Depot", o.X + 10, o.Y + 8, TextColor);
			var tb = BuildButtonRect();
			WidgetUtils.FillRectWithColor(tb, buildButtonHovered ? Color.FromArgb(255, 60, 68, 80) : Color.FromArgb(255, 32, 38, 46));
			TextCentered("TinyBold", "< Build", tb.X + tb.Width / 2f, tb.Y + 4, TextColor);
			TextCentered("Tiny", AutoSquad.BuildStamp, o.X + PanelWidth / 2f, o.Y + 32, DimColor);

			var cash = world.LocalPlayer.PlayerActor.Trait<PlayerResources>().GetCashAndResources();
			var button = DemandButtonRect();
			var canAfford = cash >= demand.Cost;
			var enabled = !demand.Building && canAfford;
			var hover = button.Contains(Viewport.LastMousePos);
			WidgetUtils.FillRectWithColor(button, enabled ? (hover ? Color.FromArgb(255, 56, 110, 60) : Color.FromArgb(255, 36, 80, 42)) : Color.FromArgb(255, 34, 38, 44));
			WidgetUtils.FillRectWithColor(new Rectangle(button.X, button.Y, button.Width, 1), BorderColor);
			WidgetUtils.FillRectWithColor(new Rectangle(button.X, button.Bottom - 1, button.Width, 1), BorderColor);
			TextCentered("Bold", "Build " + demand.UnitName + (demand.Cost > 0 ? "  $" + demand.Cost : ""), button.X + button.Width / 2f, button.Y + 6, enabled ? TextColor : SubTextColor);

			var y = button.Bottom + 8;
			if (demand.Building)
			{
				var bar = new Rectangle(o.X + 10, y, PanelWidth - 20, 8);
				WidgetUtils.FillRectWithColor(bar, DimColor);
				var frac = 1f - demand.TicksLeft / (float)demand.TicksTotal;
				WidgetUtils.FillRectWithColor(new Rectangle(bar.X, bar.Y, (int)(bar.Width * frac), bar.Height), QualityColor);
				y += 14;
			}

			var status = !demand.Building && !canAfford ? "Not enough funds" : demand.Status;
			var bad = demand.StatusIsProblem || (!demand.Building && !canAfford);
			WrappedText("Small", status, o.X + 10, y, PanelWidth - 20, 2, 13, bad ? BadColor : GoodColor);
		}

		public override void Draw()
		{
			if (demand != null)
			{
				DrawDemand();
				return;
			}

			if (squad == null)
				return;

			var o = RenderOrigin;
			var panel = new Rectangle(o.X, o.Y, Bounds.Width, Bounds.Height);
			WidgetUtils.FillRectWithColor(panel, PanelColor);
			WidgetUtils.FillRectWithColor(new Rectangle(panel.X, panel.Y, panel.Width, 2), BorderColor);
			WidgetUtils.FillRectWithColor(new Rectangle(panel.X, panel.Bottom - 2, panel.Width, 2), BorderColor);
			WidgetUtils.FillRectWithColor(new Rectangle(panel.X, panel.Y, 2, panel.Height), BorderColor);
			WidgetUtils.FillRectWithColor(new Rectangle(panel.Right - 2, panel.Y, 2, panel.Height), BorderColor);

			Text("Bold", squad.Title, o.X + 10, o.Y + 8, TextColor);

			var button = BuildButtonRect();
			WidgetUtils.FillRectWithColor(button, buildButtonHovered ? Color.FromArgb(255, 60, 68, 80) : Color.FromArgb(255, 32, 38, 46));
			WidgetUtils.FillRectWithColor(new Rectangle(button.X, button.Y, button.Width, 1), BorderColor);
			WidgetUtils.FillRectWithColor(new Rectangle(button.X, button.Bottom - 1, button.Width, 1), BorderColor);
			WidgetUtils.FillRectWithColor(new Rectangle(button.X, button.Y, 1, button.Height), BorderColor);
			WidgetUtils.FillRectWithColor(new Rectangle(button.Right - 1, button.Y, 1, button.Height), BorderColor);
			TextCentered("TinyBold", "< Build", button.X + button.Width / 2f, button.Y + 4, TextColor);

			Text("TinyBold", "QUANTITY", o.X + 10, o.Y + 26, QuantityColor);
			var qw = Game.Renderer.Fonts["TinyBold"].Measure("QUALITY").X;
			Text("TinyBold", "QUALITY", o.X + PanelWidth - 10 - qw, o.Y + 26, QualityColor);

			TextCentered("Tiny", AutoSquad.BuildStamp, o.X + PanelWidth / 2f, o.Y + 28, DimColor);

			DrawLinks();
			DrawNodes();
			DrawFooter(o);
		}

		void DrawLinks()
		{
			var byTier = squad.Nodes.GroupBy(n => n.Tier).ToDictionary(g => g.Key, g => g.ToList());

			foreach (var kv in byTier)
			{
				var tier = kv.Key;

				// Rung between the two exclusive choices of a tier.
				if (kv.Value.Count == 2)
					Line(NodePos(kv.Value[0]), NodePos(kv.Value[1]), 1, Fade(DimColor, 140));

				List<HelixNodeInfo> next;
				if (!byTier.TryGetValue(tier + 1, out next))
					continue;

				foreach (var a in kv.Value)
				{
					foreach (var b in next)
					{
						var sameStrand = a.Side == b.Side;
						if (!sameStrand && !a.Major)
							continue; // strands only cross after a major node

						var taken = squad.GetState(a) == NodeState.Done &&
							(squad.GetState(b) == NodeState.Done || squad.GetState(b) == NodeState.Researching);

						var color = taken ? SideColor(b) : Fade(sameStrand ? SideColor(a) : TextColor, 90);
						Strand(NodePos(a), NodePos(b), taken ? 4 : 2, color);
					}
				}
			}
		}

		void DrawNodes()
		{
			var rend = Game.Renderer.RgbaColorRenderer;
			foreach (var n in squad.Nodes)
			{
				var state = squad.GetState(n);
				var p = NodePos(n);
				var r = Radius(n);
				var side = SideColor(n);

				Color fill;
				Color ring;
				switch (state)
				{
					case NodeState.Done:
						fill = side;
						ring = TextColor;
						break;
					case NodeState.Researching:
						fill = Fade(side, 110);
						ring = side;
						break;
					case NodeState.Available:
						fill = Color.FromArgb(255, 24, 30, 38);
						ring = side;
						break;
					case NodeState.Excluded:
						fill = Color.FromArgb(255, 18, 20, 24);
						ring = Color.FromArgb(255, 50, 54, 60);
						break;
					default:
						fill = Color.FromArgb(255, 28, 32, 38);
						ring = DimColor;
						break;
				}

				if (n == hovered)
					rend.FillEllipse(new float3(p.X - r - 4, p.Y - r - 4, 0), new float3(p.X + r + 4, p.Y + r + 4, 0), Fade(TextColor, 90));

				var outer = r + (n.Major ? 3 : 2);
				rend.FillEllipse(new float3(p.X - outer, p.Y - outer, 0), new float3(p.X + outer, p.Y + outer, 0), n.Major && state != NodeState.Excluded ? Color.FromArgb(255, 238, 200, 80) : ring);
				rend.FillEllipse(new float3(p.X - r, p.Y - r, 0), new float3(p.X + r, p.Y + r, 0), fill);

				if (state == NodeState.Researching && squad.ResearchTicksTotal > 0)
				{
					var progress = 1f - squad.ResearchTicksLeft / (float)squad.ResearchTicksTotal;
					var h = (int)(2 * r * progress);
					var rect = new Rectangle((int)p.X - r, (int)p.Y + r - h, 2 * r, h);
					WidgetUtils.FillRectWithColor(rect, Fade(side, 200));
				}

				var label = n.Major ? "M" + n.Tier : n.Tier.ToString();
				TextCentered("Tiny", label, p.X, p.Y - 5, state == NodeState.Excluded ? DimColor : TextColor);
			}
		}

		void DrawFooter(int2 o)
		{
			var y = o.Y + HeaderHeight + maxTier * rowHeight + 6;
			var x = o.X + 10;
			var textWidth = PanelWidth - 20;
			WidgetUtils.FillRectWithColor(new Rectangle(o.X + 6, y - 4, PanelWidth - 12, 1), BorderColor);

			if (hovered != null)
			{
				var state = squad.GetState(hovered);
				Text("Bold", hovered.Name + (hovered.Major ? " (MAJOR)" : ""), x, y, SideColor(hovered));
				var below = WrappedText("Small", hovered.Description, x, y + 17, textWidth, 3, 13, TextColor);

				string status;
				Color statusColor;
				var cash = world.LocalPlayer.PlayerActor.Trait<PlayerResources>().GetCashAndResources();
				switch (state)
				{
					case NodeState.Done:
						status = "Researched";
						statusColor = GoodColor;
						break;
					case NodeState.Researching:
						status = "Researching...";
						statusColor = SideColor(hovered);
						break;
					case NodeState.Excluded:
						status = "Locked: you chose the other node on this tier";
						statusColor = BadColor;
						break;
					case NodeState.Locked:
						status = squad.ExplainLocked(hovered);
						statusColor = BadColor;
						break;
					default:
						status = squad.Researching != null ? "Wait for the current research" :
							cash < hovered.Cost ? "Not enough funds" : "Click to research";
						statusColor = squad.Researching != null || cash < hovered.Cost ? BadColor : GoodColor;
						break;
				}

				Text("Small", "$" + hovered.Cost + "   " + (hovered.Duration / 25) + "s", x, below + 2, SubTextColor);
				WrappedText("Small", status, x, below + 16, textWidth, 2, 13, statusColor);
			}
			else
			{
				Text("Bold", squad.HasUnits ? "Squad" : "Base", x, y, TextColor);
				var line = 0;
				foreach (var c in squad.Composition())
				{
					Text("Small", c.Alive + "/" + c.Wanted + "  " + AutoSquad.UnitDisplayName(squadActor, c.Unit), x, y + 17 + line * 13, SubTextColor);
					if (++line >= 5)
						break;
				}

				WrappedText("Small", squad.Status, x, o.Y + Bounds.Height - 58, textWidth, 2, 13, squad.StatusIsProblem ? BadColor : GoodColor);

				var bonus = squad.Info.AffectsAllHarvesters
					? "Spd +" + squad.SpeedBonus + "%  Armor +" + squad.ArmorBonus + "%  Silo +" + squad.StorageBonus
					: !squad.HasUnits
						? "Applies to the whole base"
						: "Spd +" + squad.SpeedBonus + "%  Dmg +" + squad.DamageBonus + "%  Rate +" + squad.FireRateBonus + "%" +
							(squad.ArmorBonus > 0 ? "  Arm +" + squad.ArmorBonus + "%" : "");
				Text("Tiny", bonus, x, o.Y + Bounds.Height - 30, QualityColor);
			}

			if (squad.Researching != null)
			{
				var bar = new Rectangle(o.X + 10, o.Y + Bounds.Height - 18, PanelWidth - 20, 8);
				WidgetUtils.FillRectWithColor(bar, DimColor);
				var frac = 1f - squad.ResearchTicksLeft / (float)Math.Max(1, squad.ResearchTicksTotal);
				WidgetUtils.FillRectWithColor(new Rectangle(bar.X, bar.Y, (int)(bar.Width * frac), bar.Height), SideColor(squad.Researching));
			}
		}
	}
}
