using System;
using System.Collections.Generic;
using UnityEngine;

namespace HolisticMotionCapture
{
    partial class HolisticMotionCapturePipeline
    {
        #region  private variables
        Dictionary<HumanBodyBones, Joint> poseJoints;
        bool isUpperBodyOnly;
        List<LowPassFilterVector3> pose_lpfs;
        List<Vector4> lpfedPoseBuffers;
        Dictionary<HumanBodyBones, LowPassFilterQuaternion> poseRotationLpfs;
        #endregion

        void PoseInit()
        {
            poseRotationLpfs = new Dictionary<HumanBodyBones, LowPassFilterQuaternion>();

            // default: T pose to A pose
            float upperArmAngle = 60;
            avatar.GetBoneTransform(HumanBodyBones.LeftUpperArm).localRotation = Quaternion.Euler(0, 0, upperArmAngle);
            avatar.GetBoneTransform(HumanBodyBones.RightUpperArm).localRotation = Quaternion.Euler(0, 0, -upperArmAngle);

            HumanBodyBones[] hipsToHead = new HumanBodyBones[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head };
            HumanBodyBones[] leftShoulderToHand = new HumanBodyBones[]{
            HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand
        };
            HumanBodyBones[] rightShoulderToHand = new HumanBodyBones[]{
            HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand
        };
            HumanBodyBones[] leftHipsToFoot = new HumanBodyBones[]{
            HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes
        };
            HumanBodyBones[] rightHipsToFoot = new HumanBodyBones[]{
            HumanBodyBones.Hips,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes
        };

            var boneListStartIndexs = new int[] { 0, 3, 3, 1, 1 };
            var boneLists = new HumanBodyBones[][]
            {
            hipsToHead,
            leftShoulderToHand,
            rightShoulderToHand,
            leftHipsToFoot,
            rightHipsToFoot
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

            poseJoints = new Dictionary<HumanBodyBones, Joint>();
            var forward = TriangleNormal(avatar.GetBoneTransform(HumanBodyBones.Spine).position, avatar.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position, avatar.GetBoneTransform(HumanBodyBones.RightUpperLeg).position);

            for (int i = 0; i < boneLists.Length; i++)
            {
                var boneList = boneLists[i];
                var boneListStartIndex = boneListStartIndexs[i];
                var fallbackParentBone = fallbackParentBones[i];
                var fallbackChildBone = fallbackChildBones[i];

                for (int j = boneListStartIndex; j < boneList.Length; j++)
                {
                    var boneTrans = avatar.GetBoneTransform(boneList[j]);
                    if (boneTrans == null) continue;
                    var bone = boneList[j];

                    HumanBodyBones parent = fallbackParentBone;
                    for (int k = j - 1; k >= 0; k--)
                    {
                        boneTrans = avatar.GetBoneTransform(boneList[k]);
                        if (boneTrans == null) continue;
                        parent = boneList[k];
                        break;
                    }

                    HumanBodyBones child = fallbackChildBone;
                    for (int k = j + 1; k < boneList.Length; k++)
                    {
                        boneTrans = avatar.GetBoneTransform(boneList[k]);
                        if (boneTrans == null) continue;
                        child = boneList[k];
                        break;
                    }

                    var inverseRot = Quaternion.identity;
                    if (bone != child)
                    {
                        inverseRot = Quaternion.Inverse(Quaternion.LookRotation(avatar.GetBoneTransform(bone).position - avatar.GetBoneTransform(child).position, forward));
                    }
                    var initRot = avatar.GetBoneTransform(bone).rotation;
                    poseJoints[bone] = new Joint(bone, parent, child, initRot, inverseRot);
                    poseRotationLpfs[bone] = new LowPassFilterQuaternion(0.25f, initRot);
                }
            }

            poseJoints[HumanBodyBones.Hips].inverseRotation = Quaternion.Inverse(Quaternion.LookRotation(forward));
            poseRotationLpfs[HumanBodyBones.Hips] = new LowPassFilterQuaternion(0.5f, avatar.GetBoneTransform(HumanBodyBones.Hips).rotation);

            poseJoints[HumanBodyBones.Head] = new Joint(HumanBodyBones.Head, HumanBodyBones.Head, HumanBodyBones.Head, avatar.GetBoneTransform(HumanBodyBones.Head).rotation, Quaternion.Inverse(Quaternion.LookRotation(forward)));
            poseRotationLpfs[HumanBodyBones.Hips] = new LowPassFilterQuaternion(0.5f, avatar.GetBoneTransform(HumanBodyBones.Head).rotation);
        }

        void PoseRender(HolisticMocapType mocapType, float scoreThreshold, bool isUpperBodyOnly, float lerpPercentage)
        {
            if (mocapType == HolisticMocapType.face_only) return;

            // Reset pose if huamn is not visible.
            if (holisticPipeline.GetPoseWorldLandmark(holisticPipeline.poseVertexCount).x < scoreThreshold)
            {
                ResetPose(lerpPercentage);
                return;
            }

            // Reset pose and update pose in below if mode was changed.
            if (this.isUpperBodyOnly != isUpperBodyOnly)
            {
                ResetPose(lerpPercentage);
                this.isUpperBodyOnly = isUpperBodyOnly;
            }

            ApplyPoseLandmarkLpf(scoreThreshold);

            // Caluculate positions of hip, neck and spine.
            var rightHipIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.RightUpperLeg];
            var leftHipIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.LeftUpperLeg];
            var rightShoulderIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.RightUpperArm];
            var leftShoulderIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.LeftUpperArm];
            Vector3 hipPosition = (RotatePoseLandmark(rightHipIndex) + RotatePoseLandmark(leftHipIndex)) / 2.0f;
            Vector3 neckPosition = (RotatePoseLandmark(rightShoulderIndex) + RotatePoseLandmark(leftShoulderIndex)) / 2.0f;
            Vector3 spinePosition = (hipPosition + neckPosition) / 2.0f;

            // Caluculate avatar forward direction and hip rotation.
            var forward = TriangleNormal(spinePosition, RotatePoseLandmark(leftHipIndex), RotatePoseLandmark(rightHipIndex));
            var hipScore = (RotatePoseLandmark(leftHipIndex).w + RotatePoseLandmark(rightHipIndex).w) * 0.5f;
            if (hipScore > scoreThreshold && !isUpperBodyOnly)
            {
                var hipRotation = Quaternion.LookRotation(forward, (spinePosition - hipPosition).normalized) * poseJoints[HumanBodyBones.Hips].inverseRotation * poseJoints[HumanBodyBones.Hips].initRotation;
                var hipTransform = avatar.GetBoneTransform(HumanBodyBones.Hips);
                // hipRotation = Quaternion.Lerp(hipTransform.rotation, hipRotation, lerpPercentage);

                poseRotationLpfs[HumanBodyBones.Hips].Add(hipRotation);
                hipRotation = poseRotationLpfs[HumanBodyBones.Hips].Get();
                hipRotation = BoneRotationClamp.Clamp(hipTransform, HumanBodyBones.Hips, hipRotation);
                hipTransform.rotation = hipRotation;
            }

            // Rotate head with pose landmark.
            var leftEyeLandmark = RotatePoseLandmark(2);
            var rightEyeLandmark = RotatePoseLandmark(5);
            var leftMouthLandmark = RotatePoseLandmark(9);
            var rightMouthLandmark = RotatePoseLandmark(10);
            var eyeMid = (leftEyeLandmark + rightEyeLandmark) * 0.5f;
            var mouthMid = (leftMouthLandmark + rightMouthLandmark) * 0.5f;
            var headScore = (eyeMid.w + mouthMid.w) * 0.5f;
            var headForward = Vector3.Cross(eyeMid - mouthMid, leftMouthLandmark - rightMouthLandmark);
            if (headScore > scoreThreshold)
            {
                var headRotation = Quaternion.LookRotation(headForward, (eyeMid - mouthMid).normalized) * poseJoints[HumanBodyBones.Head].inverseRotation * poseJoints[HumanBodyBones.Head].initRotation;
                if (isUpperBodyOnly || (!isUpperBodyOnly && hipScore > scoreThreshold))
                {
                    var spineRotationEulerAngles = headRotation.eulerAngles;
                    var spineRotation = Quaternion.Euler(headRotation.eulerAngles + new Vector3(-20, 0, 0));
                    var spineTransform = avatar.GetBoneTransform(HumanBodyBones.Spine);
                    // spineRotation = Quaternion.Lerp(spineTransform.rotation, spineRotation, lerpPercentage * 0.5f);

                    poseRotationLpfs[HumanBodyBones.Spine].Add(spineRotation);
                    spineRotation = poseRotationLpfs[HumanBodyBones.Spine].Get();
                    spineRotation = BoneRotationClamp.Clamp(spineTransform, HumanBodyBones.Spine, spineRotation);
                    spineTransform.rotation = spineRotation;
                }

                var headTransform = avatar.GetBoneTransform(HumanBodyBones.Head);
                // headRotation = Quaternion.Lerp(headTransform.rotation, headRotation, lerpPercentage);
                poseRotationLpfs[HumanBodyBones.Head].Add(headRotation);
                headRotation = poseRotationLpfs[HumanBodyBones.Head].Get();
                headRotation = BoneRotationClamp.Clamp(headTransform, HumanBodyBones.Head, headRotation);
                headTransform.rotation = headRotation;
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
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
            };
            List<HumanBodyBones> rotatedBones = new List<HumanBodyBones>();
            rotatedBones.AddRange(upperBodyBones);
            if (!isUpperBodyOnly) rotatedBones.AddRange(lowerBodyBones);

            // Rotate arms and legs.
            foreach (var bone in rotatedBones)
            {
                var poseJoint = poseJoints[bone];
                var boneLandmarkIndex = BoneToHolisticIndex.PoseTable[bone];
                var childLandmarkIndex = BoneToHolisticIndex.PoseTable[poseJoint.childBone];
                float parentScore = RotatePoseLandmark(boneLandmarkIndex).w;
                float childScore = RotatePoseLandmark(childLandmarkIndex).w;

                Vector3 toChild = RotatePoseLandmark(childLandmarkIndex) - RotatePoseLandmark(boneLandmarkIndex);
                var rot = Quaternion.LookRotation(-toChild, forward) * poseJoints[bone].inverseRotation * poseJoints[bone].initRotation;
                var boneTrans = avatar.GetBoneTransform(bone);

                if (parentScore < scoreThreshold && childScore < scoreThreshold)
                {
                    rot = poseJoints[bone].initRotation;
                    rot = Quaternion.Lerp(boneTrans.rotation, rot, lerpPercentage);
                }
                else
                {
                    poseRotationLpfs[bone].Add(rot);
                    rot = poseRotationLpfs[bone].Get();
                }
                rot = BoneRotationClamp.Clamp(boneTrans, bone, rot);
                boneTrans.rotation = rot;
            }
        }

        void ResetPose(float lerpPercentage)
        {
            foreach (var poseJoint in poseJoints)
            {
                var boneTrans = avatar.GetBoneTransform(poseJoint.Key);
                boneTrans.rotation = Quaternion.Lerp(boneTrans.rotation, poseJoints[poseJoint.Key].initRotation, lerpPercentage);
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

        void ApplyPoseLandmarkLpf(float scoreThreshold)
        {
            if (pose_lpfs == null || lpfedPoseBuffers == null)
            {
                pose_lpfs = new List<LowPassFilterVector3>();
                lpfedPoseBuffers = new List<Vector4>();
                for (int i = 0; i < holisticPipeline.poseVertexCount; i++)
                {
                    var landmark = holisticPipeline.GetPoseWorldLandmark(i);
                    pose_lpfs.Add(new LowPassFilterVector3(0.25f, landmark));
                    lpfedPoseBuffers.Add(landmark);
                }
            }

            for (int i = 0; i < holisticPipeline.poseVertexCount; i++)
            {
                var landmark = holisticPipeline.GetPoseWorldLandmark(i);

                // if (landmark.w >= scoreThreshold)
                {
                    pose_lpfs[i].Add(landmark);
                }
                var p = pose_lpfs[i].Get();
                lpfedPoseBuffers[i] = new Vector4(p.x, p.y, p.z, landmark.w);
            }
        }

        Vector4 RotatePoseLandmark(int index)
        {
            var landmark = lpfedPoseBuffers[index];
            return new Vector4(-landmark.x, landmark.y, -landmark.z, landmark.w);
        }
    }
}