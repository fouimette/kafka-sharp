﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kafka.Batching;
using Kafka.Cluster;
using Kafka.Protocol;
using Kafka.Public;
using Kafka.Routing;
using Moq;
using NUnit.Framework;

namespace tests_kafka_sharp
{
    [TestFixture]
    internal class TestConsumer
    {
        private const string TOPIC = "poulpe";
        private const string TOPIC2 = "tako";
        private const long OFFSET = 42L;
        private const int PARTITION = 0;

        [Test]
        [TestCase(Offsets.Latest)]
        [TestCase(Offsets.Earliest)]
        public void TestStart_AllPartition_OffsetUnknown(long offset)
        {
            var node = new Mock<INode>();
            var cluster = new Mock<ICluster>();
            cluster.Setup(c => c.RequireAllPartitionsForTopic(TOPIC))
                .Returns(Task.FromResult(new[] {0, 1, 2}));
            cluster.Setup(c => c.RequireNewRoutingTable())
                .Returns(() =>
                    Task.FromResult(
                        new RoutingTable(new Dictionary<string, Partition[]>
                        {
                            {
                                TOPIC,
                                new[]
                                {
                                    new Partition {Id = 0, Leader = node.Object},
                                    new Partition {Id = 1, Leader = node.Object},
                                    new Partition {Id = 2, Leader = node.Object}
                                }
                            }
                        })));
            var configuration = new Configuration {TaskScheduler = new CurrentThreadTaskScheduler()};
            var consumer = new ConsumeRouter(cluster.Object, configuration, 1);
            consumer.StartConsume(TOPIC, Partitions.All, offset);
            cluster.Verify(c => c.RequireAllPartitionsForTopic(It.Is<string>(t => t == TOPIC)), Times.AtLeastOnce());
            cluster.Verify(c => c.RequireAllPartitionsForTopic(It.Is<string>(s => s != TOPIC)), Times.Never());
            cluster.Verify(c => c.RequireNewRoutingTable(), Times.AtLeastOnce());
            node.Verify(n => n.Offset(It.IsAny<OffsetMessage>()), Times.Exactly(3));
            node.Verify(n => n.Offset(It.Is<OffsetMessage>(om => om.Partition == 0)), Times.Once());
            node.Verify(n => n.Offset(It.Is<OffsetMessage>(om => om.Partition == 1)), Times.Once());
            node.Verify(n => n.Offset(It.Is<OffsetMessage>(om => om.Partition == 2)), Times.Once());
            node.Verify(n => n.Offset(It.Is<OffsetMessage>(om => om.Topic != TOPIC)), Times.Never());
            node.Verify(n => n.Offset(It.Is<OffsetMessage>(om => om.Time != offset)), Times.Never());
        }

        [Test]
        public void TestStart_AllPartitions_KnownOffset()
        {
            var node = new Mock<INode>();
            var cluster = new Mock<ICluster>();
            cluster.Setup(c => c.RequireAllPartitionsForTopic(TOPIC))
                .Returns(Task.FromResult(new[] {0, 1, 2}));
            cluster.Setup(c => c.RequireNewRoutingTable())
                .Returns(() =>
                    Task.FromResult(
                        new RoutingTable(new Dictionary<string, Partition[]>
                        {
                            {
                                TOPIC,
                                new[]
                                {
                                    new Partition {Id = 0, Leader = node.Object},
                                    new Partition {Id = 1, Leader = node.Object},
                                    new Partition {Id = 2, Leader = node.Object}
                                }
                            }
                        })));
            var configuration = new Configuration {TaskScheduler = new CurrentThreadTaskScheduler()};
            var consumer = new ConsumeRouter(cluster.Object, configuration, 1);
            consumer.StartConsume(TOPIC, Partitions.All, OFFSET);
            cluster.Verify(c => c.RequireAllPartitionsForTopic(It.Is<string>(t => t == TOPIC)), Times.AtLeastOnce());
            cluster.Verify(c => c.RequireAllPartitionsForTopic(It.Is<string>(s => s != TOPIC)), Times.Never());
            cluster.Verify(c => c.RequireNewRoutingTable(), Times.AtLeastOnce());
            node.Verify(n => n.Fetch(It.IsAny<FetchMessage>()), Times.Exactly(3));
            node.Verify(n => n.Fetch(It.Is<FetchMessage>(fm => fm.Partition == 0)), Times.Once());
            node.Verify(n => n.Fetch(It.Is<FetchMessage>(fm => fm.Partition == 1)), Times.Once());
            node.Verify(n => n.Fetch(It.Is<FetchMessage>(fm => fm.Partition == 2)), Times.Once());
            node.Verify(n => n.Fetch(It.Is<FetchMessage>(fm => fm.Topic != TOPIC)), Times.Never());
            node.Verify(n => n.Fetch(It.Is<FetchMessage>(fm => fm.Offset != OFFSET)), Times.Never());
            node.Verify(n => n.Fetch(It.Is<FetchMessage>(fm => fm.MaxBytes != configuration.FetchMessageMaxBytes)),
                Times.Never());
        }

