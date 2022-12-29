using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SFB;
using VRM;
#if UNITY_STANDALONE_OSX
using Klak.Syphon;
#endif

public class VisualizeCtrlUI : MonoBehaviour
{
    [SerializeField] Visuallizer visuallizer;
    [SerializeField] RawImage backGroundTexture;
    [SerializeField] Dropdown backTextureSelect;
    [SerializeField] GameObject captureUI;
    [SerializeField] Toggle mirrorModeToggle;
    [SerializeField] Toggle captureToggle;
    [SerializeField] Dropdown hmcTypeSelect;
    
    readonly string loadedImagePath = "/LoadedImages";
    readonly string backOffName = "None";
    Texture defaultTexture;

    void Awake() {
        #if UNITY_STANDALONE_WIN
        var unityCapture = Camera.main.gameObject.AddComponent<UnityCapture>();
        unityCapture.ResizeMode = UnityCapture.EResizeMode.LinearResize;
        unityCapture.HideWarnings = true;
        #elif UNITY_STANDALONE_OSX
        var syphon = Camera.main.gameObject.AddComponent<SyphonServer>();
        syphon.alphaSupport = true;
        Destroy(mirrorModeToggle.gameObject);
        #else
        Destroy(captureUI);
        #endif
    }

    void Start(){
        defaultTexture = Texture2D.blackTexture;
        backGroundTexture.texture = defaultTexture;
        backGroundTexture.texture.name = backOffName;
        
        CreateImageOptions();
        CaptureSwitched();
        CreateHolisticMocapTypeOptions();
    }

    void CreateImageOptions(){
        if(!Directory.Exists(Application.persistentDataPath + loadedImagePath)){
            Directory.CreateDirectory(Application.persistentDataPath + loadedImagePath);
        }
        var imagePathes = Directory.GetFiles(Application.persistentDataPath + loadedImagePath, "*.png");
        
        var backTextureSelectOptions = new List<string>();
        backTextureSelectOptions.Add(backOffName);
        foreach(var path in imagePathes){
            backTextureSelectOptions.Add(Path.GetFileName(path));
        }
        backTextureSelect.ClearOptions();
        backTextureSelect.AddOptions(backTextureSelectOptions);
    }
    
    void CreateHolisticMocapTypeOptions(){
        var holisticMocapTypeList = Enum.GetNames(typeof(HolisticMocapType));
        var selectOptions = new List<string>();
        foreach(var type in holisticMocapTypeList){
            selectOptions.Add(type);
        }
        hmcTypeSelect.ClearOptions();
        hmcTypeSelect.AddOptions(selectOptions);

        var selectedType = (HolisticMocapType)Enum.ToObject(typeof(HolisticMocapType), hmcTypeSelect.value);
        visuallizer.SetHolisticMocapType(selectedType);
        hmcTypeSelect.onValueChanged.AddListener(selectValue => {
            var selectedType = (HolisticMocapType)Enum.ToObject(typeof(HolisticMocapType), selectValue);
            visuallizer.SetHolisticMocapType(selectedType);
        });
    }

    public void VrmFileLoad(){
        var extensions = new []{
            new ExtensionFilter("VRM Files", "vrm")
        };
        StandaloneFileBrowser.OpenFilePanelAsync("Open File", "", extensions, false, async (pathes) => {
            if(pathes.Length <= 0) return;
            var path = pathes[0];
            // cancelしても pathes.Length == 1の時あり
            if(string.IsNullOrEmpty(path)) return;

            var extension = Path.GetExtension(path).ToLower();
            if(extension != ".vrm") return;

            var instance = await VrmUtility.LoadAsync(path);
            instance.ShowMeshes();
            visuallizer.SetAnimator(instance.GetComponent<Animator>());
        });
    }

    public void ChangeBackTexture(){
        var filename = backTextureSelect.options[backTextureSelect.value].text;
        if(filename == backOffName){
            backGroundTexture.texture = defaultTexture;
            return;
        }

        byte[] bytes = File.ReadAllBytes(Application.persistentDataPath + loadedImagePath + "/" + filename);
        Texture2D texture = new Texture2D(1, 1);
        bool isLoadSuccess = texture.LoadImage(bytes);
        if(!isLoadSuccess) return;

        backGroundTexture.texture = texture;
    }

    public void NewImageLoad(){
        var extensions = new []{
            new ExtensionFilter("Image Files", "png", "jpg", "jpeg")
        };
        StandaloneFileBrowser.OpenFilePanelAsync("Open File", "", extensions, false, (pathes) => {
            if(pathes.Length <= 0) return;
            var path = pathes[0];
            // cancelしても pathes.Length == 1の時あり
            if(string.IsNullOrEmpty(path)) return;

            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(1, 1);
            bool isLoadSuccess = texture.LoadImage(bytes);
            if(!isLoadSuccess) return;

            backGroundTexture.texture = texture;

            var filename = Path.GetFileNameWithoutExtension(path) + ".png";
            var savePath = Application.persistentDataPath + loadedImagePath + "/" + filename;
            File.WriteAllBytes(savePath, texture.EncodeToPNG());

            var option = new Dropdown.OptionData();
            option.text = filename;
            backTextureSelect.options.Add(option);
            backTextureSelect.value = backTextureSelect.options.Count - 1;
        });
    }

    public void MirrorModeSwitched(){
        #if UNITY_STANDALONE_WIN
        var unityCapture = Camera.main.GetComponent<UnityCapture>();
        unityCapture.MirrorMode = mirrorModeToggle.isOn ? UnityCapture.EMirrorMode.MirrorHorizontally : UnityCapture.EMirrorMode.Disabled;
        #endif
    }

    public void CaptureSwitched(){
        #if UNITY_STANDALONE_WIN
        var unityCapture = Camera.main.GetComponent<UnityCapture>();        
        unityCapture.enabled = captureToggle.isOn;
        #elif UNITY_STANDALONE_OSX
        var syphon = Camera.main.GetComponent<SyphonServer>();        
        syphon.enabled = captureToggle.isOn;
        #endif
    }
}
