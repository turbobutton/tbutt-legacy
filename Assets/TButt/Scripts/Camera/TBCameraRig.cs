using UnityEngine;
using System.Collections;
using UnityEngine.VR;
using UnityEngine.Rendering;

#if UNITY_PS4
using UnityEngine.PS4;
#endif

namespace TButt
{
    /// <summary>
    /// Multiplatform camera rig. Reference in other scripts by using TBCameraRig.instance.
    /// 
    /// By Turbo Button
    /// www.turbo-button.com
    /// @TurboButtonInc
    /// </summary>
	public class TBCameraRig : MonoBehaviour 
	{
        public static TBCameraRig instance;

        [HideInInspector]
        public new Transform transform;

        [Header("Camera Settings")]
        [SerializeField]
        private CameraClearFlags _clearFlags;
        [SerializeField]
        private LayerMask _cullingMask;
        [SerializeField]
        private Color _backgroundColor;
        [SerializeField]
        private float _nearClipPlane = 1;
        [SerializeField]
        private float _farClipPlane = 250;
        [SerializeField]
        private bool _useOcclusionCulling;

        [Header("VR Settings")]
        public Vector3 cameraScale = new Vector3(1, 1, 1);
        public bool usePositionTracking = true;
        public OpaqueSortMode sortMode = OpaqueSortMode.NoDistanceSort;

        // Define platform-specific camera prefabs.
        [Header("Camera Prefabs")]
        [Tooltip("Used for native-integration cameras and Game window preview in editor.")]
        public GameObject StandardCamera;
        #if UNITY_EDITOR || RIFT || GEAR_VR
        [Tooltip("Used for Oculus Rift and Gear VR.")]
        public GameObject OculusCamera;
        #endif
        #if UNITY_EDITOR || STEAM_VR
        [Tooltip("Used for Steam VR.")]
        public GameObject SteamVRCamera;
        #endif

        [Header("Calibration Settings")]
        public bool isStandingExperience = false;
        public float uncalibratedPlayerHight = 1.75f;
        [SerializeField]
        private bool recenterOnLoad = false;
        [SerializeField]
        [Tooltip("Hotkey for recalibrating at runtime.")]
        private KeyCode calibrationHotkey = KeyCode.Space;

        #if UNITY_EDITOR
        // Setup the screenshot camera when in the editor.
        [Header("Screenshot Camera")]
        [Tooltip("If defined, a screenshot camera will be created when running in the editor (never in builds). Leave undefined if you don't want to use this.")]
        public TBScreenshotCamera TBScreenshotCamera;
        private Camera _screenshotCamera;
        #endif

        #region GETTERS AND SETTERS
        public CameraClearFlags clearFlags
        {
            get { return _clearFlags; }
            set { _clearFlags = value; ApplyCameraChanges(); }
        }
        public bool useOcclusionCulling
        {
            get { return _useOcclusionCulling; }
            set { _useOcclusionCulling = value; ApplyCameraChanges(); }
        }
        public LayerMask cullingMask
        {
            get { return _cullingMask; }
            set { _cullingMask = value; ApplyCameraChanges(); }
        }
        public Color backgroundColor
        {
            get { return _backgroundColor; }
            set { _backgroundColor = value; ApplyCameraChanges(); }
        }
        public float nearClipPlane
        {
            get { return _nearClipPlane; }
            set { _nearClipPlane = value; ApplyCameraChanges(); }
        }
        public float farClipPlane
        {
            get { return _farClipPlane; }
            set { _farClipPlane = value; ApplyCameraChanges(); }
        }
        #endregion

        private GameObject leftEye;
		private GameObject rightEye;
		private GameObject centerEye;
		private GameObject cameraObject;
		private GameObject trackingObject;
		private TBCameraMode cameraMode;

		private Camera centerCamera;
		private Camera leftCamera;
		private Camera rightCamera;

		private bool useDefaultScreenFade = true;

        // Transform for anchoring things to the camera.
        private Transform TBCenter;

