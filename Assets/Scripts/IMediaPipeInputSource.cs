using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IMediaPipeInputSource
{
    Texture inputImageTexture { get; }
    UniTask<Vector2> CaptureStartAsync(CancellationToken token);
    void CaptureStop();
}