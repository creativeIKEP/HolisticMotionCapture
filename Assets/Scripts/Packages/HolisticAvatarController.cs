using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HolisticAvatarController
{
    #region  private variables
    Animator avatar;
    Dictionary<HumanBodyBones, Joint> poseJoints;
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

            for(int j = 0; j<boneList.Length; j++){
                var boneTrans = avatar.GetBoneTransform(boneList[j]);
                if(boneTrans == null) continue;
                var bone = boneList[j];

                HumanBodyBones parent = HumanBodyBones.Hips;
                for(int k = j - 1; k >= 0; k--){
                    boneTrans = avatar.GetBoneTransform(boneList[k]);
                    if(boneTrans == null) continue;
                    parent = boneList[k];
                    break;
                }

                HumanBodyBones child = HumanBodyBones.Head;
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
    }

    public void PoseRender(ComputeBuffer poseWorldBuffer){
        var poseLandmarks = new Vector4[33]; // scoreなし
        poseWorldBuffer.GetData(poseLandmarks);

        for(int i = 0; i < 33; i++){
            poseLandmarks[i] = new Vector4(-poseLandmarks[i].x, poseLandmarks[i].y, -poseLandmarks[i].z, poseLandmarks[i].w);
        }

        var rightHipIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.RightUpperLeg];
        var leftHipIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.LeftUpperLeg];
        var rightShoulderIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.RightUpperArm];
        var leftShoulderIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.LeftUpperArm];
        Vector3 hipPosition = (poseLandmarks[rightHipIndex] + poseLandmarks[leftHipIndex]) / 2.0f;
        Vector3 neckPosition = (poseLandmarks[rightShoulderIndex] + poseLandmarks[leftShoulderIndex]) / 2.0f;
        Vector3 spinePosition = (hipPosition + neckPosition) / 2.0f;

        var forward = TriangleNormal(spinePosition, poseLandmarks[leftHipIndex], poseLandmarks[rightHipIndex]);
        var hipTransform = avatar.GetBoneTransform(HumanBodyBones.Hips);
        hipTransform.rotation = Quaternion.LookRotation(forward, (spinePosition - hipPosition).normalized) * poseJoints[HumanBodyBones.Hips].inverseRotation *  poseJoints[HumanBodyBones.Hips].initRotation;
        
        var rotatedBones = new HumanBodyBones[]{
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
        foreach(var bone in rotatedBones){
            var poseJoint = poseJoints[bone];
            var boneLandmarkIndex = BoneToHolisticIndex.PoseTable[bone];
            var childLandmarkIndex = BoneToHolisticIndex.PoseTable[poseJoint.childBone];

            Vector3 toChild = poseLandmarks[childLandmarkIndex] - poseLandmarks[boneLandmarkIndex];
            var boneTrans = avatar.GetBoneTransform(bone);
            boneTrans.rotation = Quaternion.LookRotation(-toChild, forward) * poseJoints[bone].inverseRotation * poseJoints[bone].initRotation;
        }
    }
    #endregion

    Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 d1 = a - b;
        Vector3 d2 = a - c;
        Vector3 dd = Vector3.Cross(d1, d2);
        dd.Normalize();
        return dd;
    }
}
