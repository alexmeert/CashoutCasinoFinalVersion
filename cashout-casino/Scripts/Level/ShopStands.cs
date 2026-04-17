using Godot;

namespace CashoutCasino
{
	/// <summary>
	/// Shop stand interactable. Press E to open the shop menu.
	/// Items: Shoes (speed), Voucher (damage), Coat (health).
	/// </summary>
	public partial class ShopStands : Node3D
	{
		[Export] public int ShoesPrice  = 30;
		[Export] public int VoucherPrice = 25;
		[Export] public int CoatPrice   = 40;

		[Export] public float SpeedBonus  = 2f;
		[Export] public float DamageBonus = 5f;
		[Export] public float HealthBonus = 25f;

		private Label3D _promptLabel;
		private Character.Player _playerInRange;
		private CanvasLayer _shopUI;
		private bool _menuOpen = false;

		public override void _Ready()
		{
			// Build prompt label
			_promptLabel = new Label3D();
			_promptLabel.Text = "[E] Shop";
			_promptLabel.FontSize = 48;
			_promptLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
			_promptLabel.Position = new Vector3(0, 2.2f, 0);
			_promptLabel.Visible = false;
			AddChild(_promptLabel);

			// Build interact area
			var area = new Area3D();
			var shape = new CollisionShape3D();
			var sphere = new SphereShape3D();
			sphere.Radius = 2.5f;
			shape.Shape = sphere;
			area.AddChild(shape);
			area.BodyEntered += OnBodyEntered;
			area.BodyExited  += OnBodyExited;
			AddChild(area);

			// Build the shop UI
			_shopUI = BuildShopUI();
			AddChild(_shopUI);
			_shopUI.Visible = false;
		}

		public override void _Process(double delta)
		{
			if (_menuOpen && (_playerInRange == null || _playerInRange.IsDead))
			{
				CloseShop();
				return;
			}

			if (_playerInRange == null) return;

			if (Input.IsActionJustPressed("interact"))
			{
				if (_menuOpen)
					CloseShop();
				else
					OpenShop();
			}

			if (_menuOpen && Input.IsKeyPressed(Key.Escape))
				CloseShop();
		}

		private void OpenShop()
		{
			_menuOpen = true;
			_shopUI.Visible = true;
			if (_promptLabel != null) _promptLabel.Visible = false;
			Input.MouseMode = Input.MouseModeEnum.Visible;
			if (_playerInRange != null) { _playerInRange.SetPhysicsProcess(false); _playerInRange.SetProcessInput(false); }
		}

		private void CloseShop()
		{
			_menuOpen = false;
			_shopUI.Visible = false;
			if (_promptLabel != null) _promptLabel.Visible = true;
			Input.MouseMode = Input.MouseModeEnum.Captured;
			if (_playerInRange != null && !_playerInRange.IsDead)
			{
				_playerInRange.SetPhysicsProcess(true);
				_playerInRange.SetProcessInput(true);
			}
		}

		private void TryBuy(int item)
		{
			if (_playerInRange == null) return;

			int price = item switch { 0 => ShoesPrice, 1 => VoucherPrice, _ => CoatPrice };
			if (_playerInRange.GetCurrency() < price)
			{
				ShowFeedback("Not enough money!");
				return;
			}

			_playerInRange.ModifyCurrency(-price);
			_playerInRange.RpcId(1, "ServerApplyUpgrade", item,
				SpeedBonus, DamageBonus, HealthBonus);

			string msg = item switch
			{
				0 => $"Shoes: +{SpeedBonus} speed",
				1 => $"Voucher: +{DamageBonus} damage",
				_ => $"Coat: +{(int)HealthBonus} HP"
			};
			ShowFeedback(msg);
			CloseShop();
		}

		private Label _feedbackLabel;
		private void ShowFeedback(string text)
		{
			if (_feedbackLabel == null) return;
			_feedbackLabel.Text = text;
			_feedbackLabel.Visible = true;
			GetTree().CreateTimer(2.5f).Timeout += () =>
			{
				if (IsInstanceValid(this) && _feedbackLabel != null)
					_feedbackLabel.Visible = false;
			};
		}

		private void OnBodyEntered(Node3D body)
		{
			GD.Print($"[Shop] BodyEntered: {body.Name} type={body.GetType().Name}");
			if (body is Character.Player p)
			{
				_playerInRange = p;
				if (_promptLabel != null)
				{
					_promptLabel.Text = "[E] Shop";
					_promptLabel.Visible = true;
				}
			}
		}

		private void OnBodyExited(Node3D body)
		{
			if (body is Character.Player p && p == _playerInRange)
			{
				_playerInRange = null;
				if (_promptLabel != null) _promptLabel.Visible = false;
				if (_menuOpen) CloseShop();
			}
		}

