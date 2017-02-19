using UnityEditor;
using UnityEngine;
using System.Collections;

namespace TButt.Editor
{
    [CustomEditor(typeof(TBScreenshotCamera))]
    public class TBScreenshotCameraEditor : UnityEditor.Editor
    {
        RenderTexture _renderTexture;

        public override void OnInspectorGUI()
        {
            TBScreenshotCamera screenshotCamera = (TBScreenshotCamera)target;

            TBEditorExtensions.ShowHeaderImage(TBEditorExtensions.turboButtonHeader);

            #region POSITION / ROTATION SETTINGS
            // If the game is running, show options for adjusting position and rotation based on TBCenter.
            if (Application.isPlaying)
            {
                // If the TBScreenshotCamera GameObject is disabled, show a message.
                if (!screenshotCamera.gameObject.activeSelf)
                {
                    EditorGUILayout.HelpBox("Enable the TBScreenshotCamera GameObject to access screenshot options.", UnityEditor.MessageType.Error);

                    GUI.backgroundColor = TBEditorExtensions.color_TButtGold;
                    if (GUILayout.Button("Enable TBScreenshotCamera", GUILayout.MinHeight(60)))
                    {
                        screenshotCamera.gameObject.SetActive(true);
                    }
                    return;
                }

                // Position and rotation settings.
                EditorGUILayout.LabelField("Camera Alignment", EditorStyles.boldLabel);
                if (!screenshotCamera.IsParented())
                {
                    if (GUILayout.Button("Attach to TBCenter"))
                    {
                        screenshotCamera.ToggleParenting();
                    }

                    if (GUILayout.Button("Reset Position"))
                    {
                        screenshotCamera.MatchVRCameraPosition();
                    }

                    if (GUILayout.Button("Reset Orientation"))
                    {
                        screenshotCamera.MatchVRCameraOrientation();
                    }
                }
                else
                {
                    if (GUILayout.Button("Detach from TBCenter"))
                    {
                        screenshotCamera.ToggleParenting();
                    }
                }

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider); // Acts s a horizontal section / line break.
            }
            #endregion

            #region RESOLUTION SETTINGS

            EditorGUILayout.LabelField("Resolution", EditorStyles.boldLabel);
            screenshotCamera.resWidth = EditorGUILayout.IntField("Width", screenshotCamera.resWidth);
            screenshotCamera.resHeight = EditorGUILayout.IntField("Height", screenshotCamera.resHeight);

            if (GUILayout.Button("Set to 4K"))
            {
                screenshotCamera.resWidth = 3840;
                screenshotCamera.resHeight = 2160;
            }

            if (GUILayout.Button("Set to 1440p"))
            {
                screenshotCamera.resWidth = 2560;
                screenshotCamera.resHeight = 1440;
            }

            if (GUILayout.Button("Set to 1080p"))
            {
                screenshotCamera.resWidth = 1920;
                screenshotCamera.resHeight = 1080;
            }

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider); // Acts s a horizontal section / line break.
            #endregion

            #region SAVE PATH SETTINGS
            // Save path location selection.
            GUILayout.Label("Save Location", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            screenshotCamera.savePath = EditorGUILayout.TextField(screenshotCamera.savePath, GUILayout.ExpandWidth(false));
            if (GUILayout.Button("Browse...", GUILayout.ExpandWidth(false)))
                screenshotCamera.savePath = EditorUtility.SaveFolderPanel("Choose a path to save your screenshot", screenshotCamera.savePath, Application.dataPath);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);  // Acts s a horizontal section / line break.

            #endregion

            #region MISC SETTINGS
            // Filename Prefix
            GUILayout.Label("Filename Prefix", EditorStyles.boldLabel);
            screenshotCamera.prefix = EditorGUILayout.TextField(screenshotCamera.prefix, GUILayout.ExpandWidth(false));

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);  // Acts s a horizontal section / line break.

            GUILayout.Label("Misc Settings", EditorStyles.boldLabel);
            screenshotCamera.openAfterSaving = (bool)EditorGUILayout.Toggle("View after saving", screenshotCamera.openAfterSaving);
            screenshotCamera.forceMSAA = (bool)EditorGUILayout.Toggle("Use 8xMSAA", screenshotCamera.forceMSAA);
            screenshotCamera.hotkey = EditorGUILayout.BeginToggleGroup("Enable hotkey", screenshotCamera.hotkey);
            screenshotCamera.hotkeyKeycode = (KeyCode)EditorGUILayout.EnumPopup("Screenshot Hotkey", screenshotCamera.hotkeyKeycode, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndToggleGroup();

            if(screenshotCamera.hideScreenshotObjects)
            {
                if (GUILayout.Button("Show Screenshot Objects"))
                {
                    screenshotCamera.ToggleScreenshotObjects(screenshotCamera.hideScreenshotObjects);
                }
            }
            else
            {
                if (GUILayout.Button("Hide Screenshot Objects"))
                {
                    screenshotCamera.ToggleScreenshotObjects(screenshotCamera.hideScreenshotObjects);
                }
            }
          
            #endregion

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);  // Acts s a horizontal section / line break.

            // The "Take Screenshot" button.
            GUI.backgroundColor = GUI.backgroundColor = TBEditorExtensions.color_TButtGold;
            if (GUILayout.Button("Take Screenshot", GUILayout.MinHeight(60)))
            {
                if (screenshotCamera.savePath == "")
                    Debug.LogError("TBScreenshotCamera: You must choose a Save Location before taking a screenshot.");
                else
                    screenshotCamera.TakeScreenshot();
            }
        }
    }
}