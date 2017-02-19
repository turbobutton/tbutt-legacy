using UnityEngine;
using System.Collections;
#if STEAM_VR
using Valve.VR;
#endif

#if DAYDREAM || FAKE_DAYDREAM
namespace TButt
{
    /// <summary>
    /// Wrapper for Daydream Controller.
    /// On Android, works natively through Google VR services.
    /// On PC, checks for Daydream input, then falls back to emulation with HTC Vive or Oculus Touch.
    /// To enable emulation, include scripting definitions FAKE_DAYDREAM and then either STEAM_VR or RIFT in your Unity build settings.
    /// </summary>
    public class TBDaydreamController : MonoBehaviour
    {
        public static TBDaydreamController instance;

        // Delegates
        public delegate void OnTouchDownEvent();
        public delegate void OnTouchUpEvent();
        public delegate void OnClickButtonDownEvent();
        public delegate void OnClickButtonUpEvent();
        public delegate void OnAppButtonDownEvent();
        public delegate void OnAppButtonUpEvent();
        public delegate void OnAppButtonLongPressEvent();
        public delegate void OnAppButtonShortPressEvent();
        public delegate void OnSwipeEvent(TBTouchpadSwipe direction);
        public delegate void OnSwipeLeftEvent();
        public delegate void OnSwipeRightEvent();
        public delegate void OnSwipeDownEvent();
        public delegate void OnSwipeUpEvent();
        public delegate void OnHandednessChangedEvent();

        // Set up our events.
        /// <summary>
        /// Fires on the frame that the touchpad was touched.
        /// </summary>
        public event OnTouchDownEvent OnTouchDown;
        /// <summary>
        /// Fires on the frame that the touchpad was released.
        /// </summary>
        public event OnTouchUpEvent OnTouchUp;
        /// <summary>
        /// Fires on the frame that the click button is pressed.
        /// </summary>
        public event OnClickButtonDownEvent OnClickButtonDown;
        /// <summary>
        /// Fires on the frame that button is no longer being hard-pressed down.
        /// </summary>
        public event OnClickButtonUpEvent OnClickButtonUp;
        /// <summary>
        /// Fires on the frame that the app button is pressed.
        /// </summary>
        public event OnAppButtonDownEvent OnAppButtonDown;
        /// <summary>
        /// Fires when the app button is no longer being hard-pressed down.
        /// </summary>
        public event OnAppButtonUpEvent OnAppButtonUp;
        /// <summary>
        /// Fires when the app button is released after being held for more than the _longAppButtonPressTime
        /// </summary>
        public event OnAppButtonLongPressEvent OnAppButtonLongPress;
        /// <summary>
        /// Fires when the app button is released after being held for less than the _longAppButtonPressTime
        /// </summary>
        public event OnAppButtonShortPressEvent OnAppButtonShortPress;
        /// <summary>
        /// Fires on any swipe. Passes a TBTouchpadSwipe direction.
        /// </summary>
        public event OnSwipeEvent OnSwipe;
        /// <summary>
        /// Fires on swipe up.
        /// </summary>
        public event OnSwipeUpEvent OnSwipeUp;
        /// <summary>
        /// Fires on swipe down.
        /// </summary>
        public event OnSwipeDownEvent OnSwipeDown;
        /// <summary>
        /// Fires on swipe left.
        /// </summary>
        public event OnSwipeLeftEvent OnSwipeLeft;
        /// <summary>
        /// Fires on swipe right.
        /// </summary>
        public event OnSwipeRightEvent OnSwipeRight;
        /// <summary>
		/// Fires on swipe right.
		/// </summary>
		public event OnHandednessChangedEvent OnHandednessChanged;

        // Swipe detection storage.
        private float _minMovMagnitude = 0.5f;  // Tune to differentiate between touches and swipes
        private float _lastTouchTime = 0f;     
        private Vector2 _touchMoveAmount;

