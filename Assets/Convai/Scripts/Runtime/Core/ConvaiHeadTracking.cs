using Convai.Scripts.Runtime.Features;
using Convai.Scripts.Runtime.LoggerSystem;
using UnityEngine;

namespace Convai.Scripts.Runtime.Core
{
    /// <summary>
    ///     This class provides head tracking functionalities for an object (like a character) with an Animator.
    ///     It requires the Animator component to be attached to the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Convai/Character Head & Eye Tracking")]
    public class ConvaiHeadTracking : MonoBehaviour
    {
        private const float BOUNDS_LOOK_AT_HEIGHT_RATIO = 0.85f;
        private const float DEFAULT_LOOK_AT_HEIGHT = 1.6f;
        private const float POSITION_UPDATE_DELAY = 2f;

        [field: Header("Tracking Properties")]
        [Tooltip("The object that the head should track.")]
        [field: SerializeField]
        public Transform TargetObject { get; set; }

        [Range(0.0f, 100.0f)] [Tooltip("The maximum distance at which the head must still track target.")] [SerializeField]
        private float trackingDistance = 10f;

        [Tooltip("Speed at which character turns towards the target.")] [Range(1f, 10f)] [SerializeField]
        private float turnSpeed = 5.0f;

        [Header("Look At Weights")]
        [Range(0f, 1f)]
        [Tooltip(
            "Controls the amount of rotation applied to the body to achieve the 'Look At' target. The closer to 1, the more the body will rotate to follow the target.")]
        [SerializeField]
        private float bodyLookAtWeight = 0.6f;

        [Range(0f, 1f)]
        [Tooltip(
            "Controls the amount of rotation applied to the head to achieve the 'Look At' target. The closer to 1, the more the head will rotate to follow the target.")]
        [SerializeField]
        private float headLookAtWeight = 0.8f;

        [Range(0f, 1f)]
        [Tooltip(
            "Controls the amount of rotation applied to the eyes to achieve the 'Look At' target. The closer to 1, the more the eyes will rotate to follow the target.")]
        [SerializeField]
        private float eyesLookAtWeight = 1f;

        [Space(10)]
        [Tooltip(
            "Set this to true if you want the character to look away randomly, false to always look at the target")]
        [SerializeField]
        private bool lookAway;

        private Animator _animator;
        private float _appliedBodyLookAtWeight;
        private ConvaiActionsHandler _convaiActionsHandler;
        private float _currentLookAtWeight;
        private Transform _currentTargetHeadBone;
        private Collider[] _currentTargetColliders;
        private Renderer[] _currentTargetRenderers;
        private Transform _currentTargetRoot;
        private float _desiredLookAtWeight = 1f;
        private Transform _headPivot;
        private bool _isActionRunning;

        private void Start()
        {
            InitializeComponents();
            InitializeHeadPivot();
            InvokeRepeating(nameof(UpdateTarget), 0, POSITION_UPDATE_DELAY);
        }

        private void OnDisable()
        {
            if (_convaiActionsHandler != null)
                _convaiActionsHandler.UnregisterForActionEvents(ConvaiActionsHandler_OnActionStarted, ConvaiActionsHandler_OnActionEnded);
        }

        /// <summary>
        ///     Unity's built-in method called during the IK pass.
        /// </summary>
        public void OnAnimatorIK(int layerIndex)
        {
            PerformHeadTracking();
        }

        private void InitializeComponents()
        {
            if (!_animator) _animator = GetComponent<Animator>();
            InitializeTargetObject();

            if (TryGetComponent(out _convaiActionsHandler))
                _convaiActionsHandler.RegisterForActionEvents(ConvaiActionsHandler_OnActionStarted, ConvaiActionsHandler_OnActionEnded);
        }

        private void ConvaiActionsHandler_OnActionStarted(string action, GameObject target)
        {
            SetActionRunning(true);
        }

        private void ConvaiActionsHandler_OnActionEnded(string action, GameObject target)
        {
            SetActionRunning(false);
        }

        private void InitializeHeadPivot()
        {
            // Check if the pivot already exists
            if (_headPivot) return;

            // Create a new GameObject for the pivot
            _headPivot = new GameObject("HeadPivot").transform;

            // Set the new GameObject as a child of this character object
            _headPivot.transform.parent = transform;

            // Position the pivot appropriately, in this case, it seems like it's a bit above the base (probably around the character's neck/head)
            _headPivot.localPosition = new Vector3(0, 1.6f, 0);
        }