        [Test]
        public void TestStart_AllPartitions_KnownOffset_GlobalBatching()
        {
            var node = new Mock<INode>();
            var cluster = new Mock<ICluster>();
            cluster.Setup(c => c.RequireAllPartitionsForTopic(TOPIC))
                .Returns(Task.FromResult(new[] { 0, 1, 2 }));
            cluster.Setup(c => c.RequireNewRoutingTable())
                .Returns(() =>
                    Task.FromResult(
                        new RoutingTable(new Dictionary<string, Partition[]>
                        {
                            {
                                TOPIC,
                                new[]
                                {
                                    new Partition {Id = 0, Leader = node.Object},
                                    new Partition {Id = 1, Leader = node.Object},
                                    new Partition {Id = 2, Leader = node.Object}
                                }
                            }
                        })));
            var configuration = new Configuration
            {
                TaskScheduler = new CurrentThreadTaskScheduler(),
                BatchStrategy = BatchStrategy.Global,
                ConsumeBatchSize = 3,
                ConsumeBufferingTime = TimeSpan.FromHours(28)
            };
            var consumer = new ConsumeRouter(cluster.Object, configuration, 1);
            consumer.StartConsume(TOPIC, Partitions.All, OFFSET);

            cluster.Verify(c => c.RequireAllPartitionsForTopic(It.Is<string>(t => t == TOPIC)), Times.AtLeastOnce());
            cluster.Verify(c => c.RequireAllPartitionsForTopic(It.Is<string>(s => s != TOPIC)), Times.Never());
            cluster.Verify(c => c.RequireNewRoutingTable(), Times.AtLeastOnce());
            node.Verify(n => n.Fetch(It.IsAny<FetchMessage>()), Times.Never());
            node.Verify(n => n.Post(It.IsAny<IBatchByTopic<FetchMessage>>()));
        }

        [Test]
        [TestCase(Offsets.Latest)]
        [TestCase(Offsets.Earliest)]
        public void TestStart_OnePartition_OffsetUnknown(long offset)
        {
            var node = new Mock<INode>();
            var cluster = new Mock<ICluster>();
            cluster.Setup(c => c.RequireNewRoutingTable())
                .Returns(() =>
                    Task.FromResult(
                        new RoutingTable(new Dictionary<string, Partition[]>
                        {
                            {
                                TOPIC,
                                new[]
                                {
                                    new Partition {Id = 0, Leader = node.Object},
                                    new Partition {Id = 1, Leader = node.Object},
                                    new Partition {Id = 2, Leader = node.Object}
                                }
                            }
                        })));
            var configuration = new Configuration {TaskScheduler = new CurrentThreadTaskScheduler()};
            var consumer = new ConsumeRouter(cluster.Object, configuration, 1);
            consumer.StartConsume(TOPIC, PARTITION, offset);
            cluster.Verify(c => c.RequireAllPartitionsForTopic(It.IsAny<string>()), Times.Never());
            cluster.Verify(c => c.RequireNewRoutingTable(), Times.AtLeastOnce());
            node.Verify(n => n.Offset(It.Is<OffsetMessage>(om => om.Partition == PARTITION)), Times.Once());
            node.Verify(n => n.Offset(It.Is<OffsetMessage>(om => om.Partition != PARTITION)), Times.Never());
            node.Verify(n => n.Offset(It.Is<OffsetMessage>(om => om.Topic != TOPIC)), Times.Never());
            node.Verify(n => n.Offset(It.Is<OffsetMessage>(om => om.Time != offset)), Times.Never());
        }

