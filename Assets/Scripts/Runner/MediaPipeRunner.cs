using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UniRx;
using Mediapipe;
using Mediapipe.Unity;

public class MediaPipeRunner : IDisposable
{
    public NormalizedLandmarkList poseLandmarks { get; private set; }
    public LandmarkList poseWorldLandmarks { get; private set; }
    public NormalizedLandmarkList faceLandmarks { get; private set; }
    public NormalizedLandmarkList leftHandLandmarks { get; private set; }
    public NormalizedLandmarkList rightHandLandmarks { get; private set; }

    private bool _isSetup;
    CompositeDisposable _disposables = new CompositeDisposable();
    private int _inputWidth;
    private int _inputHeight;
    private Texture2D _inputTexture;
    private RenderTexture _rtForInput;

    private ResourceManager _resourceManager;
    private Stopwatch _stopwatch;
    private CalculatorGraph _graph;

    private OutputStream<NormalizedLandmarkListPacket, NormalizedLandmarkList> _poseLandmarksStream;
    private OutputStream<LandmarkListPacket, LandmarkList> _poseWorldLandmarksStream;
    private OutputStream<NormalizedLandmarkListPacket, NormalizedLandmarkList> _faceLandmarksStream;
    private OutputStream<NormalizedLandmarkListPacket, NormalizedLandmarkList> _leftHandLandmarksStream;
    private OutputStream<NormalizedLandmarkListPacket, NormalizedLandmarkList> _rightHandLandmarksStream;


    private const string _graphTextAssetName = "holistic_graph";
    private const string _graphInpustStreamName = "input_video";
    private const string _graphRefineFaceLandmarksSidePacketName = "refine_face_landmarks";
    private const string _graphInputRotationSidePacketName = "input_rotation";
    private const string _graphInputVerticallyFlippedSidePacketName = "input_vertically_flipped";
    private const string _graphInputHorizontallyFlippedSidePacketName = "input_horizontally_flipped";
    private const string _graphPoseLandmarksStreamName = "pose_landmarks";
    private const string _graphPoseWorldLandmarksStreamName = "pose_world_landmarks";
    private const string _graphFaceLandmarksStreamName = "face_landmarks";
    private const string _graphLeftHandLandmarksStreamName = "left_hand_landmarks";
    private const string _graphRightHandLandmarksStreamName = "right_hand_landmarks";

    public MediaPipeRunner(bool isMirror)
    {
        _isSetup = false;
        Observable.FromCoroutine(_ => SetUp(isMirror)).Subscribe(_ => { }, () => { _isSetup = true; }).AddTo(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();

        _poseLandmarksStream?.RemoveAllListeners();
        _poseLandmarksStream?.Close();
        _poseLandmarksStream = null;

        _poseWorldLandmarksStream?.RemoveAllListeners();
        _poseWorldLandmarksStream?.Close();
        _poseWorldLandmarksStream = null;

        _faceLandmarksStream?.RemoveAllListeners();
        _faceLandmarksStream?.Close();
        _faceLandmarksStream = null;

        _leftHandLandmarksStream?.RemoveAllListeners();
        _leftHandLandmarksStream?.Close();
        _leftHandLandmarksStream = null;

        _rightHandLandmarksStream?.RemoveAllListeners();
        _rightHandLandmarksStream?.Close();
        _rightHandLandmarksStream = null;

        if (_graph != null)
        {
            _graph.CloseInputStream(_graphInpustStreamName).AssertOk();
            _graph.WaitUntilDone().AssertOk();
            _graph.Dispose();
        }

        if (_inputTexture != null)
        {
            GameObject.Destroy(_inputTexture);
            _inputTexture = null;
        }
        if (_rtForInput != null)
        {
            GameObject.Destroy(_rtForInput);
            _rtForInput = null;
        }
    }

    public void ProcessImage(Texture inputTexture)
    {
        if (!_isSetup) return;

        if (_inputWidth != inputTexture.width || _inputHeight != inputTexture.height)
        {
            if (_inputTexture != null)
            {
                GameObject.Destroy(_inputTexture);
                _inputTexture = null;
            }
            if (_rtForInput != null)
            {
                GameObject.Destroy(_rtForInput);
                _rtForInput = null;
            }
        }
        if (_inputTexture == null)
        {
            _inputTexture = new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGBA32, false);

        }
        if (_rtForInput == null)
        {
            _rtForInput = new RenderTexture(inputTexture.width, inputTexture.height, 32);
        }
        _inputWidth = inputTexture.width;
        _inputHeight = inputTexture.height;

        // copy texture
        var currentRT = RenderTexture.active;
        Graphics.Blit(inputTexture, _rtForInput);
        RenderTexture.active = _rtForInput;
        var source = new UnityEngine.Rect(0, 0, _rtForInput.width, _rtForInput.height);
        _inputTexture.ReadPixels(source, 0, 0);
        _inputTexture.Apply();
        RenderTexture.active = currentRT;

        var imageFrame = new ImageFrame(ImageFormat.Types.Format.Srgba, _inputWidth, _inputHeight, _inputWidth * 4, _inputTexture.GetRawTextureData<byte>());
        var currentTimestamp = _stopwatch.ElapsedTicks / (System.TimeSpan.TicksPerMillisecond / 1000);
        _graph.AddPacketToInputStream(_graphInpustStreamName, new ImageFramePacket(imageFrame, new Timestamp(currentTimestamp))).AssertOk();
    }