		private CanvasLayer BuildShopUI()
		{
			var font = GD.Load<FontFile>("res://Assets/upheaval/upheavtt.ttf");
			var layer = new CanvasLayer();
			layer.Layer = 25;

			// Dark background panel
			var bg = new StyleBoxFlat();
			bg.BgColor = new Color(0.04f, 0.03f, 0.07f, 0.94f);
			bg.BorderWidthLeft = bg.BorderWidthTop = bg.BorderWidthRight = bg.BorderWidthBottom = 2;
			bg.BorderColor = new Color(0.7f, 0.6f, 0.2f, 1f);
			bg.CornerRadiusTopLeft = bg.CornerRadiusTopRight =
			bg.CornerRadiusBottomLeft = bg.CornerRadiusBottomRight = 10;

			var panel = new Panel();
			panel.AnchorsPreset = (int)Control.LayoutPreset.Center;
			panel.SetAnchor(Side.Left, 0.5f); panel.SetAnchor(Side.Right, 0.5f);
			panel.SetAnchor(Side.Top, 0.5f);  panel.SetAnchor(Side.Bottom, 0.5f);
			panel.OffsetLeft = -220f; panel.OffsetRight = 220f;
			panel.OffsetTop  = -190f; panel.OffsetBottom = 190f;
			panel.AddThemeStyleboxOverride("panel", bg);
			layer.AddChild(panel);

			var vbox = new VBoxContainer();
			vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			vbox.AddThemeConstantOverride("separation", 10);
			var margin = new MarginContainer();
			margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			margin.AddThemeConstantOverride("margin_left", 18);
			margin.AddThemeConstantOverride("margin_right", 18);
			margin.AddThemeConstantOverride("margin_top", 14);
			margin.AddThemeConstantOverride("margin_bottom", 14);
			panel.AddChild(margin);
			margin.AddChild(vbox);

			// Title
			var title = new Label();
			title.Text = "SHOP";
			title.HorizontalAlignment = HorizontalAlignment.Center;
			title.AddThemeFontSizeOverride("font_size", 24);
			if (font != null) title.AddThemeFontOverride("font", font);
			title.AddThemeColorOverride("font_color", new Color(1f, 0.88f, 0.2f, 1f));
			vbox.AddChild(title);

			vbox.AddChild(new HSeparator());

			// Items
			AddShopItem(vbox, "Shoes",   $"Speed +{SpeedBonus}",   $"${ShoesPrice}",  0, font);
			AddShopItem(vbox, "Voucher", $"Damage +{DamageBonus}", $"${VoucherPrice}", 1, font);
			AddShopItem(vbox, "Coat",    $"Health +{(int)HealthBonus}", $"${CoatPrice}", 2, font);

			vbox.AddChild(new HSeparator());

			// Feedback label
			_feedbackLabel = new Label();
			_feedbackLabel.HorizontalAlignment = HorizontalAlignment.Center;
			_feedbackLabel.AddThemeFontSizeOverride("font_size", 14);
			if (font != null) _feedbackLabel.AddThemeFontOverride("font", font);
			_feedbackLabel.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f, 1f));
			_feedbackLabel.Visible = false;
			vbox.AddChild(_feedbackLabel);

			// Close hint
			var hint = new Label();
			hint.Text = "E / ESC to close";
			hint.HorizontalAlignment = HorizontalAlignment.Center;
			hint.AddThemeFontSizeOverride("font_size", 12);
			if (font != null) hint.AddThemeFontOverride("font", font);
			hint.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 1f));
			vbox.AddChild(hint);

			return layer;
		}

		private void AddShopItem(VBoxContainer parent, string name, string desc, string price, int itemId, FontFile font = null)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);

			var nameLabel = new Label();
			nameLabel.Text = name;
			nameLabel.CustomMinimumSize = new Vector2(140, 0);
			nameLabel.AddThemeFontSizeOverride("font_size", 16);
			if (font != null) nameLabel.AddThemeFontOverride("font", font);
			nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f, 1f));
			row.AddChild(nameLabel);

			var descLabel = new Label();
			descLabel.Text = desc;
			descLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			descLabel.AddThemeFontSizeOverride("font_size", 13);
			if (font != null) descLabel.AddThemeFontOverride("font", font);
			descLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.9f, 0.5f, 1f));
			row.AddChild(descLabel);

			var btnStyle = new StyleBoxFlat();
			btnStyle.BgColor = new Color(0.2f, 0.15f, 0.05f, 1f);
			btnStyle.BorderWidthLeft = btnStyle.BorderWidthTop =
			btnStyle.BorderWidthRight = btnStyle.BorderWidthBottom = 1;
			btnStyle.BorderColor = new Color(0.7f, 0.6f, 0.2f, 1f);
			btnStyle.CornerRadiusTopLeft = btnStyle.CornerRadiusTopRight =
			btnStyle.CornerRadiusBottomLeft = btnStyle.CornerRadiusBottomRight = 4;

			var btn = new Button();
			btn.Text = price;
			btn.CustomMinimumSize = new Vector2(70, 0);
			btn.AddThemeStyleboxOverride("normal", btnStyle);
			btn.AddThemeFontSizeOverride("font_size", 15);
			if (font != null) btn.AddThemeFontOverride("font", font);
			int captured = itemId;
			btn.Pressed += () => TryBuy(captured);
			row.AddChild(btn);

			parent.AddChild(row);
		}
	}
}
