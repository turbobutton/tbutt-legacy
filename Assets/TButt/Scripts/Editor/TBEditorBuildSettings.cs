using UnityEngine;
using UnityEditor;
using System.Collections;
using System.IO;
using TButt;

namespace TButt.Editor
{
    [InitializeOnLoad]
    public class TBEditorBuildSettings : EditorWindow
    {
        // Styles
        static GUIStyle sectionHeader = new GUIStyle();
        static GUIStyle sectionBox = new GUIStyle();
        static GUIStyle largeButton = new GUIStyle();

        // Toolbar stuff
        static Vector2 scrollPos;
        static EditorWindow window;
        static bool canSave = true;
        static Color stdBGColor = Color.gray;

        #region PATH SETTINGS
        static string buildDefString = "";
        static string oldBuildDefString = "";
        static string customDefs = "";
        static string _cardboardPath = "/Manifests/Cardboard/AndroidManifest.xml";
        static string _daydreamPath = "/Manifests/Daydream/AndroidManifest.xml";
        static string _OVRPath = "/Manifests/GearVR/AndroidManifest.xml";
        static string _OVRReleasePath = "/Manifests/GearVRRelease/AndroidManifest.xml";
        static string _manifestPath = "/Plugins/Android/AndroidManifest.xml";
        #endregion

        // Other settings
        static DaydreamControlSettings daydreamControlSettings = DaydreamControlSettings.Native;
        static VRPlatforms platform;

        static bool debug;
        static bool release;
        static bool demo;

        static bool swapManifest;

        static bool isCompiling = false;

        [MenuItem("Turbo Button/Build Settings...", false, 10000)]
        public static void ShowWindow()
        {
            buildDefString = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup);
            oldBuildDefString = buildDefString;
            window = EditorWindow.GetWindow(typeof(TBEditorBuildSettings), true, "Editor Build Settings", true);
            ReadCurrentSettings();
        }

        void OnGUI()
        {
            #region STYLES
                // Set styles
            sectionHeader.fontSize = 20;
            sectionHeader.normal.textColor = Color.white;

            sectionBox.contentOffset = new Vector2(10, 10);
            sectionBox.border = new RectOffset(10, 10, 10, 10);

            largeButton.fontSize = 15;
            largeButton.fixedHeight = 20;
            #endregion        

            EditorGUILayout.BeginVertical();

            ShowGeneralMenu();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("Scripting Defines: " + buildDefString, MessageType.Info);

            EditorGUILayout.BeginHorizontal(new GUILayoutOption[1] { GUILayout.Height(70) });
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Save Settings", new GUILayoutOption[1] { GUILayout.Height(40) }))
            {
                SaveBuildSettings();
                window.Close();
            }
         
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Close Without Saving", new GUILayoutOption[1] { GUILayout.Height(40) }))
            {
                window.Close();
            }
            GUI.backgroundColor = stdBGColor;
            EditorGUILayout.EndHorizontal();
        }

        void ShowGeneralMenu()
        {
            EditorGUIUtility.labelWidth = 170;
            GUILayout.BeginVertical(EditorStyles.helpBox);
            // General Controller Settings
            EditorGUILayout.LabelField("Platform Settings", sectionHeader);
            EditorGUILayout.Separator();
            GUILayout.BeginVertical(EditorStyles.helpBox);

            platform = (VRPlatforms)EditorGUILayout.EnumPopup(new GUIContent("Target Platform", "The platform you're developing for."), platform);

            switch(platform)
            {
                case VRPlatforms.Daydream:
                    daydreamControlSettings = (DaydreamControlSettings)EditorGUILayout.EnumPopup(new GUIContent("Daydream Emulation", "If you want to test Daydream on Oculus Touch or Steam VR / Vive, choose those settings here. Otherwise choose Native."), daydreamControlSettings);
                    break;
            }

            switch (platform)
            {
                case VRPlatforms.Daydream:
                case VRPlatforms.GearVR:
                case VRPlatforms.Cardboard:
                    swapManifest = (bool)EditorGUILayout.Toggle(new GUIContent("Swap Manifest", "Swap to a manfiest you've specified for this platform. See PATH SETTINGS in TBEditorBuildPanel.cs."), swapManifest);
                    break;
            }

            GUILayout.EndVertical();

            CheckForSDK();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Build Flavors", sectionHeader);
            EditorGUILayout.Separator();
            GUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Optional tags for specifying features in builds.");
            EditorGUILayout.LabelField("If you don't know what these are, you can probably leave them blank.");
            EditorGUILayout.Space();
            debug = (bool)EditorGUILayout.Toggle("Debug Build", debug);
            demo = (bool)EditorGUILayout.Toggle("Demo Build", demo);
            release = (bool)EditorGUILayout.Toggle("Release Build", release);

            GUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Custom Definitions", sectionHeader);
            EditorGUILayout.Separator();
            GUILayout.BeginVertical(EditorStyles.helpBox);

            customDefs = (string)EditorGUILayout.TextField("Scripting Defines", customDefs);
         
            GUILayout.EndVertical();
            GUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
            {
                RefreshBuildString();
            }
        }

