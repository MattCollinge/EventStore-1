﻿// Copyright (c) 2012, Event Store LLP
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
using System.Diagnostics;
using System.Linq;
using System.Text;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Exceptions;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.Services.Storage
{
    public class StorageReaderWorker: IHandle<ClientMessage.ReadEvent>,
                                      IHandle<ClientMessage.ReadStreamEventsBackward>,
                                      IHandle<ClientMessage.ReadStreamEventsForward>,
                                      IHandle<ClientMessage.ReadAllEventsForward>,
                                      IHandle<ClientMessage.ReadAllEventsBackward>,
                                      IHandle<ClientMessage.ListStreams>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageReaderWorker>();

        private readonly IReadIndex _readIndex;
        private readonly ICheckpoint _writerCheckpoint;

        public StorageReaderWorker(IReadIndex readIndex, ICheckpoint writerCheckpoint)
        {
            Ensure.NotNull(readIndex, "readIndex");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");

            _readIndex = readIndex;
            _writerCheckpoint = writerCheckpoint;
        }

        void IHandle<ClientMessage.ReadEvent>.Handle(ClientMessage.ReadEvent message)
        {
            var result = _readIndex.ReadEvent(message.EventStreamId, message.EventNumber);

            EventRecord record = result.Record;
            if (result.Result == SingleReadResult.Success && message.ResolveLinkTos)
            {
                Debug.Assert(result.Record != null);
                record = ResolveLinkToEvent(record) ?? record;
            }

            //TODO: consider returning redirected stream and ChunkNumber number
            message.Envelope.ReplyWith(new ClientMessage.ReadEventCompleted(message.CorrelationId,
                                                                            message.EventStreamId,
                                                                            message.EventNumber,
                                                                            result.Result,
                                                                            record));
        }

        void IHandle<ClientMessage.ReadStreamEventsForward>.Handle(ClientMessage.ReadStreamEventsForward message)
        {
            var lastCommitPosition = _readIndex.LastCommitPosition;

            var result = _readIndex.ReadStreamEventsForward(message.EventStreamId, message.FromEventNumber, message.MaxCount);
            if (result.Result == RangeReadResult.Success && result.Records.Length > 1)
            {
                var records = result.Records;
                for (var index = 1; index < records.Length; index++)
                {
                    if (records[index].EventNumber != records[index - 1].EventNumber + 1)
                    {
                        throw new Exception(string.Format(
                                                          "Invalid order of events has been detected in read index for the event stream '{0}'. "
                                                          + "The event {1} at position {2} goes after the event {3} at position {4}",
                                                          message.EventStreamId,
                                                          records[index].EventNumber,
                                                          records[index].LogPosition,
                                                          records[index - 1].EventNumber,
                                                          records[index - 1].LogPosition));
                    }
                }
            }

            var resolvedPairs = ResolveLinkToEvents(result.Records, message.ResolveLinks);
            message.Envelope.ReplyWith(
                                       new ClientMessage.ReadStreamEventsForwardCompleted(message.CorrelationId,
                                                                                          message.EventStreamId,
                                                                                          resolvedPairs,
                                                                                          result.Result,
                                                                                          result.NextEventNumber,
                                                                                          result.LastEventNumber,
                                                                                          result.IsEndOfStream,
                                                                                          result.IsEndOfStream ? lastCommitPosition : (long?)null));
        }

        void IHandle<ClientMessage.ReadStreamEventsBackward>.Handle(ClientMessage.ReadStreamEventsBackward message)
        {
            var lastCommitPosition = _readIndex.LastCommitPosition;

            var result = _readIndex.ReadStreamEventsBackward(message.EventStreamId, message.FromEventNumber, message.MaxCount);
            if (result.Result == RangeReadResult.Success && result.Records.Length > 1)
            {
                var records = result.Records;
                for (var index = 1; index < records.Length; index++)
                {
                    if (records[index].EventNumber != records[index - 1].EventNumber - 1)
                    {
                        throw new Exception(string.Format(
                                                          "Invalid order of events has been detected in read index for the event stream '{0}'. "
                                                          + "The event {1} at position {2} goes after the event {3} at position {4}",
                                                          message.EventStreamId,
                                                          records[index].EventNumber,
                                                          records[index].LogPosition,
                                                          records[index - 1].EventNumber,
                                                          records[index - 1].LogPosition));
                    }
                }
            }
            var resolvedPairs = ResolveLinkToEvents(result.Records, message.ResolveLinks);
            message.Envelope.ReplyWith(
                                       new ClientMessage.ReadStreamEventsBackwardCompleted(message.CorrelationId,
                                                                                           message.EventStreamId,
                                                                                           resolvedPairs,
                                                                                           result.Result,
                                                                                           result.NextEventNumber,
                                                                                           result.LastEventNumber,
                                                                                           result.IsEndOfStream,
                                                                                           result.IsEndOfStream ? lastCommitPosition : (long?)null));
        }

        private EventLinkPair[] ResolveLinkToEvents(EventRecord[] records, bool resolveLinks)
        {
            var resolved = new EventLinkPair[records.Length];
            if (resolveLinks)
            {
                for (int i = 0; i < records.Length; i++)
                {
                    EventRecord eventRecord = records[i];
                    EventRecord resolvedRecord = ResolveLinkToEvent(eventRecord);
                    resolved[i] = resolvedRecord != null
                                          ? new EventLinkPair(resolvedRecord, eventRecord)
                                          : new EventLinkPair(eventRecord, null);
                }
            }
            else
            {
                for (int i = 0; i < records.Length; ++i)
                {
                    resolved[i] = new EventLinkPair(records[i], null);
                }
            }
            return resolved;
        }

        private EventRecord ResolveLinkToEvent(EventRecord eventRecord)
        {
            EventRecord record = null;
            if (eventRecord.EventType == SystemEventTypes.LinkTo)
            {
                bool faulted = false;
                int eventNumber = -1;
                string streamId = null;
                try
                {
                    string[] parts = Encoding.UTF8.GetString(eventRecord.Data).Split('@');
                    eventNumber = int.Parse(parts[0]);
                    streamId = parts[1];
                }
                catch (Exception exc)
                {
                    faulted = true;
                    Log.ErrorException(exc, "Error while resolving link for event record: {0}", eventRecord.ToString());
                }
                if (faulted)
                    return null;

                record = _readIndex.ReadEvent(streamId, eventNumber).Record; // we don't care if unsuccessful
            }
            return record;
        }

        void IHandle<ClientMessage.ReadAllEventsForward>.Handle(ClientMessage.ReadAllEventsForward message)
        {
            var pos = new TFPos(message.CommitPosition, message.PreparePosition);
            if (pos.CommitPosition < 0 || pos.PreparePosition < 0)
            {
                var r = new ReadAllResult(new ResolvedEventRecord[0],
                                          message.MaxCount,
                                          pos,
                                          TFPos.Invalid,
                                          TFPos.Invalid,
                                          _writerCheckpoint.Read());
                message.Envelope.ReplyWith(new ClientMessage.ReadAllEventsForwardCompleted(message.CorrelationId, r));
                return;
            }
            var res = _readIndex.ReadAllEventsForward(pos, message.MaxCount);
            var result = ResolveReadAllResult(res, message.ResolveLinks);
            message.Envelope.ReplyWith(new ClientMessage.ReadAllEventsForwardCompleted(message.CorrelationId, result));
        }

        void IHandle<ClientMessage.ReadAllEventsBackward>.Handle(ClientMessage.ReadAllEventsBackward message)
        {
            var pos = new TFPos(message.CommitPosition, message.PreparePosition);
            if (pos == new TFPos(-1, -1))
            {
                var checkpoint = _writerCheckpoint.Read();
                pos = new TFPos(checkpoint, checkpoint);
            }
            if (pos.CommitPosition < 0 || pos.PreparePosition < 0)
            {
                var r = new ReadAllResult(new ResolvedEventRecord[0],
                                          message.MaxCount,
                                          pos,
                                          TFPos.Invalid,
                                          TFPos.Invalid,
                                          _writerCheckpoint.Read());
                message.Envelope.ReplyWith(new ClientMessage.ReadAllEventsForwardCompleted(message.CorrelationId, r));
                return;
            }
            var res = _readIndex.ReadAllEventsBackward(pos, message.MaxCount);
            var result = ResolveReadAllResult(res, message.ResolveLinks);
            message.Envelope.ReplyWith(new ClientMessage.ReadAllEventsBackwardCompleted(message.CorrelationId, result));
        }

        private ReadAllResult ResolveReadAllResult(IndexReadAllResult res, bool resolveLinks)
        {
            var resolved = new ResolvedEventRecord[res.Records.Count];
            if (resolveLinks)
            {
                for (int i = 0; i < resolved.Length; ++i)
                {
                    var record = res.Records[i];
                    var resolvedRecord = ResolveLinkToEvent(record.Event);
                    resolved[i] = new ResolvedEventRecord(resolvedRecord == null ? record.Event : resolvedRecord,
                                                          resolvedRecord == null ? null : record.Event,
                                                          record.CommitPosition);
                }
            }
            else
            {
                for (int i = 0; i < resolved.Length; ++i)
                {
                    resolved[i] = new ResolvedEventRecord(res.Records[i].Event, null, res.Records[i].CommitPosition);
                }
            }
            return new ReadAllResult(resolved, res.MaxCount, res.CurrentPos, res.NextPos, res.PrevPos, res.TfEofPosition);
        }

        void IHandle<ClientMessage.ListStreams>.Handle(ClientMessage.ListStreams message)
        {
            try
            {
                // from 1 to skip $stream-created event in $streams stream
                var result = _readIndex.ReadStreamEventsForward(SystemStreams.StreamsStream, 1, int.MaxValue);
                if (result.Result != RangeReadResult.Success)
                    throw new SystemStreamNotFoundException(
                            string.Format("Couldn't find system stream {0}, which should've been created with projection 'Index By Streams'",
                                          SystemStreams.StreamsStream));

                var streamIds = result.Records
                                      .Select(e =>
                                      {
                                          var dataStr = Encoding.UTF8.GetString(e.Data);
                                          var parts = dataStr.Split('@');
                                          if (parts.Length < 2)
                                          {
                                              throw new FormatException(string.Format("{0} stream event data is in bad format: {1}. Expected: eventNumber@streamid",
                                                                                      SystemStreams.StreamsStream,
                                                                                      dataStr));
                                          }
                                          var streamid = parts[1];
                                          return streamid;
                                      })
                                      .ToArray();

                message.Envelope.ReplyWith(new ClientMessage.ListStreamsCompleted(true, streamIds));
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Error while reading stream ids.");
                message.Envelope.ReplyWith(new ClientMessage.ListStreamsCompleted(false, null));
            }
        }
    }
}