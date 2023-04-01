using System.Collections.Generic;
using UnityEngine;

namespace HolisticMotionCapture
{
    partial class HolisticMotionCapturePipeline
    {
        Dictionary<HumanBodyBones, Joint> handJoints;

        void HandInit()
        {
            handJoints = new Dictionary<HumanBodyBones, Joint>();
            PerHandInit(true);
            PerHandInit(false);
        }

        void PerHandInit(bool isLeft)
        {
            var wrist = isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            int offset = isLeft ? 0 : 15;

            HumanBodyBones[] thumbList = new HumanBodyBones[] { wrist, HumanBodyBones.LeftThumbProximal + offset, HumanBodyBones.LeftThumbIntermediate + offset, HumanBodyBones.LeftThumbDistal + offset };
            HumanBodyBones[] indexList = new HumanBodyBones[] { wrist, HumanBodyBones.LeftIndexProximal + offset, HumanBodyBones.LeftIndexIntermediate + offset, HumanBodyBones.LeftIndexDistal + offset };
            HumanBodyBones[] middleList = new HumanBodyBones[] { wrist, HumanBodyBones.LeftMiddleProximal + offset, HumanBodyBones.LeftMiddleIntermediate + offset, HumanBodyBones.LeftMiddleDistal + offset };
            HumanBodyBones[] ringList = new HumanBodyBones[] { wrist, HumanBodyBones.LeftRingProximal + offset, HumanBodyBones.LeftRingIntermediate + offset, HumanBodyBones.LeftRingDistal + offset };
            HumanBodyBones[] littleList = new HumanBodyBones[] { wrist, HumanBodyBones.LeftLittleProximal + offset, HumanBodyBones.LeftLittleIntermediate + offset, HumanBodyBones.LeftLittleDistal + offset };

            const int handListStartIndex = 1;
            var handLists = new HumanBodyBones[][]
            {
            thumbList,
            indexList,
            middleList,
            ringList,
            littleList
            };

            var handDirection = avatar.GetBoneTransform(HumanBodyBones.LeftMiddleProximal + offset).position - avatar.GetBoneTransform(wrist).position;
            var wristToIndex = avatar.GetBoneTransform(HumanBodyBones.LeftIndexProximal + offset).position - avatar.GetBoneTransform(wrist).position;
            var handUp = Vector3.Cross(handDirection, wristToIndex);
            if (!isLeft) handUp *= -1;
            var handForward = Vector3.Cross(handUp, handDirection);
            if (!isLeft) handForward *= -1;

            for (int i = 0; i < handLists.Length; i++)
            {
                var handList = handLists[i];

                for (int j = handListStartIndex; j < handList.Length; j++)
                {
                    var boneTrans = avatar.GetBoneTransform(handList[j]);
                    if (boneTrans == null) continue;
                    var bone = handList[j];

                    HumanBodyBones parent = wrist;
                    for (int k = j - 1; k >= 0; k--)
                    {
                        boneTrans = avatar.GetBoneTransform(handList[k]);
                        if (boneTrans == null) continue;
                        parent = handList[k];
                        break;
                    }

                    HumanBodyBones child = bone;
                    for (int k = j + 1; k < handList.Length; k++)
                    {
                        boneTrans = avatar.GetBoneTransform(handList[k]);
                        if (boneTrans == null) continue;
                        child = handList[k];
                        break;
                    }

                    var inverseRot = Quaternion.identity;
                    if (bone != child)
                    {
                        inverseRot = Quaternion.Inverse(Quaternion.LookRotation(avatar.GetBoneTransform(bone).position - avatar.GetBoneTransform(child).position, handForward));
                    }
                    else if (avatar.GetBoneTransform(bone).childCount > 0)
                    {
                        inverseRot = Quaternion.Inverse(Quaternion.LookRotation(avatar.GetBoneTransform(bone).position - avatar.GetBoneTransform(bone).GetChild(0).position, handForward));
                    }
                    handJoints[bone] = new Joint(bone, parent, child, avatar.GetBoneTransform(bone).rotation, inverseRot);
                }
            }
            handJoints[wrist] = new Joint(wrist, wrist, wrist, avatar.GetBoneTransform(wrist).rotation, Quaternion.Inverse(Quaternion.LookRotation(handForward)));
        }

