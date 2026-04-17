using Godot;

namespace CashoutCasino.UI
{
	public partial class Podium : Node3D
	{
		[Export] public NodePath ModelSpawnPointPath = "ModelSpawnPoint";
		[Export] public NodePath CameraPath         = "PodiumCamera";
		[Export] public NodePath WinnerLabelPath    = "CanvasLayer/Panel/VBox/WinnerLabel";
		[Export] public NodePath StatsLabelPath     = "CanvasLayer/Panel/VBox/StatsLabel";

		public override void _Ready()
		{
			// Spawn the winner's character model at the spawn point
			var spawnPoint = GetNode<Node3D>(ModelSpawnPointPath);
			var camera     = GetNode<Camera3D>(CameraPath);

			var scene  = GD.Load<PackedScene>("res://Characters/Player.tscn");
			var model  = scene.Instantiate<Node3D>();
			spawnPoint.AddChild(model);
			model.RotateY(Mathf.Pi);

			// Apply winner color and name via PlayerCharacter
			var pc = model.GetNodeOrNull<PlayerCharacter>("PlayerCharacter");
			if (pc != null)
			{
				pc.PlayerName = WinnerData.Name;
				pc.MyColor    = WinnerData.Color;
				if (pc.MyMesh != null)
					pc.MyMesh.MaterialOverride = new StandardMaterial3D
						{ AlbedoColor = WinnerData.Color };
				if (pc.NameLabel != null)
					pc.NameLabel.Text = WinnerData.Name;
			}

			// Play idle animation
			var anim = model.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
			anim?.Play("Idle");

			// Point camera at head height (model is ~1.7m tall, head ~1.6m)
			var headPos = spawnPoint.GlobalPosition + Vector3.Up * 1.55f;
			camera.GlobalPosition = headPos + spawnPoint.GlobalBasis.Z * 2.2f + Vector3.Up * 0.1f;
			camera.LookAt(headPos, Vector3.Up);
			camera.Current = true;

			// Build winner overlay text
			var winnerLabel = GetNode<Label>(WinnerLabelPath);
			var statsLabel  = GetNode<Label>(StatsLabelPath);

			winnerLabel.Text = $"  {WinnerData.Name}  ";
			float kd = WinnerData.Deaths > 0
				? (float)WinnerData.Kills / WinnerData.Deaths
				: WinnerData.Kills;
			statsLabel.Text  = $"Score: ${WinnerData.Score}     K/D: {kd:F2}     ({WinnerData.Kills}K / {WinnerData.Deaths}D)";
		}
	}
}
