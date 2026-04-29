using System.Collections;
using Convai.Scripts.Runtime.Addons;
using Convai.Scripts.Runtime.Core;
using Convai.Scripts.Runtime.UI;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

public class TuringVrIntegration : MonoBehaviour
{
    private const string MetaControllerPrefabResourcePath = "VR/OVRRuntimeControllerPrefab";
    private static readonly Vector3 LeftControllerFallbackLocalPosition = new Vector3(-0.18f, -0.2f, 0.32f);
    private static readonly Vector3 RightControllerFallbackLocalPosition = new Vector3(0.18f, -0.2f, 0.32f);

    [Header("VR HUD")]
    [SerializeField] private float hudDistance = 1.1f;
    [SerializeField] private float hudHorizontalOffset = 0.32f;
    [SerializeField] private float hudVerticalOffset = -0.03f;
    [SerializeField] private float hudScale = 0.001f;

    [Header("VR Subtitles")]
    [SerializeField] private float subtitleDistance = 0.95f;
    [SerializeField] private float subtitleVerticalOffset = -0.3f;
    [SerializeField] private float subtitleScale = 0.001f;

    [Header("Talk Input")]
    [SerializeField] private bool useVrControllerTalkInput = true;

    [Header("Player Rig")]
    [SerializeField] private bool snapRigToFloor = true;
    [SerializeField] private float seatedEyeHeight = 1.6f;