        [Test]
        public void TestStart_OnePartition_OffsetUnknown_GlobalBatching()
        {
            const long offset = Offsets.Latest;
            var node = new Mock<INode>();
            var cluster = new Mock<ICluster>();
            cluster.Setup(c => c.RequireNewRoutingTable())
                .Returns(() =>
                    Task.FromResult(
                        new RoutingTable(new Dictionary<string, Partition[]>
                        {
                            {
                                TOPIC,
                                new[]
                                {
                                    new Partition {Id = 0, Leader = node.Object},
                                    new Partition {Id = 1, Leader = node.Object},
                                    new Partition {Id = 2, Leader = node.Object}
                                }
                            }
                        })));
            cluster.Setup(c => c.RequireAllPartitionsForTopic(TOPIC)).Returns(Task.FromResult(new[] {0, 1, 2}));
            var configuration = new Configuration
            {
                TaskScheduler = new CurrentThreadTaskScheduler(),
                BatchStrategy = BatchStrategy.Global,
                ConsumeBatchSize = 3,
                ConsumeBufferingTime = TimeSpan.FromHours(28)
            };
            var consumer = new ConsumeRouter(cluster.Object, configuration, 1);
            consumer.StartConsume(TOPIC, Partitions.All, offset);

            cluster.Verify(c => c.RequireAllPartitionsForTopic(It.IsAny<string>()), Times.AtLeastOnce());
            cluster.Verify(c => c.RequireNewRoutingTable(), Times.AtLeastOnce());
            node.Verify(n => n.Offset(It.IsAny<OffsetMessage>()), Times.Never());
            node.Verify(n => n.Post(It.IsAny<IBatchByTopic<OffsetMessage>>()));
        }

        [Test]
        public void TestStart_OnePartition_KnownOffset()
        {
            var node = new Mock<INode>();
            var cluster = new Mock<ICluster>();
            cluster.Setup(c => c.RequireNewRoutingTable())
                .Returns(() =>
                    Task.FromResult(
                        new RoutingTable(new Dictionary<string, Partition[]>
                        {
                            {
                                TOPIC,
                                new[]
                                {
                                    new Partition {Id = 0, Leader = node.Object},
                                    new Partition {Id = 1, Leader = node.Object},
                                    new Partition {Id = 2, Leader = node.Object}
                                }
                            }
                        })));
            var configuration = new Configuration {TaskScheduler = new CurrentThreadTaskScheduler()};
            var consumer = new ConsumeRouter(cluster.Object, configuration, 1);
            consumer.StartConsume(TOPIC, PARTITION, OFFSET);
            cluster.Verify(c => c.RequireAllPartitionsForTopic(It.IsAny<string>()), Times.Never());
            cluster.Verify(c => c.RequireNewRoutingTable(), Times.AtLeastOnce());
            node.Verify(n => n.Fetch(It.Is<FetchMessage>(fm => fm.Partition == PARTITION)), Times.Once());
            node.Verify(n => n.Fetch(It.Is<FetchMessage>(fm => fm.Topic != TOPIC)), Times.Never());
            node.Verify(n => n.Fetch(It.Is<FetchMessage>(fm => fm.Offset != OFFSET)), Times.Never());
            node.Verify(n => n.Fetch(It.Is<FetchMessage>(fm => fm.MaxBytes != configuration.FetchMessageMaxBytes)),
                Times.Never());
        }

