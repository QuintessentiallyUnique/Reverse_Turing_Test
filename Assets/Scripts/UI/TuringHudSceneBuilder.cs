using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public static class TuringHudSceneBuilder
{
    private const string CanvasName = "Turing HUD Canvas";

#if UNITY_EDITOR
    [MenuItem("Tools/Reverse Turing/Build HUD Canvas In Active Scene")]
    public static void BuildHudCanvasInActiveScene()
    {
        TuringGameManager gameManager = Object.FindFirstObjectByType<TuringGameManager>();
        BuildOrUpdateHud(gameManager);

        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
#endif

    public static TuringHudController BuildOrUpdateHud(TuringGameManager gameManager)
    {
        GameObject canvasObject = GameObject.Find(CanvasName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform panel = GetOrCreateChildRect(canvasObject.transform, "Panel");
        SetAnchor(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(560f, 220f));
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage == null)
        {
            panelImage = panel.gameObject.AddComponent<Image>();
        }
        panelImage.color = new Color(0.06f, 0.09f, 0.13f, 0.86f);

        RectTransform phaseTextRect = GetOrCreateChildRect(panel, "PhaseText");
        SetAnchor(phaseTextRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -16f), new Vector2(180f, 28f));
        TextMeshProUGUI phaseText = GetOrCreateTmpText(phaseTextRect, "PHASE", 16, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);

        RectTransform roundTextRect = GetOrCreateChildRect(panel, "RoundText");
        SetAnchor(roundTextRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-190f, -16f), new Vector2(170f, 28f));
        TextMeshProUGUI roundText = GetOrCreateTmpText(roundTextRect, "Round -", 15, FontStyles.Bold, new Color(0.73f, 0.88f, 1f, 1f), TextAlignmentOptions.Right);

        RectTransform turnTextRect = GetOrCreateChildRect(panel, "TurnText");
        SetAnchor(turnTextRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -52f), new Vector2(524f, 34f));
        TextMeshProUGUI turnText = GetOrCreateTmpText(turnTextRect, "Waiting...", 22, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);

        RectTransform instructionRect = GetOrCreateChildRect(panel, "InstructionText");
        SetAnchor(instructionRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -95f), new Vector2(524f, 70f));
        TextMeshProUGUI instructionText = GetOrCreateTmpText(instructionRect, "Waiting for the next action.", 18, FontStyles.Normal, new Color(0.88f, 0.93f, 1f, 1f), TextAlignmentOptions.TopLeft);
        instructionText.textWrappingMode = TextWrappingModes.Normal;

        RectTransform identityRect = GetOrCreateChildRect(panel, "IdentityText");
        SetAnchor(identityRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 12f), new Vector2(320f, 24f));
        TextMeshProUGUI identityText = GetOrCreateTmpText(identityRect, "Identity: Player", 15, FontStyles.Italic, new Color(0.72f, 0.84f, 0.98f, 1f), TextAlignmentOptions.Left);

        RectTransform progressBgRect = GetOrCreateChildRect(panel, "RoundProgressBackground");
        SetAnchor(progressBgRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-190f, 14f), new Vector2(170f, 16f));
        Image progressBg = progressBgRect.GetComponent<Image>();
        if (progressBg == null)
        {
            progressBg = progressBgRect.gameObject.AddComponent<Image>();
        }
        progressBg.color = new Color(1f, 1f, 1f, 0.18f);

        RectTransform progressFillRect = GetOrCreateChildRect(progressBgRect, "RoundProgressFill");
        SetAnchor(progressFillRect, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        Image progressFill = progressFillRect.GetComponent<Image>();
        if (progressFill == null)
        {
            progressFill = progressFillRect.gameObject.AddComponent<Image>();
        }
        progressFill.color = new Color(0.21f, 0.77f, 1f, 1f);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;

        TuringHudController hudController = canvasObject.GetComponent<TuringHudController>();
        if (hudController == null)
        {
            hudController = canvasObject.AddComponent<TuringHudController>();
        }

        hudController.Configure(gameManager, canvasGroup, phaseText, roundText, turnText, instructionText, identityText, progressFill);

        if (gameManager != null)
        {
            gameManager.showTurnOverlay = false;
        }

        return hudController;
    }

    private static RectTransform GetOrCreateChildRect(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            return child as RectTransform;
        }

        GameObject childObject = new GameObject(name, typeof(RectTransform));
        childObject.transform.SetParent(parent, false);
        return childObject.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI GetOrCreateTmpText(RectTransform target, string textValue, int fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI tmp = target.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            tmp = target.gameObject.AddComponent<TextMeshProUGUI>();
        }

        tmp.text = textValue;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void SetAnchor(RectTransform target, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        target.anchorMin = anchorMin;
        target.anchorMax = anchorMax;
        target.pivot = new Vector2(anchorMin.x, anchorMin.y);
        target.anchoredPosition = anchoredPosition;
        target.sizeDelta = sizeDelta;
    }
}
