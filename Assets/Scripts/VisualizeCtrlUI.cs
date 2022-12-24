using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VRM;

public class VisualizeCtrlUI : MonoBehaviour
{
    [SerializeField] Visuallizer visuallizer;
    [SerializeField] RawImage backGroundTexture;
    [SerializeField] Dropdown backTextureSelect;
    [SerializeField] Toggle mirrorModeToggle;
    [SerializeField] Toggle unityCaptureToggle;
    
    readonly string loadedImagePath = "/LoadedImages";
    readonly string backOffName = "None";
    Texture defaultTexture;

    void Start(){
        defaultTexture = Texture2D.blackTexture;
        backGroundTexture.texture = defaultTexture;
        backGroundTexture.texture.name = backOffName;
        
        CreateImageOptions();
        UnityCaptureSwitched();
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

    public async void VrmFileLoad(){
        string path = OpenFileName.ShowDialog("vrm");
        if(path == null) return;
        var extension = Path.GetExtension(path).ToLower();
        if(extension != ".vrm") return;

        var instance = await VrmUtility.LoadAsync(path);
        instance.ShowMeshes();
        visuallizer.SetAnimator(instance.GetComponent<Animator>());
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
        string path = OpenFileName.ShowDialog("png");
        if(path == null) return;
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
    }

    public void MirrorModeSwitched(){
        var unityCapture = Camera.main.GetComponent<UnityCapture>();
        unityCapture.MirrorMode = mirrorModeToggle.isOn ? UnityCapture.EMirrorMode.MirrorHorizontally : UnityCapture.EMirrorMode.Disabled;
    }

    public void UnityCaptureSwitched(){
        var unityCapture = Camera.main.GetComponent<UnityCapture>();
        unityCapture.enabled = unityCaptureToggle.isOn;
    }
}
