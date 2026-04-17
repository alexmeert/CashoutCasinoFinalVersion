using Godot;

namespace CashoutCasino.Character
{
	public abstract partial class Character : CharacterBody3D
	{
		[Export] public float maxHealth = 100f;
		[Export] public float moveSpeed = 7f;
		[Export] public float sprintMultiplier = 1.5f;
		[Export] public float crouchMultiplier = 0.6f;

		protected float currentHealth;
		protected int currentCurrency;
		protected CharacterAnimator animator;
		protected Weapon.WeaponManager weaponManager;

		protected Vector3 moveDirection = Vector3.Zero;
		protected bool isSprintingInput = false;
		protected bool isCrouching = false;
		protected bool isDead = false;

		public bool IsDead => isDead;

		public UI.WorldHealthBar WorldHealthBar;

		[Signal] public delegate void CurrencyChangedEventHandler(int newAmount);
		[Signal] public delegate void DiedEventHandler(Character killer);
		[Signal] public delegate void HealthChangedEventHandler(float current, float max);

		public override void _Ready()
		{
			currentHealth = maxHealth;
		}

		public abstract void OnInputAction(string action);
		public abstract void RequestAIDecision();

		public virtual void TakeDamage(float damage, Character attacker = null, bool isHeadshot = false)
		{
			if (isDead) return;

			currentHealth -= damage;
			currentHealth = Mathf.Max(currentHealth, 0f);
			animator?.PlayTakeDamage();

			OnHealthChangedInternal(currentHealth, maxHealth);
			EmitSignal(SignalName.HealthChanged, currentHealth, maxHealth);

			if (currentHealth <= 0)
				OnDeath(attacker);
		}

		protected virtual void OnHealthChangedInternal(float current, float max) { }

		public virtual void OnDeath(Character killer)
		{
			isDead = true;
			animator?.PlayDeath();
			EmitSignal(SignalName.Died, killer);
		}

		public virtual void RequestMovement(Vector3 direction, bool isSprinting)
		{
			moveDirection = direction;
			isSprintingInput = isSprinting;
		}

		public virtual void ModifyCurrency(int amount)
		{
			currentCurrency += amount;
			EmitSignal(SignalName.CurrencyChanged, currentCurrency);
		}

		public virtual bool CanAffordAction(Economy.CurrencyEconomy.CostType costType)
		{
			return Economy.CurrencyEconomy.CanAffordAction(this, costType);
		}

		public int GetCurrency() => currentCurrency;
		public float GetHealth() => currentHealth;
		public float GetMaxHealth() => maxHealth;
		public virtual string GetDisplayName() => Name;
		public Weapon.WeaponKind GetCurrentWeaponKind() => weaponManager?.GetCurrentWeaponKind() ?? Weapon.WeaponKind.Other;

		public override void _PhysicsProcess(double delta)
		{
			Vector3 velocity = Velocity;
			Vector3 desired = moveDirection * moveSpeed
				* (isSprintingInput ? sprintMultiplier : 1f)
				* (isCrouching ? crouchMultiplier : 1f);
			velocity.X = desired.X;
			velocity.Z = desired.Z;
			Velocity = velocity;
			base._PhysicsProcess(delta);
		}
	}
}
