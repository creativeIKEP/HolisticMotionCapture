using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRM;

partial class HolisticMotionCapture
{
    VRMBlendShapeProxy proxy;
    int faceCounter;
    List<LowPassFilter> face_lpfs;
    List<Tuple<int, Vector4>> lpfedFaceBuffers;
    List<LowPassFilter> leftEye_lpfs;
    List<Tuple<int, Vector4>> lpfedLeftEyeBuffers;
    List<LowPassFilter> rightEye_lpfs;
    List<Tuple<int, Vector4>> lpfedRightEyeBuffers;

    void FaceInit(){
        proxy = avatar.GetComponent<VRMBlendShapeProxy>();
        
        face_lpfs = new List<LowPassFilter>();
        lpfedFaceBuffers = new List<Tuple<int, Vector4>>();
        for(int i = 0; i < holisticPipeline.faceVertexCount; i++) {
            face_lpfs.Add(new LowPassFilter(2, 1.5f));
            lpfedFaceBuffers.Add(new Tuple<int, Vector4>(0, Vector4.zero));
        }

        leftEye_lpfs = new List<LowPassFilter>();
        lpfedLeftEyeBuffers = new List<Tuple<int, Vector4>>();
        for(int i = 0; i < holisticPipeline.eyeVertexCount; i++) {
            leftEye_lpfs.Add(new LowPassFilter(2, 1.5f));
            lpfedLeftEyeBuffers.Add(new Tuple<int, Vector4>(0, Vector4.zero));
        }

        rightEye_lpfs = new List<LowPassFilter>();
        lpfedRightEyeBuffers = new List<Tuple<int, Vector4>>();
        for(int i = 0; i < holisticPipeline.eyeVertexCount; i++) {
            rightEye_lpfs.Add(new LowPassFilter(2, 1.5f));
            lpfedRightEyeBuffers.Add(new Tuple<int, Vector4>(0, Vector4.zero));
        }
    }

    void FaceRender(HolisticMocapType mocapType, float faceScoreThreshold){
        faceCounter++;
        if(faceCounter >= int.MaxValue) {
            faceCounter = 1;
        }

        if(mocapType == HolisticMocapType.pose_and_hand || mocapType == HolisticMocapType.pose_only){
            return;
        }

        if(holisticPipeline.faceDetectionScore < faceScoreThreshold) {
            return;
        }

        BlinkRender();
        PupilRender();
        MouthRender();
    }

    float LpfAlpha(float x, float p_x, Vector3 param){
        float dx = x - p_x;
        float cutoff = param.y + param.x * Mathf.Abs(dx);
        float r = 2.0f * 3.141592f * cutoff * param.z;
        float alpha = r / (r + 1);
        return alpha;
    }

    float LowPassFilter(float x, float p_x, Vector3 param){
        float alpha = LpfAlpha(x, p_x, param);
        return Mathf.Lerp(p_x, x, alpha);
    }

    Vector4 FaceLandmark(int index){
        var landmark = holisticPipeline.GetFaceLandmark(index);
        
        // Low pass Filter
        var buffer = lpfedFaceBuffers[index];
        if(buffer.Item1 == faceCounter) {
            landmark = buffer.Item2;
        }
        else {
            var score = landmark.w;
            landmark = face_lpfs[index].Filter(landmark, Time.deltaTime);
            landmark.w = score;
            lpfedFaceBuffers[index] = new Tuple<int, Vector4>(faceCounter, landmark);
        }

        return landmark;
    }

    Vector4 EyeLandmark(int index, bool isLeft){
        var landmark = isLeft ? holisticPipeline.GetLeftEyeLandmark(index) : holisticPipeline.GetRightEyeLandmark(index);

        // Low pass Filter
        var buffer = isLeft ? lpfedLeftEyeBuffers[index] : lpfedRightEyeBuffers[index];
        if(buffer.Item1 == faceCounter) {
            landmark = buffer.Item2;
        }
        else {
            var score = landmark.w;
            var filter = isLeft ? leftEye_lpfs[index] : rightEye_lpfs[index];
            landmark = filter.Filter(landmark, Time.deltaTime);
            landmark.w = score;
            if(isLeft) lpfedLeftEyeBuffers[index] = new Tuple<int, Vector4>(faceCounter, landmark);
            else lpfedRightEyeBuffers[index] = new Tuple<int, Vector4>(faceCounter, landmark);
        }

        return landmark;
    }

