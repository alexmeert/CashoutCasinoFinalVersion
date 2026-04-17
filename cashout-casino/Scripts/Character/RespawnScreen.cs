using Godot;
using System;

namespace CashoutCasino.UI
{
	public partial class RespawnScreen : CanvasLayer
	{
		private RingDrawer ring;
		private Label countdownLabel;
		private Label deadLabel;

		private float totalTime;
		private float elapsed;
		private bool counting = false;
		private Action onComplete;

		public override void _Ready()
		{
			ring           = GetNode<RingDrawer>("Ring");
			countdownLabel = GetNode<Label>("CountdownLabel");
			deadLabel      = GetNodeOrNull<Label>("DeadLabel");
			Visible = false;
		}

		public void StartCountdown(float seconds, Action callback, string killerName = "")
		{
			totalTime = seconds;
			elapsed = 0f;
			onComplete = callback;
			counting = true;

			Visible = true;
			countdownLabel.Text = Mathf.CeilToInt(totalTime).ToString();
			if (deadLabel != null)
				deadLabel.Text = $"ELIMINATED BY:\n{(killerName.Length > 0 ? killerName : "Unknown")}";
			ring.Progress = 1f;
			ring.ArcColor = new Color(0.9f, 0.2f, 0.1f, 1f);
			ring.QueueRedraw();
		}

		public override void _Process(double delta)
		{
			if (!counting) return;

			elapsed += (float)delta;
			float remaining = Mathf.Max(totalTime - elapsed, 0f);
			float ratio = remaining / totalTime;

			countdownLabel.Text = Mathf.CeilToInt(remaining).ToString();

			ring.Progress = ratio;
			ring.ArcColor = new Color(
				Mathf.Lerp(0.1f, 0.9f, ratio),
				Mathf.Lerp(0.9f, 0.2f, ratio),
				0.1f, 1f);
			ring.QueueRedraw();

			if (elapsed >= totalTime)
			{
				counting = false;
				Visible = false;
				onComplete?.Invoke();
			}
		}
	}
}
