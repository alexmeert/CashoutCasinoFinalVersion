using Godot;

namespace CashoutCasino.UI
{
	/// <summary>
	/// Reusable round countdown timer. Drop into any scene and call StartCountdown(seconds).
	/// Emits TimerCompleted when it hits zero, TimerTick every second.
	/// </summary>
	public partial class CountdownTimer : CanvasLayer
	{
		[Export] public float defaultDuration = 180f;
		[Export] public bool autoStart = false;

		[Signal] public delegate void TimerCompletedEventHandler();
		[Signal] public delegate void TimerTickEventHandler(int secondsRemaining);

		private Label timeLabel;
		private Label titleLabel;

		private float timeRemaining;
		private bool running = false;

		public override void _Ready()
		{
			timeLabel = GetNode<Label>("Panel/VBox/TimeLabel");
			titleLabel = GetNodeOrNull<Label>("Panel/VBox/TitleLabel");

			timeRemaining = defaultDuration;
			UpdateDisplay();

			if (autoStart)
				StartCountdown(defaultDuration);
		}

		public void StartCountdown(float seconds)
		{
			timeRemaining = seconds;
			running = true;
			UpdateDisplay();
		}

		public void Pause() => running = false;
		public void Resume() => running = true;

		public void Reset()
		{
			running = false;
			timeRemaining = defaultDuration;
			UpdateDisplay();
		}

		public void SetTitle(string title)
		{
			if (titleLabel != null)
				titleLabel.Text = title;
		}

		public float GetTimeRemaining() => timeRemaining;
		public bool IsRunning() => running;

		public override void _Process(double delta)
		{
			if (!running) return;

			float prevRemaining = timeRemaining;
			timeRemaining -= (float)delta;

			// Fire tick signal once per whole second crossed
			if (Mathf.CeilToInt(timeRemaining) < Mathf.CeilToInt(prevRemaining))
				EmitSignal(SignalName.TimerTick, Mathf.CeilToInt(timeRemaining));

			if (timeRemaining <= 0f)
			{
				timeRemaining = 0f;
				running = false;
				UpdateDisplay();
				EmitSignal(SignalName.TimerCompleted);
				return;
			}

			UpdateDisplay();
		}

		private void UpdateDisplay()
		{
			int total = Mathf.CeilToInt(timeRemaining);
			int minutes = total / 60;
			int seconds = total % 60;
			timeLabel.Text = $"{minutes}:{seconds:D2}";

			// Turn red in the last 30 seconds
			if (timeRemaining <= 30f && timeRemaining > 0f)
			{
				float ratio = timeRemaining / 30f;
				timeLabel.AddThemeColorOverride("font_color",
					new Color(1f, Mathf.Lerp(0.1f, 1f, ratio), 0.1f, 1f));
			}
			else
			{
				timeLabel.RemoveThemeColorOverride("font_color");
			}
		}
	}
}
