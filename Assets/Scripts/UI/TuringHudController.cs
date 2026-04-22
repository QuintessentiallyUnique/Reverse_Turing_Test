using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TuringHudController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TuringGameManager gameManager;
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text identityText;
    [SerializeField] private Image roundProgressFill;

    [Header("Style")]
    [SerializeField] private Color defaultInstructionColor = new Color(0.88f, 0.93f, 1f, 1f);
    [SerializeField] private Color talkPromptColor = new Color(1f, 0.86f, 0.45f, 1f);

    [Header("Animation")]
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private float pulseSpeed = 2.25f;

    private float _targetAlpha = 1f;

    public void Configure(
        TuringGameManager manager,
        CanvasGroup canvasGroup,
        TMP_Text phaseLabel,
        TMP_Text roundLabel,
        TMP_Text turnLabel,
        TMP_Text instructionLabel,
        TMP_Text identityLabel,
        Image progressFill)
    {
        gameManager = manager;
        rootCanvasGroup = canvasGroup;
        phaseText = phaseLabel;
        roundText = roundLabel;
        turnText = turnLabel;
        instructionText = instructionLabel;
        identityText = identityLabel;
        roundProgressFill = progressFill;
    }

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<TuringGameManager>();
        }

        if (rootCanvasGroup == null)
        {
            rootCanvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        if (gameManager != null)
        {
            gameManager.TurnHudStateChanged += HandleHudStateChanged;
        }
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.TurnHudStateChanged -= HandleHudStateChanged;
        }
    }

    private void Update()
    {
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = Mathf.MoveTowards(rootCanvasGroup.alpha, _targetAlpha, fadeSpeed * Time.deltaTime);
        }

        if (instructionText == null)
        {
            return;
        }

        if (ContainsHoldToTalk(instructionText.text))
        {
            float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            instructionText.color = Color.Lerp(defaultInstructionColor, talkPromptColor, t);
            return;
        }

        instructionText.color = defaultInstructionColor;
    }

    public void SetVisible(bool visible)
    {
        _targetAlpha = visible ? 1f : 0f;
    }

    private void HandleHudStateChanged(TuringGameManager.TurnHudState state)
    {
        if (phaseText != null)
        {
            phaseText.text = string.IsNullOrWhiteSpace(state.phase) ? "Round" : state.phase;
        }

        if (roundText != null)
        {
            if (state.totalRounds > 0 && state.currentRound > 0)
            {
                roundText.text = $"Round {state.currentRound}/{state.totalRounds}";
            }
            else
            {
                roundText.text = "Round -";
            }
        }

        if (turnText != null)
        {
            turnText.text = string.IsNullOrWhiteSpace(state.turnLabel) ? "Waiting..." : state.turnLabel;
        }

        if (instructionText != null)
        {
            instructionText.text = string.IsNullOrWhiteSpace(state.instruction)
                ? "Waiting for the next action."
                : state.instruction;
        }

        if (identityText != null)
        {
            identityText.text = $"Identity: {state.playerIdentity}";
        }

        if (roundProgressFill != null)
        {
            float fillAmount = state.totalRounds <= 0 ? 0f : Mathf.Clamp01((float)state.currentRound / state.totalRounds);
            roundProgressFill.fillAmount = fillAmount;
        }
    }

    private static bool ContainsHoldToTalk(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.ToLowerInvariant();
        if (!normalized.Contains("hold"))
        {
            return false;
        }

        return normalized.Contains("talk")
               || normalized.Contains("button")
               || normalized.Contains("trigger")
               || normalized.Contains(" t");
    }
}
