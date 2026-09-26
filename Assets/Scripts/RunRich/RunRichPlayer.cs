using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RunRichClone
{
    public sealed class RunRichPlayer : MonoBehaviour
    {
        private const float TrackHalfWidth = 2.85f;

        [SerializeField] private float forwardSpeed = 4.6f;

        private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Quaternion> restRotations = new Dictionary<string, Quaternion>();
        private CharacterController controller;
        private float targetX;
        private float lastPointerX;
        private bool dragging;
        private float animationTime;

        private static readonly string[] AnimatedBones =
        {
            "mixamorig:LeftUpLeg", "mixamorig:RightUpLeg",
            "mixamorig:LeftLeg", "mixamorig:RightLeg",
            "mixamorig:LeftArm", "mixamorig:RightArm",
            "mixamorig:LeftForeArm", "mixamorig:RightForeArm",
            "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Hips"
        };

        public RunRichGame Game { get; set; }
        public Transform VisualRoot { get; set; }

        private void Awake()
        {
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = 1.75f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 0.88f, 0f);
            controller.skinWidth = 0.04f;
            targetX = transform.position.x;
        }

        private void Update()
        {
            if (Game == null)
                return;

            ReadInput();
            AnimateCharacter();

            if (!Game.IsRunning)
                return;

            var lateral = Mathf.Clamp(targetX - transform.position.x, -10f * Time.deltaTime, 10f * Time.deltaTime);
            controller.Move(new Vector3(lateral, -4f * Time.deltaTime, forwardSpeed * Time.deltaTime));
        }

        public void CacheModel()
        {
            if (VisualRoot == null)
                return;

            foreach (var transformInModel in VisualRoot.GetComponentsInChildren<Transform>(true))
            {
                foreach (var boneName in AnimatedBones)
                {
                    if (transformInModel.name != boneName || bones.ContainsKey(boneName))
                        continue;

                    bones.Add(boneName, transformInModel);
                    restRotations.Add(boneName, transformInModel.localRotation);
                }
            }
        }

        public void ResetPlayer()
        {
            controller.enabled = false;
            transform.position = new Vector3(0f, 0.05f, 1.5f);
            transform.rotation = Quaternion.identity;
            controller.enabled = true;
            targetX = 0f;
        }

        public IEnumerator Celebrate()
        {
            const float duration = 1.4f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.Rotate(0f, 260f * Time.deltaTime, 0f);
                VisualRoot.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(elapsed * 8f)) * 0.14f, 0f);
                yield return null;
            }
        }

        private void ReadInput()
        {
            var pointerDown = Input.GetMouseButtonDown(0);
            var pointerHeld = Input.GetMouseButton(0);
            var pointerUp = Input.GetMouseButtonUp(0);

            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                pointerDown = touch.phase == TouchPhase.Began;
                pointerHeld = touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
                pointerUp = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                HandlePointer(touch.position.x, pointerDown, pointerHeld, pointerUp);
            }
            else
            {
                HandlePointer(Input.mousePosition.x, pointerDown, pointerHeld, pointerUp);
            }

            var keyboard = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(keyboard) > 0.01f)
            {
                Game.StartRun();
                targetX = Mathf.Clamp(targetX + keyboard * 7f * Time.deltaTime, -TrackHalfWidth, TrackHalfWidth);
            }
        }

        private void HandlePointer(float pointerX, bool down, bool held, bool up)
        {
            if (down)
            {
                dragging = true;
                lastPointerX = pointerX;
                Game.StartRun();
            }

            if (dragging && held)
            {
                var screenScale = Mathf.Max(1f, Screen.width);
                var delta = (pointerX - lastPointerX) / screenScale;
                targetX = Mathf.Clamp(targetX + delta * 8.3f, -TrackHalfWidth, TrackHalfWidth);
                lastPointerX = pointerX;
            }

            if (up)
                dragging = false;
        }

        private void AnimateCharacter()
        {
            if (VisualRoot == null)
                return;

            animationTime += Time.deltaTime * (Game.IsRunning ? 8.8f : 2.2f);
            var stride = Game.IsRunning ? Mathf.Sin(animationTime) : 0f;
            var secondary = Game.IsRunning ? Mathf.Sin(animationTime + Mathf.PI * 0.5f) : Mathf.Sin(animationTime) * 0.08f;

            SetBone("mixamorig:LeftUpLeg", Quaternion.Euler(stride * 34f, 0f, 0f));
            SetBone("mixamorig:RightUpLeg", Quaternion.Euler(-stride * 34f, 0f, 0f));
            SetBone("mixamorig:LeftLeg", Quaternion.Euler(Mathf.Max(0f, -stride) * 38f, 0f, 0f));
            SetBone("mixamorig:RightLeg", Quaternion.Euler(Mathf.Max(0f, stride) * 38f, 0f, 0f));
            SetBone("mixamorig:LeftArm", Quaternion.Euler(-stride * 28f, 0f, 72f));
            SetBone("mixamorig:RightArm", Quaternion.Euler(stride * 28f, 0f, -72f));
            SetBone("mixamorig:LeftForeArm", Quaternion.Euler(-18f, 0f, 0f));
            SetBone("mixamorig:RightForeArm", Quaternion.Euler(-18f, 0f, 0f));
            SetBone("mixamorig:Spine", Quaternion.Euler(secondary * 2f, 0f, -secondary * 1.5f));
            SetBone("mixamorig:Hips", Quaternion.Euler(0f, secondary * 2.5f, stride * 4.5f));

            VisualRoot.localPosition = new Vector3(
                0f,
                Game.IsRunning ? Mathf.Abs(stride) * 0.035f : Mathf.Sin(animationTime) * 0.01f,
                0f);
        }

        private void SetBone(string boneName, Quaternion offset)
        {
            if (bones.TryGetValue(boneName, out var bone))
                bone.localRotation = restRotations[boneName] * offset;
        }
    }
}
