using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public sealed class RuntimePlayerVisualAnimator : MonoBehaviour
{
    enum VisualState
    {
        None = -1,
        Run = 0,
        Jump = 1,
        Strafe = 2,
        Stumble = 3
    }

    const float CrossFadeSpeed = 12f;
    const float MinClipLength = 0.016f;
    const float FallbackBobSpeed = 10f;
    const float FallbackBobAmount = 0.04f;
    const float FallbackStrafeLean = 10f;
    const float FallbackJumpLift = 0.12f;
    const float LoopWrapPadding = 0.02f;
    const float MinimumJumpDuration = 0.48f;
    const float RunLoopWindow = 1.28f;

    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable[] clipPlayables;
    AnimationClip[] clips;
    float[] clipWeights;
    float[] clipTimes;
    Animator targetAnimator;
    Transform observedRoot;
    PlayerMovement observedMovement;
    VisualState currentState = VisualState.None;
    float stumbleUntil;
    bool initialized;
    Transform fallbackRoot;
    Vector3 fallbackBaseLocalPosition;
    Quaternion fallbackBaseLocalRotation;
    Vector3 fallbackBaseLocalScale;
    float fallbackCycle;

    public void Initialize(Transform root, AnimationClip[] loadedClips)
    {
        observedRoot = root;
        observedMovement = root != null ? root.GetComponent<PlayerMovement>() : null;
        clips = loadedClips ?? new AnimationClip[0];
        stumbleUntil = 0f;
        currentState = VisualState.None;
        fallbackCycle = 0f;
        initialized = false;

        DestroyGraph();

        targetAnimator = GetComponent<Animator>();
        if (targetAnimator == null)
            targetAnimator = GetComponentInChildren<Animator>(true);

        fallbackRoot = targetAnimator != null ? targetAnimator.transform : transform;
        fallbackBaseLocalPosition = fallbackRoot.localPosition;
        fallbackBaseLocalRotation = fallbackRoot.localRotation;
        fallbackBaseLocalScale = fallbackRoot.localScale;

        if (targetAnimator == null || !HasAnyPlayableClip())
            return;

        foreach (var animator in GetComponentsInChildren<Animator>(true))
        {
            if (animator != null && animator != targetAnimator)
                animator.enabled = false;
        }

        targetAnimator.enabled = true;
        targetAnimator.applyRootMotion = false;
        targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        targetAnimator.updateMode = AnimatorUpdateMode.Normal;
        targetAnimator.runtimeAnimatorController = null;
        targetAnimator.Rebind();
        targetAnimator.Update(0f);

        graph = PlayableGraph.Create("RuntimePlayerVisualAnimator");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        mixer = AnimationMixerPlayable.Create(graph, 4);
        clipPlayables = new AnimationClipPlayable[4];
        clipWeights = new float[4];
        clipTimes = new float[4];

        for (int i = 0; i < clipPlayables.Length; i++)
        {
            AnimationClip clip = i < clips.Length ? clips[i] : null;
            if (clip == null)
                continue;

            bool shouldLoop = IsLoopingIndex(i);
            clip.wrapMode = shouldLoop ? WrapMode.Loop : WrapMode.Once;
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(true);
            playable.SetApplyPlayableIK(false);
            playable.SetDuration(shouldLoop ? double.MaxValue : Mathf.Max(MinClipLength, clip.length));
            playable.SetTime(0d);
            playable.SetSpeed(0d);

            graph.Connect(playable, 0, mixer, i);
            mixer.SetInputWeight(i, 0f);
            clipPlayables[i] = playable;
        }

        var output = AnimationPlayableOutput.Create(graph, "RuntimePlayerAnimation", targetAnimator);
        output.SetSourcePlayable(mixer);

        graph.Play();
        initialized = true;
        ForceState(ResolveDesiredState(), true);
        AdvanceClipTimes(0f);
        ApplyDirectionalPresentation(ResolveDesiredState());
    }

    public void TriggerStumble(float duration = 0.85f)
    {
        stumbleUntil = Mathf.Max(stumbleUntil, Time.time + duration);

        if (initialized)
            ForceState(VisualState.Stumble, true);
    }

    void LateUpdate()
    {
        VisualState desiredState = ResolveDesiredState();
        if (initialized && graph.IsValid() && mixer.IsValid())
        {
            ForceState(desiredState, false);
            AdvanceClipTimes(Time.deltaTime);
            ApplyDirectionalPresentation(desiredState);
            return;
        }

        ApplyFallbackMotion(desiredState);
    }

    void OnDisable()
    {
        RestoreFallbackPose();
    }

    void OnDestroy()
    {
        DestroyGraph();
    }

    VisualState ResolveDesiredState()
    {
        if (Time.time < stumbleUntil)
            return VisualState.Stumble;

        bool isGrounded = observedMovement == null || observedMovement.IsGrounded;
        if (!isGrounded)
            return VisualState.Jump;

        bool isSwitchingLane = observedMovement != null && observedMovement.CurrentLane != observedMovement.TargetLane;
        if (isSwitchingLane)
            return VisualState.Strafe;

        return VisualState.Run;
    }

    void ForceState(VisualState desiredState, bool restartClip)
    {
        int desiredIndex = GetClipIndex(desiredState);
        if (desiredIndex < 0)
            return;

        bool stateChanged = currentState != desiredState;
        if (stateChanged)
        {
            currentState = desiredState;
            restartClip = true;
        }

        float blendStep = restartClip ? 1f : Mathf.Max(0.01f, Time.deltaTime * CrossFadeSpeed);

        for (int i = 0; i < clipWeights.Length; i++)
        {
            if (!HasValidClip(i))
                continue;

            bool isActive = i == desiredIndex;
            if (restartClip && isActive)
                RestartClip(i);

            float targetWeight = isActive ? 1f : 0f;
            clipWeights[i] = restartClip
                ? targetWeight
                : Mathf.MoveTowards(clipWeights[i], targetWeight, blendStep);

            mixer.SetInputWeight(i, clipWeights[i]);
        }
    }

    void RestartClip(int clipIndex)
    {
        if (!HasValidClip(clipIndex))
            return;

        if (clipTimes != null && clipIndex >= 0 && clipIndex < clipTimes.Length)
            clipTimes[clipIndex] = 0f;

        clipPlayables[clipIndex].SetTime(0d);
        clipPlayables[clipIndex].SetDone(false);
    }

    void AdvanceClipTimes(float deltaTime)
    {
        if (clipPlayables == null || clipWeights == null || clipTimes == null)
            return;

        int activeIndex = GetClipIndex(currentState);
        for (int i = 0; i < clipPlayables.Length; i++)
        {
            if (!HasValidClip(i))
                continue;
            if (i != activeIndex && clipWeights[i] <= 0.0001f)
                continue;

            float playbackSpeed = GetPlaybackSpeed(i);
            float effectiveLength = GetEffectiveLoopLength(i);
            float clipLength = GetClipLength(i);

            if (i == activeIndex)
                clipTimes[i] += Mathf.Max(0f, deltaTime) * playbackSpeed;

            if (IsLoopingIndex(i))
            {
                if (effectiveLength > MinClipLength)
                    clipTimes[i] = Mathf.Repeat(clipTimes[i], Mathf.Max(MinClipLength, effectiveLength - LoopWrapPadding));
            }
            else
            {
                clipTimes[i] = Mathf.Clamp(clipTimes[i], 0f, Mathf.Max(0f, clipLength - MinClipLength));
            }

            clipPlayables[i].SetTime(clipTimes[i]);
            clipPlayables[i].SetDone(false);
        }
    }

    int GetClipIndex(VisualState state)
    {
        int preferredIndex = state switch
        {
            VisualState.Run => 0,
            VisualState.Jump => 1,
            VisualState.Strafe => 2,
            VisualState.Stumble => 3,
            _ => 0
        };

        if (HasValidClip(preferredIndex))
            return preferredIndex;
        if (HasValidClip(0))
            return 0;

        for (int i = 0; i < 4; i++)
        {
            if (HasValidClip(i))
                return i;
        }

        return -1;
    }

    bool HasAnyPlayableClip()
    {
        if (clips == null)
            return false;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                return true;
        }

        return false;
    }

    bool HasValidClip(int index)
    {
        return clipPlayables != null &&
               index >= 0 &&
               index < clipPlayables.Length &&
               clipPlayables[index].IsValid();
    }

    float GetClipLength(int clipIndex)
    {
        if (clips == null || clipIndex < 0 || clipIndex >= clips.Length || clips[clipIndex] == null)
            return MinClipLength;

        return Mathf.Max(MinClipLength, clips[clipIndex].length);
    }

    float GetEffectiveLoopLength(int clipIndex)
    {
        float clipLength = GetClipLength(clipIndex);
        if (!IsLoopingIndex(clipIndex))
            return clipLength;

        if (clipIndex == 0)
            return Mathf.Min(clipLength, RunLoopWindow);

        return clipLength;
    }

    bool IsLoopingIndex(int clipIndex)
    {
        return clipIndex == 0 || clipIndex == 2;
    }

    float GetPlaybackSpeed(int clipIndex)
    {
        return clipIndex switch
        {
            0 => 1.18f,
            1 => GetJumpPlaybackSpeed(),
            2 => 1.1f,
            3 => 1f,
            _ => 1f
        };
    }

    float GetJumpPlaybackSpeed()
    {
        float clipLength = GetClipLength(1);
        if (clipLength <= MinClipLength)
            return 1.12f;

        if (observedMovement == null)
            return 1.12f;

        float gravity = Mathf.Max(0.01f, observedMovement.gravity);
        float jumpForce = Mathf.Max(0.01f, observedMovement.jumpForce);
        float expectedAirTime = Mathf.Max(MinimumJumpDuration, (2f * jumpForce) / gravity);

        // Finish the full jump clip slightly before landing so the visual reads cleanly.
        float desiredVisualDuration = Mathf.Max(MinimumJumpDuration, expectedAirTime * 0.92f);
        float playbackSpeed = clipLength / desiredVisualDuration;
        return Mathf.Clamp(playbackSpeed, 1f, 2.4f);
    }

    void ApplyDirectionalPresentation(VisualState state)
    {
        if (fallbackRoot == null)
            return;

        Vector3 scale = fallbackBaseLocalScale;
        float baseX = Mathf.Abs(scale.x);
        if (baseX <= 0.0001f)
            baseX = 1f;

        if (state == VisualState.Strafe && observedMovement != null && observedMovement.TargetLane < observedMovement.CurrentLane)
            scale.x = -baseX;
        else
            scale.x = baseX;

        fallbackRoot.localScale = scale;
    }

    void ApplyFallbackMotion(VisualState state)
    {
        if (fallbackRoot == null)
            return;

        fallbackCycle += Time.deltaTime * FallbackBobSpeed;
        Vector3 targetPosition = fallbackBaseLocalPosition;
        Quaternion targetRotation = fallbackBaseLocalRotation;

        if (state == VisualState.Run)
        {
            targetPosition.y += Mathf.Sin(fallbackCycle) * FallbackBobAmount;
        }
        else if (state == VisualState.Strafe)
        {
            float direction = 0f;
            if (observedMovement != null)
                direction = Mathf.Sign(observedMovement.TargetLane - observedMovement.CurrentLane);

            targetPosition.y += Mathf.Sin(fallbackCycle) * (FallbackBobAmount * 0.7f);
            targetRotation *= Quaternion.Euler(0f, 0f, -direction * FallbackStrafeLean);
        }
        else if (state == VisualState.Jump)
        {
            targetPosition.y += FallbackJumpLift;
        }
        else if (state == VisualState.Stumble)
        {
            targetRotation *= Quaternion.Euler(18f, 0f, 0f);
        }

        fallbackRoot.localPosition = Vector3.Lerp(fallbackRoot.localPosition, targetPosition, Time.deltaTime * 12f);
        fallbackRoot.localRotation = Quaternion.Slerp(fallbackRoot.localRotation, targetRotation, Time.deltaTime * 12f);
    }

    void RestoreFallbackPose()
    {
        if (fallbackRoot == null)
            return;

        fallbackRoot.localPosition = fallbackBaseLocalPosition;
        fallbackRoot.localRotation = fallbackBaseLocalRotation;
        fallbackRoot.localScale = fallbackBaseLocalScale;
    }

    void DestroyGraph()
    {
        if (graph.IsValid())
            graph.Destroy();

        clipPlayables = null;
        clipWeights = null;
        clipTimes = null;
        initialized = false;
    }
}
