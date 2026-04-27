using System.Collections;
using Convai.Scripts.Runtime.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TuringVrSubtitleUI : MonoBehaviour, IChatUI
{
    private const int CharacterLimit = 280;
    private const float HoldSeconds = 6f;
    private const float FadeSeconds = 0.6f;

    private Camera _attachedCamera;
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private TMP_Text _speakerText;
    private TMP_Text _bodyText;
    private Coroutine _fadeRoutine;

    private float _distance = 1.1f;
    private float _verticalOffset = -0.32f;
    private float _scale = 0.0015f;
    private Vector2 _panelSize = new Vector2(1400f, 320f);

    public void Configure(Camera camera, float distance, float verticalOffset, float scale, Vector2 panelSize)
    {
        _attachedCamera = camera;
        _distance = distance;
        _verticalOffset = verticalOffset;
        _scale = scale;
        _panelSize = panelSize;

        EnsureBuilt();
        ApplyTransform();
    }

    public void Initialize(GameObject uiPrefab)
    {
        EnsureBuilt();
    }

    public void ActivateUI()
    {
        EnsureBuilt();
        gameObject.SetActive(true);
    }

    public void DeactivateUI()
    {
        gameObject.SetActive(false);
    }

    public void SendCharacterText(string charName, string text, Color characterTextColor)
    {
        SetSubtitle(charName, text, characterTextColor);
    }

    public void SendPlayerText(string playerName, string text, Color playerTextColor)
    {
        SetSubtitle(playerName, text, playerTextColor);
    }

    public CanvasGroup GetCanvasGroup()
    {
        EnsureBuilt();
        return _canvasGroup;
    }

    private void LateUpdate()
    {
        if (_attachedCamera != null && transform.parent != _attachedCamera.transform)
        {
            ApplyTransform();
        }
    }

    private void EnsureBuilt()
    {
        if (_canvas != null)
        {
            return;
        }

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 100;
        gameObject.AddComponent<CanvasScaler>();
        GraphicRaycaster raycaster = gameObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0f;

        RectTransform rt = (RectTransform)transform;
        rt.sizeDelta = _panelSize;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(transform, false);
        RectTransform bgRect = (RectTransform)background.transform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        Image bgImage = background.GetComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.65f);
        bgImage.raycastTarget = false;

        GameObject speaker = new GameObject("Speaker", typeof(RectTransform));
        speaker.transform.SetParent(transform, false);
        RectTransform speakerRect = (RectTransform)speaker.transform;
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.pivot = new Vector2(0.5f, 1f);
        speakerRect.offsetMin = new Vector2(28f, -72f);
        speakerRect.offsetMax = new Vector2(-28f, -12f);
        _speakerText = speaker.AddComponent<TextMeshProUGUI>();
        _speakerText.alignment = TextAlignmentOptions.Left;
        _speakerText.fontSize = 38f;
        _speakerText.fontStyle = FontStyles.Bold;
        _speakerText.textWrappingMode = TextWrappingModes.NoWrap;
        _speakerText.overflowMode = TextOverflowModes.Ellipsis;
        _speakerText.raycastTarget = false;
        _speakerText.text = string.Empty;

        GameObject body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(transform, false);
        RectTransform bodyRect = (RectTransform)body.transform;
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.pivot = new Vector2(0.5f, 0.5f);
        bodyRect.offsetMin = new Vector2(28f, 20f);
        bodyRect.offsetMax = new Vector2(-28f, -76f);
        _bodyText = body.AddComponent<TextMeshProUGUI>();
        _bodyText.alignment = TextAlignmentOptions.TopLeft;
        _bodyText.fontSize = 46f;
        _bodyText.textWrappingMode = TextWrappingModes.Normal;
        _bodyText.overflowMode = TextOverflowModes.Ellipsis;
        _bodyText.raycastTarget = false;
        _bodyText.text = string.Empty;
    }

    private void ApplyTransform()
    {
        if (_attachedCamera == null)
        {
            return;
        }

        Transform t = transform;
        t.SetParent(_attachedCamera.transform, false);
        t.localPosition = new Vector3(0f, _verticalOffset, _distance);
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one * _scale;

        if (_canvas != null)
        {
            _canvas.worldCamera = _attachedCamera;
        }

        RectTransform rt = (RectTransform)transform;
        rt.sizeDelta = _panelSize;
    }

    private void SetSubtitle(string speaker, string text, Color color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        EnsureBuilt();

        string trimmed = text.Length > CharacterLimit ? text.Substring(text.Length - CharacterLimit) : text;
        string colorHex = ColorUtility.ToHtmlStringRGB(color);
        _speakerText.text = $"<color=#{colorHex}>{speaker}</color>";
        _bodyText.text = trimmed;

        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
        }

        _canvasGroup.alpha = 1f;
        _fadeRoutine = StartCoroutine(FadeOutAfterDelay());
    }

    private IEnumerator FadeOutAfterDelay()
    {
        yield return new WaitForSeconds(HoldSeconds);

        float elapsed = 0f;
        while (elapsed < FadeSeconds)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / FadeSeconds);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }
}
