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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class DefaultCheckpointManager : CoreProjectionCheckpointManager
    {
        private readonly string _projectionCheckpointStreamId;
        private int _inCheckpointWriteAttempt;
        private int _lastWrittenCheckpointEventNumber;
        private int _nextStateIndexToRequest;
        private Event _checkpointEventToBePublished;
        private CheckpointTag _requestedCheckpointPosition;
        private Guid _writeRequestId;
        private Guid _readRequestId;
        private readonly CheckpointTag _zeroTag;
        private int _readRequestsInProgress;
        private readonly HashSet<Guid> _loadStateRequests = new HashSet<Guid>();

        protected readonly RequestResponseDispatcher<ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> _readDispatcher;
        protected readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> _writeDispatcher;

        public DefaultCheckpointManager(IPublisher publisher, Guid projectionCorrelationId,
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> readDispatcher,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            ProjectionConfig projectionConfig, string name, PositionTagger positionTagger,
            ProjectionNamesBuilder namingBuilder, IResultEmitter resultEmitter, bool useCheckpoints,
            bool emitPartitionCheckpoints = false)
            : base(
                publisher, projectionCorrelationId, projectionConfig,
                name, positionTagger, namingBuilder, resultEmitter, useCheckpoints, emitPartitionCheckpoints)
        {
            if (readDispatcher == null) throw new ArgumentNullException("readDispatcher");
            if (writeDispatcher == null) throw new ArgumentNullException("writeDispatcher");
            _readDispatcher = readDispatcher;
            _writeDispatcher = writeDispatcher;
            _projectionCheckpointStreamId = namingBuilder.MakeCheckpointStreamName();
            _zeroTag = positionTagger.MakeZeroCheckpointTag();
        }

        protected override void BeginWriteCheckpoint(
            CheckpointTag requestedCheckpointPosition, string requestedCheckpointState)
        {
            _requestedCheckpointPosition = requestedCheckpointPosition;
            _inCheckpointWriteAttempt = 1;
            //TODO: pass correct expected version
            _checkpointEventToBePublished = new Event(
                Guid.NewGuid(), "ProjectionCheckpoint", true,
                requestedCheckpointState == null ? null : Encoding.UTF8.GetBytes(requestedCheckpointState),
                requestedCheckpointPosition.ToJsonBytes());
            PublishWriteCheckpointEvent();
        }

        public override void RecordEventOrder(ProjectionSubscriptionMessage.CommittedEventReceived message, Action committed)
        {
            committed();
        }

        private void WriteCheckpointEventCompleted(ClientMessage.WriteEventsCompleted message, string eventStreamId)
        {
            EnsureStarted();
            if (_inCheckpointWriteAttempt == 0)
                throw new InvalidOperationException();
            if (message.Result == OperationResult.Success)
            {
                if (_logger != null)
                    _logger.Trace(
                        "Checkpoint has be written for projection {0} at sequence number {1} (current)", _name,
                        message.FirstEventNumber);
                _lastWrittenCheckpointEventNumber = message.FirstEventNumber
                                                    + (_lastWrittenCheckpointEventNumber == ExpectedVersion.NoStream
                                                       // account for StreamCreated
                                                           ? 1
                                                           : 0);

                _inCheckpointWriteAttempt = 0;
                CheckpointWritten();
            }
            else
            {
                if (_logger != null)
                {
                    _logger.Info("Failed to write projection checkpoint to stream {0}. Error: {1}",
                                 eventStreamId,
                                 Enum.GetName(typeof (OperationResult), message.Result));
                }
                switch (message.Result)
                {
                    case OperationResult.WrongExpectedVersion:
                        RequestRestart("Checkpoint stream has been written to from the outside");
                        break;
                    case OperationResult.PrepareTimeout:
                    case OperationResult.ForwardTimeout:
                    case OperationResult.CommitTimeout:
                        if (_logger != null) _logger.Info("Retrying write checkpoint to {0}", eventStreamId);
                        _inCheckpointWriteAttempt++;
                        PublishWriteCheckpointEvent();
                        break;
                    default:
                        throw new NotSupportedException("Unsupported error code received");
                }
            }
        }

        private void PublishWriteCheckpointEvent()
        {
            if (_logger != null)
                _logger.Trace(
                    "Writing checkpoint for {0} at {1} with expected version number {2}", _name,
                    _requestedCheckpointPosition, _lastWrittenCheckpointEventNumber);
            _writeRequestId = _writeDispatcher.Publish(
                new ClientMessage.WriteEvents(
                    Guid.NewGuid(), _writeDispatcher.Envelope, true, _projectionCheckpointStreamId,
                    _lastWrittenCheckpointEventNumber, _checkpointEventToBePublished), 
                    msg => WriteCheckpointEventCompleted(msg, _projectionCheckpointStreamId));
        }

        public override void Initialize()
        {
            base.Initialize();
            _writeDispatcher.Cancel(_writeRequestId);
            _readDispatcher.Cancel(_readRequestId);
            foreach (var requestId in _loadStateRequests)
                _readDispatcher.Cancel(requestId);
            _loadStateRequests.Clear();
            _inCheckpointWriteAttempt = 0;
            _lastWrittenCheckpointEventNumber = 0;
            _nextStateIndexToRequest = 0;
            _checkpointEventToBePublished = null;
            _requestedCheckpointPosition = null;
            _readRequestsInProgress = 0;
        }

        public override void GetStatistics(ProjectionStatistics info)
        {
            base.GetStatistics(info);
            info.ReadsInProgress += _readRequestsInProgress;
            info.WritesInProgress = ((_inCheckpointWriteAttempt != 0) ? 1 : 0) + info.WritesInProgress;
            info.CheckpointStatus = _inCheckpointWriteAttempt > 0
                                        ? "Writing (" + _inCheckpointWriteAttempt + ")"
                                        : info.CheckpointStatus;
        }

        protected override void RegisterNewPartition(string partition, CheckpointTag at)
        {
            EventsEmitted(
                new[]
                    {
                        new EmittedEvent(
                    _namingBuilder.GetPartitionCatalogStreamName(), Guid.NewGuid(), "$partition", partition,
                    at, null)
                    });
        }

        protected override void BeforeBeginLoadState()
        {
            _lastWrittenCheckpointEventNumber = ExpectedVersion.NoStream;
            _nextStateIndexToRequest = -1; // from the end
        }

        protected override void RequestLoadState()
        {
            const int recordsToRequest = 10;
            _readRequestId = _readDispatcher.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    Guid.NewGuid(), _readDispatcher.Envelope, _projectionCheckpointStreamId, _nextStateIndexToRequest,
                    recordsToRequest, resolveLinks: false, validationStreamVersion: null), OnLoadStateReadRequestCompleted);
        }

        private void OnLoadStateReadRequestCompleted(ClientMessage.ReadStreamEventsBackwardCompleted message)
        {
            string checkpointData = null;
            CheckpointTag checkpointTag = null;
            int checkpointEventNumber = -1;
            if (message.Events.Length > 0)
            {
                EventRecord checkpoint = message.Events.FirstOrDefault(v => v.Event.EventType == "ProjectionCheckpoint").Event;
                if (checkpoint != null)
                {
                    checkpointData = Encoding.UTF8.GetString(checkpoint.Data);
                    checkpointTag = checkpoint.Metadata.ParseJson<CheckpointTag>();
                    checkpointEventNumber = checkpoint.EventNumber;
                }
            }

            if (checkpointTag == null && message.NextEventNumber != -1)
            {
                _nextStateIndexToRequest = message.NextEventNumber;
                RequestLoadState();
                return;
            }
            _lastWrittenCheckpointEventNumber = checkpointEventNumber;
            CheckpointLoaded(checkpointTag, checkpointData);
        }

        protected override void BeginLoadPrerecordedEvents(CheckpointTag checkpointTag)
        {
            PrerecordedEventsLoaded(checkpointTag);
        }

        public override void BeginLoadPartitionStateAt(string statePartition,
                                              CheckpointTag requestedStateCheckpointTag, Action<PartitionState> loadCompleted)
        {
            var stateEventType = "Checkpoint";
            var partitionStateStreamName = _namingBuilder.MakePartitionCheckpointStreamName(statePartition);
            _readRequestsInProgress++;
            var requestId =
                _readDispatcher.Publish(
                    new ClientMessage.ReadStreamEventsBackward(
                        Guid.NewGuid(), _readDispatcher.Envelope, partitionStateStreamName, -1, 1, resolveLinks: false, validationStreamVersion: null),
                    m =>
                    OnLoadPartitionStateReadStreamEventsBackwardCompleted(
                        m, requestedStateCheckpointTag, loadCompleted,
                        partitionStateStreamName, stateEventType));
            if (requestId != Guid.Empty)
                _loadStateRequests.Add(requestId);
        }

        private void OnLoadPartitionStateReadStreamEventsBackwardCompleted(
            ClientMessage.ReadStreamEventsBackwardCompleted message, CheckpointTag requestedStateCheckpointTag,
            Action<PartitionState> loadCompleted, string partitionStreamName, string stateEventType)
        {
            //NOTE: the following remove may do nothing in tests as completed is raised before we return from publish. 
            _loadStateRequests.Remove(message.CorrelationId);

            _readRequestsInProgress--;
            if (message.Events.Length == 1)
            {
                EventRecord @event = message.Events[0].Event;
                if (@event.EventType == stateEventType)
                {
                    var loadedStateCheckpointTag = @event.Metadata.ParseJson<CheckpointTag>();
                    // always recovery mode? skip until state before current event
                    //TODO: skip event processing in case we know i has been already processed
                    if (loadedStateCheckpointTag < requestedStateCheckpointTag)
                    {
                        var state = PartitionState.Deserialize(Encoding.UTF8.GetString(@event.Data), loadedStateCheckpointTag);
                        loadCompleted(state);
                        return;
                    }
                }
            }
            if (message.NextEventNumber == -1)
            {
                var state = new PartitionState("", null, _zeroTag);
                loadCompleted(state);
                return;
            }
            _readRequestsInProgress++;
            var requestId =
                _readDispatcher.Publish(
                    new ClientMessage.ReadStreamEventsBackward(
                        Guid.NewGuid(), _readDispatcher.Envelope, partitionStreamName, message.NextEventNumber, 1,
                        resolveLinks: false, validationStreamVersion: null),
                    m =>
                    OnLoadPartitionStateReadStreamEventsBackwardCompleted(m, requestedStateCheckpointTag, loadCompleted, partitionStreamName, stateEventType));
            if (requestId != Guid.Empty)
                _loadStateRequests.Add(requestId);
        }

        protected override ProjectionCheckpoint CreateProjectionCheckpoint(CheckpointTag checkpointPosition)
        {
            return new ProjectionCheckpoint(_readDispatcher, _writeDispatcher, this, checkpointPosition,
                                            _zeroTag, _projectionConfig.MaxWriteBatchLength, _logger);
        }
    }
}