    [Header("Controller Visuals")]
    [SerializeField] private bool showControllerVisuals = true;
    [SerializeField] private bool useMetaControllerModels = false;
    [SerializeField] private GameObject controllerModelPrefab;
    [SerializeField] private bool useCapsuleFallbackIfModelMissing = true;
    [SerializeField] private Vector3 controllerVisualScale = new Vector3(0.04f, 0.08f, 0.04f);
    [SerializeField] private Color leftControllerColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField] private Color rightControllerColor = new Color(1f, 0.35f, 0.25f, 1f);

    private bool _isVrActive;
    private bool _isInitialized;
    private bool _xrStartInProgress;
    private bool _xrInitializationWarningLogged;
    private bool _subtitleConfigured;
    private bool _wasTalkHeldLastFrame;
    private bool _floorTrackingActive;
    private Camera _mainCamera;
    private Transform _trackingOrigin;
    private Transform _leftControllerVisual;
    private Transform _rightControllerVisual;
    private TuringGameManager _gameManager;
    private TuringVrSubtitleUI _vrSubtitleUI;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<TuringVrIntegration>() != null)
        {
            return;
        }

        GameObject go = new GameObject("Turing VR Integration");
        go.AddComponent<TuringVrIntegration>();
    }

    private void OnEnable()
    {
        StartCoroutine(EnsureVrReady());
    }

    private void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        if (useVrControllerTalkInput)
        {
            if (ShouldBlockTalkInput())
            {
                if (_wasTalkHeldLastFrame)
                {
                    _wasTalkHeldLastFrame = false;
                    ConvaiInputManager.Instance?.talkKeyInteract?.Invoke(false);
                }

                return;
            }

            bool isTalkHeld = IsAnyTalkControlHeld();
            if (isTalkHeld != _wasTalkHeldLastFrame)
            {
                _wasTalkHeldLastFrame = isTalkHeld;
                ConvaiInputManager.Instance?.talkKeyInteract?.Invoke(isTalkHeld);
            }
        }
    }

    private void LateUpdate()
    {
        if (_mainCamera == null || !_mainCamera.isActiveAndEnabled)
        {
            _mainCamera = ResolveMainCamera();
            _subtitleConfigured = false;
        }

        if (!_subtitleConfigured)
        {
            _subtitleConfigured = TryConfigureSubtitleUi();
        }

        if (!_isInitialized || !_isVrActive)
        {
            return;
        }

        UpdateHeadPose();
        UpdateControllerVisual(_leftControllerVisual, XRNode.LeftHand);
        UpdateControllerVisual(_rightControllerVisual, XRNode.RightHand);

        if (!_subtitleConfigured)
        {
            _subtitleConfigured = TryConfigureSubtitleUi();
        }
    }

    private IEnumerator EnsureVrReady()
    {
        float timeout = 20f;
        float elapsed = 0f;

        while (!_isInitialized)
        {
            TryStartXrIfNeeded();

            _isVrActive = IsVrRuntimeActive();
            if (_isVrActive)
            {
                TryInitializeForCurrentScene();
            }
            else
            {
                if (_mainCamera == null)
                {
                    _mainCamera = ResolveMainCamera();
                }

                if (!_subtitleConfigured)
                {
                    _subtitleConfigured = TryConfigureSubtitleUi();
                }
            }

            elapsed += 0.25f;
            if (elapsed >= timeout && !_xrInitializationWarningLogged)
            {
                Debug.LogWarning("TuringVrIntegration: XR runtime did not initialize within timeout. Will keep retrying; subtitles can still attach once the scene camera and Convai UI are available.");
                _xrInitializationWarningLogged = true;
            }

            yield return new WaitForSeconds(0.25f);
        }
    }

    private void TryStartXrIfNeeded()
    {
        XRManagerSettings xrManager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (xrManager == null || xrManager.activeLoader != null || _xrStartInProgress)
        {
            return;
        }

        StartCoroutine(StartXrLoader(xrManager));
    }

    private IEnumerator StartXrLoader(XRManagerSettings xrManager)
    {
        _xrStartInProgress = true;

        yield return xrManager.InitializeLoader();
        if (xrManager.activeLoader != null)
        {
            xrManager.StartSubsystems();
        }

        _xrStartInProgress = false;
    }

    private void TryInitializeForCurrentScene()
    {
        if (_isInitialized)
        {
            return;
        }

        _mainCamera = ResolveMainCamera();

        if (_mainCamera == null)
        {
            return;
        }

        _gameManager = FindFirstObjectByType<TuringGameManager>();
        _trackingOrigin = _mainCamera.transform.parent != null ? _mainCamera.transform.parent : _mainCamera.transform;

        // Force reliable controller proxies in runtime builds.
        showControllerVisuals = true;
        useMetaControllerModels = false;
        useCapsuleFallbackIfModelMissing = true;

        DisableDesktopLocomotion();
        ConfigurePlayerRigForVr();
        ConfigureVrHud();
        EnsureControllerVisuals();
        _subtitleConfigured = TryConfigureSubtitleUi();

        _isInitialized = true;
    }

    private void ConfigurePlayerRigForVr()
    {
        _floorTrackingActive = TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
        if (!_floorTrackingActive)
        {
            TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
        }

        if (_mainCamera == null)
        {
            return;
        }

        Transform rig = _mainCamera.transform.parent;
        if (rig != null && snapRigToFloor)
        {
            float targetY = _floorTrackingActive ? 0f : seatedEyeHeight;
            Vector3 rigLocal = rig.localPosition;
            if (!Mathf.Approximately(rigLocal.y, targetY))
            {
                rig.localPosition = new Vector3(rigLocal.x, targetY, rigLocal.z);
            }
        }

        _mainCamera.transform.localPosition = Vector3.zero;
        _mainCamera.transform.localRotation = Quaternion.identity;
    }

    private static bool TrySetTrackingOriginMode(TrackingOriginModeFlags mode)
    {
        var subsystems = new System.Collections.Generic.List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        bool any = false;
        foreach (XRInputSubsystem subsystem in subsystems)
        {
            if (subsystem == null)
            {
                continue;
            }

            TrackingOriginModeFlags supported = subsystem.GetSupportedTrackingOriginModes();
            if ((supported & mode) == 0)
            {
                continue;
            }

            if (subsystem.TrySetTrackingOriginMode(mode))
            {
                subsystem.TryRecenter();
                any = true;
            }
        }

        return any;
    }

    private static bool IsVrRuntimeActive()
    {
        XRManagerSettings xrManager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (xrManager == null || xrManager.activeLoader == null)
        {
            return false;
        }

        bool hasRunningDisplay = false;
        var displays = new System.Collections.Generic.List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        foreach (XRDisplaySubsystem display in displays)
        {
            if (display != null && display.running)
            {
                hasRunningDisplay = true;
                break;
            }
        }

        return hasRunningDisplay;
    }

    private static void DisableDesktopLocomotion()
    {
        ConvaiPlayerMovement[] movementScripts = FindObjectsByType<ConvaiPlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ConvaiPlayerMovement movement in movementScripts)
        {
            if (movement != null)
            {
                movement.enabled = false;
            }
        }
    }

    private void ConfigureVrHud()
    {
        TuringGameManager gameManager = FindFirstObjectByType<TuringGameManager>();
        if (gameManager != null)
        {
            gameManager.showTurnOverlay = false;
        }

        TuringHudController hudController = FindFirstObjectByType<TuringHudController>();
        if (hudController == null)
        {
            return;
        }

        Canvas hudCanvas = hudController.GetComponent<Canvas>();
        if (hudCanvas == null)
        {
            return;
        }

        ConfigureCanvasForHeadlockedUi(hudCanvas, hudDistance, hudHorizontalOffset, hudVerticalOffset, hudScale, new Vector2(840f, 420f));
    }

    private void EnsureControllerVisuals()
    {
        if (!showControllerVisuals || _trackingOrigin == null)
        {
            return;
        }

        if (_leftControllerVisual == null)
        {
            _leftControllerVisual = CreateControllerVisual("Left Controller", XRNode.LeftHand, leftControllerColor);
        }

        if (_rightControllerVisual == null)
        {
            _rightControllerVisual = CreateControllerVisual("Right Controller", XRNode.RightHand, rightControllerColor);
        }
    }

    private Transform CreateControllerVisual(string objectName, XRNode node, Color fallbackColor)
    {
        if (useMetaControllerModels)
        {
            Transform modelVisual = CreateMetaControllerVisual(objectName, node);
            if (modelVisual != null)
            {
                return modelVisual;
            }
        }

        if (!useCapsuleFallbackIfModelMissing)
        {
            return null;
        }

        return CreateCapsuleControllerVisual($"{objectName} Proxy", fallbackColor);
    }

    private Transform CreateMetaControllerVisual(string objectName, XRNode node)
    {
        GameObject modelPrefab = controllerModelPrefab != null
            ? controllerModelPrefab
            : Resources.Load<GameObject>(MetaControllerPrefabResourcePath);
        if (modelPrefab == null)
        {
            Debug.LogWarning(
                $"TuringVrIntegration: Could not find controller model prefab. Assign one in inspector or place OVRRuntimeControllerPrefab at Resources/{MetaControllerPrefabResourcePath}. Falling back to capsule proxies.");
            return null;
        }

        GameObject visual = Instantiate(modelPrefab, _trackingOrigin, false);
        visual.name = objectName;
        visual.SetActive(true);

        if (visual.TryGetComponent(out OVRControllerHelper helper))
        {
            helper.m_controller = node == XRNode.LeftHand ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            return visual.transform;
        }

        if (visual.TryGetComponent(out OVRRuntimeController runtimeController))
        {
            runtimeController.m_controller = node == XRNode.LeftHand ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            if (runtimeController.m_controllerModelShader == null)
            {
                runtimeController.m_controllerModelShader = Shader.Find("Standard");
            }

            return visual.transform;
        }

        OVRRuntimeController fallbackRuntimeController = visual.AddComponent<OVRRuntimeController>();
        fallbackRuntimeController.m_controller = node == XRNode.LeftHand ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        fallbackRuntimeController.m_controllerModelShader = Shader.Find("Standard");

        return visual.transform;
    }

    private Transform CreateCapsuleControllerVisual(string objectName, Color color)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = objectName;
        visual.transform.SetParent(_trackingOrigin, false);
        visual.transform.localScale = controllerVisualScale;

        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(renderer.sharedMaterial);
            material.color = color;
            renderer.sharedMaterial = material;
        }

        visual.SetActive(false);
        return visual.transform;
    }

    private void UpdateHeadPose()
    {
        if (_mainCamera == null)
        {
            return;
        }

        InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!head.isValid)
        {
            return;
        }

        if (head.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked) && !isTracked)
        {
            return;
        }

        if (head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
        {
            _mainCamera.transform.localPosition = position;
        }

        if (head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
        {
            _mainCamera.transform.localRotation = rotation;
        }
    }

    private void UpdateControllerVisual(Transform visual, XRNode node)
    {
        if (visual == null)
        {
            return;
        }

        if (IsRuntimeDrivenControllerVisual(visual))
        {
            visual.gameObject.SetActive(showControllerVisuals);
            return;
        }

        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
        {
            SetControllerFallbackPose(visual, node);
            return;
        }

        bool hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position);
        bool hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation);
        if (!hasPosition || !hasRotation)
        {
            SetControllerFallbackPose(visual, node);
            return;
        }

        if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked) && !isTracked)
        {
            SetControllerFallbackPose(visual, node);
            return;
        }

        visual.gameObject.SetActive(true);
        visual.localPosition = position;
        visual.localRotation = rotation;
    }

    private static bool IsAnyTalkControlHeld()
    {
        return IsTalkHeldOnNode(XRNode.LeftHand) || IsTalkHeldOnNode(XRNode.RightHand);
    }

    private static bool IsTalkHeldOnNode(XRNode node)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
        {
            return false;
        }

        bool triggerHeld = device.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger) && trigger;
        return triggerHeld;
    }

    private bool TryConfigureSubtitleUi()
    {
        if (_mainCamera == null || !_mainCamera.isActiveAndEnabled)
        {
            _mainCamera = ResolveMainCamera();
        }

        if (_mainCamera == null || ConvaiChatUIHandler.Instance == null)
        {
            return false;
        }

        if (_vrSubtitleUI == null)
        {
            GameObject subtitleObject = new GameObject("Turing VR Subtitle UI", typeof(RectTransform));
            _vrSubtitleUI = subtitleObject.AddComponent<TuringVrSubtitleUI>();
        }

        _vrSubtitleUI.Configure(_mainCamera, subtitleDistance, subtitleVerticalOffset, subtitleScale, new Vector2(1400f, 320f));

        ConvaiChatUIHandler uiHandler = ConvaiChatUIHandler.Instance;
        if (uiHandler.GetUIAppearances.TryGetValue(ConvaiChatUIHandler.UIType.Subtitle, out IChatUI existingSubtitleUi)
            && existingSubtitleUi != null
            && !ReferenceEquals(existingSubtitleUi, _vrSubtitleUI))
        {
            existingSubtitleUi.DeactivateUI();
        }

        uiHandler.GetUIAppearances[ConvaiChatUIHandler.UIType.Subtitle] = _vrSubtitleUI;

        if (UISaveLoadSystem.Instance != null)
        {
            UISaveLoadSystem.Instance.TranscriptUIActiveStatus = true;
            UISaveLoadSystem.Instance.UIType = ConvaiChatUIHandler.UIType.Subtitle;
        }

        IChatUI currentUi = uiHandler.GetCurrentUI();
        if (!ReferenceEquals(currentUi, _vrSubtitleUI))
        {
            currentUi?.DeactivateUI();
            uiHandler.SetUIType(ConvaiChatUIHandler.UIType.Subtitle);
        }

        _vrSubtitleUI.ActivateUI();
        Debug.Log("TuringVrIntegration: installed VR subtitle UI as Convai's active subtitle target.");
        return true;
    }

    private static Camera ResolveMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            return mainCamera;
        }

        Camera fallback = null;
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Camera camera in cameras)
        {
            if (camera == null || !camera.isActiveAndEnabled)
            {
                continue;
            }

            if (camera.CompareTag("MainCamera"))
            {
                return camera;
            }

            if (fallback == null || (fallback.stereoTargetEye == StereoTargetEyeMask.None && camera.stereoTargetEye != StereoTargetEyeMask.None))
            {
                fallback = camera;
            }
        }

        return fallback;
    }

    private void ConfigureCanvasForHeadlockedUi(Canvas canvas, float distance, float horizontalOffset, float verticalOffset, float scale, Vector2 fallbackSize)
    {
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = _mainCamera;

        RectTransform rectTransform = canvas.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            if (rectTransform.sizeDelta == Vector2.zero)
            {
                rectTransform.sizeDelta = fallbackSize;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
        }

        Transform uiTransform = canvas.transform;
        uiTransform.SetParent(_mainCamera.transform, false);
        uiTransform.localPosition = new Vector3(horizontalOffset, verticalOffset, distance);
        uiTransform.localRotation = Quaternion.identity;
        uiTransform.localScale = Vector3.one * scale;
    }

    private bool ShouldBlockTalkInput()
    {
        if (TuringSessionMenuUI.IsMenuVisible)
        {
            return true;
        }

        if (_gameManager == null)
        {
            _gameManager = FindFirstObjectByType<TuringGameManager>();
        }

        return _gameManager != null && (!_gameManager.HasGameStarted || _gameManager.HasGameFinished);
    }

    private static bool IsRuntimeDrivenControllerVisual(Transform visual)
    {
        return visual.GetComponent<OVRControllerHelper>() != null || visual.GetComponent<OVRRuntimeController>() != null;
    }

    private void SetControllerFallbackPose(Transform visual, XRNode node)
    {
        if (visual == null)
        {
            return;
        }

        visual.gameObject.SetActive(true);
        visual.localPosition = node == XRNode.LeftHand ? LeftControllerFallbackLocalPosition : RightControllerFallbackLocalPosition;
        visual.localRotation = Quaternion.identity;
    }
}
