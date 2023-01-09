using System.Collections.Generic;
using UnityEngine;

namespace HolisticMotionCapture
{
    public static class BoneToHolisticIndex
    {
        public static Dictionary<HumanBodyBones, int> PoseTable = new Dictionary<HumanBodyBones, int>(){
        // Right arm
        {HumanBodyBones.RightUpperArm, 12},
        {HumanBodyBones.RightLowerArm, 14},
        {HumanBodyBones.RightHand, 16},
        
        // Left arm
        {HumanBodyBones.LeftUpperArm, 11},
        {HumanBodyBones.LeftLowerArm, 13},
        {HumanBodyBones.LeftHand, 15},

        // Right leg
        {HumanBodyBones.RightUpperLeg, 24},
        {HumanBodyBones.RightLowerLeg, 26},
        {HumanBodyBones.RightFoot, 28},
        {HumanBodyBones.RightToes, 32},

        // Left leg
        {HumanBodyBones.LeftUpperLeg, 23},
        {HumanBodyBones.LeftLowerLeg, 25},
        {HumanBodyBones.LeftFoot, 27},
        {HumanBodyBones.LeftToes, 31}
    };

        public static Dictionary<HumanBodyBones, int> handTable = new Dictionary<HumanBodyBones, int>(){
        {HumanBodyBones.LeftHand, 0},

        {HumanBodyBones.LeftThumbProximal, 1},
        {HumanBodyBones.LeftThumbIntermediate, 2},
        {HumanBodyBones.LeftThumbDistal, 3},

        {HumanBodyBones.LeftIndexProximal, 5},
        {HumanBodyBones.LeftIndexIntermediate, 6},
        {HumanBodyBones.LeftIndexDistal, 7},

        {HumanBodyBones.LeftMiddleProximal, 9},
        {HumanBodyBones.LeftMiddleIntermediate, 10},
        {HumanBodyBones.LeftMiddleDistal, 11},

        {HumanBodyBones.LeftRingProximal, 13},
        {HumanBodyBones.LeftRingIntermediate, 14},
        {HumanBodyBones.LeftRingDistal, 15},

        {HumanBodyBones.LeftLittleProximal, 17},
        {HumanBodyBones.LeftLittleIntermediate, 18},
        {HumanBodyBones.LeftLittleDistal, 19},


        {HumanBodyBones.RightHand, 0},

        {HumanBodyBones.RightThumbProximal, 1},
        {HumanBodyBones.RightThumbIntermediate, 2},
        {HumanBodyBones.RightThumbDistal, 3},

        {HumanBodyBones.RightIndexProximal, 5},
        {HumanBodyBones.RightIndexIntermediate, 6},
        {HumanBodyBones.RightIndexDistal, 7},

        {HumanBodyBones.RightMiddleProximal, 9},
        {HumanBodyBones.RightMiddleIntermediate, 10},
        {HumanBodyBones.RightMiddleDistal, 11},

        {HumanBodyBones.RightRingProximal, 13},
        {HumanBodyBones.RightRingIntermediate, 14},
        {HumanBodyBones.RightRingDistal, 15},

        {HumanBodyBones.RightLittleProximal, 17},
        {HumanBodyBones.RightLittleIntermediate, 18},
        {HumanBodyBones.RightLittleDistal, 19}
    };
    }
}