        private void RotateCharacterTowardsTarget(Vector3 lookAtPosition)
        {
            Vector3 toTarget = lookAtPosition - transform.position;
            float distance = toTarget.magnitude;
            if (distance <= Mathf.Epsilon)
                return;

            // Calculate the angle difference between the character's forward direction and the direction towards the target.
            float angleDifference = Vector3.Angle(transform.forward, toTarget);

            // Adjust turn speed based on distance to target.
            float adjustedTurnSpeed = turnSpeed * 4 * (1f / distance);

            // If the angle difference exceeds the limit, we turn the character smoothly towards the target.
            if (Mathf.Abs(angleDifference) > 0.65f)
            {
                Vector3 targetDirection = toTarget.normalized;

                // Zero out the y-component (up-down direction) to only rotate on the horizontal plane.
                targetDirection.y = 0;
                if (targetDirection.sqrMagnitude <= Mathf.Epsilon)
                    return;

                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation,
                    adjustedTurnSpeed * Time.deltaTime);

                // Ensure that the character doesn't tilt on the X and Z axis.
                transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
            }
        }


        private void InitializeTargetObject()
        {
            if (TargetObject != null) return;

            ConvaiLogger.Warn("No target object set for head tracking. Setting default target as main camera",
                ConvaiLogger.LogCategory.Character);
            if (Camera.main != null) TargetObject = Camera.main.transform;
        }

        /// <summary>
        ///     Updates the target weight for the look-at.
        /// </summary>
        private void UpdateTarget()
        {
            _desiredLookAtWeight = lookAway ? Random.Range(0.2f, 1.0f) : 1f;
        }

        /// <summary>
        ///     Performs the head tracking towards the target object.
        /// </summary>
        private void PerformHeadTracking()
        {
            if (_isActionRunning || TargetObject == null) return;

            Vector3 lookAtPosition = GetTargetLookAtPosition();
            float distance = Vector3.Distance(transform.position, lookAtPosition);
            DrawRayToTarget(lookAtPosition);

            // only perform head tracking if within threshold distance
            if (!(distance < trackingDistance / 2))
                _desiredLookAtWeight = 0;

            _headPivot.transform.LookAt(lookAtPosition); // orient the pivot towards the target look-at position
            SetCurrentLookAtWeight();
            // set the current look at weight based on how much rotation is needed

            // limit the head rotation
            float headRotation = _headPivot.localRotation.y;
            if (Mathf.Abs(headRotation) > 0.70f)
            {
                // clamp rotation if more than 80 degrees
                headRotation = Mathf.Sign(headRotation) * 0.70f;
                Quaternion localRotation = _headPivot.localRotation;
                localRotation.y = headRotation;
                _headPivot.localRotation = localRotation;
            }

            // adjust body rotation weight based on how much the head is rotated
            float targetBodyLookAtWeight = Mathf.Abs(_headPivot.localRotation.y) > 0.45f
                ? bodyLookAtWeight / 3f
                : 0f;

            // smooth transition between current and target body rotation weight
            _appliedBodyLookAtWeight = Mathf.Lerp(_appliedBodyLookAtWeight, targetBodyLookAtWeight, Time.deltaTime);

            // Apply rotation weights to the Animator
            RotateCharacterTowardsTarget(lookAtPosition);
            AdjustAnimatorLookAt(lookAtPosition);
        }

        /// <summary>
        ///     Method to set the current look at weight based on the desired look at weight.
        /// </summary>
        private void SetCurrentLookAtWeight()
        {
            float angleDifference = _headPivot.localRotation.y;

            // Lerp the currentLookAtWeight towards the desiredLookAtWeight or towards 0 if above a certain threshold.
            _currentLookAtWeight = Mathf.Abs(angleDifference) < 0.55f
                ? Mathf.Lerp(Mathf.Clamp(_currentLookAtWeight, 0, 1), Mathf.Clamp(_desiredLookAtWeight, 0, 1),
                    Time.deltaTime * POSITION_UPDATE_DELAY)
                : Mathf.Lerp(Mathf.Clamp(_currentLookAtWeight, 0, 1), 0, Time.deltaTime * POSITION_UPDATE_DELAY);
        }

