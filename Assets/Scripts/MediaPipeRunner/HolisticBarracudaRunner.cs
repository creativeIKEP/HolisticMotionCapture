using UnityEngine;
using MediaPipe.Holistic;
using Mediapipe.BlazePose;

public class HolisticBarracudaRunner : MediaPipeRunnerBase
{
    public HolisticPipeline holisticPipeline { get; private set; }

    public HolisticBarracudaRunner(BlazePoseModel blazePoseModel = BlazePoseModel.full)
    {
        holisticPipeline = new HolisticPipeline(blazePoseModel);
    }

    public override void Dispose()
    {
        holisticPipeline.Dispose();
    }

    public override void ProcessImage(Texture inputTexture, HolisticMocapType mocapType)
    {
        holisticPipeline.ProcessImage(inputTexture, (HolisticInferenceType)mocapType);
    }

    public override Vector4 GetPoseLandmark(int index)
    {
        var l = holisticPipeline.GetPoseLandmark(index);
        return new Vector4(-l.x, l.y, -l.z, l.w);
    }

    public override Vector4 GetPoseWorldLandmark(int index)
    {
        var l = holisticPipeline.GetPoseWorldLandmark(index);
        return new Vector4(-l.x, l.y, -l.z, l.w);
    }

    public override Vector3 GetFaceLandmark(int index)
    {
        var l = holisticPipeline.GetFaceLandmark(index);
        return new Vector4(-l.x, l.y, -l.z, l.w);
    }

    // index is must be [0, 15]
    public override Vector3 GetLeftEyeLandmark(int index)
    {
        // [0 - 4] : Iris vertices
        // [5 - 75] : Eyelid and eyebrow vertices
        return holisticPipeline.GetLeftEyeLandmark(index + irisVertexCount);
    }

    // index is must be [0, 15]
    public override Vector3 GetRightEyeLandmark(int index)
    {
        // [0 - 4] : Iris vertices
        // [5 - 75] : Eyelid and eyebrow vertices
        return holisticPipeline.GetRightEyeLandmark(index + irisVertexCount);
    }

    // index is must be [0, 4]
    public override Vector3 GetLeftIrisLandmark(int index)
    {
        // [0 - 4] : Iris vertices
        // [5 - 75] : Eyelid and eyebrow vertices
        return holisticPipeline.GetLeftEyeLandmark(index);
    }

    // index is must be [0, 4]
    public override Vector3 GetRightIrisLandmark(int index)
    {
        // [0 - 4] : Iris vertices
        // [5 - 75] : Eyelid and eyebrow vertices
        return holisticPipeline.GetRightEyeLandmark(index);
    }

    public override Vector3 GetLeftHandLandmark(int index)
    {
        var l = holisticPipeline.GetLeftHandLandmark(index);
        return new Vector3(-l.x, l.y, -l.z);
    }

    public override Vector3 GetRightHandLandmark(int index)
    {
        var l = holisticPipeline.GetRightHandLandmark(index);
        return new Vector3(-l.x, l.y, -l.z);
    }
}