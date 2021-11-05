using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HolisticAvatarController
{
    #region  private variables
    Animator avatar;
    Dictionary<HumanBodyBones, Joint> poseJoints;
    bool isUpperBodyOnly;
    float spineRotPercentage = 0.6f;
    #endregion

    #region constant variables
    const int hipsToHeadStartIndex = 0;
    readonly HumanBodyBones[] hipsToHead = new HumanBodyBones[]{HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head};
    
    const int shoulderToHandStartIndex = 3;
    readonly HumanBodyBones[] leftShoulderToHand = new HumanBodyBones[]{
        HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, 
        HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand
    };
    readonly HumanBodyBones[] rightShoulderToHand = new HumanBodyBones[]{
        HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, 
        HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand
    };
    
    const int hipsToFootStartIndex = 1;
    readonly HumanBodyBones[] leftHipsToFoot = new HumanBodyBones[]{
        HumanBodyBones.Hips,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes
    };
    readonly HumanBodyBones[] rightHipsToFoot = new HumanBodyBones[]{
        HumanBodyBones.Hips,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes
    };
    #endregion

    #region public methods
    public HolisticAvatarController(Animator animatorAvatar){
        avatar = animatorAvatar;
        poseJoints = new Dictionary<HumanBodyBones, Joint>();

        var forward = TriangleNormal(avatar.GetBoneTransform(HumanBodyBones.Spine).position, avatar.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position, avatar.GetBoneTransform(HumanBodyBones.RightUpperLeg).position);
        var boneLists = new HumanBodyBones[][]
        {
            hipsToHead, 
            leftShoulderToHand, 
            rightShoulderToHand, 
            leftHipsToFoot, 
            rightHipsToFoot
        };
        var boneListStartIndexs = new int[]
        {
            hipsToHeadStartIndex, 
            shoulderToHandStartIndex, 
            shoulderToHandStartIndex, 
            hipsToFootStartIndex, 
            hipsToFootStartIndex
        };
        var fallbackParentBones = new HumanBodyBones[]
        {
            HumanBodyBones.Hips, 
            HumanBodyBones.Spine, 
            HumanBodyBones.Spine, 
            HumanBodyBones.Hips, 
            HumanBodyBones.Hips
        };
        var fallbackChildBones = new HumanBodyBones[]
        {
            HumanBodyBones.Head, 
            HumanBodyBones.LeftHand, 
            HumanBodyBones.RightHand, 
            avatar.GetBoneTransform(HumanBodyBones.LeftToes) != null ? HumanBodyBones.LeftToes : HumanBodyBones.LeftFoot, 
            avatar.GetBoneTransform(HumanBodyBones.RightToes) != null ? HumanBodyBones.RightToes : HumanBodyBones.RightFoot
        };

        for(int i=0; i<boneLists.Length; i++){
            var boneList = boneLists[i];
            var boneListStartIndex = boneListStartIndexs[i];
            var fallbackParentBone = fallbackParentBones[i];
            var fallbackChildBone = fallbackChildBones[i];

            for(int j = boneListStartIndex; j<boneList.Length; j++){
                var boneTrans = avatar.GetBoneTransform(boneList[j]);
                if(boneTrans == null) continue;
                var bone = boneList[j];

                HumanBodyBones parent = fallbackParentBone;
                for(int k = j - 1; k >= 0; k--){
                    boneTrans = avatar.GetBoneTransform(boneList[k]);
                    if(boneTrans == null) continue;
                    parent = boneList[k];
                    break;
                }

                HumanBodyBones child = fallbackChildBone;
                for(int k = j + 1; k < boneList.Length; k++){
                    boneTrans = avatar.GetBoneTransform(boneList[k]);
                    if(boneTrans == null) continue;
                    child = boneList[k];
                    break;
                }
                
                var inverseRot = Quaternion.identity;
                if(bone != child){
                    inverseRot = Quaternion.Inverse(Quaternion.LookRotation(avatar.GetBoneTransform(bone).position - avatar.GetBoneTransform(child).position, forward));
                }
                poseJoints[bone] = new Joint(bone, parent, child, avatar.GetBoneTransform(bone).rotation, inverseRot);
            }
        }
        
        poseJoints[HumanBodyBones.Hips].inverseRotation = Quaternion.Inverse(Quaternion.LookRotation(forward));
        poseJoints[HumanBodyBones.Head] = new Joint(HumanBodyBones.Head, HumanBodyBones.Head, HumanBodyBones.Head, avatar.GetBoneTransform(HumanBodyBones.Head).rotation, Quaternion.Inverse(Quaternion.LookRotation(forward)));
    }

    public void PoseRender(ComputeBuffer poseWorldBuffer, float scoreThreshold, bool isUpperBodyOnly){
        var poseLandmarks = new Vector4[34];
        poseWorldBuffer.GetData(poseLandmarks);

        // Reset pose if huamn is not visible.
        if(poseLandmarks[33].x < scoreThreshold){
            ResetPose();
            return;
        }

        // Reset pose and update pose in below if mode was changed.
        if(this.isUpperBodyOnly != isUpperBodyOnly){
            ResetPose();
            this.isUpperBodyOnly = isUpperBodyOnly;
        }

        for(int i = 0; i < 33; i++){
            poseLandmarks[i] = new Vector4(-poseLandmarks[i].x, poseLandmarks[i].y, -poseLandmarks[i].z, poseLandmarks[i].w);
        }

        // Caluculate positions of hip, neck and spine.
        var rightHipIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.RightUpperLeg];
        var leftHipIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.LeftUpperLeg];
        var rightShoulderIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.RightUpperArm];
        var leftShoulderIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.LeftUpperArm];
        Vector3 hipPosition = (poseLandmarks[rightHipIndex] + poseLandmarks[leftHipIndex]) / 2.0f;
        Vector3 neckPosition = (poseLandmarks[rightShoulderIndex] + poseLandmarks[leftShoulderIndex]) / 2.0f;
        Vector3 spinePosition = (hipPosition + neckPosition) / 2.0f;

        // Caluculate avatar forward direction and hip rotation.
        var forward = TriangleNormal(spinePosition, poseLandmarks[leftHipIndex], poseLandmarks[rightHipIndex]);
        var hipScore = (poseLandmarks[leftHipIndex].w + poseLandmarks[rightHipIndex].w) * 0.5f;
        if(hipScore > scoreThreshold && !isUpperBodyOnly){
            var hipRotation = Quaternion.LookRotation(forward, (spinePosition - hipPosition).normalized) * poseJoints[HumanBodyBones.Hips].inverseRotation *  poseJoints[HumanBodyBones.Hips].initRotation;
            var hipTransform = avatar.GetBoneTransform(HumanBodyBones.Hips);
            hipTransform.rotation = Quaternion.Lerp(hipTransform.rotation, hipRotation, hipScore);
        }
        
        var upperBodyBones = new HumanBodyBones[]{
            HumanBodyBones.RightUpperArm, 
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm
        };
        var lowerBodyBones = new HumanBodyBones[]{
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot
        };
        List<HumanBodyBones> rotatedBones = new List<HumanBodyBones>();
        rotatedBones.AddRange(upperBodyBones);
        if(!isUpperBodyOnly) rotatedBones.AddRange(lowerBodyBones);

        // Rotate arms and legs.
        foreach(var bone in rotatedBones){
            var poseJoint = poseJoints[bone];
            var boneLandmarkIndex = BoneToHolisticIndex.PoseTable[bone];
            var childLandmarkIndex = BoneToHolisticIndex.PoseTable[poseJoint.childBone];
            float score = poseLandmarks[boneLandmarkIndex].w;
            if(score < scoreThreshold) continue;

            Vector3 toChild = poseLandmarks[childLandmarkIndex] - poseLandmarks[boneLandmarkIndex];
            var rot = Quaternion.LookRotation(-toChild, forward) * poseJoints[bone].inverseRotation * poseJoints[bone].initRotation;
            var boneTrans = avatar.GetBoneTransform(bone);
            boneTrans.rotation = Quaternion.Lerp(boneTrans.rotation, rot, score);
        }

        // Rotate head with pose landmark.
        var leftEyeLandmark = poseLandmarks[2];
        var rightEyeLandmark = poseLandmarks[5];
        var leftMouthLandmark = poseLandmarks[9];
        var rightMouthLandmark = poseLandmarks[10];
        var eyeMid = (leftEyeLandmark + rightEyeLandmark) * 0.5f;
        var mouthMid = (leftMouthLandmark + rightMouthLandmark) * 0.5f;
        var headScore = (eyeMid.w + mouthMid.w) * 0.5f;
        var headForward = Vector3.Cross(eyeMid - mouthMid, leftMouthLandmark - rightMouthLandmark);
        if(headScore > scoreThreshold){
            var headRotation = Quaternion.LookRotation(headForward, (eyeMid - mouthMid).normalized) * poseJoints[HumanBodyBones.Head].inverseRotation *  poseJoints[HumanBodyBones.Head].initRotation;
            if(isUpperBodyOnly){
                var hipRotation = Quaternion.Lerp(poseJoints[HumanBodyBones.Spine].initRotation, headRotation, spineRotPercentage);
                var hipTransform = avatar.GetBoneTransform(HumanBodyBones.Spine);
                hipTransform.rotation = hipRotation;
            }
            var headTransform = avatar.GetBoneTransform(HumanBodyBones.Head);
            headTransform.rotation = Quaternion.Lerp(headTransform.rotation, headRotation, headScore);
        }
    }
    #endregion

    void ResetPose(){
        var bones = new HumanBodyBones[]{
            HumanBodyBones.Hips,
            HumanBodyBones.RightUpperArm, 
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot
        };
        foreach(var bone in bones){
            var boneTrans = avatar.GetBoneTransform(bone);
            boneTrans.rotation = poseJoints[bone].initRotation;
        }
    }

    Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 d1 = a - b;
        Vector3 d2 = a - c;
        Vector3 dd = Vector3.Cross(d1, d2);
        dd.Normalize();
        return dd;
    }
}