        private float _longAppButtonPressTime = 1.5f;
        private float _lastAppButtonPressTime;

        private static Handedness _handedness = Handedness.Right;

        #if RIFT
		// For emulation with Oculus Touch, we specify a controller. This should be loaded from TBGameSettings.
		private static OVRInput.Controller _touchController;
		private static Vector2 _joystickPosition;
		private float _minMoveMagnitudeOVRTouch = 0.25f;
        #endif

        #if STEAM_VR
        private static Quaternion _steamControllerRotation = new Quaternion(0, 0, 0, 1);
        private static Vector3 _lastVelocity;
        private static Vector3 _lastAccleration;
        private static Vector3 _gyro;
        private bool _controllerHasLoaded = false;
        #endif

        #if FAKE_DAYDREAM
        private static Quaternion _calibrationOffset = Quaternion.identity; // Stored offset for "recalibrating" orientation of Vive controllers
        private bool _recalibrateOnAppButtonLongPress = true;   // Recalibrate camera and controllers on a long press of the app button when in Fake Daydream mode.
        #endif

        void Awake()
        {
            instance = this;
            #if STEAM_VR
            gameObject.AddComponent<TBSteamVRDeviceManager>();
            #endif
            SetHandedness();
        }

        private void OnApplicationFocus(bool focus)
        {
            // When regaining focus from Daydream Home, check to see if the player's handedness preference was changed.
            if (focus)
                SetHandedness();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))   // Quit if the "X" button in the corner of the screen is tapped.
                Application.Quit();

            #if STEAM_VR
            // Early out from this update loop when if Steam VR mode if there are no Steam VR controllers and no controller emulators connected, since there is nothing to update.
            if (GvrController.State != GvrConnectionState.Connected)
            {
                if (!TBSteamVRDeviceManager.IsDeviceConnected(TBSteamVRDeviceManager.DeviceType.AnyController))
                    return;
                else if (!_controllerHasLoaded)
                {
                    if (TBSteamVRDeviceManager.GetFirstController().rotation == _calibrationOffset)    // this means we haven't received any poses from the controller yet.
                        return;
                    else
                    {
                        _calibrationOffset = TBSteamVRDeviceManager.GetFirstController().rotation;
                        _controllerHasLoaded = true;
                        Debug.Log("TBDaydreamController: Steam VR emulation started with calibration offset " + _calibrationOffset);
                    }
                }
            }
            #endif

            // Touchpad click button events
            if (GetButtonDown(TBAVRInputType.ClickButton))
            {
                if (OnClickButtonDown != null)
                    OnClickButtonDown();
            }
            else if (GetButtonUp(TBAVRInputType.ClickButton))
            {
                if (OnClickButtonUp != null)
                    OnClickButtonUp();
            }

            // App button events
            if (GetButtonDown(TBAVRInputType.AppButton))
            {
                // Long press logging.
                _lastAppButtonPressTime = Time.realtimeSinceStartup;

                if (OnAppButtonDown != null)
                    OnAppButtonDown();
            }
            else if (GetButtonUp(TBAVRInputType.AppButton))
            {
                if (OnAppButtonUp != null)
                    OnAppButtonUp();

                // Long Press / short press events.
                if (Time.realtimeSinceStartup - _lastAppButtonPressTime > _longAppButtonPressTime)
                {
                    if (OnAppButtonLongPress != null)
                        OnAppButtonLongPress();
                    #if FAKE_DAYDREAM
                    if(_recalibrateOnAppButtonLongPress)    // Recalibrate on long press of app button.
                        CalibrateOrientation(); 
                    #endif
                }
                else
                {
                    if (OnAppButtonShortPress != null)
                        OnAppButtonShortPress();
                }
            }

