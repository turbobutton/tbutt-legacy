#if STEAM_VR

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Valve.VR;

namespace TButt
{
    /// <summary>
    /// A wrapper for Steam VR events that can be used for TButt calls. Only sets up IDs and references, does not set up GameObjects to use these IDs.
    /// Based on the codepaths used in the Steam VR plugin's "SteamVR_ControllerManager.cs" script. 
    /// </summary>
    public class TBSteamVRDeviceManager : MonoBehaviour
    {
        public static TBSteamVRDeviceManager instance;

        // IDs for our controllers.
        private static uint _leftControllerIndex = OpenVR.k_unTrackedDeviceIndexInvalid;
        private static uint _rightControllerIndex = OpenVR.k_unTrackedDeviceIndexInvalid;
        private static TBSteamVRDevice[] _devices;

        private SteamVR_Utils.RigidTransform _transformUtil;

        // Delegates
        public delegate void OnControllerAssignmentChangedEvent();

        // Events
        /// <summary>
		/// Fires when a controller's Steam VR device index is changed (such as when it disconnects / reconnects).
		/// </summary>
		public event OnControllerAssignmentChangedEvent OnControllerAssignmentChanged;

        private void Awake()
        {
            instance = this;
            _devices = new TBSteamVRDevice[OpenVR.k_unMaxTrackedDeviceCount];
            for(int i = 0; i < _devices.Length; i++)
            {
                _devices[i] = new TBSteamVRDevice();
            }
            _transformUtil = new SteamVR_Utils.RigidTransform();
            RefreshDeviceAssignments();
        }

        void OnEnable()
        {
            SteamVR_Events.DeviceConnected.Listen(OnDeviceConnected);
            SteamVR_Events.NewPoses.Listen(OnNewPoses);
        }

        void OnDisable()
        {
            SteamVR_Events.DeviceConnected.Remove(OnDeviceConnected);
            SteamVR_Events.NewPoses.Remove(OnNewPoses);
        }

        void OnDeviceConnected(int index, bool connected)
        {
            bool changed = _devices[index].connected;    // will be true if this device was previously connected.
            _devices[index].connected = false;           // force the device at this index to be logged as disconnected until we check it out below.

            if(connected)
            {
                var system = OpenVR.System;
                if(system != null)
                {
                    // Assign the type for the new device. 
                    _devices[index].index = (uint)index;
                    _devices[index].deviceType = system.GetTrackedDeviceClass((uint)index);
                    _devices[index].connected = true;
                    changed = !changed;
                }
            }

            if (changed)
                RefreshDeviceAssignments();
        }

        void OnNewPoses(TrackedDevicePose_t[] poses)
        {
            for(int i = 0; i < _devices.Length; i++)
            {
                if(poses[i].bPoseIsValid)
                {
                    _transformUtil = new SteamVR_Utils.RigidTransform(poses[i].mDeviceToAbsoluteTracking);
 
                    _devices[i].rotation = _transformUtil.rot;
                    _devices[i].position = _transformUtil.pos;
                }
            }
        }

        public void RefreshDeviceAssignments()
        {
            // Make sure Open VR is running before trying to assign stuff.
            var system = OpenVR.System;
            if (system == null)
                return;

            AssignControllers(system);
        }

        void AssignControllers(CVRSystem system)
        {
            bool controllerIndexChanged = false;

            if (_rightControllerIndex != system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand))
            {
                _rightControllerIndex = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
                Debug.Log("Right controller index assigned to " + _rightControllerIndex);
                controllerIndexChanged = true;
            }

            if (_leftControllerIndex != system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand))
            {
                _leftControllerIndex = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
                Debug.Log("Left controller index assigned to " + _leftControllerIndex);
                controllerIndexChanged = true;
            }

            if ((OnControllerAssignmentChanged != null) && controllerIndexChanged)
                OnControllerAssignmentChanged();
        }

        #region ACCESSORS
        public static bool IsDeviceConnected(DeviceType type)
        {
            switch(type)
            {
                case DeviceType.AnyController:
                    if (_leftControllerIndex != OpenVR.k_unTrackedDeviceIndexInvalid)
                    {
                        if (_devices[_leftControllerIndex].connected)
                            return true;
                    }
                    if (_rightControllerIndex != OpenVR.k_unTrackedDeviceIndexInvalid)
                    {
                        if (_devices[_rightControllerIndex].connected)
                            return true;
                    }
                    return false;
                case DeviceType.LeftController:
                    if (_leftControllerIndex != OpenVR.k_unTrackedDeviceIndexInvalid)
                    {
                        if (_devices[_leftControllerIndex].connected)
                            return true;
                    }
                    return false;
                case DeviceType.RightController:
                    if (_rightControllerIndex != OpenVR.k_unTrackedDeviceIndexInvalid)
                    {
                        if (_devices[_rightControllerIndex].connected)
                            return true;
                    }
                    return false;
            }

            Debug.LogError("The IsDeviceConnected function is not implemented for type " + type);
            return false;
        }

        public static TBSteamVRDevice GetFirstController()
        {
            if (IsDeviceConnected(DeviceType.RightController))
                return _devices[_rightControllerIndex];
            else if (IsDeviceConnected(DeviceType.LeftController))
                return _devices[_leftControllerIndex];

            Debug.LogWarning("First controller requested, but no Steam VR controller is currently connected. Returning null!");
            return null;
        }

        public static TBSteamVRDevice GetLeftController()
        {
            if (IsDeviceConnected(DeviceType.LeftController))
                return _devices[_leftControllerIndex];
            else
                return null;
        }

        public static TBSteamVRDevice GetRightController()
        {
            if (IsDeviceConnected(DeviceType.RightController))
                return _devices[_rightControllerIndex];
            else
                return null;
        }
        #endregion

        public class TBSteamVRDevice
        {
            public uint index = OpenVR.k_unTrackedDeviceIndexInvalid;
            public bool connected = false;
            public ETrackedDeviceClass deviceType;
            public Quaternion rotation = Quaternion.identity;
            public Vector3 position = Vector3.zero;
        }

        public enum DeviceType
        {
            LeftController,
            RightController,
            AnyController,
            HMD
        }
    }
}
#endif