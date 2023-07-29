using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using SFB;
using UnityEngine;
using UnityEngine.UI;

public class WebCamCtrlUI : MonoBehaviour
{
    [SerializeField] Dropdown webcamSelect;
    [SerializeField] InputField widthInput;
    [SerializeField] InputField heightInput;

    public Texture webCamImage
    {
        get
        {
            if (webCam != null) return webCam.inputImageTexture;
            return null;
        }
    }

    private const string VideoSelectMenuName = "Input from video...";
    
    IMediaPipeInputSource webCam;

    void Start()
    {
        var devices = WebCamTexture.devices;
        var webcamSelectOptions = new List<string>();
        foreach (var d in devices)
        {
            if (d.name != "HolisticMotionCapture") webcamSelectOptions.Add(d.name);
        }
        webcamSelectOptions.Add(VideoSelectMenuName);
        webcamSelect.ClearOptions();
        webcamSelect.AddOptions(webcamSelectOptions);
    }

    public void CaptureSwitch()
    {
        CaptureSwitchAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTask CaptureSwitchAsync(CancellationToken token)
    {
        if (webCam != null)
        {
            webCam.CaptureStop();
            webCam = null;
            return;
        }

        var webCamName = webcamSelect.options[webcamSelect.value].text;
        if (webCamName == VideoSelectMenuName)
        {
            var extensions = new[]{
                new ExtensionFilter("Video Files", "mp4"),
            };
            StandaloneFileBrowser.OpenFilePanelAsync("Open File", "", extensions, false, async (paths) =>
            {
                if (paths.Length <= 0) return;
                var path = paths[0];
                // cancelしても paths.Length == 1の時あり
                if (string.IsNullOrEmpty(path)) return;

                var extension = Path.GetExtension(path).ToLower();
                if (extension != ".mp4") return;
                
                webCam = new VideoInput(path);
                var videoSize = await webCam.CaptureStartAsync(token);
                widthInput.text = videoSize.x.ToString();
                heightInput.text = videoSize.y.ToString();
            });
            return;
        }
        
        if (string.IsNullOrEmpty(widthInput.text) || string.IsNullOrEmpty(heightInput.text))
        {
            webCam = new WebCamInput(webCamName);
        }
        else
        {
            int w = int.Parse(widthInput.text);
            int h = int.Parse(heightInput.text);
            webCam = new WebCamInput(webCamName, w, h);
        }
        var textureSize = await webCam.CaptureStartAsync(token);
        widthInput.text = textureSize.x.ToString();
        heightInput.text = textureSize.y.ToString();
    }

    void OnApplicationQuit()
    {
        if (webCam != null) webCam.CaptureStop();
    }
}
