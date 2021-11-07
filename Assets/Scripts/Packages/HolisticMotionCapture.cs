using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MediaPipe.Holistic;
using Mediapipe.BlazePose;

public partial class HolisticMotionCapture : System.IDisposable
{
    public int poseVertexCount => holisticPipeline.poseVertexCount;
    public ComputeBuffer poseLandmarkBuffer => holisticPipeline.poseLandmarkBuffer;
    public ComputeBuffer poseLandmarkWorldBuffer => holisticPipeline.poseLandmarkWorldBuffer;
    public int faceVertexCount => holisticPipeline.faceVertexCount;
    public ComputeBuffer faceVertexBuffer => holisticPipeline.faceVertexBuffer;
    public int eyeVertexCount => holisticPipeline.eyeVertexCount;
    public ComputeBuffer leftEyeVertexBuffer => holisticPipeline.leftEyeVertexBuffer;
    public ComputeBuffer rightEyeVertexBuffer => holisticPipeline.rightEyeVertexBuffer;
    public int handVertexCount => holisticPipeline.handVertexCount;
    public ComputeBuffer leftHandVertexBuffer => holisticPipeline.leftHandVertexBuffer;
    public ComputeBuffer rightHandVertexBuffer => holisticPipeline.rightHandVertexBuffer;

    HolisticPipeline holisticPipeline;
    Animator avatar;

    public HolisticMotionCapture(Animator avatarAnimator, HolisticResource holisticResource, BlazePoseModel blazePoseModel = BlazePoseModel.full){
        avatar = avatarAnimator;
        holisticPipeline = new HolisticPipeline(holisticResource, blazePoseModel);
        PoseInit();
        HandInit();
    }

    public void Dispose(){
        holisticPipeline.Dispose();
    }

    public void AvatarPoseRender(
        Texture inputTexture, 
        float poseScoreThreshold = 0.5f,
        float handScoreThreshold = 0.5f,
        bool isUpperBodyOnly = false,
        HolisticMocapType mocapType = HolisticMocapType.full,
        BlazePoseModel blazePoseModel = BlazePoseModel.full,
        float poseDetectionThreshold = 0.75f,
        float poseDetectionIouThreshold = 0.3f)
    {
        holisticPipeline.ProcessImage(inputTexture, (HolisticInferenceType)mocapType, blazePoseModel, poseDetectionThreshold, poseDetectionIouThreshold);
        PoseRender(holisticPipeline.poseLandmarkWorldBuffer, poseScoreThreshold, isUpperBodyOnly);
        HandRender(true, holisticPipeline.leftHandVertexBuffer, handScoreThreshold);
        HandRender(false, holisticPipeline.rightHandVertexBuffer, handScoreThreshold);
    }
}