        [Test]
        [TestCase(OFFSET - 1)]
        [TestCase(Offsets.Now)]
        public void TestStopConsumeBeforeFetchLoop(long offset)
        {
            var node = new Mock<INode>();
            var cluster = new Mock<ICluster>();
            cluster.Setup(c => c.RequireNewRoutingTable())
                .Returns(() =>
                    Task.FromResult(
                        new RoutingTable(new Dictionary<string, Partition[]>
                        {
                            {
                                TOPIC,
                                new[]
                                {
                                    new Partition {Id = 0, Leader = node.Object},
                                    new Partition {Id = 1, Leader = node.Object},
                                    new Partition {Id = 2, Leader = node.Object}
                                }
                            }
                        })));
            var configuration = new Configuration { TaskScheduler = new CurrentThreadTaskScheduler() };
            var consumer = new ConsumeRouter(cluster.Object, configuration, 1);
            consumer.StartConsume(TOPIC, PARTITION, Offsets.Latest);
            consumer.StopConsume(TOPIC, PARTITION, offset);

            // Now simulate an offset response, this should not trigger any fetch request.
            consumer.Acknowledge(new CommonAcknowledgement<OffsetPartitionResponse>
            {
                Response = new CommonResponse<OffsetPartitionResponse>
                {
                    TopicsResponse = new[]
                    {
                        new TopicData<OffsetPartitionResponse>
                        {
                            TopicName = TOPIC,
                            PartitionsData =
                                new[]
                                {
                                    new OffsetPartitionResponse
                                    {
                                        ErrorCode = ErrorCode.NoError,
                                        Partition = PARTITION,
                                        Offsets = new[] {OFFSET}
                                    }
                                }
                        }
                    }
                },
                ReceivedDate = DateTime.UtcNow
            });

            // Check
            node.Verify(n => n.Fetch(It.IsAny<FetchMessage>()), Times.Never());
        }

        [Test]
        [TestCase(OFFSET + 1)]
        [TestCase(Offsets.Now)]
        public void TestStopConsumeAfterFetchLoop(long offset)
        {
            var node = new Mock<INode>();
            var cluster = new Mock<ICluster>();
            cluster.Setup(c => c.RequireNewRoutingTable())
                .Returns(() =>
                    Task.FromResult(
                        new RoutingTable(new Dictionary<string, Partition[]>
                        {
                            {
                                TOPIC,
                                new[]
                                {
                                    new Partition {Id = 0, Leader = node.Object},
                                    new Partition {Id = 1, Leader = node.Object},
                                    new Partition {Id = 2, Leader = node.Object}
                                }
                            }
                        })));
            var configuration = new Configuration { TaskScheduler = new CurrentThreadTaskScheduler() };
            var consumer = new ConsumeRouter(cluster.Object, configuration, 1);
            consumer.StartConsume(TOPIC, PARTITION, OFFSET);
            consumer.StopConsume(TOPIC, PARTITION, offset);

            // Now simulate a fetch response getting out of range, this should not trigger any new fetch request.
            consumer.Acknowledge(new CommonAcknowledgement<FetchPartitionResponse>
            {
                Response = new CommonResponse<FetchPartitionResponse>
                {
                    TopicsResponse = new[]
                    {
                        new TopicData<FetchPartitionResponse>
                        {
                            TopicName = TOPIC,
                            PartitionsData = new[]
                            {
                                new FetchPartitionResponse
                                {
                                    ErrorCode = ErrorCode.NoError,
                                    Partition = PARTITION,
                                    HighWatermarkOffset = 432515L,
                                    Messages = new List<ResponseMessage>
                                    {
                                        new ResponseMessage {Offset = OFFSET, Message = new Message()},
                                        new ResponseMessage {Offset = OFFSET + 1, Message = new Message()},
                                        new ResponseMessage {Offset = OFFSET + 2, Message = new Message()},
                                    }
                                }
                            }
                        }
                    }
                },
                ReceivedDate = DateTime.UtcNow
            });

            // Check
            node.Verify(n => n.Fetch(It.IsAny<FetchMessage>()), Times.Once());
        }

