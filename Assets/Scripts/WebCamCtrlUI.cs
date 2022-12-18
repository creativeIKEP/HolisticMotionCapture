using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WebCamCtrlUI : MonoBehaviour
{
    [SerializeField] Dropdown webcamSelect;
    [SerializeField] InputField widthInput;
    [SerializeField] InputField heightInput;

    public Texture webCamImage{
        get{
            if(webCam != null) return webCam.inputImageTexture;
            return null;
        }
    }

    WebCamInput webCam;
    
    void Start()
    {
        var devices = WebCamTexture.devices;
        var webcamSelectOptions = new List<string>();
        foreach(var d in devices){
            if(d.name != "Unity Video Capture") webcamSelectOptions.Add(d.name);
        }
        webcamSelect.ClearOptions();
        webcamSelect.AddOptions(webcamSelectOptions);
    }

    void Update(){
        if(webCam != null) webCam.UpdateTexture();
    }

    public void CaptureSwitch(){
        if(webCam != null) {
            webCam.CaptureStop();
            webCam = null;
            return;
        }
        
        var webCamName = webcamSelect.options[webcamSelect.value].text;
        if(string.IsNullOrEmpty(widthInput.text) || string.IsNullOrEmpty(heightInput.text)){
            webCam = new WebCamInput(webCamName);
        }
        else{
            int w = int.Parse(widthInput.text);
            int h = int.Parse(heightInput.text);
            webCam = new WebCamInput(webCamName, w, h);
        }

        var size = webCam.CaptureStart();
        widthInput.text = size.x.ToString();
        heightInput.text = size.y.ToString();
    }

    void OnApplicationQuit(){
        if(webCam != null) webCam.CaptureStop();
    }
}
