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
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Bus.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus
{
    [TestFixture]
    public abstract class when_starting_queued_handler : QueuedHandlerTestWithNoopConsumer
    {
        protected when_starting_queued_handler(Func<IHandle<Message>, string, TimeSpan, IQueuedHandler> queuedHandlerFactory)
                : base(queuedHandlerFactory)
        {
        }

        public override void SetUp()
        {
            base.SetUp();
            Queue.Start();
        }

        [Test]
        public void gracefully_should_not_throw()
        {
            Assert.Throws<InvalidOperationException>(() => Queue.Start());
        }

        [Test]
        public void multiple_times_should_throw()
        {
            Assert.Throws<InvalidOperationException>(() => Queue.Start());
        }

        [Test]
        public void after_being_stopped_should_throw()
        {
            Queue.Stop();
            Assert.Throws<InvalidOperationException>(() => Queue.Start());
        }
    }

    [TestFixture]
    public class when_starting_queued_handler_mres_should : when_starting_queued_handler
    {
        public when_starting_queued_handler_mres_should()
            : base((consumer, name, timeout) => new QueuedHandlerMRES(consumer, name, false, null, timeout))
        {
        }
    }

    [TestFixture]
    public class when_starting_queued_handler_autoreset : when_starting_queued_handler
    {
        public when_starting_queued_handler_autoreset()
            : base((consumer, name, timeout) => new QueuedHandlerAutoReset(consumer, name, false, null, timeout))
        {
        }
    }

    [TestFixture]
    public class when_starting_queued_handler_sleep : when_starting_queued_handler
    {
        public when_starting_queued_handler_sleep()
            : base((consumer, name, timeout) => new QueuedHandlerSleep(consumer, name, false, null, timeout))
        {
        }
    }

    [TestFixture]
    public class when_starting_queued_handler_pulse : when_starting_queued_handler
    {
        public when_starting_queued_handler_pulse()
            : base((consumer, name, timeout) => new QueuedHandlerPulse(consumer, name, false, null, timeout))
        {
        }
    }

    [TestFixture, Ignore]
    public class when_starting_queued_handler_threadpool : when_starting_queued_handler
    {
        public when_starting_queued_handler_threadpool()
            : base((consumer, name, timeout) => new QueuedHandlerThreadPool(consumer, name, false, null, timeout))
        {
        }
    }
}