        [Test]
        public void TestStopConsumeAfterFetchLoopMultiple()
        {
            var node = new Mock<INode>();
            var cluster = new Mock<ICluster>();
            cluster.Setup(c => c.RequireNewRoutingTable())
                .Returns(() =>
                    Task.FromResult(
                        new RoutingTable(new Dictionary<string, Partition[]>
                        {
                            {
                                TOPIC,
                                new[]
                                {
                                    new Partition {Id = 0, Leader = node.Object},
                                    new Partition {Id = 1, Leader = node.Object},
                                    new Partition {Id = 2, Leader = node.Object}
                                }
                            }
                        })));
            var configuration = new Configuration { TaskScheduler = new CurrentThreadTaskScheduler() };
            var consumer = new ConsumeRouter(cluster.Object, configuration, 1);
            consumer.StartConsume(TOPIC, 0, OFFSET);
            consumer.StartConsume(TOPIC, 1, OFFSET);
            consumer.StopConsume(TOPIC, Partitions.All, Offsets.Now);

            // Now simulate a fetch response getting out of range, this should not trigger any new fetch request.
            consumer.Acknowledge(new CommonAcknowledgement<FetchPartitionResponse>
            {
                Response = new CommonResponse<FetchPartitionResponse>
                {
                    TopicsResponse = new[]
                    {
                        new TopicData<FetchPartitionResponse>
                        {
                            TopicName = TOPIC,
                            PartitionsData = new[]
                            {
                                new FetchPartitionResponse
                                {
                                    ErrorCode = ErrorCode.NoError,
                                    Partition = 0,
                                    HighWatermarkOffset = 432515L,
                                    Messages = new List<ResponseMessage>
                                    {
                                        new ResponseMessage {Offset = OFFSET, Message = new Message()},
                                        new ResponseMessage {Offset = OFFSET + 1, Message = new Message()},
                                        new ResponseMessage {Offset = OFFSET + 2, Message = new Message()},
                                    }
                                },
                                new FetchPartitionResponse
                                {
                                    ErrorCode = ErrorCode.NoError,
                                    Partition = 1,
                                    HighWatermarkOffset = 432515L,
                                    Messages = new List<ResponseMessage>
                                    {
                                        new ResponseMessage {Offset = OFFSET, Message = new Message()},
                                        new ResponseMessage {Offset = OFFSET + 1, Message = new Message()},
                                        new ResponseMessage {Offset = OFFSET + 2, Message = new Message()},
                                    }
                                }
                            }
                        }
                    }
                },
                ReceivedDate = DateTime.UtcNow
            });

            // Check
            node.Verify(n => n.Fetch(It.IsAny<FetchMessage>()), Times.Exactly(2));
        }

        [Test]
        public void TestOffsetResponseIsFollowedByFetchRequest_NoError()
        {
            var node = new Mock<INode>();
            var cluster = new Mock<ICluster>();
            cluster.Setup(c => c.RequireNewRoutingTable())
                .Returns(() =>
                    Task.FromResult(
                        new RoutingTable(new Dictionary<string, Partition[]>
                        {
                            {
                                TOPIC,
                                new[]
                                {
                                    new Partition {Id = PARTITION, Leader = node.Object},
                                    new Partition {Id = PARTITION + 1, Leader = node.Object},
                                }
                            },
                            {
                                TOPIC2,
                                new[]
                                {
                                    new Partition {Id = PARTITION, Leader = node.Object},
                                }
                            }
                        })));
            var configuration = new Configuration {TaskScheduler = new CurrentThreadTaskScheduler()};
            var consumer = new ConsumeRouter(cluster.Object, configuration, 1);
            consumer.StartConsume(TOPIC, PARTITION, Offsets.Earliest);
            consumer.StartConsume(TOPIC, PARTITION + 1, Offsets.Earliest);
            consumer.StartConsume(TOPIC2, PARTITION, Offsets.Earliest);
            consumer.Acknowledge(new CommonAcknowledgement<OffsetPartitionResponse>
            {
                Response = new CommonResponse<OffsetPartitionResponse>
                {
                    TopicsResponse = new[]
                    {
                        new TopicData<OffsetPartitionResponse>
                        {
                            TopicName = TOPIC,
                            PartitionsData = new[]
                            {
                                new OffsetPartitionResponse
                                {
                                    ErrorCode = ErrorCode.NoError,
                                    Partition = PARTITION,
                                    Offsets = new[] {OFFSET}
                                },
                                new OffsetPartitionResponse
                                {
                                    ErrorCode = ErrorCode.NoError,
                                    Partition = PARTITION + 1,
                                    Offsets = new[] {OFFSET}
                                }
                            }
                        },
                        new TopicData<OffsetPartitionResponse>
                        {
                            TopicName = TOPIC2,
                            PartitionsData = new[]
                            {
                                new OffsetPartitionResponse
                                {
                                    ErrorCode = ErrorCode.NoError,
                                    Partition = PARTITION,
                                    Offsets = new[] {OFFSET}
                                },
                            }
                        }
                    }
                },
                ReceivedDate = DateTime.UtcNow
            });
            node.Verify(n => n.Fetch(It.IsAny<FetchMessage>()), Times.Exactly(3));
            node.Verify(n => n.Fetch(It.Is<FetchMessage>(fm => fm.Offset == OFFSET)), Times.Exactly(3));
            node.Verify(n => n.Fetch(It.Is<FetchMessage>(fm => fm.Topic == TOPIC)), Times.Exactly(2));
            node.Verify(n => n.Fetch(It.Is<FetchMessage>(fm => fm.Topic == TOPIC2)), Times.Exactly(1));
        }