        /// <summary>
        /// Make sure the given SDK is installed.
        /// </summary>
        void CheckForSDK()
        {
            if (platform == VRPlatforms.Rift || platform == VRPlatforms.GearVR)
            {
                string[] assets = AssetDatabase.FindAssets("OVRCameraRig", new string[]{"Assets"});
                if (assets.Length == 0)
                    EditorGUILayout.HelpBox("Oculus Plugin not detected! Please import the Oculus SDK to your project before continuing. See https://developer3.oculus.com/downloads/", UnityEditor.MessageType.Error);
            }
            else if (platform == VRPlatforms.SteamVR)
            {
                string[] assets = AssetDatabase.FindAssets("SteamVR_Camera", new string[] { "Assets" });
                if (assets.Length == 0)
                    EditorGUILayout.HelpBox("Steam VR Plugin not detected. Please install Steam VR from the asset store before continuing.", UnityEditor.MessageType.Error);
            }
            else if (platform == VRPlatforms.Daydream)
            {
                string[] assets = AssetDatabase.FindAssets("GvrSettings", new string[] { "Assets" });
                #if !UNITY_HAS_GOOGLEVR
                EditorGUILayout.HelpBox("Your current version of Unity may not support Daydream. Please use 5.4 GVR13 or 5.6b4 or newer.", UnityEditor.MessageType.Error);
                #endif
                if (assets.Length == 0)
                    EditorGUILayout.HelpBox("Google VR Plugin not detected. Please download and install the plugin before continuing. https://github.com/googlevr/gvr-unity-sdk/", UnityEditor.MessageType.Error);
                if(daydreamControlSettings == DaydreamControlSettings.Rift)
                {
                    string[] assets2 = AssetDatabase.FindAssets("OVRCameraRig", new string[] { "Assets" });
                    if (assets.Length == 0)
                        EditorGUILayout.HelpBox("Oculus Plugin not detected! Please import the Oculus SDK to your project before continuing. See https://developer3.oculus.com/downloads/", UnityEditor.MessageType.Error);
                }
                else if(daydreamControlSettings == DaydreamControlSettings.SteamVR)
                {
                    string[] assets2 = AssetDatabase.FindAssets("SteamVR_Camera", new string[] { "Assets" });
                    if (assets.Length == 0)
                        EditorGUILayout.HelpBox("Steam VR Plugin not detected. Please install Steam VR from the asset store before continuing.", UnityEditor.MessageType.Error);
                }
            }
        }

        // Populate the window based on the current settings.
        static void ReadCurrentSettings()
        {
            // FAKE_DAYDREAM must be checked first, since it can include RIFT or STEAM_VR too.
            if (oldBuildDefString.Contains("FAKE_DAYDREAM"))
            {
                platform = VRPlatforms.Daydream;
                if(oldBuildDefString.Contains("RIFT"))
                    daydreamControlSettings = DaydreamControlSettings.Rift;
                else if (oldBuildDefString.Contains("STEAM_VR"))
                    daydreamControlSettings = DaydreamControlSettings.SteamVR;
            }
            else if (oldBuildDefString.Contains("DAYDREAM"))
            {
                platform = VRPlatforms.Daydream;
            }
            else if (oldBuildDefString.Contains("RIFT"))
            {
                platform = VRPlatforms.Rift;
            }
            else if (oldBuildDefString.Contains("STEAM_VR"))
            {
                platform = VRPlatforms.SteamVR;
            }
            else if (oldBuildDefString.Contains("GEAR_VR"))
            {
                platform = VRPlatforms.GearVR;
            }
            else if (oldBuildDefString.Contains("MORPHEUS"))
            {
                platform = VRPlatforms.PS4;
            }
            else if (oldBuildDefString.Contains("CARDBOARD"))
            {
                platform = VRPlatforms.Cardboard;
            }

            // Other settings
            release = oldBuildDefString.Contains("RELEASE");
            debug = oldBuildDefString.Contains("DEBUGMODE");
            demo = oldBuildDefString.Contains("DEMOMODE");

            // Import custom definitions
            if (oldBuildDefString.Contains("CUSTOMDEFS_;"))
            {
                customDefs = oldBuildDefString.Substring(customDefs.IndexOf("CUSTOMDEFS_;") + 12, oldBuildDefString.Length-1);
            }
        }

