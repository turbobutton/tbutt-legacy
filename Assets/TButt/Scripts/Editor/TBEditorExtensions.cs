using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

namespace TButt.Editor
{
    public static class TBEditorExtensions
    {
        // Textures
        public const string TButtFolderPath = "TButt";
        public static Texture turboButtonHeader = EditorGUIUtility.LoadRequired(string.Format("{0}/inspector_header_tbutt.png", TButtFolderPath)) as Texture;

        // Colors
        public static readonly Color color_TButtGold = new Color(1f, 189 / 255f, 74 / 255f);

        static List<int> layerNumbers = new List<int>();

        public static void ShowHeaderImage(Texture tex)
        {
            var rect = GUILayoutUtility.GetRect(0f, 0f);
            rect.width = tex.width;
            rect.height = tex.height;
            GUILayout.Space(rect.height);
            GUI.DrawTexture(rect, tex);
        }

        public static void ResetColors()
        {
            GUI.backgroundColor = Color.white;
        }

        public static void BeginGroupedControls()
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.BeginHorizontal("AS TextArea", GUILayout.MinHeight(10f));
            GUILayout.BeginVertical();
            GUILayout.Space(2f);
        }

        public static void EndGroupedControls()
        {
            GUILayout.Space(3f);
            GUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3f);
            GUILayout.EndHorizontal();

            GUILayout.Space(3f);
        }
    }
}