    void BlinkRender(){
        if(proxy == null) return;

        var eyeBlink = CalculateEyeBlink();
        var leftEyeBlink = eyeBlink.x;
        var rightEyeBlink = eyeBlink.y;
        
        var preLeftEyeBlink = proxy.GetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_L));
        var preRightEyeBlink = proxy.GetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_R));
        leftEyeBlink = LowPassFilter(leftEyeBlink, preLeftEyeBlink, new Vector3(3f, 1.5f, Time.deltaTime));
        rightEyeBlink = LowPassFilter(rightEyeBlink, preRightEyeBlink, new Vector3(3f, 1.5f, Time.deltaTime));
        if(leftEyeBlink > 0.65f) leftEyeBlink = 1f;
        if(rightEyeBlink > 0.65f) rightEyeBlink = 1f;

        proxy.SetValues(new Dictionary<BlendShapeKey, float>
        {
            {BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_L), leftEyeBlink},
            {BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_R), rightEyeBlink}
        });
    }

    float minEarL = 1f;
    float minEarR = 1f;
    float maxEarL = 0f;
    float maxEarR = 0f;

    Vector2 CalculateEyeBlink(){
        var earL = CalculateEar(true);
        var earR = CalculateEar(false);
        var integratedEar = (earL + earR) * 0.5f;

        minEarL += 0.0001f;
        minEarR += 0.0001f;
        maxEarL -= 0.005f;
        maxEarR -= 0.005f;
        if(Mathf.Min(integratedEar, earL) < minEarL) minEarL = Mathf.Min(integratedEar, earL);
        if(Mathf.Min(integratedEar, earR) < minEarR) minEarR = Mathf.Min(integratedEar, earR);
        if(Mathf.Max(integratedEar, earL) > maxEarL) maxEarL = Mathf.Max(integratedEar, earL);
        if(Mathf.Max(integratedEar, earR) > maxEarR) maxEarR = Mathf.Max(integratedEar, earR);

        var eyeBlinkL = Mathf.InverseLerp(minEarL, maxEarL, earL);
        var eyeBlinkR = Mathf.InverseLerp(minEarR, maxEarR, earR);

        if(eyeBlinkL < 0.65f) eyeBlinkL = 0f;
        if(eyeBlinkL > 0.85f) eyeBlinkL = 1f;
        if(eyeBlinkR < 0.65f) eyeBlinkR = 0f;
        if(eyeBlinkR > 0.85f) eyeBlinkR = 1f;

        eyeBlinkL = 1.0f - Mathf.Clamp01(eyeBlinkL);
        eyeBlinkR = 1.0f - Mathf.Clamp01(eyeBlinkR);
        return new Vector2(eyeBlinkL, eyeBlinkR);
    }

    float CalculateEar(bool isLeft) {
        var eyeOuterCorner = EyeLandmark(5, isLeft);
        var eyeInnerCorner = EyeLandmark(13, isLeft);
        var eyeOuterUpperLid = EyeLandmark(16, isLeft);
        var eyeOuterLowerLid = EyeLandmark(8, isLeft);
        var eyeInnerUpperLid = EyeLandmark(18, isLeft);
        var eyeInnerLowerLid = EyeLandmark(10, isLeft);

        var eyeWidth = Vector2.Distance(eyeOuterCorner, eyeInnerCorner);
        if(eyeWidth < 1e-10){
            eyeWidth = (float)(1e-10);
        }
        var eyeOuterLidDistance = Vector2.Distance(eyeOuterUpperLid, eyeOuterLowerLid);
        var eyeInnerLidDistance = Vector2.Distance(eyeInnerUpperLid, eyeInnerLowerLid);
        var ear = (eyeOuterLidDistance + eyeInnerLidDistance) / (2 * eyeWidth);
        return ear;
    }

    float IntegratedBlink(float leftEyeBlink, float rightEyeBlink){
        var headRot = avatar.GetBoneTransform(HumanBodyBones.Head).rotation;
        if(0 <= headRot.eulerAngles.y && headRot.eulerAngles.y <= 180){
            return leftEyeBlink;
        }
        return rightEyeBlink;
    }

    void PupilRender(){
        var leftPupilBoneTrans = avatar.GetBoneTransform(HumanBodyBones.LeftEye);
        var rightPupilBoneTrans = avatar.GetBoneTransform(HumanBodyBones.RightEye);
        if(leftPupilBoneTrans == null || rightPupilBoneTrans == null) return;

        var leftRatio = CalculatePupil(true);
        var rightRatio = CalculatePupil(false);
        var ratioAvg = (leftRatio + rightRatio) * 0.5f * 1.5f;
        ratioAvg.x = ratioAvg.x * 0.5f + 0.5f;
        ratioAvg.y = ratioAvg.y * 0.5f + 0.5f;
        var ly = Mathf.Lerp(-12, 12, ratioAvg.x);
        var ry = Mathf.Lerp(-12, 12, ratioAvg.x);
        // disable
        // var x = Mathf.Lerp(-10, 10, ratioAvg.y);
        var x = 0;

        var param = new Vector3(3f, 1.5f, Time.deltaTime);
        var l_a = LpfAlpha(ly, leftPupilBoneTrans.localRotation.eulerAngles.y, param);
        var r_a = LpfAlpha(ry, rightPupilBoneTrans.localRotation.eulerAngles.y, param);

        leftPupilBoneTrans.localRotation = Quaternion.Lerp(leftPupilBoneTrans.localRotation, Quaternion.Euler(x, ly, 0), l_a);
        rightPupilBoneTrans.localRotation = Quaternion.Lerp(rightPupilBoneTrans.localRotation, Quaternion.Euler(x, ry, 0), r_a);
    }

    Vector2 CalculatePupil(bool isLeft){
        var eyeOuterCorner = EyeLandmark(5, isLeft);
        var eyeInnerCorner = EyeLandmark(13, isLeft);
        var eyeMidUpper = EyeLandmark(17, isLeft);
        var eyeMidLower = EyeLandmark(9, isLeft);
        var eyeWidth = Vector2.Distance(eyeOuterCorner, eyeInnerCorner);
        var eyeHeight = Vector2.Distance(eyeMidUpper, eyeMidLower);
        var eyeMidPoint = (eyeOuterCorner + eyeInnerCorner) * 0.5f;
        var pupil = EyeLandmark(0, isLeft);

        var dx = eyeMidPoint.x - pupil.x;
        var dy = eyeMidPoint.y - pupil.y;

        var ratioX = dx / (eyeWidth * 0.5f);
        var ratioY = dy / (eyeHeight * 0.5f);
        ratioY += 0.3f;
        if(float.IsInfinity(ratioX) || float.IsNaN(ratioX)) ratioX = 0;
        if(float.IsInfinity(ratioY) || float.IsNaN(ratioY)) ratioY = 0;
        return new Vector2(ratioX, ratioY);
    }

    void MouthRender(){
        var eyeInnerCornerL = FaceLandmark(133);
        var eyeInnerCornerR = FaceLandmark(362);
        var eyeOuterCornerL = FaceLandmark(130);
        var eyeOuterCornerR = FaceLandmark(263);

        var eyeInnerDistance = Vector3.Distance(eyeInnerCornerL, eyeInnerCornerR);
        var eyeOuterDistance = Vector3.Distance(eyeOuterCornerL, eyeOuterCornerR);
        if(eyeInnerDistance < 1e-10){
            eyeInnerDistance = (float)(1e-10);
        }
        if(eyeOuterDistance < 1e-10){
            eyeOuterDistance = (float)(1e-10);
        }

        var upperInnerLip = FaceLandmark(13);
        var lowerInnerLip = FaceLandmark(14);
        var mouthCornerLeft = FaceLandmark(61);
        var mouthCornerRight = FaceLandmark(291);

        var mouthOpen = Vector3.Distance(upperInnerLip, lowerInnerLip);
        var mouthWidth = Vector3.Distance(mouthCornerLeft, mouthCornerRight);

        var ratioY = mouthOpen / eyeInnerDistance;
        var ratioX = mouthWidth / eyeOuterDistance;

        ratioY = (ratioY - 0.15f) / (0.7f - 0.15f);
        ratioX = (ratioX - 0.45f) / (0.9f - 0.45f);
        ratioX = (ratioX - 0.3f) * 2f;
        
        var mouthX = ratioX;
        var mouthY = (mouthOpen / eyeInnerDistance - 0.17f) / (0.5f - 0.17f);

        var ratioI = Mathf.Clamp(mouthX * 2 * ((mouthY - 0.2f) / 0.5f), 0, 1);
        var ratioA = mouthY * 0.4f + mouthY * (1 - ratioI) * 0.6f;
        var ratioU = mouthY * ((1-ratioI) / 0.3f) * 0.01f;
        var ratioE = ((ratioU - 0.2f) / 0.8f) * (1 - ratioI) * 0.3f;
        var ratioO = (1 - ratioI) * 0.4f * ((mouthY - 0.3f) / 0.7f);

        var param = new Vector3(30.0f, 1.5f, Time.deltaTime);
        ratioI = LowPassFilter(Mathf.Clamp(ratioI, 0, 1), proxy.GetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.I)), param);
        ratioA = LowPassFilter(Mathf.Clamp(ratioA, 0, 1), proxy.GetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.A)), param);
        ratioU = LowPassFilter(Mathf.Clamp(ratioU, 0, 1), proxy.GetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.U)), param);
        ratioE = LowPassFilter(Mathf.Clamp(ratioE, 0, 1), proxy.GetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.E)), param);
        ratioO = LowPassFilter(Mathf.Clamp(ratioO, 0, 1), proxy.GetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.O)), param);

        proxy.SetValues(new Dictionary<BlendShapeKey, float>
        {
            {BlendShapeKey.CreateFromPreset(BlendShapePreset.A), ratioA},
            {BlendShapeKey.CreateFromPreset(BlendShapePreset.I), ratioI},
            {BlendShapeKey.CreateFromPreset(BlendShapePreset.U), ratioU},
            {BlendShapeKey.CreateFromPreset(BlendShapePreset.E), ratioE},
            {BlendShapeKey.CreateFromPreset(BlendShapePreset.O), ratioO},
        });
    }
}