        void Awake()
		{
            instance = this;
			transform = GetComponent<Transform> ();

            TBHMDSettings.InitializeHMDSettings();

            // Choose which camera we should be using based on what platform we're on.
            ChooseCamera();

            if (cameraObject != gameObject)
            {
                cameraObject.transform.parent = transform;
                cameraObject.transform.localPosition = Vector3.zero;
                cameraObject.transform.localRotation = Quaternion.identity;
            }

			switch(cameraMode)
			{
			    case TBCameraMode.Single: // For fancy systems that only show one camera in Unity, like the native integrations.
				    centerCamera = centerEye.GetComponent<Camera> ();
                    SyncCameraSettings(centerCamera);
                    break;
			    case TBCameraMode.Dual:   // For legacy systems that use two cameras in the editor.
				    leftCamera = leftEye.GetComponent<Camera> ();
				    rightCamera = rightEye.GetComponent<Camera> ();
                    SyncCameraSettings(leftCamera);
                    SyncCameraSettings(rightCamera);
                    break;
			}

            #if UNITY_EDITOR
            // Setup the screenshot camera. If no screenshot camera already exists, try to create one.
            if (FindObjectOfType<TBScreenshotCamera>() != null)
                TBScreenshotCamera = FindObjectOfType<TBScreenshotCamera>();
            else if(TBScreenshotCamera != null)
                TBScreenshotCamera = Instantiate(TBScreenshotCamera).gameObject.GetComponent<TBScreenshotCamera>();

            // Apply settings to the screenshot camera if it was created.
            if (TBScreenshotCamera != null)
            {
                _screenshotCamera = TBScreenshotCamera.gameObject.GetComponent<Camera>();
                SyncCameraSettings(_screenshotCamera);
                TBScreenshotCamera.transform.position = transform.position;
                TBScreenshotCamera.transform.rotation = transform.rotation;
                TBScreenshotCamera.gameObject.SetActive(false);
            }
            #endif

            // Create TBCenter, the anchor transfom to use if you need to get the camera's position.
            TBCenter = new GameObject().transform;
            TBCenter.gameObject.name = "TBCenter";
            TBCenter.MakeZeroedChildOf(centerEye.transform);
            
            // If there is no audio listener on the instantiated camera, add one to TBCenter.
            if (GetComponentInChildren<AudioListener>() == null)
                TBCenter.gameObject.AddComponent<AudioListener>();

            if (recenterOnLoad)
                CalibrateCameraPosition();
		}

        void Start()
        {
			// Set camera scale in Start so that anything made a child in Awake will also be scaled as expected.
			SetCameraScale (cameraScale);
        }

        #if RIFT || STEAM_VR || UNITY_EDITOR // Recalibrate on spacebar on PC
        void Update()
        {
            if (Input.GetKeyDown(calibrationHotkey))
                CalibrateCameraPosition();
        }
        #endif

		private void ChooseCamera()
		{
            #region OCULUS RIFT / GEAR VR CAMERA
            #if RIFT || GEAR_VR
            if(OculusCamera == null)
                Debug.LogError("Oculus camera prefab must be set in TBCameraRig to run in Rift or Gear VR mode!", gameObject);
            Debug.Log ("<color=orange>TBCameraRig: Using " + OculusCamera.gameObject.name + " (Rift / Gear VR mode)...</color>", gameObject);

            // Oculus Rift and Gear VR use the native VR support with Oculus Tools for Unity.

            // Destroy the preview camera. Only used by Unity to see where we're looking.
			Destroy (StandardCamera.gameObject);

			// Rift cameras use native Unity VR support. Single Camera Mode.
			cameraMode = TBCameraMode.Single;

			// Instnatiate the camera prefab.
			cameraObject = (GameObject)Instantiate(OculusCamera, transform.position, Quaternion.identity);

			// Set global preferences.
			OculusCamera.gameObject.GetComponent<OVRManager>().usePositionTracking = usePositionTracking;
			trackingObject = cameraObject.transform.FindChild("TrackingSpace").gameObject;
			leftEye = trackingObject.transform.FindChild("LeftEyeAnchor").gameObject;
			centerEye = trackingObject.transform.FindChild("CenterEyeAnchor").gameObject;
			rightEye = trackingObject.transform.FindChild("RightEyeAnchor").gameObject;
            if (isStandingExperience)
            {
                transform.position += new Vector3(0, uncalibratedPlayerHight, 0);
            }
			return;
            #endif
            #endregion

            #region CARDBOARD / DAYDREAM CAMERA
            #if (CARDBOARD || DAYDREAM)
            // Google VR platforms use native VR integration as of Unity 5.6.
            Debug.Log ("<color=orange>TBCameraRig: Using native Unity camera (Daydream / Cardboard mode)...</color>", gameObject);
            cameraMode = TBCameraMode.Single;
            cameraObject = transform.gameObject;
            cameraObject.transform.parent = transform;
            centerEye = StandardCamera.gameObject;
            centerEye.transform.parent = cameraObject.transform;
            if (isStandingExperience)
            {
                transform.position += new Vector3(0, uncalibratedPlayerHight, 0);
            }
            return;
            #endif
            #endregion

            #region MORPHEUS / PLAYSTATION VR CAMERA
            #if MORPHEUS
            // Morpheus uses native VR integration as of Unity 5.2.
            Debug.Log ("<color=orange>TBCameraRig: Using native Unity camera (PSVR / Morpheus mode)...</color>", gameObject);
            cameraMode = TBCameraMode.Single;
            cameraObject = new GameObject();
            cameraObject.transform.parent = transform;
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;
            cameraObject.name = "MorpheusCamera";
            centerEye = StandardCamera.gameObject;
            centerEye.transform.parent = cameraObject.transform;
            if (isStandingExperience)
            {
                transform.position += new Vector3(0, uncalibratedPlayerHight, 0);
            }
			return;
            #endif
            #endregion

            #region STEAM VR CAMERA
            #if STEAM_VR
            // Steam VR uses the Steam VR Plugin for Unity.
            if(SteamVRCamera == null)
                Debug.LogError("Steam VR camera prefab must be set in TBCameraRig to run in Steam VR mode!", gameObject);
            Debug.Log ("<color=orange>TBCameraRig: Using " + SteamVRCamera.gameObject.name + " (Steam VR mode)...</color>", gameObject);
            // Destroy the preview camera. Only used by Unity to see where we're looking before we hit play.
			Destroy (StandardCamera.gameObject);

			// Steam cameras only have a single camera in Unity.
			cameraMode = TBCameraMode.Single;

			// Instnatiate the camera prefab.
			cameraObject = (GameObject)Instantiate(SteamVRCamera, transform.position, Quaternion.identity);

			if(!isStandingExperience)
			{
                SteamVR.instance.compositor.SetTrackingSpace(Valve.VR.ETrackingUniverseOrigin.TrackingUniverseSeated);
                FindObjectOfType<SteamVR_Render>().trackingSpace = Valve.VR.ETrackingUniverseOrigin.TrackingUniverseSeated;
			}
			centerEye = FindObjectOfType<SteamVR_Camera>().gameObject;
			return;
            #endif

            #if !RIFT && !STEAM_VR && !DAYDREAM && !GEAR_VR && !CARDBOARD && !MORPHEUS
            Debug.LogError("TBCameraRig: No platform specified. Set platform in Turbo Button -> Build Settings menu.");
            #endif
            #endregion
        }

