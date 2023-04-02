using UnityEngine;

namespace HolisticMotionCapture
{
    public partial class HolisticMotionCapturePipeline : System.IDisposable
    {
        MediaPipeRunnerBase _mediapipeRunner;
        public MediaPipeRunnerBase mediapipeRunner
        {
            get { return this._mediapipeRunner; }
            private set { this._mediapipeRunner = value; }
        }

        Animator avatar;
        const float maxFps = 30.0f;
        float lastPoseUpdateTime;

        public HolisticMotionCapturePipeline(Animator avatarAnimator)
        {
            avatar = avatarAnimator;
            mediapipeRunner = new MediaPipeUnityPluginRunner(false);
            // mediapipeRunner = new HolisticBarracudaRunner(false);
            HandInit();
            PoseInit();
            FaceInit();
        }

        public void Dispose()
        {
            mediapipeRunner.Dispose();
        }

        public void AvatarPoseRender(
            Texture inputTexture,
            Transform lookTargetWorldPosition = null,
            float poseScoreThreshold = 0.5f,
            float handScoreThreshold = 0.5f,
            float faceScoreThreshold = 0.5f,
            bool isUpperBodyOnly = false,
            float lerpPercentage = 0.3f,
            HolisticMocapType mocapType = HolisticMocapType.full)
        {
            float nowTime = Time.time;
            if (nowTime - lastPoseUpdateTime < 1.0f / maxFps)
            {
                return;
            }
            lastPoseUpdateTime = nowTime;

            mediapipeRunner.ProcessImage(inputTexture, mocapType);
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