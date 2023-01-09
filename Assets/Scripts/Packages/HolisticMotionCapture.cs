using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MediaPipe.Holistic;
using Mediapipe.BlazePose;


public partial class HolisticMotionCapture : System.IDisposable
{
    HolisticPipeline _holisticPipeline;
    public HolisticPipeline holisticPipeline{
        get {return this._holisticPipeline;}
        private set {this._holisticPipeline = value;}
    }
    
    Animator avatar;
    const float maxFps = 30.0f;
    float lastPoseUpdateTime;

    public HolisticMotionCapture(Animator avatarAnimator, BlazePoseModel blazePoseModel = BlazePoseModel.full){
        avatar = avatarAnimator;
        holisticPipeline = new HolisticPipeline(blazePoseModel);
        HandInit();
        PoseInit();
        FaceInit();
    }

    public void Dispose(){
        holisticPipeline.Dispose();
    }

    public void AvatarPoseRender(
        Texture inputTexture, 
        float poseScoreThreshold = 0.5f,
        float handScoreThreshold = 0.5f,
        float faceScoreThreshold = 0.5f,
        bool isUpperBodyOnly = false,
        float lerpPercentage = 0.3f,
        HolisticMocapType mocapType = HolisticMocapType.full,
        BlazePoseModel blazePoseModel = BlazePoseModel.full,
        float poseDetectionThreshold = 0.75f,
        float poseDetectionIouThreshold = 0.3f)
    {
        float nowTime = Time.time;
        if(nowTime - lastPoseUpdateTime < 1.0f / maxFps) {
            return;
        }
        lastPoseUpdateTime = nowTime;

        holisticPipeline.ProcessImage(inputTexture, (HolisticInferenceType)mocapType, blazePoseModel, poseDetectionThreshold, poseDetectionIouThreshold);
        PoseRender(mocapType, poseScoreThreshold, isUpperBodyOnly, lerpPercentage);
        HandRender(mocapType, true, handScoreThreshold, lerpPercentage);
        HandRender(mocapType, false, handScoreThreshold, lerpPercentage);
        FaceRender(mocapType, faceScoreThreshold);
    }

    public void ResetAvatar(float lerpPercentage = 0.3f) {
        ResetPose(lerpPercentage);
        ResetHand(true, lerpPercentage);
        ResetHand(false, lerpPercentage);
        ResetFace();
    }
}
