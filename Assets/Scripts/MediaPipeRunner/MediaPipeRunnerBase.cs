using System;
using UnityEngine;

public abstract class MediaPipeRunnerBase : IDisposable
{
    public const int poseVertexCount = 33;
    public const int faceVertexCount = 468;
    public const int eyeVertexCount = 16;
    public const int irisVertexCount = 5;


    public abstract void Dispose();
    public abstract void ProcessImage(Texture inputTexture, HolisticMocapType mocapType = HolisticMocapType.full);
    public abstract Vector4 GetPoseLandmark(int index);
    public abstract Vector4 GetPoseWorldLandmark(int index);
    public abstract Vector3 GetFaceLandmark(int index);
    // index is must be [0, 15]
    public abstract Vector3 GetLeftEyeLandmark(int index);
    // index is must be [0, 15]
    public abstract Vector3 GetRightEyeLandmark(int index);
    // index is must be [0, 4]
    public abstract Vector3 GetLeftIrisLandmark(int index);
    // index is must be [0, 4]
    public abstract Vector3 GetRightIrisLandmark(int index);
    public abstract Vector3 GetLeftHandLandmark(int index);
    public abstract Vector3 GetRightHandLandmark(int index);
}