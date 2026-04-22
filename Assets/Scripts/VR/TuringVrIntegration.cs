using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

public class TuringVrIntegration : MonoBehaviour
{
    [Header("Controller Visuals")]
    [SerializeField] private bool showControllerVisuals = true;
    [SerializeField] private Vector3 controllerVisualScale = new Vector3(0.045f, 0.12f, 0.045f);
    [SerializeField] private Color leftControllerColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField] private Color rightControllerColor = new Color(1f, 0.35f, 0.25f, 1f);

    private static readonly Vector3 LeftControllerFallbackLocalPosition = new Vector3(-0.18f, -0.2f, 0.32f);
    private static readonly Vector3 RightControllerFallbackLocalPosition = new Vector3(0.18f, -0.2f, 0.32f);

    private bool _isInitialized;
    private bool _xrStartInProgress;
    private bool _isVrActive;
    private Camera _mainCamera;
    private Transform _trackingOrigin;
    private Transform _leftControllerVisual;
    private Transform _rightControllerVisual;

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

    private void LateUpdate()
    {
        if (!_isInitialized || !_isVrActive)
        {
            return;
        }

        UpdateHeadPose();
        UpdateControllerVisual(_leftControllerVisual, XRNode.LeftHand);
        UpdateControllerVisual(_rightControllerVisual, XRNode.RightHand);
    }

    private IEnumerator EnsureVrReady()
    {
        float timeout = 20f;
        float elapsed = 0f;

        while (!_isInitialized && elapsed < timeout)
        {
            TryStartXrIfNeeded();

            _isVrActive = IsVrRuntimeActive();
            if (_isVrActive)
            {
                TryInitializeForCurrentScene();
            }

            elapsed += 0.25f;
            yield return new WaitForSeconds(0.25f);
        }

        if (!_isInitialized)
        {
            Debug.LogWarning("TuringVrIntegration: XR runtime did not initialize within timeout. Controller visuals will stay inactive.");
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

        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            _mainCamera = FindFirstObjectByType<Camera>();
        }

        if (_mainCamera == null)
        {
            return;
        }

        _trackingOrigin = _mainCamera.transform.parent != null ? _mainCamera.transform.parent : _mainCamera.transform;

        DisableDesktopLocomotion();
        EnsureControllerVisuals();

        _isInitialized = true;
    }

    private static bool IsVrRuntimeActive()
    {
        XRManagerSettings xrManager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (xrManager == null || xrManager.activeLoader == null)
        {
            return false;
        }

        System.Collections.Generic.List<XRDisplaySubsystem> displays = new System.Collections.Generic.List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        foreach (XRDisplaySubsystem display in displays)
        {
            if (display != null && display.running)
            {
                return true;
            }
        }

        return false;
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

    private Transform CreateControllerVisual(string objectName, XRNode node, Color color)
    {
        GameObject visualRoot = new GameObject(objectName);
        visualRoot.transform.SetParent(_trackingOrigin, false);
        visualRoot.transform.localScale = controllerVisualScale;

        GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        grip.name = "Grip";
        grip.transform.SetParent(visualRoot.transform, false);
        grip.transform.localPosition = new Vector3(0f, 0f, 0f);
        grip.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        grip.transform.localScale = new Vector3(0.65f, 1.25f, 0.65f);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(visualRoot.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.13f, 0f);
        head.transform.localScale = new Vector3(0.7f, 0.45f, 0.9f);

        GameObject trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trigger.name = "Trigger";
        trigger.transform.SetParent(visualRoot.transform, false);
        trigger.transform.localPosition = new Vector3(0.16f, -0.02f, 0.01f);
        trigger.transform.localScale = new Vector3(0.12f, 0.18f, 0.28f);

        GameObject thumbstick = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        thumbstick.name = "Thumbstick";
        thumbstick.transform.SetParent(visualRoot.transform, false);
        thumbstick.transform.localPosition = new Vector3(-0.11f, 0.1f, 0.05f);
        thumbstick.transform.localScale = Vector3.one * 0.12f;

        GameObject menuButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
        menuButton.name = "MenuButton";
        menuButton.transform.SetParent(visualRoot.transform, false);
        menuButton.transform.localPosition = new Vector3(-0.08f, 0.21f, -0.03f);
        menuButton.transform.localScale = new Vector3(0.14f, 0.06f, 0.1f);

        ApplyColor(grip, color);
        ApplyColor(head, Color.Lerp(color, Color.white, 0.25f));
        ApplyColor(trigger, Color.Lerp(color, Color.black, 0.1f));
        ApplyColor(thumbstick, Color.white);
        ApplyColor(menuButton, Color.white);

        Destroy(grip.GetComponent<Collider>());
        Destroy(head.GetComponent<Collider>());
        Destroy(trigger.GetComponent<Collider>());
        Destroy(thumbstick.GetComponent<Collider>());
        Destroy(menuButton.GetComponent<Collider>());

        visualRoot.SetActive(false);
        return visualRoot.transform;
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

    private void SetControllerFallbackPose(Transform visual, XRNode node)
    {
        visual.gameObject.SetActive(true);
        visual.localPosition = node == XRNode.LeftHand ? LeftControllerFallbackLocalPosition : RightControllerFallbackLocalPosition;
        visual.localRotation = Quaternion.identity;
    }

    private static void ApplyColor(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Material material = new Material(Shader.Find("Standard"));
        material.color = color;
        renderer.sharedMaterial = material;
    }
}