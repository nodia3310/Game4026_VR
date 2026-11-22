using System;
using System.Reflection;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Temporary adapter: the shared contract has no "player health / died" API yet, so the demo flow reads a float
    /// <c>CurrentHealth</c> property (and <c>MaxHealth</c> if there is one) and restores health with a public
    /// <c>ResetHealth()</c> — or, when there is none (A's PlayerHealth), by setting <c>CurrentHealth</c> back to
    /// <c>MaxHealth</c> through its non-public setter. When the receiver itself has no health (A's desktop adapter forwards
    /// damage from its camera to the XR rig's PlayerHealth), the receiver it holds (property or field) is used instead.
    /// Replace with a contract once one is agreed with A.
    /// </summary>
    internal sealed class PlayerVitals
    {
        private readonly Func<float> health;
        private readonly Func<float> maxHealth;
        private readonly Action reset;

        private PlayerVitals(Func<float> health, Func<float> maxHealth, Action reset)
        {
            this.health = health;
            this.maxHealth = maxHealth;
            this.reset = reset;
        }

        public bool CanReadHealth => health != null;
        public bool CanReset => reset != null;
        public bool IsDead => health != null && health() <= 0f;

        public void Reset() => reset?.Invoke();

        public bool TryGetHealth(out float current, out float max)
        {
            current = health != null ? health() : 0f;
            max = maxHealth != null ? maxHealth() : 0f;
            return health != null;
        }

        public static PlayerVitals From(IPlayerDamageReceiver receiver)
        {
            if (receiver == null)
                return new PlayerVitals(null, null, null);
            PlayerVitals vitals = Bind(receiver);
            if (vitals.CanReadHealth)
                return vitals;
            // The receiver only forwards damage (A's desktop adapter): use the receiver it forwards to.
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type type = receiver.GetType();
            foreach (PropertyInfo property in type.GetProperties(any))
            {
                MethodInfo getter = property.GetGetMethod(true);
                if (getter == null || getter.GetParameters().Length != 0 || !typeof(IPlayerDamageReceiver).IsAssignableFrom(property.PropertyType))
                    continue;
                if (getter.Invoke(receiver, null) is IPlayerDamageReceiver inner && !ReferenceEquals(inner, receiver) && !IsDestroyed(inner))
                {
                    PlayerVitals candidate = Bind(inner);
                    if (candidate.CanReadHealth)
                        return candidate;
                }
            }
            foreach (FieldInfo field in type.GetFields(any))
            {
                if (!typeof(IPlayerDamageReceiver).IsAssignableFrom(field.FieldType))
                    continue;
                if (field.GetValue(receiver) is IPlayerDamageReceiver inner && !ReferenceEquals(inner, receiver) && !IsDestroyed(inner))
                {
                    PlayerVitals candidate = Bind(inner);
                    if (candidate.CanReadHealth)
                        return candidate;
                }
            }
            return vitals;
        }

        private static bool IsDestroyed(IPlayerDamageReceiver receiver) => receiver is UnityEngine.Object o && o == null;

        private static PlayerVitals Bind(object receiver)
        {
            Type type = receiver.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            Func<float> health = null, max = null;
            PropertyInfo current = type.GetProperty("CurrentHealth", flags);
            if (current != null && current.PropertyType == typeof(float) && current.GetGetMethod() != null)
                health = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), receiver, current.GetGetMethod());
            PropertyInfo maxProperty = type.GetProperty("MaxHealth", flags);
            if (maxProperty != null && maxProperty.PropertyType == typeof(float) && maxProperty.GetGetMethod() != null)
                max = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), receiver, maxProperty.GetGetMethod());

            Action reset = null;
            MethodInfo method = type.GetMethod("ResetHealth", flags, null, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(void))
                reset = (Action)Delegate.CreateDelegate(typeof(Action), receiver, method);
            else if (max != null && current != null && current.GetSetMethod(true) != null)
            {
                MethodInfo setter = current.GetSetMethod(true);
                Func<float> maxValue = max;
                reset = () => setter.Invoke(receiver, new object[] { maxValue() });
            }
            return new PlayerVitals(health, max, reset);
        }
    }
}
