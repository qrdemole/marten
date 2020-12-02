using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baseline;
using Baseline.Dates;
using Marten.Events;
using Marten.Events.Projections;
using Marten.Events.Projections.Async;
using Marten.Storage;
using Xunit;
using Xunit.Abstractions;

namespace Marten.Schema.Testing.ProjectionsGetStuckReproduction
{
    public class ProjectionsGetStuckReproductionTests: IntegrationContext
    {
        //Leave null for default settings (number of threads initially set to number of cpu cores)
        private readonly int? ConfigureThreadPoolParallelThreads = null;
        public static TimeSpan SimulateWorkDelay = TimeSpan.FromSeconds(0);
        private readonly TimeSpan MaxWaitTimeForNextEventToBeProcessed = TimeSpan.FromMinutes(2);
        private const int NumberOfTestEvents = 100;
        private readonly Guid[] _streamIds = Enumerable.Range(0, 10).Select(i => Guid.NewGuid()).ToArray();
        private IDaemon _daemon;

        private readonly TestProjectionBase[] _testProjections;
        private Thread _separateWorkLoadThread;

        public ProjectionsGetStuckReproductionTests(ITestOutputHelper output = null): base(output)
        {
            _testProjections = new TestProjectionBase[] {
                new TestProjection<T1>(SimulateWorkDelay, _output), new TestProjection<T2>(SimulateWorkDelay, _output), new TestProjection<T3>(SimulateWorkDelay, _output), new TestProjection<T4>(SimulateWorkDelay, _output),
                new TestProjection<T5>(SimulateWorkDelay, _output), new TestProjection<T6>(SimulateWorkDelay, _output), new TestProjection<T7>(SimulateWorkDelay, _output), new TestProjection<T8>(SimulateWorkDelay, _output),
                new TestProjection<T9>(SimulateWorkDelay, _output), new TestProjection<T10>(SimulateWorkDelay, _output)
            };

            StoreOptions(opts =>
            {
                opts.Events.AddEventTypes(new[] {typeof(TestEvent)});
                _testProjections.Each(p => opts.Events.AsyncProjections.Add(p));
            });

            if (ConfigureThreadPoolParallelThreads != null)
            {
                //try to set thread pool for faster tests execution (it doesn't affect the tests result - only the running time, but by default is disabled)
                int minWorker, minIOC;
                ThreadPool.GetMinThreads(out minWorker, out minIOC);
                ThreadPool.SetMinThreads(ConfigureThreadPoolParallelThreads.Value, minIOC);
            }
        }

        private void SimulateSeparateWorkLoad(IDocumentSession session)
        {
            _separateWorkLoadThread = new Thread(() =>
            {
                try
                {
                    for (var i = 0; i < NumberOfTestEvents; i++)
                    {
                        var testResult =
                            session.Query<ReadModelTestData>()
                                .Where(r => r.P1 == Guid.NewGuid().ToString())
                                .Where(r => r.P2 == Guid.NewGuid().ToString())
                                .Where(r => r.P3 == Guid.NewGuid().ToString())
                                .Where(r => r.P4 == Guid.NewGuid().ToString())
                                .Where(r => r.P5 == Guid.NewGuid().ToString())
                                .Where(r => r.P6 == Guid.NewGuid().ToString())
                                .Where(r => r.P7 == Guid.NewGuid().ToString())
                                .Where(r => r.P8 == Guid.NewGuid().ToString()).ToArray();
                    }
                    _output.WriteLine("FINISHED SIMULATING SEPARATE WORK LOAD");
                }
                catch (Exception e)
                {
                    _output.WriteLine($"!!!!!!!! ERROR IN SEPARATE WORKLOAD (THIS IS NOT EXPECTED BUT NOT A SUBJECT OF THE TEST): {e.Message}");
                }
            });
            _separateWorkLoadThread.Start();
        }

