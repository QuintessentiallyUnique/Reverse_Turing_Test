using UnityEditor;
using UnityEngine;

namespace Convai.Scripts.Editor.Tutorial
{
    [InitializeOnLoad]
    public static class UASNewVersionMigration
    {
        private const string MigrationNoticeShownKey = "Convai.UASNewVersionMigrationNoticeShownThisSession";
        private static bool s_HasProcessedThisSession;

        static UASNewVersionMigration()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (s_HasProcessedThisSession)
            {
                EditorApplication.update -= OnEditorUpdate;
                return;
            }

            if (SessionState.GetBool(MigrationNoticeShownKey, false))
            {
                s_HasProcessedThisSession = true;
                EditorApplication.update -= OnEditorUpdate;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            s_HasProcessedThisSession = true;
            SessionState.SetBool(MigrationNoticeShownKey, true);
            EditorApplication.delayCall += UASNewVersionMigrationWindow.ShowWindow;
            EditorApplication.update -= OnEditorUpdate;
        }

        [MenuItem("Tutorials/Show Version Update Notice")]
        private static void ShowVersionUpdateNotice()
        {
            UASNewVersionMigrationWindow.ShowWindow(); 
        }   
    }

    public sealed class UASNewVersionMigrationWindow : EditorWindow
    {
        private const string WindowTitle = "Convai Major Version Upgrade";
        private const string MigrationGuideUrl = "https://docs.convai.com/api-docs/plugins-and-integrations/unity-plugin-beta-overview/migration-guide";
        private const string HeaderImagePath = "Assets/Convai/Tutorials/Images/ImageWelcomeScreen.png";
        private static readonly Vector2 WindowSize = new(770f, 480f);
        private Texture2D m_HeaderImage;

        internal static void ShowWindow()
        {
            UASNewVersionMigrationWindow window = CreateInstance<UASNewVersionMigrationWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = WindowSize;
            window.maxSize = WindowSize;
            window.position = GetCenteredRect();
            window.ShowUtility();
            window.Focus();
        }

        private static Rect GetCenteredRect()
        {
            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
            float centeredX = mainWindow.x + (mainWindow.width - WindowSize.x) * 0.5f;
            float centeredY = mainWindow.y + (mainWindow.height - WindowSize.y) * 0.5f;
            return new Rect(centeredX, centeredY, WindowSize.x, WindowSize.y);
        }

        private void OnEnable()
        {
            m_HeaderImage = AssetDatabase.LoadAssetAtPath<Texture2D>(HeaderImagePath);
        }

        private void OnGUI()
        {
            GUIStyle headerTitleStyle = new(EditorStyles.boldLabel)
            {
                fontSize = 22,
                wordWrap = true
            };
            GUIStyle sectionHeaderStyle = new(EditorStyles.boldLabel)
            {
                fontSize = 17,
                richText = true,
                wordWrap = true,
                padding = new RectOffset(10, 0, 0, 0),
                normal = new GUIStyleState { textColor = Color.white }
            };
            GUIStyle topicDescriptionStyle = new(EditorStyles.wordWrappedLabel)
            {
                fontSize = 14,
                padding = new RectOffset(10, 0, 0, 0),
                richText = true,
                wordWrap = true
            };
            GUIStyle warningTitleStyle = new(EditorStyles.boldLabel)
            {
                fontSize = 16,
                richText = true,
                wordWrap = true,
                normal = new GUIStyleState { textColor = new Color(1f, 0.85f, 0.35f) }
            };
            GUIStyle warningTextStyle = new(EditorStyles.wordWrappedLabel)
            {
                fontSize = 14,
                richText = true,
                wordWrap = true,
                normal = new GUIStyleState { textColor = new Color(1f, 0.85f, 0.35f) }
            };
            GUIStyle warningBoxStyle = new(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10)
            };
            GUIStyle footerStyle = new(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(0, 0, 0, 0)
            };
            GUIStyle actionButtonStyle = new(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(12, 12, 8, 8),
                margin = new RectOffset(4, 4, 2, 2)
            };

            if (m_HeaderImage != null)
            {
                Rect imageRect = GUILayoutUtility.GetRect(position.width - 24f, 160f, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(imageRect, m_HeaderImage, ScaleMode.ScaleAndCrop);
            }

            GUILayout.Space(10f);
            EditorGUILayout.LabelField("Convai SDK Upgrade Notice", headerTitleStyle);
            GUILayout.Space(8f);

            EditorGUILayout.BeginVertical(warningBoxStyle);
            EditorGUILayout.LabelField("<b>Important Migration Notice</b>", warningTitleStyle);
            GUILayout.Space(3f);
            EditorGUILayout.LabelField(
                "We are introducing a <b>major SDK update</b> that includes architectural improvements and new features. These changes are designed to provide better performance, improved stability, and easier future updates.",
                warningTextStyle);
            EditorGUILayout.EndVertical();

            GUILayout.Space(10f);
            EditorGUILayout.LabelField("<color=#FFFFFF>What To Expect</color>", sectionHeaderStyle);
            GUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "This update introduces <b>changes to the SDK architecture and APIs</b>.\n"
+"If you are currently using an <b>older version of the Convai SDK</b>, you will need to <b>migrate your project</b> to continue using the latest features and updates.",
                topicDescriptionStyle);
            GUILayout.Space(3f);
            EditorGUILayout.LabelField(
                "A detailed <b>migration guide</b> will be available when the latest version is released on the Unity Asset Store.",                topicDescriptionStyle);
            GUILayout.Space(10f);
            EditorGUILayout.BeginVertical(footerStyle);
            EditorGUILayout.BeginHorizontal();

            bool hasMigrationGuide = !string.IsNullOrWhiteSpace(MigrationGuideUrl);
            using (new EditorGUI.DisabledScope(!hasMigrationGuide))
            {
                if (DrawColoredButton("Open Migration Guide", new Color(0.2f, 0.7f, 0.95f), 36f, actionButtonStyle))
                {
                    Application.OpenURL(MigrationGuideUrl);
                    Close();
                }
            }

            if (DrawColoredButton("Close", new Color(0.45f, 0.45f, 0.45f), 36f, actionButtonStyle))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            if (!hasMigrationGuide)
            {
                GUILayout.Space(6f);
                EditorGUILayout.HelpBox("Migration guide will be available when the new Asset Store version is released.", MessageType.Info);
            }
        }

        private static bool DrawColoredButton(string text, Color backgroundColor, float height, GUIStyle style)
        {
            Color oldBackgroundColor = GUI.backgroundColor;
            Color oldContentColor = GUI.contentColor;
            GUI.backgroundColor = backgroundColor;
            GUI.contentColor = Color.white;
            bool clicked = GUILayout.Button(text, style, GUILayout.Height(height), GUILayout.ExpandWidth(true));
            GUI.backgroundColor = oldBackgroundColor;
            GUI.contentColor = oldContentColor;
            return clicked;
        }
    }
}
