using Convai.Scripts.Runtime.Core;
using Convai.Scripts.Runtime.UI;
using TMPro;
using UnityEngine;

namespace Convai.Scripts.Runtime.Addons
{
    /// <summary>
    ///     Monitors the duration of the talk button press and notifies the Notification System if released prematurely.
    /// </summary>
    public class TalkButtonDurationChecker : MonoBehaviour
    {
        [Header("Talk Input Validation")]
        [SerializeField, Min(0.05f)]
        [Tooltip("Minimum hold time before release is considered intentional speech input.")]
        private float minTalkDurationSeconds = 0.2f;

        /// <summary>
        ///     Flag indicating whether the talk button was released prematurely.
        /// </summary>
        [HideInInspector] public bool isTalkKeyReleasedEarly;

        private TMP_InputField _activeInputField;

        /// <summary>
        ///     Timer to track the duration of the talk button press.
        /// </summary>
        private float _timer;

        private UIAppearanceSettings _uiAppearanceSettings;

        private void Awake()
        {
            _uiAppearanceSettings = FindObjectOfType<UIAppearanceSettings>();
            RefreshActiveInputField();
        }

        /// <summary>
        ///     Update is called once per frame.
        ///     It checks if the talk button is being held down or released.
        /// </summary>
        private void Update()
        {
            ConvaiInputManager inputManager = ConvaiInputManager.Instance;
            if (inputManager == null)
            {
                return;
            }

            // Check if the talk button is being held down and increment the timer based on the time passed since the last frame.
            if (inputManager.IsTalkKeyHeld && !UIUtilities.IsAnyInputFieldFocused()) _timer += Time.deltaTime;
        }

        private void OnEnable()
        {
            ConvaiNPCManager npcManager = ConvaiNPCManager.Instance;
            if (npcManager != null)
            {
                npcManager.OnActiveNPCChanged += ConvaiNPCManager_OnActiveNPCChanged;
            }

            if (_uiAppearanceSettings != null)
            {
                _uiAppearanceSettings.OnAppearanceChanged += UIAppearanceSettings_OnAppearanceChanged;
            }

            ConvaiInputManager inputManager = ConvaiInputManager.Instance;
            if (inputManager != null)
            {
                inputManager.talkKeyInteract += HandleTalkButtonRelease;
            }

            RefreshActiveInputField();
        }

        private void OnDisable()
        {
            ConvaiNPCManager npcManager = ConvaiNPCManager.Instance;
            if (npcManager != null)
            {
                npcManager.OnActiveNPCChanged -= ConvaiNPCManager_OnActiveNPCChanged;
            }

            if (_uiAppearanceSettings != null)
            {
                _uiAppearanceSettings.OnAppearanceChanged -= UIAppearanceSettings_OnAppearanceChanged;
            }

            ConvaiInputManager inputManager = ConvaiInputManager.Instance;
            if (inputManager != null)
            {
                inputManager.talkKeyInteract -= HandleTalkButtonRelease;
            }
        }

        private void HandleTalkButtonRelease(bool releaseState)
        {
            if (releaseState || UIUtilities.IsAnyInputFieldFocused()) return;

            if (_activeInputField != null && _activeInputField.isFocused)
            {
                _timer = 0;
                return;
            }

            if (_timer <= 0f)
            {
                _timer = 0f;
                return;
            }

            CheckTalkButtonRelease();
            // Reset the timer for the next talk action.
            _timer = 0;
        }

        private void ConvaiNPCManager_OnActiveNPCChanged(ConvaiNPC convaiNpc)
        {
            if (convaiNpc == null || convaiNpc.playerInteractionManager == null)
            {
                _activeInputField = null;
                return;
            }

            _activeInputField = convaiNpc.playerInteractionManager.FindActiveInputField();
        }

        private void UIAppearanceSettings_OnAppearanceChanged()
        {
            RefreshActiveInputField();
        }

        private void RefreshActiveInputField()
        {
            ConvaiNPCManager npcManager = ConvaiNPCManager.Instance;
            ConvaiNPC convaiNpc = npcManager != null ? npcManager.activeConvaiNPC : null;
            if (convaiNpc == null || convaiNpc.playerInteractionManager == null)
            {
                _activeInputField = null;
                return;
            }

            _activeInputField = convaiNpc.playerInteractionManager.FindActiveInputField();
        }

        /// <summary>
        ///     Checks if the talk button was released prematurely and triggers a notification if so.
        /// </summary>
        private void CheckTalkButtonRelease()
        {
            // Initialize the flag to false.
            isTalkKeyReleasedEarly = false;

            ConvaiNPCManager npcManager = ConvaiNPCManager.Instance;
            if (npcManager == null || npcManager.activeConvaiNPC == null)
            {
                return;
            }

            NotificationSystemHandler notificationHandler = NotificationSystemHandler.Instance;
            if (notificationHandler == null)
            {
                return;
            }

            // Trigger a notification if the talk button is released before reaching the minimum required duration.
            if (_timer < minTalkDurationSeconds)
            {
                // Set the flag to true and request a notification.
                isTalkKeyReleasedEarly = true;
                notificationHandler.NotificationRequest(NotificationType.TalkButtonReleasedEarly);
            }
        }
    }
}