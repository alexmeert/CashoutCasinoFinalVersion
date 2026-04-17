using Godot;
using System.Collections.Generic;
using System.Linq;

namespace CashoutCasino.UI
{
	public partial class Leaderboard : CanvasLayer
	{
		private VBoxContainer _rows;

		public override void _Ready()
		{
			_rows = GetNode<VBoxContainer>("Panel/MarginContainer/VBox/Rows");
			Visible = false;
		}

		public void Refresh()
		{
			foreach (Node child in _rows.GetChildren())
				child.QueueFree();

			var players = new List<CashoutCasino.Character.Player>();
			foreach (var node in GetTree().GetNodesInGroup("Player"))
				if (node is CashoutCasino.Character.Player p)
					players.Add(p);

			players.Sort((a, b) => b.GetFinalScore().CompareTo(a.GetFinalScore()));

			foreach (var p in players)
			{
				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 0);
				AddCell(row, p.GetDisplayName(), 200, new Color(0.9f, 0.85f, 0.6f, 1f));
				AddCell(row, $"${p.GetFinalScore()}", 120, ScoreColor(p.GetFinalScore()));
				AddCell(row, $"{p.Kills}", 80, new Color(0.3f, 1f, 0.4f, 1f));
				AddCell(row, $"{p.Deaths}", 80, new Color(1f, 0.35f, 0.35f, 1f));
				_rows.AddChild(row);
			}
		}

		private static void AddCell(HBoxContainer row, string text, int width, Color color)
		{
			var label = new Label();
			label.Text = text;
			label.CustomMinimumSize = new Vector2(width, 0);
			label.AddThemeFontSizeOverride("font_size", 16);
			label.AddThemeColorOverride("font_color", color);
			label.VerticalAlignment = VerticalAlignment.Center;

			
			var font = GD.Load<FontFile>("res://Assets/upheaval/upheavtt.ttf");
			label.AddThemeFontOverride("font", font);

			row.AddChild(label);
		}
		
		

		private static Color ScoreColor(int score) => score switch
		{
			> 0 => new Color(0.3f, 1f, 0.4f, 1f),
			< 0 => new Color(1f, 0.35f, 0.35f, 1f),
			_   => new Color(0.8f, 0.8f, 0.8f, 1f)
		};
	}
}
