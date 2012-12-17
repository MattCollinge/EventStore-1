// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;

namespace EventStore.Projections.Core.Services.Processing
{
    public abstract class WorkItem : StagedTask
    {
        protected readonly CoreProjection Projection;

        private readonly int _lastStage;
        private Action<int, object> _complete;
        private int _onStage;
        private CheckpointTag _checkpointTag;
        private object _lastStageCorrelationId;

        protected WorkItem(CoreProjection projection, object initialCorrelationId)
            : base(initialCorrelationId)
        {
            Projection = projection;
            _lastStage = 3;
        }

        public override void Process(int onStage, Action<int, object> readyForStage)
        {
            if (_checkpointTag == null)
                throw new InvalidOperationException("CheckpointTag has not been initialized");
            _complete = readyForStage;
            _onStage = onStage;
            Projection.EnsureTickPending();
            switch (onStage)
            {
                case 0:
                    GetStatePartition();
                    break;
                case 1:
                    Load(_checkpointTag);
                    break;
                case 2:
                    ProcessEvent();
                    break;
                case 3:
                    WriteOutput();
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        protected virtual void WriteOutput()
        {
            NextStage();
        }

        protected virtual void GetStatePartition()
        {
            NextStage();
        }

        protected virtual void Load(CheckpointTag checkpointTag)
        {
            NextStage();
        }

        protected virtual void ProcessEvent()
        {
            NextStage();
        }

        protected void NextStage(object newCorrelationId = null)
        {
            _lastStageCorrelationId = newCorrelationId ?? _lastStageCorrelationId ?? InitialCorrelationId;
            _complete(_onStage == _lastStage ? -1 : _onStage + 1, _lastStageCorrelationId);
        }

        protected void Complete()
        {
            _complete(-1, InitialCorrelationId);
        }

        public void SetCheckpointTag(CheckpointTag checkpointTag)
        {
            _checkpointTag = checkpointTag;
        }
    }
}
