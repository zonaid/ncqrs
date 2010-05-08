﻿using System;
using FluentAssertions;
using Ncqrs.Eventing;
using Ncqrs.Eventing.Conversion;
using NUnit.Framework;
using Ncqrs.Domain.Storage;
using Rhino.Mocks;
using Ncqrs.Eventing.Storage;
using Ncqrs.Eventing.ServiceModel.Bus;
using Ncqrs.Domain;
using System.Collections.Generic;
using Ncqrs.Domain.Mapping;

namespace Ncqrs.Tests.Domain.Storage
{
    public class DomainRepositoryTests
    {
        public class FooEvent : DomainEvent
        { }

        public class BarEvent : DomainEvent
        {
            public BarEvent()
            {
            }

            public BarEvent(Guid eventIdentifier, Guid aggregateRootId, long eventSequence, DateTime eventTimeStamp) : base(eventIdentifier, aggregateRootId, eventSequence, eventTimeStamp)
            {
            }
        }

        public class BarEvent_v2 : DomainEvent
        {
            public BarEvent_v2()
            {
            }

            public BarEvent_v2(Guid eventIdentifier, Guid aggregateRootId, long eventSequence, DateTime eventTimeStamp)
                : base(eventIdentifier, aggregateRootId, eventSequence, eventTimeStamp)
            {
            }
        }

        public class MyAggregateRoot : AggregateRootMappedWithAttributes
        {
            public void Foo()
            {
                var e = new FooEvent();
                ApplyEvent(e);
            }

            public void Bar()
            {
                var e = new BarEvent();
                ApplyEvent(e);
            }

            [EventHandlerAttribute]
            private void CatchAllHandler(DomainEvent e)
            {}
        }

        [Test]
        public void It_should_call_the_converter_for_each_event()
        {
            var store = MockRepository.GenerateMock<IEventStore>();
            var bus = MockRepository.GenerateMock<IEventBus>();
            var loader = MockRepository.GenerateMock<IAggregateRootLoader>();
            
            var fooConverter = MockRepository.GenerateMock<IEventConverter>();
            var barConverter = MockRepository.GenerateMock<IEventConverter>();

            var converter = new EventConverter()
                .AddConverter(typeof(FooEvent), fooConverter)
                .AddConverter(typeof(BarEvent), barConverter);

            var aggId = Guid.NewGuid();
            var eventsInTheStore = new DomainEvent[] { new FooEvent(), new BarEvent() };
            store.Expect(s => s.GetAllEventsForEventSource(aggId)).Return(eventsInTheStore);
            fooConverter.Expect(c => c.Convert(null)).IgnoreArguments().Return(eventsInTheStore[0]);
            barConverter.Expect(c => c.Convert(null)).IgnoreArguments().Return(eventsInTheStore[1]);

            var repository = new DomainRepository(store, bus, converter, loader);
            repository.GetById<MyAggregateRoot>(aggId);

            fooConverter.VerifyAllExpectations();
            barConverter.VerifyAllExpectations();
        }

        [Test]
        public void When_a_aggregate_root_is_requested_by_id_it_should__get_the_event_from_the_store_and_load_the_aggregate_with_the_loader_with_the_events_from_the_store()
        {
            var store = MockRepository.GenerateMock<IEventStore>();
            var bus = MockRepository.GenerateMock<IEventBus>();
            var loader = MockRepository.GenerateMock<IAggregateRootLoader>();

            var aggId = Guid.NewGuid();
            var eventsInTheStore = new DomainEvent[] { new FooEvent(), new BarEvent() };
            var loadedAggregate = new MyAggregateRoot();

            store.Expect(s => s.GetAllEventsForEventSource(aggId)).Return(eventsInTheStore);
            loader.Expect(l => l.LoadAggregateRootFromEvents(typeof (MyAggregateRoot), eventsInTheStore)).Return(loadedAggregate);

            var repository = new DomainRepository(store, bus, null, loader);
            var result = repository.GetById<MyAggregateRoot>(aggId);

            store.VerifyAllExpectations();
            loader.VerifyAllExpectations();
            result.Should().Be(loadedAggregate);
        }

        [Test]
        public void Save_test()
        {
            using (var work = NcqrsEnvironment.Get<IUnitOfWorkFactory>().CreateUnitOfWork())
            {
                var store = MockRepository.GenerateMock<IEventStore>();
                var bus = MockRepository.GenerateMock<IEventBus>();
                var aggregate = new MyAggregateRoot();

                aggregate.Foo();
                aggregate.Bar();

                store.Expect(s => s.Save(aggregate));
                bus.Expect(b => b.Publish((IEnumerable<IEvent>) null)).IgnoreArguments();

                var repository = new DomainRepository(store, bus);
                repository.Save(aggregate);

                bus.VerifyAllExpectations();
                store.VerifyAllExpectations();
            }
        }
    }
}