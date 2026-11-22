using System;
using System.Reflection;
using Game4026.Contracts;

namespace Game4026.Enemy
{
    /// <summary>
    /// Temporary adapter: the shared contract has no "player health / died" API yet, so the demo flow reads a public
    /// float <c>CurrentHealth</c> property and calls a public <c>ResetHealth()</c> method by name when the receiver has them
    /// (MockPlayer has both; A's PlayerHealth has CurrentHealth). Replace with a contract once one is agreed with A.
    /// </summary>
    internal sealed class PlayerVitals
    {
        private readonly Func<float> health;
        private readonly Action reset;

        private PlayerVitals(Func<float> health, Action reset)
        {
            this.health = health;
            this.reset = reset;
        }

        public bool CanReadHealth => health != null;
        public bool CanReset => reset != null;
        public bool IsDead => health != null && health() <= 0f;

        public void Reset() => reset?.Invoke();

        public static PlayerVitals From(IPlayerDamageReceiver receiver)
        {
            if (receiver == null)
                return new PlayerVitals(null, null);

            Type type = receiver.GetType();
            Func<float> health = null;
            PropertyInfo property = type.GetProperty("CurrentHealth", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(float) && property.GetGetMethod() != null)
                health = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), receiver, property.GetGetMethod());

            Action reset = null;
            MethodInfo method = type.GetMethod("ResetHealth", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(void))
                reset = (Action)Delegate.CreateDelegate(typeof(Action), receiver, method);

            return new PlayerVitals(health, reset);
        }
    }
}