        static void RefreshBuildString()
        {
            buildDefString = "";

            // Get initial platform stuff.
            switch(platform)
            {
                case VRPlatforms.Daydream:
                    // Daydream is special because it can be emulated.
                    switch (daydreamControlSettings)
                    {
                        case DaydreamControlSettings.Native:
                            buildDefString += "DAYDREAM";
                            break;
                        case DaydreamControlSettings.Rift:
                            buildDefString += "FAKE_DAYDREAM;RIFT";
                            break;
                        case DaydreamControlSettings.SteamVR:
                            buildDefString += "FAKE_DAYDREAM;STEAM_VR";
                            break;
                    }
                    break;
                case VRPlatforms.Cardboard:
                    buildDefString += "CARDBOARD";
                    break;
                case VRPlatforms.Rift:
                    buildDefString += "RIFT";
                    break;
                case VRPlatforms.SteamVR:
                    buildDefString += "STEAM_VR";
                    break;
                case VRPlatforms.PS4:
                    buildDefString += "MORPHEUS";
                    break;
                case VRPlatforms.GearVR:
                    buildDefString += "GEAR_VR";
                    break;
            }
          
            // Other settings
            if (debug)
                buildDefString += ";DEBUGMODE";
            if (demo)
                buildDefString += ";DEMOMODE";
            if (release)
                buildDefString += ";RELEASE";

            // Custom definitions
			if(customDefs != "") {
				if(buildDefString.Length > 0) {
					buildDefString += ";";
				}
				buildDefString += "CUSTOMDEFS_;" + customDefs;
			}
        }

        static void SaveBuildSettings()
        {
            UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, buildDefString);
            UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.PS4, buildDefString);
            UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, buildDefString);
            UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS, buildDefString);

            // Set other per-platform settings
            switch (platform)
            {
                case VRPlatforms.Daydream:
                    // Daydream is special because it can be emulated.
                    switch (daydreamControlSettings)
                    {
                        case DaydreamControlSettings.Native:
                            UnityEditorInternal.VR.VREditor.SetVREnabledDevices(BuildTargetGroup.Android, new string[] { TBHMDSettings.DeviceDaydream });
                            UnityEditor.PlayerSettings.virtualRealitySupported = true;
                            if (swapManifest)
                                SwapManifest(_daydreamPath);
                            break;
                        case DaydreamControlSettings.Rift:
                            UnityEditorInternal.VR.VREditor.SetVREnabledDevices(BuildTargetGroup.Android, new string[] { TBHMDSettings.DeviceOculus, TBHMDSettings.DeviceDaydream });
                            UnityEditorInternal.VR.VREditor.SetVREnabledDevices(BuildTargetGroup.Standalone, new string[] { TBHMDSettings.DeviceOculus });
                            UnityEditor.PlayerSettings.virtualRealitySupported = true;
                            break;
                        case DaydreamControlSettings.SteamVR:
                            // Steam VR supports native integration in 5.4 and up.
                            #if UNITY_4 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3
                            UnityEditor.PlayerSettings.virtualRealitySupported = false;
                            #else
                            UnityEditorInternal.VR.VREditor.SetVREnabledDevices(BuildTargetGroup.Android, new string[] { TBHMDSettings.DeviceOculus, TBHMDSettings.DeviceDaydream });
                            UnityEditorInternal.VR.VREditor.SetVREnabledDevices(BuildTargetGroup.Standalone, new string[] { TBHMDSettings.DeviceOpenVR });
                            UnityEditor.PlayerSettings.virtualRealitySupported = true;
                            #endif
                            break;
                    }
                    break;
                case VRPlatforms.Cardboard:
                    UnityEditor.PlayerSettings.virtualRealitySupported = true;
                    if (swapManifest)
                        SwapManifest(_cardboardPath);
                    break;
                case VRPlatforms.Rift:
                    UnityEditor.PlayerSettings.virtualRealitySupported = true;
                    break;
                case VRPlatforms.SteamVR:
                    // Steam VR supports native integration in 5.4 and up.
                    #if UNITY_4 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3
                    UnityEditor.PlayerSettings.virtualRealitySupported = false;
                    #else
                    UnityEditor.PlayerSettings.virtualRealitySupported = true;
                    #endif
                    break;
                case VRPlatforms.PS4:
                    UnityEditor.PlayerSettings.virtualRealitySupported = true;
                    break;
                case VRPlatforms.GearVR:
                    UnityEditor.PlayerSettings.virtualRealitySupported = true;
                    if (swapManifest)
                    {
                        if (release)
                            SwapManifest(_OVRReleasePath);
                        else
                            SwapManifest(_OVRPath);
                    }
                    break;
            }
        }

        // Set Android Manifests
        static void SwapManifest(string path)
        {
            FileUtil.ReplaceFile(Application.dataPath + path, GetManifestPath());
            AssetDatabase.Refresh();
        }

        static string GetManifestPath()
        {
            return Application.dataPath + _manifestPath;
        }

        enum VRPlatforms
        {
            Rift,
            SteamVR,
            PS4,
            Daydream,
            Cardboard,
            GearVR
        }

        enum DaydreamControlSettings
        {
            Native,
            Rift,
            SteamVR
        }
    }
}