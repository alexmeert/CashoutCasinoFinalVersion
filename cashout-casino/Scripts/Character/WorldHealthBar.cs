using Godot;

namespace CashoutCasino.UI
{
	public partial class WorldHealthBar : Node3D
	{
		[Export] public float hideAfterSeconds = 3f;

		private SubViewport viewport;
		private ProgressBar progressBar;
		private StyleBoxFlat fillStyle;
		private MeshInstance3D quad;

		private float hideTimer = 0f;
		private bool visible3d = false;
		private Camera3D localCamera;
		private bool _persistent = false;

		public override void _Ready()
		{
			viewport = GetNode<SubViewport>("SubViewport");
			progressBar = GetNode<ProgressBar>("SubViewport/ProgressBar");
			quad = GetNode<MeshInstance3D>("Quad");

			// Wire the SubViewport's rendered texture onto the quad
			var mat = new StandardMaterial3D();
			mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
			mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
			mat.NoDepthTest = true;
			mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			mat.AlbedoTexture = viewport.GetTexture();
			quad.MaterialOverride = mat;

			// Grab the fill StyleBox to shift its color with health
			fillStyle = progressBar.GetThemeStylebox("fill") as StyleBoxFlat;

			quad.Visible = false;
		}

		// Call once to keep the bar permanently visible (for remote players).
		public void SetPersistent(bool on)
		{
			_persistent = on;
			if (on) { visible3d = true; quad.Visible = true; }
		}

		public void ShowFor(float currentHealth, float maxHealth)
		{
			visible3d = true;
			if (!_persistent) hideTimer = hideAfterSeconds;
			quad.Visible = true;
			UpdateBar(currentHealth, maxHealth);
		}

		public void Reset()
		{
			visible3d = false;
			hideTimer = 0f;
			quad.Visible = false;
			if (progressBar != null)
				progressBar.Value = progressBar.MaxValue;
		}

		public void SetLocalCamera(Camera3D camera)
		{
			localCamera = camera;
		}

		private void UpdateBar(float current, float max)
		{
			progressBar.MaxValue = max;
			progressBar.Value = current;

			// Shift fill color green -> red as health drops
			if (fillStyle != null)
			{
				float ratio = Mathf.Clamp(current / max, 0f, 1f);
				float r = Mathf.Lerp(1f, 0.1f, ratio);
				float g = Mathf.Lerp(0.1f, 0.85f, ratio);
				fillStyle.BgColor = new Color(r, g, 0.1f, 1f);
			}
		}

		public override void _Process(double delta)
		{
			// Auto-find the active camera when not explicitly set (needed for persistent bars)
			if (localCamera == null || !IsInstanceValid(localCamera))
				localCamera = GetViewport()?.GetCamera3D();

			if (_persistent)
			{
				if (localCamera != null)
				{
					Vector3 dir = (localCamera.GlobalPosition - GlobalPosition).Normalized();
					if (dir.LengthSquared() > 0.001f)
						LookAt(localCamera.GlobalPosition, Vector3.Up);
				}
				return;
			}

			if (!visible3d) return;

			hideTimer -= (float)delta;
			if (hideTimer <= 0f)
			{
				visible3d = false;
				quad.Visible = false;
				return;
			}

			if (localCamera != null)
			{
				Vector3 dir = (localCamera.GlobalPosition - GlobalPosition).Normalized();
				if (dir.LengthSquared() > 0.001f)
					LookAt(localCamera.GlobalPosition, Vector3.Up);
			}
		}
	}
}
