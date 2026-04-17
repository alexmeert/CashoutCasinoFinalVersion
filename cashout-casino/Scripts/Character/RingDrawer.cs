using Godot;

namespace CashoutCasino.UI
{
	/// <summary>
	/// Draws a circular arc progress ring using _Draw — no textures needed.
	/// </summary>
	public partial class RingDrawer : Control
	{
		public float Progress  = 1f;
		public Color ArcColor  = new Color(0.9f, 0.2f, 0.1f, 1f);
		public float Thickness = 12f;

		private const int POINTS = 64;

		public override void _Draw()
		{
			Vector2 center = Size / 2f;
			float radius = Mathf.Min(Size.X, Size.Y) / 2f - Thickness;

			// Dark background ring
			DrawArc(center, radius, 0f, Mathf.Tau, POINTS,
				new Color(0.15f, 0.15f, 0.15f, 0.7f), Thickness, true);

			// Colored progress arc — starts from top, sweeps clockwise
			float sweepAngle = Progress * Mathf.Tau;
			if (sweepAngle > 0.01f)
			{
				DrawArc(center, radius,
					-Mathf.Pi / 2f,
					-Mathf.Pi / 2f + sweepAngle,
					POINTS, ArcColor, Thickness, true);
			}
		}
	}
}
