using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRM;

partial class HolisticMotionCapture
{
    VRMBlendShapeProxy proxy;

    void FaceInit(){
        proxy = avatar.GetComponent<VRMBlendShapeProxy>();
    }

    void FaceRender(ComputeBuffer faceVertexBuffer, ComputeBuffer leftEyeVertexBuffer, ComputeBuffer rightEyeVertexBuffer, bool isSeparateEyeBlink){
        var leftEyeLandmarks = new Vector4[eyeVertexCount];
        leftEyeVertexBuffer.GetData(leftEyeLandmarks);
        var rightEyeLandmarks = new Vector4[eyeVertexCount];
        rightEyeVertexBuffer.GetData(rightEyeLandmarks);

        BlinkRender(leftEyeLandmarks, rightEyeLandmarks, isSeparateEyeBlink);
        PupilRender(leftEyeLandmarks, rightEyeLandmarks);
    }

    void BlinkRender(Vector4[] leftEyeLandmarks, Vector4[] rightEyeLandmarks, bool isSeparateEyeBlink){
        if(proxy == null) return;

        var leftEyeBlink = CalculateEyeBlink(leftEyeLandmarks);
        var rightEyeBlink = CalculateEyeBlink(rightEyeLandmarks);
        
        if(isSeparateEyeBlink){
            proxy.SetValues(new Dictionary<BlendShapeKey, float>
            {
                {BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_L), leftEyeBlink},
                {BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_R), rightEyeBlink}
            });
        }
        else{
            var blink = IntegratedBlink(leftEyeBlink, rightEyeBlink);
            proxy.SetValues(new Dictionary<BlendShapeKey, float>
            {
                {BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink), blink}
            });
        }
    }

    float CalculateEyeBlink(Vector4[] eyeLandmarks){
        var eyeOuterCorner = eyeLandmarks[5];
        var eyeInnerCorner = eyeLandmarks[13];
        var eyeOuterUpperLid = eyeLandmarks[16];
        var eyeOuterLowerLid = eyeLandmarks[8];
        var eyeInnerUpperLid = eyeLandmarks[18];
        var eyeInnerLowerLid = eyeLandmarks[10];

        var eyeWidth = Vector2.Distance(eyeOuterCorner, eyeInnerCorner);
        var eyeOuterLidDistance = Vector2.Distance(eyeOuterUpperLid, eyeOuterLowerLid);
        var eyeInnerLidDistance = Vector2.Distance(eyeInnerUpperLid, eyeInnerLowerLid);

        var ear = (eyeOuterLidDistance + eyeInnerLidDistance) / (2 * eyeWidth);
        var maxRatio = 0.5f;
        var minRatio = 0.3f;
        var eyeOpenRatio = (ear - minRatio) / (maxRatio - minRatio);
        var eyeBlink = 1.0f - eyeOpenRatio;
        eyeBlink = Mathf.Clamp(eyeBlink, 0, 1);
        return eyeBlink;
    }

    float IntegratedBlink(float leftEyeBlink, float rightEyeBlink){
        var headRot = avatar.GetBoneTransform(HumanBodyBones.Head).rotation;
        if(0 <= headRot.eulerAngles.y && headRot.eulerAngles.y <= 180){
            return leftEyeBlink;
        }
        return rightEyeBlink;
    }

    void PupilRender(Vector4[] leftEyeLandmarks, Vector4[] rightEyeLandmarks){
        var leftPupilBoneTrans = avatar.GetBoneTransform(HumanBodyBones.LeftEye);
        var rightPupilBoneTrans = avatar.GetBoneTransform(HumanBodyBones.RightEye);
        if(leftPupilBoneTrans == null || rightPupilBoneTrans == null) return;

        var leftRatio = CalculatePupil(leftEyeLandmarks);
        var rightRatio = CalculatePupil(rightEyeLandmarks);
        var ratioAvg = (leftRatio + rightRatio) * 0.5f * 1.5f;
        ratioAvg.x = ratioAvg.x * 0.5f + 0.5f;
        ratioAvg.y = ratioAvg.y * 0.5f + 0.5f;
        var ry = Mathf.Lerp(-8, 12, ratioAvg.x);
        var ly = Mathf.Lerp(-12, 8, ratioAvg.x);
        var x = Mathf.Lerp(-10, 10, ratioAvg.y);
        leftPupilBoneTrans.localRotation = Quaternion.Euler(x, ly, 0);
        rightPupilBoneTrans.localRotation = Quaternion.Euler(x, ry, 0);
    }

    Vector2 CalculatePupil(Vector4[] eyeLandmarks){
        var eyeOuterCorner = eyeLandmarks[5];
        var eyeInnerCorner = eyeLandmarks[13];
        var eyeMidUpper = eyeLandmarks[17];
        var eyeMidLower = eyeLandmarks[9];
        var eyeWidth = Vector2.Distance(eyeOuterCorner, eyeInnerCorner);
        var eyeHeight = Vector2.Distance(eyeMidUpper, eyeMidLower);
        var eyeMidPoint = (eyeOuterCorner + eyeInnerCorner) * 0.5f;
        var pupil = eyeLandmarks[0];

        var dx = eyeMidPoint.x - pupil.x;
        var dy = eyeMidPoint.y - pupil.y;

        var ratioX = dx / (eyeWidth * 0.5f);
        var ratioY = dy / (eyeHeight * 0.5f);
        if(float.IsInfinity(ratioX) || float.IsNaN(ratioX)) ratioX = 0;
        if(float.IsInfinity(ratioY) || float.IsNaN(ratioY)) ratioY = 0;
        return new Vector2(ratioX, ratioY);
    }
}
