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
using System.Text;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Messages
{
    public static class ProjectionManagementMessage
    {

        public class OperationFailed : Message
        {
            private readonly string _reason;

            public OperationFailed(string reason)
            {
                _reason = reason;
            }

            public string Reason
            {
                get { return _reason; }
            }
        }

        public class NotFound : OperationFailed
        {
            public NotFound()
                : base("Not Found")
            {
            }

        }

        public class Post : Message
        {
            private readonly IEnvelope _envelope;
            private readonly ProjectionMode _mode;
            private readonly string _name;
            private readonly string _handlerType;
            private readonly string _query;
            private readonly bool _enabled;
            private readonly bool _emitEnabled;

            public Post(
                IEnvelope envelope, ProjectionMode mode, string name, string handlerType, string query, bool enabled,
                bool emitEnabled)
            {
                _envelope = envelope;
                _name = name;
                _handlerType = handlerType;
                _mode = mode;
                _query = query;
                _enabled = enabled;
                _emitEnabled = emitEnabled;
            }

            // shortcut for posting ad-hoc JS queries
            public Post(IEnvelope envelope, string query, bool enabled)
            {
                _envelope = envelope;
                _name = Guid.NewGuid().ToString("D");
                _handlerType = "JS";
                _mode = ProjectionMode.OneTime;
                _query = query;
                _enabled = enabled;
            }

            public ProjectionMode Mode
            {
                get { return _mode; }
            }

            public string Query
            {
                get { return _query; }
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public string Name
            {
                get { return _name; }
            }

            public string HandlerType
            {
                get { return _handlerType; }
            }

            public bool Enabled
            {
                get { return _enabled; }
            }

            public bool EmitEnabled
            {
                get { return _emitEnabled; }
            }
        }

        public class UpdateQuery : Message
        {
            private readonly IEnvelope _envelope;
            private readonly string _name;
            private readonly string _handlerType;
            private readonly string _query;
            private readonly bool? _emitEnabled;

            public UpdateQuery(IEnvelope envelope, string name, string handlerType, string query, bool? emitEnabled)
            {
                _envelope = envelope;
                _name = name;
                _handlerType = handlerType;
                _query = query;
                _emitEnabled = emitEnabled;
            }

            public string Query
            {
                get { return _query; }
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public string Name
            {
                get { return _name; }
            }

            public string HandlerType
            {
                get { return _handlerType; }
            }

            public bool? EmitEnabled
            {
                get { return _emitEnabled; }
            }
        }

        public class GetQuery : Message
        {
            private readonly IEnvelope _envelope;
            private readonly string _name;

            public GetQuery(IEnvelope envelope, string name)
            {
                _envelope = envelope;
                _name = name;
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public string Name
            {
                get { return _name; }
            }
        }

        public class Delete : Message
        {
            private readonly IEnvelope _envelope;
            private readonly string _name;
            private readonly bool _deleteCheckpointStream;
            private readonly bool _deleteStateStream;

            public Delete(IEnvelope envelope, string name, bool deleteCheckpointStream, bool deleteStateStream)
            {
                _envelope = envelope;
                _name = name;
                _deleteCheckpointStream = deleteCheckpointStream;
                _deleteStateStream = deleteStateStream;
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public string Name
            {
                get { return _name; }
            }

            public bool DeleteCheckpointStream
            {
                get { return _deleteCheckpointStream; }
            }

            public bool DeleteStateStream
            {
                get { return _deleteStateStream; }
            }
        }

        public class Updated : Message
        {
            private readonly string _name;

            public Updated(string name)
            {
                _name = name;
            }

            public string Name
            {
                get { return _name; }
            }
        }

        public class GetStatistics : Message
        {
            private readonly IEnvelope _envelope;
            private readonly ProjectionMode? _mode;
            private readonly string _name;
            private readonly bool _includeDeleted;

            public GetStatistics(IEnvelope envelope, ProjectionMode? mode, string name, bool includeDeleted)
            {
                _envelope = envelope;
                _mode = mode;
                _name = name;
                _includeDeleted = includeDeleted;
            }

            public ProjectionMode? Mode
            {
                get { return _mode; }
            }

            public string Name
            {
                get { return _name; }
            }

            public bool IncludeDeleted
            {
                get { return _includeDeleted; }
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }
        }

        public class GetState : Message
        {
            private readonly IEnvelope _envelope;
            private readonly string _name;
            private readonly string _partition;

            public GetState(IEnvelope envelope, string name, string partition)
            {
                if (envelope == null) throw new ArgumentNullException("envelope");
                if (name == null) throw new ArgumentNullException("name");
                if (partition == null) throw new ArgumentNullException("partition");
                _envelope = envelope;
                _name = name;
                _partition = partition;
            }

            public string Name
            {
                get { return _name; }
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public string Partition
            {
                get { return _partition; }
            }
        }

        public class GetAllStates : Message
        {
            private readonly IEnvelope _envelope;
            private readonly Guid _correlationId;
            private readonly string _name;

            public GetAllStates(IEnvelope envelope, Guid correlationid, string name)
            {
                if (envelope == null) throw new ArgumentNullException("envelope");
                if (name == null) throw new ArgumentNullException("name");
                _envelope = envelope;
                _correlationId = correlationid;
                _name = name;
            }

            public string Name
            {
                get { return _name; }
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }
        }

        public class GetDebugState : Message
        {
            private readonly IEnvelope _envelope;
            private readonly string _name;

            public GetDebugState(IEnvelope envelope, string name)
            {
                if (envelope == null) throw new ArgumentNullException("envelope");
                if (name == null) throw new ArgumentNullException("name");
                _envelope = envelope;
                _name = name;
            }

            public string Name
            {
                get { return _name; }
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

        }
        public class Statistics : Message
        {
            private readonly ProjectionStatistics[] _projections;

            public Statistics(ProjectionStatistics[] projections)
            {
                _projections = projections;
            }

            public ProjectionStatistics[] Projections
            {
                get { return _projections; }
            }

        }

        public class ProjectionState : Message
        {
            private readonly string _name;
            private readonly string _partition;
            private readonly string _state;
            private readonly Exception _exception;

            public ProjectionState(string name, string partition, string state, Exception exception = null)
            {
                _name = name;
                _partition = partition;
                _state = state;
                _exception = exception;
            }

            public string Name
            {
                get { return _name; }
            }

            public string State
            {
                get { return _state; }
            }

            public Exception Exception
            {
                get { return _exception; }
            }

            public string Partition
            {
                get { return _partition; }
            }
        }

        public class ProjectionAllStatesBegin : Message
        {
            private readonly Guid _correlationId;
            private readonly string _name;

            public ProjectionAllStatesBegin(Guid correlationId, string name)
            {
                _correlationId = correlationId;
                _name = name;
            }

            public string Name
            {
                get { return _name; }
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }
        }

        public class ProjectionAllStatesPart : Message
        {
            private readonly Guid _correlationId;
            private readonly string _name;
            private readonly string _partition;
            private readonly string _state;

            public ProjectionAllStatesPart(Guid correlationId, string name, string partition, string state)
            {
                _correlationId = correlationId;
                _name = name;
                _partition = partition;
                _state = state;
            }

            public string Name
            {
                get { return _name; }
            }

            public string State
            {
                get { return _state; }
            }

            public string Partition
            {
                get { return _partition; }
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }
        }

        public class ProjectionAllStatesEnd : Message
        {
            private readonly Guid _correlationId;
            private readonly string _name;
            private readonly Exception _exception;

            public ProjectionAllStatesEnd(Guid correlationId, string name, Exception exception = null)
            {
                _correlationId = correlationId;
                _name = name;
                _exception = exception;
            }

            public string Name
            {
                get { return _name; }
            }

            public Exception Exception
            {
                get { return _exception; }
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }
        }

        public class ProjectionDebugState : Message
        {
            private readonly string _name;
            private readonly CoreProjectionManagementMessage.DebugState.Event[] _events;

            public ProjectionDebugState(string name, CoreProjectionManagementMessage.DebugState.Event[] events)
            {
                _name = name;
                _events = events;
            }

            public string Name
            {
                get { return _name; }
            }

            public CoreProjectionManagementMessage.DebugState.Event[] Events
            {
                get { return _events; }
            }
        }

        public class ProjectionQuery : Message
        {
            private readonly string _name;
            private readonly string _query;
            private readonly bool _emitEnabled;

            public ProjectionQuery(string name, string query, bool emitEnabled)
            {
                _name = name;
                _query = query;
                _emitEnabled = emitEnabled;
            }

            public string Name
            {
                get { return _name; }
            }

            public string Query
            {
                get { return _query; }
            }

            public bool EmitEnabled
            {
                get { return _emitEnabled; }
            }
        }

        public class Disable : Message
        {
            private readonly IEnvelope _envelope;
            private readonly string _name;

            public Disable(IEnvelope envelope, string name)
            {
                _envelope = envelope;
                _name = name;
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public string Name
            {
                get { return _name; }
            }
        }

        public class Enable : Message
        {
            private readonly IEnvelope _envelope;
            private readonly string _name;

            public Enable(IEnvelope envelope, string name)
            {
                _envelope = envelope;
                _name = name;
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public string Name
            {
                get { return _name; }
            }
        }
    }
}