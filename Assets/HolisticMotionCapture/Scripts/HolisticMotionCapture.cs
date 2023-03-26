using UnityEngine;

namespace HolisticMotionCapture
{
    public partial class HolisticMotionCapturePipeline : System.IDisposable
    {
        MediaPipeRunner _holisticPipeline;
        public MediaPipeRunner holisticPipeline
        {
            get { return this._holisticPipeline; }
            private set { this._holisticPipeline = value; }
        }

        Animator avatar;
        const float maxFps = 30.0f;
        float lastPoseUpdateTime;

        public HolisticMotionCapturePipeline(Animator avatarAnimator)
        {
            avatar = avatarAnimator;
            holisticPipeline = new MediaPipeRunner(false);
            HandInit();
            PoseInit();
            FaceInit();
        }

        public void Dispose()
        {
            holisticPipeline.Dispose();
        }

        public void AvatarPoseRender(
            Texture inputTexture,
            Transform lookTargetWorldPosition = null,
            float poseScoreThreshold = 0.5f,
            float handScoreThreshold = 0.5f,
            float faceScoreThreshold = 0.5f,
            bool isUpperBodyOnly = false,
            float lerpPercentage = 0.3f,
            HolisticMocapType mocapType = HolisticMocapType.full,
            // BlazePoseModel blazePoseModel = BlazePoseModel.full,
            float poseDetectionThreshold = 0.75f,
            float poseDetectionIouThreshold = 0.3f)
        {
            float nowTime = Time.time;
            if (nowTime - lastPoseUpdateTime < 1.0f / maxFps)
            {
                return;
            }
            lastPoseUpdateTime = nowTime;

            // holisticPipeline.ProcessImage(inputTexture, (HolisticInferenceType)mocapType, blazePoseModel, poseDetectionThreshold, poseDetectionIouThreshold);
            holisticPipeline.ProcessImage(inputTexture);
            PoseRender(mocapType, poseScoreThreshold, isUpperBodyOnly, lerpPercentage);
            HandRender(mocapType, true, handScoreThreshold, lerpPercentage);
            HandRender(mocapType, false, handScoreThreshold, lerpPercentage);
            FaceRender(mocapType, faceScoreThreshold, lookTargetWorldPosition);
        }

        public void ResetAvatar(float lerpPercentage = 0.3f)
        {
            ResetPose(lerpPercentage);
            ResetHand(true, lerpPercentage);
            ResetHand(false, lerpPercentage);
            ResetFace();
        }
    }
}