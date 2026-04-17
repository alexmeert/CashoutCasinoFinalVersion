using Godot;

namespace CashoutCasino.UI
{
	public partial class PlayerHud : CanvasLayer
	{
		public static PlayerHud LocalInstance { get; private set; }
		public Character.Player OwnerPlayer { get; set; }

		[Export] public Texture2D RifleIcon;
		[Export] public Texture2D ShotgunIcon;
		[Export] public Texture2D PistolIcon;
		[Export] public Texture2D HeadshotIcon;

		private Label weaponNameLabel;
		private Label ammoLabel;
		private Label currencyLabel;
		private Label atmDebtLabel;
		private ProgressBar healthBar;
		private StyleBoxFlat healthFillStyle;
		private VBoxContainer killFeedContainer;
		private ColorRect damageOverlay;
		private Control healthPanel;
		private Control weaponPanel;
		private Control reticle;
		private RingDrawer _reloadRing;

		private static readonly Font KillFeedFont =
			GD.Load<Font>("res://Assets/upheaval/upheavtt.ttf");

		public Weapon.WeaponManager WeaponManager;

		public override void _Ready()
		{
			weaponNameLabel = GetNode<Label>("PlayerHUDPanel/VBox/WeaponName");
			ammoLabel       = GetNode<Label>("PlayerHUDPanel/VBox/Ammo");
			currencyLabel   = GetNode<Label>("PlayerHUDPanel/VBox/Currency");
			atmDebtLabel    = GetNodeOrNull<Label>("PlayerHUDPanel/VBox/AtmDebt");
			healthBar       = GetNodeOrNull<ProgressBar>("HealthPanel/VBox/HealthBar");
			killFeedContainer = GetNodeOrNull<VBoxContainer>("KillFeedContainer");
			damageOverlay     = GetNodeOrNull<ColorRect>("DamageOverlay");
			healthPanel       = GetNodeOrNull<Control>("HealthPanel");
			weaponPanel       = GetNodeOrNull<Control>("PlayerHUDPanel");
			reticle           = GetNodeOrNull<Control>("Reticle");

			if (healthBar != null)
			{
				healthFillStyle = new StyleBoxFlat();
				healthFillStyle.BgColor              = new Color(0.2f, 0.85f, 0.1f, 1f);
				healthFillStyle.CornerRadiusTopLeft   = 3;
				healthFillStyle.CornerRadiusTopRight  = 3;
				healthFillStyle.CornerRadiusBottomLeft  = 3;
				healthFillStyle.CornerRadiusBottomRight = 3;
				healthBar.AddThemeStyleboxOverride("fill", healthFillStyle);
			}

			if (atmDebtLabel != null)
				atmDebtLabel.Visible = false;

			// Small ring centered on the reticle, shown while reloading.
			_reloadRing = new RingDrawer();
			_reloadRing.AnchorLeft   = _reloadRing.AnchorRight  = 0.5f;
			_reloadRing.AnchorTop    = _reloadRing.AnchorBottom  = 0.5f;
			_reloadRing.OffsetLeft   = -28f;
			_reloadRing.OffsetRight  =  28f;
			_reloadRing.OffsetTop    = -28f;
			_reloadRing.OffsetBottom =  28f;
			_reloadRing.Thickness    = 5f;
			_reloadRing.ArcColor     = new Color(1f, 0.85f, 0.2f, 1f);
			_reloadRing.Visible      = false;
			AddChild(_reloadRing);
		}

		public void SetAsLocalInstance()
		{
			LocalInstance = this;
		}

		public override void _Process(double delta)
		{
			if (WeaponManager == null) return;

			weaponNameLabel.Text = WeaponManager.GetCurrentWeaponName();
			var w = WeaponManager.GetCurrentWeapon();
			if (w != null)
			{
				string reloadTag = w.IsReloading ? "  [RELOADING]" : "";
				ammoLabel.Text = $"{w.currentMag} / {w.magSize}{reloadTag}";
			}
		}

		public void OnCurrencyChanged(int newAmount)
		{
			currencyLabel.Text = $"${newAmount}";
		}

		public void OnAtmDebtChanged(int totalDebt)
		{
			if (atmDebtLabel == null) return;

			if (totalDebt <= 0)
			{
				atmDebtLabel.Visible = false;
				return;
			}

			atmDebtLabel.Text    = $"-${totalDebt} (ATM)";
			atmDebtLabel.Visible = true;
		}

		public void ShowDeathUI()
		{
			if (healthPanel != null) healthPanel.Visible = false;
			if (weaponPanel != null) weaponPanel.Visible = false;
			if (reticle    != null) reticle.Visible    = false;
		}

		public void ShowAliveUI()
		{
			if (healthPanel != null) healthPanel.Visible = true;
			if (weaponPanel != null) weaponPanel.Visible = true;
			if (reticle    != null) reticle.Visible    = true;
		}

		public void OnReloadProgress(float progress)
		{
			if (_reloadRing == null) return;
			_reloadRing.Progress = progress;
			_reloadRing.Visible  = true;
			_reloadRing.QueueRedraw();
		}

		public void OnReloadComplete()
		{
			if (_reloadRing != null) _reloadRing.Visible = false;
		}

		public void OnDamageTaken()
		{
			if (damageOverlay == null) return;
			damageOverlay.Color = new Color(0.8f, 0f, 0f, 0.45f);
			var tween = damageOverlay.CreateTween();
			tween.TweenProperty(damageOverlay, "color:a", 0.0, 0.4);
		}

