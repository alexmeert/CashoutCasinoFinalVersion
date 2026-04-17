using Godot;
using System;

namespace CashoutCasino.Character
{
	/// <summary>
	/// Abstract animation helper for characters. Both Player and AI can compose this.
	/// Implement play hooks to drive AnimationTree/AnimationPlayer states.
	/// </summary>
	public partial class CharacterAnimator : Node
	{
		public enum State { Idle, Walk, Run, Crouch, Jump, Land, Shoot, Reload, TakeDamage, Death }

		protected State currentState = State.Idle;

		public virtual void SetState(State s)
		{
			currentState = s;
		}

		public virtual void PlayTakeDamage() { }
		public virtual void PlayDeath() { }
		public virtual void PlayShoot() { }
		public virtual void PlayReload() { }
	}
}
