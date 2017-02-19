using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
namespace TButt
{
    public class TBScreenshotCamera : MonoBehaviour
    {
        public static TBScreenshotCamera instance;

        public int resWidth = 2560;
        public int resHeight = 1440;
        public string savePath = "";
        public string prefix = "Screenshot";
        public bool openAfterSaving;
        public bool forceMSAA;
        public bool hotkey;
        public KeyCode hotkeyKeycode = KeyCode.Semicolon;

        private Camera _camera;
        private bool _isParented;
        public bool hideScreenshotObjects = false;

        private RenderTexture _renderTexture;
        private TBScreenshotToggle[] _toggleObjects;

        private void OnEnable()
        {
            #if !UNITY_EDITOR
            Destroy(gameObject);
            #endif
            instance = this;
            _camera = GetComponent<Camera>();
            _toggleObjects = FindObjectsOfType<TBScreenshotToggle>();
        }

        private void Update()
        {
            if(hotkey)
                if (Input.GetKeyDown(hotkeyKeycode))
                    TakeScreenshot();
        }

        public void ToggleScreenshotObjects(bool on)
        {
            if (_toggleObjects == null)
                return;

            for(int i = 0; i < _toggleObjects.Length; i++)
            {
                _toggleObjects[i].gameObject.SetActive(on);
            }

            hideScreenshotObjects = !on;
        }

        /// <summary>
        /// Sets the position to match the VR camera's position.
        /// </summary>
        public void MatchVRCameraPosition()
        {
            transform.position = TBCameraRig.instance.GetTBCenter().position;
        }

        /// <summary>
        /// Sets the rotation to match the VR camera's rotation.
        /// </summary>
        public void MatchVRCameraOrientation()
        {
            transform.rotation = TBCameraRig.instance.GetTBCenter().rotation;
        }

        /// <summary>
        /// Toggles between being a child of TBCenter or not.
        /// </summary>
        public void ToggleParenting()
        {
            if (transform.parent == TBCameraRig.instance.GetTBCenter())
            {
                transform.SetParent(null);
                _isParented = false;
            }
            else
            {
                _isParented = true;
                transform.MakeZeroedChildOf(TBCameraRig.instance.GetTBCenter());
            }
        }

        /// <summary>
        /// Is the camera parented to TBCenter?
        /// </summary>
        /// <returns></returns>
        public bool IsParented()
        {
            return _isParented;
        }

        public void TakeScreenshot()
        {
            Debug.Log("<color=yellow>Saving your screenshot...</color>");

            // Set target render textures.
            _renderTexture = new RenderTexture(resWidth, resHeight, 24);
            _camera.targetTexture = _renderTexture;

            // Apply MSAA settings.
            int currentMSAA = QualitySettings.antiAliasing;
            if (forceMSAA)
                QualitySettings.antiAliasing = 8;

            // Render the screenshot.
            _camera.Render();
            RenderTexture.active = _renderTexture;

            // Restore old MSAA settings.
            QualitySettings.antiAliasing = currentMSAA;

            // Create the screenshot texture and write to it.
            Texture2D screenshot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);

            // Reset the target rendertextures.
            _camera.targetTexture = null;
            RenderTexture.active = null;

            // Save the screenshot.
            byte[] bytes = screenshot.EncodeToPNG();
            string filename = string.Format("{0}/{1}_{2}x{3}_{4}.png",
                                savePath,
                                prefix,
                                resWidth,
                                resHeight,
                                System.DateTime.Now.ToString("dd-MMM_HH-mm-ss"));
            System.IO.File.WriteAllBytes(filename, bytes);

            Debug.Log(string.Format("<color=green>Success!</color> Saved screenshot to: {0}", filename));

            // Open the screenshot, if we're supposed to.
            if (openAfterSaving)
                Application.OpenURL(filename);
        }
    }
}
#endif