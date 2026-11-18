using System;
using Game4026.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game4026.Enemy.DebugTools
{
    /// <summary>
    /// Desktop stand-in for the VR player (spec 4.8). WASD to move, hold right mouse to look.
    /// Receives enemy attacks through <see cref="IPlayerDamageReceiver"/> and shows remaining health.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(PlayerTarget))]
    public sealed class MockPlayer : MonoBehaviour, IPlayerDamageReceiver
    {
        [Tooltip("Camera / head transform used for looking up and down.")]
        [SerializeField] private Transform head;
        [SerializeField, Min(0f)] private float moveSpeed = 3f;
        [SerializeField, Min(0f)] private float lookSensitivity = 0.15f;
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool showHud = true;

        private CharacterController controller;
        private float pitch;
        private string hudText;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public int HitsTaken { get; private set; }

        /// <summary>Damage amount of each enemy hit.</summary>
        public event Action<float> Damaged;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            ResetHealth();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                float z = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
                Vector3 move = transform.right * x + transform.forward * z;
                if (move.sqrMagnitude > 1f)
                    move.Normalize();
                controller.SimpleMove(move * moveSpeed);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
                transform.Rotate(0f, delta.x, 0f, Space.World);
                if (head != null)
                {
                    pitch = Mathf.Clamp(pitch - delta.y, -80f, 80f);
                    head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                }
            }
        }

        public void ApplyPlayerDamage(float damage)
        {
            if (!(damage > 0f) || float.IsInfinity(damage))
                return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            HitsTaken++;
            UpdateHud();
            Debug.Log($"[MockPlayer] took {damage:0.#} damage, health {CurrentHealth:0.#}/{maxHealth:0.#}", this);
            Damaged?.Invoke(damage);
        }

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            HitsTaken = 0;
            UpdateHud();
        }

        /// <summary>Moves the player instantly (CharacterController ignores plain transform writes).</summary>
        public void Teleport(Vector3 position)
        {
            if (controller == null)
                controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
        }

        private void UpdateHud() =>
            hudText = $"MockPlayer HP {CurrentHealth:0}/{maxHealth:0}   hits {HitsTaken}\nWASD move · hold RMB look · LMB attack";

        private void OnGUI()
        {
            if (showHud)
                GUI.Label(new Rect(10f, 10f, 420f, 50f), hudText);
        }
    }
}
