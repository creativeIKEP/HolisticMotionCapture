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
    HolisticMocapType mocapType;

    public HolisticMotionCapture(Animator avatarAnimator, BlazePoseModel blazePoseModel = BlazePoseModel.full){
        avatar = avatarAnimator;
        holisticPipeline = new HolisticPipeline(blazePoseModel);
        PoseInit();
        HandInit();
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
        bool isSeparateEyeBlink = false,
        bool isUpperBodyOnly = false,
        float lerpPercentage = 0.3f,
        HolisticMocapType mocapType = HolisticMocapType.full,
        BlazePoseModel blazePoseModel = BlazePoseModel.full,
        float poseDetectionThreshold = 0.75f,
        float poseDetectionIouThreshold = 0.3f)
    {
        holisticPipeline.ProcessImage(inputTexture, (HolisticInferenceType)mocapType, blazePoseModel, poseDetectionThreshold, poseDetectionIouThreshold);

        if(this.mocapType != mocapType){
            ResetAvatar();
            this.mocapType = mocapType;
        }

        PoseRender(mocapType, poseScoreThreshold, isUpperBodyOnly, lerpPercentage);
        HandRender(mocapType, true, handScoreThreshold, lerpPercentage);
        HandRender(mocapType, false, handScoreThreshold, lerpPercentage);
        FaceRender(mocapType, faceScoreThreshold, isSeparateEyeBlink);
    }

    void ResetAvatar() {
        ResetPose(1);
        ResetHand(true, 1);
        ResetHand(false, 1);
    }
}
