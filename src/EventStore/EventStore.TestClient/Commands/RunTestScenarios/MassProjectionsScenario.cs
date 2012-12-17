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
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class MassProjectionsScenario : ProjectionsKillScenario
    {
        public MassProjectionsScenario(Action<IPEndPoint, byte[]> directSendOverTcp, int maxConcurrentRequests, int threads, int streams, int eventsPerStream, int streamDeleteStep, string dbParentPath) 
            : base(directSendOverTcp, maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep, dbParentPath)
        {
        }

        protected override void RunInternal()
        {
            var success = true;

            var nodeProcessId = StartNode();
            EnableProjectionByCategory();

            var countProjections = new List<string>();
            var bankProjections = new List<string>();

            var writeTasks = new List<Task>();

            while (GetIterationCode() < Streams / 3)
            {
                writeTasks.Add(WriteData());

                SetNextIterationCode();

                countProjections.Add(CreateCountItem());
                bankProjections.Add(CreateSumCheckForBankAccount0());

                Log.Info("Created {0} and {1}", bankProjections[bankProjections.Count - 1], 
                                                countProjections[countProjections.Count - 1]);

            }

            KillNode(nodeProcessId);
            nodeProcessId = StartNode();

            writeTasks.Add(WriteData());

            var writeTask = Task.Factory.ContinueWhenAll(writeTasks.ToArray(), tsks => Log.Info("All Data written"));

            KillNode(nodeProcessId);
            nodeProcessId = StartNode();

            int count = 10;
            while (count > 0)
            {
                Log.Info("Stop and start projection, remaining iterations {0}, waiting for data to be written.", count);

                var t1 = StartOrStopProjection(countProjections, false);
                var t2 = StartOrStopProjection(bankProjections, false);

                Task.WaitAll(new[] { t1, t2 });
                Thread.Sleep(TimeSpan.FromSeconds(10));

                var t3 = StartOrStopProjection(countProjections, true);
                var t4 = StartOrStopProjection(bankProjections, true);

                Task.WaitAll(new[] { t3, t4 });

                if (writeTask.IsCompleted)
                    count -= 1;

                if (writeTask.IsFaulted)
                    throw new ApplicationException("Failed to write data", writeTask.Exception);

                success = CheckProjectionState(GetProjectionsManager(), 
                                                        bankProjections[bankProjections.Count - 1], 
                                                        "success", 
                                                        x => x == EventsPerStream.ToString());
                if (success)
                    break;

                var sleepTimeSeconds = 10 + Streams * EventsPerStream / 1000.0;
                Log.Info("Sleep 1 for {0} seconds, remianing count {1}", sleepTimeSeconds, count);
                Thread.Sleep(TimeSpan.FromSeconds(sleepTimeSeconds));
            }

            writeTask.Wait();

            count = 20;
            success = false;
            while (!success && count > 0)
            {
                Log.Info("Wait until projections are computed, remaining iterations {0}", count);
                KillNode(nodeProcessId);
                nodeProcessId = StartNode();

                success = CheckProjectionState(GetProjectionsManager(),
                                                        bankProjections[bankProjections.Count - 1],
                                                        "success",
                                                        x => x == EventsPerStream.ToString());

                if (success)
                    break;

                var sleepTimeSeconds = 10 + (Streams * EventsPerStream) / 500;
                Log.Info("Sleep 2 for {0} seconds, remaining count {1}", sleepTimeSeconds, count);
                Thread.Sleep(TimeSpan.FromSeconds(sleepTimeSeconds));

                count -= 1;
            }

            if (!success)
                throw new ApplicationException("Last bank projection failed");
        }

        private Task StartOrStopProjection(IEnumerable<string> projections, bool enable)
        {
            var tasks = new List<Task>();
            var manager = GetProjectionsManager();

            foreach (string projection in projections)
            {

                //var isRunning = store.Projections.GetStatus(projection) == "Enabled";

                tasks.Add(enable
                              ? manager.EnableAsync(projection)
                              : manager.DisableAsync(projection));

                while (tasks.Count(x => !x.IsCompleted) > 4)
                    Thread.Sleep(50);
            }

            var task = Task.Factory.ContinueWhenAll(tasks.ToArray(), ts => { Task.WaitAll(ts); Log.Info("Projections enable/disable finished."); });
            return task;
        }

        private int _iterationCode;
        protected override int GetIterationCode()
        {
            return _iterationCode;
        }

        private void SetNextIterationCode()
        {
            _iterationCode += 1;
        }
    }
}