        void HandRender(HolisticMocapType mocapType, bool isLeft, float scoreThreshold, float lerpPercentage)
        {
            if (mocapType != HolisticMocapType.full && mocapType != HolisticMocapType.pose_and_hand)
            {
                return;
            }

            var wrist = isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            int offset = isLeft ? 0 : 15;
            var wristScore = mediapipeRunner.GetPoseLandmark(BoneToHolisticIndex.PoseTable[wrist]).w;
            if (wristScore < scoreThreshold)
            {
                ResetHand(isLeft, lerpPercentage);
                return;
            }

            var handDirection = RotateHandLandmark(BoneToHolisticIndex.handTable[HumanBodyBones.LeftMiddleProximal + offset], isLeft) - RotateHandLandmark(BoneToHolisticIndex.handTable[wrist], isLeft);
            var wristToIndex = RotateHandLandmark(BoneToHolisticIndex.handTable[HumanBodyBones.LeftIndexProximal + offset], isLeft) - RotateHandLandmark(BoneToHolisticIndex.handTable[wrist], isLeft);
            var handUp = Vector3.Cross(handDirection, wristToIndex);
            if (!isLeft) handUp *= -1;
            var handForward = Vector3.Cross(handUp, handDirection);
            if (!isLeft) handForward *= -1;
            var wristRotation = Quaternion.LookRotation(handForward, handUp) * handJoints[wrist].inverseRotation * handJoints[wrist].initRotation;

            // rotate the arm because avoid the wrist will be twisted
            var lowerArmBoneTransform = avatar.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (isLeft)
            {
                lowerArmBoneTransform = avatar.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            }
            var wristTransform = avatar.GetBoneTransform(wrist);
            var lerpedWristRotation = Quaternion.Lerp(wristTransform.rotation, avatar.GetBoneTransform(HumanBodyBones.Hips).rotation * wristRotation, lerpPercentage);
            lowerArmBoneTransform.rotation = Quaternion.Lerp(lowerArmBoneTransform.rotation, lerpedWristRotation, 0.2f);
            wristTransform.rotation = lerpedWristRotation;


            foreach (var bone in getPerHandRotatedBones(isLeft))
            {
                var handJoint = handJoints[bone];
                var boneLandmarkIndex = BoneToHolisticIndex.handTable[bone];
                var childLandmarkIndex = BoneToHolisticIndex.handTable[handJoint.childBone];

                Vector3 toChild = RotateHandLandmark(childLandmarkIndex, isLeft) - RotateHandLandmark(boneLandmarkIndex, isLeft);
                if (boneLandmarkIndex == childLandmarkIndex)
                {
                    toChild = RotateHandLandmark(boneLandmarkIndex + 1, isLeft) - RotateHandLandmark(boneLandmarkIndex, isLeft);
                }

                var rot = Quaternion.LookRotation(-toChild, handForward) * handJoints[bone].inverseRotation * handJoints[bone].initRotation;
                var boneTrans = avatar.GetBoneTransform(bone);
                boneTrans.rotation = Quaternion.Lerp(boneTrans.rotation, avatar.GetBoneTransform(HumanBodyBones.Hips).rotation * rot, lerpPercentage);
            }
        }

        void ResetHand(bool isLeft, float lerpPercentage)
        {
            var wristBone = isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            var wristTrans = avatar.GetBoneTransform(wristBone);
            wristTrans.rotation = Quaternion.Lerp(wristTrans.rotation, handJoints[wristBone].initRotation, lerpPercentage);

            foreach (var rotatedBone in getPerHandRotatedBones(isLeft))
            {
                var handJoint = handJoints[rotatedBone];
                var boneTrans = avatar.GetBoneTransform(rotatedBone);
                boneTrans.rotation = Quaternion.Lerp(boneTrans.rotation, handJoint.initRotation, lerpPercentage);
            }
        }

        HumanBodyBones[] getPerHandRotatedBones(bool isLeft)
        {
            int offset = isLeft ? 0 : 15;
            var rotatedBones = new HumanBodyBones[]{
            HumanBodyBones.LeftThumbProximal + offset, HumanBodyBones.LeftThumbIntermediate + offset, HumanBodyBones.LeftThumbDistal + offset,
            HumanBodyBones.LeftIndexProximal + offset, HumanBodyBones.LeftIndexIntermediate + offset, HumanBodyBones.LeftIndexDistal + offset,
            HumanBodyBones.LeftMiddleProximal + offset, HumanBodyBones.LeftMiddleIntermediate + offset, HumanBodyBones.LeftMiddleDistal + offset,
            HumanBodyBones.LeftRingProximal + offset, HumanBodyBones.LeftRingIntermediate + offset, HumanBodyBones.LeftRingDistal + offset,
            HumanBodyBones.LeftLittleProximal + offset, HumanBodyBones.LeftLittleIntermediate + offset, HumanBodyBones.LeftLittleDistal + offset
        };
            return rotatedBones;
        }

        Vector4 RotateHandLandmark(int index, bool isLeft)
        {
            var landmark = isLeft ? mediapipeRunner.GetLeftHandLandmark(index) : mediapipeRunner.GetRightHandLandmark(index);
            return landmark;
        }
    }
}