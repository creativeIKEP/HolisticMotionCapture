using UnityEngine;
using MediaPipe.Holistic;
using Mediapipe.BlazePose;
using HolisticMotionCapture;

public class HolisticBarracudaRunner: IMediaPipeRunner
{
    private HolisticPipeline holisticPipeline;

    public HolisticBarracudaRunner(bool isMirror, BlazePoseModel blazePoseModel = BlazePoseModel.full){
        // TODO: use isMirror
        holisticPipeline = new HolisticPipeline(blazePoseModel);
    }

    public void Dispose(){
        holisticPipeline.Dispose();
    }

    public void ProcessImage(Texture inputTexture, HolisticMocapType mocapType){
        holisticPipeline.ProcessImage(inputTexture, (HolisticInferenceType)mocapType);
    }

    public Vector4 GetPoseLandmark(int index){
        var l = holisticPipeline.GetPoseLandmark(index);
        return new Vector4(-l.x, l.y, -l.z, l.w);
    }

    public Vector4 GetPoseWorldLandmark(int index){
        var l = holisticPipeline.GetPoseWorldLandmark(index);
        return new Vector4(-l.x, l.y, -l.z, l.w);
    }

    public Vector3 GetFaceLandmark(int index){
        return holisticPipeline.GetFaceLandmark(index);
    }

    // index is must be [0, 15]
    public Vector3 GetLeftEyeLandmark(int index){
        // [0 - 4] : Iris vertices
        // [5 - 75] : Eyelid and eyebrow vertices
        return holisticPipeline.GetLeftEyeLandmark(index + 5);
    }
    
    // index is must be [0, 15]
    public Vector3 GetRightEyeLandmark(int index){
        // [0 - 4] : Iris vertices
        // [5 - 75] : Eyelid and eyebrow vertices
        return holisticPipeline.GetRightEyeLandmark(index + 5);
    }

    // index is must be [0, 4]
    public Vector3 GetLeftIrisLandmark(int index){
        // [0 - 4] : Iris vertices
        // [5 - 75] : Eyelid and eyebrow vertices
        return holisticPipeline.GetLeftEyeLandmark(index);
    }

    // index is must be [0, 4]
    public Vector3 GetRightIrisLandmark(int index){
        // [0 - 4] : Iris vertices
        // [5 - 75] : Eyelid and eyebrow vertices
        return holisticPipeline.GetRightEyeLandmark(index);
    }

    public Vector3 GetLeftHandLandmark(int index){
        var l = holisticPipeline.GetLeftHandLandmark(index);
        return new Vector3(-l.x, l.y, -l.z);
    }

    public Vector3 GetRightHandLandmark(int index){
        var l = holisticPipeline.GetRightHandLandmark(index);
        return new Vector3(-l.x, l.y, -l.z);
    }
}