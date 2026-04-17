using Godot;

namespace CashoutCasino
{
	/// <summary>
	/// ATM Machine interactable.
	/// - Only usable when the player's currency is 0
	/// - Gives ATM_LOAN ($50) in ammo/currency
	/// - Also gives half of INITIAL_SPAWN back if broke
	/// - Marks the loan as score debt on the player (-$50 shown in HUD)
	/// - 20 second cooldown
	/// </summary>
	public partial class ATMMachine : StaticBody3D
	{
		[Export] public float cooldownTime = 20f;

		private Label3D promptLabel;
		private Label3D resultLabel;
		private MeshInstance3D cooldownQuad;
		private ProgressBar cooldownBar;
		private StyleBoxFlat cooldownFill;

		private Character.Player playerInRange;
		private float cooldownRemaining = 0f;
		private bool onCooldown = false;

		public override void _Ready()
		{
			promptLabel  = GetNodeOrNull<Label3D>("PromptLabel");
			resultLabel  = GetNodeOrNull<Label3D>("ResultLabel");
			cooldownQuad = GetNodeOrNull<MeshInstance3D>("CooldownBar/Quad");
			cooldownBar  = GetNodeOrNull<ProgressBar>("CooldownBar/SubViewport/Bar");

			var vp = GetNodeOrNull<SubViewport>("CooldownBar/SubViewport");
			if (vp != null && cooldownQuad != null)
			{
				var mat = new StandardMaterial3D();
				mat.ShadingMode   = BaseMaterial3D.ShadingModeEnum.Unshaded;
				mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
				mat.NoDepthTest   = true;
				mat.Transparency  = BaseMaterial3D.TransparencyEnum.Alpha;
				mat.AlbedoTexture = vp.GetTexture();
				cooldownQuad.MaterialOverride = mat;
			}

			if (cooldownBar != null)
			{
				cooldownFill = new StyleBoxFlat();
				cooldownFill.BgColor             = new Color(0.2f, 0.6f, 1f, 1f);
				cooldownFill.CornerRadiusTopLeft  = 3;
				cooldownFill.CornerRadiusTopRight = 3;
				cooldownFill.CornerRadiusBottomLeft  = 3;
				cooldownFill.CornerRadiusBottomRight = 3;
				cooldownBar.AddThemeStyleboxOverride("fill", cooldownFill);

				var bg = new StyleBoxFlat();
				bg.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
				cooldownBar.AddThemeStyleboxOverride("background", bg);
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

		private void TryUse(Character.Player player)
		{
			if (onCooldown) return;

			if (player.GetCurrency() > 0)
			{
				ShowResult("Only usable at $0!");
				return;
			}

			int loan = Economy.CurrencyEconomy.ATM_LOAN;
			player.AddAtmDebt(loan);

			ShowResult($"+${loan} ammo  (debt: -${player.GetAtmDebt()})");

			onCooldown        = true;
			cooldownRemaining = cooldownTime;
			if (cooldownBar  != null) cooldownBar.Value    = 100f;
			if (cooldownQuad != null) cooldownQuad.Visible = true;
			UpdatePrompt();
		}

		private void UpdateCooldownBar()
		{
			if (cooldownBar == null) return;
			cooldownBar.Value = (cooldownRemaining / cooldownTime) * 100f;
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
				: "[E] ATM  (only at $0)";
		}

		private void OnBodyEntered(Node3D body)
		{
			if (body is Character.Player p)
			{
				playerInRange = p;
				UpdatePrompt();
				if (promptLabel != null) promptLabel.Visible = true;
				if (onCooldown && cooldownQuad != null) cooldownQuad.Visible = true;
			}
		}

		private void OnBodyExited(Node3D body)
		{
			if (body is Character.Player p && p == playerInRange)
			{
				playerInRange = null;
				if (promptLabel  != null) promptLabel.Visible  = false;
				if (resultLabel  != null) resultLabel.Visible  = false;
				if (cooldownQuad != null) cooldownQuad.Visible = false;
			}
		}
	}
}