        private void SyncCameraSettings(Camera cam)
        {
            cam.clearFlags = clearFlags;
            cam.cullingMask = cullingMask;
            cam.nearClipPlane = nearClipPlane;
            cam.farClipPlane = farClipPlane;
            cam.backgroundColor = backgroundColor;
            cam.useOcclusionCulling = useOcclusionCulling;
            cam.opaqueSortMode = sortMode;
        }

        public Transform GetTBCenter()
        {
            return TBCenter;
        }

        public Camera GetCenterEyeCamera()
        {
            if (cameraMode == TBCameraMode.Single)
                return centerCamera;
            else
            {
                // Default to the left camera when there are two. Have to pick one! :/
                return leftCamera;
            }
        }

        public Camera GetLeftEyeCamera()
        {
            return leftCamera;
        }

        public Camera GetRightEyeCamera()
        {
            return rightCamera;
        }

        public GameObject GetPlatformCameraPrefab()
        {
            return cameraObject;
        }

        /// <summary>
        /// Recalibrates the center position.
        /// </summary>
		public void CalibrateCameraPosition()
		{
            Debug.Log("<color=orange>TBCameraRig: Recentering camera...</color>", gameObject);
            #if RIFT || GEAR_VR
		    OVRManager.display.RecenterPose();
            #elif STEAM_VR
            StartCoroutine(TBHMDSettings.CalibrateSteamVR());
            #elif MORPHEUS || DAYDREAM || CARDBOARD
            TBHMDSettings.CalibrateNativeCamera();
            #endif
        }

        public GameObject GetActiveCameraObject()
        {
            return cameraObject;
        }

        public TBCameraMode GetCameraMode()
        {
            return cameraMode;
        }

		public void SetCameraScale (float val)
		{
			transform.localScale = Vector3.one * val;
		}

		public void SetCameraScale (Vector3 scale)
		{
			transform.localScale = scale;
		}

		public void ResetCameraScale ()
		{
			SetCameraScale (cameraScale);
		}

		public float GetCameraScale ()
		{
			return transform.localScale.x;
		}

        [ExecuteInEditMode]
        void ApplyCameraChanges()
        {
            if (Application.isPlaying)
            {
                if (cameraMode == TBCameraMode.Single)
                {
                    if(centerCamera != null)
                        SyncCameraSettings(centerCamera);
                }
                else
                {
                    if(leftCamera != null)
                        SyncCameraSettings(leftCamera);
                    if(rightCamera != null)
                        SyncCameraSettings(rightCamera);
                }
            }
            else
            {
                if (StandardCamera != null)
                    SyncCameraSettings(StandardCamera.GetComponent<Camera>());
            }
        }

        #if UNITY_EDITOR
        void OnValidate()
        {
            ApplyCameraChanges();
        }
        #endif
    }

	public enum TBCameraMode
	{
		Single,
		Dual
	}
}

