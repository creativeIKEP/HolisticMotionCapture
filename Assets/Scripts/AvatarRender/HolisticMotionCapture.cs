using UnityEngine;

public enum HolisticMotionCaptureWorkType
{
    CPU,
    GPU
}

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
    HolisticMotionCaptureWorkType _workType;

    public HolisticMotionCapturePipeline(Animator avatarAnimator, HolisticMotionCaptureWorkType workType = HolisticMotionCaptureWorkType.CPU)
    {
        avatar = avatarAnimator;
        _workType = workType;
        if (workType == HolisticMotionCaptureWorkType.CPU)
        {
            mediapipeRunner = new MediaPipeUnityPluginRunner();
        }
        else
        {
            mediapipeRunner = new HolisticBarracudaRunner();
        }

        // default: T pose to A pose
        float upperArmAngle = 60;
        avatar.GetBoneTransform(HumanBodyBones.LeftUpperArm).localRotation = Quaternion.Euler(0, 0, upperArmAngle);
        avatar.GetBoneTransform(HumanBodyBones.RightUpperArm).localRotation = Quaternion.Euler(0, 0, -upperArmAngle);
        HandInit();
        PoseInit();
        FaceInit();
    }

    public void Dispose()
    {
        mediapipeRunner.Dispose();
    }

    public void ChangeWorkType(HolisticMotionCaptureWorkType type)
    {
        _workType = type;
        Dispose();
        if (type == HolisticMotionCaptureWorkType.CPU)
        {
            mediapipeRunner = new MediaPipeUnityPluginRunner();
        }
        else
        {
            mediapipeRunner = new HolisticBarracudaRunner();
        }
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
        PoseRender(mocapType, poseScoreThreshold, isUpperBodyOnly, lerpPercentage);
        HandRender(mocapType, true, handScoreThreshold, lerpPercentage);
        HandRender(mocapType, false, handScoreThreshold, lerpPercentage);
        FaceRender(mocapType, faceScoreThreshold, lookTargetWorldPosition);

        float nowTime = Time.time;
        if (nowTime - lastPoseUpdateTime < 1.0f / maxFps)
        {
            return;
        }
        lastPoseUpdateTime = nowTime;

        mediapipeRunner.ProcessImage(inputTexture, mocapType);
    }

    public void ResetAvatar(float lerpPercentage = 0.3f)
    {
        ResetPose(lerpPercentage);
        ResetHand(true, lerpPercentage);
        ResetHand(false, lerpPercentage);
        ResetFace();
    }
}