    private IEnumerator SetUp(bool isMirror)
    {
        _resourceManager = new StreamingAssetsResourceManager("MediaPipeUnityPluginModels");
        yield return _resourceManager.PrepareAssetAsync("pose_detection.bytes");
        yield return _resourceManager.PrepareAssetAsync("pose_landmark_full.bytes");
        yield return _resourceManager.PrepareAssetAsync("face_detection_short_range.bytes");
        yield return _resourceManager.PrepareAssetAsync("face_landmark_with_attention.bytes");
        yield return _resourceManager.PrepareAssetAsync("hand_recrop.bytes");
        yield return _resourceManager.PrepareAssetAsync("hand_landmark_full.bytes");
        yield return _resourceManager.PrepareAssetAsync("handedness.txt");

        var graphTextAsset = Resources.Load(_graphTextAssetName) as TextAsset;
        _graph = new CalculatorGraph(graphTextAsset.text);

        var sidePacket = new SidePacket();
        sidePacket.Emplace(_graphInputRotationSidePacketName, new IntPacket(0));
        sidePacket.Emplace(_graphInputVerticallyFlippedSidePacketName, new BoolPacket(true));
        sidePacket.Emplace(_graphInputHorizontallyFlippedSidePacketName, new BoolPacket(isMirror));
        sidePacket.Emplace(_graphRefineFaceLandmarksSidePacketName, new BoolPacket(true));

        _poseLandmarksStream = new OutputStream<NormalizedLandmarkListPacket, NormalizedLandmarkList>(_graph, _graphPoseLandmarksStreamName);
        _poseLandmarksStream.AddListener(OnPoseLandmarksOutput);
        _poseWorldLandmarksStream = new OutputStream<LandmarkListPacket, LandmarkList>(_graph, _graphPoseWorldLandmarksStreamName);
        _poseWorldLandmarksStream.AddListener(OnPoseWorldLandmarksOutput);
        _faceLandmarksStream = new OutputStream<NormalizedLandmarkListPacket, NormalizedLandmarkList>(_graph, _graphFaceLandmarksStreamName);
        _faceLandmarksStream.AddListener(OnFaceLandmarksOutput);
        _leftHandLandmarksStream = new OutputStream<NormalizedLandmarkListPacket, NormalizedLandmarkList>(_graph, _graphLeftHandLandmarksStreamName);
        _leftHandLandmarksStream.AddListener(OnLeftHandLandmarksOutput);
        _rightHandLandmarksStream = new OutputStream<NormalizedLandmarkListPacket, NormalizedLandmarkList>(_graph, _graphRightHandLandmarksStreamName);
        _rightHandLandmarksStream.AddListener(OnRightHandLandmarksOutput);

        _graph.StartRun(sidePacket).AssertOk();

        _stopwatch = new Stopwatch();
        _stopwatch.Start();
    }

    private void OnPoseLandmarksOutput(object stream, OutputEventArgs<NormalizedLandmarkList> eventArgs)
    {
        if (eventArgs.value == null) return;
        poseLandmarks = eventArgs.value;
    }

    private void OnPoseWorldLandmarksOutput(object stream, OutputEventArgs<LandmarkList> eventArgs)
    {
        if (eventArgs.value == null) return;
        poseWorldLandmarks = eventArgs.value;
    }

    private void OnFaceLandmarksOutput(object stream, OutputEventArgs<NormalizedLandmarkList> eventArgs)
    {
        if (eventArgs.value == null) return;
        faceLandmarks = eventArgs.value;
    }

    private void OnLeftHandLandmarksOutput(object stream, OutputEventArgs<NormalizedLandmarkList> eventArgs)
    {
        if (eventArgs.value == null) return;
        leftHandLandmarks = eventArgs.value;
    }

    private void OnRightHandLandmarksOutput(object stream, OutputEventArgs<NormalizedLandmarkList> eventArgs)
    {
        if (eventArgs.value == null) return;
        rightHandLandmarks = eventArgs.value;
    }
}