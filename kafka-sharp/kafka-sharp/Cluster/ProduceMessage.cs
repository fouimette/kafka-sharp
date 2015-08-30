﻿// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Concurrent;
using System.Threading;
using Kafka.Protocol;
using Kafka.Public;

namespace Kafka.Cluster
{
    /// <summary>
    /// Those objects are pooled to minimize stress on the GC.
    /// Use New/Release for managing lifecycle.
    /// </summary>
    class ProduceMessage
    {
        public string Topic;
        public Message Message;
        public DateTime ExpirationDate;
        public int RequiredPartition = Partitions.Any;
        public int Partition = Partitions.None;

        private ProduceMessage() { }

        public static long Allocated { get { return _allocated; } }
        public static long Released { get { return _released; } }

        private static long _allocated;
        private static long _released;

        public static ProduceMessage New(string topic, int partition, Message message, DateTime expirationDate)
        {
            ProduceMessage reserved;
            if (!_produceMessagePool.TryDequeue(out reserved))
            {
                reserved = new ProduceMessage();
            }
            reserved.Topic = topic;
            reserved.Partition = reserved.RequiredPartition = partition;
            reserved.Message = message;
            reserved.ExpirationDate = expirationDate;
            Interlocked.Increment(ref _allocated);
            return reserved;
        }

        public static void Release(ProduceMessage message)
        {
            _produceMessagePool.Enqueue(message);
            Interlocked.Increment(ref _released);
        }

        static readonly ConcurrentQueue<ProduceMessage> _produceMessagePool = new ConcurrentQueue<ProduceMessage>();
    }
}
