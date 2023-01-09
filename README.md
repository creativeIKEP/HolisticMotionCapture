# HolisticMotionCapture

![demo](https://user-images.githubusercontent.com/34697515/211329841-83ea990d-a597-44f1-a252-efaca8a38cb7.gif)

HolisticMotionCapture is an application that can capture the motion of a person with only a monocular color camera and move the VRM avatar's pose, face, and hands.

Pose, face and hands can be moved simultaneously or individually.

You can also output the rendered video to other applications.

## Install

hoge

## Usage

### Load your VRM file

Push the `Load VRM` for loading your VRM file.

### Image Device

1. Select source input camera device from `Input Device` pull down.
2. (Option) Set the resolution of camera images in `W` and `H` input field.
3. Push the `Start/Stop` button for starting or stopping camera capture.

### BackGround Select

- You can change the background image from pull down.
- Images are loaded from and save to `C:/Users/<user name>/AppData/LocalLow/IKEP/HolisticMotionCapture/LoadedImages` directory in Windows.
- Images are loaded from and save to `/Users/<user name>/Library/Application Support/IKEP/HolisticMotionCapture/LoadedImages` directory in Mac OS.
- You can output black image if you select the `None` option from pull down.
- You can load new images as the background image from the `New image` button.
  HolisticMotionCapture is supported PNG(`.png`) or JPG(`.jpg`, `.jpeg`) images.
  - You can continue to use the image is loaded once, if application is restarting.

### Output Control

#### For Windows

Rendered images can be output as the virtual camera image if `Output on/off` toggle is on.
You can show composited image in another applications when you select a camera named `HolisticMotionCapture` in another applications.

#### For Mac OS

Rendered images can be output as the [Syphon](https://syphon.info/) image if `Output on/off` toggle is on.
You can receive composited image in another applications compatible with Syphon.

### Avatar Control

- `Mode`: You can choose to move your avatar's pose, face, or hands.
- `Reset pose` button: You can reset your avatar pose.
- `Move upper body only` toggle: You can choose to move only the upper body or move the whole body. It is useful when only your upper body is shown in the camera such as video conferencing.
- `Look Camera` toggle: You can choose whether your avatar should always look at the camera or not.
- Mouse Operation
  - The operation UI can be displayed/hidden with a mouse click.
  - You can move the virtual camera by mouse dragging and mouse scrolling.

## For Developers

HolisticMotionCapture provides a package to move VRM avatars independently of my application.

### Install HolisticMotionCapture package

HolisticMotionCapture package can be installed by adding following sections to your manifest file (`Packages/manifest.json`).

To the `scopedRegistries` section:

```
{
    "name": "Keijiro",
    "url": "https://registry.npmjs.com",
    "scopes": [ "jp.keijiro" ]
},
{
  "name": "creativeikep",
  "url": "https://registry.npmjs.com",
  "scopes": [ "jp.ikep" ]
}
```

To the `dependencies` section:

```
"jp.ikep.holistic-motion-capture": "1.0.0"
```

Finally, the manifest file looks like below:

```
{
    "scopedRegistries": [
        {
            "name": "Keijiro",
            "url": "https://registry.npmjs.com",
            "scopes": [ "jp.keijiro" ]
        },
        {
            "name": "creativeikep",
            "url": "https://registry.npmjs.com",
            "scopes": [ "jp.ikep" ]
        }
    ],
    "dependencies": {
        "jp.ikep.holistic-motion-capture": "1.0.0",
        ...
    }
}
```

### Usage demo HolisticMotionCapture package

```cs
using UnityEngine;
using HolisticMotionCapture;

public class Visualizer : MonoBehaviour
{
    // Animator of VRM avatar
    [SerializeField] Animator avatar;

    HolisticMotionCapturePipeline motionCapture;
    WebCamTexture webCam;

    void Start()
    {
        // Initialize
        motionCapture = new HolisticMotionCapturePipeline(avatar);

        webCam = new WebCamTexture("Your webcam name", width, height);
        webCam.Play();
    }

    void Update()
    {
        // Ability to operate avatars with textures only.
        // You can also specify some optional arguments.
        motionCapture.AvatarPoseRender(webCam);
    }
}

```

## Demo image

Video for demo was downloaded from [here](https://www.pexels.com/ja-jp/video/5089491/)

## Dependencies

HolisticMotionCapture **package** uses the following packages:

- [HolisticBarracuda](https://github.com/creativeIKEP/HolisticBarracuda)
- [UniVRM](https://github.com/vrm-c/UniVRM)
  - HolisticMotionCapture includes source codes of UniVRM. It's same that you write below packages to your manifest.json.
    ```
    "com.vrmc.vrmshaders": "https://github.com/vrm-c/UniVRM.git?path=/Assets/VRMShaders#v0.108.0",
    "com.vrmc.gltf": "https://github.com/vrm-c/UniVRM.git?path=/Assets/UniGLTF#v0.108.0",
    "com.vrmc.univrm": "https://github.com/vrm-c/UniVRM.git?path=/Assets/VRM#v0.108.0"
    ```
  - HolisticMotionCapture is not yet compatible with VRM 1.0.

HolisticMotionCapture **application** also uses the following packages:

- [Unity Capture](https://github.com/creativeIKEP/UnityCapture/tree/HolisticMotionCaptureCamera)(This is customized for HolisticMotionCapture. Original Unity Capture is [here](https://github.com/schellingb/UnityCapture))
- [KlakSyphon](https://github.com/keijiro/KlakSyphon)
  - [LICENSE](/Syphon-LICENSE)
- [Unity Standalone File Browser](https://github.com/gkngkc/UnityStandaloneFileBrowser)

## Author

[IKEP](https://ikep.jp)

## LICENSE

Copyright (c) 2023 IKEP

[Apache-2.0](/LICENSE.md)