        /// <summary>
        ///     Method to apply rotation weights to the Animator
        /// </summary>
        private void AdjustAnimatorLookAt(Vector3 lookAtPosition)
        {
            // Check if Animator or TargetObject are null
            if (!_animator || TargetObject == null)
            {
                // If either is null, set the look-at weight to 0 and return, effectively ending the method early
                if (_animator)
                    _animator.SetLookAtWeight(0);
                return;
            }

            // Set the look-at weights in the Animator.
            // This is used to dictate how much the body, head or eyes should turn to "look at" the target.
            // `Mathf.Clamp` is used to ensure the weight values lie between 0 and 1 (inclusive).
            // The body weight is clamped between 0 to 0.5 since it's less advisable to rotate the body too much versus the head or eyes.
            _animator.SetLookAtWeight(Mathf.Clamp(
                    _currentLookAtWeight, 0, 1),
                Mathf.Clamp(_appliedBodyLookAtWeight, 0, .5f),
                Mathf.Clamp(headLookAtWeight / 1.25f, 0, .8f),
                Mathf.Clamp(eyesLookAtWeight, 0, 1));

            // Set the look-at position for the Animator (where the body/head/eyes will turn toward)
            _animator.SetLookAtPosition(lookAtPosition);
        }

        /// <summary>
        ///     DebugLog utility to visualize the tracking mechanism
        /// </summary>
        private void DrawRayToTarget(Vector3 lookAtPosition)
        {
            Vector3 pos = transform.position;
            // Draw a debug ray from our position to the normalized direction towards the target, scaled by half of the tracking distance threshold.
            // The purpose is to visualize the direction and focus of the head tracking, and it's a useful debug tool in Unity's Scene view.
            // "Normalized" ensures that the vector has a magnitude (length) of 1, keeping the scaling of the vector consistent.
            // This ray appears red in the Scene view.
            Debug.DrawRay(pos,
                (lookAtPosition - pos).normalized * trackingDistance / 2, Color.red);
        }

        private Vector3 GetTargetLookAtPosition()
        {
            RefreshTargetReferences();

            if (_currentTargetHeadBone)
                return _currentTargetHeadBone.position;

            if (TryGetTargetBounds(out Bounds targetBounds))
            {
                return new Vector3(targetBounds.center.x,
                    targetBounds.min.y + targetBounds.size.y * BOUNDS_LOOK_AT_HEIGHT_RATIO,
                    targetBounds.center.z);
            }

            return TargetObject.position + Vector3.up * DEFAULT_LOOK_AT_HEIGHT;
        }

        private void RefreshTargetReferences()
        {
            if (_currentTargetRoot == TargetObject) return;

            _currentTargetRoot = TargetObject;
            _currentTargetHeadBone = null;
            _currentTargetRenderers = null;
            _currentTargetColliders = null;

            if (TargetObject == null) return;

            Animator targetAnimator = TargetObject.GetComponentInParent<Animator>();
            if (!targetAnimator)
                targetAnimator = TargetObject.GetComponentInChildren<Animator>();

            if (targetAnimator && targetAnimator.isHuman)
            {
                _currentTargetHeadBone = targetAnimator.GetBoneTransform(HumanBodyBones.Head);

                if (!_currentTargetHeadBone)
                    _currentTargetHeadBone = targetAnimator.GetBoneTransform(HumanBodyBones.Neck);

                if (!_currentTargetHeadBone)
                    _currentTargetHeadBone = targetAnimator.GetBoneTransform(HumanBodyBones.UpperChest);

                if (!_currentTargetHeadBone)
                    _currentTargetHeadBone = targetAnimator.GetBoneTransform(HumanBodyBones.Chest);
            }

            _currentTargetRenderers = TargetObject.GetComponentsInChildren<Renderer>();
            _currentTargetColliders = TargetObject.GetComponentsInChildren<Collider>();
        }

        private bool TryGetTargetBounds(out Bounds bounds)
        {
            if (TryEncapsulateRendererBounds(_currentTargetRenderers, out bounds))
                return true;

            return TryEncapsulateColliderBounds(_currentTargetColliders, out bounds);
        }

        private static bool TryEncapsulateRendererBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            if (renderers == null || renderers.Length == 0)
                return false;

            bool hasBounds = false;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        private static bool TryEncapsulateColliderBounds(Collider[] colliders, out Bounds bounds)
        {
            bounds = default;
            if (colliders == null || colliders.Length == 0)
                return false;

            bool hasBounds = false;
            foreach (Collider collider in colliders)
            {
                if (!collider)
                    continue;

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(collider.bounds);
            }

            return hasBounds;
        }

        public void SetActionRunning(bool newValue)
        {
            _isActionRunning = newValue;
        }
    }
}
