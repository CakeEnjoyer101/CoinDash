using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class RuntimePlayerVisualAnimator : MonoBehaviour
{
    struct BonePose
    {
        public Transform bone;
        public Quaternion baseRotation;
        public Vector3 runEulerScale;
        public float runPhase;
        public Vector3 jumpEulerScale;
    }

    Animation animationComponent;
    Transform observedRoot;
    PlayerMovement observedMovement;
    Animator humanoidAnimator;
    string idleClipName;
    string runClipName;
    string jumpClipName;
    string currentClipName;
    Vector3 lastRootPosition;
    float lateralLean;
    float lateralLeanVelocity;
    float smoothedRunWeight;
    float smoothedRunWeightVelocity;
    float smoothedJumpWeight;
    float smoothedJumpWeightVelocity;
    bool isJumping;
    bool isRunning;
    BonePose[] bonePoses = Array.Empty<BonePose>();
    Transform hipsBone;
    Transform spineBone;
    Transform headBone;
    Quaternion hipsBaseRotation;
    Quaternion spineBaseRotation;
    Quaternion headBaseRotation;

    public void Initialize(Transform root, AnimationClip[] clips)
    {
        observedRoot = root;
        observedMovement = root != null ? root.GetComponent<PlayerMovement>() : null;
        lastRootPosition = root != null ? root.position : Vector3.zero;
        CacheProceduralRig();

        if (clips == null || clips.Length == 0)
            return;

        animationComponent = gameObject.GetComponent<Animation>();
        if (animationComponent == null)
            animationComponent = gameObject.AddComponent<Animation>();

        foreach (var clip in clips.Where(c => c != null && !string.IsNullOrWhiteSpace(c.name)))
        {
            if (animationComponent.GetClip(clip.name) != null)
                continue;

            clip.legacy = true;
            animationComponent.AddClip(clip, clip.name);
        }

        idleClipName = FindClipName("idle");
        runClipName = FindClipName("run", "walk", "move");
        jumpClipName = FindClipName("jump", "air");

        if (string.IsNullOrEmpty(runClipName))
            runClipName = idleClipName;
        if (string.IsNullOrEmpty(idleClipName))
            idleClipName = runClipName;

        SetWrapMode(idleClipName, WrapMode.Loop);
        SetWrapMode(runClipName, WrapMode.Loop);
        SetWrapMode(jumpClipName, WrapMode.ClampForever);

        PlayClip(!string.IsNullOrEmpty(runClipName) ? runClipName : idleClipName);
    }

    void Update()
    {
        if (observedRoot == null)
            return;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 rootDelta = observedRoot.position - lastRootPosition;
        lastRootPosition = observedRoot.position;

        float forwardSpeed = Mathf.Abs(rootDelta.z) / deltaTime;
        float lateralSpeed = rootDelta.x / deltaTime;
        lateralLean = Mathf.SmoothDamp(lateralLean, Mathf.Clamp(lateralSpeed * 1.25f, -11f, 11f), ref lateralLeanVelocity, 0.12f, Mathf.Infinity, deltaTime);

        isJumping = observedMovement != null ? !observedMovement.IsGrounded : Mathf.Abs(rootDelta.y) > 0.02f;
        isRunning = observedMovement != null ? observedMovement.playerSpeed > 0.1f : forwardSpeed > 0.1f;

        if (animationComponent == null)
            return;

        if (isJumping && !string.IsNullOrEmpty(jumpClipName))
        {
            PlayClip(jumpClipName);
            return;
        }

        if (isRunning && !string.IsNullOrEmpty(runClipName))
        {
            PlayClip(runClipName);
            return;
        }

        PlayClip(idleClipName);
    }

    void LateUpdate()
    {
        ApplyProceduralPose();
    }

    void CacheProceduralRig()
    {
        humanoidAnimator = GetComponentInChildren<Animator>(true);

        hipsBone = ResolveBone(HumanBodyBones.Hips, "hips", "pelvis");
        spineBone = ResolveBone(HumanBodyBones.Spine, "spine", "chest", "upperchest");
        headBone = ResolveBone(HumanBodyBones.Head, "head");

        if (hipsBone != null)
            hipsBaseRotation = hipsBone.localRotation;
        if (spineBone != null)
            spineBaseRotation = spineBone.localRotation;
        if (headBone != null)
            headBaseRotation = headBone.localRotation;

        var poses = new List<BonePose>();
        AddBonePose(poses, ResolveBone(HumanBodyBones.LeftUpperArm, "leftupperarm", "leftarm", "arm_l"), new Vector3(-16f, 0f, 5f), Mathf.PI, new Vector3(-12f, 0f, 7f));
        AddBonePose(poses, ResolveBone(HumanBodyBones.RightUpperArm, "rightupperarm", "rightarm", "arm_r"), new Vector3(-16f, 0f, -5f), 0f, new Vector3(-12f, 0f, -7f));
        AddBonePose(poses, ResolveBone(HumanBodyBones.LeftLowerArm, "leftlowerarm", "leftforearm", "forearm_l"), new Vector3(-8f, 0f, 3f), Mathf.PI, new Vector3(-6f, 0f, 3f));
        AddBonePose(poses, ResolveBone(HumanBodyBones.RightLowerArm, "rightlowerarm", "rightforearm", "forearm_r"), new Vector3(-8f, 0f, -3f), 0f, new Vector3(-6f, 0f, -3f));
        AddBonePose(poses, ResolveBone(HumanBodyBones.LeftUpperLeg, "leftupleg", "leftthigh", "upleg_l"), new Vector3(18f, 0f, 3f), 0f, new Vector3(6f, 0f, 0f));
        AddBonePose(poses, ResolveBone(HumanBodyBones.RightUpperLeg, "rightupleg", "rightthigh", "upleg_r"), new Vector3(18f, 0f, -3f), Mathf.PI, new Vector3(6f, 0f, 0f));
        AddBonePose(poses, ResolveBone(HumanBodyBones.LeftLowerLeg, "leftleg", "leftcalf", "calf_l"), new Vector3(-12f, 0f, 0f), Mathf.PI, new Vector3(8f, 0f, 0f));
        AddBonePose(poses, ResolveBone(HumanBodyBones.RightLowerLeg, "rightleg", "rightcalf", "calf_r"), new Vector3(-12f, 0f, 0f), 0f, new Vector3(8f, 0f, 0f));
        bonePoses = poses.ToArray();
    }

    Transform ResolveBone(HumanBodyBones humanBone, params string[] fallbackKeywords)
    {
        if (humanoidAnimator != null && humanoidAnimator.isHuman)
        {
            var bone = humanoidAnimator.GetBoneTransform(humanBone);
            if (bone != null)
                return bone;
        }

        return FindBoneByKeywords(fallbackKeywords);
    }

    Transform FindBoneByKeywords(params string[] keywords)
    {
        if (keywords == null || keywords.Length == 0)
            return null;

        foreach (var transformChild in GetComponentsInChildren<Transform>(true))
        {
            string lowered = transformChild.name.ToLowerInvariant();
            for (int i = 0; i < keywords.Length; i++)
            {
                if (lowered.Contains(keywords[i]))
                    return transformChild;
            }
        }

        return null;
    }

    void AddBonePose(List<BonePose> poses, Transform bone, Vector3 runEulerScale, float runPhase, Vector3 jumpEulerScale)
    {
        if (bone == null)
            return;

        poses.Add(new BonePose
        {
            bone = bone,
            baseRotation = bone.localRotation,
            runEulerScale = runEulerScale,
            runPhase = runPhase,
            jumpEulerScale = jumpEulerScale
        });
    }

    void ApplyProceduralPose()
    {
        if ((bonePoses == null || bonePoses.Length == 0) && hipsBone == null && spineBone == null && headBone == null)
            return;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        float speedFactor = observedMovement != null
            ? Mathf.InverseLerp(6f, 20f, observedMovement.playerSpeed)
            : 0.65f;
        float cycle = Time.time * Mathf.Lerp(4.4f, 8.1f, speedFactor);

        float targetRunWeight = isRunning ? 1f : 0.12f;
        if (animationComponent != null && animationComponent.isPlaying)
            targetRunWeight *= 0.52f;
        smoothedRunWeight = Mathf.SmoothDamp(smoothedRunWeight, targetRunWeight, ref smoothedRunWeightVelocity, 0.11f, Mathf.Infinity, deltaTime);

        float targetJumpWeight = isJumping ? 1f : 0f;
        smoothedJumpWeight = Mathf.SmoothDamp(smoothedJumpWeight, targetJumpWeight, ref smoothedJumpWeightVelocity, 0.1f, Mathf.Infinity, deltaTime);

        float runWeight = smoothedRunWeight;
        float jumpWeight = smoothedJumpWeight;
        float poseBlend = 1f - Mathf.Exp(-11f * deltaTime);

        for (int i = 0; i < bonePoses.Length; i++)
        {
            BonePose pose = bonePoses[i];
            if (pose.bone == null)
                continue;

            float swing = Mathf.Sin(cycle + pose.runPhase);
            Vector3 euler = (pose.runEulerScale * swing * runWeight) + (pose.jumpEulerScale * jumpWeight);
            Quaternion targetRotation = pose.baseRotation * Quaternion.Euler(euler);
            pose.bone.localRotation = Quaternion.Slerp(pose.bone.localRotation, targetRotation, poseBlend);
        }

        if (hipsBone != null)
        {
            Quaternion hipsTarget = hipsBaseRotation * Quaternion.Euler(0f, lateralLean * 0.26f, -lateralLean * 0.22f);
            hipsBone.localRotation = Quaternion.Slerp(hipsBone.localRotation, hipsTarget, poseBlend);
        }

        if (spineBone != null)
        {
            float chestBob = Mathf.Sin(cycle * 0.5f) * 2.2f * runWeight;
            Quaternion spineTarget = spineBaseRotation * Quaternion.Euler((-3f * runWeight) + (-6f * jumpWeight) + chestBob, lateralLean * 0.3f, lateralLean * 0.22f);
            spineBone.localRotation = Quaternion.Slerp(spineBone.localRotation, spineTarget, poseBlend);
        }

        if (headBone != null)
        {
            float headBob = Mathf.Sin(cycle * 0.5f) * 1.5f * runWeight;
            Quaternion headTarget = headBaseRotation * Quaternion.Euler(headBob, -lateralLean * 0.18f, -lateralLean * 0.12f);
            headBone.localRotation = Quaternion.Slerp(headBone.localRotation, headTarget, poseBlend);
        }
    }

    string FindClipName(params string[] keywords)
    {
        foreach (AnimationState state in animationComponent)
        {
            string lowered = state.name.ToLowerInvariant();
            for (int i = 0; i < keywords.Length; i++)
            {
                if (lowered.Contains(keywords[i]))
                    return state.name;
            }
        }

        foreach (AnimationState state in animationComponent)
            return state.name;

        return null;
    }

    void SetWrapMode(string clipName, WrapMode wrapMode)
    {
        if (string.IsNullOrEmpty(clipName) || animationComponent == null || animationComponent[clipName] == null)
            return;

        animationComponent[clipName].wrapMode = wrapMode;
    }

    void PlayClip(string clipName)
    {
        if (string.IsNullOrEmpty(clipName) || currentClipName == clipName || animationComponent == null)
            return;

        if (animationComponent[clipName] == null)
            return;

        currentClipName = clipName;
        animationComponent.CrossFade(clipName, 0.18f);
    }
}
