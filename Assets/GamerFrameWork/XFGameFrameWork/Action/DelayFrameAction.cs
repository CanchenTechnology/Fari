using System;
using UnityEngine;

namespace XFGameFrameWork.ActionXF
{
    public class DelayFrameAction : ActionBase
    {
        private int _frameCount;
        private readonly Action _onComplete;

        public DelayFrameAction(int frameCount, Action onComplete)
        {
            _frameCount = frameCount;
            _onComplete = onComplete;
        }

        public override void Start()
        {
            IsFinished = false;
        }

        public override void Update(float deltaTime)
        {
            if (IsFinished) return;

            if (boundTarget != null && boundTarget == null)
            {
                // 绑定对象被销毁，自动结束
                IsFinished = true;
                return;
            }

            _frameCount--;

            if (_frameCount <= 0)
            {
                _onComplete?.Invoke();
                IsFinished = true;
            }
        }
    }
}
