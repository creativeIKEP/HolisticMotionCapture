using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using HolisticMotionCapture;
using SFB;
using VRM;
#if UNITY_STANDALONE_OSX
using Klak.Syphon;
#endif

public class VisualizeCtrlUI : MonoBehaviour
{
    [SerializeField] Visuallizer visuallizer;
    [SerializeField] Dropdown vrmSelectDropdown;
    [SerializeField] Texture defaultTexture;
    [SerializeField] RawImage backGroundTexture;
    [SerializeField] Dropdown backTextureSelect;
    [SerializeField] GameObject captureUI;
    [SerializeField] Toggle mirrorModeToggle;
    [SerializeField] Toggle captureToggle;
    [SerializeField] Dropdown hmcTypeSelect;
    [SerializeField] Toggle isUpperBodyOnlyToggle;
    [SerializeField] Toggle lookCameraToggle;

    readonly string loadedVrmsPath = "/VrmFiles";
    readonly string loadedImagePath = "/LoadedImages";
    readonly string defaultTextureName = "Default";
    readonly string backOffName = "None";
    string defaultVrmPath;

    void Awake()
    {
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

    void Start()
    {
        defaultVrmPath = Application.dataPath + "/SampleAvatar/SampleAvatar.vrm";
        ChangeVrm(defaultVrmPath);
        backGroundTexture.texture = defaultTexture;
        backGroundTexture.texture.name = defaultTextureName;

        CreateVrmDropdownOptions();
        CreateImageOptions();
        CaptureSwitched();
        CreateHolisticMocapTypeOptions();
        ChangeIsUpperBodyOnly();
        ChangeLookCamera();
    }

    void CreateVrmDropdownOptions()
    {
        if (!Directory.Exists(Application.persistentDataPath + loadedVrmsPath))
        {
            Directory.CreateDirectory(Application.persistentDataPath + loadedVrmsPath);
        }
        var vrmPathes = Directory.GetFiles(Application.persistentDataPath + loadedVrmsPath, "*.vrm");

        var vrmSelectOptions = new List<string>();
        vrmSelectOptions.Add(Path.GetFileNameWithoutExtension(defaultVrmPath));
        foreach (var path in vrmPathes)
        {
            vrmSelectOptions.Add(Path.GetFileNameWithoutExtension(path));
        }
        vrmSelectDropdown.ClearOptions();
        vrmSelectDropdown.AddOptions(vrmSelectOptions);
    }

    void CreateImageOptions()
    {
        if (!Directory.Exists(Application.persistentDataPath + loadedImagePath))
        {
            Directory.CreateDirectory(Application.persistentDataPath + loadedImagePath);
        }
        var imagePathes = Directory.GetFiles(Application.persistentDataPath + loadedImagePath, "*.png");

        var backTextureSelectOptions = new List<string>();
        backTextureSelectOptions.Add(defaultTextureName);
        backTextureSelectOptions.Add(backOffName);
        foreach (var path in imagePathes)
        {
            backTextureSelectOptions.Add(Path.GetFileName(path));
        }
        backTextureSelect.ClearOptions();
        backTextureSelect.AddOptions(backTextureSelectOptions);
    }

    void CreateHolisticMocapTypeOptions()
    {
        var holisticMocapTypeList = Enum.GetNames(typeof(HolisticMocapType));
        var selectOptions = new List<string>();
        foreach (var type in holisticMocapTypeList)
        {
            selectOptions.Add(type);
        }
        hmcTypeSelect.ClearOptions();
        hmcTypeSelect.AddOptions(selectOptions);

        var selectedType = (HolisticMocapType)Enum.ToObject(typeof(HolisticMocapType), hmcTypeSelect.value);
        visuallizer.SetHolisticMocapType(selectedType);
        hmcTypeSelect.onValueChanged.AddListener(selectValue =>
        {
            var selectedType = (HolisticMocapType)Enum.ToObject(typeof(HolisticMocapType), selectValue);
            visuallizer.SetHolisticMocapType(selectedType);
        });
    }

    public void ChangeIsUpperBodyOnly()
    {
        visuallizer.SetIsUpperBodyOnly(isUpperBodyOnlyToggle.isOn);
    }

    public void ChangeLookCamera()
    {
        visuallizer.SetMainCameraLook(lookCameraToggle.isOn);
    }

    public void ChangeVrmFromDropdownUi()
    {
        var filename = vrmSelectDropdown.options[vrmSelectDropdown.value].text;
        var path = Application.persistentDataPath + loadedVrmsPath + "/" + filename + ".vrm";
        ChangeVrm(path);
    }

    private async Task ChangeVrm(string path)
    {
        var instance = await VrmUtility.LoadAsync(path);
        instance.ShowMeshes();
        visuallizer.SetAnimator(instance.GetComponent<Animator>());
    }

    public void VrmFileLoad()
    {
        var extensions = new[]{
            new ExtensionFilter("VRM Files", "vrm")
        };
        StandaloneFileBrowser.OpenFilePanelAsync("Open File", "", extensions, false, async (pathes) =>
        {
            if (pathes.Length <= 0) return;
            var path = pathes[0];
            // cancelしても pathes.Length == 1の時あり
            if (string.IsNullOrEmpty(path)) return;

            var extension = Path.GetExtension(path).ToLower();
            if (extension != ".vrm") return;

            await ChangeVrm(path);

            var filename = Path.GetFileNameWithoutExtension(path) + ".vrm";
            var savePath = Application.persistentDataPath + loadedVrmsPath + "/" + filename;
            File.Copy(path, savePath, true);

            var option = new Dropdown.OptionData();
            option.text = Path.GetFileNameWithoutExtension(path);
            vrmSelectDropdown.options.Add(option);
            vrmSelectDropdown.value = vrmSelectDropdown.options.Count - 1;
            vrmSelectDropdown.RefreshShownValue();
        });
    }

    public void ChangeBackTexture()
    {
        var filename = backTextureSelect.options[backTextureSelect.value].text;
        if (filename == defaultTexture.name)
        {
            backGroundTexture.texture = defaultTexture;
            return;
        }
        if (filename == backOffName)
        {
            backGroundTexture.texture = Texture2D.blackTexture;
            return;
        }

        byte[] bytes = File.ReadAllBytes(Application.persistentDataPath + loadedImagePath + "/" + filename);
        Texture2D texture = new Texture2D(1, 1);
        bool isLoadSuccess = texture.LoadImage(bytes);
        if (!isLoadSuccess) return;

        backGroundTexture.texture = texture;
    }

    public void NewImageLoad()
    {
        var extensions = new[]{
            new ExtensionFilter("Image Files", "png", "jpg", "jpeg")
        };
        StandaloneFileBrowser.OpenFilePanelAsync("Open File", "", extensions, false, (pathes) =>
        {
            if (pathes.Length <= 0) return;
            var path = pathes[0];
            // cancelしても pathes.Length == 1の時あり
            if (string.IsNullOrEmpty(path)) return;

            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(1, 1);
            bool isLoadSuccess = texture.LoadImage(bytes);
            if (!isLoadSuccess) return;

            backGroundTexture.texture = texture;

            var filename = Path.GetFileNameWithoutExtension(path) + ".png";
            var savePath = Application.persistentDataPath + loadedImagePath + "/" + filename;
            File.WriteAllBytes(savePath, texture.EncodeToPNG());

            var option = new Dropdown.OptionData();
            option.text = filename;
            backTextureSelect.options.Add(option);
            backTextureSelect.value = backTextureSelect.options.Count - 1;
            backTextureSelect.RefreshShownValue();
        });
    }

    public void MirrorModeSwitched()
    {
#if UNITY_STANDALONE_WIN
        var unityCapture = Camera.main.GetComponent<UnityCapture>();
        unityCapture.MirrorMode = mirrorModeToggle.isOn ? UnityCapture.EMirrorMode.MirrorHorizontally : UnityCapture.EMirrorMode.Disabled;
#endif
    }

    public void CaptureSwitched()
    {
#if UNITY_STANDALONE_WIN
        var unityCapture = Camera.main.GetComponent<UnityCapture>();
        unityCapture.enabled = captureToggle.isOn;
#elif UNITY_STANDALONE_OSX
        var syphon = Camera.main.GetComponent<SyphonServer>();        
        syphon.enabled = captureToggle.isOn;
#endif
    }
}