        [Fact]
        //This was supposed to fail... it fails sometimes but not for the reasons I wanted to show case
        public void AllEventsShouldBeProcessed()
        {
            StartProjections();

            SimulateSeparateWorkLoad(theSession);

            for (var i = 0; i < NumberOfTestEvents; i++)
            {
                AppendEvents(_streamIds[i % 10], new[] {new TestEvent {Message = i}}).ConfigureAwait(false).GetAwaiter().GetResult();


                if (i%1000 == 0)
                    _output.WriteLine($"Added events: {i}, All projections processed events count: {_testProjections.Select(p => p.GetEventsCounter()).Sum()}");
            }

            SimulateSeparateWorkLoad(theSession);

            for (var i = 0; i < NumberOfTestEvents; i++)
            {
                AppendEvents(_streamIds[i % 10], new[] {new TestEvent {Message = i}}).ConfigureAwait(false).GetAwaiter().GetResult();


                if (i%1000 == 0)
                    _output.WriteLine($"Added events: {i}, All projections processed events count: {_testProjections.Select(p => p.GetEventsCounter()).Sum()}");
            }

            _output.WriteLine("!!!!!!!!!!!!!!!!!!!!!! ADDING EVENTS DONE !!!!!!!!!!!!!!!!!!!!!!!!");

            var currentEventsCounter = _testProjections.Select(p => p.GetEventsCounter()).Sum();
            var lastEventsCounter = currentEventsCounter;
            var isProcessingStuck = false;
            var lastChangeObservedAt = DateTime.Now;

            while (lastEventsCounter < NumberOfTestEvents * _testProjections.Length * 2 && !isProcessingStuck)
            {
                currentEventsCounter = _testProjections.Select(p => p.GetEventsCounter()).Sum();

                isProcessingStuck = IsProcessingStuck(lastEventsCounter, currentEventsCounter, ref lastChangeObservedAt);

                _output.WriteLine($"Last change observed at {lastChangeObservedAt}, Elapsed: {DateTime.Now - lastChangeObservedAt}, Current number of processed events: {currentEventsCounter}, Previous: {lastEventsCounter}");

                lastEventsCounter = currentEventsCounter;

                Thread.Sleep(TimeSpan.FromSeconds(5));
            }

            Assert.Equal(NumberOfTestEvents * _testProjections.Length * 2, lastEventsCounter);
        }

        [Fact]
        //This test should show that expected number of events is processed when skipping the entire event store
        //It fails right now due to some issues with the session
        public void AllEventsProcessedWhenProjectionsCalledManually()
        {
            var addingEventTasks = new List<Task>();
            for (var i = 0; i < NumberOfTestEvents; i++)
            {
                addingEventTasks.Add(FeedAllProjectionsManually(new[] {new TestEvent {Message = i}}));
            }

            Task.WaitAll(addingEventTasks.ToArray());

            var lastEventsCounter = _testProjections.Select(p => p.GetEventsCounter()).Sum();

            //We don't need to wait for the events to be processed since there is no queue (event store) involved
            Assert.Equal(NumberOfTestEvents * _testProjections.Length, lastEventsCounter);
        }

        private async Task FeedAllProjectionsManually(TestEvent[] testEvents)
        {
            var cancellationToken = new CancellationToken();
            var eventsList = testEvents.Select(eventData => new Event<TestEvent>(eventData)).ToList();

            foreach (var testProjection in _testProjections)
            {
                //the session, from and too in the event page are not important for the test case
                await testProjection.ApplyAsync(theSession, new EventPage(0, 100, eventsList), cancellationToken);
            }
        }

        private void StartProjections()
        {
            var settings = new DaemonSettings {LeadingEdgeBuffer = 0.Seconds()};

            _daemon = theStore.BuildProjectionDaemon(
                logger: new DebugDaemonLogger(),
                settings: settings
            );
            _daemon.StartAll();
        }

        private bool IsProcessingStuck(in int lastEventsCounter, in int currentEventsCounter,
            ref DateTime lastChangeObservedAt)
        {
            var sinceLastObservation = DateTime.Now - lastChangeObservedAt;
            if (lastEventsCounter != currentEventsCounter)
                lastChangeObservedAt = DateTime.Now;

            return lastEventsCounter == currentEventsCounter &&
                   (sinceLastObservation > MaxWaitTimeForNextEventToBeProcessed);
        }

        private async Task AppendEvents(Guid streamId, IEnumerable<object> events)
        {
            using (var session = theStore.LightweightSession())
            {
                session.Events.Append(streamId, events.ToArray());
                await session.SaveChangesAsync();
            }
        }
    }

    public abstract class TestProjectionBase: IProjection
    {
        protected readonly TimeSpan _simulateWorkDelay;
        protected readonly ITestOutputHelper _testOutputHelper;

        public static Random Random = new Random();
        public Type[] Consumes => new[] {typeof(TestEvent)};

        public AsyncOptions AsyncOptions => new AsyncOptions();

        public TestProjectionBase(TimeSpan simulateWorkDelay, ITestOutputHelper testOutputHelper)
        {
            _simulateWorkDelay = simulateWorkDelay;
            _testOutputHelper = testOutputHelper;
        }

