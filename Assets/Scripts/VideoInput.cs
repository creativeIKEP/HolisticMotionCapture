using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;

public class VideoInput : IMediaPipeInputSource
{
    // Provide input image Texture.
    public Texture inputImageTexture => _inputRT;

    private static VideoPlayer _video;
    private RenderTexture _inputRT;

    public VideoInput(string videoUrl)
    {
        if (_video == null)
        {
            var go = new GameObject("VideoInput");
            _video = go.AddComponent<VideoPlayer>();
        }
        _video.playOnAwake = false;
        _video.isLooping = true;
        _video.url = videoUrl;
        _video.renderMode = VideoRenderMode.RenderTexture;
    }

    public async UniTask<Vector2> CaptureStartAsync(CancellationToken token)
    {
        _video.Prepare();
        await UniTask.WaitUntil(() => _video.isPrepared, cancellationToken: token);
        _inputRT = new RenderTexture((int)_video.width, (int)_video.height, 0);
        _video.targetTexture = _inputRT;
        _video.Play();
        return new Vector2(_video.width, _video.height);
    }

    public void CaptureStop()
    {
        if (_video != null) _video.Stop();
        if (_inputRT != null) _inputRT.Release();
    }
}