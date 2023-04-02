using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    [SerializeField] Dropdown workTypeSelect;

    readonly string loadedVrmsPath = "/VrmFiles";
    string defaultVrmPath;
    readonly string avatarPlayerPrefsKey = "SelectedVrmFileName";
    readonly string loadedImagePath = "/LoadedImages";
    readonly string defaultTextureName = "Default";
    readonly string backOffName = "None";
    readonly string backImagePlayerPrefsKey = "SelectedBackgroundImageFileName";


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
        defaultVrmPath = Application.streamingAssetsPath + "/DefaultSampleAvatar.vrm";
        CreateVrmDropdownOptions();
        CreateImageOptions();
        CaptureSwitched();
        CreateHolisticMocapTypeOptions();
        ChangeIsUpperBodyOnly();
        ChangeLookCamera();

        var lastOpenVrm = PlayerPrefs.GetString(avatarPlayerPrefsKey);
        var initVrm = string.IsNullOrEmpty(lastOpenVrm) ? defaultVrmPath : lastOpenVrm;
        ChangeVrmFromFileName(initVrm);
        for (int i = 0; i < vrmSelectDropdown.options.Count; i++)
        {
            var option = vrmSelectDropdown.options[i];
            if (option.text == initVrm)
            {
                vrmSelectDropdown.value = i;
                vrmSelectDropdown.RefreshShownValue();
                break;
            }
        }

        var lastOpenbackImage = PlayerPrefs.GetString(backImagePlayerPrefsKey);
        var initBackImage = string.IsNullOrEmpty(lastOpenbackImage) ? defaultTextureName : lastOpenbackImage;
        ChangeBackTextureFromFileName(initBackImage);
        for (int i = 0; i < backTextureSelect.options.Count; i++)
        {
            var option = backTextureSelect.options[i];
            if (option.text == initBackImage)
            {
                backTextureSelect.value = i;
                backTextureSelect.RefreshShownValue();
                break;
            }
        }

        InitWorkTypeDropdown();
    }

    void OnDestroy()
    {
        PlayerPrefs.SetString(avatarPlayerPrefsKey, vrmSelectDropdown.options[vrmSelectDropdown.value].text);
        PlayerPrefs.SetString(backImagePlayerPrefsKey, backTextureSelect.options[backTextureSelect.value].text);
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

    void InitWorkTypeDropdown()
    {
        var typeList = Enum.GetNames(typeof(HolisticMotionCaptureWorkType));
        var selectOptions = new List<string>();
        foreach (var type in typeList)
        {
            selectOptions.Add(type);
        }
        workTypeSelect.ClearOptions();
        workTypeSelect.AddOptions(selectOptions);

        var selectedType = (HolisticMotionCaptureWorkType)Enum.ToObject(typeof(HolisticMotionCaptureWorkType), workTypeSelect.value);
        visuallizer.SetWorkType(selectedType);
        workTypeSelect.onValueChanged.AddListener(selectValue =>
        {
            var selectedType = (HolisticMotionCaptureWorkType)Enum.ToObject(typeof(HolisticMotionCaptureWorkType), selectValue);
            visuallizer.SetWorkType(selectedType);
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
        ChangeVrmFromFileName(filename);
    }

    private async Task ChangeVrmFromFileName(string filename)
    {
        var path = Application.persistentDataPath + loadedVrmsPath + "/" + filename + ".vrm";
        if (filename == Path.GetFileNameWithoutExtension(defaultVrmPath))
        {
            path = defaultVrmPath;
        }
        await ChangeVrmFromPath(path);
    }

    private async Task ChangeVrmFromPath(string path)
    {
        var instance = await VrmUtility.LoadAsync(path);
        instance.ShowMeshes();
        var animator = instance.GetComponent<Animator>();
        visuallizer.SetAnimator(animator);
        Camera.main.transform.position = animator.GetBoneTransform(HumanBodyBones.Head).position + Vector3.forward * 0.8f;
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

            await ChangeVrmFromPath(path);

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
        ChangeBackTextureFromFileName(filename);
    }

    private void ChangeBackTextureFromFileName(string filename)
    {
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
