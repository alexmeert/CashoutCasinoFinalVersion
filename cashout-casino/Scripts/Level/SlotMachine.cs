using Godot;

namespace CashoutCasino
{
	public partial class SlotMachine : StaticBody3D
	{
		[Export] public int cost = 20;
		[Export] public float cooldownTime = 20f;

		private Label3D promptLabel;
		private Label3D resultLabel;
		private MeshInstance3D cooldownQuad;
		private ProgressBar cooldownBar;
		private StyleBoxFlat cooldownFill;

		private Character.Character playerInRange;
		private float cooldownRemaining = 0f;
		private bool onCooldown = false;

		private readonly RandomNumberGenerator rng = new RandomNumberGenerator();

		public override void _Ready()
		{
			rng.Randomize();

			promptLabel  = GetNodeOrNull<Label3D>("PromptLabel");
			resultLabel  = GetNodeOrNull<Label3D>("ResultLabel");
			cooldownQuad = GetNodeOrNull<MeshInstance3D>("CooldownBar/Quad");
			cooldownBar  = GetNodeOrNull<ProgressBar>("CooldownBar/SubViewport/Bar");

			// Wire SubViewport texture onto the quad
			var vp = GetNodeOrNull<SubViewport>("CooldownBar/SubViewport");
			if (vp != null && cooldownQuad != null)
			{
				var mat = new StandardMaterial3D();
				mat.ShadingMode    = BaseMaterial3D.ShadingModeEnum.Unshaded;
				mat.BillboardMode  = BaseMaterial3D.BillboardModeEnum.Enabled;
				mat.NoDepthTest    = true;
				mat.Transparency   = BaseMaterial3D.TransparencyEnum.Alpha;
				mat.AlbedoTexture  = vp.GetTexture();
				cooldownQuad.MaterialOverride = mat;
			}

			// Grab fill style so we can tint it
			if (cooldownBar != null)
			{
				cooldownFill = new StyleBoxFlat();
				cooldownFill.BgColor = new Color(0.9f, 0.7f, 0.1f, 1f);
				cooldownFill.CornerRadiusTopLeft     = 3;
				cooldownFill.CornerRadiusTopRight    = 3;
				cooldownFill.CornerRadiusBottomLeft  = 3;
				cooldownFill.CornerRadiusBottomRight = 3;
				cooldownBar.AddThemeStyleboxOverride("fill", cooldownFill);

				var bgStyle = new StyleBoxFlat();
				bgStyle.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
				cooldownBar.AddThemeStyleboxOverride("background", bgStyle);
			}

			var area = GetNodeOrNull<Area3D>("InteractArea");
			if (area != null)
			{
				area.BodyEntered += OnBodyEntered;
				area.BodyExited  += OnBodyExited;
			}

			if (promptLabel  != null) promptLabel.Visible  = false;
			if (resultLabel  != null) resultLabel.Visible  = false;
			if (cooldownQuad != null) cooldownQuad.Visible = false;
		}

		public override void _Process(double delta)
		{
			if (onCooldown)
			{
				cooldownRemaining -= (float)delta;

				if (cooldownRemaining <= 0f)
				{
					cooldownRemaining = 0f;
					onCooldown = false;
					if (cooldownQuad != null) cooldownQuad.Visible = false;
					UpdatePrompt();
				}
				else
				{
					UpdateCooldownBar();
				}
			}

			if (playerInRange == null) return;

			if (Input.IsActionJustPressed("interact"))
				TryUse(playerInRange);
		}

		private void UpdateCooldownBar()
		{
			if (cooldownBar == null) return;

			float ratio = cooldownRemaining / cooldownTime;
			cooldownBar.Value = ratio * 100f;

			// Shift yellow -> red as cooldown progresses
			if (cooldownFill != null)
				cooldownFill.BgColor = new Color(
					Mathf.Lerp(0.9f, 0.9f, ratio),
					Mathf.Lerp(0.1f, 0.7f, ratio),
					0.1f, 1f);
		}

		private void TryUse(Character.Character player)
		{
			if (onCooldown) return;

			if (player.GetCurrency() < cost)
			{
				ShowResult("Not enough $!");
				return;
			}

			player.ModifyCurrency(-cost);

			int roll   = rng.RandiRange(1, 4);
			int payout = 0;
			string resultText;

			switch (roll)
			{
				case 1:
					payout = 0;
					resultText = $"Nothing!  -${cost}";
					break;
				case 2:
					payout = cost / 2;
					resultText = $"Half back!  +${payout}";
					break;
				case 3:
					payout = cost;
					resultText = $"Return!  +${payout}";
					break;
				default: // 4
					payout = cost * 2;
					resultText = $"DOUBLE!  +${payout}";
					break;
			}

			if (payout > 0)
				player.ModifyCurrency(payout);

			ShowResult(resultText);

			onCooldown        = true;
			cooldownRemaining = cooldownTime;

			if (cooldownBar  != null) cooldownBar.Value   = 100f;
			if (cooldownQuad != null) cooldownQuad.Visible = true;

			UpdatePrompt();
		}

		private void ShowResult(string text)
		{
			if (resultLabel == null) return;
			resultLabel.Text    = text;
			resultLabel.Visible = true;

			GetTree().CreateTimer(3f).Timeout += () =>
			{
				if (IsInstanceValid(this) && resultLabel != null)
					resultLabel.Visible = false;
			};
		}

		private void UpdatePrompt()
		{
			if (promptLabel == null) return;
			promptLabel.Text = onCooldown
				? "[On cooldown]"
				: $"[E] Use Slot Machine  (${cost})";
		}

		private void OnBodyEntered(Node3D body)
		{
			if (body is Character.Character c)
			{
				playerInRange = c;
				UpdatePrompt();
				if (promptLabel != null) promptLabel.Visible = true;
				if (onCooldown && cooldownQuad != null) cooldownQuad.Visible = true;
			}
		}

		private void OnBodyExited(Node3D body)
		{
			if (body is Character.Character c && c == playerInRange)
			{
				playerInRange = null;
				if (promptLabel  != null) promptLabel.Visible  = false;
				if (resultLabel  != null) resultLabel.Visible  = false;
				if (cooldownQuad != null) cooldownQuad.Visible = false;
			}
		}
	}
}
