using Godot;
using System;

namespace CashoutCasino.Economy
{
	public static class CurrencyEconomy
	{
		public enum ElimType { Body, Head, Grenade, Bounty }
		public enum CostType { Reroll, Heal, ShootAR, ShootShotgun, ShootPistol, Grenade, Other }

		public const int INITIAL_SPAWN      = 100;
		public const int BODY_ELIM          = 10;
		public const int HEAD_ELIM          = 20;
		public const int GRENADE_ELIM       = 25;
		public const int REROLL_COST        = 15;
		public const int HEAL_COST          = 20;
		public const int AR_SHOOT_COST      = 1;
		public const int SHOTGUN_SHOOT_COST = 5;
		public const int PISTOL_SHOOT_COST  = 1;
		public const int DEATH_PENALTY      = 30;
		public const int BOUNTY_ELIM        = 40;

		// ATM: gives this much ammo/currency but marks the same as a score penalty
		public const int ATM_LOAN           = 50;

		public static void ApplyCurrencyGain(CashoutCasino.Character.Character player, ElimType type)
		{
			switch (type)
			{
				case ElimType.Body:    player.ModifyCurrency(BODY_ELIM);    break;
				case ElimType.Head:    player.ModifyCurrency(HEAD_ELIM);    break;
				case ElimType.Grenade: player.ModifyCurrency(GRENADE_ELIM); break;
				case ElimType.Bounty:  player.ModifyCurrency(BOUNTY_ELIM);  break;
			}
		}

		public static void ApplyCurrencyCost(CashoutCasino.Character.Character player, CostType costType)
		{
			switch (costType)
			{
				case CostType.Reroll:       player.ModifyCurrency(-REROLL_COST);        break;
				case CostType.Heal:         player.ModifyCurrency(-HEAL_COST);           break;
				case CostType.ShootAR:      player.ModifyCurrency(-AR_SHOOT_COST);       break;
				case CostType.ShootShotgun: player.ModifyCurrency(-SHOTGUN_SHOOT_COST);  break;
				case CostType.ShootPistol:  player.ModifyCurrency(-PISTOL_SHOOT_COST);   break;
				case CostType.Grenade:      player.ModifyCurrency(-REROLL_COST);         break;
				default: break;
			}
		}

		public static bool CanAffordAction(CashoutCasino.Character.Character player, CostType costType)
		{
			int cost = 0;
			switch (costType)
			{
				case CostType.Reroll:       cost = REROLL_COST;        break;
				case CostType.Heal:         cost = HEAL_COST;           break;
				case CostType.ShootAR:      cost = AR_SHOOT_COST;       break;
				case CostType.ShootShotgun: cost = SHOTGUN_SHOOT_COST;  break;
				case CostType.ShootPistol:  cost = PISTOL_SHOOT_COST;   break;
				case CostType.Grenade:      cost = REROLL_COST;         break;
			}
			return player.GetCurrency() >= cost;
		}
	}
}
