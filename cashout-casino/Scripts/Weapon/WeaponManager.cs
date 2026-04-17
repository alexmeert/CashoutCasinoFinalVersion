using Godot;
using System.Collections.Generic;
using CashoutCasino.Economy;

namespace CashoutCasino.Weapon
{
	public partial class WeaponManager : Node3D
	{
		protected List<Weapon> weapons = new();
		public int currentWeaponIndex;
		public int grenadeCount;

		public Camera3D PlayerCamera;

		public override void _Ready()
		{
			foreach (Node child in GetChildren())
			{
				if (child is Weapon w)
				{
					weapons.Add(w);
					w.Visible = false;
				}
			}
		}

		public void Setup()
		{
			foreach (var w in weapons)
			{
				w.Reparent(this, true);
				w.Visible = false;
			}

			if (weapons.Count > 0)
				EquipWeapon(0);
		}

		public Weapon GetCurrentWeapon()
		{
			if (weapons.Count == 0)
				return null;

			return weapons[Mathf.Clamp(currentWeaponIndex, 0, weapons.Count - 1)];
		}

		public bool IsReloading      => GetCurrentWeapon()?.IsReloading ?? false;
		public bool CanInterruptCurrentReload => GetCurrentWeapon()?.CanInterruptReload ?? false;
		public int  GetCurrentMag()     => GetCurrentWeapon()?.currentMag ?? 0;
		public int  GetCurrentMagSize() => GetCurrentWeapon()?.magSize    ?? 0;

		// Returns true if the weapon accepted the reload request.
		public bool StartReload() => GetCurrentWeapon()?.StartReload() ?? false;

		// Returns true if more steps remain (shotgun shell-by-shell).
		public bool CompleteReloadStep() => GetCurrentWeapon()?.FinishReloadStep() ?? false;

		public void CancelReload() => GetCurrentWeapon()?.CancelReload();

		public System.Collections.Generic.IReadOnlyList<Weapon> GetWeapons() => weapons;

		public void SwitchWeapon(int slotIndex)
		{
			if (slotIndex < 0 || slotIndex >= weapons.Count)
				return;

			var current = GetCurrentWeapon();
			if (current != null)
			{
				current.CancelReload();
				current.Visible = false;
			}

			EquipWeapon(slotIndex);
		}

		private void EquipWeapon(int index)
		{
			currentWeaponIndex = index;

			var w = weapons[index];
			w.Visible = true;
			w.FireCamera = PlayerCamera;
		}

		public bool CurrentWeaponHoldToFire()
		{
			return GetCurrentWeapon()?.HoldToFire ?? false;
		}

		public bool FireCurrentWeapon(Vector3 direction, CashoutCasino.Character.Character owner)
		{
			var w = GetCurrentWeapon();
			if (w == null)
				return false;

			CurrencyEconomy.CostType costType = w.FireCostType;
			if (costType == CurrencyEconomy.CostType.Other)
				return false;

			if (!w.CanFire() || !owner.CanAffordAction(costType))
				return false;

			if (!w.Fire(direction, owner))
				return false;

			CurrencyEconomy.ApplyCurrencyCost(owner, costType);
			SyncAmmoToAllWeapons(owner.GetCurrency());
			return true;
		}

		public void SyncAmmoToAllWeapons(int currency)
		{
			foreach (var w in weapons)
				w.SyncAmmoDisplay(currency);
		}

		public void ReloadCurrentWeapon()
		{
		}

		public void ModifyGrenadeCount(int delta)
		{
			grenadeCount += delta;
		}

		public int GetCurrentAmmo()
		{
			return GetCurrentWeapon()?.GetAmmoCount() ?? 0;
		}

		public string GetCurrentWeaponName() => GetCurrentWeapon()?.DisplayName ?? "None";

		public WeaponKind GetCurrentWeaponKind() => GetCurrentWeapon()?.Kind ?? WeaponKind.Other;
	}
}
