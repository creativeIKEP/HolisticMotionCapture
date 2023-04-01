using System;
using UnityEngine;
using HolisticMotionCapture;

public interface IMediaPipeRunner : IDisposable
{
    void ProcessImage(Texture inputTexture, HolisticMocapType mocapType = HolisticMocapType.full);

    Vector4 GetPoseLandmark(int index);
    Vector4 GetPoseWorldLandmark(int index);

    Vector3 GetFaceLandmark(int index);
    // index is must be [0, 15]
    Vector3 GetLeftEyeLandmark(int index);
    // index is must be [0, 15]
    Vector3 GetRightEyeLandmark(int index);

    // index is must be [0, 4]
    Vector3 GetLeftIrisLandmark(int index);
    // index is must be [0, 4]
    Vector3 GetRightIrisLandmark(int index);

    Vector3 GetLeftHandLandmark(int index);
    Vector3 GetRightHandLandmark(int index);
}