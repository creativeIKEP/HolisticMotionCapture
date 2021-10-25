using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HolisticAvatarController
{
    Animator avatar;
    Dictionary<HumanBodyBones, Joint> poseJoints;
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


    Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 d1 = a - b;
        Vector3 d2 = a - c;

        Vector3 dd = Vector3.Cross(d1, d2);
        dd.Normalize();

        return dd;
    }

    public HolisticAvatarController(Animator animatorAvatar){
        avatar = animatorAvatar;
        poseJoints = new Dictionary<HumanBodyBones, Joint>();

        var forward = TriangleNormal(avatar.GetBoneTransform(HumanBodyBones.Spine).position, avatar.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position, avatar.GetBoneTransform(HumanBodyBones.RightUpperLeg).position);

        for(int i = 0; i<hipsToHead.Length; i++){
            var boneTrans = avatar.GetBoneTransform(hipsToHead[i]);
            if(boneTrans == null) continue;
            var bone = hipsToHead[i];

            HumanBodyBones parent = HumanBodyBones.Hips;
            for(int j = i - 1; j >= 0; j--){
                boneTrans = avatar.GetBoneTransform(hipsToHead[j]);
                if(boneTrans == null) continue;
                parent = hipsToHead[j];
                break;
            }

            HumanBodyBones child = HumanBodyBones.Head;
            for(int j = i + 1; j < hipsToHead.Length; j++){
                boneTrans = avatar.GetBoneTransform(hipsToHead[j]);
                if(boneTrans == null) continue;
                child = hipsToHead[j];
                break;
            }
            
            poseJoints[bone] = new Joint(avatar, bone, parent, child, forward);
        }

        for(int i = shoulderToHandStartIndex; i<leftShoulderToHand.Length; i++){
            var boneTrans = avatar.GetBoneTransform(leftShoulderToHand[i]);
            if(boneTrans == null) continue;
            var bone = leftShoulderToHand[i];

            HumanBodyBones parent = HumanBodyBones.Spine;
            for(int j = i - 1; j >= 0; j--){
                boneTrans = avatar.GetBoneTransform(leftShoulderToHand[j]);
                if(boneTrans == null) continue;
                parent = leftShoulderToHand[j];
                break;
            }

            HumanBodyBones child = HumanBodyBones.LeftHand;
            for(int j = i + 1; j < leftShoulderToHand.Length; j++){
                boneTrans = avatar.GetBoneTransform(leftShoulderToHand[j]);
                if(boneTrans == null) continue;
                child = leftShoulderToHand[j];
                break;
            }
            
            poseJoints[bone] = new Joint(avatar, bone, parent, child, forward);
        }

        for(int i = shoulderToHandStartIndex; i<rightShoulderToHand.Length; i++){
            var boneTrans = avatar.GetBoneTransform(rightShoulderToHand[i]);
            if(boneTrans == null) continue;
            var bone = rightShoulderToHand[i];

            HumanBodyBones parent = HumanBodyBones.Spine;
            for(int j = i - 1; j >= 0; j--){
                boneTrans = avatar.GetBoneTransform(rightShoulderToHand[j]);
                if(boneTrans == null) continue;
                parent = rightShoulderToHand[j];
                break;
            }

            HumanBodyBones child = HumanBodyBones.RightHand;
            for(int j = i + 1; j < rightShoulderToHand.Length; j++){
                boneTrans = avatar.GetBoneTransform(rightShoulderToHand[j]);
                if(boneTrans == null) continue;
                child = rightShoulderToHand[j];
                break;
            }
            
            poseJoints[bone] = new Joint(avatar, bone, parent, child, forward);
        }

        for(int i = hipsToFootStartIndex; i<leftHipsToFoot.Length; i++){
            var boneTrans = avatar.GetBoneTransform(leftHipsToFoot[i]);
            if(boneTrans == null) continue;
            var bone = leftHipsToFoot[i];

            HumanBodyBones parent = HumanBodyBones.Hips;
            for(int j = i - 1; j >= 0; j--){
                boneTrans = avatar.GetBoneTransform(leftHipsToFoot[j]);
                if(boneTrans == null) continue;
                parent = leftHipsToFoot[j];
                break;
            }

            HumanBodyBones child = avatar.GetBoneTransform(HumanBodyBones.LeftToes) != null ? HumanBodyBones.LeftToes : HumanBodyBones.LeftFoot;
            for(int j = i + 1; j < leftHipsToFoot.Length; j++){
                boneTrans = avatar.GetBoneTransform(leftHipsToFoot[j]);
                if(boneTrans == null) continue;
                child = leftHipsToFoot[j];
                break;
            }
            
            poseJoints[bone] = new Joint(avatar, bone, parent, child, forward);
        }

        for(int i = hipsToFootStartIndex; i<rightHipsToFoot.Length; i++){
            var boneTrans = avatar.GetBoneTransform(rightHipsToFoot[i]);
            if(boneTrans == null) continue;
            var bone = rightHipsToFoot[i];

            HumanBodyBones parent = HumanBodyBones.Hips;
            for(int j = i - 1; j >= 0; j--){
                boneTrans = avatar.GetBoneTransform(rightHipsToFoot[j]);
                if(boneTrans == null) continue;
                parent = rightHipsToFoot[j];
                break;
            }

            HumanBodyBones child = avatar.GetBoneTransform(HumanBodyBones.RightToes) != null ? HumanBodyBones.RightToes : HumanBodyBones.RightFoot;
            for(int j = i + 1; j < rightHipsToFoot.Length; j++){
                boneTrans = avatar.GetBoneTransform(rightHipsToFoot[j]);
                if(boneTrans == null) continue;
                child = rightHipsToFoot[j];
                break;
            }
            
            poseJoints[bone] = new Joint(avatar, bone, parent, child, forward);
        }
        
        poseJoints[HumanBodyBones.Hips].inverse = Quaternion.Inverse(Quaternion.LookRotation(forward));
    }

    public void PoseRender(ComputeBuffer poseWorldBuffer){
        var poseLandmarks = new Vector4[33]; // scoreなし
        poseWorldBuffer.GetData(poseLandmarks);

        for(int i = 0; i < 33; i++){
            poseLandmarks[i] = new Vector4(-poseLandmarks[i].x, poseLandmarks[i].y, -poseLandmarks[i].z, poseLandmarks[i].w);
        }


        Vector3 hipPosition = (poseLandmarks[23] + poseLandmarks[24]) / 2.0f;
        Vector3 neckPosition = (poseLandmarks[11] + poseLandmarks[12]) / 2.0f;
        Vector3 spinePosition = (hipPosition + neckPosition) / 2.0f;
        Vector3 hipToNeckVec = neckPosition - hipPosition;


        var forward = TriangleNormal(spinePosition, poseLandmarks[23], poseLandmarks[24]);
        var hipTransform = avatar.GetBoneTransform(HumanBodyBones.Hips);
        hipTransform.rotation = Quaternion.LookRotation(forward, (spinePosition - hipPosition).normalized) * poseJoints[HumanBodyBones.Hips].inverse *  poseJoints[HumanBodyBones.Hips].initRotation;
        

        //right arm
        Vector3 toChild = poseLandmarks[14] - poseLandmarks[12];
        var boneTrans = avatar.GetBoneTransform(HumanBodyBones.RightUpperArm);
        var rot = Quaternion.LookRotation(-toChild, forward) * poseJoints[HumanBodyBones.RightUpperArm].inverse * poseJoints[HumanBodyBones.RightUpperArm].initRotation;
        boneTrans.rotation = rot;

        toChild = poseLandmarks[16] - poseLandmarks[14];
        boneTrans = avatar.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rot = Quaternion.LookRotation(-toChild, forward) * poseJoints[HumanBodyBones.RightLowerArm].inverse * poseJoints[HumanBodyBones.RightLowerArm].initRotation;
        boneTrans.rotation = rot;


        // left arm
        toChild = poseLandmarks[13] - poseLandmarks[11];
        boneTrans = avatar.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rot = Quaternion.LookRotation(-toChild, forward) * poseJoints[HumanBodyBones.LeftUpperArm].inverse * poseJoints[HumanBodyBones.LeftUpperArm].initRotation;
        boneTrans.rotation = rot;

        toChild = poseLandmarks[15] - poseLandmarks[13];
        boneTrans = avatar.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        rot = Quaternion.LookRotation(-toChild, forward) * poseJoints[HumanBodyBones.LeftLowerArm].inverse * poseJoints[HumanBodyBones.LeftLowerArm].initRotation;
        boneTrans.rotation = rot;

        //right leg
        toChild = poseLandmarks[26] - poseLandmarks[24];
        boneTrans = avatar.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        rot = Quaternion.LookRotation(-toChild, forward) * poseJoints[HumanBodyBones.RightUpperLeg].inverse * poseJoints[HumanBodyBones.RightUpperLeg].initRotation;
        boneTrans.rotation = rot;

        toChild = poseLandmarks[28] - poseLandmarks[26];
        boneTrans = avatar.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        rot = Quaternion.LookRotation(-toChild, forward) * poseJoints[HumanBodyBones.RightLowerLeg].inverse * poseJoints[HumanBodyBones.RightLowerLeg].initRotation;
        boneTrans.rotation = rot;

        toChild = poseLandmarks[32] - poseLandmarks[28];
        boneTrans = avatar.GetBoneTransform(HumanBodyBones.RightFoot);
        rot = Quaternion.LookRotation(-toChild, forward) * poseJoints[HumanBodyBones.RightFoot].inverse * poseJoints[HumanBodyBones.RightFoot].initRotation;
        boneTrans.rotation = rot;
        Debug.Log(poseJoints[HumanBodyBones.RightFoot].childBone);
        
        //left leg
        toChild = poseLandmarks[25] - poseLandmarks[23];
        boneTrans = avatar.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rot = Quaternion.LookRotation(-toChild, forward) * poseJoints[HumanBodyBones.LeftUpperLeg].inverse * poseJoints[HumanBodyBones.LeftUpperLeg].initRotation;
        boneTrans.rotation = rot;

        toChild = poseLandmarks[27] - poseLandmarks[25];
        boneTrans = avatar.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rot = Quaternion.LookRotation(-toChild, forward) * poseJoints[HumanBodyBones.LeftLowerLeg].inverse * poseJoints[HumanBodyBones.LeftLowerLeg].initRotation;
        boneTrans.rotation = rot;

        toChild = poseLandmarks[31] - poseLandmarks[27];
        boneTrans = avatar.GetBoneTransform(HumanBodyBones.LeftFoot);
        rot = Quaternion.LookRotation(-toChild, forward) * poseJoints[HumanBodyBones.LeftFoot].inverse * poseJoints[HumanBodyBones.LeftFoot].initRotation;
        boneTrans.rotation = rot;
    }
}
