using UnityEngine;
using System.Collections;
using UnityEngine.VR;
using System.Runtime.InteropServices;

namespace TButt
{
    public static class TBHMDSettings
    {
        // Strings that are compatible with Unity's built-in VRSettings functions
        public static readonly string DeviceOculus = "Oculus";
        public static readonly string DeviceOpenVR = "OpenVR";
        public static readonly string DeviceDaydream = "daydream";

        private static bool _usesNativeIntegration;
        private static int _refreshRate;
        private static bool _supportsPositionTracking;
        private static Color32 _defaultFadeColor = Color.black;
        private static bool _initialized = false;

        public static void InitializeHMDSettings()
        {
            if (_initialized)
                return;
            GetHMDProfile();
            Time.fixedDeltaTime = 1 / (float)_refreshRate;
            Time.maximumDeltaTime = Time.fixedDeltaTime;

            #if STEAM_VR
            //  SteamVR_Fade.View(defaultFadeColor, 1f);
            #endif

            _usesNativeIntegration = UnityEngine.VR.VRSettings.enabled;

            _initialized = true;
        }

        private static void GetHMDProfile()
        {
            #if MORPHEUS
            // Set to whatever the refresh rate on the headset is.
            // Need to investigate how to pull this in.
            _supportsPositionTracking = true;
            _refreshRate = UnityEngine.VR.VRDevice.refreshRate;

            #elif GEAR_VR
            _refreshRate = 60;

            // 1x renderscale on Gear VR = 1024x1024.
            VRSettings.renderScale = 1f;                       
            Application.targetFrameRate = _refreshRate;
            supportsPositionTracking = false;

            #elif DAYDREAM
            _refreshRate = 60;

            // 0.7x renderscale recommended by Google for Daydream.
            VRSettings.renderScale = .7f;                       
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = _refreshRate;

            // On Daydream / Cardboard, need to explicitly prevent screen from going black.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            _supportsPositionTracking = false;

            #elif CARDBOARD
            _refreshRate = 60;

            // 0.7x renderscale recommended by Google for Daydream.
            VRSettings.renderScale = .7f;                       
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = _refreshRate;

            // On Daydream / Cardboard, need to explicitly prevent screen from going black.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            _supportsPositionTracking = false;

            #elif RIFT
             if (UnityEngine.VR.VRDevice.model == "Oculus Rift DK2")
                _refreshRate = 75;
            else
                _refreshRate = 90;
            _supportsPositionTracking = OVRPlugin.positionSupported;

            #elif STEAM_VR
            _refreshRate = (int)SteamVR.instance.hmd_DisplayFrequency;
            _supportsPositionTracking = true;  
            #endif
        }

        /// <summary>
        /// Returns fixed timestep based on the HMD's refresh rate.
        /// </summary>
        /// <returns></returns>
        public static float GetFixedTimestep()
        {
            return 1f / _refreshRate;
        }

        public static bool HasPositionalTracking()
        {
            return _supportsPositionTracking;
        }

        /// <summary>
        /// Sets the Unity renderscale.
        /// </summary>
        /// <param name="scale">between 0.1 and 2.</param>
        public static void SetRenderscale(float scale)
        {
            if (_usesNativeIntegration)
                UnityEngine.VR.VRSettings.renderScale = scale;
        }

        #if STEAM_VR
        /// <summary>
        /// Calibrates Steam VR headset with a coroutine, since we can't guarantee the HMD is active during scene transitions and loads.
        /// </summary>
        /// <returns></returns>
        public static IEnumerator CalibrateSteamVR()
        {
            yield return new WaitForSeconds(0.02f);
            SteamVR.instance.hmd.ResetSeatedZeroPose();
            Debug.Log("Calibrated Steam VR");
        }
        #endif

        public static void CalibrateNativeCamera()
        {
            if(_usesNativeIntegration)
                UnityEngine.VR.InputTracking.Recenter();
        }
    }
}