        public void Apply(IDocumentSession session, EventPage page) => throw new NotImplementedException("Synchronous version is not used");

        public async Task ApplyAsync(IDocumentSession session, EventPage page, CancellationToken token)
        {
            try
            {
                await SimulateWork(session, page, token);
            }
            catch (Exception e)
            {
                _testOutputHelper.WriteLine($"!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! ERROR in projection apply {e.Message}");
                throw;
            }
            
        }

        protected abstract Task SimulateWork(IDocumentSession session, EventPage page, CancellationToken token);
        public abstract int GetEventsCounter();

        public void EnsureStorageExists(ITenant tenant)
        {
        }
    }

    public class TestProjection<T>: TestProjectionBase
    {
        private readonly object LastMessageSync = new object();
        private int _eventsCounter;
        protected readonly ConcurrentDictionary<Guid, int> _storedIds = new ConcurrentDictionary<Guid, int>();

        public TestProjection(TimeSpan simulateWorkDelay, ITestOutputHelper testOutputHelper): base(simulateWorkDelay,
            testOutputHelper)
        {
        }

        public override int GetEventsCounter()
        {
            lock (LastMessageSync)
            {
                return _eventsCounter;
            }
        }

        protected override async Task SimulateWork(IDocumentSession session, EventPage page, CancellationToken token)
        {
            await Task.Run(() =>
            {
                Task.Delay(_simulateWorkDelay).ConfigureAwait(false).GetAwaiter().GetResult();
                foreach (var e in page.Events)
                {
                    //To simulate some DB load
                    TryToUpdateOldMessage(e, session);

                    var messageId = ((TestEvent)e.Data).Message;

                    if (!_storedIds.TryAdd(e.Id, messageId))
                    {
                        _testOutputHelper.WriteLine($"Duplicated event with id {e.Id}, message id:{messageId} skipped");
                        continue;
                    }


                    var t = session.Query<ReadModelTestData>().FirstOrDefault(r => r.MessageId == messageId);
                    if (t != null)
                    {
                        t.P10 = Guid.NewGuid().ToString();
                        session.Store(t);
                    }
                    else
                    {
                        var r = CreateReadModel(e);
                        session.Store(r);
                    }

                    lock (LastMessageSync)
                    {
                        _eventsCounter++;
                    }
                }
            }, token);
        }

        private void TryToUpdateOldMessage(IEvent @event, IDocumentSession session)
        {
            var td = (TestEvent)@event.Data;
            var candidateToUpdateId = td.Message - 200;

            if (candidateToUpdateId > 0)
            {
                var r = session.Query<ReadModelTestData>().FirstOrDefault(r => r.MessageId == candidateToUpdateId);
                if (r != null)
                {
                    r.P11 = Guid.NewGuid().ToString();
                    session.Store(r);
                }
            }
        }

        private ReadModelTestData CreateReadModel(IEvent @event)
        {
            return new ReadModelTestData
            {
                MessageId =  ((TestEvent)@event.Data).Message,
                Id = Guid.NewGuid(),
                CreatedByEventId = @event.Id,
                P1 = Guid.NewGuid().ToString(),
                P2 = Guid.NewGuid().ToString(),
                P3 = Guid.NewGuid().ToString(),
                P4 = Guid.NewGuid().ToString(),
                P5 = Guid.NewGuid().ToString(),
                P6 = Guid.NewGuid().ToString(),
                P7 = Guid.NewGuid().ToString(),
                P8 = Guid.NewGuid().ToString(),
                P9 = Guid.NewGuid().ToString(),
                P10 = Guid.NewGuid().ToString(),
                P11 = Guid.NewGuid().ToString(),
                P12 = Guid.NewGuid().ToString(),
                P13 = Guid.NewGuid().ToString(),
                P14 = Guid.NewGuid().ToString(),
                P15 = Guid.NewGuid().ToString(),
                P16 = Guid.NewGuid().ToString(),
                ListOfProperties = Enumerable.Range(1, 30).Select(i => Guid.NewGuid().ToString()).ToList()
            };
        }

        public new AsyncOptions AsyncOptions
        {
            get
            {
                if (typeof(T) == typeof(T1))
                    return new AsyncOptions() {PageSize = 1};
                return new AsyncOptions();
            }
        }
    }

    public class T1 { }
    public class T2 { }
    public class T3 { }
    public class T4 { }
    public class T5 { }
    public class T6 { }
    public class T7 { }
    public class T8 { }
    public class T9 { }
    public class T10 { }


    public class TestEvent
    {
        public int Message { get; set; }
    }
}
