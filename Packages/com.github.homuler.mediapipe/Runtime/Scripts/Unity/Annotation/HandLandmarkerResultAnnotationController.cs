// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using Mediapipe.Tasks.Vision.HandLandmarker;
using System;
using System.Net;
using UnityEngine;

namespace Mediapipe.Unity
{
    public static class HandResultBus
    {
        // 외부 프로젝트에서 구독 가능
        public static event Action<HandLandmarkerResult> OnHandResult;

        internal static void Publish(HandLandmarkerResult r) => OnHandResult?.Invoke(r);
    }

    public class HandLandmarkerResultAnnotationController : AnnotationController<MultiHandLandmarkListAnnotation>
    {
        [SerializeField] private bool _visualizeZ = false;

        private readonly object _currentTargetLock = new object();
        private HandLandmarkerResult _currentTarget;

        public void DrawNow(HandLandmarkerResult target)
        {
            target.CloneTo(ref _currentTarget);
            SyncNow();
        }

        public void DrawLater(HandLandmarkerResult target) => UpdateCurrentTarget(target);

        protected void UpdateCurrentTarget(HandLandmarkerResult newTarget)
        {
            lock (_currentTargetLock)
            {
                newTarget.CloneTo(ref _currentTarget);
                isStale = true;
            }
        }

        protected override void SyncNow()
        {
            lock (_currentTargetLock)
            {
                isStale = false;
                annotation.SetHandedness(_currentTarget.handedness);
                annotation.Draw(_currentTarget.handLandmarks, _visualizeZ);

                HandResultBus.Publish(_currentTarget);
            }
        }
    }
}
