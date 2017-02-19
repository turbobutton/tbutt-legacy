using UnityEditor;
using UnityEngine;
using System.Collections;

namespace TButt.Editor
{
    [CustomEditor(typeof(TBCameraRig))]
    public class TBCameraRigEditor : UnityEditor.Editor
    {
        RenderTexture _renderTexture;

        public override void OnInspectorGUI()
        {
           TBEditorExtensions. ShowHeaderImage(TBEditorExtensions.turboButtonHeader);

           base.OnInspectorGUI();

            TBCameraRig cameraRig = (TBCameraRig)target;
     
            // If the game is running, show a button to quickly access TBScreenshotCamera.
            if (Application.isPlaying)
            {
                // If the TBScreenshotCamera GameObject is disabled, show a message.
                if (cameraRig.TBScreenshotCamera != null)
                {
                    GUI.backgroundColor = TBEditorExtensions.color_TButtGold;
                    if (GUILayout.Button("Select TBScreenshotCamera", GUILayout.MinHeight(50)))
                    {
                        Selection.activeObject = cameraRig.TBScreenshotCamera.gameObject;
                    }
                    return;
                }
            }
        }
    }
}