        [Test]
        public void TestFetchResponseIsFollowedByFetchRequest_NoError()
        {
            var node = new Mock<INode>();
            var cluster = new Mock<ICluster>();
            cluster.Setup(c => c.RequireNewRoutingTable())
                .Returns(() =>
                    Task.FromResult(
                        new RoutingTable(new Dictionary<string, Partition[]>
                        {
                            {
                                TOPIC,
                                new[]
                                {
                                    new Partition {Id = PARTITION, Leader = node.Object},
                                    new Partition {Id = PARTITION + 1, Leader = node.Object},
                                }
                            },
                            {
                                TOPIC2,
                                new[]
                                {
                                    new Partition {Id = PARTITION, Leader = node.Object},
                                }
                            }
                        })));
            var configuration = new Configuration {TaskScheduler = new CurrentThreadTaskScheduler()};
            var consumer = new ConsumeRouter(cluster.Object, configuration, 1);
            consumer.StartConsume(TOPIC, PARTITION, Offsets.Earliest);
            consumer.StartConsume(TOPIC, PARTITION + 1, Offsets.Earliest);
            consumer.StartConsume(TOPIC2, PARTITION, Offsets.Earliest);
            consumer.Acknowledge(new CommonAcknowledgement<FetchPartitionResponse>
            {
                Response = new CommonResponse<FetchPartitionResponse>
                {
                    TopicsResponse = new[]
                    {
                        new TopicData<FetchPartitionResponse>
                        {
                            TopicName = TOPIC,
                            PartitionsData = new[]
                            {
                                new FetchPartitionResponse
                                {
                                    ErrorCode = ErrorCode.NoError,
                                    Partition = PARTITION,
                                    HighWatermarkOffset = 432515L,
                                    Messages = new List<ResponseMessage>
                                    {
                                        new ResponseMessage {Offset = OFFSET, Message = new Message()},
                                        new ResponseMessage {Offset = OFFSET + 1, Message = new Message()},
                                    }
                                },
                                new FetchPartitionResponse
                                {
                                    ErrorCode = ErrorCode.NoError,
                                    Partition = PARTITION + 1,
                                    HighWatermarkOffset = 432515L,
                                    Messages = new List<ResponseMessage>
                                    {
                                        new ResponseMessage {Offset = OFFSET, Message = new Message()},
                                    }
                                },
                            }
                        },
                        new TopicData<FetchPartitionResponse>
                        {
                            TopicName = TOPIC2,
                            PartitionsData = new[]
                            {
                                new FetchPartitionResponse
                                {
                                    ErrorCode = ErrorCode.NoError,
                                    Partition = PARTITION,
                                    HighWatermarkOffset = 432515L,
                                    Messages = new List<ResponseMessage>
                                    {
                                        new ResponseMessage {Offset = OFFSET, Message = new Message()},
                                        new ResponseMessage {Offset = OFFSET + 1, Message = new Message()},
                                        new ResponseMessage {Offset = OFFSET + 2, Message = new Message()},
                                    }
                                },
                            }
                        }
                    }
                },
                ReceivedDate = DateTime.UtcNow
            });
            node.Verify(n => n.Fetch(It.IsAny<FetchMessage>()), Times.Exactly(3));
            node.Verify(n => n.Fetch(It.Is<FetchMessage>(fm => fm.Topic == TOPIC)), Times.Exactly(2));
            node.Verify(
                n =>
                    n.Fetch(
                        It.Is<FetchMessage>(
                            fm => fm.Topic == TOPIC && fm.Partition == PARTITION && fm.Offset == OFFSET + 2)),
                Times.Once());
            node.Verify(
                n =>
                    n.Fetch(
                        It.Is<FetchMessage>(
                            fm => fm.Topic == TOPIC && fm.Partition == PARTITION + 1 && fm.Offset == OFFSET + 1)),
                Times.Once());
            node.Verify(
                n =>
                    n.Fetch(
                        It.Is<FetchMessage>(
                            fm => fm.Topic == TOPIC2 && fm.Offset == OFFSET + 3 && fm.Partition == PARTITION)),
                Times.Exactly(1));
        }

