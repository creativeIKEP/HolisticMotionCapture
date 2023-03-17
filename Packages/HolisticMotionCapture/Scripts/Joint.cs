using UnityEngine;

namespace HolisticMotionCapture
{
    public class Joint
    {
        public HumanBodyBones bone;
        public HumanBodyBones parentBone;
        public HumanBodyBones childBone;
        public Quaternion initRotation;
        public Quaternion inverseRotation;
        public LowPassFilter filter;

        public Joint(HumanBodyBones bone, HumanBodyBones parent, HumanBodyBones child, Quaternion initRot, Quaternion inverseRot)
        {
            this.bone = bone;
            parentBone = parent;
            childBone = child;
            initRotation = initRot;
            inverseRotation = inverseRot;
            filter = new LowPassFilter(1, 1);
        }
    };
}