using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniRx;

public class WebCamInput: IMediaPipeInputSource
{
    // Provide input image Texture.
    public Texture inputImageTexture => _inputRT;

    private readonly WebCamTexture _webCamTexture;
    private RenderTexture _inputRT;
    private readonly CompositeDisposable _compositeDisposable;


    public WebCamInput(string webCamName)
    {
        _webCamTexture = new WebCamTexture(webCamName);
        _compositeDisposable = new CompositeDisposable();
    }

    public WebCamInput(string webCamName, int w, int h)
    {
        _webCamTexture = new WebCamTexture(webCamName, w, h);
        _compositeDisposable = new CompositeDisposable();
    }

    public async UniTask<Vector2> CaptureStartAsync(CancellationToken token)
    {
        _webCamTexture.Play();
        await UniTask.WaitWhile(() => _webCamTexture.width < 100, cancellationToken: token);
        _inputRT = new RenderTexture(_webCamTexture.width, _webCamTexture.height, 0);

        Observable.EveryUpdate().Subscribe(_ => UpdateTexture()).AddTo(_compositeDisposable);
        
        return new Vector2(_webCamTexture.width, _webCamTexture.height);
    }

    public void CaptureStop()
    {
        if (_webCamTexture != null) _webCamTexture.Stop();
        if (_inputRT != null) _inputRT.Release();
        _compositeDisposable.Clear();
    }

    private void UpdateTexture()
    {
        if (_webCamTexture == null || _inputRT == null) return;
        if (!_webCamTexture.didUpdateThisFrame) return;

        var aspect1 = (float)_webCamTexture.width / _webCamTexture.height;
        var aspect2 = (float)_inputRT.width / _inputRT.height;
        var aspectGap = aspect2 / aspect1;

        var vMirrored = _webCamTexture.videoVerticallyMirrored;
        var scale = new Vector2(aspectGap, vMirrored ? -1 : 1);
        var offset = new Vector2((1 - aspectGap) / 2, vMirrored ? 1 : 0);

        Graphics.Blit(_webCamTexture, _inputRT, scale, offset);
    }
}