        [Test]
        public void TestMessagesAreFilteredAndPropagated()
        {
            var node = new Mock<INode>();
            var cluster = new Mock<ICluster>();
            cluster.Setup(c => c.RequireNewRoutingTable())
                .Returns(() =>
                    Task.FromResult(
                        new RoutingTable(new Dictionary<string, Partition[]>
                        {
                            {
                                TOPIC,
                                new[]
                                {
                                    new Partition {Id = PARTITION, Leader = node.Object},
                                }
                            }
                        })));
            var configuration = new Configuration {TaskScheduler = new CurrentThreadTaskScheduler()};
            var consumer = new ConsumeRouter(cluster.Object, configuration, 1);
            consumer.StartConsume(TOPIC, PARTITION, OFFSET);
            consumer.StopConsume(TOPIC, PARTITION, OFFSET + 1);
            int messageReceivedRaised = 0;
            consumer.MessageReceived += kr =>
            {
                Assert.AreEqual(TOPIC, kr.Topic);
                Assert.AreEqual(PARTITION, kr.Partition);
                Assert.IsTrue(kr.Offset == OFFSET || kr.Offset == OFFSET + 1);
                ++messageReceivedRaised;
            };
            consumer.Acknowledge(new CommonAcknowledgement<FetchPartitionResponse>
            {
                Response = new CommonResponse<FetchPartitionResponse>
                {
                    TopicsResponse = new[]
                    {
                        new TopicData<FetchPartitionResponse>
                        {
                            TopicName = TOPIC,
                            PartitionsData = new[]
                            {
                                new FetchPartitionResponse
                                {
                                    ErrorCode = ErrorCode.NoError,
                                    Partition = PARTITION,
                                    HighWatermarkOffset = 432515L,
                                    Messages = new List<ResponseMessage>
                                    {
                                        new ResponseMessage {Offset = OFFSET - 1, Message = new Message()}, // Will be filtered
                                        new ResponseMessage {Offset = OFFSET, Message = new Message()},
                                        new ResponseMessage {Offset = OFFSET + 1, Message = new Message()},
                                        new ResponseMessage {Offset = OFFSET + 2, Message = new Message()},
                                    }
                                }
                            }
                        }
                    }
                },
                ReceivedDate = DateTime.UtcNow
            });
            Assert.AreEqual(2, messageReceivedRaised);
        }

        [Test]
        public void TestPostponeIfNoRoute()
        {
            var cluster = new Mock<ICluster>();
            cluster.SetupGet(c => c.Logger).Returns(new DevNullLogger());
            cluster.Setup(c => c.RequireNewRoutingTable())
                .Returns(() =>
                    Task.FromResult(
                        new RoutingTable(new Dictionary<string, Partition[]>
                        {
                            {
                                TOPIC,
                                new[]
                                {
                                    new Partition {Id = PARTITION + 1},
                                }
                            }
                        })));
            var configuration = new Configuration { TaskScheduler = new CurrentThreadTaskScheduler() };
            var consumer = new ConsumeRouter(cluster.Object, configuration, 1);
            int postponedRaised = 0;
            consumer.FetchPostponed += (t, p) =>
            {
                Assert.AreEqual(TOPIC, t);
                Assert.AreEqual(PARTITION, p);
                ++postponedRaised;
            };
            consumer.StartConsume(TOPIC, PARTITION, Offsets.Earliest);

            Assert.AreEqual(1, postponedRaised);
        }
    }
}
