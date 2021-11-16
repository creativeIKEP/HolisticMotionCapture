using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MediaPipe.Holistic;
using Mediapipe.BlazePose;

using VRM;

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
    VRMBlendShapeProxy proxy;

    public HolisticMotionCapture(Animator avatarAnimator, HolisticResource holisticResource, BlazePoseModel blazePoseModel = BlazePoseModel.full){
        avatar = avatarAnimator;
        holisticPipeline = new HolisticPipeline(holisticResource, blazePoseModel);
        PoseInit();
        HandInit();
        proxy = avatar.GetComponent<VRMBlendShapeProxy>();
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
        // PoseRender(holisticPipeline.poseLandmarkWorldBuffer, poseScoreThreshold, isUpperBodyOnly);
        // HandRender(true, holisticPipeline.leftHandVertexBuffer, handScoreThreshold);
        // HandRender(false, holisticPipeline.rightHandVertexBuffer, handScoreThreshold);

        hoge(true);
        hoge(false);
        // kalidoface();
    }

    void hoge(bool isLeft){
        // var faceLandmarks = new Vector4[faceVertexCount];
        // holisticPipeline.faceVertexBuffer.GetData(faceLandmarks);

        // var eyeOuterCorner = faceLandmarks[130];
        // var eyeInnerCorner = faceLandmarks[133];
        // var eyeOuterUpperLid = faceLandmarks[160];
        // var eyeMidUpperLid = faceLandmarks[159];
        // var eyeInnerUpperLid = faceLandmarks[158];
        // var eyeOuterLowerLid = faceLandmarks[144];
        // var eyeMidLowerLid = faceLandmarks[145];
        // var eyeInnerLowerLid = faceLandmarks[153];

        var eyeLandmarks = new Vector4[eyeVertexCount];
        if(isLeft) holisticPipeline.leftEyeVertexBuffer.GetData(eyeLandmarks);
        else holisticPipeline.rightEyeVertexBuffer.GetData(eyeLandmarks);

        var eyeOuterCorner = eyeLandmarks[5];
        var eyeInnerCorner = eyeLandmarks[13];
        var eyeOuterUpperLid = eyeLandmarks[16];
        var eyeOuterLowerLid = eyeLandmarks[8];
        var eyeMidUpperLid = eyeLandmarks[17];
        var eyeMidLowerLid = eyeLandmarks[9];
        var eyeInnerUpperLid = eyeLandmarks[18];
        var eyeInnerLowerLid = eyeLandmarks[10];



        var eyeWidth = Vector2.Distance(eyeOuterCorner, eyeInnerCorner);
        var eyeOuterLidDistance = Vector2.Distance(eyeOuterUpperLid, eyeOuterLowerLid);
        var eyeMidLidDistance = Vector2.Distance(eyeMidUpperLid, eyeMidLowerLid);
        var eyeInnerLidDistance = Vector2.Distance(eyeInnerUpperLid, eyeInnerLowerLid);

        var ear = (eyeOuterLidDistance + eyeInnerLidDistance) / (2*eyeWidth);
        Debug.Log(ear);
        ear = (ear - 0.25f) / (0.4f - 0.25f);
        ear = 1.0f - ear;
        ear = Mathf.Clamp(ear, 0, 1);
        var blinkEye = isLeft ? BlendShapePreset.Blink_L : BlendShapePreset.Blink_R;
        proxy.SetValues(new Dictionary<BlendShapeKey, float>
        {
            {BlendShapeKey.CreateFromPreset(blinkEye), ear}
        });
    }



    // float min_ratio = 0.55f;
    // float max_ratio = 0.85f;
    // float time_count = 0;
    // void eye(){
    //     var faceLandmarks = new Vector4[faceVertexCount];
    //     holisticPipeline.faceVertexBuffer.GetData(faceLandmarks);

    //     var eyeOuterCorner = faceLandmarks[130];
    //     var eyeInnerCorner = faceLandmarks[133];
    //     var eyeOuterUpperLid = faceLandmarks[160];
    //     var eyeMidUpperLid = faceLandmarks[159];
    //     var eyeInnerUpperLid = faceLandmarks[158];
    //     var eyeOuterLowerLid = faceLandmarks[144];
    //     var eyeMidLowerLid = faceLandmarks[145];
    //     var eyeInnerLowerLid = faceLandmarks[153];

    //     var eyeWidth = Vector2.Distance(eyeOuterCorner, eyeInnerCorner);
    //     var eyeOuterLidDistance = Vector2.Distance(eyeOuterUpperLid, eyeOuterLowerLid);
    //     var eyeMidLidDistance = Vector2.Distance(eyeMidUpperLid, eyeMidLowerLid);
    //     var eyeInnerLidDistance = Vector2.Distance(eyeInnerUpperLid, eyeInnerLowerLid);
    //     var eyeLIdAvg = (eyeOuterLidDistance + eyeMidLidDistance + eyeInnerLidDistance) / 3.0f;

    //     var ratio = eyeLIdAvg / eyeWidth;
    //     if(ratio > 1) return;

    //     if(min_ratio > ratio) min_ratio = ratio;
    //     if(max_ratio < ratio) max_ratio = ratio;

    //     var l = (Mathf.Clamp(ratio, min_ratio, max_ratio) - min_ratio) / (max_ratio - min_ratio);
    //     l = 1.0f - l;
    //     l = Mathf.Clamp(l, 0, 1);

    //     proxy.SetValues(new Dictionary<BlendShapeKey, float>
    //     {
    //         {BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_L), l}
    //     });

    //     time_count += Time.deltaTime;
    //     if(time_count > 1){
    //         max_ratio *= 0.9f;
    //         min_ratio *= 1.1f;
    //         time_count = 0;
    //     }
    // }


    // void kalidoface(){
    //     var faceLandmarks = new Vector4[faceVertexCount];
    //     holisticPipeline.faceVertexBuffer.GetData(faceLandmarks);

    //     var eyeOuterCorner = faceLandmarks[130];
    //     var eyeInnerCorner = faceLandmarks[133];
    //     var eyeOuterUpperLid = faceLandmarks[160];
    //     var eyeMidUpperLid = faceLandmarks[159];
    //     var eyeInnerUpperLid = faceLandmarks[158];
    //     var eyeOuterLowerLid = faceLandmarks[144];
    //     var eyeMidLowerLid = faceLandmarks[145];
    //     var eyeInnerLowerLid = faceLandmarks[153];

    //     var eyeWidth = Vector2.Distance(eyeOuterCorner, eyeInnerCorner);
    //     var eyeOuterLidDistance = Vector2.Distance(eyeOuterUpperLid, eyeOuterLowerLid);
    //     var eyeMidLidDistance = Vector2.Distance(eyeMidUpperLid, eyeMidLowerLid);
    //     var eyeInnerLidDistance = Vector2.Distance(eyeInnerUpperLid, eyeInnerLowerLid);
    //     var eyeLIdAvg = (eyeOuterLidDistance + eyeMidLidDistance + eyeInnerLidDistance) / 3.0f;
        
    //     var eyeDistance  = eyeLIdAvg / eyeWidth;

    //     var maxRatio = 0.285f;
    //     var ratio = Mathf.Clamp(eyeDistance / maxRatio, 0, 2);//Mathf.Max(Mathf.Min(eyeDistance / maxRatio, 2), 0);
        
    //     var max = 0.5f;
    //     var min = 0.35f;
    //     var remap = (Mathf.Clamp(ratio, min, max) - min) / (max - min);//(Mathf.Max(Mathf.Min(ratio, max), min) - min) / (max - min);//(Mathf.Clamp(ratio, min, max) - min) / (max - min);


    //     remap = 1.0f - remap;
    //     remap = Mathf.Lerp(Mathf.Clamp(remap, 0, 1), proxy.GetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_L)), 0.5f);
    //     ///


    //      eyeOuterCorner = faceLandmarks[263];
    //      eyeInnerCorner = faceLandmarks[362];
    //      eyeOuterUpperLid = faceLandmarks[387];
    //      eyeMidUpperLid = faceLandmarks[386];
    //      eyeInnerUpperLid = faceLandmarks[385];
    //      eyeOuterLowerLid = faceLandmarks[373];
    //      eyeMidLowerLid = faceLandmarks[374];
    //      eyeInnerLowerLid = faceLandmarks[380];

    //      eyeWidth = Vector2.Distance(eyeOuterCorner, eyeInnerCorner);
    //      eyeOuterLidDistance = Vector2.Distance(eyeOuterUpperLid, eyeOuterLowerLid);
    //      eyeMidLidDistance = Vector2.Distance(eyeMidUpperLid, eyeMidLowerLid);
    //      eyeInnerLidDistance = Vector2.Distance(eyeInnerUpperLid, eyeInnerLowerLid);
    //      eyeLIdAvg = (eyeOuterLidDistance + eyeMidLidDistance + eyeInnerLidDistance) / 3.0f;
        
    //      eyeDistance  = eyeLIdAvg / eyeWidth;

    //      maxRatio = 0.285f;
    //      ratio = Mathf.Clamp(eyeDistance / maxRatio, 0, 2);//Mathf.Max(Mathf.Min(eyeDistance / maxRatio, 2), 0);
        
    //      max = 0.5f;
    //      min = 0.35f;
    //      var remap_r = (Mathf.Clamp(ratio, min, max) - min) / (max - min);//(Mathf.Max(Mathf.Min(ratio, max), min) - min) / (max - min);//(Mathf.Clamp(ratio, min, max) - min) / (max - min);
        
    //     remap_r = 1.0f - remap_r;
    //     remap_r = Mathf.Lerp(Mathf.Clamp(remap_r, 0, 1), proxy.GetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_R)), 0.5f);

    //     ///
    //     remap_r = Mathf.Clamp(remap_r, 0, 1);
    //     remap = Mathf.Clamp(remap, 0, 1);
    //     var blinkDiff = Mathf.Abs(remap - remap_r);
    //     var blinkThresh = 0.8f;
    //     var isClosing = remap < 0.3f && remap_r < 0.3f;
    //     var isOpen = remap > 0.6f && remap_r > 0.6f;


    //     var l = blinkDiff >= blinkThresh && !isClosing && !isOpen ? remap : (remap_r > remap ? Mathf.Lerp(remap_r, remap, 0.95f) : Mathf.Lerp(remap_r, remap, 0.05f));
    //     var r = blinkDiff >= blinkThresh && !isClosing && !isOpen ? remap_r : (remap_r > remap ? Mathf.Lerp(remap_r, remap, 0.95f) : Mathf.Lerp(remap_r, remap, 0.05f));


    //     proxy.SetValues(new Dictionary<BlendShapeKey, float>
    //     {
    //         {BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_L), remap},
    //         {BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_R), remap_r}
    //     });
    // }
}