            // Touchpad events
            if (GetButtonDown(TBAVRInputType.Touchpad))
            {
                if (OnTouchDown != null)
                    OnTouchDown();
            }
            else if (GetButtonUp(TBAVRInputType.Touchpad))
            {
                if (OnTouchUp != null)
                    OnTouchUp();
            }

            // Log input so we can detect swipes.
            LogTouchpadInput();

            #if FAKE_DAYDREAM
            if (GvrController.Recentered)
                CalibrateOrientation();
            #endif

            #if STEAM_VR
            // Update Steam VR device stuff.
            _steamControllerRotation = TBSteamVRDeviceManager.GetFirstController().rotation;
            _lastAccleration = SteamVR_Controller.Input((int)TBSteamVRDeviceManager.GetFirstController().index).velocity;
            _lastAccleration = new Vector3(_lastAccleration.x - _lastVelocity.x, _lastAccleration.y - _lastVelocity.y, _lastAccleration.z - _lastVelocity.z) / Time.unscaledDeltaTime;
            _lastVelocity = SteamVR_Controller.Input((int)TBSteamVRDeviceManager.GetFirstController().index).velocity;
            _gyro = SteamVR_Controller.Input((int)TBSteamVRDeviceManager.GetFirstController().index).angularVelocity;
            _gyro = new Vector3(_gyro.x, _gyro.z, _gyro.y);
            #endif
        }

        /// <summary>
        /// Returns the calibrated orientation of the controller as a quaternion.
        /// </summary>
        /// <returns></returns>
        public static Quaternion GetRotation()
        {
            #if FAKE_DAYDREAM
            if (GvrController.State == GvrConnectionState.Connected)
                return GvrController.Orientation * Quaternion.Inverse(_calibrationOffset);
            #if RIFT
            else
				return OVRInput.GetLocalControllerRotation(_touchController) * Quaternion.Inverse(_calibrationOffset);
            #elif STEAM_VR
            else
                return _steamControllerRotation * Quaternion.Inverse(_calibrationOffset);
            #endif
            #elif DAYDREAM
            return GvrController.Orientation;
            #else
            return Quaternion.identity;
            #endif
        }

        public static Quaternion GetRawRotation()
        {
            #if FAKE_DAYDREAM
            if (GvrController.State == GvrConnectionState.Connected)
                return GvrController.Orientation;
            #if RIFT
            else
				return OVRInput.GetLocalControllerRotation(_touchController);
            #elif STEAM_VR
            else
                return _steamControllerRotation;
            #endif
            #elif DAYDREAM
            return GvrController.Orientation;
            #else
            return Quaternion.identity;
            #endif
        }

        /// <summary>
        /// Returns true during the frame the given button was pressed.
        /// </summary>
        /// <param name="inputType"></param>
        /// <returns></returns>
        public static bool GetButtonDown(TBAVRInputType inputType)
        {
            #if FAKE_DAYDREAM
            if (GvrController.State == GvrConnectionState.Connected)
                return GetAndroidVRButtonDown(inputType);
            #if RIFT
			return GetOVRTouchButtonDown(inputType);
            #elif STEAM_VR
            return GetSteamVRButtonDown(inputType);
            #endif
            #elif DAYDREAM
            return GetAndroidVRButtonDown(inputType);
            #else
            return false;
            #endif
        }

        /// <summary>
        /// Returns true during the frame the given button was released.
        /// </summary>
        /// <param name="inputType"></param>
        /// <returns></returns>
        public static bool GetButtonUp(TBAVRInputType inputType)
        {
            #if FAKE_DAYDREAM
            if (GvrController.State == GvrConnectionState.Connected)
                return GetAndroidVRButtonUp(inputType);
            #if RIFT
			return GetOVRTouchButtonUp(inputType);
            #elif STEAM_VR
            return GetSteamVRButtonUp(inputType);
            #endif
            #elif DAYDREAM
            return GetAndroidVRButtonUp(inputType);
            #else
            return false;
            #endif
        }

        /// <summary>
        /// Returns true while the given button is held.
        /// </summary>
        /// <param name="inputType"></param>
        /// <returns></returns>
        public static bool GetButton(TBAVRInputType inputType)
        {
            #if FAKE_DAYDREAM
            if (GvrController.State == GvrConnectionState.Connected)
                return GetAndroidVRButton(inputType);
            #if RIFT
			return GetOVRTouchButton(inputType);
            #elif STEAM_VR
            return GetSteamVRButton(inputType);
            #endif
            #elif DAYDREAM
            return GetAndroidVRButton(inputType);
            #else
            return false;
            #endif
        }

        /// <summary>
        /// Resets the orientation of the controller and/or the camera.
        /// </summary>
        /// <param name="recenterController">Reset controller orientation.</param>
        /// <param name="recenterCamera">Reset camera orientation.</param>
        public static void CalibrateOrientation(bool recenterController = true, bool recenterCamera = true)
        {
            #if FAKE_DAYDREAM
            if (recenterCamera)
                if (TBCameraRig.instance != null)
                    TBCameraRig.instance.CalibrateCameraPosition();

            if (GvrController.State == GvrConnectionState.Connected)
            {
                if (recenterController)
                    _calibrationOffset = GvrController.Orientation;
            }
            else
            {
                #if RIFT
				if(recenterController)
					_calibrationOffset = OVRInput.GetLocalControllerRotation(_touchController);
                #elif STEAM_VR
                if (recenterController)
                    _calibrationOffset = _steamControllerRotation;
                #endif
            }
            #elif DAYDREAM
			if(recenterCamera)
                if(TBCameraRig.instance != null)
                    TBCameraRig.instance.CalibrateCameraPosition();
            #endif
        }

        /// <summary>
        /// Returns position of touchpad touch between 0,1.
        /// </summary>
        /// <returns></returns>
        public static Vector2 GetTouchPosition()
        {
            #if FAKE_DAYDREAM
            if (GvrController.State == GvrConnectionState.Connected)
                return GvrController.TouchPos;
            #if RIFT
			_joystickPosition = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _touchController);
			// Oculus Touch joystick returns from -1,1. Convert to 0,1 scale to match AVR remote.
			if(_joystickPosition.x > 0)
				_joystickPosition.x = (_joystickPosition.x / 2) + 0.5f;
			else
				_joystickPosition.x = (_joystickPosition.x / 2) + +0.5f;
			if(_joystickPosition.y > 0)
				_joystickPosition.y = (_joystickPosition.y / -2) + 0.5f;
			else
				_joystickPosition.y = (_joystickPosition.y / -2) + 0.5f;
			return _joystickPosition;
            #elif STEAM_VR
            return SteamVR_Controller.Input((int)TBSteamVRDeviceManager.GetFirstController().index).GetAxis(Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad);
            #endif
            #elif DAYDREAM
            return GvrController.TouchPos;
            #else
            return Vector2.zero;
            #endif
        }

        public static Vector3 GetAcceleration()
        {
            #if FAKE_DAYDREAM
            if (GvrController.State == GvrConnectionState.Connected)
                return GvrController.Accel;
            #if RIFT
			return OVRInput.GetLocalControllerAcceleration(_touchController);
            #elif STEAM_VR
            return _lastAccleration;
            #endif
            #else
            return GvrController.Accel;
            #endif
        }

        public static Vector3 GetGyro()
        {
            #if FAKE_DAYDREAM
            if (GvrController.State == GvrConnectionState.Connected)
                return GvrController.Gyro;
            #if RIFT
			return OVRInput.GetLocalControllerAngularVelocity(_touchController).eulerAngles;
            #elif STEAM_VR
            return _gyro;
            #endif
            #else
            return GvrController.Gyro;
            #endif
        }

        public void SetHandedness()
        {
            Handedness newHandedness;

            #if FAKE_DAYDREAM
            #if RIFT
            if (TBGameSettings.playerSettings.rightHanded)
            { 
                _touchController = OVRInput.Controller.RTouch;
                newHandedness = Handedness.Right;
            }
            else
            { 
                _touchController = OVRInput.Controller.LTouch;
                newHandedness = Handedness.Left;
            }
            #elif STEAM_VR
            _lastVelocity = Vector3.zero;
            #endif
            #else

            // Use Daydream's GVRSettings to determine Handedness when on device.
            if (GvrSettings.Handedness == GvrSettings.UserPrefsHandedness.Left)
                newHandedness = Handedness.Left;
            else
                newHandedness = Handedness.Right;

            if (newHandedness != _handedness)
            {
                Debug.Log("Player's handedness was updated.");
                _handedness = newHandedness;
                if (OnHandednessChanged != null)
                    OnHandednessChanged();
            }
            #endif
        }

        public static Handedness GetHandedness()
        {
            return _handedness;
        }


        #region NATIVE ANDROID VR INPUT DETECTION

        #if DAYDREAM || FAKE_DAYDREAM
        /// <summary>
        /// Checks to see if an AVR button was pressed on this frame. Native functions.
        /// </summary>
        /// <param name="inputType"></param>
        /// <returns></returns>
        private static bool GetAndroidVRButtonDown(TBAVRInputType inputType)
        {
            switch (inputType)
            {
                case TBAVRInputType.ClickButton:
                    return GvrController.ClickButtonDown;
                case TBAVRInputType.AppButton:
                    return GvrController.AppButtonDown;
                case TBAVRInputType.Touchpad:
                    return GvrController.TouchDown;
            }

            // Return false if the controller is not connected or we don't recognize the button type.
            return false;
        }

        /// <summary>
        /// Checks to see if an AVR button was released on this frame. Native functions.
        /// </summary>
        /// <param name="inputType"></param>
        /// <returns></returns>
        private static bool GetAndroidVRButtonUp(TBAVRInputType inputType)
        {
            switch (inputType)
            {
                case TBAVRInputType.ClickButton:
                    return GvrController.ClickButtonUp;
                case TBAVRInputType.AppButton:
                    return GvrController.AppButtonUp;
                case TBAVRInputType.Touchpad:
                    return GvrController.TouchUp;
            }

            // Return false if the controller is not connected or we don't recognize the button type.
            return false;
        }

        /// <summary>
        /// Checks to see if an AVR button is being held. Native functions.
        /// </summary>
        /// <param name="inputType"></param>
        /// <returns></returns>
        private static bool GetAndroidVRButton(TBAVRInputType inputType)
        {
            switch (inputType)
            {
                case TBAVRInputType.ClickButton:
                    return GvrController.ClickButton;
                case TBAVRInputType.AppButton:
                    return GvrController.AppButton;
                case TBAVRInputType.Touchpad:
                    return GvrController.IsTouching;
            }

            // Return false if the controller is not connected or we don't recognize the button type.
            return false;
        }
        #endif
        #endregion

        #region EMULATION WITH OCULUS TOUCH CONTROLLERS

        #if RIFT
		private static bool GetOVRTouchButtonDown(TBAVRInputType inputType)
		{
			switch(inputType) {
			case TBAVRInputType.ClickButton:
				return OVRInput.GetDown(OVRInput.Button.One, _touchController) || OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, _touchController);
			case TBAVRInputType.AppButton:
				return OVRInput.GetDown(OVRInput.Button.Two, _touchController);
			case TBAVRInputType.Touchpad:
				return OVRInput.GetDown(OVRInput.Touch.PrimaryThumbstick, _touchController);
			}

			// Return false if we don't recognize the button type.
			return false;
		}

		private static bool GetOVRTouchButtonUp(TBAVRInputType inputType)
		{
			switch(inputType) {
			case TBAVRInputType.ClickButton:
				return OVRInput.GetUp(OVRInput.Button.One, _touchController) || OVRInput.GetUp(OVRInput.Button.PrimaryThumbstick, _touchController);
			case TBAVRInputType.AppButton:
				return OVRInput.GetUp(OVRInput.Button.Two, _touchController);
			case TBAVRInputType.Touchpad:
				return OVRInput.GetUp(OVRInput.Touch.PrimaryThumbstick, _touchController);
			}

			// Return false if we don't recognize the button type.
			return false;
		}

		private static bool GetOVRTouchButton(TBAVRInputType inputType)
		{
			switch(inputType) {
			case TBAVRInputType.ClickButton:
				return OVRInput.Get(OVRInput.Button.One, _touchController) || OVRInput.Get(OVRInput.Button.PrimaryThumbstick, _touchController);
			case TBAVRInputType.AppButton:
				return OVRInput.Get(OVRInput.Button.Two, _touchController);
			case TBAVRInputType.Touchpad:
				return OVRInput.Get(OVRInput.Touch.PrimaryThumbstick, _touchController);
			}

			// Return false if we don't recognize the button type.
			return false;
		}
        #endif
        #endregion

        #region EMULATION WITH STEAM VR / HTC VIVE CONTROLLERS

        #if STEAM_VR
        private static bool GetSteamVRButtonDown(TBAVRInputType inputType)
        {
            // if the device is not connected, return false
            if ((int)TBSteamVRDeviceManager.GetFirstController().index < 0)
            {
                return false;
            }

            switch (inputType)
            {
                case TBAVRInputType.ClickButton:
                    return SteamVR_Controller.Input((int)TBSteamVRDeviceManager.GetFirstController().index).GetPressDown(Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad);
                case TBAVRInputType.AppButton:
                    return SteamVR_Controller.Input((int)TBSteamVRDeviceManager.GetFirstController().index).GetPressDown(Valve.VR.EVRButtonId.k_EButton_ApplicationMenu);
                case TBAVRInputType.Touchpad:
                    return SteamVR_Controller.Input((int)TBSteamVRDeviceManager.GetFirstController().index).GetTouchDown(Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad);
            }

            // Return false if we don't recognize the button type.
            return false;
        }

        private static bool GetSteamVRButtonUp(TBAVRInputType inputType)
        {
            // if the device is not connected, return false
            if ((int)TBSteamVRDeviceManager.GetFirstController().index < 0)
            {
                return false;
            }

            switch (inputType)
            {
                case TBAVRInputType.ClickButton:
                    return SteamVR_Controller.Input((int)TBSteamVRDeviceManager.GetFirstController().index).GetPressUp(Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad);
                case TBAVRInputType.AppButton:
                    return SteamVR_Controller.Input((int)TBSteamVRDeviceManager.GetFirstController().index).GetPressUp(Valve.VR.EVRButtonId.k_EButton_ApplicationMenu);
                case TBAVRInputType.Touchpad:
                    return SteamVR_Controller.Input((int)TBSteamVRDeviceManager.GetFirstController().index).GetTouchUp(Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad);
            }

            // Return false if we don't recognize the button type.
            return false;
        }

        private static bool GetSteamVRButton(TBAVRInputType inputType)
        {
            // if the device is not connected, return false
            if (!TBSteamVRDeviceManager.IsDeviceConnected(TBSteamVRDeviceManager.DeviceType.AnyController))
                return false;

            switch (inputType)
            {
                case TBAVRInputType.ClickButton:
                    return SteamVR_Controller.Input((int)TBSteamVRDeviceManager.GetFirstController().index).GetPress(Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad);
                case TBAVRInputType.AppButton:
                    return SteamVR_Controller.Input((int)TBSteamVRDeviceManager.GetFirstController().index).GetPress(Valve.VR.EVRButtonId.k_EButton_ApplicationMenu);
                case TBAVRInputType.Touchpad:
                    return SteamVR_Controller.Input((int)TBSteamVRDeviceManager.GetFirstController().index).GetTouch(Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad);
            }

            // Return false if we don't recognize the button type.
            return false;
        }
        #endif

        #endregion

        #region SWIPE FUNCTIONS

        /// <summary>
        /// Checks for touch events, and logs change in position.
        /// </summary>
        private void LogTouchpadInput()
        {
            #if DAYDREAM  // More optimized to just call this stuff directly when we're on device.
            if (GvrController.TouchDown)
            {
                _touchMoveAmount = GvrController.TouchPos;
                _lastTouchTime = Time.realtimeSinceStartup;
            }
            else if (GvrController.TouchUp)
            {
                _touchMoveAmount -= GvrController.TouchPos;
                CheckForSwipes(ref _touchMoveAmount);
            }
            #elif FAKE_DAYDREAM
            if (GetButtonDown(TBAVRInputType.Touchpad))
            {
            #if RIFT
                
				if(GvrController.State == GvrConnectionState.Connected)
					_touchMoveAmount = GetTouchPosition();
				else
					_touchMoveAmount = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _touchController);
            #else
                _touchMoveAmount = GetTouchPosition();
            #endif
                _lastTouchTime = Time.realtimeSinceStartup;
            }
            else if (GetButtonUp(TBAVRInputType.Touchpad))
            {
                #if RIFT
				if(GvrController.State == GvrConnectionState.Connected)
					_touchMoveAmount -= GetTouchPosition();
				else
					_touchMoveAmount -= OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _touchController);
				_minMovMagnitude = _minMoveMagnitudeOVRTouch;
                #else
                _touchMoveAmount -= GetTouchPosition();
                #endif
                CheckForSwipes(ref _touchMoveAmount);
            }
            #endif
        }

        /// <summary>
        /// Called when a touch event ends. Checks to see if a swipe occured.
        /// </summary>
        /// <param name="move"></param>
        private void CheckForSwipes(ref Vector2 move)
        {
            #if RIFT
			if(GvrController.State == GvrConnectionState.Connected)
				move = move.normalized;
            #endif
            if (Time.realtimeSinceStartup - _lastTouchTime > 0.75F)
            {
                return;
            }
            else if (move.magnitude < _minMovMagnitude)
            {
                return;
            }
            else
            {
                // Left/Right
                if (Mathf.Abs(move.x) > Mathf.Abs(move.y))
                {
                    if (move.x > 0.0f)
                        TriggerSwipeLeftEvent();
                    else
                        TriggerSwipeRightEvent();
                }
                // Up/Down
                else
                {
                    if (move.y < 0.0f)
                        TriggerSwipeDownEvent();
                    else
                        TriggerSwipeUpEvent();
                }
            }
        }

        private void TriggerSwipeUpEvent()
        {
            if (OnSwipeUp != null)
                OnSwipeUp();
            if (OnSwipe != null)
                OnSwipe(TBTouchpadSwipe.Up);
        }

        private void TriggerSwipeDownEvent()
        {
            if (OnSwipeDown != null)
                OnSwipeDown();
            if (OnSwipe != null)
                OnSwipe(TBTouchpadSwipe.Down);
        }

        private void TriggerSwipeLeftEvent()
        {
            if (OnSwipeLeft != null)
                OnSwipeLeft();
            if (OnSwipe != null)
                OnSwipe(TBTouchpadSwipe.Left);
        }

        private void TriggerSwipeRightEvent()
        {
            if (OnSwipeRight != null)
                OnSwipeRight();
            if (OnSwipe != null)
                OnSwipe(TBTouchpadSwipe.Right);
        }

        public enum Handedness
        {
            Right,
            Left
        }

        #endregion
    }

    [System.Serializable]
    public enum TBAVRInputType : int
    {
        ClickButton,
        AppButton,
        Touchpad
    }
}
#endif