		public void OnHealthChanged(float current, float max)
		{
			if (healthBar == null || healthFillStyle == null) return;

			healthBar.MaxValue = max;
			healthBar.Value    = current;

			float ratio = Mathf.Clamp(current / max, 0f, 1f);
			float r = Mathf.Lerp(1f, 0.1f, ratio);
			float g = Mathf.Lerp(0.1f, 0.85f, ratio);
			healthFillStyle.BgColor = new Color(r, g, 0.1f, 1f);
		}

		public void AddKillEntry(string killerName, string weaponKind, string victimName, int rewardAmount = 0, bool isHeadshot = false)
		{
			if (killFeedContainer == null) return;

			bool isOwnKill = killerName.Length > 0 && killerName == (OwnerPlayer?.GetDisplayName() ?? "");

			var container = new PanelContainer();
			container.Modulate = new Color(1, 1, 1, 1);

			var bg = new StyleBoxFlat();
			if (isOwnKill)
			{
				bg.BgColor = new Color(0.7f, 0.05f, 0.05f, 0.55f);
				bg.BorderColor = new Color(1f, 0.15f, 0.15f, 1f);
				bg.BorderWidthLeft = bg.BorderWidthRight = bg.BorderWidthTop = bg.BorderWidthBottom = 2;
			}
			else
			{
				bg.BgColor = new Color(0f, 0f, 0f, 0.45f);
				bg.BorderWidthLeft = bg.BorderWidthRight = bg.BorderWidthTop = bg.BorderWidthBottom = 0;
			}
			bg.CornerRadiusTopLeft = bg.CornerRadiusTopRight = bg.CornerRadiusBottomLeft = bg.CornerRadiusBottomRight = 4;
			bg.ContentMarginLeft = bg.ContentMarginRight = 8;
			bg.ContentMarginTop = bg.ContentMarginBottom = 4;
			container.AddThemeStyleboxOverride("panel", bg);

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);
			row.Alignment = HBoxContainer.AlignmentMode.Center;
			row.AddChild(MakeNameLabel(killerName, new Color(1f, 1f, 1f, 1f)));
			row.AddChild(MakeWeaponWidget(weaponKind));
			if (isHeadshot)
				row.AddChild(MakeWeaponWidget("Headshot"));
			row.AddChild(MakeNameLabel(victimName, new Color(1f, 0.25f, 0.25f, 1f)));
			container.AddChild(row);

			killFeedContainer.AddChild(container);

			if (isOwnKill && rewardAmount > 0)
				ShowKillRewardPopup(rewardAmount);

			var tween = container.CreateTween();
			tween.TweenInterval(4.0);
			tween.TweenProperty(container, "modulate:a", 0.0, 0.6);
			tween.TweenCallback(Callable.From(container.QueueFree));
		}

		public void ShowKillRewardPopup(int amount)
		{
			var lbl = new Label();
			lbl.Text = $"+${amount}  KILL";
			var settings = new LabelSettings();
			settings.FontSize = 32;
			settings.FontColor = new Color(0.25f, 1f, 0.35f, 1f);
			settings.OutlineSize = 3;
			settings.OutlineColor = new Color(0f, 0f, 0f, 0.8f);
			if (KillFeedFont != null) settings.Font = KillFeedFont;
			lbl.LabelSettings = settings;
			lbl.HorizontalAlignment = HorizontalAlignment.Center;
			lbl.AnchorLeft = lbl.AnchorRight = 0.5f;
			lbl.AnchorTop = lbl.AnchorBottom = 0.5f;
			lbl.GrowHorizontal = Control.GrowDirection.Both;
			lbl.GrowVertical = Control.GrowDirection.Both;
			lbl.OffsetLeft = -150f;
			lbl.OffsetRight = 150f;
			lbl.OffsetTop = 60f;
			lbl.OffsetBottom = 100f;
			AddChild(lbl);

			var tween = lbl.CreateTween();
			tween.SetParallel(true);
			tween.TweenProperty(lbl, "offset_top", -40f, 0.8).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(lbl, "offset_bottom", 0f, 0.8).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(lbl, "modulate:a", 0.0, 0.8).SetDelay(0.4);
			tween.SetParallel(false);
			tween.TweenCallback(Callable.From(lbl.QueueFree));
		}

		private Label MakeNameLabel(string text, Color color)
		{
			var lbl = new Label();
			lbl.Text = text.Length > 0 ? text : "Unknown";
			var settings = new LabelSettings();
			settings.FontSize = 18;
			settings.FontColor = color;
			if (KillFeedFont != null) settings.Font = KillFeedFont;
			lbl.LabelSettings = settings;
			lbl.VerticalAlignment = VerticalAlignment.Center;
			return lbl;
		}

		private Control MakeWeaponWidget(string weaponKind)
		{
			Texture2D icon = weaponKind switch
			{
				"Rifle"    => RifleIcon,
				"Shotgun"  => ShotgunIcon,
				"Pistol"   => PistolIcon,
				"Headshot" => HeadshotIcon,
				_          => null,
			};

			if (icon != null)
			{
				var tex = new TextureRect();
				tex.Texture = icon;
				tex.CustomMinimumSize = new Vector2(32, 32);
				tex.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
				tex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
				return tex;
			}

			string tag = weaponKind switch
			{
				"Rifle"   => "[AR]",
				"Shotgun" => "[SG]",
				"Pistol"  => "[DP]",
				_         => "[?]",
			};
			return MakeNameLabel(tag, new Color(1f, 0.85f, 0.3f, 1f));
		}
	}
}
