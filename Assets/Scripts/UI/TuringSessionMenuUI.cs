using UnityEngine;
using UnityEngine.XR;
using Convai.Scripts.Runtime.Core;

public class TuringSessionMenuUI : MonoBehaviour
{
    [SerializeField] private TuringGameManager gameManager;

    public static bool IsMenuVisible { get; private set; }

    private bool _subscribed;
    private bool _showResultPopup;
    private bool _didPlayerWin;
    private bool _isPaused;
    private bool _pauseButtonHeldLastFrame;
    private bool _talkInputLocked;
    private string _resultText = "";
    private Vector2 _resultScroll;
    private GUIStyle _panelStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _bodyStyle;
    private GUIStyle _resultBodyStyle;
    private GUIStyle _buttonStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<TuringSessionMenuUI>() != null)
        {
            return;
        }

        GameObject go = new GameObject("Turing Session Menu UI");
        go.AddComponent<TuringSessionMenuUI>();
    }

    private void Awake()
    {
        AttachManager();
    }

    private void Update()
    {
        if (gameManager == null)
        {
            AttachManager();
            return;
        }

        HandlePauseInput();

        bool menuVisible = !gameManager.HasGameStarted || _isPaused || (gameManager.HasGameFinished && _showResultPopup);
        IsMenuVisible = menuVisible;
        ApplyTalkInputLock(menuVisible);

        if (menuVisible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable()
    {
        IsMenuVisible = false;
        SetPaused(false);
        ApplyTalkInputLock(false);
        DetachManager();
    }

    private void OnGUI()
    {
        if (gameManager == null)
        {
            return;
        }

        EnsureStyles();

        if (!gameManager.HasGameStarted)
        {
            DrawStartPopup();
            return;
        }

        if (gameManager.HasGameFinished && _showResultPopup)
        {
            DrawResultPopup();
            return;
        }

        if (_isPaused)
        {
            DrawPausePopup();
            return;
        }

        DrawInGameButtons();
    }

    private void AttachManager()
    {
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<TuringGameManager>();
        }

        if (gameManager == null || _subscribed)
        {
            return;
        }

        gameManager.GameFinished += HandleGameFinished;
        _subscribed = true;
    }

    private void DetachManager()
    {
        if (gameManager == null || !_subscribed)
        {
            return;
        }

        gameManager.GameFinished -= HandleGameFinished;
        _subscribed = false;
    }

    private void HandleGameFinished(bool playerWon, string outcomeText)
    {
        SetPaused(false);
        _didPlayerWin = playerWon;
        _resultText = string.IsNullOrWhiteSpace(outcomeText) ? (playerWon ? "You win." : "You lose.") : outcomeText;
        _resultScroll = Vector2.zero;
        _showResultPopup = true;
    }

    private void DrawStartPopup()
    {
        float width = 520f;
        float height = 280f;
        Rect panelRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(panelRect, GUIContent.none, _panelStyle);

        GUILayout.BeginArea(new Rect(panelRect.x + 24f, panelRect.y + 22f, panelRect.width - 48f, panelRect.height - 44f));
        GUILayout.Label("Reverse Turing Test", _titleStyle);
        GUILayout.Space(8f);
        GUILayout.Label("Press Start Game when you are ready. The session will keep the HUD hidden until then.", _bodyStyle);
        GUILayout.Space(24f);
        if (GUILayout.Button("Start Game", _buttonStyle, GUILayout.Height(46f)))
        {
            gameManager.BeginGameSession();
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Quit", _buttonStyle, GUILayout.Height(42f)))
        {
            PrepareForSceneTransition();
            gameManager.RequestQuitGame();
        }

        GUILayout.EndArea();
    }

    private void DrawInGameButtons()
    {
        Rect pauseRect = new Rect(Screen.width - 272f, 16f, 124f, 44f);
        if (GUI.Button(pauseRect, "Pause", _buttonStyle))
        {
            SetPaused(true);
        }

        Rect quitRect = new Rect(Screen.width - 140f, 16f, 124f, 44f);
        if (GUI.Button(quitRect, "Quit", _buttonStyle))
        {
            PrepareForSceneTransition();
            gameManager.RequestQuitGame();
        }
    }

    private void DrawPausePopup()
    {
        float width = 460f;
        float height = 240f;
        Rect panelRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(panelRect, GUIContent.none, _panelStyle);

        GUILayout.BeginArea(new Rect(panelRect.x + 24f, panelRect.y + 22f, panelRect.width - 48f, panelRect.height - 44f));
        GUILayout.Label("Paused", _titleStyle);
        GUILayout.Space(8f);
        GUILayout.Label("Press controller menu button again, or choose an option.", _bodyStyle);
        GUILayout.Space(24f);
        if (GUILayout.Button("Resume", _buttonStyle, GUILayout.Height(46f)))
        {
            SetPaused(false);
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Quit", _buttonStyle, GUILayout.Height(42f)))
        {
            PrepareForSceneTransition();
            gameManager.RequestQuitGame();
        }

        GUILayout.EndArea();
    }

    private void DrawResultPopup()
    {
        float width = 640f;
        float height = 420f;
        Rect panelRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(panelRect, GUIContent.none, _panelStyle);

        string title = _didPlayerWin ? "You Won" : "You Lost";

        GUILayout.BeginArea(new Rect(panelRect.x + 24f, panelRect.y + 22f, panelRect.width - 48f, panelRect.height - 44f));
        GUILayout.Label(title, _titleStyle);
        GUILayout.Space(8f);
        _resultScroll = GUILayout.BeginScrollView(_resultScroll, GUILayout.ExpandHeight(true));
        GUILayout.Label(_resultText, _resultBodyStyle);
        GUILayout.EndScrollView();
        GUILayout.Space(24f);
        if (GUILayout.Button("Try Again", _buttonStyle, GUILayout.Height(46f)))
        {
            PrepareForSceneTransition();
            gameManager.RequestRetryGame();
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Quit", _buttonStyle, GUILayout.Height(42f)))
        {
            PrepareForSceneTransition();
            gameManager.RequestQuitGame();
        }

        GUILayout.EndArea();
    }

    private void HandlePauseInput()
    {
        if (gameManager == null || !gameManager.HasGameStarted || gameManager.HasGameFinished)
        {
            _pauseButtonHeldLastFrame = false;
            return;
        }

        bool pauseButtonHeld = IsControllerPauseHeld();
        bool pauseButtonPressedThisFrame = pauseButtonHeld && !_pauseButtonHeldLastFrame;
        _pauseButtonHeldLastFrame = pauseButtonHeld;

        bool keyboardPausePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
        if (!keyboardPausePressed && !pauseButtonPressedThisFrame)
        {
            return;
        }

        SetPaused(!_isPaused);
    }

    private void SetPaused(bool paused)
    {
        if (_isPaused == paused)
        {
            return;
        }

        _isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
    }

    private void PrepareForSceneTransition()
    {
        SetPaused(false);
        _showResultPopup = false;
        _pauseButtonHeldLastFrame = false;
        IsMenuVisible = false;
        ApplyTalkInputLock(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ApplyTalkInputLock(bool lockTalkInput)
    {
        ConvaiInputManager inputManager = ConvaiInputManager.Instance;
        if (inputManager == null)
        {
            return;
        }

        if (lockTalkInput && !_talkInputLocked)
        {
            inputManager.talkKeyInteract?.Invoke(false);
        }

#if ENABLE_INPUT_SYSTEM
        var talkAction = inputManager.GetTalkKeyAction();
        if (talkAction != null)
        {
            if (lockTalkInput)
            {
                if (talkAction.enabled)
                {
                    talkAction.Disable();
                }
            }
            else
            {
                if (!talkAction.enabled)
                {
                    talkAction.Enable();
                }
            }
        }
#endif

        _talkInputLocked = lockTalkInput;
    }

    private static bool IsControllerPauseHeld()
    {
        return IsPauseHeldOnNode(XRNode.LeftHand) || IsPauseHeldOnNode(XRNode.RightHand);
    }

    private static bool IsPauseHeldOnNode(XRNode node)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
        {
            return false;
        }

        bool menuHeld = device.TryGetFeatureValue(CommonUsages.menuButton, out bool menuButton) && menuButton;
        bool secondaryHeld = device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondaryButton) && secondaryButton;
        bool stickClickHeld = device.TryGetFeatureValue(CommonUsages.secondary2DAxisClick, out bool stickClick) && stickClick;
        return menuHeld || secondaryHeld || stickClickHeld;
    }

    private void EnsureStyles()
    {
        if (_panelStyle == null)
        {
            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.padding = new RectOffset(12, 12, 12, 12);
        }

        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.fontSize = 24;
            _titleStyle.alignment = TextAnchor.MiddleCenter;
            _titleStyle.wordWrap = true;
            _titleStyle.normal.textColor = Color.white;
        }

        if (_bodyStyle == null)
        {
            _bodyStyle = new GUIStyle(GUI.skin.label);
            _bodyStyle.fontSize = 16;
            _bodyStyle.alignment = TextAnchor.UpperCenter;
            _bodyStyle.wordWrap = true;
            _bodyStyle.normal.textColor = new Color(0.92f, 0.95f, 1f, 1f);
        }

        if (_resultBodyStyle == null)
        {
            _resultBodyStyle = new GUIStyle(_bodyStyle);
            _resultBodyStyle.alignment = TextAnchor.UpperLeft;
            _resultBodyStyle.fontSize = 15;
            _resultBodyStyle.wordWrap = true;
        }

        if (_buttonStyle == null)
        {
            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = 18;
        }
    }
}