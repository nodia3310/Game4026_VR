using System;
using Game4026.Enemy;
using Game4026.Interaction;
using Game4026.Interaction.Grabbing;
using Game4026.Interaction.Melee;
using Game4026.Interaction.Ranged;
using Game4026.Interaction.TestScene;
using UnityEngine;

namespace Game4026.PlayerCombat
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class MuseumDemoRun : MonoBehaviour
    {
        [Serializable]
        private sealed class Equipment
        {
            public PhysicalGrabbable item;
            public Transform realmRest;
            [NonSerialized] public Pose HallRest;
        }

        [SerializeField] private DemoFlow flow;
        [SerializeField] private Behaviour debugKeys;
        [SerializeField] private MuseumFloorConfirmation floorConfirmation;
        [SerializeField] private MuseumPatrolLogPage patrolBook;
        [SerializeField] private TrackedPlayerHurtbox player;
        [SerializeField] private InteractionDesktopMode desktop;
        [SerializeField] private Camera headset;
        [SerializeField] private FieldBackpack backpack;
        [SerializeField] private Transform hallStart, realmArrival;
        [SerializeField] private Equipment[] equipment;

        private Pose playerPose;
        private bool enteredRealm, restartRequested;
        private TextMesh worldHudText;
        private string fullHudText, readingHudText;

        private void Awake()
        {
            playerPose = ReadPose(player.transform);
            foreach (var entry in equipment)
                entry.HallRest = ReadPose(entry.item.transform);
        }

        private void OnEnable() => player.ResetForRestart += OnRestart;
        private void OnDisable()
        {
            player.ResetForRestart -= OnRestart;
            RestoreHealthDisplay();
        }
        private void OnRestart() => restartRequested = true;

        private void Update()
        {
            if (flow.enabled || !floorConfirmation.IsGameplayReady) return;
            flow.enabled = true;
            debugKeys.enabled = true;
        }

        private void LateUpdate()
        {
            UpdateReadingHud();
            var camera = desktop.IsDesktopActive ? desktop.DesktopCamera : headset;
            var fader = ScreenFader.Current;
            if (fader && fader.transform.parent != camera.transform)
                fader.transform.SetParent(camera.transform, false);

            // Continuous locomotion moves a child rig. Only B's scripted travel moves this root.
            Pose newPose = ReadPose(player.transform);
            bool nowInRealm = Mathf.Abs(newPose.position.y - realmArrival.position.y) <
                              Mathf.Abs(newPose.position.y - hallStart.position.y);
            if (restartRequested)
            {
                restartRequested = false;
                backpack.ResetContents();
                foreach (var entry in equipment)
                    ReturnToRest(entry.item, entry.HallRest);
            }
            else if (newPose != playerPose)
            {
                Quaternion turn = newPose.rotation * Quaternion.Inverse(playerPose.rotation);
                foreach (var entry in equipment)
                {
                    var item = entry.item;
                    if (!item.gameObject.activeSelf) continue; // The backpack carries stored equipment.
                    if (item.IsHeld)
                    {
                        Pose pose = ReadPose(item.transform);
                        MoveItem(item, new Pose(newPose.position + turn * (pose.position - playerPose.position),
                            turn * pose.rotation));
                    }
                    else if (nowInRealm != enteredRealm && entry.realmRest)
                    {
                        ReturnToRest(item, nowInRealm ? ReadPose(entry.realmRest) : entry.HallRest);
                    }
                }
            }

            playerPose = newPose;
            enteredRealm = nowInRealm;
        }

        private void UpdateReadingHud()
        {
            if (!patrolBook || !patrolBook.IsOpen)
            {
                RestoreHealthDisplay();
                return;
            }

            // DemoWorldHud writes the objective and HP to one TextMesh before this LateUpdate.
            if (!worldHudText)
                worldHudText = flow.GetComponentInChildren<TextMesh>(true);
            if (!worldHudText || !worldHudText.gameObject.activeInHierarchy) return;

            string current = worldHudText.text;
            if (fullHudText != null && current == readingHudText) return;
            readingHudText = flow.HudMessage;
            fullHudText = current;
            worldHudText.text = readingHudText;
        }

        private void RestoreHealthDisplay()
        {
            if (fullHudText == null) return;
            if (worldHudText && worldHudText.text == readingHudText)
                worldHudText.text = fullHudText;
            fullHudText = null;
        }

        private static void ReturnToRest(PhysicalGrabbable item, Pose pose)
        {
            var grab = item.Grab;
            var manager = grab.interactionManager;
            if (manager && grab.isSelected)
            {
                bool throws = grab.throwOnDetach;
                grab.throwOnDetach = false;
                manager.SelectCancel(grab.firstInteractorSelecting, grab);
                grab.throwOnDetach = throws;
            }

            if (item.TryGetComponent(out ReboundingWeapon rebound) && rebound.IsInFlight)
            {
                rebound.enabled = false;
                rebound.enabled = true;
            }
            MoveItem(item, pose);
        }

        private static void MoveItem(PhysicalGrabbable item, Pose pose)
        {
            if (item.TryGetComponent(out BowDrawAim bow)) bow.CancelDraw();
            if (item.TryGetComponent(out MeleeWeapon melee)) melee.ResetSampling();
            var body = item.Body;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.position = pose.position;
            body.rotation = pose.rotation;
            item.transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        private static Pose ReadPose(Transform source) => new Pose(source.position, source.rotation);
    }
}
