using System;
using System.Collections.Generic;
using System.Linq;
using NServiceBus;
using Raven.Client;

namespace SmsTracking
{
    public class CoordinatorTracker : 
        IHandleMessages<CoordinatorCreated>
    {
        public IDocumentStore DocumentStore { get; set; }

        public void Handle(CoordinatorCreated message)
        {
            using(var session = DocumentStore.OpenSession())
            {
                var coordinatorTrackingData = new CoordinatorTrackingData();
                coordinatorTrackingData.CoordinatorId = message.CoordinatorId;
                coordinatorTrackingData.MessageStatuses = message
                    .ScheduledMessages
                    .Select(s => new MessageSendingStatus { Number = s.Number, ScheduledSendingTime = s.ScheduledTime })
                    .ToList();
                session.Store(coordinatorTrackingData, message.CoordinatorId.ToString());
                session.SaveChanges();
            }
        }
    }

    public class CoordinatorCreated
    {
        public Guid CoordinatorId { get; set; }

        public List<MessageSchedule> ScheduledMessages { get; set; }
    }

    public class MessageSchedule
    {
        public string Number { get; set; }

        public DateTime ScheduledTime { get; set; }
    }

    public class CoordinatorTrackingData
    {
        public Guid CoordinatorId { get; set; }

        public CoordinatorStatusTracking CurrentStatus { get; set; }

        public List<MessageSendingStatus> MessageStatuses { get; set; }

        public DateTime? CompletionDate { get; set; }
    }

    public class MessageSendingStatus
    {
        public string Number { get; set; }

        public DateTime ScheduledSendingTime { get; set; }

        public MessageStatusTracking Status { get; set; }

        public Decimal? Cost { get; set; }

        public DateTime? ActualSentTime { get; set; }
    }

    public enum MessageStatusTracking
    {
        Scheduled,
        Paused,
        Completed
    }

    public enum CoordinatorStatusTracking
    {
        Started,
        Paused,
        Completed
    }
}